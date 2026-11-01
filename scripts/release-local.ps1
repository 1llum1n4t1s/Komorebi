# release-local.ps1 — ローカル署名付き Velopack リリース (Windows チャンネル専用)
#
# SimplySign (Certum クラウド署名) は Desktop 接続 + スマホトークンが必要で
# GitHub Actions からは署名できないため、Windows 向けリリースは本スクリプトでローカル実行する。
#
# ⚠️ Komorebi はクロスプラットフォーム配信 (win-x64 / win-arm64 / osx-arm64 / linux-x64 / linux-arm64)。
#    本スクリプトが扱うのは Authenticode 署名対象の Windows 2 チャンネルのみ。
#    macOS / Linux チャンネルと standalone パッケージ (zip / deb / rpm / AppImage) は引き続き
#    .github/workflows/release.yml (release/** push) が配信する。
#    クリーンアップは「Windows 以外のチャンネルの manifest を R2 から取得して保護」するため、
#    他チャンネルの nupkg を誤削除しない。
#
# 前提:
#   - SimplySign Desktop が接続済み (証明書が CurrentUser\My に見えていること)
#   - Directory.Build.props の <Version> がリリースしたいバージョンになっていること (/vava 済み)
#   - D:\Dev\Secret\secrets.json に cloudflare.api_token があること
#
# 使い方:
#   pwsh scripts/release-local.ps1                # フルリリース (build + sign + upload + cleanup)
#   pwsh scripts/release-local.ps1 -SkipUpload    # ビルド + 署名のみ (アップロードしない動作確認用)
#   pwsh scripts/release-local.ps1 -Runtimes win-x64   # 対象 RID を絞る (テスト用)

[CmdletBinding()]
param(
    [switch]$SkipUpload,
    [string[]]$Runtimes = @('win-x64', 'win-arm64')
)




$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# ---- 定数 ----
# Velopack (vpk) は常に最新安定版を使う (ゆろ君ルール): NuGet から実行時に最新を解決して pin する
$VpkVersion = (Invoke-RestMethod 'https://api.nuget.org/v3-flatcontainer/vpk/index.json' -TimeoutSec 30).versions |
    Where-Object { $_ -notmatch '-' } | Select-Object -Last 1
