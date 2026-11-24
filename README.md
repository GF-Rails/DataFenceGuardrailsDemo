# DataFence Guardrails

A commercial Defense-in-Depth pipeline of seven layers for AI-to-SQL applications, enforcing zero-trust boundaries
between raw AI output and SQL Server.

**Read-only by design.** Guardrails lets AI read your SQL Server data safely. It allows a single read-only SELECT and rejects everything else (UPDATE, INSERT, DELETE, MERGE, EXEC, multiple statements). This is deliberate: AI should not change data with direct SQL. To let AI change data, expose each change as a custom tool or method and run it through DataFence AgentShield, which adds typed arguments, authorization, approvals and recovery.


| Layer | Section of the options | Checks | Touches a database? |
|---|---|---|---|
| Prompt | `PromptGuardrails` |
| SQL | `SqlGuardrails` |
| Schema | `SchemaGuardrails` |
| Parameters | `ParameterGuardrails` |
| Database | `DatabaseGuardrails` |
| Result | `ResultGuardrails` |
| Audit | `AuditLogger` |

Every layer is on by default. Switch a layer off with `options.AuditLogger.Disable()` and back on with `Enable()`, or use `options.DisableAllGuardrails()` then `Enable()` on the layers you want (`EnableAll()` turns everything on). A disabled layer is skipped and its settings are kept but ignored, without error. An enabled layer with nothing configured passes everything through.

## Which layer touches a database

**Only the Database layer can reach a database.** The other layers inspect text or in-memory data.

- You create the connection and assign it to `options.DatabaseGuardrails.Connection`. You also dispose it; it is never disposed for you.
- To guarantee no database access, set `options.DatabaseGuardrails.DisableDatabaseAccess = true` (default `false`); requests then fail with `QIP008`.
- `Validate` runs the checks that need no database (prompt, SQL, schema, parameters) in that order and never touches the database. `Submit` is the only call that does, and only with an approved `Validate` result.
- Use a least-privilege, read-only database login.

## Getting started

```csharp
// To license, see menu 11 (Manage License) in the demo.
// Without a license the 30-day trial runs.

var options = new DataFenceGuardrailsOptions();
options.SqlGuardrails.AllowedTables.Add("SalesLT.SalesOrderHeader");
options.SqlGuardrails.RequiredColumn = "CustomerID";   // every query must filter on this column (row isolation)
options.SchemaGuardrails.AddTable("SalesLT.SalesOrderHeader");   // objects only, columns are not checked
options.ParameterGuardrails.Define("id", SqlDbType.Int);
options.DatabaseGuardrails.Connection

var guardrails = new DataFenceGuardrails(options);

var input = new DataFenceInput();
input.Sql = "SELECT SalesOrderID, TotalDue FROM SalesLT.SalesOrderHeader WHERE CustomerID = @id";
input.Variables = new Dictionary<string, object>();
input.Variables.Add("id", 42);
input.Prompt = "show my orders";   // the user's request; set only what your turned-on guardrails use

DataFenceResult result = guardrails.Validate(input);

if (result.Approved)
	result = guardrails.Submit(result);   // sends the query, returns masked rows

if (!result.Approved)
	foreach (var v in result.Violations)
		Console.WriteLine($"Blocked [{v.Code}] {v.Message}");
else
	Show(result.Data);   // masked rows
```

The input holds the values of one request, and `Validate` and `Submit` return a new result each time, so you only declare a `DataFenceResult` variable and never create one. A new `DataFenceInput` starts empty. Create a new input per request when requests set different properties. You can reuse one input when every check sets the same properties (for example only `Prompt`), because each check overwrites it. A property left over from an earlier check is checked again, or rejected (`DFP005`) if no turned-on layer uses it. Do not share one input between threads.

Every rejection is a `DataFenceViolation` (component, stable `Code`, `Severity`, `Message`, `Input`, `TimestampUtc`)

## Common features (all layers)

