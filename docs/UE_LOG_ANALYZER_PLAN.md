# UE Log Analyzer — Planning Document

## Public Repository Constraint

This repository will be public from the beginning.

Design and implementation must assume that all committed code, fixtures, profiles, documentation, examples, and generated reports are visible to external users.

### Public-safe rules

Do not commit:

- real game logs from commercial projects
- proprietary source snippets from Unreal Engine, platform SDKs, console SDKs, or private plugins
- real player identifiers
- real deployment IDs, session IDs, server URLs, IPs, tokens, secrets, or API keys
- NDA-covered platform details
- internal paths, usernames, machine names, or build-machine names
- customer/support conversations unless explicitly approved and sanitized

### Fixtures and examples

All test fixtures must be either:

1. fully synthetic logs created for this repository, or
2. heavily sanitized logs with no project, user, service, or platform-sensitive data.

Preferred fixture style:

```text
[2026.01.01-12.00.00:000][123]LogOnline: Warning: JoinSession failed. Session=<session_id> Result=Unknown
[2026.01.01-12.00.01:000][124]LogNet: Warning: NetworkFailure: PendingConnectionFailure
```

Use placeholders:

- <ticket_id>
- <queue_name>
- <entity_id>
- <session_id>
- <lobby_id>
- <ip_address>
- <user_id>
- <machine_name>
- <project_name>

## Placement in the repository

Use multiple Markdown files once implementation starts:

- `README.md`: short user-facing overview, install/build instructions, and common CLI examples.
- `docs/architecture.md`: detailed architecture, parser contract, MCP design, profiles, and extension points.
- `AGENTS.md`: development rules for Codex/AI agents, including parser-boundary constraints, testing expectations, and non-goals.

For the initial planning commit, use this document as:

```text
/docs/ue-log-analyzer-plan.md
```

After the project skeleton exists, split it into `README.md`, `docs/architecture.md`, and `AGENTS.md`.

---

## Goal

Build a reusable Unreal Engine log analyzer that can parse, clean, summarize, filter, compare, and expose UE logs to AI tools without requiring the AI to read full raw log files.

The tool must support:

- Generic Unreal Engine log analysis.
- Project-specific profiles.
- Noise suppression.
- Duplicate suppression.
- Semantic event extraction.
- Human-readable Markdown reports.
- Machine-readable JSON output.
- Host/client log comparison.
- Future AI integration through MCP.
- Replaceable parser implementations.
- Test-driven development with deterministic, reviewable outputs.

---

## Recommended stack

Use C# / .NET for the first implementation.

### Rationale

C# is preferred because:

- It is easier for many UE/C++ teams to review than Rust.
- It has strong test tooling.
- It has good CLI, JSON, YAML, and file-processing support.
- It is suitable for an MCP server.
- It is productive for mostly AI-assisted/vibecoded development if protected by tests.
- It can stream large logs efficiently when implemented carefully.

Rust remains a valid future optimization path, especially for very large logs. The architecture must preserve this option from day one.

---

## Core architectural rule

All downstream systems must consume structured `LogEvent` objects, not raw text lines.

This applies to:

- filters
- dedupe
- normalizers
- semantic classifiers
- profile rules
- summarizers
- host/client comparison
- report generation
- JSON export
- MCP server tools/resources

Only parser adapters may read raw log lines directly.

---

## Replaceable parser architecture

The parser is an implementation detail behind a stable contract.

```text
Raw log file
  -> ILogEventSource / ILogParser
    -> stream of LogEvent objects
      -> filters
      -> dedupe
      -> classifiers
      -> summarizers
      -> reports
      -> MCP
```

The default parser is implemented in C#, but the architecture must allow replacing it later with:

1. an external executable that streams NDJSON `LogEvent` records, or
2. a native library adapter.

No downstream code may depend on parser-specific classes, regexes, raw line strings, or C# parser internals.

---

## `LogEvent` contract

Define `LogEvent` in the core library. It is the canonical interchange object.

```csharp
public sealed record LogEvent(
    string SourceId,
    string SourcePath,
    int LineNumber,
    DateTimeOffset? Timestamp,
    TimeSpan? RelativeTime,
    int? Frame,
    string Category,
    string Verbosity,
    string Message,
    IReadOnlyList<string> ContinuationLines,
    IReadOnlyDictionary<string, string> Fields,
    string RawTextHash
);
```

### Field meanings

