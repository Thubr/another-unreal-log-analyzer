using UeLogKit.Core.Normalization;

namespace UeLogKit.Core.Tests.Normalization;

public sealed class LogEventNormalizerTests
{
    [Fact]
    public void Normalize_redacts_dynamic_identifiers_in_message_continuations_and_fields()
    {
        var logEvent = new LogEvent(
            SchemaVersion: "1.0",
            SourceId: "synthetic-source",
            SourcePath: "synthetic.log",
            LineNumber: 42,
            Timestamp: new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            RelativeTime: TimeSpan.FromSeconds(12),
            Frame: 7,
            Category: "LogOnline",
            Verbosity: "Warning",
            Message: "Event=Session.Join Session=Session-ABC123 Ticket=Ticket-98765 User=User-42",
            ContinuationLines: ["Session=Session-ABC123"],
            Fields: new Dictionary<string, string>
            {
                ["Session"] = "Session-ABC123",
                ["Ticket"] = "Ticket-98765",
                ["User"] = "User-42",
                ["Event"] = "Session.Join"
            },
            RawTextHash: "synthetic-hash");

        var normalized = new LogEventNormalizer().Normalize(logEvent);

        Assert.Equal("Event=Session.Join Session=<session_id> Ticket=<ticket_id> User=<user_id>", normalized.Message);
        Assert.Equal("<session_id>", normalized.Fields["Session"]);
        Assert.Equal("<ticket_id>", normalized.Fields["Ticket"]);
        Assert.Equal("<user_id>", normalized.Fields["User"]);
        Assert.Equal("Session=<session_id>", normalized.ContinuationLines[0]);
        Assert.Equal(logEvent.RawTextHash, normalized.RawTextHash);
        Assert.Equal(logEvent.SourceId, normalized.SourceId);
        Assert.Equal(logEvent.SourcePath, normalized.SourcePath);
        Assert.Equal(logEvent.LineNumber, normalized.LineNumber);
        Assert.Equal(logEvent.Timestamp, normalized.Timestamp);
        Assert.Equal(logEvent.RelativeTime, normalized.RelativeTime);
        Assert.Equal(logEvent.Frame, normalized.Frame);
        Assert.Equal(logEvent.Category, normalized.Category);
        Assert.Equal(logEvent.Verbosity, normalized.Verbosity);
        Assert.Equal(logEvent.SchemaVersion, normalized.SchemaVersion);
    }

    [Fact]
    public void Normalize_redacts_quoted_key_values_and_id_aliases()
    {
        var logEvent = new LogEvent(
            SchemaVersion: "1.0",
            SourceId: "synthetic-source",
            SourcePath: "synthetic.log",
            LineNumber: 43,
            Timestamp: null,
            RelativeTime: null,
            Frame: null,
            Category: "LogOnline",
            Verbosity: "Display",
            Message: "Session=\"Session-ABC123\" TicketId=\"Ticket-98765\" UserId=\"User-42\"",
            ContinuationLines: [
                "SessionId=\"Session-ABC123\"",
                "ticket=\"Ticket-98765\" user=\"User-42\""
            ],
            Fields: new Dictionary<string, string>
            {
                ["SessionId"] = "Session-ABC123",
                ["TicketId"] = "Ticket-98765",
                ["UserId"] = "User-42",
                ["session"] = "Session-ABC123",
                ["ticket"] = "Ticket-98765",
                ["user"] = "User-42"
            },
            RawTextHash: "synthetic-hash-quoted");

        var normalized = new LogEventNormalizer().Normalize(logEvent);

        Assert.Equal("Session=\"<session_id>\" TicketId=\"<ticket_id>\" UserId=\"<user_id>\"", normalized.Message);
        Assert.Equal("SessionId=\"<session_id>\"", normalized.ContinuationLines[0]);
        Assert.Equal("ticket=\"<ticket_id>\" user=\"<user_id>\"", normalized.ContinuationLines[1]);
        Assert.Equal("<session_id>", normalized.Fields["SessionId"]);
        Assert.Equal("<ticket_id>", normalized.Fields["TicketId"]);
        Assert.Equal("<user_id>", normalized.Fields["UserId"]);
        Assert.Equal("<session_id>", normalized.Fields["session"]);
        Assert.Equal("<ticket_id>", normalized.Fields["ticket"]);
        Assert.Equal("<user_id>", normalized.Fields["user"]);
        Assert.Equal(logEvent.RawTextHash, normalized.RawTextHash);
    }

    [Fact]
    public void Normalize_redacts_json_like_continuation_lines()
    {
        var logEvent = new LogEvent(
            SchemaVersion: "1.0",
            SourceId: "synthetic-source",
            SourcePath: "synthetic.log",
            LineNumber: 44,
            Timestamp: null,
            RelativeTime: null,
            Frame: null,
            Category: "LogHttp",
            Verbosity: "Warning",
            Message: "Synthetic payload attached",
            ContinuationLines: [
                "  \"ticket\": \"Ticket-98765\",",
                "  \"userId\": \"User-42\",",
                "  \"sessionId\": \"Session-ABC123\""
            ],
            Fields: new Dictionary<string, string>(),
            RawTextHash: "synthetic-hash-json");

        var normalized = new LogEventNormalizer().Normalize(logEvent);

        Assert.Equal("  \"ticket\": \"<ticket_id>\",", normalized.ContinuationLines[0]);
        Assert.Equal("  \"userId\": \"<user_id>\",", normalized.ContinuationLines[1]);
        Assert.Equal("  \"sessionId\": \"<session_id>\"", normalized.ContinuationLines[2]);
        Assert.Equal(logEvent.RawTextHash, normalized.RawTextHash);
    }
}
