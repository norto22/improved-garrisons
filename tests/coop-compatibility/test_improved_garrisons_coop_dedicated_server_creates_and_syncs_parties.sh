#!/usr/bin/env zsh
set -euo pipefail

module_dir="${1:-}"
repo_root="${0:A:h:h:h}"
client_bin="$module_dir/bin/Win64_Shipping_Client"
server_bin="$module_dir/bin/Win64_Shipping_Server"
bootstrap_dll="$client_bin/ImprovedGarrisons.CoopBootstrap.dll"
runtime_dll="$client_bin/Adapters/ImprovedGarrisons.CoopIntegration.dll"
narrow_dll="$client_bin/ImprovedGarrisons.CoopAdapter.dll"
harness="$repo_root/tests/coop-compatibility/ImprovedGarrisons.CoopCompatibilityHarness.csproj"

if [[ -z "$module_dir" || ! -d "$module_dir" ]]; then
  print -u2 "Expected the deployable ImprovedGarrisons module directory."
  exit 2
fi

if [[ -f "$narrow_dll" ]]; then
  print -u2 "FAIL test_improved_garrisons_coop_dedicated_server_creates_and_syncs_parties: conflicting narrow CoopAdapter is still deployed."
  exit 1
fi

server_engine="$repo_root/VanillaModuleFiles/BannerlordCoop/DedicatedServer/engine"
rtk /home/vscode/.dotnet/dotnet run --project "$harness" --configuration Release -- "$module_dir" "$server_engine"

for required in ImprovedGarrisons.dll ImprovedGarrisons.CoopBootstrap.dll; do
  [[ -f "$client_bin/$required" ]]
  [[ -f "$server_bin/$required" ]]
  rtk cmp -s "$client_bin/$required" "$server_bin/$required"
done
[[ -f "$runtime_dll" ]]
[[ -f "$server_bin/Adapters/ImprovedGarrisons.CoopIntegration.dll" ]]
rtk cmp -s "$runtime_dll" "$server_bin/Adapters/ImprovedGarrisons.CoopIntegration.dll"

decompile_dir="$(mktemp -d)"
trap 'rm -rf "$decompile_dir"' EXIT
export DOTNET_ROOT=/home/vscode/.dotnet
export PATH=/home/vscode/.dotnet:/home/vscode/.dotnet/tools:$PATH

rtk ilspycmd -p -lv CSharp7_3 --disable-updatecheck -o "$decompile_dir/main" "$client_bin/ImprovedGarrisons.dll"
rtk ilspycmd -p -lv CSharp7_3 --disable-updatecheck -o "$decompile_dir/bootstrap" "$bootstrap_dll"
rtk ilspycmd -p -lv CSharp7_3 --disable-updatecheck -o "$decompile_dir/runtime" "$runtime_dll"

main_party_manager="$decompile_dir/main/ImprovedGarrisons.AI.AIManagers/PartyManager.cs"
bootstrap_project="$decompile_dir/bootstrap/ImprovedGarrisons.CoopBootstrap.csproj"
runtime_project="$decompile_dir/runtime/ImprovedGarrisons.CoopIntegration.csproj"

if rtk rg -q 'EnterPartyCreationScope|CoopCompatibilityService' "$main_party_manager"; then
  print -u2 "Conflicting AllowedThread coordinator remains in PartyManager."
  exit 1
fi