- `SourceId`: stable ID for one loaded log file.
- `SourcePath`: original file path.
- `LineNumber`: first physical line used by this event.
- `Timestamp`: parsed absolute timestamp, if present.
- `RelativeTime`: relative timestamp, if available or derived.
- `Frame`: UE frame number, if present.
- `Category`: UE log category, such as `LogOnline`, `LogNet`, `LogTemp`, `LogLevel`.
- `Verbosity`: UE verbosity, such as `VeryVerbose`, `Verbose`, `Display`, `Warning`, `Error`, `Fatal`.
- `Message`: normalized event message body, excluding category/verbosity prefix.
- `ContinuationLines`: multiline payload attached to this event.
- `Fields`: extracted key/value data. Examples: `TicketId`, `Queue`, `Session`, `TransitionId`, `Platform`, `Role`, `Result`, `Reason`.
- `RawTextHash`: stable hash of the raw source text for dedupe/debugging without requiring downstream systems to keep raw lines.

Optional raw text access may exist only in parser/debug modules. Normal pipeline code should not require it.

---

## Parser interfaces

### Core parser abstraction

```csharp
public interface ILogEventSource
{
    IAsyncEnumerable<LogEvent> ReadEventsAsync(
        LogInput input,
        ParserOptions options,
        CancellationToken cancellationToken = default);
}
```

### Parser selection

Parser selection should be configuration-driven.

Examples:

```bash
uelog summarize Game.log --parser csharp
uelog summarize Game.log --parser external --parser-path ./uelog-rust-parser.exe
uelog summarize Game.log --parser native --parser-library ./uelog_parser.dll
```

Default:

```text
--parser csharp
```

---

## C# parser adapter

Project:

```text
UeLogKit.Parsing.CSharp
```

Responsibilities:

- Parse standard UE text logs.
- Support timestamped lines.
- Support lines without timestamps.
- Support malformed lines.
- Attach multiline continuations.
- Extract category, verbosity, frame, message.
- Extract basic `Fields` from key/value patterns.
- Emit `LogEvent` records.

The C# parser is the default implementation, not the architecture itself.

---

## External parser adapter

Project:

```text
UeLogKit.Parsing.ExternalProcess
```

Responsibilities:

- Launch an external executable.
- Pass input path and parser options.
- Read NDJSON from stdout.
- Convert each NDJSON line into `LogEvent`.
- Capture stderr as parser diagnostics.
- Fail clearly if the external parser emits invalid records.

Example external parser contract:

```bash
uelog-rust-parser --input Game.log --format ndjson
```

Each stdout line must be one complete `LogEvent` JSON object:

```json
{"sourceId":"game","sourcePath":"Game.log","lineNumber":42,"timestamp":"2026-05-14T19:30:00.123Z","frame":1024,"category":"LogOnline","verbosity":"Warning","message":"JoinSession failed","continuationLines":[],"fields":{"Session":"GameSession"},"rawTextHash":"sha256:..."}
```

### External parser rules

- It must stream records. It must not require the full file to be parsed before output starts.
- It must output valid NDJSON.
- It must preserve line numbers.
- It must use the same `LogEvent` schema version as the C# core.
- It must not perform summarization, dedupe, filtering, or classification unless explicitly added as separate optional tooling.

---

## Native parser adapter

Project:

```text
UeLogKit.Parsing.Native
```

Responsibilities:

- Load a native library parser if needed later.
- Convert native parser output into `LogEvent`.
- Keep all unsafe/native interop isolated.

This adapter is not required for MVP. The external NDJSON adapter is the safer first Rust integration path.

---

## Schema versioning

`LogEvent` must have a schema version in serialized form.

Example:

```json
{
  "schemaVersion": "uelog.logevent.v1",
  "sourceId": "client",
  "lineNumber": 1204,
  "category": "LogOnline",
  "verbosity": "Warning",
  "message": "JoinSession failed"
}
```

Versioning is required because external parsers may be developed independently.

---

## Downstream pipeline

All downstream stages accept `IAsyncEnumerable<LogEvent>` or materialized `IReadOnlyList<LogEvent>` depending on size and command needs.

```text
ILogEventSource
  -> LogEvent stream
  -> Profile tagging
  -> Normalization
  -> Filtering
  -> Dedupe
  -> Semantic classification
  -> Summarization
  -> Report/export/MCP
```

### Important constraint

Downstream systems may inspect:

- `Category`
- `Verbosity`
- `Message`
- `ContinuationLines`
- `Fields`
- timestamps
- line numbers
- source metadata

They may not inspect parser implementation details.

---

## Normalization

