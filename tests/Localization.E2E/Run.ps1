param([string]$Node = 'node')

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$artifactRoot = Join-Path $repoRoot "tests/CloneLongPaths.E2E/bin/localization-artifacts/$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
$preload = Join-Path $artifactRoot 'fail-read.cjs'

# 失敗候補: 書込み前の読み込み失敗でもexit 0、または失敗後にlocaleを書き換える。
# 実CLIのfs境界だけを隔離し、実際のプロセス終了コードを観測する。
@'
const Module = require('module');
const original = Module._load;
Module._load = function(request, parent, isMain) {
    if (request === 'fs-extra') {
        return {
            readFile: async () => { throw new Error('RERE_SYNTHETIC_READ_FAILURE'); },
            writeFile: async () => { throw new Error('RERE_UNEXPECTED_WRITE'); },
        };
    }
    return original.call(this, request, parent, isMain);
};
'@ | Set-Content -LiteralPath $preload -Encoding utf8

$stderrPath = Join-Path $artifactRoot 'stderr.log'
$stdoutPath = Join-Path $artifactRoot 'stdout.log'
$nodeExecutable = (Get-Command $Node -ErrorAction Stop).Source
$process = Start-Process -FilePath $nodeExecutable -ArgumentList @('--require', ('"' + $preload + '"'), ('"' + (Join-Path $repoRoot 'build/scripts/localization-check.js') + '"')) -WorkingDirectory $repoRoot -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
$stderr = Get-Content -LiteralPath $stderrPath -Raw
$passed = $process.ExitCode -eq 1 -and $stderr.Contains('RERE_SYNTHETIC_READ_FAILURE') -and !$stderr.Contains('RERE_UNEXPECTED_WRITE')
@{
    Scenario = 'localization-read-failure-before-write'
    ExitCode = $process.ExitCode
    Passed = $passed
    ArtifactDirectory = $artifactRoot
    Script = (Join-Path $repoRoot 'build/scripts/localization-check.js')
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artifactRoot 'result.json') -Encoding utf8
Write-Output "Artifacts: $artifactRoot"
if (!$passed) { throw "ローカライズCLI失敗経路の検証に失敗しました: $stderrPath" }
Write-Output 'localization-read-failure-before-write: PASS'