if (-not $VpkVersion) { throw 'vpk の最新安定版バージョンの取得に失敗しました (NuGet API)' }
Write-Host "vpk 最新安定版: $VpkVersion"
$WranglerVersion = '4.156.0'         # サプライチェーン対策でバージョン固定
$Bucket = 'komorebi-updates'
$BaseUrl = 'https://komorebi.kagayoi.com'
$AccountId = '10901bfadbf1005164774a7350082985'
$SecretsPath = 'D:\Dev\Secret\secrets.json'
$CertSubjectName = 'Open Source Developer Yuichiro Shinozaki'
# /n (Subject 名) で選択: 証明書の年次更新で thumbprint が変わっても動く
$SignParams = "/n `"$CertSubjectName`" /fd SHA256 /td SHA256 /tr http://time.certum.pl"

# Komorebi はチャンネル名 = RID (Lhamiel の 'win' とは異なり win-x64 もフル表記)
$RuntimeMatrix = @{
    'win-x64'   = @{ Channel = 'win-x64' }
    'win-arm64' = @{ Channel = 'win-arm64' }
}
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot
if ($Runtimes.Count -eq 0 -or @($Runtimes | Select-Object -Unique).Count -ne $Runtimes.Count) { throw 'RID は重複なしで 1 個以上指定してください' }
foreach ($runtime in $Runtimes) {
    if (-not $RuntimeMatrix.ContainsKey($runtime)) { throw "未知の runtime: $runtime" }
}
$WorkDir = Join-Path $RepoRoot 'local-release'
$ArtifactsDir = Join-Path $WorkDir 'artifacts'

# 作業先をリポジトリ直下に固定する。削除は CMD から Prunlet を直接実行する。
$WorkDir = [System.IO.Path]::GetFullPath($WorkDir)
if ([System.IO.Path]::GetDirectoryName($WorkDir) -ne [System.IO.Path]::GetFullPath($RepoRoot)) {
    throw 'リリース作業ディレクトリがリポジトリの範囲外です'
}
if (Test-Path -LiteralPath $WorkDir) {
    $linked = @(Get-Item -LiteralPath $WorkDir -Force) + @(Get-ChildItem -LiteralPath $WorkDir -Recurse -Force)
    if ($linked | Where-Object { $_.Attributes -band [System.IO.FileAttributes]::ReparsePoint }) {
        throw 'リリース作業ディレクトリにリンクがあります。清掃範囲を確認してください'
    }
    throw "既存の作業領域があります。保持内容を確認し、CMD から Prunlet で清掃してから再実行してください: $WorkDir"
}

function Invoke-Native {
    param([string]$Description, [scriptblock]$Block)
    & $Block
    if ($LASTEXITCODE -ne 0) { throw "$Description が失敗しました (exit $LASTEXITCODE)" }
}

# ---- 0. プリフライト ----
Write-Host '== プリフライト ==' -ForegroundColor Cyan

# Git Bash (MSYS) 経由で起動すると括弧入り環境変数が落ちて、Native AOT の
# リンク段 (Microsoft.NETCore.Native.targets) の vswhere.exe 解決が壊れるため補完する
if (-not ${env:ProgramFiles(x86)}) { ${env:ProgramFiles(x86)} = 'C:\Program Files (x86)' }

# 一部の非対話シェルでは Windows 標準の OS 環境変数も欠落する。
# Native AOT は OS=Windows_NT でホスト OS を判定するため、未設定時だけ補完する。
if (-not $env:OS) { $env:OS = 'Windows_NT' }

# VS 2026 の vcvarsall は PATH 上の vswhere.exe を呼ぶ (GitHub ランナーは PATH 済み)。
# ローカルでは VS Installer ディレクトリが PATH に無いので AOT リンクが落ちる → 追加
$vsInstallerDir = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
if ($env:PATH -notlike "*$vsInstallerDir*") { $env:PATH = "$env:PATH;$vsInstallerDir" }

# vpk (dotnet tool) は古い .NET ランタイム要求のことがある → 最新メジャーにロールフォワード
$env:DOTNET_ROLL_FORWARD = 'Major'

# XPath で取得 (member enumeration は Version を持たない PropertyGroup 混在時に StrictMode で throw する)
$versionNode = ([xml](Get-Content 'Directory.Build.props' -Raw)).SelectSingleNode('/Project/PropertyGroup/Version')
$version = if ($versionNode) { $versionNode.InnerText.Trim() } else { $null }
if (-not $version) { throw 'Directory.Build.props から <Version> を取得できませんでした' }
Write-Host "バージョン: $version"

# SimplySign 接続確認 (証明書が見えなければ署名できないので最初に落とす)
$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -like "CN=$CertSubjectName*" -and $_.NotAfter -gt (Get-Date) }
if (-not $cert) {
    throw "署名証明書 (CN=$CertSubjectName) が見つかりません。SimplySign Desktop を起動してトークンでログインしてください。"
}
Write-Host "署名証明書: $($cert.Subject) (期限 $($cert.NotAfter.ToString('yyyy-MM-dd')))"

# Cloudflare トークン (アップロード時のみ必要)
if (-not $SkipUpload) {
    $secrets = Get-Content $SecretsPath -Raw | ConvertFrom-Json
    if (-not $secrets.cloudflare.api_token) { throw "secrets.json に cloudflare.api_token が見つかりません" }
    $env:CLOUDFLARE_API_TOKEN = $secrets.cloudflare.api_token
    $env:CLOUDFLARE_ACCOUNT_ID = $AccountId
}

New-Item -ItemType Directory -Path $ArtifactsDir -Force | Out-Null
$toolDir = Join-Path $WorkDir 'tools'
Invoke-Native 'vpk のローカルインストール' { dotnet tool install vpk --tool-path $toolDir --version $VpkVersion }
$vpk = Join-Path $toolDir 'vpk.exe'
$sourceSHA = (git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceSHA -notmatch '^[a-f0-9]{40}$') { throw 'リリース元 SHA を取得できませんでした' }

# ---- 1. ビルド + 署名付きパッケージング (RID ごと) ----
foreach ($runtime in $Runtimes) {
    $config = $RuntimeMatrix[$runtime]
    if (-not $config) { throw "未知の runtime: $runtime (本スクリプトは Windows チャンネル専用)" }
    $publishDir = Join-Path $WorkDir "publish-$runtime"

    Write-Host "== publish: $runtime ==" -ForegroundColor Cyan
    Invoke-Native "dotnet publish ($runtime)" {
        dotnet publish src/Komorebi.csproj -c Release -r $runtime -o $publishDir -p:RestoreLockedMode=true -p:UseSharedCompilation=false -nodeReuse:false
    }

    if (-not (Test-Path (Join-Path $publishDir 'Komorebi.exe'))) {
        throw "Komorebi.exe が publish 出力にありません ($runtime)"
    }

    Write-Host "== vpk pack + 署名: $runtime ==" -ForegroundColor Cyan
    # CI (velopack.yml) の Windows 系 vpk pack 引数 + --signParams (ローカル署名の追加分)
    # --icon: Setup.exe のアイコン (旧 CI は未指定でアイコン無し Setup.exe になっていた)
    Invoke-Native "vpk pack ($runtime)" {
        & $vpk pack `
            --packId Komorebi `
            --packVersion $version `
            --mainExe Komorebi.exe `
            --packDir $publishDir `
            --outputDir $ArtifactsDir `
            --runtime $runtime `
            --channel $config.Channel `
            --icon (Join-Path $RepoRoot 'src\App.ico') `
            --signParams $SignParams
    }
}

