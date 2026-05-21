using UeLogKit.Core.Normalization;

namespace UeLogKit.Core.Dedupe;

public sealed class LogEventDeduper
{
    private static readonly TimeSpan BurstWindow = TimeSpan.FromSeconds(10);
    private readonly LogEventNormalizer _normalizer = new();

    public IReadOnlyList<DedupedLogEvent> Deduplicate(IReadOnlyList<LogEvent> events, DedupeMode mode)
    {
        return mode switch
        {
            DedupeMode.None => events.Select(e => new DedupedLogEvent(e, 1)).ToArray(),
            DedupeMode.Exact => DeduplicateByKey(events, normalize: false),
            DedupeMode.Normalized => DeduplicateByKey(events, normalize: true),
            DedupeMode.Burst => DeduplicateBursts(events),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported dedupe mode.")
        };
    }

    private IReadOnlyList<DedupedLogEvent> DeduplicateByKey(IReadOnlyList<LogEvent> events, bool normalize)
    {
        var results = new List<DedupedLogEvent>();
        var indexes = new Dictionary<DedupeKey, int>();

        foreach (var original in events)
        {
            var candidate = normalize ? _normalizer.Normalize(original) : original;
            var key = DedupeKey.From(candidate);
            if (indexes.TryGetValue(key, out var index))
            {
                var existing = results[index];
                results[index] = existing with { Count = existing.Count + 1 };
                continue;
            }

            indexes[key] = results.Count;
            results.Add(new DedupedLogEvent(candidate, 1));
        }

        return results;
    }

    private IReadOnlyList<DedupedLogEvent> DeduplicateBursts(IReadOnlyList<LogEvent> events)
    {
        var results = new List<DedupedLogEvent>();
        DedupedLogEvent? current = null;
        DedupeKey? currentKey = null;
        DateTimeOffset? currentLastTimestamp = null;

        foreach (var logEvent in events)
        {
            var key = DedupeKey.From(logEvent);
            if (current is not null
                && currentKey == key
                && logEvent.Timestamp is not null
                && currentLastTimestamp is not null
                && logEvent.Timestamp.Value - currentLastTimestamp.Value <= BurstWindow)
            {
                current = current with { Count = current.Count + 1 };
                currentLastTimestamp = logEvent.Timestamp;
                results[^1] = current;
                continue;
            }

            current = new DedupedLogEvent(logEvent, 1);
            currentKey = key;
            currentLastTimestamp = logEvent.Timestamp;
            results.Add(current);
        }

        return results;
    }

    private sealed record DedupeKey(string Category, string Verbosity, string Message, string ContinuationText)
    {
        public static DedupeKey From(LogEvent logEvent)
        {
            return new DedupeKey(
                logEvent.Category,
                logEvent.Verbosity,
                logEvent.Message,
                string.Join('\u001f', logEvent.ContinuationLines));
        }
    }
}
