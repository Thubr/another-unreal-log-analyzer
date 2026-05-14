namespace UeLogKit.Core;

public sealed record ParserOptions(
    bool IncludeContinuationLines = true,
    bool CaptureRawTextHash = true,
    bool StrictSchemaVersion = true,
    string ExpectedSchemaVersion = LogEventSchemaVersion.V1
);