# 署名後の最終パッケージと manifest を一体で照合する。展開物は検証用に保持する。
Write-Host '== 配信前の実物検証 ==' -ForegroundColor Cyan
$verificationRoot = Join-Path $WorkDir 'preupload-verification'
New-Item -ItemType Directory -Path $verificationRoot | Out-Null
function Assert-SignedApp {
    param([string]$Path, [string]$Runtime)
    $sig = Get-AuthenticodeSignature -LiteralPath $Path
    if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notlike "CN=$CertSubjectName*") { throw "アプリ署名検証失敗: $Path" }
    $info = [Diagnostics.FileVersionInfo]::GetVersionInfo($Path)
    if ($info.FileVersion -ne "$version.0" -or ($info.ProductVersion -split '\+')[0] -ne $version) { throw "アプリの版が不一致: $Path ($($info.FileVersion), $($info.ProductVersion))" }
    if ($info.ProductVersion.Contains('+') -and $info.ProductVersion -ne "$version+$sourceSHA") { throw "アプリの source SHA が不一致: $Path" }
    $bytes = [IO.File]::ReadAllBytes($Path)
    $pe = [BitConverter]::ToInt32($bytes, 0x3c)
    $machine = [BitConverter]::ToUInt16($bytes, $pe + 4)
    $expectedMachine = if ($Runtime -eq 'win-x64') { 0x8664 } else { 0xaa64 }
    if ($machine -ne $expectedMachine) { throw "アプリの CPU が不一致: $Path ($Runtime)" }
}
function Read-PackageApp {
    param([string]$Path, [string]$Runtime, [string]$Kind)
    $zip = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $appEntry = if ($Kind -eq 'nupkg') { 'lib/app/Komorebi.exe' } else { 'current/Komorebi.exe' }
        $apps = @($zip.Entries | Where-Object { $_.FullName -ceq $appEntry })
        if ($apps.Count -ne 1) { throw "Komorebi.exe が一意に存在しません: $Path" }
        if ($Kind -eq 'nupkg') {
            $specs = @($zip.Entries | Where-Object { $_.FullName -like '*.nuspec' })
            if ($specs.Count -ne 1) { throw "nuspec が一意に存在しません: $Path" }
            $reader = [IO.StreamReader]::new($specs[0].Open())
            try { $spec = [xml]$reader.ReadToEnd() } finally { $reader.Dispose() }
            $meta = $spec.package.metadata
            if ($meta.id -cne 'Komorebi' -or $meta.version -ne $version -or $meta.rid -ne $Runtime -or $meta.channel -ne $Runtime -or $meta.mainExe -cne 'Komorebi.exe') { throw "nupkg identity が不一致: $Path" }
        }
        $target = Join-Path $verificationRoot "$Runtime-$Kind.exe"
        [IO.Compression.ZipFileExtensions]::ExtractToFile($apps[0], $target)
        Assert-SignedApp -Path $target -Runtime $Runtime
        return (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    } finally { $zip.Dispose() }
}
foreach ($runtime in $Runtimes) {
    $manifestPath = Join-Path $ArtifactsDir "releases.$runtime.json"
    $assets = @((Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json).Assets)
    if ($assets.Count -ne 1) { throw "新規リリースの manifest は full package 1 個を必要とします: $runtime" }
    $asset = $assets[0]
    $packageName = "Komorebi-$version-$runtime-full.nupkg"
    if ($asset.PackageId -cne 'Komorebi' -or $asset.Version -ne $version -or $asset.Type -ne 'Full' -or $asset.FileName -cne $packageName) { throw "manifest identity が不一致: $runtime" }
    $package = Get-Item -LiteralPath (Join-Path $ArtifactsDir $packageName)
    if ($package.Length -ne $asset.Size -or (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA1).Hash -ne $asset.SHA1 -or (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash -ne $asset.SHA256) { throw "manifest の size/hash が不一致: $packageName" }
    $appHash = Read-PackageApp -Path $package.FullName -Runtime $runtime -Kind 'nupkg'
    $portableHash = Read-PackageApp -Path (Join-Path $ArtifactsDir "Komorebi-$runtime-Portable.zip") -Runtime $runtime -Kind 'portable'
    if ($appHash -ne $portableHash) { throw "portable と nupkg のアプリが不一致: $runtime" }
    if (-not (Test-Path -LiteralPath (Join-Path $ArtifactsDir "Komorebi-$runtime-Setup.exe") -PathType Leaf)) { throw "Setup.exe がありません: $runtime" }
    $inventory = @(Get-Content -LiteralPath (Join-Path $ArtifactsDir "assets.$runtime.json") -Raw | ConvertFrom-Json)
    $expectedNames = @($packageName, "Komorebi-$runtime-Setup.exe", "Komorebi-$runtime-Portable.zip")
    if ($inventory.Count -ne 3 -or @(Compare-Object ($inventory.RelativeFileName | Sort-Object) ($expectedNames | Sort-Object)).Count) { throw "assets manifest が不一致: $runtime" }
    $legacy = @(Get-Content -LiteralPath (Join-Path $ArtifactsDir "RELEASES-$runtime"))
    if ($legacy.Count -ne 1 -or $legacy[0].Trim() -ne "$($asset.SHA1) $packageName $($asset.Size)") { throw "RELEASES manifest が不一致: $runtime" }
    Write-Host "  ✅ $runtime : manifest / nupkg / portable の版・CPU・署名・hash が一致"
}
foreach ($exe in Get-ChildItem $ArtifactsDir -Filter '*.exe') {
    $sig = Get-AuthenticodeSignature $exe.FullName
    if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notlike "CN=$CertSubjectName*") {
        throw "署名検証失敗: $($exe.Name) → $($sig.Status)"
    }
    Write-Host "  ✅ $($exe.Name): Valid ($($sig.SignerCertificate.Subject -replace ',.*$'))"
}

