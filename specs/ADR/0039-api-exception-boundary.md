# ADR-0039: Global API Exception Boundary

## Status

Accepted

## Context

HTTP endpoints returned domain failures by catching `ApiException` locally and mapping codes to
`ProblemDetails` through resource-specific `*ErrorMapper` classes. That duplicated the same status
decisions across controllers and endpoint classes, with two catch styles and divergent default arms.

The failure mode was structural: a new endpoint could forget the catch block and leak a raw 500 for
an already typed domain error such as `intent.not_found`.

## Decision

HTTP transport handles `ApiException` through one ASP.NET Core `IExceptionHandler`. The handler uses
the existing `ApiProblems.Build` writer so every typed error response keeps `code` and custom
extensions in `ProblemDetails.Extensions`.

HTTP status selection lives in a single declarative registry from error code to status code. Unknown
`ApiException` codes still produce a structured 500 with the original code, so the client receives a
ProblemDetails payload instead of an unshaped exception response.

Endpoint/controller code must not catch `ApiException`. This is enforced by an architecture test that
scans production IL and excludes the MCP audit boundary, where `ApiException` is converted into MCP
tool errors rather than HTTP responses.

## Consequences

### Positive

- New HTTP endpoints inherit correct typed error handling without local boilerplate.
- Error-code status decisions have one source of truth.
- ProblemDetails payloads consistently include `code` and domain-provided extensions.
- The endpoint invariant is test-enforced instead of relying on code review memory.

### Negative / Risks

- Per-endpoint titles are no longer customized; clients should rely on `status`, `detail`, `code`,
  and extensions.
- Adding a new code with a non-500 HTTP meaning requires updating the registry.
