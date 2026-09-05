#!/usr/bin/env zsh
set -euo pipefail

subject_dll="${1:-}"
repo_root="${0:A:h:h:h}"
runtime_log="$repo_root/tests/coop-compatibility/fixtures/original-get-id-parts-nre.txt"
integration_dll="${subject_dll:h}/Adapters/ImprovedGarrisons.CoopIntegration.dll"
bootstrap_dll="${subject_dll:h}/ImprovedGarrisons.CoopBootstrap.dll"
module_dir="${subject_dll:h:h:h}"
server_engine="$repo_root/VanillaModuleFiles/BannerlordCoop/DedicatedServer/engine"
harness="$repo_root/tests/coop-compatibility/ImprovedGarrisons.CoopCompatibilityHarness.csproj"

if [[ -z "$subject_dll" || ! -f "$subject_dll" ]]; then
  print -u2 "Expected an ImprovedGarrisons.dll path."
  exit 2
fi

rtk rg -q 'CampaignObjectType.*GetIdParts' "$runtime_log"
rtk rg -q 'PartyManager.InitializeNewParty' "$runtime_log"

if [[ ! -f "$integration_dll" ]]; then
  print -u2 "The comprehensive Coop integration assembly is missing."
  exit 1
fi
if [[ ! -f "$bootstrap_dll" ]]; then
  print -u2 "The dependency-free Coop bootstrap assembly is missing."
  exit 1
fi

decompile_dir="$(mktemp -d)"
trap 'rm -rf "$decompile_dir"' EXIT
export DOTNET_ROOT=/home/vscode/.dotnet
export PATH=/home/vscode/.dotnet:/home/vscode/.dotnet/tools:$PATH

rtk ilspycmd -p -lv CSharp7_3 --disable-updatecheck -o "$decompile_dir/main" "$subject_dll"
rtk ilspycmd -p -lv CSharp7_3 --disable-updatecheck -o "$decompile_dir/integration" "$integration_dll"
rtk ilspycmd -p -lv CSharp7_3 --disable-updatecheck -o "$decompile_dir/bootstrap" "$bootstrap_dll"

party_manager="$decompile_dir/main/ImprovedGarrisons.AI.AIManagers/PartyManager.cs"
village_manager="$decompile_dir/main/ImprovedGarrisons.AI.AIManagers/VillageRecruitPartyManager.cs"
transfer_manager="$decompile_dir/main/ImprovedGarrisons.AI.AIManagers/TransferPartyManager.cs"
recruiter_manager="$decompile_dir/main/ImprovedGarrisons.AI.AIManagers/GarrisonRecruiterPartyManager.cs"
runtime_source="$decompile_dir/integration/ImprovedGarrisons.CoopIntegration.Runtime/IntegrationRuntime.cs"
bootstrap_source="$decompile_dir/bootstrap/ImprovedGarrisons.CoopBootstrap/IntegrationSubModule.cs"

if rtk rg -q 'EnterPartyCreationScope|CoopCompatibilityService' "$party_manager"; then
  print -u2 "The removed narrow AllowedThread coordinator is still present."
  exit 1
fi

rtk rg -q 'InitializeNewParty' "$village_manager"
rtk rg -q '== null' "$village_manager"
rtk rg -q 'InitializeNewParty' "$transfer_manager"
rtk rg -q '== null' "$transfer_manager"
rtk rg -q 'string.IsNullOrEmpty' "$recruiter_manager"
rtk rg -q 'InitializeNewPartyPrefix' "$decompile_dir/integration"
rtk rg -q 'IntegrationRuntime.IsServer' "$decompile_dir/integration"
if ! rtk rg -q 'CoopMobilePartyRegistration' "$decompile_dir/integration"; then
  print -u2 "The runtime does not gate server simulation on Coop-native MobileParty registration readiness."
  exit 1
fi
rtk rg -q 'IAutoRegistryFactory' "$decompile_dir/integration"
rtk rg -q 'IsManaged' "$decompile_dir/integration"
rtk rg -q 'PatchAll' "$decompile_dir/integration"
rtk rg -q 'GetPatchInfo' "$decompile_dir/integration"
rtk rg -q 'native-mobile-party-registry-ready' "$decompile_dir/integration" "$bootstrap_source"
if ! rtk rg -q 'if \(IsServer && !CoopMobilePartyRegistration\.EnsureReady' "$runtime_source"; then
  print -u2 "The client still runs the server-only MobileParty registry repair before loading the host campaign."
  exit 1
fi

if rtk rg -q 'IntegrationTransport\.IsConnected && !_patchesApplied' "$runtime_source"; then
  print -u2 "Client/server isolation patches are still gated on the IG transport handshake."
  exit 1
fi
if rtk rg -q '_activationFailures >= 5' "$bootstrap_source"; then
  print -u2 "The bootstrap still permanently abandons runtime activation after five failures."
  exit 1
fi
if rtk rg -q 'ActionRequest|ActionAck' "$decompile_dir/integration"; then
  print -u2 "The removed generic Improved Garrisons request/ack protocol is still deployed."
  exit 1
fi
rtk rg -q 'SendInformationMessage' "$decompile_dir/integration"
rtk rg -q 'Fortify' "$decompile_dir/integration"
rtk rg -q 'StatusText' "$decompile_dir/integration"
if rtk rg -q 'GuardUnsupportedPrefix|RecruiterUnsupportedPrefix|TransferBegin|TransferCommit|TransferAbort|PendingTransfer' "$decompile_dir/integration"; then
  print -u2 "The integration still drops supported actions or ships the unusable transfer-session protocol."
  exit 1
fi
rtk rg -q 'IConnectionCollection' "$decompile_dir/integration"
rtk rg -q 'HasCompletedCampaignSynchronization' "$decompile_dir/integration"
if rtk rg -q 'CompatibilityHello|CompatibilityResult|CompatibilityReady|CampaignReadyHandler|CompatiblePeers|ReadyPeers|_compatibilityReady' "$decompile_dir/integration"; then
  print -u2 "The integration still uses a second service-readiness protocol instead of Coop's dedicated-server connection state."
  exit 1
fi
if rtk rg -q 'MobileParty[^\n]*StringId\s*=' "$decompile_dir/integration"; then
  print -u2 "The integration assigns a manual MobileParty StringId instead of using Coop's native registry."
  exit 1
fi

rtk /home/vscode/.dotnet/dotnet run --project "$harness" --configuration Release -- "$module_dir" "$server_engine"

print "PASS test_initialize_new_party_coop_blocked_stringid_is_preserved (server-authoritative boundary)"
