// DataFenceComponents Copyright (c) 2026 All rights reserved.

using System.Data;
using DataFenceGuardrailsEngine;

namespace Demo.Tests
{
    internal class DatabaseGuardrailsDemo
    {
        // StartDemo is demo only, not part of the component. The menu in Program.cs calls StartDemo to start this demo.
        public static void StartDemo()
        {
            Console.WriteLine("=== DatabaseGuardrails - Custom Test Console ===");
            // Only the Database guardrail is used in this demo. Normally you would have all the guardrails enabled together.
            // The others are switched off here only to illustrate what this one guardrail does.
            // Allowed queries are sent to the connection from Program.cs.

            // QUICK START: running a query takes four steps, and the code below does each one.
            //
            // Step 1. Give the guardrail the database connection you created and own. You open and dispose it, never the library.
            //         options.DatabaseGuardrails.Connection = Program.Connection;
            //
            // Step 2. Create the DataFenceGuardrails object from the options. Do this once, when your program starts.
            //         var guardrails = new DataFenceGuardrails(options);
            //
            // Step 3. For each request, put the SQL in a DataFenceInput, call Validate, and when it is approved call Submit.
            //         input.Sql = "SELECT ...";
            //         var result = guardrails.Validate(input);
            //         if (result.Approved) result = guardrails.Submit(result);
            //
            // Step 4. Read the result.
            //         result.Approved is true when the query passed and ran.
            //         result.Data holds the rows that came back.
            //         When it is false, result.Violation holds the code, severity and message.
            //
            // DisableAllGuardrails and DatabaseGuardrails.Enable are for this demo only.
            // In your own code you would enable every guardrail you use.
            //
            // The connection is created once in Program.cs (Program.Connection); the simulated one has its own sample rows.
            // The simulated connection must not be slow here, so the delay is reset; a real database is unaffected.
            if (Program.Simulated)
            {
                ((SimulatedDatabaseConnectionObject)Program.Connection).Delay = TimeSpan.Zero;
            }

            var options = new DataFenceGuardrailsOptions();
            options.DisableAllGuardrails();
            options.DatabaseGuardrails.Enable();
            options.DatabaseGuardrails.Connection = Program.Connection;
            var guardrails = new DataFenceGuardrails(options);

            Console.WriteLine("1. Plain SELECT");
            var input = new DataFenceInput();
            input.Sql = "SELECT TOP 3 ProductID, Name FROM SalesLT.Product";
            Console.WriteLine("Input: " + input.Sql);
            var result = guardrails.Validate(input);
            if (result.Approved) result = guardrails.Submit(result);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("2. DELETE");
            input = new DataFenceInput();
            input.Sql = "DELETE FROM SalesLT.Product";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) result = guardrails.Submit(result);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("3. UPDATE");
            input = new DataFenceInput();
            input.Sql = "UPDATE SalesLT.Product SET Name = 'x'";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) result = guardrails.Submit(result);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("4. Stacked statements");
            input = new DataFenceInput();
            input.Sql = "SELECT 1; SELECT 2";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) result = guardrails.Submit(result);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("5. SELECT ... INTO");
            input = new DataFenceInput();
            input.Sql = "SELECT * INTO Copy FROM SalesLT.Product";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) result = guardrails.Submit(result);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            // SELEC and FORM are misspelled on purpose, to show that SQL that does not parse is blocked.
            Console.WriteLine("6. Garbled SQL");
            input = new DataFenceInput();
            input.Sql = "SELEC * FORM";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) result = guardrails.Submit(result);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            // Examples 7 to 10 change a database option, so each one builds its own DataFenceGuardrails from its own options.
            // MaxRows and CommandTimeout are read when DataFenceGuardrails is created, so they cannot be changed afterwards.

            // DisableDatabaseAccess: nothing can reach a database.
            Console.WriteLine("7. DisableDatabaseAccess = true");
            options = new DataFenceGuardrailsOptions();
            options.DisableAllGuardrails();
            options.DatabaseGuardrails.Enable();
            options.DatabaseGuardrails.Connection = Program.Connection;
            options.DatabaseGuardrails.DisableDatabaseAccess = true;
            guardrails = new DataFenceGuardrails(options);
            input = new DataFenceInput();
            input.Sql = "SELECT 1";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) result = guardrails.Submit(result);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            // Row cap: only the first 5 rows are returned, so the result is still approved.
            Console.WriteLine("8. Row cap (MaxRows = 5): only the first 5 rows are returned");
            options = new DataFenceGuardrailsOptions();
            options.DisableAllGuardrails();
            options.DatabaseGuardrails.Enable();
            options.DatabaseGuardrails.Connection = Program.Connection;
            options.DatabaseGuardrails.MaxRows = 5;
            guardrails = new DataFenceGuardrails(options);
            input = new DataFenceInput();
            input.Sql = "SELECT CustomerID FROM SalesLT.Customer";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) result = guardrails.Submit(result);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            // Forced command timeout.
            // DatabaseGuardrails limits every command to Database.CommandTimeout (a TimeSpan).
            // The actual default is 30 seconds. This demo sets it to 2 seconds for testing purposes only,
            // so that the timeout is easy to trigger.
            // To force a timeout, this demo makes the simulated database take one second longer than the limit
            // (the Delay property of the simulated connection), so the query is stopped and the result carries code QIP015.
            // A real database has no Delay property, so a deliberately slow query is used instead.
            Console.WriteLine("9. Forced command timeout (Database.CommandTimeout)");
            options = new DataFenceGuardrailsOptions();
            options.DisableAllGuardrails();
            options.DatabaseGuardrails.Enable();
            options.DatabaseGuardrails.Connection = Program.Connection;
            options.DatabaseGuardrails.CommandTimeout = TimeSpan.FromSeconds(2);
            guardrails = new DataFenceGuardrails(options);
            if (Program.Simulated)
            {
                ((SimulatedDatabaseConnectionObject)Program.Connection).Delay = options.DatabaseGuardrails.CommandTimeout + TimeSpan.FromSeconds(1);
            }
            input = new DataFenceInput();
            input.Sql = "SELECT * FROM SalesLT.Customer a CROSS JOIN SalesLT.Customer b CROSS JOIN SalesLT.Customer c";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) result = guardrails.Submit(result);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");
            if (Program.Simulated)
            {
                ((SimulatedDatabaseConnectionObject)Program.Connection).Delay = TimeSpan.Zero;
            }

            // No connection set: the request is refused with code QIP009 because there is nothing to run the SQL on.
            Console.WriteLine("10. No connection set (set Database.Connection to your own connection object)");
            options = new DataFenceGuardrailsOptions();
            options.DisableAllGuardrails();
            options.DatabaseGuardrails.Enable();
            guardrails = new DataFenceGuardrails(options);
            input = new DataFenceInput();
            input.Sql = "SELECT 1";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) result = guardrails.Submit(result);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");
        }
    }
}
