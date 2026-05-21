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
$DotNetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$DotNetPath = if ($DotNetCommand) {
    $DotNetCommand.Source
} else {
    $Candidates = @(
        (Join-Path ${env:ProgramFiles} "dotnet/dotnet.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "dotnet/dotnet.exe")
    )

    $Candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}

if (-not $DotNetPath) {
    throw "The .NET SDK was not found. Install .NET 8 SDK or add dotnet to PATH."
}

New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null

& $DotNetPath publish $ProjectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --output $PublishDir `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true

if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path $ArtifactPath)) {
    throw "Expected artifact was not created: $ArtifactPath"
}

Write-Host "Published CLI artifact:"
Write-Host $ArtifactPath
