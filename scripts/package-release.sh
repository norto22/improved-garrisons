#!/usr/bin/env bash
set -euo pipefail

release_script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
release_repo_root="$(cd -- "$release_script_dir/.." && pwd)"
release_manifest="$release_repo_root/.github/release-files.txt"
release_output_dir="${1:-$release_repo_root/dist}"
release_label="${2:-}"

if [[ ! -f "$release_manifest" ]]; then
    printf 'Release manifest is missing: %s\n' "$release_manifest" >&2
    exit 1
fi

module_version="$(sed -n 's/.*<Version value="\(v[^"]*\)".*/\1/p' "$release_repo_root/ImprovedGarrisons/SubModule.xml" | head -n 1)"
if [[ ! "$module_version" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    printf 'SubModule.xml has an invalid release version: %s\n' "$module_version" >&2
    exit 1
fi

if [[ -z "$release_label" ]]; then
    release_label="$module_version"
fi
if [[ ! "$release_label" =~ ^[A-Za-z0-9._-]+$ ]]; then
    printf 'Release label contains unsafe filename characters: %s\n' "$release_label" >&2
    exit 1
fi

mkdir -p -- "$release_output_dir"
release_output_dir="$(cd -- "$release_output_dir" && pwd)"
release_stage="$(mktemp -d)"
release_expected="$(mktemp)"
release_actual="$(mktemp)"
release_tracked="$(mktemp)"
trap 'rm -rf -- "$release_stage"; rm -f -- "$release_expected" "$release_actual" "$release_tracked"' EXIT INT TERM

sed -e '/^[[:space:]]*#/d' -e '/^[[:space:]]*$/d' "$release_manifest" | sort > "$release_expected"
if [[ -s "$release_expected" ]] && [[ -n "$(uniq -d "$release_expected")" ]]; then
    printf 'Release manifest contains duplicate paths.\n' >&2
    uniq -d "$release_expected" >&2
    exit 1
fi

git -C "$release_repo_root" ls-files ImprovedGarrisons | sort > "$release_tracked"
if ! diff -u -- "$release_expected" "$release_tracked"; then
    printf 'Tracked module files differ from the release allowlist; review .github/release-files.txt.\n' >&2
    exit 1
fi

while IFS= read -r release_path; do
    release_source="$release_repo_root/$release_path"
    release_destination="$release_stage/$release_path"
    if [[ ! -f "$release_source" ]]; then
        printf 'Required release file is missing: %s\n' "$release_path" >&2
        exit 1
    fi

    mkdir -p -- "$(dirname -- "$release_destination")"
    cp -- "$release_source" "$release_destination"
done < "$release_expected"

(
    cd -- "$release_stage"
    find ImprovedGarrisons -type f -print | sort
) > "$release_actual"
if ! diff -u -- "$release_expected" "$release_actual"; then
    printf 'Staged release contents differ from the allowlist.\n' >&2
    exit 1
fi

cmp --silent \
    "$release_stage/ImprovedGarrisons/bin/Win64_Shipping_Client/ImprovedGarrisons.dll" \
    "$release_stage/ImprovedGarrisons/bin/Win64_Shipping_Server/ImprovedGarrisons.dll"
cmp --silent \
    "$release_stage/ImprovedGarrisons/bin/Win64_Shipping_Client/ImprovedGarrisons.CoopBootstrap.dll" \
    "$release_stage/ImprovedGarrisons/bin/Win64_Shipping_Server/ImprovedGarrisons.CoopBootstrap.dll"
cmp --silent \
    "$release_stage/ImprovedGarrisons/bin/Win64_Shipping_Client/Adapters/ImprovedGarrisons.CoopIntegration.dll" \
    "$release_stage/ImprovedGarrisons/bin/Win64_Shipping_Server/Adapters/ImprovedGarrisons.CoopIntegration.dll"

release_archive="$release_output_dir/ImprovedGarrisons-$release_label.zip"
rm -f -- "$release_archive"
(
    cd -- "$release_stage"
    zip -q -9 -r "$release_archive" ImprovedGarrisons
)

unzip -Z1 "$release_archive" | sed '/\/$/d' | sort > "$release_actual"
if ! diff -u -- "$release_expected" "$release_actual"; then
    printf 'Release archive contains files outside the allowlist.\n' >&2
    exit 1
fi

printf '%s\n' "$release_archive"
