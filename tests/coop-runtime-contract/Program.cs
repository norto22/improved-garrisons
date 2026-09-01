using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Autofac;
using Common;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using Coop.Core.Client;
using Coop.Core.Client.States;
using Coop.Core.Common;
using Coop.Core.Server.Connections;
using GameInterface;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Time.Interfaces;
using GameInterface.Services.UI.Interfaces;
using ImprovedGarrisons.CoopIntegration.Protocol;
using LiteNetLib;

namespace ImprovedGarrisons.CoopRuntimeContract;

public static class ContractRunner
{
    private const string TestName = "test_shipped_integration_binds_to_real_coop_network_contract";

    public static int Run()
    {
        try
        {
            GameThread.Instance.MarkGameThread();
            TestPersistentDataUsesCoopDataDirectory();
            TestLegacyPersistenceMigrationIsScopedAndNonDestructive();
            TestClientSendsConfigRequestThroughCoopNetwork();
            ResetIntegrationTransport();
            TestClientFallsBackToLocalExecutionWhenCoopInactive();
            TestServerSubscribesAndRepliesThroughCoopNetwork();
            ResetIntegrationTransport();
            TestSettingSliderDragCoalescesToOneSend();
            test_apply_setting_slider_updates_return_silent_success();
            Console.WriteLine($"PASS {TestName}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL {TestName}: {exception}");
            return 1;
        }
        finally
        {
            ContainerProvider.Clear();
            GameThread.Instance.UnmarkGameThread();
        }
    }

