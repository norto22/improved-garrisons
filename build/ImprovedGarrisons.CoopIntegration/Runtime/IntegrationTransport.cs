using System;
using Common;
using Common.Messaging;
using Common.Network;
using Common.Serialization;
using Coop.Core.Client;
using Coop.Core.Server.Connections;
using GameInterface;
using GameInterface.Services.GameDebug.Messages;
using ImprovedGarrisons.CoopIntegration.Persistence;
using ImprovedGarrisons.CoopIntegration.Protocol;
using LiteNetLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using ClientCampaignState = Coop.Core.Client.States.CampaignState;
using ClientMissionState = Coop.Core.Client.States.MissionState;

namespace ImprovedGarrisons.CoopIntegration.Runtime
{
    internal static class IntegrationTransport
    {
        private sealed class BrokerHandlers
        {
            private readonly object lifetime = new object();

            public void OnPartyIntent(MessagePayload<PartyIntent> payload)
            {
                GC.KeepAlive(lifetime);
                HandlePartyIntent(payload);
            }

            public void OnSettingsIntent(MessagePayload<SettingsIntent> payload)
            {
                GC.KeepAlive(lifetime);
                HandleSettingsIntent(payload);
            }

            public void OnManagementIntent(MessagePayload<ManagementIntent> payload)
            {
                GC.KeepAlive(lifetime);
                HandleManagementIntent(payload);
            }

            public void OnConfigRequest(MessagePayload<ConfigRequest> payload)
            {
                GC.KeepAlive(lifetime);
                HandleConfigRequest(payload);
            }

            public void OnConfigSync(MessagePayload<ConfigSync> payload)
            {
                GC.KeepAlive(lifetime);
                HandleConfigSync(payload);
            }

            public void OnStateSync(MessagePayload<StateSync> payload)
            {
                GC.KeepAlive(lifetime);
                HandleStateSync(payload);
            }

            public void OnPartyManifest(MessagePayload<PartyManifest> payload)
            {
                GC.KeepAlive(lifetime);
                HandlePartyManifest(payload);
            }

            public void OnServerHealth(MessagePayload<ServerHealth> payload)
            {
                GC.KeepAlive(lifetime);
                HandleServerHealth(payload);
            }
        }

        private const int PollIntervalMilliseconds = 2_000;
        private const int ConfigRetryIntervalMilliseconds = 2_000;
        private const int MaximumConfigAttempts = 60;
        private const uint ErrorColor = 4_292_095_020u;

        private static readonly Type[] MessageTypes =
        {
            typeof(PartyIntent),
            typeof(SettingsIntent),
            typeof(ManagementIntent),
            typeof(ConfigRequest),
            typeof(ConfigSync),
            typeof(StateSync),
            typeof(PartyManifest),
            typeof(ServerHealth)
        };

        private static readonly BrokerHandlers HandlerTarget = new BrokerHandlers();
        private static readonly Action<MessagePayload<PartyIntent>> PartyIntentHandler = HandlerTarget.OnPartyIntent;
        private static readonly Action<MessagePayload<SettingsIntent>> SettingsIntentHandler = HandlerTarget.OnSettingsIntent;
        private static readonly Action<MessagePayload<ManagementIntent>> ManagementIntentHandler = HandlerTarget.OnManagementIntent;
        private static readonly Action<MessagePayload<ConfigRequest>> ConfigRequestHandler = HandlerTarget.OnConfigRequest;
        private static readonly Action<MessagePayload<ConfigSync>> ConfigSyncHandler = HandlerTarget.OnConfigSync;
        private static readonly Action<MessagePayload<StateSync>> StateSyncHandler = HandlerTarget.OnStateSync;
        private static readonly Action<MessagePayload<PartyManifest>> PartyManifestHandler = HandlerTarget.OnPartyManifest;
        private static readonly Action<MessagePayload<ServerHealth>> ServerHealthHandler = HandlerTarget.OnServerHealth;
        private static IMessageBroker? _broker;
        private static INetwork? _network;
        private static int _nextPoll;
        private static bool _hookedAsServer;
        private static int _nextHealth;
        private static int _nextConfigRequest;
        private static int _configAttempts;
        private static bool _configReceived;