if ($SkipUpload) {
    Write-Host "`n✅ -SkipUpload 指定のためここで終了。成果物: $ArtifactsDir" -ForegroundColor Green
    Get-ChildItem $ArtifactsDir | Format-Table Name, @{n='Size(MB)'; e={[math]::Round($_.Length/1MB,1)}}
    return
}

# ---- 2. R2 アップロード ----
# - manifest (releases.*.json) は必ず最後にアップロードする (Ferry rere #F-001 と同方針)。
#   manifest が先に上がると「manifest が指す nupkg が R2 に無い時間窓」ができ、
#   その間の更新チェックが 404 で失敗する
# - *.nupkg は put のみ (過去版は cleanup ステップが manifest 基準で削除)
Write-Host '== R2 アップロード ==' -ForegroundColor Cyan
$files = Get-ChildItem $ArtifactsDir -File
$ordered = @($files | Where-Object { $_.Name -notlike 'releases.*.json' }) +
           @($files | Where-Object { $_.Name -like 'releases.*.json' })
$uploaded = 0
foreach ($f in $ordered) {
    Write-Host "  ↑ $($f.Name)"
    Invoke-Native "R2 put ($($f.Name))" {
        pnpm dlx "wrangler@$WranglerVersion" r2 object put "$Bucket/$($f.Name)" --file $f.FullName --remote
    }
    $uploaded++
}
Write-Host "✅ R2 アップロード完了: $uploaded ファイル"

