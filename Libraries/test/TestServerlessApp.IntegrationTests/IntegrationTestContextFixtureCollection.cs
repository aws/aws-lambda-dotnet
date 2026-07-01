// Registers the AssemblyFixture test framework so test classes can share a single
// IntegrationTestContextFixture (one deployed stack) via IAssemblyFixture<T> while still
// running in parallel. Without this attribute IAssemblyFixture<T> is silently ignored.
[assembly: Xunit.TestFramework("Xunit.Extensions.AssemblyFixture.AssemblyFixtureFramework", "Xunit.Extensions.AssemblyFixture")]