        public static bool IsConnected => _broker != null && _network != null;

        public static void Poll()
        {
            int now = Environment.TickCount;
            if (_nextPoll != 0 && unchecked(now - _nextPoll) < 0)
            {
                return;
            }

            _nextPoll = unchecked(now + PollIntervalMilliseconds);
            if (!ContainerProvider.TryResolve(out IMessageBroker broker) ||
                !ContainerProvider.TryResolve(out INetwork network) ||
                !ContainerProvider.TryResolve(out ISerializableTypeMapper mapper))
            {
                Teardown();
                return;
            }

            if (!ReferenceEquals(broker, _broker) || !ReferenceEquals(network, _network))
            {
                Teardown();
                mapper.AddTypes(MessageTypes);
                _hookedAsServer = IntegrationRuntime.IsServer;
                if (_hookedAsServer)
                {
                    broker.Subscribe(PartyIntentHandler);
                    broker.Subscribe(SettingsIntentHandler);
                    broker.Subscribe(ManagementIntentHandler);
                    broker.Subscribe(ConfigRequestHandler);
                }
                else
                {
                    broker.Subscribe(ConfigSyncHandler);
                    broker.Subscribe(StateSyncHandler);
                    broker.Subscribe(PartyManifestHandler);
                    broker.Subscribe(ServerHealthHandler);
                }

                _broker = broker;
                _network = network;
                RuntimeStatus.Write("transport-connected:" + (_hookedAsServer ? "server" : "client"));
                IntegrationLog.Information("Coop transport connected as " + (_hookedAsServer ? "server" : "client"));
                if (!_hookedAsServer)
                {
                    _configAttempts = 0;
                    _configReceived = false;
                    _nextConfigRequest = 0;
                }
            }

            if (!_hookedAsServer && IsClientInCampaign())
            {
                PollConfigRequest(now);
            }

            if (_hookedAsServer && (_nextHealth == 0 || unchecked(now - _nextHealth) >= 0))
            {
                _nextHealth = unchecked(now + 5_000);
                BroadcastServerHealth();
            }
        }

        public static string SendIntent(IServerIntent intent)
        {
            if (intent == null)
            {
                throw new ArgumentNullException(nameof(intent));
            }

            if (_network == null || IntegrationRuntime.IsServer || !IsClientInCampaign())
            {
                IntegrationLog.Warning("intent not sent because the BannerlordCoop campaign transport is not ready");
                ShowLocal("IG: could not reach the Coop server for that action. Try again once you are connected.", ErrorColor);
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(intent.OperationId))
            {
                intent.OperationId = Guid.NewGuid().ToString("N");
            }

            _network.SendAll(intent);
            IntegrationLog.Information("intent sent: " + intent.GetType().Name + " " + intent.OperationId);
            return intent.OperationId;
        }

        public static void BroadcastManifest(string serializedEntries, long revision)
        {
            if (_network != null && IntegrationRuntime.IsServer)
            {
                SendToSynchronizedPeers(new PartyManifest { SerializedEntries = serializedEntries, Revision = revision });
            }
        }

        public static void BroadcastState(string settingsText, string activityText, long revision)
        {
            if (_network != null && IntegrationRuntime.IsServer)
            {
                SendToSynchronizedPeers(new StateSync { SettingsText = settingsText, ActivityText = activityText, Revision = revision });
            }
        }

        private static void HandlePartyIntent(MessagePayload<PartyIntent> payload)
        {
            PartyIntent? intent = payload.What;
            if (intent == null)
            {
                return;
            }

            DispatchIntent(new ServerAction
            {
                OperationId = intent.OperationId,
                Kind = intent.Operation.ToString(),
                SettlementId = intent.SettlementId,
                StringArgument = intent.StringArgument,
                IntegerArgument = intent.IntegerArgument,
                BooleanArgument = intent.BooleanArgument
            }, payload.Who as NetPeer);
        }

