#!/usr/bin/env bash
set -euo pipefail
umask 022
repo=$(git rev-parse --show-toplevel)
root=$(mktemp -d "${1:?}/localization-workflow-test.XXXXXXXX")
apply="$repo/.github/scripts/localization-apply.sh"
update="$repo/.github/scripts/localization-update.sh"
workflow="$repo/.github/workflows/localization-update.yml"

# 書込み job で生成器や依存を実行しない構成と認証分離を確認する。
generate=$(sed -n '/^  generate-localization:/,/^  update-localization:/p' "$workflow")
write=$(sed -n '/^  update-localization:/,$p' "$workflow")
[[ $generate == *'contents: read'* && $generate == *'persist-credentials: false'* ]]
[[ $generate != *'statuses: write'* && $generate != *'GH_TOKEN: '* ]]
[[ $write == *'persist-credentials: false'* && $write != *'bun '* && $write != *'localization-check.js'* ]]

fixture() {
  name=$1
  mkdir -p "$root/$name/work/docs" "$root/$name/work/src/Resources/Locales" \
    "$root/$name/artifact/docs" "$root/$name/artifact/src/Resources/Locales"
  cd "$root/$name/work"
  git init -q
  git config user.name Test
  git config user.email test@example.com
  printf old > docs/TRANSLATION.md
  printf '<ResourceDictionary xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"><x:String x:Key="A">English</x:String></ResourceDictionary>' > src/Resources/Locales/en_US.axaml
  printf '<ResourceDictionary xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"><x:String x:Key="A">Locale</x:String><x:String x:Key="Extra">Remove</x:String></ResourceDictionary>' > src/Resources/Locales/ja_JP.axaml
  git add .
  git commit -qm base
  export GITHUB_SHA; GITHUB_SHA=$(git rev-parse HEAD)
  printf '# Translation Status\n\nThis document shows the translation status of each locale file in the repository.\n\n## Details\n\n### ![en_US](https://img.shields.io/badge/en__US-%%E2%%88%%9A-brightgreen)\n\n### ![ja__JP](https://img.shields.io/badge/ja__JP-%%E2%%88%%9A-brightgreen)' > "$root/$name/artifact/docs/TRANSLATION.md"
  cp src/Resources/Locales/en_US.axaml "$root/$name/artifact/src/Resources/Locales/en_US.axaml"
  printf '<ResourceDictionary xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"><x:String x:Key="A">Locale</x:String></ResourceDictionary>' > "$root/$name/artifact/src/Resources/Locales/ja_JP.axaml"
}

fixture valid
bash "$apply" "$root/valid/artifact"
[[ $(cat docs/TRANSLATION.md) == '# Translation Status'* && $(cat src/Resources/Locales/ja_JP.axaml) != *Extra* ]]

# App 設定と branch rule を分けて判定し、不完全な設定や API 失敗は保留する。
export GH_TOKEN=github-token GITHUB_REPOSITORY=owner/repo GITHUB_OUTPUT="$root/guard-output" GUARD_REPOSITORY_ID=1
export GUARD_APP_SLUG=example-guard GUARD_BOT_ID=456
if GUARD_CLIENT_ID=Iv123 GUARD_APP_ID= GUARD_PRIVATE_KEY=dummy bash "$update" --guard-config; then exit 1; fi
if GUARD_CLIENT_ID=Iv123 GUARD_APP_ID=invalid GUARD_PRIVATE_KEY=dummy bash "$update" --guard-config; then exit 1; fi

