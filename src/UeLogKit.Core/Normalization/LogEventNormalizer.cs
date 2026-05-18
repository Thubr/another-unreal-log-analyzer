using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UeLogKit.Core.Pipeline;

namespace UeLogKit.Core.Normalization;

public sealed partial class LogEventNormalizer : ILogEventPipelineStage
{
    public async IAsyncEnumerable<LogEvent> ProcessAsync(
        IAsyncEnumerable<LogEvent> input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var logEvent in input.WithCancellation(cancellationToken))
        {
            yield return Normalize(logEvent);
        }
    }

    public LogEvent Normalize(LogEvent logEvent)
    {
        return logEvent with
        {
            Message = NormalizeText(logEvent.Message),
            ContinuationLines = logEvent.ContinuationLines.Select(NormalizeText).ToArray(),
            Fields = logEvent.Fields.ToDictionary(kv => kv.Key, kv => NormalizeField(kv.Key, kv.Value))
        };
    }

    private static string NormalizeField(string key, string value)
    {
        return key.ToLowerInvariant() switch
        {
            "session" or "sessionid" => "<session_id>",
            "ticket" or "ticketid" => "<ticket_id>",
            "user" or "userid" => "<user_id>",
            _ => NormalizeText(value)
        };
    }

    private static string NormalizeText(string text)
    {
        var normalized = SessionPattern().Replace(text, "${prefix}<session_id>${suffix}");
        normalized = TicketPattern().Replace(normalized, "${prefix}<ticket_id>${suffix}");
        normalized = UserPattern().Replace(normalized, "${prefix}<user_id>${suffix}");
        return normalized;
    }

    [GeneratedRegex(@"(?<prefix>\b[Ss]ession(?:Id)?""?\s*(?:=|:)\s*""?)[A-Za-z0-9_-]+(?<suffix>""?)", RegexOptions.CultureInvariant)]
    private static partial Regex SessionPattern();

    [GeneratedRegex(@"(?<prefix>\b[Tt]icket(?:Id)?""?\s*(?:=|:)\s*""?)[A-Za-z0-9_-]+(?<suffix>""?)", RegexOptions.CultureInvariant)]
    private static partial Regex TicketPattern();

    [GeneratedRegex(@"(?<prefix>\b[Uu]ser(?:Id)?""?\s*(?:=|:)\s*""?)[A-Za-z0-9_-]+(?<suffix>""?)", RegexOptions.CultureInvariant)]
    private static partial Regex UserPattern();
}
