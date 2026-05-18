# Normalization Redaction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a core `LogEvent` normalization component that redacts dynamic identifier-like values into public-safe placeholders.

**Architecture:** Keep normalization downstream of parsing. The parser continues to emit observed `LogEvent` data, and a new core pipeline stage returns normalized `LogEvent` copies with safe `Message`, `ContinuationLines`, and `Fields` values.

**Tech Stack:** C#/.NET 8, xUnit tests, existing `ILogEventPipelineStage` contract.

---

### Task 1: Core Normalization Stage

**Files:**
- Create: `src/UeLogKit.Core/Normalization/LogEventNormalizer.cs`
- Create: `tests/UeLogKit.Core.Tests/Normalization/LogEventNormalizerTests.cs`

- [ ] **Step 1: Write failing tests for message and field redaction**

Create `tests/UeLogKit.Core.Tests/Normalization/LogEventNormalizerTests.cs` with tests that instantiate `LogEventNormalizer`, pass a synthetic `LogEvent`, and assert:

```csharp
Assert.Equal("Event=Session.Join Session=<session_id> Ticket=<ticket_id> User=<user_id>", normalized.Message);
Assert.Equal("<session_id>", normalized.Fields["Session"]);
Assert.Equal("<ticket_id>", normalized.Fields["Ticket"]);
Assert.Equal("<user_id>", normalized.Fields["User"]);
Assert.Equal("Session=<session_id>", normalized.ContinuationLines[0]);
Assert.Equal(logEvent.RawTextHash, normalized.RawTextHash);
```

The synthetic source values must be `Session=Session-ABC123`, `Ticket=Ticket-98765`, and `User=User-42`.

- [ ] **Step 2: Run the failing tests**

Run:

```powershell
dotnet test tests/UeLogKit.Core.Tests/UeLogKit.Core.Tests.csproj --filter FullyQualifiedName~LogEventNormalizerTests
```

Expected before implementation: compile failure because `LogEventNormalizer` does not exist.

- [ ] **Step 3: Implement the minimal normalizer**

Create `src/UeLogKit.Core/Normalization/LogEventNormalizer.cs` with:

```csharp
using System.Text.RegularExpressions;

namespace UeLogKit.Core.Normalization;

public sealed partial class LogEventNormalizer : ILogEventPipelineStage
{
    public async IAsyncEnumerable<LogEvent> ProcessAsync(
        IAsyncEnumerable<LogEvent> input,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
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
        var normalized = SessionPattern().Replace(text, "$1<session_id>");
        normalized = TicketPattern().Replace(normalized, "$1<ticket_id>");
        normalized = UserPattern().Replace(normalized, "$1<user_id>");
        return normalized;
    }

    [GeneratedRegex(@"\b(Session(?:Id)?=)([A-Za-z0-9_-]+)", RegexOptions.CultureInvariant)]
    private static partial Regex SessionPattern();

    [GeneratedRegex(@"\b(Ticket(?:Id)?=)([A-Za-z0-9_-]+)", RegexOptions.CultureInvariant)]
    private static partial Regex TicketPattern();

    [GeneratedRegex(@"\b(User(?:Id)?=)([A-Za-z0-9_-]+)", RegexOptions.CultureInvariant)]
    private static partial Regex UserPattern();
}
```

- [ ] **Step 4: Run targeted tests**

Run:

```powershell
dotnet test tests/UeLogKit.Core.Tests/UeLogKit.Core.Tests.csproj --filter FullyQualifiedName~LogEventNormalizerTests
```

Expected after implementation: all `LogEventNormalizerTests` pass.

- [ ] **Step 5: Run full verification**

Run:

```powershell
dotnet test UeLogKit.sln
```

Expected: all tests pass, including parser contract tests.

### Assumptions

- This task does not wire normalization into CLI commands yet.
- This task does not remove original parser fields or raw hashes.
- Placeholder support is intentionally limited to session, ticket, and user identifiers because those are already listed in the public repo guidance and are enough to prove the pipeline shape.
