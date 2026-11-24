// DataFenceComponents Copyright (c) 2026 All rights reserved.

using DataFenceComponents.Core;
using DataFenceGuardrailsEngine;

namespace Demo.Tests
{
    internal class SqlGuardrailsDemo
    {
        // StartDemo is demo only, not part of the component. The menu in Program.cs calls StartDemo to start this demo.
        public static void StartDemo()
        {
            Console.WriteLine("=== SqlGuardrails - Custom Test Console ===");
            // Only the SQL guardrail is used in this demo. Normally you would have all the guardrails enabled together.
            // The others are switched off here only to illustrate what this one guardrail does.
            // Note that SqlGuardrails works outside of the database: it checks the SQL text before anything is run
            // and never touches the database.

            // QUICK START: validating a request takes four steps, and the code below does each one.
            //
            // Step 1. Say which tables the SQL may use and which column every query must filter on.
            //         options.SqlGuardrails.AllowedTables.Add("SalesLT.SalesOrderHeader");
            //         options.SqlGuardrails.RequiredColumn = "CustomerID";
            //
            // Step 2. Create the DataFenceGuardrails object from the options. Do this once, when your program starts.
            //         var guardrails = new DataFenceGuardrails(options);
            //
            // Step 3. For each request, put the SQL in a DataFenceInput and call Validate.
            //         input.Sql = "SELECT ...";
            //         var result = guardrails.Validate(input);
            //
            // Step 4. Read the result.
            //         result.Approved is true when the SQL passed.
            //         When it is false, result.Violation holds the code, severity and message.
            //         To run an approved query, call guardrails.Submit(result); see the DatabaseGuardrails demo.
            //
            // The two lines DisableAllGuardrails and SqlGuardrails.Enable are for this demo only.
            // In your own code you would enable every guardrail you use.
            var options = new DataFenceGuardrailsOptions();
            options.DisableAllGuardrails();
            options.SqlGuardrails.Enable();
            options.SqlGuardrails.AllowedTables.Add("SalesLT.SalesOrderHeader");
            options.SqlGuardrails.AllowedTables.Add("SalesLT.ProductDescription");
            options.SqlGuardrails.RequiredColumn = "CustomerID";
            var guardrails = new DataFenceGuardrails(options);

            Console.WriteLine("1. Valid query: allowed table, customer filter present");
            var input = new DataFenceInput();
            input.Sql = "SELECT TOP 3 * FROM SalesLT.SalesOrderHeader WHERE CustomerID = 101;";
            Console.WriteLine("Input: " + input.Sql);
            var result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("2. Destructive statement: the AI tries to drop a table");
            input = new DataFenceInput();
            input.Sql = "DROP TABLE SalesLT.SalesOrderHeader;";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("3. Unauthorized table: the AI queries a table that is not on the allowed list");
            input = new DataFenceInput();
            input.Sql = "SELECT * FROM FinancialLedger;";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("4. Customer filter bypass: the AI forgets the required CustomerID filter");
            input = new DataFenceInput();
            input.Sql = "SELECT * FROM SalesLT.SalesOrderHeader WHERE Status = 5;";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("5. Tautology: OR 1=1 makes the customer filter always true");
            input = new DataFenceInput();
            input.Sql = "SELECT TOP 10 * FROM SalesLT.SalesOrderHeader WHERE CustomerID = 101 OR 1=1;";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("6. Tautology: AND 1 = 1 with the customer filter present");
            input = new DataFenceInput();
            input.Sql = "SELECT * FROM SalesLT.SalesOrderHeader WHERE CustomerID = 101 AND 1 = 1;";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("7. UNION: the AI reads a second, unauthorized table");
            input = new DataFenceInput();
            input.Sql = "SELECT * FROM SalesLT.SalesOrderHeader WHERE CustomerID = 101 UNION SELECT * FROM SalesLT.Customer;";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("8. Stacked statements: a valid query followed by a destructive one");
            input = new DataFenceInput();
            input.Sql = "SELECT TOP 1 * FROM SalesLT.SalesOrderHeader WHERE CustomerID = 101; DROP TABLE SalesLT.SalesOrderHeader;";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("9. Commented-out condition");
            input = new DataFenceInput();
            input.Sql = "SELECT * FROM SalesLT.SalesOrderHeader WHERE CustomerID = 101 -- OR 1=1";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("10. Invalid T-SQL: the AI produces a query that does not parse");
            input = new DataFenceInput();
            input.Sql = "SELECT TOP * FROM SalesLT.SalesOrderHeader WHERE;";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("11. Statement type: the AI tries to change data");
            input = new DataFenceInput();
            input.Sql = "UPDATE SalesLT.SalesOrderHeader SET Comment = 'x' WHERE CustomerID = 101;";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("12. Statement type: the AI tries to alter a table");
            input = new DataFenceInput();
            input.Sql = "ALTER TABLE SalesLT.SalesOrderHeader ADD Extra INT;";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("13. SELECT INTO: the AI copies data into a new table");
            input = new DataFenceInput();
            input.Sql = "SELECT * INTO Stolen FROM SalesLT.SalesOrderHeader WHERE CustomerID = 101;";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("14. CTE: the customer filter is in the base query");
            input = new DataFenceInput();
            input.Sql = "WITH c AS (SELECT * FROM SalesLT.SalesOrderHeader WHERE CustomerID = 101) SELECT * FROM c;";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("15. CTE: the base query has no customer filter");
            input = new DataFenceInput();
            input.Sql = "WITH c AS (SELECT * FROM SalesLT.SalesOrderHeader) SELECT * FROM c;";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("16. Required column value: the filter matches the caller's customer id (42)");
            input = new DataFenceInput();
            input.Sql = "SELECT * FROM SalesLT.SalesOrderHeader WHERE CustomerID = 42;";
            input.RequiredColumnValue = "42";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("17. Required column value: the AI filters on a different customer id (7)");
            input = new DataFenceInput();
            input.Sql = "SELECT * FROM SalesLT.SalesOrderHeader WHERE CustomerID = 7;";
            input.RequiredColumnValue = "42";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            // Rule settings can be changed at any time, even after the guardrails were created.
            // A severity override only changes how the violation is labeled; the verdict stays BLOCKED.
            Console.WriteLine("18. SVG007 raised to Critical (verdict stays BLOCKED)");
            options.SqlGuardrails.Rules.SetSeverity("SVG007", Severity.Critical);
            input = new DataFenceInput();
            input.Sql = "TRUNCATE TABLE SalesLT.SalesOrderHeader;";
            Console.WriteLine("Input: " + input.Sql);
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            // Vector search: reject weak matches so the AI never answers from text that is too far from the question.
            // EvaluateVectorDistance returns true when the match is close enough to use.
            // The text to use in your answer is returned in "answer": the original text when accepted,
            // or a fallback notice when the match is rejected.
            Console.WriteLine("19. Vector distance gate: matches farther than Sql.MaxAllowedDistance (0.20) are rejected");
            var vectorOptions = new DataFenceGuardrailsOptions();
            vectorOptions.DisableAllGuardrails();
            vectorOptions.SqlGuardrails.Enable();
            vectorOptions.SqlGuardrails.MaxAllowedDistance = 0.20;
            var vector = new DataFenceGuardrails(vectorOptions);

            string text = "Our return policy allows refunds within 30 days.";
            bool accepted = vector.EvaluateVectorDistance(text, 0.12, out string answer);
            if (accepted) Console.WriteLine("Distance 0.12: ACCEPTED");
            else Console.WriteLine("Distance 0.12: REJECTED");
            Console.WriteLine("Text used: " + answer + "\n");

            text = "Bikes are available in red and blue.";
            accepted = vector.EvaluateVectorDistance(text, 0.20, out answer);
            if (accepted) Console.WriteLine("Distance 0.20: ACCEPTED");
            else Console.WriteLine("Distance 0.20: REJECTED");
            Console.WriteLine("Text used: " + answer + "\n");

            text = "Completely unrelated paragraph about shipping to Mars.";
            accepted = vector.EvaluateVectorDistance(text, 0.48, out answer);
            if (accepted) Console.WriteLine("Distance 0.48: ACCEPTED");
            else Console.WriteLine("Distance 0.48: REJECTED");
            Console.WriteLine("Text used: " + answer + "\n");

            Console.WriteLine("20. Special characters: a control character in the SQL is rejected");
            input = new DataFenceInput();
            input.Sql = "SELECT * FROM SalesLT.SalesOrderHeader WHERE CustomerID = 101" + (char)0;
            Console.WriteLine("Input: SELECT * FROM SalesLT.SalesOrderHeader WHERE CustomerID = 101, followed by a NUL character");
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");
        }
    }
}