# ---- 2.5 Cloudflare エッジキャッシュのパージ ----
# 新鮮な配信物をローカルの SHA-256 と比較し、不一致が実証された固定 URL だけパージする。
Write-Host '== Cloudflare キャッシュパージ ==' -ForegroundColor Cyan
$verificationDir = Join-Path $WorkDir 'verification'
New-Item -ItemType Directory -Path $verificationDir -Force | Out-Null
function Test-PublishedFile {
    param([System.IO.FileInfo]$File, [switch]$Fresh)
    $target = Join-Path $verificationDir $File.Name
    $url = "$BaseUrl/$($File.Name)"
    $request = @{ Uri = $url; OutFile = $target; TimeoutSec = 120; MaximumRetryCount = 3; RetryIntervalSec = 5 }
    if ($Fresh) {
        $request.Uri += "?_=$([Guid]::NewGuid().ToString('N'))"
        $request.Headers = @{ 'Cache-Control' = 'no-cache' }
    }
    Invoke-WebRequest @request
    return (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $File.FullName -Algorithm SHA256).Hash
}
$purgeUrls = @(Get-ChildItem $ArtifactsDir -File | Where-Object { $_.Name -notlike '*.nupkg' } | ForEach-Object {
    $freshMatches = Test-PublishedFile -File $_ -Fresh
    $canonicalMatches = Test-PublishedFile -File $_
    if (-not $freshMatches -or -not $canonicalMatches) { "$BaseUrl/$($_.Name)" }
})
if ($purgeUrls.Count -gt 0) {
    $cfHeaders = @{ Authorization = "Bearer $($env:CLOUDFLARE_API_TOKEN)" }
    $zoneName = ([uri]$BaseUrl).Host -replace '^[^.]+\.', ''
    $zoneResp = Invoke-RestMethod -Uri "https://api.cloudflare.com/client/v4/zones?name=$zoneName" -Headers $cfHeaders -TimeoutSec 30
    if (-not $zoneResp.success -or @($zoneResp.result).Count -ne 1) { throw "Cloudflare zone '$zoneName' を一意に取得できませんでした" }
    $zoneId = $zoneResp.result[0].id
    $purgeBody = [PSCustomObject]@{ files = $purgeUrls } | ConvertTo-Json -Compress
    $purgeResp = Invoke-RestMethod -Method Post -Uri "https://api.cloudflare.com/client/v4/zones/$zoneId/purge_cache" `
        -Headers $cfHeaders -ContentType 'application/json' -Body $purgeBody -TimeoutSec 30
    if (-not $purgeResp.success) { throw "Cloudflare キャッシュパージに失敗しました: $($purgeResp.errors | ConvertTo-Json -Compress)" }
    Write-Host "  ✅ パージ: $($purgeUrls.Count) URL"
    $purgeUrls | ForEach-Object { Write-Host "     $_" }
} else {
    Write-Host '  パージ対象なし'
}

# ---- 3. 配信確認 (CDN/edge 伝播チェック) ----
Write-Host '== 配信確認 ==' -ForegroundColor Cyan
foreach ($runtime in $Runtimes) {
    $channel = $RuntimeMatrix[$runtime].Channel
    $url = "$BaseUrl/releases.$channel.json"
    $resp = Invoke-WebRequest -Uri $url -TimeoutSec 30 -MaximumRetryCount 3 -RetryIntervalSec 5
    Write-Host "  $url → HTTP $($resp.StatusCode) ($($resp.RawContentLength) bytes)"
}
foreach ($file in (Get-ChildItem $ArtifactsDir -File)) {
    if (-not (Test-PublishedFile -File $file)) { throw "配信ファイルの SHA-256 が一致しません: $($file.Name)" }
    Write-Host "  ✅ SHA-256 一致: $($file.Name)"
}

# ---- 4. 旧バージョン nupkg のクリーンアップ (Aggressive 戦略) ----
# keep set = ローカル artifacts の manifest (今アップロードしたもの) +
#            今回ビルドしていない Windows チャンネルの manifest を R2 から取得。
# ローカルリリースが所有する Windows チャンネルの「.nupkg かつ keep set 外」だけを削除する。
# macOS / Linux は CI 側だけが cleanup する。双方が全チャンネルを cleanup すると並行実行時に、
# 相手が nupkg を upload して manifest を更新するまでの間に新 nupkg を誤削除しうるため。
# 固定ファイル名 (Setup.exe /
# Portable.zip / AppImage / deb / rpm / komorebi_*.zip / RELEASES* / assets.*.json /
# releases.*.json) は .nupkg ではないので対象外 = 安全。
Write-Host '== 旧 nupkg クリーンアップ ==' -ForegroundColor Cyan
$keep = @{}
$manifests = Get-ChildItem $ArtifactsDir -Filter 'releases.*.json'
if (-not $manifests) { throw 'artifacts に releases.*.json が見つかりません' }
foreach ($m in $manifests) {
    foreach ($asset in (Get-Content $m.FullName -Raw | ConvertFrom-Json).Assets) {
        if ($asset.FileName) { $keep[$asset.FileName] = $true }
    }
}

# 今回リリースしていない全チャンネル (macOS / Linux 含む) の manifest を取得して keep set に追加。
# 取得できないチャンネルがあると keep set が不完全 = 配信中の成果物を誤削除しうるので
# 安全側に倒して cleanup ごと中止する (アップロード済みのリリース自体は有効)。
# ⚠️ 旧実装は Windows チャンネルだけを keep set に入れ、削除対象を「Windows チャンネル名を
#    含む *.nupkg」に限定していた。そのためチャンネル名を持たないバージョン付き配布物
#    (komorebi_x.y.z.macos-apple-silicon.zip / .deb / .rpm / .AppImage) が一切 cleanup されず、
#    リリースを重ねるほど R2 に溜まる構造だった (Ferry では同原因で 351 個 7.2GB 滞留)。
$releasedChannels = @($Runtimes | ForEach-Object { $RuntimeMatrix[$_].Channel })
$AllChannels = @('win-x64', 'win-arm64', 'osx-arm64', 'linux-x64', 'linux-arm64')
foreach ($channel in ($AllChannels | Where-Object { $releasedChannels -notcontains $_ })) {
    $url = "$BaseUrl/releases.$channel.json"
    try {
        # クエリで CDN キャッシュをバイパス。R2 は text 系でない Content-Type で返すことが
        # あり、その場合 .Content は byte[] になるため UTF-8 デコードしてから JSON parse する
        $resp = Invoke-WebRequest -Uri "${url}?_=$([Guid]::NewGuid().ToString('N'))" `
            -Headers @{ 'Cache-Control' = 'no-cache' } -TimeoutSec 30 -MaximumRetryCount 3 -RetryIntervalSec 5
        $raw = $resp.Content
        if ($raw -is [byte[]]) { $raw = [System.Text.Encoding]::UTF8.GetString($raw) }
        $remote = $raw | ConvertFrom-Json
    } catch {
        throw "他チャンネル manifest の取得に失敗 ($url)。keep set が不完全なため cleanup を中止します — $($_.Exception.Message)"
    }
    $assetsProp = $remote.PSObject.Properties['Assets']
    if (-not $assetsProp) {
        throw "他チャンネル manifest の形式が想定外です ($url)。keep set が不完全なため cleanup を中止します"
    }
    if ($assetsProp.Value) {
        foreach ($asset in $assetsProp.Value) {
            if ($asset.FileName) { $keep[$asset.FileName] = $true }
        }
    }
}
Write-Host "  保持対象 (manifest 参照): $($keep.Count) 件 (全 $($AllChannels.Count) チャンネル)"

