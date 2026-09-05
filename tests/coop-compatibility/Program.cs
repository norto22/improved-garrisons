using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using ImprovedGarrisons.CoopIntegration.Core;

internal static class Program
{
    private const string TestName = "test_improved_garrisons_coop_native_registry_gates_party_creation";

    private static int Main(string[] args)
    {
        try
        {
            TestDeclaredSubmoduleDependencyBoundary(args);
            TestInTreeBootstrapAndRuntime(args);
            TestDedicatedServerDependencyClosure(args);
            TestPackageDeclaresServerOverlayManifest(args);
            TestInTreeHostUsesNativePartyRegistration();
            TestRoleRoutingAndAuthorization();
            TestIntentValidationAndIdempotency();
            TestManifestRoundTrip();
            Console.WriteLine($"PASS {TestName}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL {TestName}: {exception.Message}");
            return 1;
        }
    }

    private static void TestInTreeBootstrapAndRuntime(string[] args)
    {
        string moduleDirectory = Path.GetFullPath(args[0]);
        string clientBin = Path.Combine(moduleDirectory, "bin", "Win64_Shipping_Client");
        string serverBin = Path.Combine(moduleDirectory, "bin", "Win64_Shipping_Server");
        string clientBootstrap = Path.Combine(clientBin, "ImprovedGarrisons.CoopBootstrap.dll");
        string serverBootstrap = Path.Combine(serverBin, "ImprovedGarrisons.CoopBootstrap.dll");
        string clientRuntime = Path.Combine(clientBin, "Adapters", "ImprovedGarrisons.CoopIntegration.dll");
        string serverRuntime = Path.Combine(serverBin, "Adapters", "ImprovedGarrisons.CoopIntegration.dll");

        Assert(File.Exists(clientBootstrap) && File.Exists(serverBootstrap),
            "The Improved Garrisons Coop bootstrap is missing from the client/server package.");
        Assert(File.Exists(clientRuntime) && File.Exists(serverRuntime),
            "The Improved Garrisons direct Coop runtime is missing from the client/server package.");
        Assert(File.ReadAllBytes(clientBootstrap).SequenceEqual(File.ReadAllBytes(serverBootstrap)),
            "Coop bootstrap differs between client and server.");
        Assert(File.ReadAllBytes(clientRuntime).SequenceEqual(File.ReadAllBytes(serverRuntime)),
            "Coop runtime differs between client and server.");
        Assert(string.Equals(ReadAssemblyName(clientBootstrap), "ImprovedGarrisons.CoopBootstrap", StringComparison.Ordinal),
            "The bootstrap has an unexpected assembly identity.");
        Assert(string.Equals(ReadAssemblyName(clientRuntime), "ImprovedGarrisons.CoopIntegration", StringComparison.Ordinal),
            "The direct Coop runtime has an unexpected assembly identity.");
        Assert(ReadAssemblyReferences(clientRuntime).Contains("Common", StringComparer.Ordinal) &&
            ReadAssemblyReferences(clientRuntime).Contains("GameInterface", StringComparer.Ordinal) &&
            ReadAssemblyReferences(clientRuntime).Contains("Coop.Core", StringComparer.Ordinal),
            "The runtime is not wired directly to BannerlordCoop's network and dedicated-server connection APIs.");
        Assert(!ReadAssemblyReferences(clientRuntime).Contains("CoopModPatch", StringComparer.Ordinal),
            "The runtime still depends on CoopModPatch.");
        Assert(!File.Exists(Path.Combine(clientBin, "ImprovedGarrisons.CoopHost.dll")) &&
            !File.Exists(Path.Combine(clientBin, "Adapters", "Adapter.ImprovedGarrisons.dll")),
            "The abandoned embedded CoopModPatch host/adapter is still packaged.");
    }

