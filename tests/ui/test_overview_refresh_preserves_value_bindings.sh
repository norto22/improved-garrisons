#!/usr/bin/env zsh
set -euo pipefail

subject_dll="${1:-}"

if [[ -z "$subject_dll" || ! -f "$subject_dll" ]]; then
	print -u2 "Expected an ImprovedGarrisons.dll path."
	exit 2
fi

decompile_dir="$(rtk mktemp -d)"
trap 'rtk rm -rf "$decompile_dir"' EXIT
export DOTNET_ROOT=/home/vscode/.dotnet
export PATH=/home/vscode/.dotnet:/home/vscode/.dotnet/tools:$PATH

rtk ilspycmd -p -lv CSharp7_3 --disable-updatecheck -o "$decompile_dir/main" "$subject_dll"

overview_vm="$decompile_dir/main/ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus/OverviewUIVM.cs"
settlement_vm="$decompile_dir/main/ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus.OverviewUtils/SettlementItemWidgetVM.cs"

overview_refresh="$(rtk sed -n '/private void RefreshSettlements()/,/public void OnCompactModePress()/p' "$overview_vm")"
settlement_refresh="$(rtk sed -n '/public override void RefreshValues()/,/^\s*}\s*$/p' "$settlement_vm")"
stable_refresh="$(print -r -- "$overview_refresh" | rtk sed -n '/Count == .*Count.*All/,/MBBindingList<SettlementItemWidgetVM>/p')"

if ! print -r -- "$stable_refresh" | rtk rg -U -q 'foreach .*SettlementItemWidgetVM.*Settlements[[:space:][:print:]]*RefreshValues[[:space:][:print:]]*return;'; then
	print -u2 "Overview refresh recreates settlement rows instead of refreshing their existing value bindings."
	exit 1
fi

if print -r -- "$stable_refresh" | rtk rg -q 'Settlements ='; then
	print -u2 "Overview refresh replaces the bound settlement collection on its stable-fief path."
	exit 1
fi

if ! print -r -- "$settlement_refresh" | rtk rg -U -q 'SettlementInformation[[:space:][:print:]]*RefreshValues'; then
	print -u2 "Settlement row refresh does not update its existing money, food, garrison, and guard-party values."
	exit 1
fi

print "PASS test_overview_refresh_preserves_value_bindings"
