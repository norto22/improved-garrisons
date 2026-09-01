#!/usr/bin/env zsh
set -euo pipefail

subject_dll="${1:-}"

if [[ -z "$subject_dll" || ! -f "$subject_dll" ]]; then
	print -u2 "Expected an ImprovedGarrisons.dll path."
	exit 2
fi

integration_dll="${subject_dll:h}/Adapters/ImprovedGarrisons.CoopIntegration.dll"
if [[ ! -f "$integration_dll" ]]; then
	print -u2 "Expected the Coop integration adapter beside ImprovedGarrisons.dll."
	exit 2
fi

decompile_dir="$(rtk mktemp -d)"
trap 'rtk rm -rf "$decompile_dir"' EXIT
export DOTNET_ROOT=/home/vscode/.dotnet
export PATH=/home/vscode/.dotnet:/home/vscode/.dotnet/tools:$PATH

rtk ilspycmd -p -lv CSharp7_3 --disable-updatecheck -o "$decompile_dir/main" "$subject_dll"
rtk ilspycmd -p -lv CSharp7_3 --disable-updatecheck -o "$decompile_dir/integration" "$integration_dll"

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

state_store="$decompile_dir/integration/ImprovedGarrisons.CoopIntegration.Persistence/SettingsStateStore.cs"
patches="$decompile_dir/integration/ImprovedGarrisons.CoopIntegration.Patching/ClientServerPatches.cs"
option_vm="$decompile_dir/main/ImprovedGarrisons.ImprovedGarrisonsUI.UIElements/ImprovedGarrisonsOptionVM.cs"
training_vm="$decompile_dir/main/ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus/TrainingUIVM.cs"
recruitment_vm="$decompile_dir/main/ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus/RecruitmentUIVM.cs"
guards_vm="$decompile_dir/main/ImprovedGarrisons.ImprovedGarrisonsUI.SubMenus/GuardsUIVM.cs"
gauntlet="$decompile_dir/main/ImprovedGarrisons.ImprovedGarrisonsUI/ImprovedGarrisonsUIGauntlet.cs"
root_vm="$decompile_dir/main/ImprovedGarrisons.ImprovedGarrisonsUI/ImprovedGarrisonsUIVM.cs"

failures=0
record_failure() {
	print -u2 -- "FAIL $1"
	(( failures += 1 ))
}

apply_state="$(rtk sed -n '/public static void ApplyState(/,/public static void MarkDirty()/p' "$state_store")"
if ! print -r -- "$apply_state" | rtk rg -q 'ForceFullRefresh'; then
	record_failure "H1: authoritative settings sync does not request a full active-view refresh."
fi

option_refresh="$(rtk sed -n '/public override void RefreshValues()/,/public void OnPress()/p' "$option_vm")"
if ! rtk rg -q '_refreshBooleanValue' "$option_vm" ||
   ! rtk rg -q '_refreshFloatValue' "$option_vm" ||
   ! print -r -- "$option_refresh" | rtk rg -q '_optionBooleanValue =' ||
   ! print -r -- "$option_refresh" | rtk rg -q '_optionFloatValue ='; then
	record_failure "H5: option rows do not read current boolean and slider values from the model."
fi
if print -r -- "$option_refresh" | rtk rg -q '_on(Boolean|Float)ChangeAction'; then
	record_failure "H5: model-to-view option refresh invokes a user change callback."
fi

training_refresh="$(rtk sed -n '/public override void RefreshValues()/,$p' "$training_vm")"
recruitment_refresh="$(rtk sed -n '/public override void RefreshValues()/,$p' "$recruitment_vm")"
guards_refresh="$(rtk sed -n '/public override void RefreshValues()/,$p' "$guards_vm")"
if ! print -r -- "$training_refresh" | rtk rg -U -q 'TrainingSettingsVM[[:space:][:print:]]*RefreshValues'; then
	record_failure "H5: Training does not refresh its visible option rows."
