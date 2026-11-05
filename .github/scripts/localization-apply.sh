#!/usr/bin/env bash
set -euo pipefail
artifact=${1:?}
[[ -d $artifact && ! -L $artifact ]]
[[ $(git rev-parse HEAD) == "${GITHUB_SHA:?}" ]]
[[ -z $(git status --porcelain) ]]

# Artifact は信頼しない。既存の通常ファイルのみを、全件検査後に取り込む。
for dir in docs src src/Resources src/Resources/Locales; do
  [[ -d $dir && ! -L $dir ]]
done
while IFS= read -r -d '' entry; do
  relative=${entry#"$artifact"/}
  [[ ! -L $entry ]]
  if [[ -d $entry ]]; then
    case $relative in docs|src|src/Resources|src/Resources/Locales) ;; *) exit 1 ;; esac
    [[ $(stat -c %a "$entry") == 755 ]]
  elif [[ -f $entry ]]; then
    case $relative in
      docs/TRANSLATION.md) ;;
      src/Resources/Locales/*.axaml)
        name=${relative#src/Resources/Locales/}
        [[ $name =~ ^[A-Za-z0-9_.-]+\.axaml$ ]] || exit 1 ;;
      *) exit 1 ;;
    esac
    [[ $(stat -c %a "$entry") == 644 ]]
    [[ -f $relative && ! -L $relative ]]
    [[ $(git ls-files -s -- "$relative") == "100644 "* ]]
  else
    exit 1
  fi
done < <(find "$artifact" -mindepth 1 -print0)

while IFS= read -r -d '' path; do
  [[ -f $artifact/$path && ! -L $artifact/$path ]]
done < <(git ls-files -z -- docs/TRANSLATION.md 'src/Resources/Locales/*.axaml')

validator_dir=$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:?}}/localization-validator.XXXXXXXX")
cp -- "$(dirname "${BASH_SOURCE[0]}")/localization-validate.cs" "$validator_dir/validator.cs"
DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_NOLOGO=1 \
  dotnet run --property:UseSharedCompilation=false "$validator_dir/validator.cs" -- "$artifact"

while IFS= read -r -d '' path; do
  cp -- "$artifact/$path" "$path"
done < <(git ls-files -z -- docs/TRANSLATION.md 'src/Resources/Locales/*.axaml')