# HTTP 応答で rule JSON と pagination の不整合を確認する。
cat > "$root/rule-check.mjs" <<'EOF'
import { readFileSync, writeFileSync } from 'node:fs';
const [update, root] = process.argv.slice(2);
const context = 'kagayoi/automation-merge-guard-v1';
const rule = checks => ({ type: 'required_status_checks', parameters: { required_status_checks: checks } });
let pages, mode, firstReads;
const server = Bun.serve({ port: 0, fetch: request => {
  const url = new URL(request.url), page = Number(url.searchParams.get('page') || 1);
  if (page === 1) firstReads++;
  const headers = {};
  if (page < pages.length) headers.Link = `<https://api.github.com/repositories/1/rules/branches/main?per_page=100&page=${page + 1}>; rel="next", <https://api.github.com/repositories/1/rules/branches/main?per_page=100&page=${pages.length}>; rel="last"`;
  else if (page > 1 && page <= pages.length) headers.Link = `<https://api.github.com/repositories/1/rules/branches/main?per_page=100&page=${page - 1}>; rel="prev", <https://api.github.com/repositories/1/rules/branches/main?per_page=100&page=1>; rel="first"`;
  if (mode === 'lastonly') headers.Link = '<https://api.github.com/repositories/1/rules/branches/main?per_page=100&page=2>; rel="last"';
  if (mode === 'foreign') headers.Link = '<https://api.github.com/repositories/2/rules/branches/main?per_page=100&page=2>; rel="next"';
  if (mode === 'skip') headers.Link = '<https://api.github.com/repositories/1/rules/branches/main?per_page=100&page=3>; rel="next"';
  if (mode === 'lastoverflow') headers.Link = page === 1
    ? '<https://api.github.com/repositories/1/rules/branches/main?per_page=100&page=2>; rel="next", <https://api.github.com/repositories/1/rules/branches/main?per_page=100&page=2>; rel="last"'
    : '<https://api.github.com/repositories/1/rules/branches/main?per_page=100&page=3>; rel="next"';
  if (mode === 'firstlinkchanged' && page === 1 && firstReads > 1) headers.Link = '<https://api.github.com/repositories/1/rules/branches/main?per_page=100&page=2>; rel="next"';
  const response = pages[page - 1] ?? [];
  if (mode === 'firstchanged' && page === 1 && firstReads > 1) return Response.json([rule([{context, integration_id: 123}]), ...response.slice(1)], {headers});
  return Response.json(response, { headers });
} });
const scenarios = [
  [[[]], false, true, 'enabled=false'],
  [[[rule([{ context: context.toUpperCase(), integration_id: 123 }])]], true, true, 'enabled=true'],
  [[[...Array.from({length: 100}, (_, id) => ({type: 'creation', ruleset_id: id + 1}))], [rule([{ context, integration_id: 123 }])]], true, true, 'enabled=true'],
  [[[rule([{ context, integration_id: 123 }])]], false, false],
  [[[rule([{ context, integration_id: 124 }])]], true, false],
  [[[rule([{ context, integration_id: 123 }]), rule([{ context: context.toUpperCase(), integration_id: 124 }])]], true, false],
  [[[...Array.from({length: 99}, (_, id) => ({type: 'creation', ruleset_id: id + 1})), rule([{ context, integration_id: 123 }])], [rule([{ context: context.toUpperCase(), integration_id: 124 }])]], true, false],
  [[[rule([{ context, integration_id: 123 }]), rule([{ context, integration_id: null }])]], true, false],
  [[[{ type: 'required_status_checks', parameters: { required_status_checks: null } }]], true, false],
  [[[]], false, false, null, 'lastonly'],
  [[[]], false, false, null, 'foreign'],
  [[[...Array.from({length: 100}, (_, id) => ({type: 'creation', ruleset_id: id + 1}))]], false, false, null, 'skip'],
  [[[...Array.from({length: 100}, (_, id) => ({type: 'creation', ruleset_id: id + 1}))]], false, true, 'enabled=false'],
  [[[...Array.from({length: 100}, (_, id) => ({type: 'creation', ruleset_id: id + 1}))], []], false, false],
  [[Array.from({length: 100}, (_, id) => ({type: 'creation', ruleset_id: id + 1})), Array.from({length: 100}, (_, id) => ({type: 'creation', ruleset_id: id + 101})), []], false, false, null, 'lastoverflow'],
  [[Array.from({length: 100}, (_, id) => ({type: 'creation', ruleset_id: id + 1})), [{type: 'creation', ruleset_id: 101}]], false, false, null, 'firstchanged'],
  [[Array.from({length: 100}, (_, id) => ({type: 'creation', ruleset_id: id + 1})), [{type: 'creation', ruleset_id: 101}]], false, false, null, 'firstlinkchanged'],
  [Array.from({length: 10}, (_, page) => Array.from({length: 100}, (_, i) => ({type: 'creation', ruleset_id: page * 100 + i + 1}))), false, true, 'enabled=false'],
  [Array.from({length: 11}, (_, page) => Array.from({length: 100}, (_, i) => ({type: 'creation', ruleset_id: page * 100 + i + 1}))), false, false],
];
for (let i = 0; i < scenarios.length; i++) {
  const [rulePages, configured, succeeds, output, testMode] = scenarios[i]; pages = rulePages; mode = testMode; firstReads = 0;
  const out = `${root}/real-rule-${i}`; writeFileSync(out, '');
  const child = Bun.spawn(['bash', update, '--guard-config'], { stdout: 'pipe', stderr: 'pipe', env: {
    ...process.env, LOCALIZATION_GUARD_TEST_API: `http://127.0.0.1:${server.port}`, GITHUB_OUTPUT: out,
    GH_TOKEN: 'github-token', GUARD_CLIENT_ID: configured ? 'Iv123' : '',
    GUARD_APP_ID: configured ? '123' : '', GUARD_PRIVATE_KEY: configured ? 'dummy' : '',
    GUARD_APP_SLUG: configured ? 'example-guard' : '', GUARD_BOT_ID: configured ? '456' : '',
  } });
  const [code, stderr] = await Promise.all([child.exited, new Response(child.stderr).text()]);
  if ((code === 0) !== succeeds || (output && !readFileSync(out, 'utf8').split(/\r?\n/).includes(output)))
    throw new Error(`Rule scenario ${i} failed: ${code} ${stderr}`);
}
server.stop();
EOF
bun "$root/rule-check.mjs" "$update" "$root"

