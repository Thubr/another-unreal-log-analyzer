# Local CLI Build Artifact Design

## Goal

Make the CLI easy to build and use locally by producing a ready-to-run executable artifact in a repository `build/` folder.

The default artifact is a Windows x64 self-contained single-file executable:

```text
build/uelog/win-x64/uelog.exe
```

## Scope

- Add a root-level build script for publishing the CLI.
- Publish `src/UeLogKit.Cli/UeLogKit.Cli.csproj`.
- Default to `Release`, `win-x64`, self-contained, and single-file output.
- Keep the generated `build/` folder out of git.
- Document the build and run commands in `README.md`.

This does not add real logs, proprietary examples, internal identifiers, or fixture data.

## User Experience

From the repository root, a developer can run:

```powershell
./build.ps1
```

The script creates:

```text
build/uelog/win-x64/uelog.exe
```

The executable can then be run directly:

```powershell
./build/uelog/win-x64/uelog.exe parse path/to/synthetic.log --format=json
```

The script accepts optional parameters for runtime, configuration, and output root so future builds can target other runtime identifiers without changing the default path.

## Architecture

The CLI remains a normal .NET console application. Packaging is handled outside the application code with `dotnet publish`, so parser, normalization, profile, and dedupe behavior remain unchanged.

The build script is responsible for:

- resolving the repository root,
- creating the output folder when needed,
- calling `dotnet publish` with deterministic publish settings,
- failing immediately if publish fails.

The publish command uses these defaults:

```text
Configuration: Release
Runtime: win-x64
SelfContained: true
PublishSingleFile: true
Output: build/uelog/<runtime>/
```

## Files

- `build.ps1`: root-level publish script.
- `.gitignore`: add `/build/`.
- `README.md`: document local executable build and usage.
- Existing tests remain synthetic and continue to validate CLI behavior.

## Error Handling

PowerShell error handling should stop on publish failures so a failed build does not look successful. The script should print the final artifact path only after `dotnet publish` exits successfully.

## Testing And Verification

Verification consists of:

- run the existing .NET test suite,
- run the new build script or equivalent `dotnet publish`,
- confirm `build/uelog/win-x64/uelog.exe` exists.

No tests or docs will use real Unreal logs or sensitive project data.
