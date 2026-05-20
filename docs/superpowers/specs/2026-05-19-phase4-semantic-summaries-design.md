# Phase 4 Semantic Summaries Design

## Goal

Upgrade `uelog summarize` with deterministic semantic findings, first likely failure detection, an important timeline, and compact Markdown-style output.

The classifier must be easy to extend without changing runtime code for every new Unreal Engine subsystem pattern. Domain-specific semantic rules are loaded from YAML. Only generic fallback rules live in code.

## Scope

Phase 4 adds:

- YAML-loaded semantic rule definitions.
- A deterministic semantic rule engine in `UeLogKit.Core`.
- Built-in default semantic rules for common Unreal log domains.
- Generic code fallback classifications for fatal and otherwise-unclassified error events.
- A reusable summary model and formatter.
- An upgraded `uelog summarize <logPath> [--profile=<Profile>]` output.

This phase does not add AI runtime classification, MCP server behavior, multi-log comparison, or real log fixtures.

## Public-Safe Rule Authoring

Committed rules and tests must use synthetic examples only. No real logs, internal identifiers, proprietary source snippets, platform NDA details, secrets, or private project data may be committed.

AI may be used later to analyze sanitized log corpora and propose candidate YAML rules, but those rules must be reviewed, committed, and tested as deterministic data before runtime use. The CLI must not call AI to classify logs in this phase.

## Architecture

The runtime flow is:

```text
LogEvent[]
  -> SemanticRuleLoader
  -> SemanticRuleEngine
  -> LogSummaryBuilder
  -> SummaryMarkdownFormatter
  -> uelog summarize output
```

The CLI remains thin. It parses events, loads the selected profile and default semantic rules, builds a summary, and writes formatted text. Classification and summary construction live in core so future MCP and compare features can reuse them.

## Semantic Rule Files

Default rules are YAML files bundled with the core project.

Initial path:

```text
src/UeLogKit.Core/Semantics/Rules/ue-default.semantic-rules.yaml
```

The rules file is copied to the build output so it works for tests, normal CLI runs, and local publish output.

The loader supports built-in rule names first:

```text
ue-default
```

The design should leave room for loading a custom YAML file later, but the first CLI version does not need a public `--semantic-rules=` option.

## Rule Schema

The MVP schema is deliberately small:

```yaml
name: ue-default
version: "1"
rules:
  - id: ue.network.failure
    kind: network.failure
    severity: high
    reason: Network failure detected
    match:
      categories:
        - LogNet
        - LogNetTraffic
      min_verbosity: Warning
      message_contains_any:
        - NetworkFailure
        - PendingConnectionFailure
      message_contains_all: []
```

Fields:

- `id`: stable rule identifier for tests and diagnostics.
- `kind`: semantic event kind shown in summaries.
- `severity`: `low`, `medium`, `high`, or `critical`.
- `reason`: concise human-readable explanation.
- `match.categories`: optional category allow-list.
- `match.min_verbosity`: optional minimum severity threshold using Unreal verbosity ordering.
- `match.message_contains_any`: optional list where at least one term must appear.
- `match.message_contains_all`: optional list where every term must appear.

All message matching is case-insensitive. A missing match field means that field does not constrain the rule.

## Built-In Generic Fallbacks

Only generic fallbacks are implemented in code:

- `Fatal` verbosity or message containing `Fatal error` produces `runtime.fatal` with `critical` severity when no YAML rule already classified the event.
- `Error` verbosity produces `runtime.error` with `high` severity when no YAML rule already classified the event.

These fallbacks prevent the summary from missing high-severity generic failures while keeping subsystem-specific knowledge in YAML.

## Default Rule Coverage

The initial YAML ruleset should cover broad Unreal failure categories without overfitting to one project:

- network failures
- online session failures
- online login/auth failures
- travel failures
- load or streaming failures
- script or blueprint errors

Rules should use broad category and message terms, not exact project-specific messages.

## Summary Model

Add reusable core records for:

- `SemanticLogEvent`: classification result with rule id, kind, severity, reason, source line, category, timestamp, relative time, and message.
- `LogSummary`: aggregate summary with event counts, warning/error counts, optional important event count, semantic findings, first likely failure, and important timeline entries.

The summary builder should consume `IReadOnlyList<LogEvent>`, optional `LogProfile`, and semantic rules.

First likely failure selection is deterministic:

1. Choose the earliest semantic event with severity `critical`.
2. If none, choose earliest `high`.
3. If none, choose earliest `medium`.
4. If none, no first likely failure is reported.

Important timeline entries include semantic findings plus profile-important events, ordered by source line, with a small cap so output stays compact.

## `uelog summarize` Output

The command keeps existing count lines for compatibility:

```text
Total events: 42
Warnings: 3
Errors: 1
Important events: 5
```

Then it appends compact Markdown-style sections when findings exist:

```text
First likely failure:
- [12] network.failure high: Network failure detected

Semantic findings:
- network.failure: 2
- travel.failure: 1
- runtime.error: 1

Important timeline:
- [10] LogOnline Warning: ...
- [12] LogNet Warning: ...
```

If there are no semantic findings, the command should omit the semantic sections rather than printing empty headings.

## Testing

Tests use synthetic logs and direct core objects.

Coverage:

- YAML rule loading succeeds for `ue-default`.
- Rule matching is deterministic and case-insensitive.
- Domain-specific events are classified by YAML rules, not generic fallbacks.
- Fatal and Error fallback classifications work when no YAML rule matches.
- First likely failure selection follows severity and source order.
- Summary output keeps existing count lines and appends semantic sections.
- Generated fixtures do not contain real logs or sensitive identifiers.

## Non-Goals

- No runtime AI classifier.
- No large corpus analysis workflow.
- No custom semantic rule CLI option in the first Phase 4 slice.
- No MCP resources or tools.
- No multi-log comparison.