rtk rg -q 'CreateGuards' "$decompile_dir/runtime"
rtk rg -q 'CreateRecruiter' "$decompile_dir/runtime"
rtk rg -q 'TransferDirect' "$decompile_dir/runtime"
rtk rg -q 'SetUpgradePath' "$decompile_dir/runtime"
rtk rg -q 'Fortify' "$decompile_dir/runtime"
rtk rg -q 'PartyManifest' "$decompile_dir/runtime"
rtk rg -q 'StatusText' "$decompile_dir/runtime"
rtk rg -q 'SendInformationMessage' "$decompile_dir/runtime"
rtk rg -q 'ContainerProvider' "$decompile_dir/runtime"
rtk rg -q 'CoopMobilePartyRegistration' "$decompile_dir/runtime"
rtk rg -q 'IAutoRegistryFactory' "$decompile_dir/runtime"
rtk rg -q 'native-mobile-party-registry-ready' "$decompile_dir/runtime" "$decompile_dir/bootstrap"
rtk rg -q 'IConnectionCollection' "$decompile_dir/runtime"
rtk rg -q 'HasCompletedCampaignSynchronization' "$decompile_dir/runtime"
rtk rg -q 'IClientLogic' "$decompile_dir/runtime"
rtk rg -q 'SendToSynchronizedPeers' "$decompile_dir/runtime"
if rtk rg -q 'CompatibilityHello|CompatibilityResult|CompatibilityReady|CampaignReadyHandler|CompatiblePeers|ReadyPeers|_compatibilityReady' "$decompile_dir/runtime"; then
  print -u2 "Improved Garrisons still has a second compatibility/service-readiness gate outside BannerlordCoop's campaign state."
  exit 1
fi
if rtk rg -q '_network\.SendAll\(new (PartyManifest|StateSync|ServerHealth)' "$decompile_dir/runtime"; then
  print -u2 "The server broadcasts Improved Garrisons state without Coop's synchronized-peer routing."
  exit 1
fi
if rtk rg -q 'ActionRequest|ActionAck' "$decompile_dir/runtime"; then
  print -u2 "The generic Improved Garrisons request/ack protocol must not ship."
  exit 1
fi
if rtk rg -q 'GuardUnsupportedPrefix|RecruiterUnsupportedPrefix' "$decompile_dir/runtime"; then
  print -u2 "Guard fortify or recruiter culture conversation actions are still dropped locally."
  exit 1
fi
if rtk rg -q 'TransferBegin|TransferCommit|TransferAbort|PendingTransfer' "$decompile_dir/runtime"; then
  print -u2 "The unusable multi-stage transfer protocol is still deployed."
  exit 1
fi
if rtk rg -q 'TcpListener|HttpListener|UdpClient|NamedPipeServerStream|WebApplication|Kestrel' "$decompile_dir/runtime" "$decompile_dir/bootstrap"; then
  print -u2 "Improved Garrisons still contains an alternate server or transport implementation."
  exit 1
fi
rtk rg -q 'ImprovedGarrisons\.CoopIntegration\.dll' "$decompile_dir/bootstrap"
rtk rg -q 'ImprovedGarrisons\.Main' "$decompile_dir/bootstrap"
if rtk rg -q 'DedicatedServer\.Windows|DedicatedServerSubModule|CoopServerHost|ActivateDedicatedServer|InvokeDedicatedServer' "$decompile_dir/bootstrap"; then
  print -u2 "The bootstrap still replaces or manually hosts BannerlordCoop's dedicated server."
  exit 1
fi

transport_source="$decompile_dir/runtime/ImprovedGarrisons.CoopIntegration.Runtime/IntegrationTransport.cs"
if ! rtk sed -n '/SendActionOutcome/,/HandleConfigRequest/p' "$transport_source" | rtk rg -U -q 'network\.Send[[:space:][:print:]]*SendInformationMessage'; then
  print -u2 "Server action results are not using BannerlordCoop's notification command."
  exit 1
fi
if ! rtk sed -n '/DispatchIntent/,/HandleConfigRequest/p' "$transport_source" | rtk rg -q 'HasCompletedCampaignSynchronization'; then
  print -u2 "The dedicated server does not gate Improved Garrisons intents on Coop campaign synchronization."
  exit 1
fi
if rtk sed -n '/public static string SendIntent/,/public static void BroadcastManifest/p' "$transport_source" | rtk rg -q 'compatib|service'; then
  print -u2 "Client intents are still blocked on a separate Improved Garrisons service gate."
  exit 1
fi

conversation_source="$decompile_dir/runtime/ImprovedGarrisons.CoopIntegration.Patching/ConversationPatches.cs"
rtk rg -q 'PartyIntentKind\.Fortify' "$conversation_source"
rtk rg -q 'PromptChangeRecruitmentCulture' "$conversation_source"
if ! rtk sed -n '/HandleConfigRequest/,/HandleActionAck/p' "$transport_source" | rtk rg -q 'GameThread\.RunSafe'; then
  print -u2 "ConfigRequest still reads live campaign state from the Coop network thread."
  exit 1