- Rule severity can be overridden by code and any rule can be disabled through the `Rules` property of each section. Severity only labels the event; a disabled rule only silences its report where the rule table says so.
- Fail closed: unexpected failures are rejected with an internal-error code. `Validate` and `Submit` do not throw for rejected input, and exceptions from your `OnViolation` callbacks are swallowed.
- `Validate` and `Submit` have `ValidateAsync` and `SubmitAsync` twins. Use the async versions in web apps and services (no thread is blocked while the database runs, and they can be cancelled); use the synchronous ones in console tools and scripts. The synchronous method never waits on a task.
- Thread-safe once configured. Configure the options before sharing an instance.
- Dependency injection: `services.AddDataFence(o => ...)` registers `DataFenceGuardrails` and `IDataFenceGuardrails`.

```csharp
var options = new DataFenceGuardrailsOptions();
options.PromptGuardrails.Rules.SetSeverity("LQF011", Severity.Critical);
options.PromptGuardrails.Rules.DisableRule("LQF012");
options.PromptGuardrails.OnViolation = v => Console.WriteLine($"{v.Code}: {v.Message}");
```

## Prompt layer

Fast, deterministic, offline first layer that blocks known prompt-injection patterns. **Reworded attacks can pass, so it is not a substitute for the later layers. Add your own classifier through the `Classifier` hook for broader coverage, and use a least-privilege database login.**

Settings: `MaxInputLength`, `NormalizeInput`, `AllowFormattingCharacters` (default `true`), `DeobfuscateInput` (default `false`), `BlockedKeywords`, `Classifier`, `ClassifierTimeout` and `AddCustomRule`.

## SQL layer

allowed tables, required filter column (row isolation), tautologies

`RequiredColumn`, `MaxAllowedDistance`.
- `RequiredColumn` is a row-isolation filter, not column protection: every query must filter on that column (for example `CustomerID`) and, when `RequiredColumnValue` is set, compare it to that value (`SVG004`). It does not limit which columns a query selects.
- Per-request
- `EvaluateVectorDistance(text, distance, out safeText)` accepts a vector search match only when its distance is within `MaxAllowedDistance`; otherwise it returns `false`, a safe fallback message, and reports `SVG006`.
- CTEs and derived tables are supported; a CTE name is not an unauthorized table.

## Schema layer

Allowlists the tables, views and functions the SQL may use: `options.SchemaGuardrails.AddTable("SalesLT.Product")`. Table names must include the schema, except `dbo`, which is optional (`Orders` equals `dbo.Orders`); use the same names as `SqlGuardrails.AllowedTables`. Unmapped objects are rejected (`SCR001`), system objects such as `sys` and `INFORMATION_SCHEMA` are always rejected (`SCR002`).

**Columns are not checked.** DataFence does not enforce which columns a query selects, and `AddTable` takes no column list. Column-level protection belongs in the database: expose only the columns you want through views or table-valued functions, grant the login access to those, and allowlist them here. Stored procedures cannot be executed through DataFence: the full flow accepts only a plain SELECT and rejects `EXEC` (`SVG007`). Used on its own, SchemaGuardrails still checks procedure names it finds in the SQL against the allowlist.

## Parameters layer

Typed, length-limited variables. `options.ParameterGuardrails.Define("name", SqlDbType.NVarChar, 50)` defines each accepted variable; the values passed to `Validate` are cast to the declared type. Over-length strings are rejected (`PSN006`) by default, or truncated and reported (`PSN004`) when you choose `OverLength.Truncate`.

## Database layer

Runs one checked SELECT on your connection: forced command timeout, row cap, single statement, optional query rate limit, circuit breaker and estimated-cost cap (all off by default). Database failures and timeouts come back in the result with a `QIP` code; they are not thrown.

## Result layer

Masks Luhn-valid card numbers, US social security numbers, API keys/tokens and columns with sensitive names. A pattern that times out masks the whole cell (fail closed).

## Audit layer

Tamper-evident JSON-lines log of every violation and every approved request. Each entry carries the hash of the previous entry. Settings: `FilePath`, optional `EncryptionKey` (32-byte AES-256-GCM key that you supply) and `RedactInput` (removes the prompt and SQL text from the entries).

### Reading the audit log

Turn the layer on and give it a file path: `options.AuditLogger.Enable(); options.AuditLogger.FilePath = @"C:\Logs\audit.log";`. The file is created on the first entry and only ever appended to.

