# another-unreal-log-analyzer

aUELA - Another Unreal Engine Log Analyzer is a simple tool aiming to make Unreal logs more readable, filterable and sortable.

## Build a local executable

From the repository root, publish a ready-to-run Windows x64 executable:

```powershell
./build.ps1
```

The executable is written to:

```text
build/uelog/win-x64/uelog.exe
```

Run it directly:

```powershell
./build/uelog/win-x64/uelog.exe analyze path/to/synthetic.log --summary
```

The build output folder is local-only and is ignored by git.

## Usage

The primary CLI flow is `analyze`, a composable pipeline over one log:

```text
parse -> normalize/redact -> dedupe/clean -> filter -> summarize/project -> output
```

```bash
uelog analyze <logPath> [options]
uelog inspect <logPath> [--profile=<Profile>]
```

### `analyze` options

Cleanup and dedupe:

```bash
--normalize
--dedupe=none|exact|normalized|burst
--clean-only
```

Filters:

```bash
--category=<Category>
--category=<A>,<B>
--exclude-category=<A>,<B>
--min-level=<Fatal|Error|Warning|Display|Log|Verbose|VeryVerbose>
--contains=<Text>
--filter=<Text>
--since=<TimeSpan>
--until=<TimeSpan>
--profile=<Profile>
```

Output:

```bash
--summary
--facets
--no-events
--format=text|json|ndjson
--out=<path>
--limit=<n>
--explain
--preset=triage|clean|errors|online
```

By default, `uelog analyze <logPath>` prints concise readable event rows. It does not dedupe or normalize unless requested. `--clean-only` defaults to normalized readable output.

### Common workflows

I want to remove duplicates:

```bash
uelog analyze Saved/Logs/Game.log --clean-only --dedupe=normalized
```

I want only errors and warnings:

```bash
uelog analyze Saved/Logs/Game.log --min-level=Warning
```

I want a clean file for sharing:

```bash
uelog analyze Saved/Logs/Game.log --clean-only --dedupe=normalized --out=Saved/Logs/Game.clean.log
```

I want machine-readable output:

```bash
uelog analyze Saved/Logs/Game.log --dedupe=normalized --category=LogOnline --format=ndjson
```

I want to understand what will run:

```bash
uelog analyze Saved/Logs/Game.log --normalize --dedupe=normalized --category=LogNet --min-level=Warning --explain
```

### Other commands

The CLI still includes these focused commands for compatibility and direct access to narrower stages:

```bash
uelog analyze <logPath> [options]
uelog parse <logPath> [--format=json|ndjson] [--normalize]
uelog summarize <logPath> [--profile=<Profile>]
uelog filter <logPath> [--category=<Category>] [--min-level=<Level>] [--profile=<Profile>] [--normalize]
uelog clean <logPath> [--dedupe=none|exact|normalized|burst]
uelog categories <logPath> [--profile=<Profile>] [--format=text|json]
uelog inspect <logPath> [--profile=<Profile>]
```

### Command details

- `analyze`
  - Runs the composable parse, cleanup, filter, summary, and output pipeline.
  - `--dedupe=normalized --category=<Category>` dedupes before filtering.
  - `--clean-only` emits simplified `Category: Verbosity: Message` lines and normalizes by default.
  - `--summary --facets` prints event totals, warning/error totals, category counts, and verbosity counts.
  - `--format=json` writes a JSON array of matching structured events.
  - `--format=ndjson` writes one matching structured event per line.
  - `--preset=triage|clean|errors|online` applies a workflow shortcut; explicit flags override preset defaults.

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
  - `--category=<A>,<B>` keeps events matching any listed category.
  - `--exclude-category=<A>,<B>` excludes listed categories.
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

- `categories`
  - Lists categories present in the log with event counts, sorted by count.
  - `--profile=<Profile>` excludes categories marked as noise by the profile.
  - `--format=json` emits the full facet summary for tooling.

- `inspect`
  - Opens a full-pane terminal facet browser over one parsed log.
  - Shows category counts, level filters, matching events, and selected-event detail.
  - Exports a reproducible normalized `uelog analyze` command from the current filter state.

### Examples

```bash
uelog analyze Saved/Logs/Game.log
uelog analyze Saved/Logs/Game.log --dedupe=normalized --category=LogOnline --min-level=Warning
uelog analyze Saved/Logs/Game.log --clean-only --dedupe=normalized
uelog analyze Saved/Logs/Game.log --profile=ue-online --summary --facets
uelog analyze Saved/Logs/Game.log --dedupe=burst --filter=join --format=ndjson
uelog parse Saved/Logs/Game.log --format=json
uelog parse Saved/Logs/Game.log --format=ndjson
uelog parse Saved/Logs/Game.log --format=json --normalize
uelog summarize Saved/Logs/Game.log --profile=ue-default
uelog filter Saved/Logs/Game.log --category=LogNet --min-level=Warning
uelog filter Saved/Logs/Game.log --category=LogOnline --normalize
uelog filter Saved/Logs/Game.log --profile=ue-online
uelog clean Saved/Logs/Game.log --dedupe=normalized
uelog categories Saved/Logs/Game.log --profile=ue-default
uelog categories Saved/Logs/Game.log --format=json
uelog inspect Saved/Logs/Game.log --profile=ue-online
```
