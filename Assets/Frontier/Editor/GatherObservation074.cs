namespace Frontier.Editor
{
    // Reuses the same predeclared ordinary openings and timing windows. The
    // rejection count is sampled after a settled step; it includes unavailable
    // targets and ordinary-route failures and is not a per-frame safety proof.
    public static class GatherObservation074
    {
        public static void RunNormal()=>MarketObservation073.RunDeclaredThreeCases("normal-gather074-v1");
    }
}
