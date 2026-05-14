# Public Repository Guidelines

This document defines repository hygiene requirements for **aUELA**.

## 1) No-Real-Logs Policy

Do not commit real logs from internal, customer, or production environments.

Allowed data sources:

1. fully synthetic fixtures created for this repository,
2. explicitly sanitized fixtures approved as safe for publication.

## 2) Sensitive Data Prohibition

Do not commit or expose:

- proprietary or NDA-covered platform details,
- internal identifiers,
- secrets, tokens, credentials,
- internal infrastructure data (IPs, URLs, hostnames, machine names, internal paths),
- customer/support conversation data.

## 3) Placeholder and Normalization Standard

When examples need identity-like values, use placeholders:

- `<ticket_id>`
- `<entity_id>`
- `<session_id>`
- `<queue_name>`
- `<ip_address>`
- `<user_id>`
- `<machine_name>`

Normalization features must support redaction/normalization to these forms.

## 4) Synthetic Fixtures Requirement

All test fixtures for MVP must be synthetic.

Fixture goals:

- represent realistic UE logging patterns,
- include multiline and malformed cases,
- include high-volume duplicate/noise patterns,
- include sanitized token-like patterns for normalization tests.

## 5) Test Gate Before Real-Log Support

Before any real-log workflow is considered, the repo must have passing tests for:

- sanitization of identifiers,
- normalization of dynamic fields,
- prevention of raw sensitive token leakage in outputs.

## 6) Review Checklist

Every PR should confirm:

- [ ] No real logs included
- [ ] No secrets or internal identifiers included
- [ ] Fixtures are synthetic or explicitly sanitized
- [ ] Sanitization/normalization tests added or updated
- [ ] Docs updated if policy or behavior changed
