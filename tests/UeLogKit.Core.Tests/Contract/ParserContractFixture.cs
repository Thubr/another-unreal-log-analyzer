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

        return fixture
            ?? throw new InvalidOperationException($"Failed to deserialize fixture at '{fixturePath}'.");
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