        private static void HandleSettingsIntent(MessagePayload<SettingsIntent> payload)
        {
            SettingsIntent? intent = payload.What;
            if (intent == null)
            {
                return;
            }

            bool directSetting = intent.Operation <= SettingsIntentKind.ToggleRemoveNonTemplateTroops;
            DispatchIntent(new ServerAction
            {
                OperationId = intent.OperationId,
                Kind = directSetting ? "ApplySetting" : intent.Operation.ToString(),
                SettingOperation = (int)intent.Operation,
                SettlementId = intent.SettlementId,
                StringArgument = intent.StringArgument,
                IntegerArgument = intent.IntegerArgument,
                FloatArgument = intent.FloatArgument,
                BooleanArgument = intent.BooleanArgument,
                ArgumentKind = intent.ArgumentKind,
                ListArgument = intent.ListArgument
            }, payload.Who as NetPeer);
        }

        private static void HandleManagementIntent(MessagePayload<ManagementIntent> payload)
        {
            ManagementIntent? intent = payload.What;
            if (intent == null)
            {
                return;
            }

            DispatchIntent(new ServerAction
            {
                OperationId = intent.OperationId,
                Kind = intent.Operation.ToString(),
                SettlementId = intent.SettlementId,
                StringArgument = intent.StringArgument,
                IntegerArgument = intent.IntegerArgument,
                BooleanArgument = intent.BooleanArgument,
                ListArgument = intent.ListArgument
            }, payload.Who as NetPeer);
        }

        private static void DispatchIntent(ServerAction action, NetPeer? peer)
        {
            if (!IntegrationRuntime.IsServer || peer == null || !HasCompletedCampaignSynchronization(peer))
            {
                return;
            }

            IntegrationLog.Information("intent received: " + action.Kind + " " + action.OperationId);
            GameThread.RunSafe(
                () => SendActionOutcome(peer, ServerActionDispatcher.Dispatch(action, peer)),
                false,
                "ImprovedGarrisons.CoopIntegration." + action.Kind);
        }

        private static void SendActionOutcome(NetPeer peer, ActionOutcome outcome)
        {
            INetwork? network = _network;
            if (network == null || peer.ConnectionState != ConnectionState.Connected ||
                outcome == null || string.IsNullOrWhiteSpace(outcome.Text))
            {
                return;
            }

            network.Send(peer, new SendInformationMessage(outcome.Text));
        }

        private static void HandleConfigRequest(MessagePayload<ConfigRequest> payload)
        {
            INetwork? network = _network;
            if (!IntegrationRuntime.IsServer || network == null)
            {
                return;
            }

            NetPeer? peer = payload?.Who as NetPeer;
            if (peer == null || !HasCompletedCampaignSynchronization(peer))
            {
                return;
            }

            GameThread.RunSafe(() =>
            {
                network.SendImmediate(peer, new ConfigSync { ConfigXml = SettingsStateStore.ReadConfigXml(), Revision = SettingsStateStore.Revision });
                network.SendImmediate(peer, new StateSync
                {
                    SettingsText = SettingsStateStore.BuildSettingsText(),
                    ActivityText = SettingsStateStore.BuildActivityText(),
                    Revision = SettingsStateStore.Revision
                });
                network.SendImmediate(peer, new PartyManifest
                {
                    SerializedEntries = PartyManifestStore.SerializedManifest,
                    Revision = PartyManifestStore.Revision
                });
                IntegrationLog.Information("initial config/state/manifest sent immediately to requesting peer");
            }, false, "ImprovedGarrisons.CoopIntegration.ConfigRequest");
        }

        private static void HandleConfigSync(MessagePayload<ConfigSync> payload)
        {
            if (payload?.What != null)
            {
                ConfigSync config = payload.What;
                GameThread.RunSafe(() =>
                {
                    _configReceived = true;
                    SettingsStateStore.ApplyConfigXml(config.ConfigXml, config.Revision);
                    IntegrationLog.Information("server configuration received at revision " + config.Revision);
                }, false, "ImprovedGarrisons.CoopIntegration.ConfigSync");
            }
        }

        private static void HandleStateSync(MessagePayload<StateSync> payload)
        {
            if (payload?.What != null)
            {
                StateSync state = payload.What;
                GameThread.RunSafe(
                    () => SettingsStateStore.ApplyState(state.SettingsText, state.ActivityText, state.Revision),
                    false,
                    "ImprovedGarrisons.CoopIntegration.StateSync");
            }
        }

