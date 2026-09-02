using System;

namespace ImprovedGarrisons.CoopIntegration.Core
{
    public static class ActionAuthorization
    {
        public static bool CanMutateSettlement(string peerClanId, string ownerClanId)
        {
            return !string.IsNullOrWhiteSpace(peerClanId)
                && !string.IsNullOrWhiteSpace(ownerClanId)
                && string.Equals(peerClanId, ownerClanId, StringComparison.Ordinal);
        }
    }
}