fixture unauthorized
mkdir "$root/unauthorized/artifact/.github"
printf hostile > "$root/unauthorized/artifact/.github/config"
if bash "$apply" "$root/unauthorized/artifact"; then exit 1; fi
[[ $(cat docs/TRANSLATION.md) == old && ! -e .github/config ]]

fixture traversal
mkdir -p "$root/traversal/artifact/src/Resources/Locales/escape"
printf hostile > "$root/traversal/artifact/src/Resources/Locales/escape/payload.axaml"
if bash "$apply" "$root/traversal/artifact"; then exit 1; fi
[[ $(cat docs/TRANSLATION.md) == old ]]

for kind in english translation attribute structure deletion document dtd; do
  fixture "$kind"
  case $kind in
    english) printf hostile > "$root/$kind/artifact/src/Resources/Locales/en_US.axaml" ;;
    translation) printf '<ResourceDictionary xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"><x:String x:Key="A">Hostile</x:String></ResourceDictionary>' > "$root/$kind/artifact/src/Resources/Locales/ja_JP.axaml" ;;
    attribute) printf '<ResourceDictionary xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"><x:String x:Key="A" xml:space="preserve">Locale</x:String></ResourceDictionary>' > "$root/$kind/artifact/src/Resources/Locales/ja_JP.axaml" ;;
    structure) printf '<ResourceDictionary xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"><x:String x:Key="A">Locale</x:String><Injected /></ResourceDictionary>' > "$root/$kind/artifact/src/Resources/Locales/ja_JP.axaml" ;;
    deletion) printf '<ResourceDictionary xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" />' > "$root/$kind/artifact/src/Resources/Locales/ja_JP.axaml" ;;
    document) printf hostile > "$root/$kind/artifact/docs/TRANSLATION.md" ;;
    dtd) printf '<!DOCTYPE x [<!ENTITY a SYSTEM "file:///etc/passwd">]><ResourceDictionary xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"><x:String x:Key="A">&a;</x:String></ResourceDictionary>' > "$root/$kind/artifact/src/Resources/Locales/ja_JP.axaml" ;;
  esac
  if bash "$apply" "$root/$kind/artifact"; then exit 1; fi
  [[ $(cat docs/TRANSLATION.md) == old ]]
