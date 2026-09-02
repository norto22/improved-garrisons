namespace ImprovedGarrisons.CoopIntegration.Core
{
    public static class IntegrationRoleRouter
    {
        public static bool ShouldExecuteLocally(bool coopActive, bool isServer)
        {
            return !coopActive || isServer;
        }
    }
}
