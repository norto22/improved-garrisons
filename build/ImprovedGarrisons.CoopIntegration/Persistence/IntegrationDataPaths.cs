using System;
using System.IO;
using System.Reflection;

namespace ImprovedGarrisons.CoopIntegration.Persistence
{
    internal static class IntegrationDataPaths
    {
        private static readonly string[] PersistedFileNames =
        {
            "settlement-settings.txt",
            "settlement-settings.txt.bak",
            "party-manifest.txt",
            "party-manifest.txt.bak"
        };

        private static readonly object Sync = new object();
        private static string? _directory;

        public static string FilePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("A safe file name is required.", nameof(fileName));
            }

            string directory = ResolveDirectory();
            return directory.Length == 0 ? string.Empty : Path.Combine(directory, fileName);
        }

        public static void WriteAtomic(string path, string contents)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string? directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, contents ?? string.Empty);
            if (File.Exists(path))
            {
                string backup = path + ".bak";
                File.Replace(temporary, path, backup, true);
            }
            else
            {
                File.Move(temporary, path);
            }
        }

        private static string ResolveDirectory()
        {
            lock (Sync)
            {
                if (_directory != null)
                {
                    return _directory;
                }

                try
                {
                    // The dedicated-server launcher publishes its resolved --data-dir through
                    // BANNERLORD_USER_DIR. CoopSaveManager uses the same variable for the paired campaign
                    // JSON files, so this follows custom data directories instead of reconstructing the
                    // default Documents path by convention. Normal clients do not set the variable and use
                    // the Documents fallback below.
                    string? coopDataDirectory = ResolveCoopDataDirectory();
                    if (!string.IsNullOrEmpty(coopDataDirectory))
                    {
                        string persistentDirectory = Path.Combine(coopDataDirectory, "ImprovedGarrisons");
                        Directory.CreateDirectory(persistentDirectory);

                        string? legacyDirectory = FindLegacyDirectory();
                        if (legacyDirectory != null && legacyDirectory.Length > 0 && !SameDirectory(legacyDirectory, persistentDirectory))
                        {
                            MigrateLegacyData(legacyDirectory, persistentDirectory);
                        }

                        // BANNERLORD_USER_DIR can point somewhere other than the Documents-based directory
                        // every prior release always used. Migrate from there too, or a server upgrading with
                        // the variable already set (the documented standard config) silently loses its data.
                        // This is best-effort: a failure here must not sink the persistentDirectory resolution
                        // that already succeeded above.
                        try
                        {
                            string? documentsDirectory = ResolveDocumentsUserDirectory();
                            if (!string.IsNullOrEmpty(documentsDirectory))
                            {
                                string documentsPersistentDirectory = Path.Combine(documentsDirectory, "ImprovedGarrisons");
                                if (!SameDirectory(documentsPersistentDirectory, persistentDirectory))
                                {
                                    MigrateLegacyData(documentsPersistentDirectory, persistentDirectory);
                                }
                            }
                        }
                        catch (Exception exception)
                        {
                            Runtime.IntegrationLog.Error("Documents-based legacy migration unavailable: " + exception.GetBaseException().Message);
                        }

                        _directory = persistentDirectory;
                        return persistentDirectory;
                    }
                }
                catch (Exception exception)
                {
                    Runtime.IntegrationLog.Error("Coop data directory unavailable: " + exception.GetBaseException().Message);
                }

                try
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    if (!string.IsNullOrEmpty(appData))
                    {
                        string persistentDirectory = Path.Combine(appData, "BannerlordCoop", "server-data", "improved-garrisons");
                        Directory.CreateDirectory(persistentDirectory);

                        string? legacyDirectory = FindLegacyDirectory();
                        if (legacyDirectory != null && legacyDirectory.Length > 0 && !SameDirectory(legacyDirectory, persistentDirectory))
                        {
                            MigrateLegacyData(legacyDirectory, persistentDirectory);
                        }

                        _directory = persistentDirectory;
                        return persistentDirectory;
                    }
                }
                catch (Exception exception)
                {
                    Runtime.IntegrationLog.Error("LocalAppData data directory unavailable: " + exception.GetBaseException().Message);
                }

                // Independent try/catch: an exception in the LocalAppData branch above must not skip this
                // last-resort attempt, or persistence silently disables itself for the rest of the process.
                try
                {
                    string? lastResortDirectory = FindLegacyDirectory();
                    if (lastResortDirectory != null && lastResortDirectory.Length > 0)
                    {
                        Directory.CreateDirectory(lastResortDirectory);
                        _directory = lastResortDirectory;
                        Runtime.IntegrationLog.Warning("using legacy install-tree persistence directory as a last resort: " + lastResortDirectory);
                        return lastResortDirectory;
                    }
                }
                catch (Exception exception)
                {
                    Runtime.IntegrationLog.Error("last-resort data directory unavailable: " + exception.GetBaseException().Message);
                }

                _directory = string.Empty;
                return _directory;
            }
        }

        private static string? ResolveCoopDataDirectory()
        {
            string? configured = Environment.GetEnvironmentVariable("BANNERLORD_USER_DIR");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            return ResolveDocumentsUserDirectory();
        }

        private static string? ResolveDocumentsUserDirectory()
        {
            string? documents = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            if (string.IsNullOrWhiteSpace(documents))
            {
                object? helper = TaleWorlds.Library.Common.PlatformFileHelper;
                documents = (string?)helper?.GetType()
                    .GetProperty("DocumentsPath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(helper);
            }

            return string.IsNullOrWhiteSpace(documents)
                ? null
                : Path.Combine(documents, TaleWorlds.Engine.Utilities.GetApplicationName(), "CoopData", "DedicatedServer");
        }

        private static string? FindLegacyDirectory()
        {
            string? current = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            for (int depth = 0; depth < 10 && !string.IsNullOrEmpty(current); depth++)
            {
                string serverData = Path.Combine(current, "server-data");
                if (Directory.Exists(serverData))
                {
                    return Path.Combine(serverData, "improved-garrisons");
                }

                current = Path.GetDirectoryName(current);
            }

            return null;
        }

        private static void MigrateLegacyData(string legacyDirectory, string persistentDirectory)
        {
            if (!Directory.Exists(legacyDirectory))
            {
                return;
            }

            int migrated = 0;
            foreach (string fileName in PersistedFileNames)
            {
                string source = Path.Combine(legacyDirectory, fileName);
                string destination = Path.Combine(persistentDirectory, fileName);
                if (!File.Exists(source) || File.Exists(destination))
                {
                    continue;
                }

                try
                {
                    File.Copy(source, destination, false);
                    migrated++;
                }
                catch (IOException exception)
                {
                    Runtime.IntegrationLog.Error("legacy persistence migration failed for " + fileName + ": " + exception.Message);
                }
                catch (UnauthorizedAccessException exception)
                {
                    Runtime.IntegrationLog.Error("legacy persistence migration denied for " + fileName + ": " + exception.Message);
                }
            }

            if (migrated > 0)
            {
                Runtime.IntegrationLog.Information("migrated " + migrated + " legacy persistence file(s) to " + persistentDirectory);
            }
        }

        private static bool SameDirectory(string left, string right)
        {
            char[] separators = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
            string normalizedLeft = Path.GetFullPath(left).TrimEnd(separators);
            string normalizedRight = Path.GetFullPath(right).TrimEnd(separators);
            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }
    }
}
