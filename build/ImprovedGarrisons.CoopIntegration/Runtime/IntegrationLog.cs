using Common.Logging;
using Serilog;

namespace ImprovedGarrisons.CoopIntegration.Runtime
{
    internal static class IntegrationLog
    {
        private sealed class IntegrationLogCategory
        {
        }

        private static readonly ILogger Logger = LogManager.GetLogger<IntegrationLogCategory>();

        public static void Information(string message)
        {
            Logger.Information("[ImprovedGarrisons] {Message}", message);
        }

        public static void Warning(string message)
        {
            Logger.Warning("[ImprovedGarrisons] {Message}", message);
        }

        public static void Error(string message)
        {
            Logger.Error("[ImprovedGarrisons] {Message}", message);
        }
    }
}
