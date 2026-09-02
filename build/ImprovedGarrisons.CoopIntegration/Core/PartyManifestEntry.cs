using System;

namespace ImprovedGarrisons.CoopIntegration.Core
{
    public sealed class PartyManifestEntry : IEquatable<PartyManifestEntry>
    {
        public PartyManifestEntry(string kind, string partyId, string homeSettlementId, string detail, string statusText)
        {
            Kind = kind ?? string.Empty;
            PartyId = partyId ?? string.Empty;
            HomeSettlementId = homeSettlementId ?? string.Empty;
            Detail = detail ?? string.Empty;
            StatusText = statusText ?? string.Empty;
        }

        public string Kind { get; }

        public string PartyId { get; }

        public string HomeSettlementId { get; }

        public string Detail { get; }

        public string StatusText { get; }

        public bool Equals(PartyManifestEntry? other)
        {
            return other != null
                && string.Equals(Kind, other.Kind, StringComparison.Ordinal)
                && string.Equals(PartyId, other.PartyId, StringComparison.Ordinal)
                && string.Equals(HomeSettlementId, other.HomeSettlementId, StringComparison.Ordinal)
                && string.Equals(Detail, other.Detail, StringComparison.Ordinal)
                && string.Equals(StatusText, other.StatusText, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as PartyManifestEntry);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Kind);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(PartyId);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(HomeSettlementId);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Detail);
                return (hash * 31) + StringComparer.Ordinal.GetHashCode(StatusText);
            }
        }
    }
}