Without a key, the file is plain text with one JSON object per line. Each object has these fields:

| Field | Meaning |
|---|---|
| `Seq` | Entry number, starting at 1 and increasing by 1. |
| `TimeUtc` | When the event happened, in UTC. |
The `CorrelationId` you set on the `DataFenceInput`, or null.
| `Component` | The layer that reported the event. |
| `Code` | The rule code (for example `SVG007`), or `DFP000` for an approved request. |
| `Severity` | The severity label. |
| `Message` | What happened. |
| `Input` | The prompt or SQL text, or `[redacted]` when `RedactInput` is on, or null. |
| `PrevHash` | Hash of the previous entry (a fixed start value for the first entry). |
| `Hash` | Hash of this entry, which includes `PrevHash`. |

PowerShell:

```powershell
Get-Content C:\Logs\audit.log | ForEach-Object { $_ | ConvertFrom-Json } | Format-Table Seq, TimeUtc, Component, Code, Severity, Message
```

C#:

```csharp
foreach (string line in File.ReadLines(path))
{
    using JsonDocument entry = JsonDocument.Parse(line);
    Console.WriteLine(entry.RootElement.GetProperty("Code").GetString() + " " + entry.RootElement.GetProperty("Message").GetString());
}
```

Encryption is optional and off by default. To turn it on, set `AuditLogger.EncryptionKey` to a 32-byte key that you create and keep. The library never stores the key. With a key, every line is encrypted, so the file cannot be read with the examples above; you can only check it with `DataFenceGuardrails.VerifyAuditLog(path, key)`. If you lose the key, the file can no longer be verified or continued.

`RedactInput` and encryption are independent. `RedactInput` removes the prompt and SQL text from the entries, so it never reaches the file. Encryption hides the whole line from anyone who does not have the key. Use either or both.

Verify a file at any time with `DataFenceGuardrails.VerifyAuditLog(path)` (or `(path, key)` for an encrypted file). It returns `false` if any entry was edited, deleted or reordered, or if the key is wrong.

## Rule codes

Each table lists the default severity. Override

### DFP - pipeline (no section; reported by `DataFenceGuardrails`)

| Code | Default severity | Triggered when | What to do |
|---|---|---|---|
| `DFP000` | Info | The request was approved. Written to the audit log only. | None. |
| `DFP001` | Critical | A stage or the audit write failed unexpectedly; blocked. | Investigate. |
| `DFP002` | Blocked | Variables without the `ParameterGuardrails` section. | Fix the call. |
| `DFP003` | Blocked | No active license or evaluation. | Activate your license in the demo's Manage License menu. |
| `DFP004` | Blocked | `Submit` was given a result that is not approved, was already submitted, came from another instance, or the Database layer is off. Nothing is sent. | Call `Validate` first and submit its approved result once. |
| `DFP005` | Blocked | A value was set in `DataFenceInput` that no turned-on guardrail uses, so it would not have been checked. | Remove the value, or turn on the guardrail that uses it. |

### LQF - Prompt (`PromptGuardrails` section)

