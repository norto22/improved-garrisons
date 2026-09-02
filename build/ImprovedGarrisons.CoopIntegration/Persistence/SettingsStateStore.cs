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

namespace ImprovedGarrisons.CoopIntegration.Persistence
{
    internal static class SettingsStateStore
    {
        private static readonly string SettingsPath = IntegrationDataPaths.FilePath("settlement-settings.txt");
        private static readonly PropertyInfo[] PrimitiveProperties = BuildPrimitiveProperties();
        private static readonly Dictionary<string, PropertyInfo> PropertiesByName = BuildPropertyMap();
        private static bool _restored;
        private static bool _force = true;
        private static string _lastState = string.Empty;
        private static string _lastConfig = string.Empty;
        private static long _remoteRevision;

        public static long Revision { get; private set; }

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

        public static string BuildSettingsText()
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
                if (troops == null || troops.Count == 0)
                {
                    continue;
                }

                builder.Append("Template=");
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
                ApplySettingsText(settingsText ?? string.Empty);
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

        internal static void PollServer()
        {
            if (!IntegrationRuntime.IsServer || global::ImprovedGarrisons.Main.GarrisonBehavior == null)
            {
                return;
            }

            if (!_restored)
            {
                _restored = true;
                RestoreSettings();
            }

            string settings = BuildSettingsText();
            string activity = BuildActivityText();
            string config = ReadConfigXml();
            string stateKey = settings + "\u0001" + activity;
            if (!_force && stateKey == _lastState && config == _lastConfig)
            {
                return;
            }

            _force = false;
            _lastState = stateKey;
            _lastConfig = config;
            Revision++;
            try
            {
                IntegrationDataPaths.WriteAtomic(SettingsPath, settings);
            }
            catch (IOException exception)
            {
                IntegrationLog.Error("settings persist failed: " + exception.Message);
            }

            IntegrationTransport.BroadcastState(settings, activity, Revision);
        }

        private static void RestoreSettings()
        {
            if (string.IsNullOrEmpty(SettingsPath) || !File.Exists(SettingsPath))
            {
                return;
            }

            try
            {
                ApplySettingsText(File.ReadAllText(SettingsPath));
                IntegrationLog.Information("restored persisted settlement settings");
            }
            catch (IOException exception)
            {
                IntegrationLog.Error("settings restore failed: " + exception.Message);
            }
            catch (FormatException exception)
            {
                IntegrationLog.Error("settings restore rejected: " + exception.Message);
            }
        }

        private static void ApplySettingsText(string text)
        {
            Dictionary<string, GarrisonSettings>? allSettings = global::ImprovedGarrisons.Main.GarrisonBehavior?.SettlementSettingsData;
            if (allSettings == null || string.IsNullOrEmpty(text))
            {
                return;
            }

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
                    if (!allSettings.TryGetValue(key, out current) || current == null || current is NPCGarrisonSettings)
                    {
                        current = new GarrisonSettings();
                        allSettings[key] = current;
                    }

                    continue;
                }

                if (current == null)
                {
                    continue;
                }

                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                string name = line.Substring(0, separator);
                string value = line.Substring(separator + 1);
                if (name == "TroopsToUpgradeTo")
                {
                    string[] fields = value.Split(',');
                    bool[] paths = new bool[fields.Length];
                    for (int index = 0; index < fields.Length; index++)
                    {
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
        }

        private static void ApplyTemplate(GarrisonSettings settings, string value)
        {
            Dictionary<string, int> troops = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string entry in value.Split(';'))
            {
                int separator = entry.LastIndexOf(':');
                if (separator <= 0 || !int.TryParse(entry.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
                {
                    continue;
                }

                string id = Decode(entry.Substring(0, separator));
                if (!string.IsNullOrWhiteSpace(id) && count > 0)
                {
                    troops[id] = Math.Min(count, 10_000);
                }
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
            catch (FormatException)
            {
                // A malformed property is ignored without discarding the rest of the state snapshot.
            }
            catch (OverflowException)
            {
                // A malformed property is ignored without discarding the rest of the state snapshot.
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