fi

if rtk rg -q 'Reference Include="(Common|GameInterface|LiteNetLib|protobuf-net.Core|Serilog)"' "$bootstrap_project"; then
  print -u2 "Bootstrap has an early Coop runtime dependency."
  exit 1
fi
rtk rg -q 'Reference Include="Common"' "$runtime_project"
rtk rg -q 'Reference Include="GameInterface"' "$runtime_project"
rtk rg -q 'Reference Include="Coop.Core"' "$runtime_project"
if rtk rg -q 'Reference Include="CoopModPatch"' "$runtime_project"; then
  print -u2 "Direct runtime still references CoopModPatch."
  exit 1
fi
if rtk rg -q 'forceHeadlessModules|rollingSaveVersionModules|modded-server-modules|Environment\.Exit' "$decompile_dir/bootstrap"; then
  print -u2 "Bootstrap still mutates the dedicated server outside the Improved Garrisons module."
  exit 1
fi

rtk rg -q '<Version value="v[0-9]+\.[0-9]+\.[0-9]+"' "$module_dir/SubModule.xml"
rtk rg -q '<ModuleType value="Official"' "$module_dir/SubModule.xml"
if [[ ! -f "$module_dir/ServerInstall/DedicatedServer.Windows.SubModule.xml" ]]; then
  print -u2 "ServerInstall/DedicatedServer.Windows.SubModule.xml must ship; it is the only manifest that declares the Improved Garrisons bootstrap inside DedicatedServer.Windows."
  exit 1
fi
rtk rg -q '<DLLName value="ImprovedGarrisons.CoopBootstrap.dll"/>' "$module_dir/ServerInstall/DedicatedServer.Windows.SubModule.xml"
rtk rg -q '<SubModuleClassType value="ImprovedGarrisons.CoopBootstrap.IntegrationSubModule"/>' "$module_dir/ServerInstall/DedicatedServer.Windows.SubModule.xml"
if [[ ! -f "$module_dir/SERVER-INSTALL.txt" ]]; then
  print -u2 "SERVER-INSTALL.txt must ship with instructions for applying the DedicatedServer.Windows manifest overlay."
  exit 1
fi
if rtk rg -q 'CoopModPatch|CoopHost|Adapter\.ImprovedGarrisons' "$module_dir/SubModule.xml" "$client_bin" "$server_bin"; then
  print -u2 "Abandoned CoopModPatch host artifacts are still packaged."
  exit 1
fi
if rtk rg -q '<DLLName value="ImprovedGarrisons.CoopIntegration.dll"' "$module_dir/SubModule.xml"; then
  print -u2 "Coop-dependent runtime must not be declared as a Bannerlord submodule."
  exit 1
fi
rtk rg -q '<DLLName value="ImprovedGarrisons.CoopBootstrap.dll"' "$module_dir/SubModule.xml"
rtk rg -q '<Tag key="DedicatedServerType" value="none"' "$module_dir/SubModule.xml"
rtk rg -q '<Tag key="DedicatedServerType" value="custom"' "$module_dir/SubModule.xml"
rtk rg -q '<Tag key="IsNoRenderModeElement" value="false"' "$module_dir/SubModule.xml"
if rtk rg -q '<Tag key="IsNoRenderModeElement" value="true"' "$module_dir/SubModule.xml"; then
  print -u2 "Bannerlord v1.4.8 rejects IsNoRenderModeElement=true submodules."
  exit 1
fi
rtk rg -q 'modGameVersion = "v1.4.8."' "$decompile_dir/main/ImprovedGarrisons/Main.cs"
rtk zsh "$repo_root/tests/coop-runtime-contract/test_shipped_integration_binds_to_real_coop_network_contract.sh" "$module_dir"

print "PASS test_improved_garrisons_coop_native_registry_creates_and_syncs_parties (binary/manifest boundary)"
