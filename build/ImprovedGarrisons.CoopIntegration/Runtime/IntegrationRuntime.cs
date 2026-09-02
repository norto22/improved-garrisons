using System;
using Common;
using ImprovedGarrisons.CoopIntegration.Patching;
using ImprovedGarrisons.CoopIntegration.Persistence;

namespace ImprovedGarrisons.CoopIntegration.Runtime
{
    public static class IntegrationRuntime
    {
        private const int FailureRetryMilliseconds = 5_000;
        private static int _consecutiveFailures;
        private static int _nextRetry;
        private static bool _patchesApplied;
        private static bool _initialized;

        public static bool CoopActive => IntegrationTransport.IsConnected;

        public static bool NativePartyRegistrationReady => CoopMobilePartyRegistration.IsReady;

        public static bool IsServer
        {
            get
            {
                try
                {
                    return ModInformation.IsServer;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public static bool Initialize()
        {
            if (!_patchesApplied)
            {
                ClientServerPatches.Apply();
                _patchesApplied = true;
            }

            if (IsServer && !CoopMobilePartyRegistration.EnsureReady(out string status))
            {
                RuntimeStatus.Write(status);
                return false;
            }

            RuntimeStatus.Write(IsServer ? "native-mobile-party-registry-ready" : "client-runtime-ready");
            if (!_initialized)
            {
                _initialized = true;
                IntegrationLog.Information("runtime initialized with Coop-native MobileParty registration ready");
            }

            return true;
        }

        public static void Tick(float deltaTime)
        {
            int now = Environment.TickCount;
            if (_nextRetry != 0 && unchecked(now - _nextRetry) < 0)
            {
                return;
            }

            try
            {
                if (IsServer && !CoopMobilePartyRegistration.EnsureReady(out string status))
                {
                    RuntimeStatus.Write(status);
                    _nextRetry = unchecked(now + FailureRetryMilliseconds);
                    return;
                }

                IntegrationTransport.Poll();
                if (IntegrationTransport.IsConnected)
                {
                    PartyManifestStore.Poll();
                }

                _consecutiveFailures = 0;
                _nextRetry = 0;
            }
            catch (Exception exception)
            {
                _consecutiveFailures++;
                _nextRetry = unchecked(now + FailureRetryMilliseconds);
                string failure = "runtime-tick-failed:" + exception.GetBaseException().GetType().Name;
                RuntimeStatus.Write(failure);
                IntegrationLog.Error("runtime tick failed (attempt " + _consecutiveFailures + "; retrying): " + exception.GetBaseException());
            }
        }
    }
}
