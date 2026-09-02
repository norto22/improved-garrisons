using System;
using System.IO;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ImprovedGarrisons.CoopBootstrap
{
    public sealed class IntegrationSubModule : MBSubModuleBase
    {
        private const string RuntimeAssemblyName = "ImprovedGarrisons.CoopIntegration";
        private static bool _initialized;
        private static Action<float>? _tickRuntime;
        private static int _nextActivationAttempt;
        private static int _activationFailures;
        private static bool _runtimeReady;
        private static string? _lastStatus;
        private static object? _serverMain;
        private static bool _pendingBeforeInitialScreen;
        private static Game? _pendingGameStartGame;
        private static IGameStarter? _pendingGameStarter;
        private static Game? _pendingGameLoadedGame;
        private static object? _pendingGameLoadedInitializer;
        private static Game? _pendingCampaignStartGame;
        private static object? _pendingCampaignStarter;
        private static Game? _pendingInitializationFinishedGame;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            WriteStatus("bootstrap-loaded");
            Console.WriteLine("[ImprovedGarrisons] Coop bootstrap loaded; waiting for BannerlordCoop networking.");
            EnsureRuntimeAndServerMain();
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            EnsureRuntimeAndServerMain();
            _tickRuntime?.Invoke(dt);
            InvokeServerMain("OnApplicationTick", new[] { typeof(float) }, dt);
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            EnsureRuntimeAndServerMain();
            if (_serverMain == null)
            {
                _pendingBeforeInitialScreen = true;
            }
            else
            {
                InvokeServerMain("OnBeforeInitialModuleScreenSetAsRoot", Type.EmptyTypes);
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            EnsureRuntimeAndServerMain();
            if (_serverMain == null)
            {
                _pendingGameStartGame = game;
                _pendingGameStarter = gameStarterObject;
            }
            else
            {
                InvokeServerMain("OnGameStart", new[] { typeof(Game), typeof(IGameStarter) }, game, gameStarterObject);
            }
        }

        public override void OnGameLoaded(Game game, object initializerObject)
        {
            base.OnGameLoaded(game, initializerObject);
            EnsureRuntimeAndServerMain();
            if (_serverMain == null)
            {
                _pendingGameLoadedGame = game;
                _pendingGameLoadedInitializer = initializerObject;
            }
            else
            {
                InvokeServerMain("OnGameLoaded", new[] { typeof(Game), typeof(object) }, game, initializerObject);
            }
        }

        public override void OnCampaignStart(Game game, object starterObject)
        {
            base.OnCampaignStart(game, starterObject);
            EnsureRuntimeAndServerMain();
            if (_serverMain == null)
            {
                _pendingCampaignStartGame = game;
                _pendingCampaignStarter = starterObject;
            }
            else
            {
                InvokeServerMain("OnCampaignStart", new[] { typeof(Game), typeof(object) }, game, starterObject);
            }
        }

        public override void OnGameInitializationFinished(Game game)
        {
            base.OnGameInitializationFinished(game);
            EnsureRuntimeAndServerMain();
            if (_serverMain == null)
            {
                _pendingInitializationFinishedGame = game;
            }
            else
            {
                InvokeServerMain("OnGameInitializationFinished", new[] { typeof(Game) }, game);
            }
        }

        public override void OnGameEnd(Game game)
        {
            InvokeServerMain("OnGameEnd", new[] { typeof(Game) }, game);
            base.OnGameEnd(game);
        }

        protected override void OnSubModuleUnloaded()
        {
            InvokeServerMain("OnSubModuleUnloaded", Type.EmptyTypes);
            base.OnSubModuleUnloaded();
        }

        private static void ActivateServerMain()
        {
            try
            {
                Assembly mainAssembly = FindLoadedAssembly("ImprovedGarrisons") ?? Assembly.LoadFrom(GetMainPath());
                Type? mainType = mainAssembly.GetType("ImprovedGarrisons.Main", false, false);
                if (mainType == null)
                {
                    throw new TypeLoadException("ImprovedGarrisons.Main is missing.");
                }

                _serverMain = Activator.CreateInstance(mainType);
                if (_serverMain == null)
                {
                    throw new InvalidOperationException("ImprovedGarrisons.Main could not be created.");
                }

                InvokeServerMain("OnSubModuleLoad", Type.EmptyTypes);
                ReplayPendingServerLifecycle();
                WriteStatus("server-main-activated");
            }
            catch (Exception exception)
            {
                WriteStatus("server-main-failed:" + exception.GetBaseException().GetType().Name);
                throw;
            }
        }

        private static void InvokeServerMain(string methodName, Type[] parameterTypes, params object[] arguments)
        {
            object? serverMain = _serverMain;
            if (serverMain == null)
            {
                return;
            }

            MethodInfo? method = serverMain.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            method?.Invoke(serverMain, arguments);
        }

        private static void ReplayPendingServerLifecycle()
        {
            if (_pendingBeforeInitialScreen)
            {
                _pendingBeforeInitialScreen = false;
                InvokeServerMain("OnBeforeInitialModuleScreenSetAsRoot", Type.EmptyTypes);
            }

            if (_pendingGameStartGame != null && _pendingGameStarter != null)
            {
                InvokeServerMain(
                    "OnGameStart",
                    new[] { typeof(Game), typeof(IGameStarter) },
                    _pendingGameStartGame,
                    _pendingGameStarter);
                _pendingGameStartGame = null;
                _pendingGameStarter = null;
            }

            if (_pendingGameLoadedGame != null && _pendingGameLoadedInitializer != null)
            {
                InvokeServerMain(
                    "OnGameLoaded",
                    new[] { typeof(Game), typeof(object) },
                    _pendingGameLoadedGame,
                    _pendingGameLoadedInitializer);
                _pendingGameLoadedGame = null;
                _pendingGameLoadedInitializer = null;
            }

            if (_pendingCampaignStartGame != null && _pendingCampaignStarter != null)
            {
                InvokeServerMain(
                    "OnCampaignStart",
                    new[] { typeof(Game), typeof(object) },
                    _pendingCampaignStartGame,
                    _pendingCampaignStarter);
                _pendingCampaignStartGame = null;
                _pendingCampaignStarter = null;
            }

            if (_pendingInitializationFinishedGame != null)
            {
                InvokeServerMain(
                    "OnGameInitializationFinished",
                    new[] { typeof(Game) },
                    _pendingInitializationFinishedGame);
                _pendingInitializationFinishedGame = null;
            }
        }

        private static void EnsureRuntimeAndServerMain()
        {
            if (!_runtimeReady)
            {
                _runtimeReady = TryActivateRuntime();
            }

            if (_runtimeReady && GameNetwork.IsDedicatedServer && _serverMain == null)
            {
                ActivateServerMain();
            }
        }

        private static bool TryActivateRuntime()
        {
            if (_runtimeReady && _tickRuntime != null)
            {
                return true;
            }

            int now = Environment.TickCount;
            if (_nextActivationAttempt != 0 && unchecked(now - _nextActivationAttempt) < 0)
            {
                return false;
            }

            _nextActivationAttempt = unchecked(now + 2_000);
            string[] requiredAssemblies =
            {
                "Common",
                "Coop.Core",
                "GameInterface",
                "0Harmony",
                "LiteNetLib",
                "protobuf-net.Core",
                "Serilog"
            };
            string? missingAssembly = null;
            foreach (string requiredAssembly in requiredAssemblies)
            {
                if (!IsAssemblyLoaded(requiredAssembly))
                {
                    missingAssembly = requiredAssembly;
                    break;
                }
            }

            if (missingAssembly != null)
            {
                WriteStatus("waiting-for-assembly:" + missingAssembly);
                return false;
            }

            try
            {
                _ = FindLoadedAssembly("ImprovedGarrisons") ?? Assembly.LoadFrom(GetMainPath());
                Assembly runtimeAssembly = FindLoadedAssembly(RuntimeAssemblyName) ?? Assembly.LoadFrom(GetRuntimePath());
                Type? runtimeType = runtimeAssembly.GetType(
                    "ImprovedGarrisons.CoopIntegration.Runtime.IntegrationRuntime",
                    false,
                    false);
                MethodInfo? initializeMethod = runtimeType?.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                MethodInfo? tickMethod = runtimeType?.GetMethod("Tick", BindingFlags.Public | BindingFlags.Static);
                if (initializeMethod == null || tickMethod == null)
                {
                    throw new MissingMethodException("The Coop integration runtime entry points are missing.");
                }

                Func<bool> initializeRuntime = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), initializeMethod);
                Action<float> tickRuntime = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), tickMethod);
                if (!initializeRuntime())
                {
                    _tickRuntime = null;
                    return false;
                }

                _tickRuntime = tickRuntime;
                _activationFailures = 0;
                bool dedicatedServer = GameNetwork.IsDedicatedServer;
                WriteStatus(dedicatedServer ? "native-mobile-party-registry-ready" : "client-runtime-ready");
                Console.WriteLine(dedicatedServer
                    ? "[ImprovedGarrisons] Coop integration ready; native MobileParty registration verified."
                    : "[ImprovedGarrisons] Coop client integration ready; waiting for the host campaign.");
                return true;
            }
            catch (Exception exception)
            {
                _tickRuntime = null;
                _activationFailures++;
                int retryDelay = Math.Min(30_000, 2_000 << Math.Min(_activationFailures, 3));
                _nextActivationAttempt = unchecked(now + retryDelay);
                WriteStatus("runtime-activation-failed:" + exception.GetBaseException().GetType().Name);
                Console.WriteLine("[ImprovedGarrisons] Coop runtime activation failed (attempt " + _activationFailures + "; retrying): " + exception.GetBaseException());
                return false;
            }
        }

        private static void WriteStatus(string status)
        {
            if (string.Equals(_lastStatus, status, StringComparison.Ordinal))
            {
                return;
            }

            _lastStatus = status;
            try
            {
                string binDirectory = GetModuleBinDirectory();
                string? moduleDirectory = Path.GetDirectoryName(Path.GetDirectoryName(binDirectory));
                if (!string.IsNullOrEmpty(moduleDirectory))
                {
                    string moduleData = Path.Combine(moduleDirectory, "ModuleData");
                    Directory.CreateDirectory(moduleData);
                    File.WriteAllText(
                        Path.Combine(moduleData, "CoopRuntime.status"),
                        DateTime.UtcNow.ToString("O") + Environment.NewLine +
                        status + Environment.NewLine +
                        "activation-retries=automatic" + Environment.NewLine);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("[ImprovedGarrisons] could not write Coop runtime status: " + exception.GetBaseException().Message);
            }
        }

        private static string GetRuntimePath()
        {
            string runtimePath = Path.Combine(GetModuleBinDirectory(), "Adapters", RuntimeAssemblyName + ".dll");
            if (!File.Exists(runtimePath))
            {
                throw new FileNotFoundException("The Improved Garrisons Coop runtime is missing.", runtimePath);
            }

            return runtimePath;
        }

        private static string GetMainPath()
        {
            string mainPath = Path.Combine(GetModuleBinDirectory(), "ImprovedGarrisons.dll");
            if (!File.Exists(mainPath))
            {
                throw new FileNotFoundException("The Improved Garrisons main assembly is missing.", mainPath);
            }

            return mainPath;
        }

        private static string GetModuleBinDirectory()
        {
            string? bootstrapDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(bootstrapDirectory))
            {
                throw new DirectoryNotFoundException("The Improved Garrisons bootstrap directory could not be resolved.");
            }

            if (File.Exists(Path.Combine(bootstrapDirectory, "ImprovedGarrisons.dll")))
            {
                return bootstrapDirectory;
            }

            string? current = bootstrapDirectory;
            for (int depth = 0; depth < 8 && !string.IsNullOrEmpty(current); depth++)
            {
                string moduleDirectory = Path.Combine(current, "ImprovedGarrisons");
                if (File.Exists(Path.Combine(moduleDirectory, "SubModule.xml")))
                {
                    string clientBin = Path.Combine(moduleDirectory, "bin", "Win64_Shipping_Client");
                    if (File.Exists(Path.Combine(clientBin, "ImprovedGarrisons.dll")))
                    {
                        return clientBin;
                    }

                    string serverBin = Path.Combine(moduleDirectory, "bin", "Win64_Shipping_Server");
                    if (File.Exists(Path.Combine(serverBin, "ImprovedGarrisons.dll")))
                    {
                        return serverBin;
                    }
                }

                current = Path.GetDirectoryName(current);
            }

            throw new DirectoryNotFoundException("The Improved Garrisons module bin directory could not be located.");
        }

        private static bool IsAssemblyLoaded(string simpleName)
        {
            return FindLoadedAssembly(simpleName) != null;
        }

        private static Assembly? FindLoadedAssembly(string simpleName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(assembly.GetName().Name, simpleName, StringComparison.Ordinal))
                {
                    return assembly;
                }
            }

            return null;
        }
    }
}
