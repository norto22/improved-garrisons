using Common.Messaging;
using ProtoBuf;

namespace ImprovedGarrisons.CoopIntegration.Protocol
{
    public interface IServerIntent : IMessage
    {
        string OperationId { get; set; }

        string SettlementId { get; set; }
    }

    public enum PartyIntentKind
    {
        SyncTown = 1,
        CreateGuards = 2,
        CreateRecruiter = 3,
        OrderPatrol = 4,
        OrderReturn = 5,
        ReturnRecruiter = 6,
        SetRecruiterCulture = 7,
        Escort = 8,
        EscortPlayer = 9,
        Fortify = 10
    }

    public enum SettingsIntentKind
    {
        SetReturnPercentage = 1,
        SetAutoGarrisonThreshold = 2,
        SetAutoGarrisonSize = 3,
        TogglePrisonerSell = 4,
        ToggleAutoGuards = 5,
        ToggleAutoGuardDefend = 6,
        TogglePrisonerRecruit = 7,
        ToggleUpgrade = 8,
        ToggleReplenish = 9,
        ToggleDestroyHideout = 10,
        ToggleHorseBuy = 11,
        SetRecruiterAmountToRecruit = 12,
        SetRecruitmentThreshold = 13,
        ToggleRecruitOnlyElite = 14,
        TogglePrisonerRecruitmentAboveThreshold = 15,
        TogglePrisonerRecruitment = 16,
        ToggleVanillaRecruitment = 17,
        ToggleRegionRecruitment = 18,
        ToggleRecruiterOnlyElites = 19,
        ToggleRecruiterBuyHorses = 20,
        TogglePrisonerRecruitmentIgnoresTemplate = 21,
        ToggleRecruiterAutoSpawn = 22,
        SetTownMaxUpgradeTier = 23,
        ToggleVanillaTraining = 24,
        ToggleTraining = 25,
        ToggleAutoSpawn = 26,
        ToggleFollowTemplate = 27,
        ToggleRemoveNonTemplateTroops = 28,
        RemoveUpgradeTarget = 29,
        SetTemplateFull = 30,
        SetUpgradePath = 31,
        AdjustTemplateCount = 32
    }

    public enum ManagementIntentKind
    {
        TransferDirect = 2,
        CopyAll = 5,
        CopySpecific = 6
    }

    [ProtoContract]
    public sealed class PartyIntent : IServerIntent
    {
        [ProtoMember(1)]
        public string OperationId { get; set; } = string.Empty;

        [ProtoMember(2)]
        public PartyIntentKind Operation { get; set; }

        [ProtoMember(3)]
        public string SettlementId { get; set; } = string.Empty;

        [ProtoMember(4)]
        public string StringArgument { get; set; } = string.Empty;

        [ProtoMember(5)]
        public int IntegerArgument { get; set; }

        [ProtoMember(6)]
        public bool BooleanArgument { get; set; }
    }

    [ProtoContract]
    public sealed class SettingsIntent : IServerIntent
    {
        [ProtoMember(1)]
        public string OperationId { get; set; } = string.Empty;

        [ProtoMember(2)]
        public SettingsIntentKind Operation { get; set; }

        [ProtoMember(3)]
        public string SettlementId { get; set; } = string.Empty;

        [ProtoMember(4)]
        public string StringArgument { get; set; } = string.Empty;

        [ProtoMember(5)]
        public int IntegerArgument { get; set; }

        [ProtoMember(6)]
        public float FloatArgument { get; set; }

        [ProtoMember(7)]
        public bool BooleanArgument { get; set; }

        [ProtoMember(8)]
        public int ArgumentKind { get; set; }

        [ProtoMember(9)]
        public string ListArgument { get; set; } = string.Empty;
    }

    [ProtoContract]
    public sealed class ManagementIntent : IServerIntent
    {
        [ProtoMember(1)]
        public string OperationId { get; set; } = string.Empty;

        [ProtoMember(2)]
        public ManagementIntentKind Operation { get; set; }

        [ProtoMember(3)]
        public string SettlementId { get; set; } = string.Empty;

        [ProtoMember(4)]
        public string StringArgument { get; set; } = string.Empty;

        [ProtoMember(5)]
        public int IntegerArgument { get; set; }

        [ProtoMember(6)]
        public bool BooleanArgument { get; set; }

        [ProtoMember(7)]
        public string ListArgument { get; set; } = string.Empty;
    }

    [ProtoContract]
    public sealed class ConfigRequest : IMessage
    {
        [ProtoMember(1)]
        public string RequestId { get; set; } = string.Empty;
    }

    [ProtoContract]
    public sealed class ConfigSync : IMessage
    {
        [ProtoMember(1)]
        public string ConfigXml { get; set; } = string.Empty;

        [ProtoMember(2)]
        public long Revision { get; set; }
    }

    [ProtoContract]
    public sealed class StateSync : IMessage
    {
        [ProtoMember(1)]
        public string SettingsText { get; set; } = string.Empty;

        [ProtoMember(2)]
        public string ActivityText { get; set; } = string.Empty;

        [ProtoMember(3)]
        public long Revision { get; set; }
    }

    [ProtoContract]
    public sealed class PartyManifest : IMessage
    {
        [ProtoMember(1)]
        public string SerializedEntries { get; set; } = string.Empty;

        [ProtoMember(2)]
        public long Revision { get; set; }
    }

    [ProtoContract]
    public sealed class ServerHealth : IMessage
    {
        [ProtoMember(1)]
        public bool Ready { get; set; }

        [ProtoMember(2)]
        public string Detail { get; set; } = string.Empty;

        [ProtoMember(3)]
        public long ServerTick { get; set; }
    }
}
