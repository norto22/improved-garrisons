using System;
using System.Linq;
using System.Reflection;
using Autofac;
using GameInterface;
using GameInterface.AutoSync;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace ImprovedGarrisons.CoopIntegration.Runtime
{
    // BannerlordCoop only ever registers and Harmony-patches MobileParty (RegistryManager.RegisterAllGameObjects
    // + PatchLifetimes) in response to its own CampaignReady message, which is itself only ever published from a
    // Harmony postfix on SandBox.View.Map.MapScreen.OnInitialize() (GameInterface.Services.GameState.Patches.
    // GameLoadedPatch) -- a UI view class. A headless dedicated server never creates a MapScreen, so that patch
    // never fires, CampaignReady is never published, and Coop's own MobileParty setup never runs -- on every
    // boot, permanently, not as an occasional miss. This class is therefore not a repair for a rare failure: on
    // a dedicated server it is the only thing that will ever perform this setup, every time. It does the
    // identical work Coop's own GameLoadedPatch would have triggered (construct the native registry if it was
    // never auto-activated, then apply its queued Harmony prefix), just without a UI event to hang it off.
    internal static class CoopMobilePartyRegistration
    {
        private const string NativeRegistryTypeName = "GameInterface.Services.MobileParties.MobilePartyRegistry";
        private static readonly object Sync = new object();
        private static ILifetimeScope? _container;
        private static bool _ready;

        public static bool IsReady
        {
            get
            {
                lock (Sync)
                {
                    return _ready;
                }
            }
        }

        public static bool EnsureReady(out string status)
        {
            lock (Sync)
            {
                if (!ContainerProvider.TryGetContainer(out ILifetimeScope container))
                {
                    Reset(null);
                    status = "waiting-for-coop-container";
                    return false;
                }

                if (!ReferenceEquals(_container, container))
                {
                    Reset(container);
                }

                if (_ready)
                {
                    status = "native-mobile-party-registry-ready";
                    return true;
                }

                if (!container.TryResolve(out IAutoRegistryFactory? registryFactory) || registryFactory == null ||
                    !container.TryResolve(out IAutoSyncPatchCollector? patchCollector) || patchCollector == null)
                {
                    status = "native-mobile-party-registry-services-unavailable";
                    return false;
                }

                bool managed = registryFactory.IsManaged(typeof(MobileParty));
                bool constructorPatched = HasNativeConstructorPrefixes();
                string? setupFailure = null;

                // Gate on the actual missing piece (`constructorPatched`), not the coarser `managed` signal --
                // IsManaged() walks up MobileParty's base-type chain, so it can read true from an unrelated
                // registry even when MobileParty's own was never built. Retry indefinitely (the caller already
                // throttles how often this runs): Coop's own trigger will never arrive to make this moot, so
                // this keeps being the only path until it succeeds.
                if (!constructorPatched)
                {
                    try
                    {
                        if (!managed)
                        {
                            ActivateNativeRegistry(container);
                        }

                        registryFactory.RegisterAll();
                        patchCollector.PatchAll();
                    }
                    catch (Exception exception)
                    {
                        Exception root = exception.GetBaseException();
                        setupFailure = root.GetType().Name + ": " + root.Message;
                        IntegrationLog.Error("native MobileParty registration setup failed: " + exception);
                    }

                    managed = registryFactory.IsManaged(typeof(MobileParty));
                    constructorPatched = HasNativeConstructorPrefixes();
                }

                _ready = managed && constructorPatched;
                status = _ready
                    ? "native-mobile-party-registry-ready"
                    : setupFailure != null
                        ? Truncate("native-mobile-party-setup-threw:" + setupFailure, 400)
                        : !managed
                            ? "native-mobile-party-registry-unavailable"
                            : Truncate("native-mobile-party-constructor-prefix-unavailable:" + DescribeConstructorPrefixes(), 400);
                return _ready;
            }
        }

        public static bool ValidateCreatedParty(MobileParty? party)
        {
            lock (Sync)
            {
                if (!_ready || party == null || string.IsNullOrWhiteSpace(party.StringId) ||
                    !ContainerProvider.TryResolve(out IObjectManager? objectManager) || objectManager == null ||
                    !objectManager.TryGetId(party, out string objectId) || string.IsNullOrWhiteSpace(objectId))
                {
                    _ready = false;
                    RuntimeStatus.Write("native-mobile-party-postcondition-failed");
                    IntegrationLog.Error("Coop-native MobileParty creation postcondition failed; further Improved Garrisons party creation is blocked.");
                    return false;
                }

                return true;
            }
        }

        private static void ActivateNativeRegistry(ILifetimeScope container)
        {
            Assembly gameInterface = typeof(IAutoRegistryFactory).Assembly;
            Type registryType = gameInterface.GetType(NativeRegistryTypeName, true, false)
                ?? throw new TypeLoadException(NativeRegistryTypeName + " is missing.");
            ConstructorInfo constructor = registryType
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .OrderByDescending(candidate => candidate.GetParameters().Length)
                .FirstOrDefault()
                ?? throw new MissingMethodException(NativeRegistryTypeName + " has no constructor.");
            object?[] arguments = constructor.GetParameters()
                .Select(parameter => ResolveParameter(container, parameter))
                .ToArray();
            constructor.Invoke(arguments);
            IntegrationLog.Warning("Coop native MobilePartyRegistry was never auto-activated by Coop's own container build and has been constructed directly through the existing Coop container.");
        }

        private static object? ResolveParameter(ILifetimeScope container, ParameterInfo parameter)
        {
            if (container.TryResolve(parameter.ParameterType, out object? value))
            {
                return value;
            }

            if (parameter.HasDefaultValue)
            {
                return parameter.DefaultValue;
            }

            throw new InvalidOperationException("Coop container cannot resolve " + parameter.ParameterType.FullName + ".");
        }

        private static bool HasNativeConstructorPrefixes()
        {
            // Identify the prefix by method name + declaring generic type only, not by Harmony owner id.
            // AutoRegistryFactory's own Harmony instance is named "CoopAutoRegistryFactory", but that is not
            // the instance actually used to patch MobileParty's constructor -- AutoSyncPatchCollector patches
            // with whichever Harmony instance Coop's own container injects into it (observed live: named
            // "Bannerlord.Coop"), an implementation detail this class has no business hardcoding. A closed
            // generic method+type match (LifetimePatches<MobileParty>.CreatePrefix) is already unambiguous.
            ConstructorInfo[] constructors = AccessTools.GetDeclaredConstructors(typeof(MobileParty)).ToArray();
            return constructors.Length > 0 && constructors.All(constructor =>
            {
                Patches? patchInfo = Harmony.GetPatchInfo(constructor);
                return patchInfo != null && patchInfo.Prefixes.Any(prefix =>
                    string.Equals(prefix.PatchMethod?.Name, "CreatePrefix", StringComparison.Ordinal) &&
                    prefix.PatchMethod?.DeclaringType?.FullName?.StartsWith(
                        "GameInterface.Registry.Auto.LifetimePatches`1",
                        StringComparison.Ordinal) == true);
            });
        }

        private static string DescribeConstructorPrefixes()
        {
            ConstructorInfo[] constructors = AccessTools.GetDeclaredConstructors(typeof(MobileParty)).ToArray();
            if (constructors.Length == 0)
            {
                return "0 declared constructors found on " + typeof(MobileParty).AssemblyQualifiedName;
            }

            return string.Join(" | ", constructors.Select(constructor =>
            {
                Patches? patchInfo = Harmony.GetPatchInfo(constructor);
                int prefixCount = patchInfo?.Prefixes.Count ?? 0;
                string owners = prefixCount == 0
                    ? "none"
                    : string.Join(",", patchInfo!.Prefixes.Select(prefix => prefix.owner + "/" + prefix.PatchMethod?.Name));
                return prefixCount + " prefixes (" + owners + ")";
            }));
        }

        private static void Reset(ILifetimeScope? container)
        {
            _container = container;
            _ready = false;
        }

        private static string Truncate(string value, int maxLength)
        {
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
