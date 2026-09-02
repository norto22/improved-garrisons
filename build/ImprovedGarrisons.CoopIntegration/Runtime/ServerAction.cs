namespace ImprovedGarrisons.CoopIntegration.Runtime
{
    internal sealed class ServerAction
    {
        public string OperationId { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string SettlementId { get; set; } = string.Empty;

        public int SettingOperation { get; set; }

        public string StringArgument { get; set; } = string.Empty;

        public int IntegerArgument { get; set; }

        public float FloatArgument { get; set; }

        public bool BooleanArgument { get; set; }

        public int ArgumentKind { get; set; }

        public string ListArgument { get; set; } = string.Empty;
    }

    internal sealed class ActionOutcome
    {
        public bool Success { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public string Data { get; set; } = string.Empty;
    }
}
