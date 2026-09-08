using System;
using System.Collections.Generic;
using GameInterface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;

namespace ImprovedGarrisons.CoopIntegration.Persistence
{
    internal static class ServerClanRegistry
    {
        private static readonly HashSet<string> ClanIds = new HashSet<string>(StringComparer.Ordinal);

        internal static void RestorePlayers()
        {
            if (!ContainerProvider.TryResolve(out IPlayerManager players)
                || !ContainerProvider.TryResolve(out IObjectManager objects)
                || players.Players == null)
            {
                return;
            }

            foreach (Player player in players.Players)
            {
                if (!string.IsNullOrWhiteSpace(player.ClanId)
                    && objects.TryGetObject(player.ClanId, out Clan clan))
                {
                    Record(clan);
                }
            }
        }

        public static void Record(Clan? clan) => Record(clan?.StringId);

        public static void Record(string? clanId)
        {
            if (!string.IsNullOrWhiteSpace(clanId))
            {
                ClanIds.Add(clanId!);
            }
        }

        public static bool Contains(Clan? clan) => Contains(clan?.StringId);

        public static bool Contains(string? clanId)
        {
            return !string.IsNullOrWhiteSpace(clanId) && ClanIds.Contains(clanId!);
        }

        // On a dedicated server, MobileParty.MainParty.ActualClan is always null (Coop removes the
        // server's main party at boot), so ImprovedSettlement.CheckIfNPCGarrison's vanilla single-player
        // comparison against it is meaningless there. A settlement is treated as player-owned instead
        // when its owner clan is one Improved Garrisons has already recorded as a connected player's clan.
        public static bool IsNpcGarrison(string? ownerClanId)
        {
            return string.IsNullOrWhiteSpace(ownerClanId) || !Contains(ownerClanId);
        }
    }
}
