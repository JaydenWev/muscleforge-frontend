#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
case "${1-}" in '') check=false ;; --check) check=true ;; *) echo 'Usage: scripts/generate-api-client.sh [--check]' >&2; exit 2 ;; esac
[ "$#" -le 1 ] || exit 2
expected=$(sed -n 's/.*"contractSha256": "\([0-9a-f]*\)".*/\1/p' contracts/source.json)
if command -v sha256sum >/dev/null 2>&1; then
    actual=$(sha256sum contracts/openapi.json | cut -d ' ' -f 1)
else
    actual=$(shasum -a 256 contracts/openapi.json | cut -d ' ' -f 1)
fi
[ "$actual" = "$expected" ] || { echo 'Contract SHA-256 does not match contracts/source.json.' >&2; exit 1; }
grep -q '"generator": "NSwag.ConsoleCore"' contracts/source.json
grep -q '"generatorVersion": "14.7.1"' contracts/source.json
grep -q '"version": "14.7.1"' .config/dotnet-tools.json
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT HUP INT TERM
generate() {
    config=$1 output=$2
    dotnet tool run nswag run "$config" "/variables:Output=$work/generated.cs"
    if "$check"; then
        cmp -s "$work/generated.cs" "$output" || { echo "Generated client differs: $output" >&2; exit 1; }
    else
        cp "$work/generated.cs" "$output"
    fi
}
generate nswag.json src/MuscleForge.Client/Generated/RawApiClient.g.cs
generate nswag-fixture.json tests/MuscleForge.Mobile.UnitTests/GeneratedApiClientFixture.g.cs
echo 'Generated clients are current.'
