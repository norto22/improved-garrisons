using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using Autofac;
using Common.Messaging;
using Common.Serialization;
using Coop.Core.Server.Connections;
using GameInterface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using ImprovedGarrisons;
using ImprovedGarrisons.CoopIntegration.Protocol;
using ImprovedGarrisons.SaveSystem;
using ImprovedGarrisons.SaveSystem.Configuration;
using ImprovedGarrisons.SaveSystem.SaveData.DataManipulationManager;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using LiteNetLib;

namespace ImprovedGarrisons.CoopRuntimeContract;

public static partial class ContractRunner
{
    private static void RunSurplusGuardSettingsTests()
    {
        PropertyInfo option = typeof(GarrisonSettings).GetProperty("GuardsAutoSpawnFromExcess")
            ?? throw new MissingMemberException("Surplus guard creation setting is missing.");
        GarrisonSettings settings = new();
        Assert(option.PropertyType == typeof(bool) && !(bool)option.GetValue(settings)!,
            "Surplus guard creation must default to disabled.");
        option.SetValue(settings, true);
        Assert((bool)option.GetValue(settings.clone())!, "Copying settlement settings lost the surplus guard option.");
        FieldInfo backingField = typeof(GarrisonSettings).GetField("<GuardsAutoSpawnFromExcess>k__BackingField",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert(backingField.IsDefined(typeof(OptionalFieldAttribute), false),
            "Old binary saves must be permitted to omit the surplus guard field.");
        TestSettingsLegacyBinaryCompatibility(option);

        GarrisonBehavior? previousBehavior = Main.GarrisonBehavior;
        Main.GarrisonBehavior = new GarrisonBehavior();
        try
        {
            Main.GarrisonBehavior.SettlementSettingsData["Surplus Settings Town"] = settings;
            Type store = typeof(ConfigRequest).Assembly.GetType(
                "ImprovedGarrisons.CoopIntegration.Persistence.SettingsStateStore", true)!;
            MethodInfo serialize = store.GetMethod("BuildSettingsText", BindingFlags.Static | BindingFlags.Public)!;
            MethodInfo apply = store.GetMethod("ApplySettingsText", BindingFlags.Static | BindingFlags.NonPublic)!;
            string text = (string)serialize.Invoke(null, new object?[] { null })!;
            Assert(text.Contains("GuardsAutoSpawnFromExcess=True"), "Authoritative state omitted the enabled surplus option.");
            option.SetValue(settings, false);
            apply.Invoke(null, new object[] { text, false });
            Assert((bool)option.GetValue(settings)!, "State roundtrip lost the surplus guard option.");
            string legacy = "[" + Convert.ToBase64String(Encoding.UTF8.GetBytes("Surplus Settings Town")) + "]\nGuardsAutoSpawn=True\n";
            apply.Invoke(null, new object[] { legacy, false });
            Assert(!(bool)option.GetValue(settings)!, "Legacy state left a stale surplus option enabled.");
            Assert(settings.GuardsAutoSpawn, "Legacy state reset an unrelated guard setting.");
        }
        finally
        {
            Main.GarrisonBehavior = previousBehavior;
        }

        Assert(Enum.TryParse("ToggleAutoGuardsFromExcess", out SettingsIntentKind operation) && (int)operation == 33,
            "Surplus guard intent must be appended as wire ID 33.");
        Assert((int)SettingsIntentKind.AdjustTemplateCount == 32 && (int)SettingsIntentKind.ToggleAutoGuards == 5,
            "Existing setting wire IDs changed.");
        MethodInfo setter = typeof(MobileGarrisonSettings).GetMethod("ToggleAutoGuardsFromExcess")
            ?? throw new MissingMethodException("Surplus guard setter is missing.");
        Type patches = typeof(ConfigRequest).Assembly.GetType("ImprovedGarrisons.CoopIntegration.Patching.ClientServerPatches", true)!;
        MethodInfo map = patches.GetMethod("TryGetSettingOperation", BindingFlags.Static | BindingFlags.NonPublic)!;
        object?[] arguments = { setter, default(SettingsIntentKind) };
        Assert((bool)map.Invoke(null, arguments)! && (SettingsIntentKind)arguments[1]! == operation,
            "The client cannot map the surplus guard setting to its wire operation.");
        TestSettingsSurplusSetterAndForwarding(option, setter, operation, patches);
        Console.WriteLine("PASS surplus guard defaults, clone, state roundtrip, legacy omission, and client operation mapping");
    }

    // The production save format is BinaryFormatter; this fixture contains only locally generated
    // settings and never reads external bytes. Enabling its legacy reader is scoped to this test.
#pragma warning disable SYSLIB0011, SYSLIB0050
    private static void TestSettingsLegacyBinaryCompatibility(PropertyInfo option)
    {
        const string legacySwitch = "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization";
        AppContext.TryGetSwitch(legacySwitch, out bool previousSwitch);
        string directory = Path.Combine(Path.GetTempPath(), "ig-surplus-binary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            AppContext.SetSwitch(legacySwitch, true);
            using var reflectionScope = System.Runtime.Loader.AssemblyLoadContext.EnterContextualReflection(typeof(GarrisonSettings).Assembly);
            GarrisonSettings original = new() { GuardsAutoSpawn = true, GuardsAutoSpawnSize = 73 };
            original.Template.SetTroops(new Dictionary<string, int> { ["legacy-archer"] = 42 });
            option.SetValue(original, true);
            SurrogateSelector selector = new();
            selector.AddSurrogate(typeof(GarrisonSettings), new StreamingContext(StreamingContextStates.All),
                new SettingsLegacySurrogate());
            BinaryFormatter formatter = new() { SurrogateSelector = selector };
            string legacyPath = Path.Combine(directory, "legacy.bin");
            using (FileStream stream = File.Create(legacyPath))
            {
                int version = (int)typeof(FileWriter).GetField("saveVersionNumber", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                stream.WriteByte(checked((byte)version));
                formatter.Serialize(stream, original);
            }

            GarrisonSettings legacy = FileWriter.DeserializeFromBin<GarrisonSettings>(legacyPath);
            Assert(!(bool)option.GetValue(legacy)!, "The actual save reader did not default an omitted legacy binary field to false.");
            Assert(legacy.GuardsAutoSpawn && legacy.GuardsAutoSpawnSize == 73 &&
                legacy.Template.GetTroopList()["legacy-archer"] == 42,
                "Reading legacy settings lost existing guard settings or template counts.");
            string currentPath = Path.Combine(directory, "current.bin");
            FileWriter.SerializeToBin(original, currentPath);
            GarrisonSettings current = FileWriter.DeserializeFromBin<GarrisonSettings>(currentPath);
            Assert((bool)option.GetValue(current)!, "The actual save writer/reader lost the enabled surplus guard field.");
            Console.WriteLine("PASS actual binary save reader with omitted legacy field and current enabled roundtrip");
        }
        finally
        {
            AppContext.SetSwitch(legacySwitch, previousSwitch);
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class SettingsLegacySurrogate : ISerializationSurrogate
    {
        public void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            foreach (FieldInfo field in FormatterServices.GetSerializableMembers(typeof(GarrisonSettings), context).Cast<FieldInfo>())
            {
                if (field.Name != "<GuardsAutoSpawnFromExcess>k__BackingField")
                {
                    info.AddValue(field.Name, field.GetValue(obj), field.FieldType);
                }
            }
        }

        public object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector? selector)
        {
            throw new NotSupportedException("The production reader must deserialize the fixture without a surrogate.");
        }
    }
#pragma warning restore SYSLIB0011, SYSLIB0050

    private static void TestSettingsSurplusSetterAndForwarding(PropertyInfo option, MethodInfo setter,
        SettingsIntentKind operation, Type patches)
    {
        using SettingsTownFixture fixture = new();
        Town town = fixture.Town;
        Settlement settlement = fixture.Settlement;
        GarrisonSettings settings = fixture.Settings;
        settings.GuardsAutoSpawn = true;
        settings.GuardsAutoSpawnToDefend = true;
        try
        {
            setter.Invoke(MobileGarrisonSettings.Instance, new object[] { town, true });
            Assert((bool)option.GetValue(settings)!, "The real town setter did not enable surplus guards.");
            setter.Invoke(MobileGarrisonSettings.Instance, new object[] { town, false });
            Assert(!(bool)option.GetValue(settings)!, "The real town setter did not disable surplus guards.");
            Assert(settings.GuardsAutoSpawn && settings.GuardsAutoSpawnToDefend,
                "Disabling surplus guards changed other automatic guard settings.");

            ResetIntegrationTransport();
            var (network, _) = ConnectAsClient();
            MethodInfo forward = patches.GetMethod("ForwardSettingPrefix", BindingFlags.Static | BindingFlags.Public)!;
            Assert(!(bool)forward.Invoke(null, new object[] { setter, new object[] { town, true } })!,
                "The client executed the surplus setter locally instead of forwarding it.");
            SettingsIntent sent = network.SentAll.OfType<SettingsIntent>().Single(intent => intent.Operation == operation);
            Assert(sent.BooleanArgument && sent.SettlementId == settlement.StringId,
                "The forwarded surplus intent lost its value or settlement identity.");
            Assert(!(bool)option.GetValue(settings)!, "Forwarding mutated the client settings before authoritative sync.");
            ResetIntegrationTransport();

            Type dispatcher = typeof(ConfigRequest).Assembly.GetType("ImprovedGarrisons.CoopIntegration.Runtime.ServerActionDispatcher", true)!;
            Type actionType = typeof(ConfigRequest).Assembly.GetType("ImprovedGarrisons.CoopIntegration.Runtime.ServerAction", true)!;
            object action = Activator.CreateInstance(actionType, nonPublic: true)!;
            actionType.GetProperty("SettingOperation")!.SetValue(action, (int)sent.Operation);
            actionType.GetProperty("BooleanArgument")!.SetValue(action, sent.BooleanArgument);
            MethodInfo applySetting = dispatcher.GetMethod("ApplySetting", BindingFlags.Static | BindingFlags.NonPublic)!;
            object outcome = applySetting.Invoke(null, new object[] { action, town })!;
            Assert((bool)outcome.GetType().GetProperty("Success")!.GetValue(outcome)! && (bool)option.GetValue(settings)!,
                "The server did not apply the forwarded surplus guard setting.");
            TestSettingsSurplusReceiveAndAuthorization(option, settings, settlement, fixture.Clan, sent);
            Console.WriteLine("PASS surplus guard town setter, client forwarding without mutation, and server application");
        }
        finally
        {
            ResetIntegrationTransport();
        }
    }

    private static void TestSettingsSurplusReceiveAndAuthorization(PropertyInfo option, GarrisonSettings settings,
        Settlement settlement, Clan clan, SettingsIntent sent)
    {
        FieldInfo managerInstance = typeof(MBObjectManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)!;
        object? previousManager = managerInstance.GetValue(null);
        var previousParties = Main.PartyManagement;
        Type registry = typeof(ConfigRequest).Assembly.GetType("ImprovedGarrisons.CoopIntegration.Persistence.ServerClanRegistry", true)!;
        HashSet<string> registeredClans = (HashSet<string>)registry.GetField("ClanIds", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        bool clanWasRegistered = registeredClans.Contains(clan.StringId);
        try
        {
            MBObjectManager manager = MBObjectManager.Init();
            manager.RegisterType<Settlement>("Settlement", "Settlements", 1, autoCreateInstance: false);
            manager.RegisterObject(settlement);
            Main.PartyManagement = (ImprovedGarrisons.AI.AIManagers.PartyManager)RuntimeHelpers.GetUninitializedObject(
                typeof(ImprovedGarrisons.AI.AIManagers.PartyManager));
            Common.ModInformation.IsServer = true;
            MessageBroker broker = new();
            RecordingNetwork network = new();
            SerializableTypeMapper mapper = new();
            FakeConnectionCollection connections = new();
            IPlayerManager playerManager = DispatchProxy.Create<IPlayerManager, SettingsLookupProxy>();
            SettingsLookupProxy players = (SettingsLookupProxy)playerManager;
            players.Player = new Player("surplus-controller", "surplus-hero", "surplus-party", clan.StringId, "surplus-character");
            IObjectManager objectManager = DispatchProxy.Create<IObjectManager, SettingsLookupProxy>();
            ((SettingsLookupProxy)objectManager).Clan = clan;
            using IContainer container = BuildContainer(builder =>
            {
                builder.RegisterInstance(connections).As<IConnectionCollection>();
                builder.RegisterInstance(playerManager).As<IPlayerManager>();
                builder.RegisterInstance(objectManager).As<IObjectManager>();
                RegisterCommon(builder, broker, network, mapper);
            });
            ContainerProvider.SetContainer(container);
            InvokeTransport("Poll");
            NetPeer peer = (NetPeer)RuntimeHelpers.GetUninitializedObject(typeof(NetPeer));

            option.SetValue(settings, false);
            sent.OperationId = "surplus-owned-" + Guid.NewGuid().ToString("N");
            broker.Publish(peer, sent);
            Assert((bool)option.GetValue(settings)!,
                "The real receive path did not route wire ID 33 through the authorized ApplySetting action.");
            Assert(connections.CampaignSynchronizationChecks > 0,
                "The surplus intent bypassed the synchronized-peer gate.");

            option.SetValue(settings, false);
            players.Player = new Player("foreign-controller", "foreign-hero", "foreign-party", "foreign-clan", "foreign-character");
            Clan foreignClan = (Clan)RuntimeHelpers.GetUninitializedObject(typeof(Clan));
            foreignClan.StringId = "foreign-clan";
            ((SettingsLookupProxy)objectManager).Clan = foreignClan;
            sent.OperationId = "surplus-foreign-" + Guid.NewGuid().ToString("N");
            broker.Publish(peer, sent);
            Assert(!(bool)option.GetValue(settings)!, "A foreign clan enabled surplus guards for another clan's settlement.");
            players.Player = null;
            sent.OperationId = "surplus-unknown-" + Guid.NewGuid().ToString("N");
            broker.Publish(peer, sent);
            Assert(!(bool)option.GetValue(settings)!, "An unknown player enabled surplus guards.");
            Console.WriteLine("PASS surplus guard wire receive, owned-clan success, foreign-clan denial, and unknown-player denial");
        }
        finally
        {
            ResetIntegrationTransport();
            Main.PartyManagement = previousParties;
            managerInstance.SetValue(null, previousManager);
            if (!clanWasRegistered)
            {
                registeredClans.Remove(clan.StringId);
            }
        }
    }

    private sealed class SettingsTownFixture : IDisposable
    {
        private readonly FieldInfo currentGame = typeof(Game).GetField("_current", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? previousGame;
        private readonly GarrisonBehavior? previousBehavior = Main.GarrisonBehavior;
        private readonly bool previousServer = Common.ModInformation.IsServer;

        public Town Town { get; }
        public Settlement Settlement { get; }
        public GarrisonSettings Settings { get; } = new();
        public Clan Clan { get; }

        public SettingsTownFixture()
        {
            previousGame = currentGame.GetValue(null);
            Game game = (Game)RuntimeHelpers.GetUninitializedObject(typeof(Game));
            Clan = (Clan)RuntimeHelpers.GetUninitializedObject(typeof(Clan));
            Clan.StringId = "surplus-settings-clan";
            Hero hero = (Hero)RuntimeHelpers.GetUninitializedObject(typeof(Hero));
            CharacterObject player = (CharacterObject)RuntimeHelpers.GetUninitializedObject(typeof(CharacterObject));
            SettingsSetField(hero, "_clan", Clan);
            SettingsSetField(player, "_heroObject", hero);
            game.PlayerTroop = player;
            Settlement = (Settlement)RuntimeHelpers.GetUninitializedObject(typeof(Settlement));
            Settlement.StringId = "surplus-settings-town";
            SettingsSetField(Settlement, "_name", new TextObject("Surplus Settings Town"));
            PartyBase party = (PartyBase)RuntimeHelpers.GetUninitializedObject(typeof(PartyBase));
            SettingsSetField(party, "<Settlement>k__BackingField", Settlement);
            SettingsSetField(Settlement, "<Party>k__BackingField", party);
            Town = (Town)RuntimeHelpers.GetUninitializedObject(typeof(Town));
            SettingsSetField(Town, "_owner", party);
            SettingsSetField(Town, "_ownerClan", Clan);
            Settlement.Town = Town;
            GarrisonBehavior behavior = new();
            behavior.SettlementSettingsData["Surplus Settings Town"] = Settings;
            Main.GarrisonBehavior = behavior;
            currentGame.SetValue(null, game);
        }

        public void Dispose()
        {
            Main.GarrisonBehavior = previousBehavior;
            currentGame.SetValue(null, previousGame);
            Common.ModInformation.IsServer = previousServer;
        }
    }

    public class SettingsLookupProxy : DispatchProxy
    {
        public Player? Player { get; set; }
        public Clan? Clan { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "TryGetPlayer" && args != null)
            {
                args[1] = Player;
                return Player != null;
            }
            if (targetMethod?.Name == "TryGetObject" && args != null)
            {
                bool found = Clan != null && Equals(args[0], Clan.StringId);
                args[1] = found ? Clan : null;
                return found;
            }

            Type returnType = targetMethod?.ReturnType ?? typeof(void);
            return returnType == typeof(void) || !returnType.IsValueType ? null : Activator.CreateInstance(returnType);
        }
    }

    private static void SettingsSetField(object target, string name, object value)
    {
        for (Type? type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo? field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
        }

        throw new MissingFieldException(target.GetType().FullName, name);
    }
}
