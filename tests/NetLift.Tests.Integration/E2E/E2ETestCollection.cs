namespace NetLift.Tests.Integration.E2E;

/// <summary>
/// Collection definition for E2E tests.
/// This ensures E2E tests don't run in parallel to avoid resource contention.
/// </summary>
[CollectionDefinition("E2E", DisableParallelization = true)]
public class E2ETestCollection
{
    // This class is intentionally empty. It's used only to define the test collection.
}
