using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using System.Xml;
using ImprovedGarrisons.ActivityLogging;
using ImprovedGarrisons.CoopIntegration.Runtime;
using ImprovedGarrisons.SaveSystem.SaveData.DataTypes;
using TaleWorlds.CampaignSystem.Settlements;

namespace ImprovedGarrisons.CoopIntegration.Persistence
{
    internal static class SettingsStateStore
    {
        private static string SettingsPath => IntegrationDataPaths.FilePath("settlement-settings.txt");
        private static readonly PropertyInfo[] PrimitiveProperties = BuildPrimitiveProperties();
        private static readonly Dictionary<string, PropertyInfo> PropertiesByName = BuildPropertyMap();
        private static Dictionary<string, GarrisonSettings>? _restoredSettings;
        private static bool _force = true;
        private static string _lastState = string.Empty;
        private static string _lastConfig = string.Empty;
        private static string? _lastPersistedSettings;
        private static long _remoteRevision;

        public static long Revision { get; private set; }

        // Called from IntegrationTransport.Teardown() on every disconnect (including a reconnect to a
        // server that just restarted). Without this, _remoteRevision keeps its pre-restart value, and
        // the freshly-restarted server's own low revision numbers are permanently rejected by the
        // ApplyConfigXml/ApplyState guards below.
        internal static void ResetClientState()
        {
            _remoteRevision = 0;
        }

