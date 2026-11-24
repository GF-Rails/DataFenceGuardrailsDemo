// DataFenceComponents Copyright (c) 2026 All rights reserved.

using DataFenceGuardrailsEngine;

namespace Demo.Tests
{
    internal class SchemaGuardrailsDemo
    {
        // StartDemo is demo only, not part of the component. The menu in Program.cs calls StartDemo to start this demo.
        public static void StartDemo()
        {
            Console.WriteLine("=== SchemaGuardrails - Custom Test Console ===");
            // Only the Schema guardrail is used in this demo. Normally you would have all the guardrails enabled together.
            // The others are switched off here only to illustrate what this one guardrail does.
            // Note that SchemaGuardrails runs inside your application and never touches the database.

            // QUICK START: validating a request takes four steps, and the code below does each one.
            //
            // Step 1. List each table, view or procedure the SQL may use. Columns are not checked.
            //         options.SchemaGuardrails.AddTable("SalesLT.Product");
            //         Any table that is not listed is rejected. System objects (sys, INFORMATION_SCHEMA) are always rejected.
            //
            // Step 2. Create the DataFenceGuardrails object from the options. Do this once, when your program starts.
            //         var guardrails = new DataFenceGuardrails(options);
            //
            // Step 3. For each request, put the SQL in a DataFenceInput and call Validate.
            //         input.Sql = "SELECT ...";
            //         var result = guardrails.Validate(input);
            //
            // Step 4. Read the result.
            //         result.Approved is true when every table is allowed.
            //         When it is false, result.Violation holds the code, severity and message.
            //
            // DisableAllGuardrails and SchemaGuardrails.Enable are for this demo only.
            // In your own code you would enable every guardrail you use.
            var options = new DataFenceGuardrailsOptions();
            options.DisableAllGuardrails();
            options.SchemaGuardrails.Enable();

            // Allowed table 1: SalesLT.Product
            options.SchemaGuardrails.AddTable("SalesLT.Product");

            // Allowed table 2: SalesLT.SalesOrderHeader
            options.SchemaGuardrails.AddTable("SalesLT.SalesOrderHeader");

            // Allowed table 3: sp_orders. The name looks like a system procedure, but the customer listed it on purpose.
            options.SchemaGuardrails.AddTable("sp_orders");

            var guardrails = new DataFenceGuardrails(options);

            Console.WriteLine("1. Known table");
            var input = new DataFenceInput();
            input.Sql = "SELECT * FROM SalesLT.Product";
            Console.WriteLine("Input: " + input.Sql);
            var result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("2. Case and brackets are ignored");
            input = new DataFenceInput();
            input.Sql = "SELECT * FROM [SALESLT].[salesorderheader]";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("3. Several known tables");
            input = new DataFenceInput();
            input.Sql = "SELECT p.Name FROM SalesLT.Product p JOIN SalesLT.SalesOrderHeader o ON o.SalesOrderID = p.ProductID";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("4. Unmapped table: not in the allowlist");
            input = new DataFenceInput();
            input.Sql = "SELECT * FROM SalesLT.Product p JOIN Salaries s ON s.Id = p.ProductID";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("5. System catalog: sys.objects");
            input = new DataFenceInput();
            input.Sql = "SELECT * FROM sys.objects";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("6. System catalog: INFORMATION_SCHEMA");
            input = new DataFenceInput();
            input.Sql = "SELECT * FROM INFORMATION_SCHEMA.TABLES";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("7. Cross-database system object");
            input = new DataFenceInput();
            input.Sql = "SELECT * FROM master.sys.databases";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("8. Dangerous prefix, not listed: blocked as a system object");
            input = new DataFenceInput();
            input.Sql = "EXEC xp_cmdshell 'dir'";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("9. Dangerous-looking prefix, but listed by the customer: allowed");
            input = new DataFenceInput();
            input.Sql = "EXEC sp_orders";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("10. CTE name is not a table");
            input = new DataFenceInput();
            input.Sql = "WITH x AS (SELECT * FROM SalesLT.Product) SELECT * FROM x";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("11. Unmapped table hidden in a subquery");
            input = new DataFenceInput();
            input.Sql = "SELECT * FROM SalesLT.Product WHERE ProductID IN (SELECT ProductID FROM Salaries)";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("12. Unparsable SQL");
            input = new DataFenceInput();
            input.Sql = "SELECT FROM WHERE ((";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");
        }
    }
}
