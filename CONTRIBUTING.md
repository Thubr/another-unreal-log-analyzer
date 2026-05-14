# Contributing

Thanks for contributing to **aUELA** (another Unreal Engine Log Analyzer).

## Ground Rules (Public Repo)

This repository is public. All contributions must be safe for public disclosure.

### Never commit

- real production logs,
- proprietary source snippets,
- platform SDK internals,
- real player IDs, service IDs, session IDs, ticket IDs, lobby IDs,
- real IPs, server URLs, machine names, internal paths,
- secrets/tokens/credentials.

### Required placeholder style

Use placeholders like:

- `<ticket_id>`
- `<entity_id>`
- `<session_id>`
- `<queue_name>`
- `<ip_address>`
- `<user_id>`
- `<machine_name>`

### Fixtures policy

- Prefer fully synthetic fixtures.
- Sanitized fixtures require explicit maintainer approval.
- Add/extend sanitization and normalization tests before introducing new fixture categories.

## Development Expectations

- Follow contracts-first development.
- Keep parser boundaries clean (downstream systems consume `LogEvent`, not raw lines).
- Add tests with each change.
- Keep docs updated when behavior changes.

## Pull Requests

PRs should include:

1. Summary of change,
2. Test evidence,
3. Confirmation that no sensitive data was introduced.