        public static string ReadConfigXml()
        {
            try
            {
                object? manager = GetConfigManager();
                object? config = manager?.GetType().GetProperty("Config", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(manager, null);
                if (config == null)
                {
                    return string.Empty;
                }

                StringBuilder builder = new StringBuilder();
                using (StringWriter writer = new StringWriter(builder, CultureInfo.InvariantCulture))
                {
                    new XmlSerializer(config.GetType()).Serialize(writer, config);
                }

                return builder.ToString();
            }
            catch (InvalidOperationException exception)
            {
                IntegrationLog.Error("config serialization failed: " + exception.GetBaseException().Message);
                return string.Empty;
            }
        }

        public static string BuildSettingsText(string? clanIdFilter = null)
        {
            Dictionary<string, GarrisonSettings>? settings = global::ImprovedGarrisons.Main.GarrisonBehavior?.SettlementSettingsData;
            if (settings == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            List<string> keys = new List<string>(settings.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                GarrisonSettings value = settings[key];
                if (value == null || value is NPCGarrisonSettings)
                {
                    continue;
                }

                if (clanIdFilter != null && !string.Equals(ResolveOwnerClanId(key), clanIdFilter, StringComparison.Ordinal))
                {
                    continue;
                }

                builder.Append('[').Append(Encode(key)).AppendLine("]");
                foreach (PropertyInfo property in PrimitiveProperties)
                {
                    object propertyValue = property.GetValue(value, null);
                    builder.Append(property.Name).Append('=').AppendLine(Convert.ToString(propertyValue, CultureInfo.InvariantCulture));
                }

                if (value.TroopsToUpgradeTo != null)
                {
                    builder.Append("TroopsToUpgradeTo=");
                    for (int index = 0; index < value.TroopsToUpgradeTo.Length; index++)
                    {
                        if (index > 0)
                        {
                            builder.Append(',');
                        }

                        builder.Append(value.TroopsToUpgradeTo[index] ? '1' : '0');
                    }

                    builder.AppendLine();
                }

                Dictionary<string, int>? troops = value.Template?.GetTroopList();
                builder.Append("Template=");
                if (troops == null || troops.Count == 0)
                {
                    builder.AppendLine();
                    continue;
                }

                bool first = true;
                foreach (KeyValuePair<string, int> troop in troops)
                {
                    if (!first)
                    {
                        builder.Append(';');
                    }

                    first = false;
                    builder.Append(Encode(troop.Key)).Append(':').Append(troop.Value.ToString(CultureInfo.InvariantCulture));
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        // Mirrors the settlement-name lookup GarrisonBehavior.GetAllPlayerSettlements() already uses for the
        // single-player invariant, so a clan-scoped sync only ever reports what that invariant would allow.
        private static string? ResolveOwnerClanId(string settlementKey)
        {
            foreach (Settlement settlement in Settlement.All)
            {
                if (settlement?.Town != null && (settlement.Town.IsCastle || settlement.Town.IsTown)
                    && string.Equals(settlement.Name.ToString(), settlementKey, StringComparison.Ordinal))
                {
                    return settlement.OwnerClan?.StringId;
                }
            }

            return null;
        }

        public static string BuildActivityText()
        {
            Dictionary<string, ActivityLog>? logs = global::ImprovedGarrisons.Main.ActivityLogManager?.ActivityLogs;
            if (logs == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            List<string> keys = new List<string>(logs.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                ActivityLog log = logs[key];
                if (log == null)
                {
                    continue;
                }

                builder.Append(Encode(key)).Append('|')
                    .Append(log.DailyRecruits).Append('|')
                    .Append(log.DailyUpgrades).Append('|')
                    .Append(log.DailyPrisonerTurnover).Append('|')
                    .Append(log.WeeklyRecruits).Append('|')
                    .Append(log.WeeklyUpgrades).Append('|')
                    .Append(log.WeeklyPrisonerTurnover).Append('|')
                    .Append(log.WeeklyRecruitmentCosts.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(log.WeeklyTrainingCosts.ToString(CultureInfo.InvariantCulture)).AppendLine();
            }

            return builder.ToString();
        }

        public static void ApplyConfigXml(string xml, long revision)
        {
            if (IntegrationRuntime.IsServer || revision < _remoteRevision || string.IsNullOrWhiteSpace(xml))
            {
                return;
            }

            try
            {
                object? manager = GetConfigManager();
                PropertyInfo? property = manager?.GetType().GetProperty("Config", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Type? configType = property?.PropertyType;
                if (manager == null || property == null || configType == null)
                {
                    return;
                }

                XmlReaderSettings readerSettings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                };
                using (StringReader textReader = new StringReader(xml))
                using (XmlReader reader = XmlReader.Create(textReader, readerSettings))
                {
                    object? config = new XmlSerializer(configType).Deserialize(reader);
                    if (config != null)
                    {
                        property.SetValue(manager, config, null);
                    }
                }

                _remoteRevision = revision;
                Revision = revision;
                IntegrationLog.Information("server configuration applied at revision " + revision);
            }
            catch (InvalidOperationException exception)
            {
                IntegrationLog.Error("server configuration rejected: " + exception.GetBaseException().Message);
            }
        }

        public static void ApplyState(string settingsText, string activityText, long revision)
        {
            if (IntegrationRuntime.IsServer || revision < _remoteRevision)
            {
                return;
            }

            try
            {
                // A network-delivered sync is now clan-scoped and authoritative for this peer's own clan
                // (see BuildSettingsText's clanIdFilter), so anything it no longer names -- most commonly a
                // stale foreign-clan entry from before this fix, or before this peer's clan was registered --
                // must be dropped here rather than accumulating forever.
                ApplySettingsText(settingsText ?? string.Empty, pruneAbsentKeys: true);
                ApplyActivityText(activityText ?? string.Empty);
                _remoteRevision = revision;
                Revision = revision;
                IntegrationLog.Information("server settings/activity applied at revision " + revision);

                // Client prefixes skip the local mutation methods that normally mark individual tabs dirty.
                // Refresh the existing active datasource after authoritative state has been adopted. A structural
                // full refresh replaces bound slider widgets and can disconnect the widget that owns a live drag.
                // ApplySettingsText can rewrite a settlement's training template (ApplyTemplate), which the
                // Training tab only picks up when its own dirty flag is set, so mark it explicitly here.
                global::ImprovedGarrisons.ImprovedGarrisonsUI.UIManager.Instance.MarkTrainingTroopsDirty();
                global::ImprovedGarrisons.ImprovedGarrisonsUI.UIManager.Instance.RefreshCurrentUiTab();
            }
            catch (FormatException exception)
            {
                IntegrationLog.Error("server state rejected: " + exception.Message);
            }
        }

        public static void MarkDirty()
        {
            _force = true;
            // Every settings-changing server action calls this. Without also waking the outer poll,
            // the write+broadcast still waits for PartyManifestStore's own 10s schedule (ServerPollMilliseconds)
            // -- a player action (saving a template, dragging a slider) would sit invisible on the client for
            // up to 10s, easily read as "my change didn't work". MarkDirty is only ever called server-side
            // (all call sites are in ServerActionDispatcher), so this is always safe to call from here.
            PartyManifestStore.RequestImmediatePoll();
        }

        internal static bool EnsureServerRestored()
        {
            if (!IntegrationRuntime.ServerCampaignReady || global::ImprovedGarrisons.Main.GarrisonBehavior == null)
            {
                return false;
            }

            Dictionary<string, GarrisonSettings> settings = global::ImprovedGarrisons.Main.GarrisonBehavior.SettlementSettingsData;
            if (!ReferenceEquals(_restoredSettings, settings))
            {
                // A session load can replace IGSaveData after the transport first connects.
                // Restore only after Coop finishes loading, and repeat for a replacement save object.
                if (!RestoreSettings())
                {
                    return false;
                }

                ServerClanRegistry.RestorePlayers();
                _restoredSettings = settings;
                _force = true;
            }

            return true;
        }

        internal static void PollServer()
        {
            if (!EnsureServerRestored())
            {
                return;
            }

            string settings = BuildSettingsText();
            string activity = BuildActivityText();
            string config = ReadConfigXml();
            string stateKey = settings + "\u0001" + activity;
            if (!_force && stateKey == _lastState && config == _lastConfig)
            {
                return;
            }

            try
            {
                // Activity counters also change the network revision. They must not continually
                // replace the settings backup with another copy of the same settings.
                if (settings != _lastPersistedSettings)
                {
                    if (string.IsNullOrEmpty(SettingsPath))
                    {
                        IntegrationLog.Error("settings persist failed: no writable data directory");
                        return;
                    }

                    IntegrationDataPaths.WriteAtomic(SettingsPath, settings);
                    _lastPersistedSettings = settings;
                }
            }
            catch (IOException exception)
            {
                IntegrationLog.Error("settings persist failed: " + exception.Message);
                return;
            }
            catch (UnauthorizedAccessException exception)
            {
                IntegrationLog.Error("settings persist denied: " + exception.Message);
                return;
            }

            _force = false;
            _lastState = stateKey;
            _lastConfig = config;
            Revision++;
            IntegrationTransport.BroadcastState(activity, Revision);
        }

        private static bool RestoreSettings()
        {
            if (string.IsNullOrEmpty(SettingsPath))
            {
                return false;
            }

            if (!File.Exists(SettingsPath))
            {
                _lastPersistedSettings = null;
                return true;
            }

            try
            {
                string persisted = File.ReadAllText(SettingsPath);
                ApplySettingsText(persisted);
                _lastPersistedSettings = persisted;
                IntegrationLog.Information("restored persisted settlement settings");
                return true;
            }
            catch (IOException exception)
            {
                IntegrationLog.Error("settings restore failed: " + exception.Message);
            }
            catch (FormatException exception)
            {
                IntegrationLog.Error("settings restore rejected: " + exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                IntegrationLog.Error("settings restore denied: " + exception.Message);
            }

            return false;
        }

        private static void ApplySettingsText(string text, bool pruneAbsentKeys = false)
        {
            Dictionary<string, GarrisonSettings>? allSettings = global::ImprovedGarrisons.Main.GarrisonBehavior?.SettlementSettingsData;
            if (allSettings == null)
            {
                return;
            }

            // Parse the complete snapshot before touching live settings. A malformed save must
            // neither partially reset the campaign nor be persisted over its recoverable source.
            Dictionary<string, GarrisonSettings> incoming = new Dictionary<string, GarrisonSettings>(StringComparer.Ordinal);
            GarrisonSettings? current = null;
            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                if (line.Length == 0)
                {
                    continue;
                }

                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    string key = Decode(line.Substring(1, line.Length - 2));
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        throw new FormatException("A settlement identifier is empty.");
                    }
                    current = new GarrisonSettings();
                    incoming[key] = current;
                    continue;
                }

                if (current == null)
                {
                    throw new FormatException("A setting has no settlement header.");
                }

                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    throw new FormatException("A setting record is incomplete.");
                }

                string name = line.Substring(0, separator);
                string value = line.Substring(separator + 1);
                if (name == "TroopsToUpgradeTo")
                {
                    string[] fields = value.Length == 0 ? Array.Empty<string>() : value.Split(',');
                    bool[] paths = new bool[fields.Length];
                    for (int index = 0; index < fields.Length; index++)
                    {
                        if (fields[index] != "0" && fields[index] != "1")
                        {
                            throw new FormatException("An upgrade path is invalid.");
                        }
                        paths[index] = fields[index] == "1";
                    }

                    current.TroopsToUpgradeTo = paths;
                }
                else if (name == "Template")
                {
                    ApplyTemplate(current, value);
                }
                else if (PropertiesByName.TryGetValue(name, out PropertyInfo property))
                {
                    SetPrimitive(property, current, value);
                }
            }

            foreach (KeyValuePair<string, GarrisonSettings> entry in incoming)
            {
                if (allSettings.TryGetValue(entry.Key, out GarrisonSettings existing)
                    && existing != null && !(existing is NPCGarrisonSettings))
                {
                    // Preserve the settings object referenced by the open UI.
                    foreach (PropertyInfo property in PrimitiveProperties)
                    {
                        property.SetValue(existing, property.GetValue(entry.Value, null), null);
                    }

                    existing.TroopsToUpgradeTo = entry.Value.TroopsToUpgradeTo;
                    if (existing.Template == null)
                    {
                        existing.Template = entry.Value.Template;
                    }
                    else
                    {
                        existing.Template.SetTroops(entry.Value.Template.GetTroopList());
                    }
                }
                else
                {
                    allSettings[entry.Key] = entry.Value;
                }
            }

            if (pruneAbsentKeys)
            {
                List<string> staleKeys = new List<string>();
                foreach (KeyValuePair<string, GarrisonSettings> entry in allSettings)
                {
                    if (entry.Value is NPCGarrisonSettings || incoming.ContainsKey(entry.Key))
                    {
                        continue;
                    }

                    staleKeys.Add(entry.Key);
                }

                foreach (string staleKey in staleKeys)
                {
                    allSettings.Remove(staleKey);
                }
            }
        }

        private static void ApplyTemplate(GarrisonSettings settings, string value)
        {
            Dictionary<string, int> troops = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string entry in value.Split(';'))
            {
                if (value.Length == 0)
                {
                    break;
                }

                int separator = entry.LastIndexOf(':');
                if (separator <= 0 || !int.TryParse(entry.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                    || count < 0)
                {
                    throw new FormatException("A template target is invalid.");
                }

                string id = Decode(entry.Substring(0, separator));
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new FormatException("A template troop identifier is empty.");
                }

                troops[id] = Math.Min(count, 10_000);
            }

            settings.Template?.SetTroops(troops);
        }

        private static void SetPrimitive(PropertyInfo property, GarrisonSettings settings, string value)
        {
            try
            {
                object parsed;
                if (property.PropertyType == typeof(bool))
                {
                    parsed = bool.Parse(value);
                }
                else if (property.PropertyType == typeof(int))
                {
                    parsed = int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
                }
                else if (property.PropertyType == typeof(float))
                {
                    parsed = float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
                }
                else
                {
                    parsed = value;
                }

                property.SetValue(settings, parsed, null);
            }
            catch (FormatException exception)
            {
                throw new FormatException("Invalid setting " + property.Name + ".", exception);
            }
            catch (OverflowException exception)
            {
                throw new FormatException("Invalid setting " + property.Name + ".", exception);
            }
        }

        private static void ApplyActivityText(string text)
        {
            Dictionary<string, ActivityLog>? logs = global::ImprovedGarrisons.Main.ActivityLogManager?.ActivityLogs;
            if (logs == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            foreach (string rawLine in text.Split('\n'))
            {
                string[] fields = rawLine.TrimEnd('\r').Split('|');
                if (fields.Length != 9)
                {
                    continue;
                }

                string key = Decode(fields[0]);
                if (!logs.TryGetValue(key, out ActivityLog log) || log == null)
                {
                    log = new ActivityLog();
                    logs[key] = log;
                }

                SetActivityProperty(log, "DailyRecruits", fields[1]);
                SetActivityProperty(log, "DailyUpgrades", fields[2]);
                SetActivityProperty(log, "DailyPrisonerTurnover", fields[3]);
                SetActivityProperty(log, "WeeklyRecruits", fields[4]);
                SetActivityProperty(log, "WeeklyUpgrades", fields[5]);
                SetActivityProperty(log, "WeeklyPrisonerTurnover", fields[6]);
                if (float.TryParse(fields[7], NumberStyles.Float, CultureInfo.InvariantCulture, out float recruitmentCost))
                {
                    log.WeeklyRecruitmentCosts = recruitmentCost;
                }

                if (float.TryParse(fields[8], NumberStyles.Float, CultureInfo.InvariantCulture, out float trainingCost))
                {
                    log.WeeklyTrainingCosts = trainingCost;
                }
            }
        }

        private static void SetActivityProperty(ActivityLog log, string name, string value)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return;
            }

            PropertyInfo? property = typeof(ActivityLog).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            property?.SetValue(log, parsed, null);
        }

        private static object? GetConfigManager()
        {
            Type? type = Type.GetType("ImprovedGarrisons.SaveSystem.Configuration.ConfigManager, ImprovedGarrisons", false);
            return type?.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null, null);
        }

        private static PropertyInfo[] BuildPrimitiveProperties()
        {
            List<PropertyInfo> properties = new List<PropertyInfo>();
            foreach (PropertyInfo property in typeof(GarrisonSettings).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                Type type = property.PropertyType;
                if (property.CanRead && property.CanWrite
                    && (type == typeof(bool) || type == typeof(int) || type == typeof(float) || type == typeof(string)))
                {
                    properties.Add(property);
                }
            }

            properties.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            return properties.ToArray();
        }

        private static Dictionary<string, PropertyInfo> BuildPropertyMap()
        {
            Dictionary<string, PropertyInfo> result = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
            foreach (PropertyInfo property in PrimitiveProperties)
            {
                result[property.Name] = property;
            }

            return result;
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string Decode(string value)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch (FormatException exception)
            {
                throw new FormatException("A synchronized state identifier is malformed.", exception);
            }
        }
    }
}
