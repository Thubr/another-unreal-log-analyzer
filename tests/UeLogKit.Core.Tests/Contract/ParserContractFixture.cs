using System.Text.Json;

namespace UeLogKit.Core.Tests.Contract;

public sealed record ParserContractFixture(
    string Name,
    LogInput Input,
    ParserOptions Options,
    IReadOnlyList<LogEvent> ExpectedEvents
)
{
    public static ParserContractFixture LoadFromFile(string fixturePath)
    {
        using var stream = File.OpenRead(fixturePath);
        var fixture = JsonSerializer.Deserialize<ParserContractFixture>(stream, SerializerOptions);

        fixture = fixture
            ?? throw new InvalidOperationException($"Failed to deserialize fixture at '{fixturePath}'.");

        if (!Path.IsPathRooted(fixture.Input.SourcePath))
        {
            var fixtureDirectory = Path.GetDirectoryName(fixturePath)
                ?? throw new InvalidOperationException($"Failed to resolve fixture directory for '{fixturePath}'.");
            var resolvedInput = fixture.Input with { SourcePath = Path.Combine(fixtureDirectory, fixture.Input.SourcePath) };
            var resolvedExpected = fixture.ExpectedEvents
                .Select(e => e with { SourcePath = resolvedInput.SourcePath })
                .ToArray();

            fixture = fixture with
            {
                Input = resolvedInput,
                ExpectedEvents = resolvedExpected
            };
        }

        return fixture;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