| Code | Default severity | Triggered when | What to do |
|---|---|---|---|
| `LQF005` | Blocked | The input is `null`. | Fix the caller. |
| `LQF006` | Critical | A rule timed out (100 ms limit) or failed unexpectedly, so the input is rejected (fails closed). | Treat as hostile. Its severity can be overridden. Disabling it only silences the report; the input is still rejected. |
| `LQF007` | Blocked | Input is longer than `MaxInputLength`. | Shorten the input or raise the limit. If you screen pasted documents or long articles, raise `MaxInputLength` or set it to `null`. |
| `LQF008` | Blocked | A `BlockedKeywords` entry was found. | Reject. |
| `LQF009` | Blocked | No active license or evaluation. | Activate your license in the demo's Manage License menu. |
| `LQF010` | Blocked | Control character, lone surrogate, bidirectional override or line or paragraph separator (formatting characters too when `AllowFormattingCharacters` is `false`). | Reject. |
| `LQF011` | Blocked | Instruction override, for example "ignore all previous instructions". | Reject. |
| `LQF012` | Blocked | Request to reveal the system prompt or hidden instructions. | Reject. |
| `LQF013` | Blocked | Role or persona switch, for example "you are now in developer mode". | Reject. |
| `LQF014` | Blocked | Request to bypass or disable safeguards. | Reject. |
| `LQF017` | Blocked | Fake role or delimiter markers such as a `system:` line or `[INST]`. | Reject. |
| `LQF018` | Blocked | A Base64, hex or ROT13 payload that decodes to one of the attacks above. | Reject. |
| `LQF019` | Warning | Mixed-script look-alike letters. Off by default. | Enable with `EnableRule`. |
| `LQF020` | Warning | Excessive repetition or padding. Off by default. | Enable with `EnableRule`. |
| `LQF022` | Blocked | Secret or key (AWS, private key, JWT, API keys, GitHub tokens) in the prompt. | Remove the secret. |
| `LQF023` | Blocked | Instruction override in Spanish, French or German. Multilingual coverage is limited: for other languages, translate the input to English first and check the translated text, or use the Classifier hook. | Reword the prompt. |
| `LQF024` | Warning | Role-play or hypothetical framing. Off by default. | Enable with `EnableRule`. |
| `LQF025` | Blocked | Your classifier (`Classifier`) flagged the prompt, failed or timed out (fails closed). | Optional; set `Classifier` and `ClassifierTimeout`. |

URL encoding is decoded up to three times before matching. The classifier hook


### Adding your own classifier (optional)

**Prompt guardrails (built in).** Our prompt guardrails check each prompt against a fixed set of text patterns, such as "ignore all previous instructions". This is regex-based matching. It is fast, predictable and needs no extra files. It only recognizes attacks that look like the patterns it knows, so reworded attacks get through. That is why the later layers (SQL, schema, parameters, database, result masking) exist.

**Classifier (optional, you supply it).** A classifier is a small AI model trained to recognize prompt-injection attacks by meaning, not by exact wording. You give it a prompt and it answers "attack" or "not an attack". It can catch reworded attacks the patterns miss, but it can also wrongly flag a normal prompt. It needs a model file, which is not included in this product. You connect it to the guardrails with the `Classifier` setting, and it runs after the built-in checks. If it throws or takes longer than `ClassifierTimeout`, the prompt is rejected with LQF025.

**Important:** No prompt filter catches every attack, and regex-based filters in particular catch only a share of them. The classifier is optional extra protection. It does not replace the other layers.

If the classifier fails or takes too long, the request is blocked.

```csharp
// MyModel
// This is an illustration only; DataFenceComponents does not ship a model.
options.PromptGuardrails.Classifier = async (text, token) => await MyModel.IsAttackAsync(text, token);
options.PromptGuardrails.ClassifierTimeout = TimeSpan.FromSeconds(2);
```

Notes:
- The classifier runs in addition to the pattern rules, never instead of them. The classifier adds to the built-in rules and does not replace them. Keep the rules on, and disable an individual rule only if it causes false positives for your users. No model is included.
- Return true for an attack and false for safe. A true result, an exception or a timeout rejects the prompt with LQF025.
- The token passed to your function is cancelled when ClassifierTimeout expires, so pass it to your model or service call.
- The function runs on every request and requests can run concurrently, so it must be thread-safe. Create the model session once and reuse it.

Models add download size, latency and their own false positives, so measure one on your own prompts before relying on it.


Customer rule codes (from `AddCustomRule`) may not start with `LQF`. Disabling `LQF005` makes a `null` input count as safe; disabling `LQF007` removes the length check.


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
| `SVG010` | Blocked | Control character, lone surrogate, bidirectional override or line or paragraph separator in the SQL. | Reject. |


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
| `PSN009` | Blocked | Control character, lone surrogate, bidirectional override or line or paragraph separator in a string value. | Reject, or set `AllowControlCharacters`. |

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



## Exceptions that remain

Runtime checks return blocked results rather than throwing. These exceptions are deliberate:

- Setup mistakes: null options, invalid rule codes, duplicate or reserved custom rule codes, `MaxRows` below 1, an `EncryptionKey` that is not 32 bytes, invalid `Define(...)` arguments.
- `OperationCanceledException` when the caller cancels `DataFenceGuardrails.SubmitAsync`.