fi
if ! print -r -- "$recruitment_refresh" | rtk rg -U -q 'RecruitmentSettings[[:space:][:print:]]*RefreshValues' ||
   ! print -r -- "$recruitment_refresh" | rtk rg -q 'ToggleRegionRecruitment[^\n]*RefreshValues'; then
	record_failure "H5: Recruitment does not refresh its visible and standalone option rows."
fi
if ! print -r -- "$guards_refresh" | rtk rg -U -q 'GuardSettings[[:space:][:print:]]*RefreshValues' ||
   ! print -r -- "$guards_refresh" | rtk rg -q 'ToggleAutoGuardCreation[^\n]*RefreshValues'; then
	record_failure "H5: Guards does not refresh its visible and standalone option rows."
fi

compact_close="$(rtk sed -n '/public void CloseCompactUI()/,/public void CloseUi()/p' "$gauntlet")"
normal_close="$(rtk sed -n '/public void CloseUi()/,/public void UpdateUiContents()/p' "$gauntlet")"
current_tab_refresh="$(rtk sed -n '/public void UpdateCurrentUiTab()/,/public void UpdateSettlementSelector()/p' "$gauntlet")"
if ! print -r -- "$current_tab_refresh" | rtk rg -U -q '_compactDatsource[[:space:][:print:]]*RefreshValues' ||
   ! print -r -- "$current_tab_refresh" | rtk rg -U -q '_datasource[[:space:][:print:]]*RefreshValues'; then
	record_failure "H6: the refresh pump does not target whichever normal or compact datasource is live."
fi
if ! print -r -- "$compact_close" | rtk rg -q '_compactLayer = null' ||
   ! print -r -- "$compact_close" | rtk rg -q '_compactDatsource = null' ||
   ! print -r -- "$normal_close" | rtk rg -q 'CloseCompactUI'; then
	record_failure "H6: compact UI teardown can leave its layer or datasource attached."
fi

if ! rtk rg -q '"ExecuteAdd"[^\n]*ExecuteAddPrefix' "$patches" ||
   ! rtk rg -q '"ExecuteRemove"[^\n]*ExecuteRemovePrefix' "$patches"; then
	record_failure "H7: template +/- methods are not patched on connected Coop clients."
fi
template_delta="$(rtk sed -n '/public static bool ExecuteAddPrefix/,/public static bool SetTemplatePrefix/p' "$patches")"
h7_forwarding_ok=true
for required_pattern in 'Hero\.MainHero\.Clan' 'Show\(' 'SettingsIntentKind\.AdjustTemplateCount' 'IntegrationTransport\.SendIntent'; do
	if ! print -r -- "$template_delta" | rtk rg -q "$required_pattern"; then
		h7_forwarding_ok=false
	fi
done
if print -r -- "$template_delta" | rtk rg -q 'AddOrUpdateCharacter'; then
	h7_forwarding_ok=false
fi
if [[ "$h7_forwarding_ok" != true ]]; then
	record_failure "H7: template +/- is not forwarded without local mutation and blocked for non-owned settlements."
fi

root_refresh="$(rtk sed -n '/public override void RefreshValues()/,$p' "$root_vm")"
overview_case="$(print -r -- "$root_refresh" | rtk sed -n '/case "OverviewTab":/,/case "TrainingTab":/p')"
if ! print -r -- "$overview_case" | rtk rg -q 'OverviewDatasource[^\n]*RefreshValues' ||
   rtk rg -q 'Campaign\.Current\.TimeControlMode' "$root_vm"; then
	record_failure "H8: active Overview refresh is still suppressed while campaign time is stopped."
fi

if (( failures > 0 )); then
	print -u2 -- "FAIL test_authoritative_ui_updates_stale_views_without_local_divergence ($failures contract failures)"
	exit 1
fi

print "PASS test_authoritative_ui_updates_stale_views_without_local_divergence"