$api = "https://api.cloudflare.com/client/v4/accounts/$AccountId/r2/buckets/$Bucket"
$headers = @{ Authorization = "Bearer $($env:CLOUDFLARE_API_TOKEN)" }

$allKeys = [System.Collections.Generic.List[string]]::new()
$cursor = ''
while ($true) {
    $uri = "$api/objects?per_page=1000" + $(if ($cursor) { "&cursor=$cursor" })
    $resp = Invoke-RestMethod -Uri $uri -Headers $headers -TimeoutSec 30
    foreach ($obj in $resp.result) { $allKeys.Add($obj.key) }
    # 全件 1 ページに収まると result_info が省略される (StrictMode 下では直接参照が throw)
    $info = $resp.PSObject.Properties['result_info']
    if (-not $info -or -not $info.Value) { break }
    $truncated = $info.Value.PSObject.Properties['is_truncated']
    if (-not $truncated -or -not $truncated.Value) { break }
    $cursorProp = $info.Value.PSObject.Properties['cursor']
    $cursor = if ($cursorProp) { $cursorProp.Value } else { '' }
    if (-not $cursor) { break }
}

# 全プロジェクト共通の保持ポリシー: 直近 2 バージョン。keep set が全チャンネルを
# 網羅した (上記) ので、チャンネル名による絞り込みは不要になった。
# 固定ファイル名 (Setup.exe / Portable.zip / *.AppImage / *.pkg / RELEASES* /
# assets.*.json / releases.*.json) はバージョン文字列を含まないので対象外 = 安全。
$KeepVersionCount = 2
$versionPattern = '(\d+\.\d+\.\d+)'
$allVersions = @(
    $allKeys | ForEach-Object {
        $m = [regex]::Match($_, $versionPattern)
        if ($m.Success) { $m.Groups[1].Value }
    } | Sort-Object -Property { [version]$_ } -Unique
)
$keepVersions = @($allVersions | Select-Object -Last $KeepVersionCount)
Write-Host "  保持バージョン: $($keepVersions -join ', ') (全 $($allVersions.Count) 世代)"