The normalizer receives `LogEvent` and returns `LogEvent` or an enriched wrapper such as `AnalyzedLogEvent`.

Normalize dynamic values such as:

```text
TicketId=abc123      -> TicketId=<ticket_id>
LobbyId=xyz789       -> LobbyId=<lobby_id>
SessionId=123        -> SessionId=<session_id>
EntityId=abc         -> EntityId=<entity_id>
```

Normalized text must be used for dedupe. Original values should remain available through `Fields` if useful and safe.

---

## Filters

Filters operate on `LogEvent`.

Examples:

```bash
uelog filter Game.log --category LogOnline,LogNet
uelog filter Game.log --exclude-category LogSlate,LogRHI
uelog filter Game.log --min-level Warning
uelog filter Game.log --contains "JoinSession"
uelog filter Game.log --since "00:01:00" --until "00:03:00"
```

Filters should be composable and testable.

---

## Dedupe

Dedupe operates on `LogEvent` or normalized event wrappers.

Support:

```bash
--dedupe exact
--dedupe normalized
--dedupe burst
```

### Exact dedupe

Collapses identical category/verbosity/message/continuation groups.

### Normalized dedupe

Collapses events that differ only by IDs, pointers, timestamps, or other configured dynamic values.

### Burst dedupe

Collapses repeated polling/timer events within a time window.

Example output:

```text
[17x over 8.2s] LogOnline: Polling session state...
```

---

## Profiles

Profiles are YAML files that configure analysis without recompiling.

Profiles can define:

- noise categories
- important categories
- important patterns
- field extraction patterns
- semantic event rules
- dedupe normalization patterns
- output preferences

Example generic profile:

```yaml
name: ue-default

noise_categories:
  - LogSlate
  - LogRHI
  - LogD3D12RHI
  - LogRenderer
  - LogShaderCompilers
  - LogDerivedDataCache
  - LogPakFile

important_categories:
  - LogOnline
  - LogNet
  - LogNetTraffic
  - LogTravel
  - LogLoad
  - LogStreaming
  - LogWindows
  - LogOutputDevice
  - LogBlueprint
  - LogScript
  - LogInit

important_patterns:
  - "Error:"
  - "Fatal error"
  - "ensure"
  - "assert"
  - "ConnectionTimeout"
  - "JoinSession"
  - "TravelFailure"
  - "NetworkFailure"
  - "Login failed"
  - "Access violation"
```

---

## Semantic event classifier

The classifier is deterministic and rule-based for MVP.

It consumes `LogEvent` and emits semantic events or annotations.

Example:

```json
{
  "kind": "online.session.join_failed",
  "severity": "high",
  "category": "LogOnline",
  "reason": "JoinSession failed",
  "confidence": 0.86,
  "sourceLine": 1204
}
```

Do not use AI for core classification in MVP. AI should consume the reduced outputs through MCP.

---

## Summarizer

The summarizer consumes `LogEvent`/classified events and outputs compact findings.

Include:

- file metadata
- total event count
- physical line count if available
- duration if timestamps exist
- category counts
- warning/error counts
- top repeated messages
- important timeline
- first likely failure
- suspicious gaps
- suggested next queries

Example output:

```text
File: Game.log
Events: 812,441
Duration: 00:08:22
Errors: 12
Warnings: 184

Most relevant:
1. [00:01:12.554] LogOnline Warning: Login failed
2. [00:01:18.903] LogOnline Error: JoinSession failed
3. [00:01:19.004] LogNet Warning: NetworkFailure: PendingConnectionFailure

Suppressed:
- 12,404 LogSlate events
- 4,821 LogRHI events
- 714 repeated polling events
```

---

## Log comparison

Comparison consumes multiple `LogEvent` streams.

Example command:

```bash
uelog compare Host.log Client.log --profile ue-online
```

Compare:

- first failure
- session lifecycle
- connection lifecycle
- travel lifecycle
- login/auth lifecycle
- host/client divergence
- whether one side continued while the other failed

Example output:

```text
Divergence:
- Host created matchmaking ticket and entered WaitingForMatch.
- Client received transition state but failed while joining the host ticket.
- Host reached MatchFound and continued alone.

Likely focus:
- JoinMatchMakingTicket path
- ticket propagation
- queue mismatch
- session teardown timing
```

---

## CLI commands

Implement:

```bash
uelog parse Game.log --json events.json
uelog summarize Game.log --profile ue-default
uelog clean Game.log --dedupe normalized --out Game.clean.log
uelog filter Game.log --category LogOnline --min-level Warning
uelog categories Game.log
uelog compare Host.log Client.log --profile ue-online
uelog index Saved/Logs --out .uelog-index
uelog mcp --index .uelog-index
```

