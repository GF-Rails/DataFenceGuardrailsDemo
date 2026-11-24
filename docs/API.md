# DataFence Guardrails - API Reference

DataFenceComponents Copyright (c) 2026 All rights reserved.

This document describes every public type, method and property a customer uses. See [README.md](README.md) for an overview and [ARCHITECTURE.md](ARCHITECTURE.md) for how the layers fit together. Target frameworks: .NET 8 and .NET 10.


**Read-only by design.** Guardrails lets AI read your SQL Server data safely. It allows a single read-only SELECT and rejects everything else (UPDATE, INSERT, DELETE, MERGE, EXEC, multiple statements). This is deliberate: AI should not change data with direct SQL. To let AI change data, expose each change as a custom tool or method and run it through DataFence AgentShield, which adds typed arguments, authorization, approvals and recovery.

You work with one entry point, `DataFenceGuardrails`, and one options object, `DataFenceGuardrailsOptions`. The seven layers (prompt, SQL, schema, parameters, database, result, audit) are configured through sections of that options object. Nothing else needs to be created or called.

## Conventions

- `Validate` and `Submit` each have an `Async` twin with the same behavior.
- Runtime checks never throw for rejected input. They fail closed and report the rejection in the result, with a stable rule code. The exceptions are setup mistakes (options, constructors) and the types noted in the "Throws" columns.
- A rule's severity only labels the reported event. It never changes the verdict.
- Rule codes are stable and never reused or renumbered.
- Instances are thread-safe once configured. Configure the options before sharing an instance.
- "Throws: None" means the method does not throw for any input.

## Contents

