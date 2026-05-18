# another-unreal-log-analyzer

aUELA - Another Unreal Engine Log Analyzer is a simple tool aiming to make Unreal logs more readable, filterable and sortable.

## Usage (CLI MVP)

The repository now includes a minimal CLI project at `src/UeLogKit.Cli` with these commands:

```bash
uelog parse <logPath> [--format=json|ndjson] [--normalize]
uelog summarize <logPath> [--profile=<Profile>]
uelog filter <logPath> [--category=<Category>] [--min-level=<Level>] [--profile=<Profile>] [--normalize]
uelog clean <logPath> [--dedupe=none|exact|normalized|burst]
```

### Command details

- `parse`
  - Parses a log file through the current default parser and writes structured events.
  - `--format=json` (default) writes a JSON array.
  - `--format=ndjson` writes one JSON event per line.
  - `--normalize` redacts supported identifier-like values in structured output.

- `summarize`
  - Prints a minimal summary including:
    - total event count
    - warning count
    - error count (includes `Error` and `Fatal` verbosity)
  - `--profile=<Profile>` adds an important event count using a built-in profile name such as `ue-default` or `ue-online`, or a YAML profile path.

- `filter`
  - Filters parsed events and prints matching lines in a readable format.
  - `--category=<Category>` keeps only events with a matching category.
  - `--min-level=<Level>` keeps events at or above the provided severity threshold.
  - `--contains=<Text>` keeps only events whose message contains the text (case-insensitive).
  - `--since=<TimeSpan>` and `--until=<TimeSpan>` filter by relative offset from first timestamped event (for example `00:00:05`).
  - `--profile=<Profile>` excludes categories marked as noise by the profile.
  - `--normalize` redacts supported identifier-like values in filtered output.

- `clean`
  - Emits simplified, normalized text lines in `Category: Verbosity: Message` format.
  - `--dedupe=exact` collapses identical category/verbosity/message/continuation groups.
  - `--dedupe=normalized` collapses groups after identifier normalization.
  - `--dedupe=burst` collapses adjacent timestamped repeats inside a short burst window.

### Examples

```bash
uelog parse Saved/Logs/Game.log --format=json
uelog parse Saved/Logs/Game.log --format=ndjson
uelog parse Saved/Logs/Game.log --format=json --normalize
uelog summarize Saved/Logs/Game.log --profile=ue-default
uelog filter Saved/Logs/Game.log --category=LogNet --min-level=Warning
uelog filter Saved/Logs/Game.log --category=LogOnline --normalize
uelog filter Saved/Logs/Game.log --profile=ue-online
uelog clean Saved/Logs/Game.log --dedupe=normalized
```

> Note: this is an intentionally narrow MVP command surface focused on parse/summarize/filter/clean flows.