        private static void HandlePartyManifest(MessagePayload<PartyManifest> payload)
        {
            if (payload?.What != null)
            {
                PartyManifest manifest = payload.What;
                GameThread.RunSafe(
                    () => PartyManifestStore.ApplyRemote(manifest.SerializedEntries, manifest.Revision),
                    false,
                    "ImprovedGarrisons.CoopIntegration.PartyManifest");
            }
        }

        private static void HandleServerHealth(MessagePayload<ServerHealth> payload)
        {
            if (payload?.What != null && !payload.What.Ready)
            {
                IntegrationLog.Warning("server health: " + payload.What.Detail);
            }
        }

        private static void BroadcastServerHealth()
        {
            if (_network == null)
            {
                return;
            }

            bool ready = IntegrationRuntime.NativePartyRegistrationReady &&
                global::ImprovedGarrisons.Main.PartyManagement != null &&
                global::ImprovedGarrisons.Main.GarrisonBehavior != null;
            string detail = ready ? "ready" : "Improved Garrisons or Coop-native MobileParty registration is not ready";
            SendToSynchronizedPeers(new ServerHealth { Ready = ready, Detail = detail, ServerTick = Environment.TickCount });
        }

        private static bool IsClientInCampaign()
        {
            if (IntegrationRuntime.IsServer || !ContainerProvider.TryResolve(out IClientLogic clientLogic))
            {
                return false;
            }

            return clientLogic.State is ClientCampaignState || clientLogic.State is ClientMissionState;
        }

        private static bool HasCompletedCampaignSynchronization(NetPeer peer)
        {
            return ContainerProvider.TryResolve(out IConnectionCollection connections) &&
                connections.HasCompletedCampaignSynchronization(peer);
        }

        private static void SendToSynchronizedPeers(IMessage message)
        {
            INetwork? network = _network;
            if (network == null || !IntegrationRuntime.IsServer ||
                !ContainerProvider.TryResolve(out IConnectionCollection connections))
            {
                return;
            }

            foreach (IConnectionLogic connection in connections)
            {
                NetPeer peer = connection.Peer;
                if (peer.ConnectionState == ConnectionState.Connected &&
                    connections.HasCompletedCampaignSynchronization(peer))
                {
                    network.Send(peer, message);
                }
            }
        }

        private static void PollConfigRequest(int now)
        {
            if (_network == null || _configReceived || _configAttempts >= MaximumConfigAttempts ||
                (_nextConfigRequest != 0 && unchecked(now - _nextConfigRequest) < 0))
            {
                return;
            }

            _configAttempts++;
            _nextConfigRequest = unchecked(now + ConfigRetryIntervalMilliseconds);
            _network.SendAll(new ConfigRequest { RequestId = Guid.NewGuid().ToString("N") });
            IntegrationLog.Information("server configuration requested (attempt " + _configAttempts + "/" + MaximumConfigAttempts + ")");
            if (_configAttempts == MaximumConfigAttempts)
            {
                ShowLocal("IG: the server did not answer configuration requests. Improved Garrisons actions may be unavailable.", ErrorColor);
            }
        }

        private static void Teardown()
        {
            if (_broker != null)
            {
                if (_hookedAsServer)
                {
                    _broker.Unsubscribe(PartyIntentHandler);
                    _broker.Unsubscribe(SettingsIntentHandler);
                    _broker.Unsubscribe(ManagementIntentHandler);
                    _broker.Unsubscribe(ConfigRequestHandler);
                }
                else
                {
                    _broker.Unsubscribe(ConfigSyncHandler);
                    _broker.Unsubscribe(StateSyncHandler);
                    _broker.Unsubscribe(PartyManifestHandler);
                    _broker.Unsubscribe(ServerHealthHandler);
                }
            }

            _broker = null;
            _network = null;
            _configAttempts = 0;
            _configReceived = false;
            _nextConfigRequest = 0;
        }

        private static void ShowLocal(string text, uint color)
        {
            InformationManager.DisplayMessage(new InformationMessage(text, Color.FromUint(color)));
        }
    }
}
