using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace ImprovedGarrisons.CoopIntegration.Persistence
{
    internal static class ServerClanRegistry
    {
        private static readonly HashSet<string> ClanIds = new HashSet<string>(StringComparer.Ordinal);

        public static void Record(Clan? clan)
        {
            string? id = clan?.StringId;
            if (id != null && !string.IsNullOrWhiteSpace(id))
            {
                ClanIds.Add(id);
            }
        }

        public static bool Contains(Clan? clan)
        {
            string? id = clan?.StringId;
            return id != null && !string.IsNullOrWhiteSpace(id) && ClanIds.Contains(id);
        }
    }
}