done
for count in 1049 1050 1051; do
  case $count in 1049) fixed=78.05 ;; 1050) fixed=78.13 ;; 1051) fixed=78.20 ;; esac
  [[ $(bun -e "process.stdout.write(($count / 1344 * 100).toFixed(2))") == "$fixed" ]]
  fixture "round$count"
  english=src/Resources/Locales/en_US.axaml
  translated=src/Resources/Locales/ja_JP.axaml
  printf '<ResourceDictionary xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">' > "$english"
  printf '<ResourceDictionary xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">' > "$translated"
  for ((i=0; i<1344; i++)); do
    printf -v key 'K%04d' "$i"
    printf '<x:String x:Key="%s">value</x:String>' "$key" >> "$english"
    if (( i < count )); then printf '<x:String x:Key="%s">value</x:String>' "$key" >> "$translated"; fi
  done
  printf '</ResourceDictionary>' >> "$english"
  printf '</ResourceDictionary>' >> "$translated"
  git add -- "$english" "$translated"
  git commit -qm rounding
  export GITHUB_SHA; GITHUB_SHA=$(git rev-parse HEAD)
  artifact="$root/round$count/artifact"
  cp "$english" "$artifact/$english"
  cp "$translated" "$artifact/$translated"
  {
    printf '# Translation Status\n\nThis document shows the translation status of each locale file in the repository.\n\n## Details\n\n### ![en_US](https://img.shields.io/badge/en__US-%%E2%%88%%9A-brightgreen)\n\n'
    printf '### ![ja__JP](https://img.shields.io/badge/ja__JP-%s%%25-yellow)\n\n' "$fixed"
    printf '<details>\n<summary>Missing keys in ja_JP.axaml</summary>\n\n'
    for ((i=count; i<1344; i++)); do printf -- '- K%04d\n' "$i"; done
    printf '\n</details>'
  } > "$artifact/docs/TRANSLATION.md"
  bash "$apply" "$artifact"
done
fixture multi_segment
[[ $(bun -e "process.stdout.write('zh_Hant_TW.axaml'.replace('.axaml', '').replace('_', '__'))") == zh__Hant_TW ]]
cp "$root/multi_segment/artifact/src/Resources/Locales/ja_JP.axaml" src/Resources/Locales/zh_Hant_TW.axaml
git add -- src/Resources/Locales/zh_Hant_TW.axaml
git commit -qm multi_segment
export GITHUB_SHA; GITHUB_SHA=$(git rev-parse HEAD)
cp src/Resources/Locales/zh_Hant_TW.axaml "$root/multi_segment/artifact/src/Resources/Locales/zh_Hant_TW.axaml"
printf '\n\n### ![zh__Hant_TW](https://img.shields.io/badge/zh__Hant_TW-%%E2%%88%%9A-brightgreen)' >> "$root/multi_segment/artifact/docs/TRANSLATION.md"
bash "$apply" "$root/multi_segment/artifact"
fixture repeated_extension
[[ $(bun -e "process.stdout.write('a.axaml_b.axaml'.replace('.axaml', '').replace('_', '__'))") == a__b.axaml ]]
cp "$root/repeated_extension/artifact/src/Resources/Locales/ja_JP.axaml" src/Resources/Locales/a.axaml_b.axaml
git add -- src/Resources/Locales/a.axaml_b.axaml
git commit -qm repeated_extension
export GITHUB_SHA; GITHUB_SHA=$(git rev-parse HEAD)
cp src/Resources/Locales/a.axaml_b.axaml "$root/repeated_extension/artifact/src/Resources/Locales/a.axaml_b.axaml"
printf '# Translation Status\n\nThis document shows the translation status of each locale file in the repository.\n\n## Details\n\n### ![en_US](https://img.shields.io/badge/en__US-%%E2%%88%%9A-brightgreen)\n\n### ![a__b.axaml](https://img.shields.io/badge/a__b.axaml-%%E2%%88%%9A-brightgreen)\n\n### ![ja__JP](https://img.shields.io/badge/ja__JP-%%E2%%88%%9A-brightgreen)' > "$root/repeated_extension/artifact/docs/TRANSLATION.md"
bash "$apply" "$root/repeated_extension/artifact"
fixture position
printf '<ResourceDictionary xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"><x:String x:Key="A">English</x:String><x:String x:Key="B">English</x:String></ResourceDictionary>' > src/Resources/Locales/en_US.axaml
printf '<ResourceDictionary xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"><Merged/><x:String x:Key="A">A</x:String><x:String x:Key="B">B</x:String></ResourceDictionary>' > src/Resources/Locales/ja_JP.axaml
git add -- src/Resources/Locales/en_US.axaml src/Resources/Locales/ja_JP.axaml
git commit -qm position
export GITHUB_SHA; GITHUB_SHA=$(git rev-parse HEAD)
cp src/Resources/Locales/en_US.axaml "$root/position/artifact/src/Resources/Locales/en_US.axaml"
printf '<ResourceDictionary xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"><x:String x:Key="A">A</x:String><Merged/><x:String x:Key="B">B</x:String></ResourceDictionary>' > "$root/position/artifact/src/Resources/Locales/ja_JP.axaml"
if bash "$apply" "$root/position/artifact"; then exit 1; fi
[[ $(cat docs/TRANSLATION.md) == old ]]