    private static void TestPersistentDataUsesCoopDataDirectory()
    {
        Type paths = GetIntegrationDataPathsType();
        FieldInfo directory = paths.GetField("_directory", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(paths.FullName, "_directory");
        MethodInfo filePath = paths.GetMethod("FilePath", BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(paths.FullName, "FilePath");
        string root = Path.Combine(Path.GetTempPath(), "ig-coop-data-contract-" + Guid.NewGuid().ToString("N"));
        string? previous = Environment.GetEnvironmentVariable("BANNERLORD_USER_DIR");

        Directory.CreateDirectory(root);
        try
        {
            Environment.SetEnvironmentVariable("BANNERLORD_USER_DIR", root);
            directory.SetValue(null, null);

            string actual = (string)(filePath.Invoke(null, new object[] { "contract-state.txt" })
                ?? throw new InvalidOperationException("IntegrationDataPaths.FilePath returned null."));
            string expected = Path.Combine(root, "ImprovedGarrisons", "contract-state.txt");

            Assert(string.Equals(Path.GetFullPath(actual), Path.GetFullPath(expected), StringComparison.Ordinal),
                $"The shipped integration resolved persistent data to '{actual}' instead of Coop's data directory '{expected}'.");
        }
        finally
        {
            directory.SetValue(null, null);
            Environment.SetEnvironmentVariable("BANNERLORD_USER_DIR", previous);
            Directory.Delete(root, recursive: true);
        }
    }

    private static void TestLegacyPersistenceMigrationIsScopedAndNonDestructive()
    {
        Type paths = GetIntegrationDataPathsType();
        MethodInfo migrate = paths.GetMethod("MigrateLegacyData", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(paths.FullName, "MigrateLegacyData");
        string root = Path.Combine(Path.GetTempPath(), "ig-persistence-migration-contract-" + Guid.NewGuid().ToString("N"));
        string legacy = Path.Combine(root, "server-data", "improved-garrisons");
        string persistent = Path.Combine(root, "CoopData", "DedicatedServer", "ImprovedGarrisons");

        Directory.CreateDirectory(legacy);
        Directory.CreateDirectory(persistent);
        try
        {
            File.WriteAllText(Path.Combine(legacy, "settlement-settings.txt"), "legacy-settings");
            File.WriteAllText(Path.Combine(legacy, "party-manifest.txt"), "legacy-manifest");
            File.WriteAllText(Path.Combine(legacy, "party-manifest.txt.bak"), "legacy-backup");
            File.WriteAllText(Path.Combine(legacy, "player-assignments.json"), "must-not-migrate");
            File.WriteAllText(Path.Combine(persistent, "party-manifest.txt"), "persistent-manifest");

            migrate.Invoke(null, new object[] { legacy, persistent });

            Assert(File.ReadAllText(Path.Combine(persistent, "settlement-settings.txt")) == "legacy-settings",
                "The shipped integration did not migrate legacy settlement settings.");
            Assert(File.ReadAllText(Path.Combine(persistent, "party-manifest.txt")) == "persistent-manifest",
                "Legacy migration overwrote an existing persistent party manifest.");
            Assert(File.ReadAllText(Path.Combine(persistent, "party-manifest.txt.bak")) == "legacy-backup",
                "The shipped integration did not migrate the legacy party-manifest backup.");
            Assert(!File.Exists(Path.Combine(persistent, "player-assignments.json")),
                "Legacy migration copied a file outside Improved Garrisons' explicit persistence allowlist.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void TestClientSendsConfigRequestThroughCoopNetwork()
    {
        ModInformation.IsServer = false;
        MessageBroker broker = new();
        RecordingNetwork network = new();
        SerializableTypeMapper mapper = new();
        FakeClientLogic logic = new();
        CampaignState campaignState = new(
            logic,
            broker,
            network,
            CreateDefaultProxy<ILoadingInterface>(),
            CreateDefaultProxy<IGameStateInterface>(),
            CreateDefaultProxy<ICoopFinalizer>(),
            CreateDefaultProxy<IMapTimeTrackerInterface>());
        logic.State = campaignState;

        using IContainer container = BuildContainer(builder =>
        {
            builder.RegisterInstance(logic).As<IClientLogic>();
            RegisterCommon(builder, broker, network, mapper);
        });
        ContainerProvider.SetContainer(container);

        InvokeTransport("Poll");

        Assert(network.SentAll.Any(message => message is ConfigRequest),
            "The shipped client runtime did not send ConfigRequest through BannerlordCoop INetwork.");
        Assert(mapper.TryGetId(typeof(ConfigRequest), out _),
            "The shipped client runtime did not register ConfigRequest with Coop's real serializer mapper.");
    }

    private static void TestClientFallsBackToLocalExecutionWhenCoopInactive()
    {
        ModInformation.IsServer = false;

        Type patches = typeof(ConfigRequest).Assembly.GetType(
            "ImprovedGarrisons.CoopIntegration.Patching.ClientServerPatches",
            throwOnError: true)!;
        MethodInfo isClient = patches.GetMethod("IsClient", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(patches.FullName, "IsClient");

        bool shouldForwardToServer = (bool)isClient.Invoke(null, null)!;

        Assert(!shouldForwardToServer,
            "The shipped client runtime intercepted an action and tried to forward it to the server even though " +
            "Coop is not connected; it should have run the action locally instead.");
    }

    private static void TestSettingSliderDragCoalescesToOneSend()
    {
        ModInformation.IsServer = false;
        MessageBroker broker = new();
        RecordingNetwork network = new();
        SerializableTypeMapper mapper = new();
        FakeClientLogic logic = new();
        CampaignState campaignState = new(
            logic,
            broker,
            network,
            CreateDefaultProxy<ILoadingInterface>(),
            CreateDefaultProxy<IGameStateInterface>(),
            CreateDefaultProxy<ICoopFinalizer>(),
            CreateDefaultProxy<IMapTimeTrackerInterface>());
        logic.State = campaignState;

        using IContainer container = BuildContainer(builder =>
        {
            builder.RegisterInstance(logic).As<IClientLogic>();
            RegisterCommon(builder, broker, network, mapper);
        });
        ContainerProvider.SetContainer(container);

        InvokeTransport("Poll");

        Type patches = typeof(ConfigRequest).Assembly.GetType(
            "ImprovedGarrisons.CoopIntegration.Patching.ClientServerPatches",
            throwOnError: true)!;
        MethodInfo sendThrottled = patches.GetMethod("SendSettingThrottled", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(patches.FullName, "SendSettingThrottled");

        // Simulates dragging a slider from 0 to 19 with no real time elapsing between ticks -- exactly
        // what a fast mouse drag looks like to Environment.TickCount within one throttle window.
        for (int value = 0; value < 20; value++)
        {
            SettingsIntent intent = new() { SettlementId = "contract-test-town", IntegerArgument = value };
            sendThrottled.Invoke(null, new object?[] { SettingsIntentKind.SetRecruiterAmountToRecruit, intent });
        }

        int sentSettingsIntents = network.SentAll.Count(message => message is SettingsIntent);
        Assert(sentSettingsIntents >= 1 && sentSettingsIntents <= 2,
            $"20 rapid setting changes within one throttle window produced {sentSettingsIntents} network sends " +
            "(expected 1, occasionally 2 on a timing edge); the throttle is not coalescing a fast slider drag.");
    }

    private static void test_apply_setting_slider_updates_return_silent_success()
    {
        Type integrationAssemblyMarker = typeof(ConfigRequest);
        Type dispatcher = integrationAssemblyMarker.Assembly.GetType(
            "ImprovedGarrisons.CoopIntegration.Runtime.ServerActionDispatcher",
            throwOnError: true)!;
        Type actionType = integrationAssemblyMarker.Assembly.GetType(
            "ImprovedGarrisons.CoopIntegration.Runtime.ServerAction",
            throwOnError: true)!;
        MethodInfo applySetting = dispatcher.GetMethod("ApplySetting", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(dispatcher.FullName, "ApplySetting");
        PropertyInfo settingOperation = actionType.GetProperty("SettingOperation")
            ?? throw new MissingMemberException(actionType.FullName, "SettingOperation");

        SettingsIntentKind[] sliderOperations =
        {
            SettingsIntentKind.SetReturnPercentage,
            SettingsIntentKind.SetAutoGarrisonThreshold,
            SettingsIntentKind.SetAutoGarrisonSize,
            SettingsIntentKind.SetRecruiterAmountToRecruit,
            SettingsIntentKind.SetRecruitmentThreshold,
            SettingsIntentKind.SetTownMaxUpgradeTier
        };

        foreach (SettingsIntentKind operation in sliderOperations)
        {
            object action = Activator.CreateInstance(actionType, nonPublic: true)
                ?? throw new InvalidOperationException($"Could not construct {actionType.FullName}.");
            settingOperation.SetValue(action, (int)operation);

            object outcome;
            try
            {
                outcome = applySetting.Invoke(null, new object?[] { action, null })
                    ?? throw new InvalidOperationException($"ApplySetting returned null for {operation}.");
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw exception.InnerException;
            }

            Type outcomeType = outcome.GetType();
            bool success = (bool)(outcomeType.GetProperty("Success")?.GetValue(outcome)
                ?? throw new MissingMemberException(outcomeType.FullName, "Success"));
            string code = (string)(outcomeType.GetProperty("Code")?.GetValue(outcome)
                ?? throw new MissingMemberException(outcomeType.FullName, "Code"));
            string text = (string)(outcomeType.GetProperty("Text")?.GetValue(outcome)
                ?? throw new MissingMemberException(outcomeType.FullName, "Text"));

            Assert(success, $"{operation} did not retain a successful setting outcome.");
            Assert(code == "updated", $"{operation} returned outcome code '{code}' instead of 'updated'.");
            Assert(string.IsNullOrEmpty(text),
                $"{operation} returned success chat text '{text}'; routine slider updates must be silent.");
        }
    }

    private static void TestServerSubscribesAndRepliesThroughCoopNetwork()
    {
        ModInformation.IsServer = true;
        MessageBroker broker = new();
        RecordingNetwork network = new();
        SerializableTypeMapper mapper = new();
        FakeConnectionCollection connections = new();

        using IContainer container = BuildContainer(builder =>
        {
            builder.RegisterInstance(connections).As<IConnectionCollection>();
            RegisterCommon(builder, broker, network, mapper);
        });
        ContainerProvider.SetContainer(container);

        InvokeTransport("Poll");

        NetPeer peer = (NetPeer)RuntimeHelpers.GetUninitializedObject(typeof(NetPeer));
        broker.Publish(peer, new ConfigRequest { RequestId = "contract-test" });

        Assert(connections.CampaignSynchronizationChecks == 1,
            "The shipped server runtime did not ask Coop whether the requesting peer completed campaign synchronization.");
        Assert(network.SentImmediate.Any(message => message is ConfigSync),
            "The shipped server runtime did not send ConfigSync through BannerlordCoop INetwork.");
        Assert(network.SentImmediate.Any(message => message is StateSync),
            "The shipped server runtime did not send StateSync through BannerlordCoop INetwork.");
        Assert(network.SentImmediate.Any(message => message is PartyManifest),
            "The shipped server runtime did not send PartyManifest through BannerlordCoop INetwork.");
        Assert(mapper.TryGetId(typeof(ConfigSync), out _),
            "The shipped server runtime did not register ConfigSync with Coop's real serializer mapper.");
    }

    private static IContainer BuildContainer(Action<ContainerBuilder> configure)
    {
        ContainerBuilder builder = new();
        configure(builder);
        return builder.Build();
    }

    private static void RegisterCommon(
        ContainerBuilder builder,
        IMessageBroker broker,
        INetwork network,
        ISerializableTypeMapper mapper)
    {
        builder.RegisterInstance(broker).As<IMessageBroker>();
        builder.RegisterInstance(network).As<INetwork>();
        builder.RegisterInstance(mapper).As<ISerializableTypeMapper>();
    }

    private static void ResetIntegrationTransport()
    {
        InvokeTransport("Teardown");
        Type transport = GetTransportType();
        transport.GetField("_nextPoll", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, 0);
        transport.GetField("_nextHealth", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, 0);
        ContainerProvider.Clear();
    }

    private static void InvokeTransport(string methodName)
    {
        MethodInfo method = GetTransportType().GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(GetTransportType().FullName, methodName);
        try
        {
            method.Invoke(null, null);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            throw exception.InnerException;
        }
    }

    private static Type GetTransportType()
    {
        return typeof(ConfigRequest).Assembly.GetType(
            "ImprovedGarrisons.CoopIntegration.Runtime.IntegrationTransport",
            throwOnError: true)!;
    }

    private static Type GetIntegrationDataPathsType()
    {
        return typeof(ConfigRequest).Assembly.GetType(
            "ImprovedGarrisons.CoopIntegration.Persistence.IntegrationDataPaths",
            throwOnError: true)!;
    }

    private static T CreateDefaultProxy<T>() where T : class
    {
        return DispatchProxy.Create<T, DefaultDispatchProxy>();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public class DefaultDispatchProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Type returnType = targetMethod?.ReturnType ?? typeof(void);
            return returnType == typeof(void) || !returnType.IsValueType
                ? null
                : Activator.CreateInstance(returnType);
        }
    }

    private sealed class PassiveClientState : IClientState
    {
        public void Connect() { }
        public void Disconnect() { }
        public void StartCharacterCreation() { }
        public void LoadSavedData() { }
        public void ExitGame() { }
        public void EnterMainMenu() { }
        public void EnterCampaignState() { }
        public void EnterMissionState() { }
        public void ValidateModules() { }
        public void Dispose() { }
    }

    private sealed class FakeClientLogic : IClientLogic
    {
        public Player Player { get; set; } = null!;
        public IClientState State { get; set; } = new PassiveClientState();
        public bool RunningState => State is CampaignState or MissionState;
        public void Start() { }
        public void Stop() { }
        public void Connect() { }
        public void Disconnect() { }
        public void StartCharacterCreation() { }
        public void LoadSavedData() { }
        public void ExitGame() { }
        public void EnterMainMenu() { }
        public void EnterCampaignState() { }
        public void EnterMissionState() { }
        public void ValidateModules() { }
        public void Dispose() { }
        public TState SetState<TState>() where TState : IClientState => throw new NotSupportedException();
    }

    private sealed class RecordingNetwork : INetwork
    {
        public List<IMessage> SentAll { get; } = new();
        public List<IMessage> SentImmediate { get; } = new();
        public INetworkConfig Config => null!;
        public void Send(NetPeer netPeer, IPacket packet) { }
        public void SendImmediate(NetPeer netPeer, IPacket packet) { }
        public void SendAll(IPacket packet) { }
        public void SendAllBut(NetPeer excludedPeer, IPacket packet) { }
        public void Send(NetPeer netPeer, IMessage message) { }
        public void SendImmediate(NetPeer netPeer, IMessage message) => SentImmediate.Add(message);
        public void SendAll(IMessage message) => SentAll.Add(message);
        public void SendAllBut(NetPeer excludedPeer, IMessage message) { }
        public void FlushPendingMessages() { }
        public void Start() { }
        public void Dispose() { }
    }

    private sealed class FakeConnectionCollection : IConnectionCollection
    {
        public int CampaignSynchronizationChecks { get; private set; }
        public IEnumerable<IConnectionLogic> LoadingPeers => Array.Empty<IConnectionLogic>();
        public bool HasCompletedCampaignSynchronization(NetPeer peer)
        {
            CampaignSynchronizationChecks++;
            return true;
        }
        public IEnumerator<IConnectionLogic> GetEnumerator() => Enumerable.Empty<IConnectionLogic>().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Dispose() { }
    }
}