- [Core types](#core-types)
- [DataFenceGuardrails](#datafenceguardrails)
- [Rule codes](#rule-codes)

## Core types

Namespace `DataFenceComponents.Core`.

### Severity

| Value | Number | Meaning |
|---|---|---|
| `Info` | 0 | Informational only. |
| `Warning` | 1 | Suspicious input that deserves attention. |
| `Blocked` | 2 | Input rejected by a rule. |
| `Critical` | 3 | A strong attack signature that needs immediate attention. |

### DataFenceViolation

`public record DataFenceViolation(string Component, string Code, Severity Severity, string Message, string? Input, DateTime TimestampUtc)`

| Property | Type | Description |
|---|---|---|
| `Component` | `string` | Layer that raised the event, for example `PromptGuardrails`. |
| `Code` | `string` | Stable rule code, for example `LQF011`. |
| `Severity` | `Severity` | Severity after any customer override. |
| `Message` | `string` | Human-readable explanation. |
| `Input` | `string?` | The text that triggered the rule, or `null` when there was none. |
| `TimestampUtc` | `DateTime` | When the match was detected. |

### RuleSettings

Per-rule customer settings, available as `Rules` on every section. Rule codes are matched case-insensitively. Configure before use; the settings are not synchronized for concurrent changes.

| Method | Parameters | Return Type | Throws | Description |
|---|---|---|---|---|
| `SetSeverity` | `string code`, `Severity severity` | `RuleSettings` | `ArgumentException` if `code` is null, empty or whitespace | Overrides the severity reported for a rule. Returns the same instance for chaining. |
| `DisableRule` | `string code` | `RuleSettings` | `ArgumentException` if `code` is null, empty or whitespace | Turns the rule off. A disabled rule is not evaluated, except where the rule table below says it only silences the report (timeout and fail-closed rules). Returns the same instance. |
| `EnableRule` | `string code` | `RuleSettings` | `ArgumentException` if `code` is null, empty or whitespace | Turns a disabled rule back on. Returns the same instance. |
| `IsEnabled` | `string code` | `bool` | None | `true` unless the rule was disabled. |
| `GetSeverity` | `string code`, `Severity defaultSeverity` | `Severity` | None | The override for the rule, or `defaultSeverity` when there is none. |

```csharp
var options = new DataFenceGuardrailsOptions();
options.PromptGuardrails.Rules.SetSeverity("LQF011", Severity.Critical);
options.PromptGuardrails.OnViolation = v => Console.WriteLine($"{v.Code}: {v.Message}");
```

### DataFenceLicense

Static class holding the license state shared by every layer in the process.

| Member | Parameters | Return Type | Throws | Description |
|---|---|---|---|---|
| `TryActivate` | `string licenseKey`, `out string message` | `bool` | None | Activates the license key for this developer computer, so the key need not stay in source code. `true` when activated; `message` is the text to show the developer. Used mainly by the demo app. |
| `TryDeactivate` | `string licenseKey`, `out string message` | `bool` | None | Releases this computer's seat so the key can be used on another computer. `true` when released; `message` is the text to show the developer. Used mainly by the demo app. |
| `TryDeactivate` | `out string message` | `bool` | None | Releases this computer's seat without typing the key, using the license stored on this computer. `true` when released; `message` names the deactivated key in masked form (last four characters). When nothing is activated it returns `false` with the message "There is no active license on this computer to deactivate." The local license is deleted only after the service confirms. |
| `Tag` | `string message` | `string` | None | Appends the trial notice to the message when `TrialNotice` applies. |
| `TrialTag` (const) | none | `string` | None | `[DataFence trial version - license required for production use]`. |
| `NotLicensedMessage` (const) | none | `string` | None | Message reported when a request fails closed because there is no license. |
| `Status` | none | `LicenseStatus` | None | `Unlicensed`, `Trial` or `Licensed`. |
| `IsLicensed` | none | `bool` | None | `true` unless `Status` is `Unlicensed`. When unlicensed, every request fails closed with `DFP003`. |
| `TrialNotice` | none | `string?` | None | The trial tag from day 15 of the evaluation; otherwise `null`. |

### DataFenceInfo

Static class in namespace `DataFenceComponents.Core` that identifies the build in use. Every value is read from the assembly, so it always matches the DLL. When you contact support, send the text from `Report()`.

| Member | Parameters | Return Type | Throws | Description |
|---|---|---|---|---|
| `Version` | none | `string` | None | The product version, for example `1.0.0`. |
| `Product` | none | `string` | None | The product name, `DataFence Guardrails`. |
| `Company` | none | `string` | None | The publisher, `DataFence`. |
| `Copyright` | none | `string` | None | The copyright statement. |
| `Runtime` | none | `string` | None | The .NET runtime the application is running on. |
| `LicenseStatus` | none | `string` | None | `Unlicensed`, `Trial` or `Licensed`. |
| `Description` | none | `string` | None | One line such as `DataFence Guardrails 1.0.0 (.NET 8.0.x)`. |
license status and the version

`DataFenceGuardrails.Version` (instance property, `string`) returns the same value as `DataFenceInfo.Version`.

## DataFenceGuardrails

Namespace `DataFenceGuardrailsEngine`. One options object holds the settings of every layer; one call checks a request with the layers you turned on and, when it is approved, submits it to the database and returns masked rows. The pipeline order is prompt, SQL, schema, parameters, database, result masking, audit.

The license is activated once on each developer computer, in the demo's Manage License menu; it is not part of the options.

### DataFenceGuardrailsOptions

(no `SqlGuardrails.AllowedTables`, no `SchemaGuardrails` tables, no `AuditLogger.FilePath`)

| Property | Type | Default | Description |
|---|---|---|---|
| `Failures` | `FailureMode` | `First` | `First`: the first failing check stops the request. `All`: every check is performed and each failing layer reports its first violation (up to four before submission: prompt, SQL, schema, parameters). Nothing is submitted if any check failed. |
| `PromptGuardrails` | `PromptSection` | on | Prompt checks (LQF rules). |
| `SqlGuardrails` | `SqlSection` | on | SQL checks (SVG rules). |
| `SchemaGuardrails` | `SchemaSection` | on | Table allowlist (SCR rules). Columns are not checked. |
| `ParameterGuardrails`
| `DatabaseGuardrails` | `DatabaseSection` | on | Database access (QIP rules). |
| `ResultGuardrails` | `ResultSection` | on | Result masking (DRS rules). |
| `AuditLogger` | `AuditSection` | on | Audit log (SAL rules). |

| Method | Parameters | Return Type | Throws | Description |
|---|---|---|---|---|
| `EnableAll` | none | `void` | None | Turns every layer on. |
| `DisableAllGuardrails` | none | `void` | None | Turns every layer off. Then call `Enable()` on the layers you want. |

Every section (`PromptSection`, `SqlSection`, `SchemaSection`, `ParametersSection`, `DatabaseSection`, `ResultSection`, `AuditSection`) also has:

| Member | Type | Default | Description |
|---|---|---|---|
| `Enabled` | `bool` | `true` | Whether the layer is used. |
| `Enable()` | `void` | n/a | Sets `Enabled` to `true`. |
| `Disable()` | `void` | n/a | Sets `Enabled` to `false`; settings are kept but ignored. |

`FailureMode` values: `First`, `All`.

### Settings shared by every section

| Property | Type | Default | Description |
|---|---|---|---|
| `Rules` | `RuleSettings` | empty settings | Severity overrides and on/off switches for the rules of this section. Read-only property. |
| `OnViolation` | `Action<DataFenceViolation>?` | `null` | Called with the full violation whenever a rule of this section matches. Exceptions thrown by the callback are swallowed. |

### PromptSection

| Property / Method | Type / Parameters | Default | Description |
|---|---|---|---|
| `MaxInputLength` | `int?` | `10000` | Longest prompt accepted; `null` means no limit. Over-length prompts are rejected (`LQF007`). If you screen pasted documents or long articles, raise it or set it to `null`. |
| `NormalizeInput` | `bool` | `true` | Decodes and normalizes text before the rules run, so encoded attacks are caught. |
| `AllowFormattingCharacters` | `bool` | `true` | Accepts zero-width joiner/non-joiner, zero-width space, word joiner, byte-order mark and left-to-right/right-to-left marks used in many languages. When `false`, they are rejected with `LQF010`. |
| `DeobfuscateInput` | `bool` | `false` | Undoes leetspeak and spaced-out letters before matching. |
| `Classifier` | `Func<string, CancellationToken, Task<bool>>?` | `null` | Optional classifier; returns `true` for an attack. A `true` result, an exception or a timeout rejects the prompt (`LQF025`). |
| `ClassifierTimeout` | `TimeSpan` | 2 seconds | Longest time the classifier may take. |
| `BlockedKeywords` | `List<string>` | empty |
The code must not start with `LQF` (reserved for built-in rules) and must not already be used. A bad code or pattern throws `ArgumentException`.

### SqlSection

| Property | Type | Default | Description |
|---|---|---|---|
Comparison ignores case, brackets and quotes. Schema-qualified matching is strict: a table in any schema other than `dbo` must be written with its schema in both the query and this list, and a name without its schema or with a different schema is rejected (`SVG003`). Exception: `dbo` is optional, so `Orders` and `dbo.Orders` are the same table. Use the same names in `SchemaGuardrails` tables. |
| `RequiredColumn` | `string` | `"CustomerID"` | Column every query must filter on. |
| `MaxAllowedDistance` | `double` | `0.35` | Largest vector distance accepted by `EvaluateVectorDistance`. |

`AllowFormattingCharacters` (`bool`, default `true`) accepts zero-width joiner/non-joiner and left-to-right/right-to-left marks; when `false` they are rejected with `SVG010`.

SQL checks run only when `AllowedTables` is not empty. When `RequiredColumnValue` is set on the input, the WHERE clause must filter `RequiredColumn` with exactly that value (`SVG004`).

### SchemaSection

SchemaGuardrails allowlists object names only (tables, views, functions). It does not enforce columns: there is no column list, and a query may select any column of an allowed object. Protect columns in the database with views or table-valued functions, and allowlist those here. Stored procedures cannot be executed through DataFence: the full flow accepts only a plain SELECT and rejects `EXEC` (`SVG007`). Used on its own, SchemaGuardrails still checks procedure names it finds in the SQL against the allowlist.

| Method | Parameters | Return Type | Description |
|---|---|---|---|
| `AddTable` | `string table` | `SchemaSection` | Allows a table, view or function by name and returns the section. Names follow the same strict rule as `SqlGuardrails.AllowedTables`: schema required except for `dbo`, which is optional (`Orders` equals `dbo.Orders`). Any object that is not added is rejected (`SCR001`); system objects are always rejected (`SCR002`). **Columns are not checked and there is no column parameter**; enforce column-level access with database views or table-valued functions. Throws `ArgumentException` when the name is blank. |

### ParametersSection

| Property / Method | Type / Parameters | Default | Description |
|---|---|---|---|
| `DefaultMaxStringLength` | `int` | `1000` | Longest string accepted for variables without their own length. |
| `DefaultOverLength` | `OverLength` | `Reject` | What happens to over-length strings. |
| `AllowFormattingCharacters` | `bool` | `true` | Accepts zero-width joiner/non-joiner, zero-width space, word joiner, byte-order mark and left-to-right/right-to-left marks in string values. When `false`, they are rejected with `PSN009`. |
| `AllowControlCharacters` | `bool` | `false` | Accepts control characters other than tab, line feed and carriage return in string values. By default they are rejected with `PSN009`. |
| `Define` | `string name`, `SqlDbType type`, `int? maxLength = null`, `OverLength? overLength = null` | n/a | Defines a variable that will be accepted and returns the section. Variables passed to `Validate` that were not defined are rejected (`PSN001`). |

`OverLength` (namespace `ParameterGuardrailsEngine`) values: `Reject` (rejected, `PSN006`) and `Truncate` (cut to the limit and reported as `PSN004`; surrogate pairs are never split).

### DatabaseSection

| Property | Type | Default | Description |
|---|---|---|---|
| `Connection` | `DbConnection?` | `null` | Your connection. You create and dispose it. A request to submit with no connection fails with a `QIP` code. |
| `CommandTimeout` | `TimeSpan` | 30 seconds | Timeout forced on every command. Rounded up to whole seconds, minimum 1 second. |
| `MaxRows` | `int` | `1000` | Maximum rows per result. Further rows are dropped. Must be at least 1. |
| `DisableDatabaseAccess` | `bool` | `false` | When `true`, nothing reaches a database; requests fail with `QIP008`. |
| `MaxEstimatedQueryCost` | `double` | `0` (off) | Largest SQL Server estimated plan cost allowed. Needs the SHOWPLAN permission (otherwise `QIP013`). |
| `MaxQueriesPerMinute` | `int` | `0` (off) | Largest number of queries started per minute. |
| `FailureThreshold` | `int` | `0` (off) | Consecutive database failures that open the circuit breaker. |
| `BreakDuration` | `TimeSpan` | 30 seconds | How long the circuit stays open. Only used when `FailureThreshold` is above 0. |

Use a least-privilege, read-only login. Provider failures and command timeouts never escape as exceptions; they come back in the result with a `QIP` code.

### ResultSection

| Property | Type | Default | Description |
|---|---|---|---|
| `MaskText` | `string` | `"***"` | Text that replaces a masked value or match. |
| `RegexTimeout` | `TimeSpan` | 250 ms | Time allowed for one pattern on one cell. On timeout the whole cell is masked (fail closed, `DRS005`). |
| `SensitiveColumnNames` | `ISet<string>` | empty | Extra column-name fragments to always mask, added to the built-in list. Read-only property; add to the set. |

### AuditSection

| Property | Type | Default | Description |
|---|---|---|---|
| `FilePath` | `string` | empty | Path of the log file. An enabled section with no path writes nothing. |
| `EncryptionKey` | `byte[]?` | `null` | Optional 32-byte key. When set, every log line is encrypted; a key of any other length is rejected with `ArgumentException`. The library never stores the key: keep it outside the log. An encrypted log can only be checked with `VerifyAuditLog(path, key)`. |
| `RedactInput` | `bool` | `false` | Replaces the input text of logged entries with `[redacted]`, so prompts and SQL never reach the log. |

Each entry carries the hash of the previous entry, so changing or removing a line in the middle is detected by `VerifyAuditLog`. Someone with file access can still truncate the end or replace the whole file.

Entry fields: `Seq`, `TimeUtc`, `CorrelationId`, `Component`, `Code`, `Severity`, `Message`, `Input`, `PrevHash`, `Hash`. An approved request is logged with code `DFP000`. Encryption is optional and off by default; the library never stores the key. With a key, the file is not readable in an editor and can only be checked with `VerifyAuditLog(path, key)`. `RedactInput` is independent of encryption. See the README section "Reading the audit log" for PowerShell and C# examples.

### DataFenceInput class

`public sealed class DataFenceInput` holds the values of one request. Options hold shared settings; the input holds per-request values. Create a new input for each request and set only the properties your turned-on guardrails use. A property that no turned-on guardrail uses is rejected (`DFP005`).

| Property | Type | Default | Used by |
|---|---|---|---|
| `Prompt` | `string?` | `null` | PromptGuardrails |
| `Sql` | `string` | empty | SqlGuardrails, SchemaGuardrails, ParameterGuardrails, DatabaseGuardrails |
| `Variables` | `Dictionary<string, object>?` | `null` | ParameterGuardrails |
| `RequiredColumnValue` | `string?` | `null` | SqlGuardrails (the WHERE clause must filter the required column with exactly this value) |
| `CorrelationId` | `string?` | `null` | AuditLogger |

Creating and reusing inputs and results:

- A new `DataFenceInput` starts empty, so nothing carries over. Create a new input for each request when requests set different properties (for example `Variables` on one and `RequiredColumnValue` on another).
- You may reuse one input for several checks when every check sets the same properties, for example only `Prompt`, or only `Sql`. Each check overwrites the property, so nothing is left over. If you reuse an input and a property from an earlier check is still set, it is checked again, or rejected (`DFP005`) when no turned-on guardrail uses it. Set it to `null` (`Sql` to an empty string) before the next check.
- `Validate` and `Submit` copy or consume what they need, so changing an input after `Validate` returns does not affect a result you already hold.
- Do not use one `DataFenceInput` from several threads at the same time. Use one input per request.
- `Validate` and `Submit` create and return a new `DataFenceResult` each time. You never create one yourself. Declare a variable (`DataFenceResult result;`) and assign each returned result to it. `result = guardrails.Submit(result);` replaces the approved result with the submitted one.

### DataFenceGuardrails class

`public sealed class DataFenceGuardrails : IDataFenceGuardrails`

| Method | Parameters | Return Type | Throws | Description |
|---|---|---|---|---|
| `DataFenceGuardrails` (constructor) | `DataFenceGuardrailsOptions options` | n/a | `ArgumentNullException` if `options` is null | Creates each layer from the sections that are enabled. |
Blank SQL is accepted: the SQL and schema checks are skipped and `Submit` runs nothing.
| `ValidateAsync` | `DataFenceInput input`, `CancellationToken cancellationToken = default` | `Task<DataFenceResult>` | `ArgumentNullException`; `OperationCanceledException` when cancelled | Async twin of `Validate`. Does not block a thread while it waits for the audit log. |
| `Submit` | `DataFenceResult validated` | `DataFenceResult` | `ArgumentNullException` if `validated` is null; none for refused input | Takes a result approved by `Validate` and runs it: executes the query, masks the result and writes the audit entry. The only call that touches the database. A result that was not approved, was already submitted, or came from another instance is refused (`DFP004`). |
| `SubmitAsync` | `DataFenceResult validated`, `CancellationToken cancellationToken = default` | `Task<DataFenceResult>` | `ArgumentNullException`; `OperationCanceledException` when cancelled | Async twin of `Submit`. |
| `EvaluateVectorDistance` | `string textPayload`, `double computedDistance`, `out string finalSanitizedText` | `bool` | `InvalidOperationException` if the `SqlGuardrails` section is disabled | Accepts a vector search match only when `computedDistance` is within `SqlGuardrails.MaxAllowedDistance`. Returns `true` with the text unchanged; otherwise returns `false`, sets a safe fallback message and reports `SVG006`. |
| `VerifyAuditLog` (static) | `string filePath`, `byte[]? encryptionKey = null` | `bool` | None | Checks the hash chain of an audit log. `true` when every line is intact and in order; `false` when tampered with, unreadable, missing or the path is invalid. |
| `Version` (property) | none | `string` | None | Same value as `DataFenceInfo.Version`. |

Behavior:

- `Validate` checks only the values set in the input. Leaving `Prompt` as `null` skips the prompt check.
- `Variables` supplied while the `ParameterGuardrails` section is disabled are rejected (`DFP005`).
- A refusal from the database layer or a database failure while running the query is returned with its `QIP` code. The query was submitted, so `Submitted` is true and `Approved` is false. Any other failure is returned as `DFP001`.
- If an audit entry cannot be written, the result is withheld (`DFP001`).
- When `AuditLogger` is enabled and has a `FilePath`, every result is logged: each violation, or `DFP000` for an approved request.

### AddDataFence

| Method | Parameters | Return Type | Throws | Description |
|---|---|---|---|---|
| `AddDataFence` (extension on `IServiceCollection`) | `Action<DataFenceGuardrailsOptions> configure` | `IServiceCollection` | `ArgumentNullException` if `services` or `configure` is null | Registers `DataFenceGuardrails` and `IDataFenceGuardrails` as singletons. |

### DataFenceResult

`public sealed record DataFenceResult(bool Approved, bool Submitted, DataTable? Data, IReadOnlyList<DataFenceViolation> Violations)`

| Property | Type | Description |
|---|---|---|
| `Approved` | `bool` | `true` when every check passed. It does not mean the query was sent. |
| `Submitted` | `bool` | `true` when the query was sent to the database. Sent does not mean succeeded. |
| `Data` | `DataTable?` | The masked rows (raw rows if result masking is off); `null` when nothing was submitted or the request failed. |
| `Violations` | `IReadOnlyList<DataFenceViolation>` | Every failure: one in `First` mode, up to one per layer in `All` mode. Empty when approved. |
| `Violation` | `DataFenceViolation?` | The first failure, or `null` when approved. `Component` names the layer. |
| `Notice` | `string?` | Trial notice text from day 15 of the evaluation; otherwise `null`. |

| Approved | Submitted | Meaning |
|---|---|---|
| false | false | Blocked by a check |
| true | false | Checks passed, not sent (result of `Validate`) |
| true | true | Checks passed and sent |
| false | true | Sent, then the database or result masking failed |

### Example

```csharp
// Activate your key once in the demo's Manage License menu.

var options = new DataFenceGuardrailsOptions();
options.SqlGuardrails.AllowedTables.Add("dbo.Orders");
options.SqlGuardrails.RequiredColumn = "CustomerID";
options.ParameterGuardrails.Define("id", SqlDbType.Int);
options.DatabaseGuardrails.Connection = connection; // use a least-privilege, read-only login
// Every layer is on by default. To turn one off: options.AuditLogger.Disable();

var guardrails = new DataFenceGuardrails(options);

DataFenceResult result = guardrails.Validate(new DataFenceInput
{
	Prompt = userPrompt,
	Sql = "SELECT Id, Total FROM Orders WHERE CustomerID = @id",
	Variables = new() { ["id"] = 42 }
});

if (result.Approved)
	result = guardrails.Submit(result); // the only call that touches the database

if (!result.Approved)
	foreach (var v in result.Violations)
		Console.WriteLine($"{v.Component} {v.Code}: {v.Message}");
```

## Rule codes

Each table lists the default severity. Override a severity or turn a rule off through the `Rules` property of the matching section (`options.SqlGuardrails.Rules.SetSeverity("SVG007", Severity.Critical)`).

### DFP - pipeline (no section; reported by `DataFenceGuardrails`)

| Code | Default severity | Triggered when | What to do |
|---|---|---|---|
| `DFP000` | Info | The request was approved. Written to the audit log only. | None. |
| `DFP001` | Critical | A stage or the audit write failed unexpectedly; blocked. | Investigate. |
| `DFP002` | Blocked | Variables without the `ParameterGuardrails` section. | Fix the call. |
| `DFP003` | Blocked | No active license or evaluation. | Activate your license in the demo's Manage License menu. |
| `DFP004` | Blocked | `Submit` was given a result that `Validate` did not approve, one already submitted, or one from another instance; or the `DatabaseGuardrails` section is off. | Call `Validate` first and submit its approved result once. |
| `DFP005` | Blocked | A value was set in `DataFenceInput` that no turned-on guardrail uses, so it would not have been checked. | Remove the value, or turn on the guardrail that uses it. |

### LQF - Prompt (`PromptGuardrails` section)

| Code | Default severity | Triggered when | What to do |
|---|---|---|---|
| `LQF005` | Blocked | The input is `null`. | Fix the caller. |
| `LQF006` | Critical | A rule timed out (100 ms limit) or failed unexpectedly, so the input is rejected (fails closed). | Treat as hostile. Its severity can be overridden. Disabling it only silences the report; the input is still rejected. |
| `LQF007` | Blocked | Input is longer than `MaxInputLength`. | Shorten the input or raise the limit. If you screen pasted documents or long articles, raise `MaxInputLength` or set it to `null`. |
| `LQF008` | Blocked | A `BlockedKeywords` entry was found. | Reject. |
| `LQF009` | Blocked | No active license or evaluation. | Activate your license in the demo's Manage License menu. |
| `LQF010` | Blocked | The prompt contains a control character, lone surrogate, bidirectional override or line or paragraph separator (and formatting characters when `AllowFormattingCharacters` is `false`). Tab, line feed and carriage return are always allowed. | Reject. |
| `LQF011` | Blocked | Instruction override, for example "ignore all previous instructions". | Reject. |
| `LQF012` | Blocked | Request to reveal the system prompt or hidden instructions. | Reject. |
| `LQF013` | Blocked | Role or persona switch, for example "you are now in developer mode". | Reject. |
| `LQF014` | Blocked | Request to bypass or disable safeguards or safety filters. | Reject. |
| `LQF017` | Blocked | Fake role or delimiter markers such as a `system:` line, `[INST]`, `</system>` or `<|im_start|>`. | Reject. |
| `LQF018` | Blocked | A Base64 or hex token, or ROT13 text, that decodes to one of the attacks above (URL encoding is decoded up to three times first). | Reject. |
| `LQF019` | Warning | Letters from mixed scripts (look-alike characters). Off by default; enable with `EnableRule`. | Enable only if your users write in one script. |
| `LQF020` | Warning | Excessive repetition or padding. Off by default; enable with `EnableRule`. | Enable to block padding attacks. |
| `LQF022` | Blocked | Secret or key in the prompt. | Remove the secret. |
| `LQF023` | Blocked | Spanish, French or German instruction override. Multilingual coverage is limited: for other languages, translate the input to English first and check the translated text, or use the Classifier hook. | Reword the prompt. |
| `LQF024` | Warning | Role-play or hypothetical framing. Off by default; enable with `EnableRule`. | Enable to flag evasion framing. |
| `LQF025` | Blocked | `Classifier` flagged the prompt, threw or exceeded `ClassifierTimeout` (fails closed). | Optional hook. |

`ClassifierTimeout` (TimeSpan, default 2 seconds). The classifier hook applies to prompts only; no other layer has one.


### Adding your own classifier (optional)

PromptGuardrails is a fast, deterministic first layer. For broader coverage (reworded attacks, other languages) you can add a classifier: a function you supply that returns true when the text is an attack. It runs in addition to the pattern rules. If it throws or takes longer than ClassifierTimeout, the prompt is rejected with LQF025. Allow for slow machines when you set ClassifierTimeout. No classifier catches every attack.

```csharp
// MyModel is your own
// This is an illustration only; DataFenceComponents does not ship a model.
options.PromptGuardrails.Classifier = async (text, token) => await MyModel.IsAttackAsync(text, token);
options.PromptGuardrails.ClassifierTimeout = TimeSpan.FromSeconds(2);
```

Notes:
- The classifier runs in addition to the pattern rules, never instead of them. The classifier adds to the built-in rules and does not replace them. Keep the rules on, and disable an individual rule only if it causes false positives for your users.
- Return true for an attack and false for safe. A true result, an exception or a timeout rejects the prompt with LQF025.
- The token passed to your function is cancelled when ClassifierTimeout expires, so pass it to your model or service call.
- The function runs on every request and requests can run concurrently, so it must be thread-safe. Create the model session once and reuse it.

Models add download size, latency and their own false positives, so measure one on your own prompts before relying on it.


Customer rule codes (from `AddCustomRule`) may not start with `LQF`. LQF001-LQF004 are retired (SQL syntax is checked by the Sql section) and never reused; LQF015, LQF016 and LQF021 are reserved. Prompt rules are pattern-based and do not catch every reworded attack. Disabling `LQF005` makes a `null` input count as safe; disabling `LQF007` removes the length check.



### Example: connecting an open-source classifier (ONNX)

This example connects a published open-source prompt-injection model through the `Classifier` hook. We tested it with ProtectAI's `deberta-v3-small-prompt-injection-v2` and `deberta-v3-base-prompt-injection-v2` (Hugging Face; both list the Apache-2.0 license, and Hugging Face may ask you to accept the model terms before download). DataFenceComponents does not ship, host or support these models; check each model's license and terms yourself.

1. Download the ONNX model, `spm.model` and the tokenizer files into a folder.
2. Add the NuGet packages `Microsoft.ML.OnnxRuntime` and `Microsoft.ML.Tokenizers`.
3. Create the classifier once at startup and reuse it for every request.

```csharp
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

sealed class OnnxClassifier : IDisposable
{
    private readonly InferenceSession _session;
    private readonly SentencePieceTokenizer _tokenizer;
    private readonly string[] _inputs;

    public OnnxClassifier(string dir)
    {
        _session = new InferenceSession(Path.Combine(dir, "model.onnx"));
        using FileStream f = File.OpenRead(Path.Combine(dir, "spm.model"));
        _tokenizer = SentencePieceTokenizer.Create(f, false, false);
        _inputs = _session.InputMetadata.Keys.ToArray();
    }

    public long[] Tokenize(string text)
    {
        var ids = new List<long> { 1 };
        foreach (int id in _tokenizer.EncodeToIds(text)) { if (ids.Count >= 511) break; ids.Add(id); }
        ids.Add(2);
        return ids.ToArray();
    }

    public float[] Logits(string text)
    {
        long[] ids = Tokenize(text);
        var mask = new long[ids.Length]; Array.Fill(mask, 1L);
        var feeds = new List<NamedOnnxValue>();
        foreach (string name in _inputs)
        {
            long[] data = name == "input_ids" ? ids : mask;
            feeds.Add(NamedOnnxValue.CreateFromTensor(name, new DenseTensor<long>(data, new[] { 1, data.Length })));
        }
        using var results = _session.Run(feeds);
        return results.First().AsEnumerable<float>().ToArray();
    }

    public double Score(string text)
    {
        float[] lg = Logits(text);
        double max = lg.Max();
        double[] e = lg.Select(v => Math.Exp(v - max)).ToArray();
        return e[1] / e.Sum();
    }

    public Task<bool> IsAttack(string text, CancellationToken ct, double threshold = 0.5)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Score(text) >= threshold);
    }

    public void Dispose() => _session.Dispose();
}

// At startup:
var clf = new OnnxClassifier(@"C:\models\deberta-v3-small-prompt-injection-v2");
options.PromptGuardrails.Classifier = (text, token) => clf.IsAttack(text, token);
options.PromptGuardrails.ClassifierTimeout = TimeSpan.FromSeconds(5);
```

Notes:
- The first call is slow while the model warms up. Set `ClassifierTimeout` high enough for your hardware; a timeout rejects the prompt (fails closed).
- The `base` model is about 739 MB on disk. The `small` model is faster.
- The threshold (0.5 above) is yours to tune. Lower catches more attacks and flags more safe prompts.

#### What we measured

We ran each model through the hook above on 141 jailbreak prompts and 259 benign prompts, plus 1,000 benign instructions from the Dolly dataset, with a score threshold of 0.5. Each row compares the built-in pattern rules alone with the pattern rules plus that model (how the hook runs).

| Model | Attacks blocked, patterns alone (of 141) | Attacks blocked, patterns + model (of 141) | Benign blocked (of 259) | Dolly benign blocked (of 1,000) | Classifier latency, mean / 95th percentile |
|---|---|---|---|---|---|
| `deberta-v3-small-prompt-injection-v2` | 40 | 115 | 3 | 1 | 132 ms / 943 ms |
| `deberta-v3-base-prompt-injection-v2` | 44 | 121 | 7 | 0 | 322 ms / 2,261 ms |

These are results on our own test data on one development machine, not guarantees. The C# tokenizer matched the Python reference (identical token ids and scores on 8 of 8 sample prompts).

#### Why not 100%

No classifier catches every attack, and none avoids every false alarm:
- A classifier returns a probability, not a certainty. The threshold (0.5 here) trades one error for the other: lowering it catches more attacks but blocks more safe prompts; raising it does the reverse.
- A model only knows what it was trained on. Reworded, novel or unusual attacks can look safe to it, and some safe prompts (for example, ones that quote or discuss attacks) can look like attacks.
- The larger model caught more attacks but also blocked more safe prompts and was slower, so a bigger model is not simply better.
- The built-in pattern rules and the classifier miss different attacks, which is why they run together. The later layers (SQL, schema, parameters, database, result) exist to catch what gets past the prompt layer.

Neither model catches every attack, and both can block some safe prompts, so measure on your own prompts and hardware before relying on a model. A classifier that throws, or exceeds `ClassifierTimeout`, rejects the prompt with `LQF025` (fails closed).


### SVG - SQL (`SqlGuardrails` section)

| Code | Default severity | Triggered when | What to do |
|---|---|---|---|
| `SVG001` | Blocked | The SQL is not valid T-SQL. | Reject; ask the model to regenerate. |
| `SVG002` | Critical | `DROP` or `DELETE` found, including in stacked statements. | Reject and investigate. |
| `SVG003` | Blocked | A table is not in `allowedTables`. | Reject. |
| `SVG004` | Blocked | The required column is missing from the filter, or does not equal the required value. | Reject. This is the required-column check. |
| `SVG005` | Blocked | An always-true condition such as `1 = 1`. | Reject. |
| `SVG006` | Blocked | Reported by `EvaluateVectorDistance` when the distance is over `MaxAllowedDistance`. | Use the fallback text. |
| `SVG007` | Blocked | Not a plain SELECT (`UPDATE`, `INSERT`, `MERGE`, `ALTER`, `TRUNCATE`, `CREATE`, `EXEC`, `SELECT ... INTO`, multiple statements, or no statement). | Reject. |
| `SVG008` | Blocked | No active license or evaluation. | Activate your license in the demo's Manage License menu. |
| `SVG009` | Blocked | The check failed unexpectedly (for example a `null` input). | Reject; fix the caller. |
| `SVG010` | Blocked | The SQL contains a control character, lone surrogate, bidirectional override or line or paragraph separator (checked before parsing). | Reject. |


### SCR - Schema (`SchemaGuardrails` section)

| Code | Default severity | Triggered when | What to do |
|---|---|---|---|
| `SCR001` | Blocked | A table is not in the allowlist. | Reject; add the table only if it is intended. |
| `SCR002` | Critical | A `sys`/`INFORMATION_SCHEMA` object, or an `xp_`/`sp_`/`fn_`/legacy `sys*` name that is not listed. | Reject and investigate. |
| `SCR003` | Blocked | A table name was null, empty or whitespace. | Fix the caller. |
| `SCR004` | Blocked | The SQL could not be parsed. Always rejects. | Reject. |
| `SCR005` | Blocked | The check failed unexpectedly (for example a `null` input). | Reject; fix the caller. |
| `SCR006` | Blocked | No active license or evaluation. | Activate your license in the demo's Manage License menu. |

Disabling a rule only silences its report; the input is still rejected. Callback exceptions from `OnViolation` are swallowed and never change the verdict.


### PSN - Parameters (`ParameterGuardrails` section)

| Code | Default severity | Triggered when | What to do |
|---|---|---|---|
| `PSN001` | Blocked | A variable has no definition. | Reject; define it only if intended. |
| `PSN002` | Blocked | A value cannot be cast to its defined type. | Reject. |
| `PSN003` | Critical | A variable name is not a valid identifier. | Reject and investigate. |
| `PSN004` | Warning | A string was truncated (`OverLength.Truncate`). Reported only. | Informational. |
| `PSN005` | Blocked | The dictionary was `null`. | Fix the caller. |
| `PSN006` | Blocked | A string exceeded its maximum length and over-length is rejected. | Reject or shorten. |
| `PSN007` | Blocked | The check failed unexpectedly. | Reject. |
| `PSN008` | Blocked | No active license or evaluation. | Activate your license in the demo's Manage License menu. |
| `PSN009` | Blocked | A string value contains a control character, lone surrogate, bidirectional override or line or paragraph separator. | Reject, or set `AllowControlCharacters` for free text. |

Disabling a rule only silences its report; the variables are still rejected. Callback exceptions are swallowed.


### QIP - Database (`DatabaseGuardrails` section)

| Code | Default severity | Triggered when | What to do |
|---|---|---|---|
| `QIP001` | Blocked | Not a plain SELECT, `SELECT INTO`, or no statement. | Reject. |
| `QIP002` | Blocked | More than one statement. | Reject. |
| `QIP003` | Blocked | The SQL could not be parsed. | Reject. |
| `QIP004` | Blocked | `ExecuteNonQuery` or `ExecuteScalar` called on a wrapped command. | Use a reader. |
| `QIP005` | Warning | The result was cut at `MaxRows`. Reported only; no exception. | Informational. |
| `QIP006` | Blocked | `Validate` failed unexpectedly (for example a `null` input). | Reject. |
| `QIP007` | Blocked | No active license or evaluation. | Activate your license in the demo's Manage License menu. |
| `QIP008` | Blocked | `DisableDatabaseAccess` is `true`. | Enable access or do not call. |
| `QIP009` | Blocked | No connection was supplied and `Connection` is not set. | Set `Connection` or pass a connection. |
| `QIP010` | Blocked | More than `MaxQueriesPerMinute` queries were started within a minute. | Slow down or raise the limit. |
| `QIP011` | Blocked | The circuit breaker is open after `FailureThreshold` consecutive database failures. | Wait for `BreakDuration`. |
| `QIP012` | Blocked | The estimated plan cost is above `MaxEstimatedQueryCost`. | Simplify the query or raise the cap. |
| `QIP013` | Blocked | The login lacks the SHOWPLAN permission (SQL Server error 262), so the cost cannot be read. | Grant SHOWPLAN or set `MaxEstimatedQueryCost` to 0. |
| `QIP014` | Blocked | The database failed the command (for example an invalid object name or a lost connection). The message carries the database error and the original exception is the inner exception. | Read the message and `InnerException`, fix the SQL or the database. |
| `QIP015` | Blocked | The command ran longer than `CommandTimeout` and was stopped. The original exception is the inner exception. | Simplify the query or raise `CommandTimeout`. |

Disabling a rule only silences its report; the command is still refused. Callback exceptions are swallowed.


### DRS - Result (`ResultGuardrails` section)

| Code | Default severity | Triggered when | What to do |
|---|---|---|---|
| `DRS001` | Critical | A Luhn-valid card number was masked. | Review why the query returns it. |
| `DRS002` | Critical | A US social security number was masked. | Review the query. |
| `DRS003` | Critical | An API key, token or private key was masked. | Review the query. |
| `DRS004` | Warning | A column with a sensitive name was masked. | Informational. |
| `DRS005` | Critical | A pattern timed out, so the whole cell was masked. | Investigate the data. |
| `DRS006` | Critical | The scan failed unexpectedly; no data is returned. | Treat as an error. |
| `DRS007` | Blocked | No active license or evaluation; no data is returned. | Activate your license in the demo's Manage License menu. |

Disabling a detector rule (`DRS001` to `DRS003`) turns that detector off, so those values are no longer masked. Disabling `DRS004` stops masking by column name. Disabling `DRS005`, `DRS006` or `DRS007` only silences the report; the cell is still masked or the data is still withheld. Callback exceptions are swallowed.


### SAL - Audit (`AuditLogger` section)

| Code | Default severity | Triggered when | What to do |
|---|---|---|---|
| `SAL001` | Critical | An entry could not be written. The error is rethrown. | Fix the path, permissions or disk. |
| `SAL002` | Critical | The existing log could not be read to continue its chain; logging refuses to start. | Use the right key, or start a new file. |
| `SAL003` | Blocked | No active license or evaluation. Thrown as `InvalidOperationException` with the code in the message. | Activate your license in the demo's Manage License menu. |









**Changing keys.** When you receive a new key (for example at renewal), the recommended steps are to deactivate the old license (Deactivate needs no key; it uses the license stored on this computer and shows the masked key it released), then activate the new one. If you activate the new key directly, the old key on this computer is replaced automatically; a mistyped new key leaves the old activation unchanged. Deactivate is mainly for moving a license to another computer. If the service cannot release the seat, the local license is still removed and the seat can be released from the console. The Manage License menu shows the active product and masked key (for example "...A1B2").
