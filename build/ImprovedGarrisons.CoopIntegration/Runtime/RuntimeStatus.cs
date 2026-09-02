using System;
using System.IO;
using System.Reflection;

namespace ImprovedGarrisons.CoopIntegration.Runtime
{
    internal static class RuntimeStatus
    {
        private static readonly object Sync = new object();
        private static string? _lastStatus;

        public static void Write(string status)
        {
            lock (Sync)
            {
                if (string.Equals(_lastStatus, status, StringComparison.Ordinal))
                {
                    return;
                }

                _lastStatus = status;
                try
                {
                    string? current = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    for (int depth = 0; depth < 7 && !string.IsNullOrEmpty(current); depth++)
                    {
                        if (File.Exists(Path.Combine(current, "SubModule.xml")))
                        {
                            string moduleData = Path.Combine(current, "ModuleData");
                            Directory.CreateDirectory(moduleData);
                            File.WriteAllText(
                                Path.Combine(moduleData, "CoopRuntime.status"),
                                DateTime.UtcNow.ToString("O") + Environment.NewLine +
                                status + Environment.NewLine +
                                "activation-retries=automatic" + Environment.NewLine);
                            return;
                        }

                        current = Path.GetDirectoryName(current);
                    }
                }
                catch (Exception exception)
                {
                    Console.WriteLine("[ImprovedGarrisons] could not write Coop runtime status: " + exception.GetBaseException().Message);
                }
            }
        }
    }
}
