# Local CLI Build Artifact Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a root-level build flow that publishes a ready-to-run Windows executable at `build/uelog/win-x64/uelog.exe`.

**Architecture:** Keep packaging outside the CLI runtime code. A PowerShell script invokes `dotnet publish` for `src/UeLogKit.Cli/UeLogKit.Cli.csproj` with self-contained single-file defaults, while documentation explains how to build and run the resulting executable.

**Tech Stack:** .NET 8, PowerShell, existing xUnit test suite.

---

## File Structure

- Create `build.ps1`: owns local publish orchestration and defaults.
- Modify `.gitignore`: ignores generated `/build/` artifacts.
- Modify `README.md`: documents local executable build and usage.
- Existing CLI code remains unchanged because packaging can be handled entirely by `dotnet publish`.

## Task 1: Ignore Local Build Artifacts

**Files:**
- Modify: `.gitignore`

- [ ] **Step 1: Add failing git-ignore verification**

Run:

```bash
git check-ignore build/uelog/win-x64/uelog.exe
```

Expected: command exits non-zero because `/build/` is not ignored yet.

- [ ] **Step 2: Add `/build/` to `.gitignore`**

Add this line in the build output section:

```gitignore
/build/
```

- [ ] **Step 3: Verify build artifacts are ignored**

Run:

```bash
git check-ignore build/uelog/win-x64/uelog.exe
```

Expected: output includes `build/uelog/win-x64/uelog.exe`.

## Task 2: Add Local Publish Script

**Files:**
- Create: `build.ps1`

- [ ] **Step 1: Write script behavior before implementation**

The script must:

```text
Default Runtime: win-x64
Default Configuration: Release
Default OutputRoot: build/uelog
Project: src/UeLogKit.Cli/UeLogKit.Cli.csproj
Publish properties: SelfContained=true, PublishSingleFile=true
Final artifact path: build/uelog/<runtime>/uelog.exe for win-* runtimes
```

- [ ] **Step 2: Create `build.ps1`**

Create:

```powershell
[CmdletBinding()]
param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "build/uelog"
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectPath = Join-Path $RepoRoot "src/UeLogKit.Cli/UeLogKit.Cli.csproj"
$PublishDir = Join-Path $RepoRoot (Join-Path $OutputRoot $Runtime)
$ExecutableName = if ($Runtime.StartsWith("win-", [System.StringComparison]::OrdinalIgnoreCase)) { "uelog.exe" } else { "uelog" }
$ArtifactPath = Join-Path $PublishDir $ExecutableName

New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null

dotnet publish $ProjectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --output $PublishDir `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true

if (-not (Test-Path $ArtifactPath)) {
    throw "Expected artifact was not created: $ArtifactPath"
}

Write-Host "Published CLI artifact:"
Write-Host $ArtifactPath
```

- [ ] **Step 3: Verify script syntax**

Run:

```bash
pwsh -NoProfile -Command '$null = [scriptblock]::Create((Get-Content -Raw ./build.ps1)); "syntax ok"'
```

Expected: output contains `syntax ok`.

## Task 3: Document The Build Artifact Flow

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Add README section**

Add this section after the introductory paragraph and before `Usage (CLI MVP)`:

````markdown
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
./build/uelog/win-x64/uelog.exe parse path/to/synthetic.log --format=json
```

The build output folder is local-only and is ignored by git.
````

- [ ] **Step 2: Verify README mentions the artifact path**

Run:

```bash
rg -n "build/uelog/win-x64/uelog.exe|./build.ps1" README.md
```

Expected: output includes both strings.

## Task 4: Full Verification

**Files:**
- No file edits.

- [ ] **Step 1: Run the test suite**

Run:

```bash
dotnet test UeLogKit.sln
```

Expected: tests pass.

- [ ] **Step 2: Publish the executable**

Run:

```bash
pwsh ./build.ps1
```

Expected: command exits zero and prints `build/uelog/win-x64/uelog.exe`.

- [ ] **Step 3: Confirm artifact exists**

Run:

```bash
test -f build/uelog/win-x64/uelog.exe
```

Expected: command exits zero.

- [ ] **Step 4: Confirm generated artifact remains ignored**

Run:

```bash
git status --short --ignored build
```

Expected: output shows ignored build content with `!! build/` or ignored entries under `build/`.