# Bare remote で App status と Git 認証の分離を検証する。
real_git=$(command -v git)
real_cat=$(command -v cat)
real_dotnet=$(command -v dotnet)
mkdir "$root/push-bin"
mkdir "$root/rule-mock-bin"
cp "$repo/.github/scripts/localization-guard.cs" "$root/guard.cs"
cat > "$root/rule-mock-bin/dotnet" <<'EOF'
#!/usr/bin/env bash
[[ ${MOCK_RULE:-} != error ]] || exit 1
printf '%s\n' "${MOCK_RULE:?}"
EOF
cat > "$root/push-bin/cat" <<'EOF'
#!/usr/bin/env bash
if [[ ${1:-} == /proc/sys/kernel/random/uuid ]]; then echo 01234567-89ab-4cde-8fab-0123456789ab; else exec "$REAL_CAT" "$@"; fi
EOF
cat > "$root/push-bin/git" <<'EOF'
#!/usr/bin/env bash
if [[ ${1:-} == push || ${1:-} == fetch || ${1:-} == ls-remote ]]; then
  expected="AUTHORIZATION: basic $(printf 'x-access-token:%s' github-token | base64 -w0)"
  [[ ${GIT_CONFIG_VALUE_0:-} == "$expected" ]] || exit 1
fi
if [[ ${MOCK_PUSH:-} == cleanup_failure && ${1:-} == push && ${!#} == :refs/heads/automation/localization-* ]]; then exit 1; fi
exec "$REAL_GIT" "$@"
EOF
cat > "$root/push-bin/gh" <<'EOF'
#!/usr/bin/env bash
[[ ${GH_TOKEN:-} == app-token ]] || exit 1
case "$*" in
  *apps/example-guard*) if [[ $MOCK_PUSH == bad_app ]]; then printf '124\tIv123\texample-guard\n'; elif [[ $MOCK_PUSH == bad_client ]]; then printf '123\tIv999\texample-guard\n'; else printf '123\tIv123\texample-guard\n'; fi ;;
  *users/example-guard%5Bbot%5D*) if [[ $MOCK_PUSH == bad_bot ]]; then printf '999\texample-guard[bot]\tBot\n'; else printf '456\texample-guard[bot]\tBot\n'; fi ;;
  *'--method POST'*) [[ $MOCK_PUSH != status_failure ]] || exit 1; printf '789\n' ;;
  *) exit 1 ;;