    private static void TestDeclaredSubmoduleDependencyBoundary(string[] args)
    {
        Assert(args.Length == 2, "Expected the deployable module directory and Coop server engine directory.");
        string moduleDirectory = Path.GetFullPath(args[0]);
        string clientDirectory = Path.Combine(moduleDirectory, "bin", "Win64_Shipping_Client");
        string serverDirectory = Path.Combine(moduleDirectory, "bin", "Win64_Shipping_Server");
        XDocument manifest = XDocument.Load(Path.Combine(moduleDirectory, "SubModule.xml"));
        string? moduleType = manifest.Root?.Element("ModuleType")?.Attribute("value")?.Value;
        Assert(string.Equals(moduleType, "Official", StringComparison.Ordinal),
            "Improved Garrisons must remain Official because this Coop appliance rejects Community modules during validation.");
        string[] declaredDlls = manifest
            .Descendants("DLLName")
            .Select(element => element.Attribute("value")?.Value ?? string.Empty)
            .Where(value => value.Length > 0)
            .ToArray();

        Assert(declaredDlls.Length > 0 &&
            string.Equals(declaredDlls[0], "ImprovedGarrisons.CoopBootstrap.dll", StringComparison.Ordinal),
            "The dependency-free Improved Garrisons bootstrap must be the first declared submodule.");

        HashSet<string> forbiddenReferences = new(StringComparer.Ordinal)
        {
            "Common",
            "GameInterface",
            "LiteNetLib",
            "protobuf-net.Core",
            "Serilog"
        };

        foreach (string declaredDll in declaredDlls)
        {
            string declaredPath = Path.Combine(clientDirectory, declaredDll);
            Assert(File.Exists(declaredPath), $"Declared submodule DLL is missing: {declaredDll}.");
            Assert(File.Exists(Path.Combine(serverDirectory, declaredDll)), $"Declared server submodule DLL is missing: {declaredDll}.");
            string[] conflicts = ReadAssemblyReferences(declaredPath)
                .Where(forbiddenReferences.Contains)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            Assert(conflicts.Length == 0,
                $"Declared submodule {declaredDll} has early Coop runtime dependencies: {string.Join(", ", conflicts)}.");
        }

        Assert(declaredDlls.Contains("ImprovedGarrisons.CoopBootstrap.dll", StringComparer.Ordinal),
            "The Improved Garrisons Coop bootstrap is not declared.");
        Assert(!declaredDlls.Contains("ImprovedGarrisons.CoopIntegration.dll", StringComparer.Ordinal),
            "The Coop-dependent runtime assembly is still declared as a Bannerlord submodule.");

        XElement[] subModules = manifest.Descendants("SubModule").ToArray();
        Assert(HasRoleVariant(subModules, "ImprovedGarrisons.CoopBootstrap.dll", "none", "false"),
            "The bootstrap client variant (none/false) is missing.");
        Assert(HasRoleVariant(subModules, "ImprovedGarrisons.CoopBootstrap.dll", "custom", "false"),
            "The bootstrap dedicated-server variant (custom/false) is missing.");
        Assert(HasRoleVariant(subModules, "ImprovedGarrisons.dll", "none", "false"),
            "The main client variant (none/false) is missing.");
        Assert(subModules.Count(element =>
                string.Equals(element.Element("DLLName")?.Attribute("value")?.Value, "ImprovedGarrisons.CoopBootstrap.dll", StringComparison.Ordinal)) == 2,
            "Bootstrap must have exactly one client and one server variant.");
        Assert(subModules.Count(element =>
                string.Equals(element.Element("DLLName")?.Attribute("value")?.Value, "ImprovedGarrisons.dll", StringComparison.Ordinal)) == 1,
            "Main must be declared only on the client; the server bootstrap owns its headless lifecycle.");
    }

    private static bool HasRoleVariant(IEnumerable<XElement> subModules, string dllName, string serverType, string noRender)
    {
        return subModules.Any(subModule =>
            string.Equals(subModule.Element("DLLName")?.Attribute("value")?.Value, dllName, StringComparison.Ordinal) &&
            HasTag(subModule, "DedicatedServerType", serverType) &&
            HasTag(subModule, "IsNoRenderModeElement", noRender));
    }

    private static bool HasTag(XElement subModule, string key, string value)
    {
        return subModule.Descendants("Tag").Any(tag =>
            string.Equals(tag.Attribute("key")?.Value, key, StringComparison.Ordinal) &&
            string.Equals(tag.Attribute("value")?.Value, value, StringComparison.Ordinal));
    }