## Demo and tests

The customer download includes a demo: a console application (the `DataFenceGuardrailsDemo` folder) with a menu item for each guardrail. The DataFence Component demo gives an example of every guardrail function. Start it, pick a menu item and it runs the examples for you.

| Menu | Shows |
|---|---|
| 1 | Quick Start: setup once, then Process each AI request. One prompt
| 2 to 8 | One item per guardrail: PromptGuardrails, SqlGuardrails, SchemaGuardrails, ParameterGuardrails, DatabaseGuardrails, ResultGuardrails and AuditLogger. |
| 9 | Common Features that apply to all guardrails: severity overrides, disabling rules, callbacks, and the other shared settings. |
| 10 | DataFenceGuardrails: every layer in one call (`Validate` then `Submit`), with failures, options and audit. |
| 11 | Manage License: activate, deactivate and About License (the version and support report). |
| 12 | Exit. |

Each example is written as plain, direct code with a QUICK START comment at the top, so you can copy it into your own application. Every example prints its outcome in the same form: `Result: APPROVED`, or `Result: BLOCKED <code> (<severity>) - <message>`. Each guardrail demo switches the other layers off by calling `DisableAllGuardrails()` and then `Enable()` on only its own layer; normally you would enable every layer you use. The demo runs without a database by using a simulated connection; to use a real one, change the connection in `Program.cs`.

The xUnit project `Tests` covers every guardrail: valid and invalid SQL, tautologies, customer isolation, table and schema allowlists, statement types, vector distance, parameter typing and truncation, database timeout/row cap/`DisableDatabaseAccess`, masking, audit chain, integrated flow and licensing. Run it with `dotnet test` or Test Explorer in Visual Studio.

## Which version do I have?

You can always see exactly which DataFence Guardrails you are running. This information is available in your own code:

- `DataFenceInfo.Version`
- `DataFenceInfo.Description` is one line with the product, version and .NET runtime.
- `DataFenceInfo.Report()` returns all of this as one block of text, plus the version of each of the nine DataFence DLLs (each is loaded if needed). All DLLs share one version; a line ending in `MISMATCH` means a DLL from a different release is mixed in, and `NOT FOUND` means a DLL is missing.


When you contact support, please include the text from `Report()`. It tells us the exact version, runtime, license state and DLLs you are using, which saves time. The same version information is also in each DLL's file properties (Details tab).

## License

Commercial library, sold as one bundle with one license key that unlocks every guardrail. The EULA delivered with the package is the binding text; this section is a summary.

- **Evaluation:** without a license key the library runs a 30-day evaluation. From day 15, `DataFenceLicense.TrialNotice` returns "[DataFence trial version - license required for production use]"; it is null when licensed or earlier. When the evaluation has ended and there is no license, every layer fails closed with its NotLicensed code (`LQF009`, `SVG008`, `SCR006`, `PSN008`, `QIP007`, `DRS007`, `DFP003`; the audit logger throws `InvalidOperationException` with `SAL003`).
- **Activation:** enter your key once on each developer computer, in the demo's Manage License menu. The key does not need to be placed in your source code. Use Deactivate in the same menu to release a seat, for example when you move to another computer. When you receive a new key, for example at renewal, deactivate the old license (no key is needed; the stored license is used and the masked key is shown) and then activate the new one. If you activate the new key directly, the old key on this computer is replaced automatically, and a mistyped new key leaves the old activation unchanged. The menu shows the active product and masked key, for example "DataFence Guardrails license ...A1B2".

- **Seats:** one seat is one developer. Each developer needs their own seat, including developers who share a server or build machine. The key is activated once per developer computer, and the number of seats you buy (1, 5 or 10 seats, larger sizes by agreement) is the number of developer computers the key can be activated on. A Bundle key covers both DataFence Guardrails and DataFence AgentShield; one seat is one developer computer however many of the products run on it, and each product is activated separately on that computer.
- **Released applications:** you may deploy or distribute the applications you build without limit. A licensed build does not expire.
- **Your key:** the key is confidential. Do not publish it; you are responsible for all use of it.
- **Payment:** charged at purchase; no refunds. If a key is refunded or disabled, the developer license stops working.

