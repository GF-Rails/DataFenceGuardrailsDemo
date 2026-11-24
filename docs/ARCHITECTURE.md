# DataFence Guardrails - Architecture

DataFenceComponents Copyright (c) 2026 All rights reserved.

This document describes how the components fit together and where each trust boundary sits. See [API.md](API.md) for the public API and [README.md](README.md) for an overview.

## Contents

- [Purpose](#purpose)
- [Pipeline flow](#pipeline-flow)
- [Defense in depth](#defense-in-depth)
- [Layer boundaries](#layer-boundaries)
- [Database boundary](#database-boundary)
- [Fail-closed behavior](#fail-closed-behavior)
- [Limits and recommendations](#limits-and-recommendations)

## Purpose

DataFence Guardrails is a Defense-in-Depth pipeline of seven layers that enforces zero-trust boundaries
between raw AI output and SQL Server. Every request passes through the layers you turn on, and the first rejection stops it.

**Read-only by design.** Guardrails lets AI read your SQL Server data safely. It allows a single read-only SELECT and rejects everything else (UPDATE, INSERT, DELETE, MERGE, EXEC, multiple statements). This is deliberate: AI should not change data with direct SQL. To let AI change data, expose each change as a custom tool or method and run it through DataFence AgentShield, which adds typed arguments, authorization, approvals and recovery.


## Pipeline flow

In this document a "pipeline" means the fixed sequence of stages that one request passes through, each stage being one component. The first stage that rejects the request stops it; nothing after that stage runs.

```
 prompt
   |
   v
 PromptGuardrails      pattern checks on the raw prompt
   |
   v
 SqlGuardrails         syntax, statement type, tables, required column, tautologies
   |
   v
 SchemaGuardrails      table allowlist (columns are not checked), system objects
   |
   v
 ParameterGuardrails   typed, length-limited parameters
   |
   v
 DatabaseGuardrails    timeout, row cap, SELECT only, execution
   |
   v
 ResultGuardrails      masks cards, SSNs, secrets, sensitive columns
   |
   v
 AuditLogger           tamper-evident log of every event
```

`DataFenceGuardrails` runs these stages in this order for you. `Validate` runs prompt, SQL, schema and parameters and never touches the database; `Submit` then runs database, result masking and audit. Layers you turn off are skipped. With `Failures = All`, every pre-submission check runs and each failing layer reports its first violation; nothing is submitted if any failed. `DataFenceGuardrails` is the only public entry point; the individual layers are internal and cannot be called on their own.

## Defense in depth

No single layer is trusted to stop everything. Each layer assumes the one before it may have been bypassed.

| Layer | What it checks |
|---|---|
| PromptGuardrails | Prompt injection patterns in the incoming text. |
| SqlGuardrails | The generated SQL: it must parse, be a single plain SELECT, use only allowed tables, include the required column, and contain no tautologies. |
| SchemaGuardrails | Every table the SQL touches against a fixed allowlist; system objects are always rejected. |
| ParameterGuardrails | Untrusted values become typed parameters so user text never reaches the SQL string. |
| DatabaseGuardrails | A forced command timeout, a row cap, and one plain SELECT per command at execution time. |
| ResultGuardrails | Masks sensitive values in the returned data before it leaves your application. |
| AuditLogger | Records events in an append-only log with a hash chain, so tampering is detectable. |

## Layer boundaries

- Layers never reference each other and are not called directly. You work with `DataFenceGuardrails`, `DataFenceGuardrailsOptions` and the input, result and violation types.
- Every guardrail reports a rejection as exactly one `DataFenceViolation` with a stable code. Codes are never reused or renumbered.
- All nine DLLs share one version, company (DataFence) and product (DataFence Guardrails). `DataFenceInfo.Report()` loads each DLL, lists its version and flags a `MISMATCH` or a `NOT FOUND` DLL, so one text identifies exactly what is installed.
- Any rule can have its severity overridden or be disabled through the `Rules` property of each section of `DataFenceGuardrailsOptions`. Severity only labels the event and does not change the verdict.

## Database boundary

The database layer is the only layer that can reach a database. Every other layer inspects text or in-memory data:

- `SqlGuardrails` and `SchemaGuardrails` parse SQL in memory.
- `ParameterGuardrails` only builds parameter objects.
- `AuditLogger` writes a file only.
- `DataFenceGuardrails` reaches a database only through the database layer.

To guarantee that no database is reached, set `options.DatabaseGuardrails.DisableDatabaseAccess = true`; every entry point then fails with `QIP008`. Alternatively, call only `Validate` and run the checked SQL yourself.

## Fail-closed behavior

- An unexpected failure in a check is rejected with the component's internal-error code rather than allowed.
- A rule that times out is rejected. For `PromptGuardrails`, this is reported as a Critical `LQF006` event; disabling it only silences the report, because input that could not be evaluated is never treated as safe.
- `ResultGuardrails` returns no data when a scan fails, and masks the whole cell when a pattern times out.
- `DataFenceGuardrails` withholds the result if an audit entry cannot be written.
- Hostile characters are rejected before any other check: control characters (except tab, line feed and carriage return), lone surrogates, bidirectional overrides and line or paragraph separators (`LQF010`, `SVG010`, `PSN009`). They can hide an attack from a reader or a filter. Zero-width spaces, joiners, word joiners, byte-order marks and left-to-right/right-to-left marks are common in pasted web text and many languages in many languages, so they pass unless `AllowFormattingCharacters` is `false`; parameter values may also allow control characters with `AllowControlCharacters`.
- Runtime checks return blocked results rather than throwing, except for the deliberate exceptions listed in [API.md](API.md).

## Limits and recommendations

- `PromptGuardrails` is pattern-based, so reworded attacks can pass. The later layers exist to catch them. An optional `Classifier` hook lets you add your own model.
  No model is included.
- Use a least-privilege database login so that the database itself limits what any query can do.
- `AuditLogger` is tamper-evident, not immutable: someone with file access can still truncate the end or replace the whole file. Encryption (optional, key supplied by you and never stored by the library) is independent of `RedactInput`.
- An encrypted log cannot be read in an editor and can only be checked with `VerifyAuditLog(path, key)`. Keep the key outside the log; a lost key means the file can no longer be verified or continued.
- Instances are thread-safe once configured; configure options before sharing an instance.

There is one Validate method. It takes a DataFenceInput that holds the values of one request (Prompt, Sql, Variables, RequiredColumnValue, CorrelationId). Options hold the shared settings and the input holds the per-request values, so no call needs null or named arguments. A value that no turned-on layer uses would go unchecked, so it is rejected (DFP005).

