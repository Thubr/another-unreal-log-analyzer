# another-unreal-log-analyzer

aUELA - Another Unreal Engine Log Analyzer is a simple tool aiming to make Unreal logs more readable, filterable and sortable.

## Usage (CLI MVP)

The repository now includes a minimal CLI project at `src/UeLogKit.Cli` with these commands:

```bash
uelog parse <logPath> [--format=json|ndjson]
uelog summarize <logPath>
uelog filter <logPath> [--category=<Category>] [--min-level=<Level>]
uelog clean <logPath>
```

### Command details

- `parse`
  - Parses a log file through the current default parser and writes structured events.
  - `--format=json` (default) writes a JSON array.
  - `--format=ndjson` writes one JSON event per line.

- `summarize`
  - Prints a minimal summary including:
    - total event count
    - warning count
    - error count (includes `Error` and `Fatal` verbosity)

- `filter`
  - Filters parsed events and prints matching lines in a readable format.
  - `--category=<Category>` keeps only events with a matching category.
  - `--min-level=<Level>` keeps events at or above the provided severity threshold.
  - `--contains=<Text>` keeps only events whose message contains the text (case-insensitive).
  - `--since=<TimeSpan>` and `--until=<TimeSpan>` filter by relative offset from first timestamped event (for example `00:00:05`).

- `clean`
  - Emits simplified, normalized text lines in `Category: Verbosity: Message` format.

### Examples

```bash
uelog parse Saved/Logs/Game.log --format=json
uelog parse Saved/Logs/Game.log --format=ndjson
uelog summarize Saved/Logs/Game.log
uelog filter Saved/Logs/Game.log --category=LogNet --min-level=Warning
uelog clean Saved/Logs/Game.log
```

> Note: this is an intentionally narrow MVP command surface focused on parse/summarize/filter/clean flows.
