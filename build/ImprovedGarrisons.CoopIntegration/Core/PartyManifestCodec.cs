using System;
using System.Collections.Generic;
using System.Text;

namespace ImprovedGarrisons.CoopIntegration.Core
{
    public static class PartyManifestCodec
    {
        private const int MaximumEntries = 4096;
        private const int MaximumSerializedLength = 4 * 1024 * 1024;
        private static readonly char[] NewLineSeparator = { '\n' };

        public static string Serialize(IEnumerable<PartyManifestEntry> entries)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(entries);
#else
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }
#endif

            StringBuilder result = new StringBuilder();
            int count = 0;
            foreach (PartyManifestEntry entry in entries)
            {
                if (entry == null)
                {
                    throw new ArgumentException("Manifest entries cannot be null.", nameof(entries));
                }

                if (++count > MaximumEntries)
                {
                    throw new ArgumentException("The party manifest is too large.", nameof(entries));
                }

                if (result.Length > 0)
                {
                    result.Append('\n');
                }

                result.Append(Encode(entry.Kind)).Append('|')
                    .Append(Encode(entry.PartyId)).Append('|')
                    .Append(Encode(entry.HomeSettlementId)).Append('|')
                    .Append(Encode(entry.Detail)).Append('|')
                    .Append(Encode(entry.StatusText));
            }

            if (result.Length > MaximumSerializedLength)
            {
                throw new ArgumentException("The party manifest is too large.", nameof(entries));
            }

            return result.ToString();
        }

        public static IReadOnlyList<PartyManifestEntry> Parse(string serialized)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(serialized);
#else
            if (serialized == null)
            {
                throw new ArgumentNullException(nameof(serialized));
            }
#endif

            if (serialized.Length > MaximumSerializedLength)
            {
                throw new FormatException("The party manifest is too large.");
            }

            List<PartyManifestEntry> entries = new List<PartyManifestEntry>();
            if (serialized.Length == 0)
            {
                return entries;
            }

            string[] lines = serialized.Split(NewLineSeparator, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > MaximumEntries)
            {
                throw new FormatException("The party manifest contains too many entries.");
            }

            foreach (string line in lines)
            {
                string[] fields = line.TrimEnd('\r').Split('|');
                if (fields.Length != 4 && fields.Length != 5)
                {
                    throw new FormatException("A party manifest entry is malformed.");
                }

                entries.Add(new PartyManifestEntry(
                    Decode(fields[0]),
                    Decode(fields[1]),
                    Decode(fields[2]),
                    Decode(fields[3]),
                    fields.Length == 5 ? Decode(fields[4]) : string.Empty));
            }

            return entries;
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        private static string Decode(string value)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch (FormatException exception)
            {
                throw new FormatException("A party manifest field is malformed.", exception);
            }
        }
    }
}
