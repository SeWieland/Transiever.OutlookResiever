#!/usr/bin/env bash
set -euo pipefail

version="${1:?Usage: build-release-assets.sh <version>}"
project="src/Transiever.OutlookResiever.Cli/Transiever.OutlookResiever.Cli.csproj"
artifacts="artifacts"
output="$artifacts/publish/win-x64"

mkdir -p "$artifacts" "$output"

dotnet publish "$project" --configuration Release --runtime win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:PublishTrimmed=false -p:Version="$version" \
  --output "$output"

(
  cd "$output"
  7z a -tzip "../../olrx-win-x64.zip" .
)