$toDelete = $allKeys | Where-Object {
    # manifest が参照するファイルは絶対保持 (消すと自動更新が壊れる)
    if ($keep.ContainsKey($_)) { return $false }
    # 固定ファイル名はバージョン文字列を含まない = 毎リリース上書きなので保持
    $m = [regex]::Match($_, $versionPattern)
    if (-not $m.Success) { return $false }
    return $keepVersions -notcontains $m.Groups[1].Value
}
if (-not $toDelete) {
    Write-Host '  ✅ 削除対象なし'
} else {
    $deleted = 0; $failed = 0
    foreach ($key in $toDelete) {
        $encoded = [uri]::EscapeDataString($key)
        try {
            Invoke-RestMethod -Method Delete -Uri "$api/objects/$encoded" -Headers $headers -TimeoutSec 30 | Out-Null
            Write-Host "  🗑️  $key"
            $deleted++
        } catch {
            Write-Warning "  削除失敗: $key — $($_.Exception.Message)"
            $failed++
        }
    }
    Write-Host "  🧹 クリーンアップ: $deleted 削除 / $failed 失敗"
    # 全件失敗は token 権限等の異常なので fail (一部失敗は次回リリースで再試行される)
    if ($failed -gt 0 -and $deleted -eq 0) { throw '旧 nupkg の削除がすべて失敗しました。API token の権限を確認してください。' }
}

# ---- 5. 通常開発用lockfileの検証 ----
# Directory.Build.targets が構成・AOT・RIDごとにlockを分けるため、
# publish後にDebugの正本を再生成しない。検証のみ行い、既存の固定値を維持する。
Write-Host '== 通常開発用lockfile 検証 ==' -ForegroundColor Cyan
try {
    Invoke-Native 'dotnet restore --locked-mode' {
        dotnet restore Komorebi.slnx --locked-mode
    }
    Write-Host '  ✅ 通常開発用lockfile の固定復元 OK'
} catch {
    Write-Warning "  lockfile の固定復元に失敗しました (アップロード済みリリースには影響なし)。依存関係と構成を確認してください — $($_.Exception.Message)"
}
Write-Host "`n🎉 リリース完了: v$version → $BaseUrl (Windows チャンネルのみ。macOS/Linux は release.yml が配信)" -ForegroundColor Green
