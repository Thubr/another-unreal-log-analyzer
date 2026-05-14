namespace UeLogKit.Core.Tests.Contract;

public sealed class ParserContractTests
{
    [Fact]
    public async Task SyntheticFixture_roundtrips_through_contract_harness()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "synthetic_basic_contract.json");
        var fixture = ParserContractFixture.LoadFromFile(fixturePath);
        var source = new StubLogEventSource(fixture.ExpectedEvents);

        await ParserContractTestHarness.RunFixtureAsync(source, fixturePath);
    }
}
