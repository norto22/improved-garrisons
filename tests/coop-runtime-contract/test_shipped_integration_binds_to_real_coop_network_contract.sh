#!/usr/bin/env zsh
set -euo pipefail

repo_root="${0:A:h:h:h}"
module_dir="${1:-$repo_root/ImprovedGarrisons}"
module_dir="${module_dir:A}"
project="$repo_root/tests/coop-runtime-contract/ImprovedGarrisons.CoopRuntimeContract.csproj"
host_project="$repo_root/tests/coop-runtime-contract/host/ImprovedGarrisons.CoopRuntimeContract.Host.csproj"
worker="$repo_root/tests/coop-runtime-contract/bin/Release/net8.0/ImprovedGarrisons.CoopRuntimeContract.dll"

rtk /home/vscode/.dotnet/dotnet build "$project" --configuration Release --verbosity quiet --no-incremental -p:ModuleRoot="$module_dir"
rtk /home/vscode/.dotnet/dotnet run --project "$host_project" --configuration Release -- "$worker" "$repo_root"