Parser selection:

```bash
uelog summarize Game.log --parser csharp
uelog summarize Game.log --parser external --parser-path ./uelog-rust-parser.exe
uelog summarize Game.log --parser native --parser-library ./uelog_parser.dll
```

---

## Output formats

Support:

- plain text
- Markdown
- JSON
- NDJSON `LogEvent` stream
- cleaned log text

Recommended generated files:

```text
summary.txt
report.md
events.json
events.ndjson
cleaned.log
```

---

## MCP integration

Project:

```text
UeLogKit.McpServer
```

The MCP server must expose bounded, filtered, indexed access to logs. It must not expose full raw logs by default.

### MCP resources

```text
uelog://sessions
uelog://logs/{logId}/metadata
uelog://logs/{logId}/summary
uelog://logs/{logId}/timeline
uelog://logs/{logId}/errors
uelog://logs/{logId}/warnings
uelog://logs/{logId}/categories
uelog://logs/{logId}/events
```

### MCP tools

#### `load_log`

```json
{
  "path": "Saved/Logs/Game.log",
  "profile": "ue-online",
  "parser": "csharp"
}
```

#### `summarize_log`

```json
{
  "logId": "abc123",
  "maxEvents": 80,
  "profile": "ue-online"
}
```

#### `query_log`

```json
{
  "logId": "abc123",
  "category": ["LogOnline", "LogNet"],
  "minLevel": "Warning",
  "contains": ["JoinSession", "Failure"],
  "maxEvents": 100
}
```

#### `get_context_window`

```json
{
  "logId": "abc123",
  "line": 48291,
  "before": 30,
  "after": 50,
  "dedupe": true
}
```

#### `compare_logs`

```json
{
  "logIds": ["host", "client"],
  "alignBy": "timestamp",
  "profile": "ue-online"
}
```

#### `find_first_failure`

```json
{
  "logId": "abc123",
  "systems": ["online", "network", "travel"]
}
```

---

## AI usage pattern

The AI should receive summaries and targeted slices, not full raw logs.

First response from MCP should look like:

```json
{
  "metadata": {
    "eventCount": 820000,
    "duration": "00:12:44",
    "categories": ["LogInit", "LogOnline", "LogNet"]
  },
  "summary": {
    "errors": 18,
    "warnings": 244,
    "topRepeatedMessages": [],
    "importantTimeline": []
  },
  "suggestedQueries": [
    "Show first 50 warnings from LogOnline",
    "Show context around first Fatal",
    "Compare host/client around JoinSession"
  ]
}
```

Goal:

```text
Long logs stay outside the AI context. Only reduced summaries and selected context windows are passed to AI tools.
```

---

## Testing strategy

Use test-first development.

Recommended packages:

```text
xUnit or NUnit
FluentAssertions
Verify for snapshot/golden-file tests
YamlDotNet
System.Text.Json
BenchmarkDotNet
```

### Parser contract tests

Every parser implementation must pass the same parser contract test suite.

Test inputs:

- timestamped UE line
- UE line without timestamp
- malformed line
- multiline callstack
- JSON continuation
- HTTP payload continuation
- known category/verbosity examples

Expected output:

- identical `LogEvent` records regardless of parser implementation

This is mandatory so a future Rust parser can replace the C# parser without breaking downstream systems.

### Unit tests

Test:

- parsing UE lines
- loading YAML profiles
- applying filters
- normalizing dynamic values
- dedupe exact
- dedupe normalized
- dedupe burst
- semantic classification
- summary generation
- host/client comparison
- MCP query limits

### Golden-file tests

Store sample input and expected output:

```text
tests/fixtures/online/join-session-failed.log
tests/expected/online/join-session-failed.summary.md
```

Golden-file tests are important because reviewers can inspect output changes as diffs.

---

## Project structure

```text
UeLogKit/
  README.md
  AGENTS.md
  docs/
    architecture.md
    profiles.md
    mcp.md
    parser-contract.md
  src/
    UeLogKit.Core/
    UeLogKit.Parsing.CSharp/
    UeLogKit.Parsing.ExternalProcess/
    UeLogKit.Parsing.Native/
    UeLogKit.Cli/
    UeLogKit.McpServer/
    UeLogKit.Tests/
    UeLogKit.Benchmarks/
  profiles/
    ue-default.yaml
    ue-online.yaml
    ue-crash.yaml
    ue-cook.yaml
    ue-networking.yaml
  samples/
    basic-editor.log
    multiplayer-host.log
    multiplayer-client.log
```