esac
EOF
cat > "$root/push-bin/dotnet" <<'EOF'
#!/usr/bin/env bash
[[ ${GH_TOKEN:-} == app-token ]] || exit 1
if [[ $MOCK_PUSH == real_* ]]; then exec "$REAL_DOTNET" "$@"; fi
[[ $MOCK_PUSH != bad_readback && $MOCK_PUSH != later_status && $MOCK_PUSH != unknown_status ]]
EOF
chmod +x "$root/push-bin/"* "$root/rule-mock-bin/dotnet"
cat > "$root/status-server.mjs" <<'EOF'
import { readFileSync } from 'node:fs';
const counts = new Map();
const server = Bun.serve({ port: 0, fetch(request) {
  const url = new URL(request.url), parts = url.pathname.split('/');
  const history = parts.at(-1) === 'statuses', scenario = readFileSync(process.argv[2], 'utf8').trim(), sha = parts.at(-2);
  const status = { id: 789, context: 'kagayoi/automation-merge-guard-v1', state: 'success',
    target_url: 'https://github.com/owner/repo/actions/runs/1/attempts/1', description: 'Validated localization update' };
  if (scenario === 'real_case') status.context = status.context.toUpperCase();
  if (history) {
    status.creator = { id: scenario === 'real_creator' ? 999 : 456, login: 'example-guard[bot]' };
    if (scenario === 'real_history_newer') return Response.json([{...status, id: 790, context: status.context.toUpperCase(), state: 'pending'}, status]);
    const page = Number(url.searchParams.get('page') || 1);
    const historyKey = `history/${scenario}/${sha}`;
    if (page === 1) counts.set(historyKey, (counts.get(historyKey) || 0) + 1);
    if (scenario === 'real_history_first_changed') {
      const items = page === 1 ? [status, ...Array.from({length: 99}, (_, i) => ({id: 1000 + i + (counts.get(historyKey) > 1 && i === 0 ? 10000 : 0), context: `other-${i}`}))] : [{id: 3000, context: 'other-last'}];
      const headers = page === 1 ? {Link: `<https://api.github.com/repositories/1/statuses/${sha}?per_page=100&page=2>; rel="next"`} : {};
      return Response.json(items, {headers});
    }
    if (scenario === 'real_history_lastoverflow') {
      const items = page === 1 ? [status, ...Array.from({length: 99}, (_, i) => ({id: 1000 + i, context: `other-${i}`}))] : Array.from({length: 100}, (_, i) => ({id: 2000 + i, context: `more-${i}`}));
      const headers = page === 1 ? {Link: `<https://api.github.com/repositories/1/statuses/${sha}?per_page=100&page=2>; rel="next", <https://api.github.com/repositories/1/statuses/${sha}?per_page=100&page=2>; rel="last"`} : {Link: `<https://api.github.com/repositories/1/statuses/${sha}?per_page=100&page=3>; rel="next"`};
      return Response.json(items, {headers});
    }
    if (scenario === 'real_history_page2') {
      const items = page === 1 ? Array.from({length: 100}, (_, i) => ({id: 1000 + i, context: `other-${i}`})) : [status];
      const headers = page === 1 ? {Link: `<https://api.github.com/repositories/1/statuses/${sha}?per_page=100&page=2>; rel="next"`} : {};
      return Response.json(items, {headers});
    }
    if (scenario === 'real_history_cross_duplicate') {
      const items = page === 1 ? [status, ...Array.from({length: 99}, (_, i) => ({id: 1000 + i, context: `other-${i}`}))] : [{id: 1000, context: 'other-0'}];
      const headers = page === 1 ? {Link: `<https://api.github.com/repositories/1/statuses/${sha}?per_page=100&page=2>; rel="next"`} : {};
      return Response.json(items, {headers});
    }
    if (scenario === 'real_history_empty_last') {
      const items = page === 1 ? [status, ...Array.from({length: 99}, (_, i) => ({id: 1000 + i, context: `other-${i}`}))] : [];
      const headers = page === 1 ? {Link: `<https://api.github.com/repositories/1/statuses/${sha}?per_page=100&page=2>; rel="next", <https://api.github.com/repositories/1/statuses/${sha}?per_page=100&page=2>; rel="last"`} : {};
      return Response.json(items, {headers});
    }
    if (scenario === 'real_history_sentinel_last') {
      const items = page === 1 ? [status, ...Array.from({length: 99}, (_, i) => ({id: 1000 + i, context: `other-${i}`}))] : [];
      const link = `<https://api.github.com/repositories/1/statuses/${sha}?per_page=100&page=1>`;
      const headers = page === 1 ? {Link: `${link}; rel="last"`} : {Link: `${link}; rel="prev", ${link}; rel="last", ${link}; rel="first"`};
      return Response.json(items, {headers});
    }
    if (scenario === 'real_history_sentinel') return Response.json(page === 1 ? [status, ...Array.from({length: 99}, (_, i) => ({id: 1000 + i, context: `other-${i}`}))] : []);
    return Response.json(scenario === 'real_history_missing' ? [] : scenario === 'real_history_duplicate' ? [status, status] : [status]);
  }
  const key = `${scenario}/${sha}`, count = (counts.get(key) || 0) + 1; counts.set(key, count);
  if (scenario === 'real_changed' && count > 1) { status.id = 790; status.state = 'pending'; }
  if (scenario === 'real_pending') { status.id = 790; status.state = 'pending'; }
  if (scenario === 'real_failure') { status.id = 790; status.state = 'failure'; }
  if (scenario === 'real_target') status.target_url = 'https://example.invalid/';
  const headers = {};
  if (scenario === 'real_multipage' || scenario === 'real_combined_first_changed') {
    const page = Number(url.searchParams.get('page') || 1);
    const items = [status, ...Array.from({length: 100}, (_, i) => ({id: 1000 + i, context: `other-${i}`, state: 'success', target_url: null}))];
    if (scenario === 'real_combined_first_changed' && count > 2) items[1].state = 'failure';
    if (page === 1) headers.Link = `<https://api.github.com/repositories/1/status/${sha}?per_page=100&page=2>; rel="next"`;
    return Response.json({sha, repository: {id: 1}, total_count: items.length, statuses: items.slice((page - 1) * 100, page * 100)}, {headers});
  }
  if (scenario === 'real_context_duplicate' || scenario === 'real_id_duplicate' || scenario === 'real_other_changed') {
    const other = {id: scenario === 'real_id_duplicate' ? 789 : 999, context: scenario === 'real_context_duplicate' ? status.context.toUpperCase() : 'other', state: scenario === 'real_other_changed' && count > 1 ? 'failure' : 'success', target_url: null};
    return Response.json({sha, repository: {id: 1}, total_count: 2, statuses: [status, other]});
  }
  if (scenario === 'real_duplicate' && url.searchParams.get('page') === '1') {
    headers.Link = `<https://api.github.com/repositories/1/status/${sha}?per_page=100&page=2>; rel="next"`;
  }
  if (scenario === 'real_lastonly') headers.Link = `<https://api.github.com/repositories/1/status/${sha}?per_page=100&page=2>; rel="last"`;
  return Response.json({ sha: scenario === 'real_sha' ? 'wrong' : sha, repository: {id: 1}, total_count: scenario === 'real_count' ? 2 : 1,
    statuses: scenario === 'real_malformed' ? null : scenario === 'real_missing' ? [] : [status] }, { headers });
} });
console.log(server.port);
EOF
printf initial > "$root/status-scenario"
coproc STATUS_SERVER { exec bun "$root/status-server.mjs" "$root/status-scenario"; }
status_server_pid=$STATUS_SERVER_PID
trap 'kill "$status_server_pid" 2>/dev/null || true' EXIT
read -r status_port <&"${STATUS_SERVER[0]}"
[[ $status_port =~ ^[0-9]+$ ]]
for scenario in app fallback status_failure bad_readback later_status unknown_status bad_app bad_client bad_slug bad_bot cleanup_failure real_match real_case real_multipage real_history_page2 real_history_sentinel real_history_sentinel_last real_pending real_failure real_creator real_history_missing real_history_duplicate real_history_cross_duplicate real_history_empty_last real_history_first_changed real_history_lastoverflow real_history_newer real_changed real_other_changed real_combined_first_changed real_context_duplicate real_id_duplicate real_target real_sha real_malformed real_missing real_duplicate real_count real_lastonly rule_unset rule_other rule_duplicate rule_malformed rule_case_other rule_error; do
  dir="$root/push-$scenario"; mkdir "$dir"; cd "$dir"
  git init -q --bare remote.git
  git init -q -b main work; cd work
  git config user.name Test; git config user.email test@example.com
  mkdir -p docs src/Resources/Locales
  printf old > docs/TRANSLATION.md; printf old > src/Resources/Locales/en_US.axaml
  git add .; git commit -qm base
  base=$(git rev-parse HEAD); git remote add origin "$dir/remote.git"; git push -q origin main
  printf new > docs/TRANSLATION.md
  enabled=true; [[ $scenario != fallback ]] || enabled=false
  rc=0
  if [[ $scenario == rule_* ]]; then
    rule=invalid client=Iv123 app=123 key=dummy slug=example-guard bot=456
    case $scenario in
      rule_unset) rule=id:123; client=; app=; key=; slug=; bot= ;;
      rule_other|rule_case_other) rule=id:124 ;;
      rule_error) rule=error ;;
    esac
    PATH="$root/rule-mock-bin:$PATH" MOCK_RULE="$rule" GH_TOKEN=github-token GUARD_CLIENT_ID="$client" GUARD_APP_ID="$app" GUARD_PRIVATE_KEY="$key" GUARD_APP_SLUG="$slug" GUARD_BOT_ID="$bot" \
      bash "$update" --guard-config || rc=$?
    [[ $rc != 0 ]] || exit 1
  else
    expected_slug=example-guard bot_id=456
    [[ $scenario != bad_slug ]] || expected_slug=other-guard
    printf '%s' "$scenario" > "$root/status-scenario"
    PATH="$root/push-bin:$PATH" REAL_GIT="$real_git" REAL_CAT="$real_cat" REAL_DOTNET="$real_dotnet" LOCALIZATION_GUARD_TEST_API="http://127.0.0.1:$status_port" MOCK_PUSH="$scenario" \
      GH_TOKEN=github-token GUARD_ENABLED="$enabled" GUARD_CLIENT_ID=Iv123 GUARD_APP_ID=123 GUARD_APP_SLUG=example-guard GUARD_EXPECTED_SLUG="$expected_slug" GUARD_BOT_ID="$bot_id" GUARD_APP_TOKEN=app-token \
      GUARD_HELPER="$root/guard.cs" GITHUB_SHA="$base" GITHUB_RUN_ID=1 GITHUB_RUN_ATTEMPT=1 bash "$update" || rc=$?
  fi
  main=$(git --git-dir="$dir/remote.git" rev-parse refs/heads/main)
  temp=$(git --git-dir="$dir/remote.git" rev-parse -q --verify refs/heads/automation/localization-01234567-89ab-4cde-8fab-0123456789ab || true)
  case $scenario in
    app|fallback|real_match|real_case|real_multipage|real_history_page2|real_history_sentinel|real_history_sentinel_last) [[ $rc == 0 && $main == $(git rev-parse HEAD) && -z $temp ]] ;;
    cleanup_failure) [[ $rc != 0 && $main == $(git rev-parse HEAD) && $temp == "$main" ]] ;;
    *) [[ $rc != 0 && $main == "$base" && -z $temp ]] ;;
  esac
done
kill "$status_server_pid"
wait "$status_server_pid" 2>/dev/null || true
trap - EXIT
fixture symlink
mv "$root/symlink/artifact/docs/TRANSLATION.md" "$root/symlink/source"
ln -s "$root/symlink/source" "$root/symlink/artifact/docs/TRANSLATION.md"
if bash "$apply" "$root/symlink/artifact"; then exit 1; fi
[[ $(cat docs/TRANSLATION.md) == old ]]

fixture mode
chmod 755 "$root/mode/artifact/docs/TRANSLATION.md"
if bash "$apply" "$root/mode/artifact"; then exit 1; fi
[[ $(cat docs/TRANSLATION.md) == old ]]
echo 'localization artifact isolation: PASS'
