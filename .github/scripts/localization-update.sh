#!/usr/bin/env bash
set -euo pipefail

context=kagayoi/automation-merge-guard-v1
if [[ ${1:-} == --guard-config ]]; then
  client=${GUARD_CLIENT_ID:-} app=${GUARD_APP_ID:-} key=${GUARD_PRIVATE_KEY:-} slug=${GUARD_APP_SLUG:-} bot=${GUARD_BOT_ID:-}
  configured=false
  if [[ -n $client || -n $app || -n $key || -n $slug || -n $bot ]]; then
    [[ $client =~ ^[A-Za-z0-9._-]{5,128}$ && $app =~ ^[1-9][0-9]{0,14}$ && -n $key && $slug =~ ^[a-z0-9]+(-[a-z0-9]+)*$ && $bot =~ ^[1-9][0-9]{0,14}$ ]] || {
      echo 'Incomplete or invalid guard App configuration' >&2; exit 1;
    }
    configured=true
  fi
  helper_dir=$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:?}}/localization-guard.XXXXXXXX")
  cp -- "$(dirname "${BASH_SOURCE[0]}")/localization-guard.cs" "$helper_dir/guard.cs"
  helper="$helper_dir/guard.cs"
  export DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_NOLOGO=1
  rule=$(dotnet run --property:UseSharedCompilation=false --property:PublishAot=false "$helper" -- rules)
  echo "helper=$helper" >> "${GITHUB_OUTPUT:?}"
  case $rule in
    none)
      if $configured; then echo 'Guard App configured without required rule' >&2; exit 1; fi
      echo 'enabled=false' >> "${GITHUB_OUTPUT:?}" ;;
    "id:$app")
      if ! $configured; then echo 'Guard App is required but not configured' >&2; exit 1; fi
      echo 'enabled=true' >> "${GITHUB_OUTPUT:?}" ;;
    *) echo 'Guard rule source is missing, mismatched, or duplicated' >&2; exit 1 ;;
  esac
  exit 0
fi
[[ $# == 0 ]]
base=${GITHUB_SHA:?}
target="https://github.com/${GITHUB_REPOSITORY:?}/actions/runs/${GITHUB_RUN_ID:?}/attempts/${GITHUB_RUN_ATTEMPT:?}"
message='doc: Update translation status and sort locale files'
[[ $(git rev-parse HEAD) == "$base" ]]
[[ -z $(git diff --cached --name-only) ]]

# 生成以外の変更はコミットも認証もしない。
while IFS= read -r -d '' path; do
  case "$path" in
    docs/TRANSLATION.md) ;;
    src/Resources/Locales/*.axaml) [[ ${path#src/Resources/Locales/} != */* ]] || exit 1 ;;
    *) echo "Unexpected changed path: $path" >&2; exit 1 ;;
  esac
done < <(git diff --name-only -z)
git add -- docs/TRANSLATION.md 'src/Resources/Locales/*.axaml'
if git diff --cached --quiet; then
  echo 'No changes to commit'
  exit 0
fi
tree=$(git write-tree)
git -c user.name='github-actions[bot]' -c user.email='github-actions[bot]@users.noreply.github.com' commit -m "$message"
commit=$(git rev-parse HEAD)
[[ $(git show -s --format=%P "$commit") == "$base" ]]
[[ $(git rev-parse "$commit^{tree}") == "$tree" ]]
[[ $(git show -s --format=%B "$commit") == "$message" ]]
while IFS= read -r -d '' path; do
  case "$path" in
    docs/TRANSLATION.md) ;;
    src/Resources/Locales/*.axaml) [[ ${path#src/Resources/Locales/} != */* ]] || exit 1 ;;
    *) echo "Unexpected committed path: $path" >&2; exit 1 ;;
  esac
done < <(git diff-tree --no-commit-id --name-only -r -z "$base" "$commit")

ref="refs/heads/automation/localization-$(cat /proc/sys/kernel/random/uuid)"
created=false
cleanup=true
finish() {
  local rc=$?
  trap - EXIT
  if $created && $cleanup; then
    if ! git push "--force-with-lease=$ref:$commit" origin ":$ref"; then
      echo "Temporary ref cleanup failed; inspect $ref" >&2
      rc=1
    fi
  elif $created; then
    echo "Main push or verification uncertain; retained $ref at expected $commit" >&2
  fi
  exit "$rc"
}
trap finish EXIT
# checkout は認証を保存しない。Git の認証情報はこの短い step 内だけに渡す。
export GIT_CONFIG_COUNT=1
export GIT_CONFIG_KEY_0='http.https://github.com/.extraheader'
export GIT_CONFIG_VALUE_0="AUTHORIZATION: basic $(printf 'x-access-token:%s' "${GH_TOKEN:?}" | base64 -w0)"
if ! git push "--force-with-lease=$ref:" origin "$commit:$ref"; then
  echo "Temporary ref creation failed or is uncertain; inspect $ref" >&2
  exit 1
fi
created=true
[[ $(git ls-remote --refs origin "$ref") == "$commit"$'\t'"$ref" ]]

if [[ ${GUARD_ENABLED:-false} == true ]]; then
  app=${GUARD_APP_ID:?} slug=${GUARD_APP_SLUG:?} bot_id=${GUARD_BOT_ID:?} client=${GUARD_CLIENT_ID:?}
  [[ $app =~ ^[1-9][0-9]{0,14}$ && $slug =~ ^[a-z0-9]+(-[a-z0-9]+)*$ && $slug == "${GUARD_EXPECTED_SLUG:?}" && $bot_id =~ ^[1-9][0-9]{0,14}$ && $client =~ ^[A-Za-z0-9._-]{5,128}$ && -n ${GUARD_APP_TOKEN:-} ]]
  bot_login="$slug[bot]"
  app_identity=$(GH_TOKEN="$GUARD_APP_TOKEN" gh api "apps/$slug" --jq '[.id, .client_id, .slug] | @tsv')
  [[ $app_identity == "$app"$'\t'"$client"$'\t'"$slug" ]]
  bot_identity=$(GH_TOKEN="$GUARD_APP_TOKEN" gh api "users/$slug%5Bbot%5D" --jq '[.id, .login, .type] | @tsv')
  [[ $bot_identity == "$bot_id"$'\t'"$bot_login"$'\t'Bot ]]
  status_id=$(GH_TOKEN="$GUARD_APP_TOKEN" gh api --method POST "repos/$GITHUB_REPOSITORY/statuses/$commit" \
    -f state=success -f context="$context" -f target_url="$target" \
    -f description='Validated localization update' --jq '.id')
  [[ $status_id =~ ^[0-9]+$ ]]
  DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_NOLOGO=1 \
    GH_TOKEN="$GUARD_APP_TOKEN" dotnet run --property:UseSharedCompilation=false --property:PublishAot=false "${GUARD_HELPER:?}" -- status "$commit" "$status_id"
elif [[ ${GUARD_ENABLED:-false} != false ]]; then
  echo 'Invalid guard state' >&2; exit 1
fi

[[ $(git ls-remote --refs origin refs/heads/main) == "$base"$'\t'refs/heads/main ]]
# 送信結果が不明な場合は再送も ref 清掃もしない。
cleanup=false
git push "--force-with-lease=refs/heads/main:$base" origin "$commit:refs/heads/main"
git fetch --no-tags origin refs/heads/main
git merge-base --is-ancestor "$commit" FETCH_HEAD
cleanup=true
echo "Localization update verified: $commit"