---

## UE integration plan

Do not implement the parser as a UE plugin first.

Future UE plugin should be a thin wrapper:

```text
UE Editor Plugin
  -> calls uelog.exe
  -> reads JSON/Markdown output
  -> displays report in editor
```

Features:

- menu item: `Tools -> UE Log Analyzer`
- open latest `Saved/Logs`
- select profile
- run summary
- show timeline
- show category table
- copy AI-ready summary
- export support bundle

No parser logic should be duplicated inside the UE plugin.

---

## Development phases

### Phase 1 — Core contracts and parser boundary

Deliver:

- `LogEvent`
- `ILogEventSource`
- parser options
- parser selection model
- C# parser adapter
- NDJSON serialization/deserialization
- parser contract tests

Acceptance criteria:

- C# parser emits `LogEvent` records.
- Downstream test stubs consume only `LogEvent`.
- NDJSON roundtrip works.
- Parser contract tests exist before adding advanced analysis.

### Phase 2 — Generic CLI

Deliver:

- `uelog parse`
- `uelog summarize`
- `uelog filter`
- `uelog clean`

Acceptance criteria:

- CLI can parse a normal UE log.
- CLI can output NDJSON/JSON.
- CLI can summarize warnings/errors.
- CLI can filter by category and verbosity.

### Phase 3 — Profiles and dedupe

Deliver:

- YAML profile loading
- profile inheritance
- `ue-default`
- `ue-online`
- `ue-crash`
- `ue-cook`
- dedupe exact
- dedupe normalized
- dedupe burst

Acceptance criteria:

- Profiles influence filtering and importance scoring.
- Dedupe output is covered by golden tests.

### Phase 4 — Semantic events and summaries

Deliver:

- deterministic semantic classifier
- importance scoring
- important timeline
- first failure detection
- Markdown report generation

Acceptance criteria:

- Online/network/travel failures are detected.
- Summary is compact and useful.
- Classifier is tested and deterministic.

### Phase 5 — Multi-log comparison

Deliver:

- `uelog compare`
- host/client alignment
- divergence report

Acceptance criteria:

- Can compare two logs by timestamp or event order.
- Can identify first divergence.
- Can produce Markdown report.

### Phase 6 — MCP server

Deliver:

- `uelog mcp`
- bounded query tools
- summary resources
- context window retrieval
- compare logs through MCP

Acceptance criteria:

- MCP server does not return full raw logs by default.
- Query limits are enforced.
- MCP tools consume indexed `LogEvent` data.

### Phase 7 — Optional Rust parser

Deliver later only if needed:

- Rust parser executable
- NDJSON `LogEvent` streaming
- external parser adapter tests

Acceptance criteria:

- Rust parser passes the same parser contract tests.
- Downstream outputs are unchanged for the same fixture logs.

---

## Non-goals for version 1

Do not implement initially:

- full GUI
- full UE plugin parser
- AI-based classification
- binary trace analysis
- Unreal Insights replacement
- live log streaming
- automatic root-cause certainty claims
- parser-specific downstream logic

The tool should assist investigation, not claim certainty without evidence.

---

## Logging recommendations for UE projects

The analyzer should work with existing logs, but projects can improve results with structured logs.

Prefer:

```cpp
UE_LOG(LogOnlineFlow, Display,
    TEXT("Event=Session.Join.Start Session=%s User=%s"),
    *SessionName.ToString(),
    *UserId);
```

Instead of:

```cpp
UE_LOG(LogTemp, Warning,
    TEXT("[%s][Line: %d] - Trying to join"),
    *FString(__FUNCTION__),
    __LINE__);
```

Recommended structured fields:

```text
Event=
Session=
TicketId=
Queue=
TransitionId=
Platform=
Role=
UserId=
Result=
Reason=
State=
PreviousState=
NewState=
```

Important multiplayer systems should avoid `LogTemp` when possible.

---

## Final decision

Build a C#/.NET reusable UE log analyzer with a replaceable parser boundary.

Primary executable:

```bash
uelog
```

Primary MCP command:

```bash
uelog mcp
```

Primary architectural guarantee:

```text
Every downstream feature consumes LogEvent objects. Parser implementations are replaceable adapters.
```

Primary design goal:

```text
Make long Unreal Engine logs short, searchable, structured, comparable, and AI-queryable without consuming large context windows.
```