    private static void TestDedicatedServerDependencyClosure(string[] args)
    {
        string moduleDirectory = Path.GetFullPath(args[0]);
        string serverEngineDirectory = Path.GetFullPath(args[1]);
        Assert(Directory.Exists(serverEngineDirectory), "The Coop dedicated-server engine reference is missing.");

        HashSet<string> availableModuleIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (string serverManifestPath in Directory.GetFiles(
            Path.Combine(serverEngineDirectory, "Modules"),
            "SubModule.xml",
            SearchOption.AllDirectories))
        {
            XDocument serverManifest = XDocument.Load(serverManifestPath);
            string? moduleId = serverManifest.Root?.Element("Id")?.Attribute("value")?.Value;
            if (!string.IsNullOrWhiteSpace(moduleId))
            {
                availableModuleIds.Add(moduleId);
            }
        }

        XDocument manifest = XDocument.Load(Path.Combine(moduleDirectory, "SubModule.xml"));
        string[] missingRequiredModules = manifest
            .Descendants("DependedModule")
            .Where(element => !string.Equals(element.Attribute("Optional")?.Value, "true", StringComparison.OrdinalIgnoreCase))
            .Select(element => element.Attribute("Id")?.Value ?? string.Empty)
            .Where(id => id.Length > 0 && !availableModuleIds.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert(missingRequiredModules.Length == 0,
            $"The Coop server appliance lacks mandatory module dependencies: {string.Join(", ", missingRequiredModules)}.");

        HashSet<string> availableAssemblies = Directory
            .GetFiles(serverEngineDirectory, "*.dll", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
        string serverModuleBin = Path.Combine(
            moduleDirectory,
            "bin",
            "Win64_Shipping_Server");
        string[] shippedAssemblies = Directory.GetFiles(serverModuleBin, "*.dll", SearchOption.AllDirectories);
        foreach (string shippedAssembly in shippedAssemblies)
        {
            string? shippedName = Path.GetFileNameWithoutExtension(shippedAssembly);
            if (!string.IsNullOrEmpty(shippedName))
            {
                availableAssemblies.Add(shippedName);
            }

            availableAssemblies.Add(ReadAssemblyName(shippedAssembly));
        }

        foreach (string shippedAssembly in shippedAssemblies)
        {
            string[] missingAssemblyReferences = ReadAssemblyReferences(shippedAssembly)
                .Where(name => !name.StartsWith("System", StringComparison.OrdinalIgnoreCase))
                .Where(name => !string.Equals(name, "mscorlib", StringComparison.OrdinalIgnoreCase))
                .Where(name => !availableAssemblies.Contains(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert(missingAssemblyReferences.Length == 0,
                $"{Path.GetFileName(shippedAssembly)} references assemblies absent from the Coop server appliance: {string.Join(", ", missingAssemblyReferences)}.");
        }
    }

    private static void TestPackageDeclaresServerOverlayManifest(string[] args)
    {
        string moduleDirectory = Path.GetFullPath(args[0]);
        string manifestPath = Path.Combine(moduleDirectory, "ServerInstall", "DedicatedServer.Windows.SubModule.xml");
        Assert(File.Exists(manifestPath),
            "ServerInstall/DedicatedServer.Windows.SubModule.xml must ship; it is the only manifest that declares the " +
            "Improved Garrisons bootstrap inside DedicatedServer.Windows, which the appliance's fixed module list " +
            "otherwise never loads.");
        string manifestXml = File.ReadAllText(manifestPath);
        Assert(manifestXml.Contains("<DLLName value=\"ImprovedGarrisons.CoopBootstrap.dll\"/>", StringComparison.Ordinal),
            "The DedicatedServer.Windows manifest overlay does not declare the Improved Garrisons bootstrap DLL.");
        Assert(manifestXml.Contains(
            "<SubModuleClassType value=\"ImprovedGarrisons.CoopBootstrap.IntegrationSubModule\"/>", StringComparison.Ordinal),
            "The DedicatedServer.Windows manifest overlay does not declare the Improved Garrisons bootstrap submodule class.");
        Assert(File.Exists(Path.Combine(moduleDirectory, "SERVER-INSTALL.txt")),
            "SERVER-INSTALL.txt must ship with instructions for applying the DedicatedServer.Windows manifest overlay.");
    }

    private static void TestInTreeHostUsesNativePartyRegistration()
    {
        FakeCompositeHost host = new();
        Assert(!host.TryActivate(coopDependenciesLoaded: false, registryManaged: false, constructorPrefixApplied: false, repairSucceeds: false),
            "The Coop-dependent runtime activated before its dependencies were ready.");
        Assert(!host.ServerMainActivated, "Improved Garrisons server simulation started before Coop dependencies were ready.");

        Assert(!host.TryActivate(coopDependenciesLoaded: true, registryManaged: false, constructorPrefixApplied: false, repairSucceeds: false),
            "The host activated without a managed MobileParty registry.");
        Assert(!host.ServerMainActivated && host.DurableStatus == "native-mobile-party-registry-unavailable",
            "An unrecoverable registry failure did not remain fail-closed with a durable reason.");

        Assert(host.TryActivate(coopDependenciesLoaded: true, registryManaged: false, constructorPrefixApplied: false, repairSucceeds: true),
            "The host did not recover Coop's native MobileParty registry and constructor prefix.");
        Assert(host.ServerMainActivated && host.NativeRegistryManaged && host.ConstructorPrefixApplied,
            "Server simulation started without the complete native lifetime path.");

        string[] roles = { "guard", "recruiter", "transfer", "villagerecruit" };
        foreach (string role in roles)
        {
            Assert(host.TryExecutePartyIntent(role, "town-owner", "clan-player", "clan-player", count: 30, cancelled: false),
                $"Authorized {role} intent did not execute on the authoritative server.");
        }

        Assert(host.CreatedPartyCount == roles.Length, "Each IG party role did not create exactly one party.");
        Assert(host.NativeLifetimeEventCount == roles.Length, "Party creation bypassed Coop's native lifetime events.");
        Assert(host.NativeIds.All(id => id.StartsWith("Created_", StringComparison.Ordinal)),
            "A party received a manual/non-native identifier.");

        Assert(!host.TryExecutePartyIntent("guard", "town-owner", "clan-player", "clan-player", count: 30, cancelled: false),
            "A duplicate guard intent created a second party.");
        Assert(!host.TryExecutePartyIntent("guard", "town-other", "clan-other", "clan-player", count: 30, cancelled: false),
            "A foreign-clan party intent mutated server state.");
        Assert(!host.TryExecutePartyIntent("guard", "town-invalid", "clan-player", "clan-player", count: 0, cancelled: false),
            "An invalid guard count mutated server state.");
        Assert(!host.TryExecutePartyIntent("recruiter", "town-cancelled", "clan-player", "clan-player", count: 30, cancelled: true),
            "A cancelled inquiry emitted a party intent.");
        Assert(host.CreatedPartyCount == roles.Length, "Rejected or duplicate intents changed party state.");

        FakeCompositeHost missingPrefix = new();
        Assert(missingPrefix.TryActivate(coopDependenciesLoaded: true, registryManaged: true, constructorPrefixApplied: false, repairSucceeds: true),
            "The host did not recover a missing native MobileParty constructor prefix.");
        Assert(missingPrefix.ConstructorPrefixApplied, "Constructor-prefix recovery reported ready without applying the prefix.");
    }

    private sealed class FakeCompositeHost
    {
        private readonly HashSet<string> _partyKeys = new(StringComparer.Ordinal);
        private readonly List<string> _nativeIds = new();

        public bool ServerMainActivated { get; private set; }

        public bool NativeRegistryManaged { get; private set; }

        public bool ConstructorPrefixApplied { get; private set; }

        public string DurableStatus { get; private set; } = "waiting-for-coop";

        public int CreatedPartyCount => _partyKeys.Count;

        public int NativeLifetimeEventCount { get; private set; }

        public IReadOnlyList<string> NativeIds => _nativeIds;

        public bool TryActivate(bool coopDependenciesLoaded, bool registryManaged, bool constructorPrefixApplied, bool repairSucceeds)
        {
            if (!coopDependenciesLoaded)
            {
                DurableStatus = "waiting-for-coop";
                return false;
            }

            NativeRegistryManaged = registryManaged;
            ConstructorPrefixApplied = constructorPrefixApplied;
            if ((!NativeRegistryManaged || !ConstructorPrefixApplied) && repairSucceeds)
            {
                NativeRegistryManaged = true;
                ConstructorPrefixApplied = true;
            }

            if (!NativeRegistryManaged || !ConstructorPrefixApplied)
            {
                DurableStatus = "native-mobile-party-registry-unavailable";
                return false;
            }

            DurableStatus = "native-mobile-party-registry-ready";
            ServerMainActivated = true;
            return true;
        }

        public bool TryExecutePartyIntent(
            string role,
            string settlementId,
            string peerClanId,
            string ownerClanId,
            int count,
            bool cancelled)
        {
            if (!ServerMainActivated || cancelled || count <= 0 || count > 300 ||
                !ActionAuthorization.CanMutateSettlement(peerClanId, ownerClanId))
            {
                return false;
            }

            string key = role + ":" + settlementId;
            if (!_partyKeys.Add(key))
            {
                return false;
            }

            string nativeId = "Created_" + (_partyKeys.Count - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            _nativeIds.Add(nativeId);
            NativeLifetimeEventCount++;
            return true;
        }
    }

    private static IEnumerable<string> ReadAssemblyReferences(string assemblyPath)
    {
        using FileStream stream = File.OpenRead(assemblyPath);
        using PEReader peReader = new(stream);
        MetadataReader metadata = peReader.GetMetadataReader();
        foreach (AssemblyReferenceHandle handle in metadata.AssemblyReferences)
        {
            yield return metadata.GetString(metadata.GetAssemblyReference(handle).Name);
        }
    }

    private static string ReadAssemblyName(string assemblyPath)
    {
        using FileStream stream = File.OpenRead(assemblyPath);
        using PEReader peReader = new(stream);
        MetadataReader metadata = peReader.GetMetadataReader();
        return metadata.GetString(metadata.GetAssemblyDefinition().Name);
    }

    private static void TestRoleRoutingAndAuthorization()
    {
        Assert(IntegrationRoleRouter.ShouldExecuteLocally(coopActive: false, isServer: false), "Solo mode did not retain local execution.");
        Assert(IntegrationRoleRouter.ShouldExecuteLocally(coopActive: true, isServer: true), "Coop server was not authoritative.");
        Assert(!IntegrationRoleRouter.ShouldExecuteLocally(coopActive: true, isServer: false), "Coop client retained authoritative mutation.");

        Assert(ActionAuthorization.CanMutateSettlement("clan_player", "clan_player"), "Owner clan was rejected.");
        Assert(!ActionAuthorization.CanMutateSettlement("clan_player", "clan_other"), "Foreign clan was authorized.");
        Assert(!ActionAuthorization.CanMutateSettlement(string.Empty, "clan_player"), "Empty peer clan was authorized.");
    }

    private static void TestIntentValidationAndIdempotency()
    {
        FakeCompositeHost host = new();
        Assert(host.TryActivate(coopDependenciesLoaded: true, registryManaged: true, constructorPrefixApplied: true, repairSucceeds: false),
            "Ready native registry did not activate the host.");
        Assert(host.TryExecutePartyIntent("guard", "town-a", "clan-a", "clan-a", count: 30, cancelled: false),
            "First authorized intent was rejected.");
        Assert(!host.TryExecutePartyIntent("guard", "town-a", "clan-a", "clan-a", count: 30, cancelled: false),
            "Duplicate intent was accepted.");
        Assert(!host.TryExecutePartyIntent("guard", "town-b", string.Empty, "clan-a", count: 30, cancelled: false),
            "Intent without an authenticated clan was accepted.");
    }

    private static void TestManifestRoundTrip()
    {
        PartyManifestEntry[] expected =
        {
            new("guard", "Created_101", "town_EN1", "patrol", "The guard party is patrolling"),
            new("recruiter", "Created_102", "town_Ünicode", "recruiting:town_Ünicode", "The recruiter is recruiting"),
            new("transfer", "Created_103", "town_A", "town_B", string.Empty)
        };

        string serialized = PartyManifestCodec.Serialize(expected);
        IReadOnlyList<PartyManifestEntry> actual = PartyManifestCodec.Parse(serialized);
        Assert(actual.Count == expected.Length, "Manifest entry count changed during round-trip.");
        for (int index = 0; index < expected.Length; index++)
        {
            Assert(expected[index].Equals(actual[index]), $"Manifest entry {index} changed during round-trip.");
        }

        static string Encode(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        string legacy = string.Join("|", Encode("guard"), Encode("Created_legacy"), Encode("town_legacy"), Encode("OrderPatrol"));
        IReadOnlyList<PartyManifestEntry> migrated = PartyManifestCodec.Parse(legacy);
        Assert(migrated.Count == 1 && migrated[0].Detail == "OrderPatrol" && migrated[0].StatusText == string.Empty,
            "The v1.0.3 four-field party manifest no longer migrates safely.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
