// DataFenceComponents Copyright (c) 2026 All rights reserved.

using System.Data;
using DataFenceGuardrailsEngine;

namespace Demo.Tests
{
    internal class ResultGuardrailsDemo
    {
        // StartDemo is demo only, not part of the component. The menu in Program.cs calls StartDemo to start this demo.
        public static void StartDemo()
        {
            Console.WriteLine("=== ResultGuardrails - Custom Test Console ===");
            // Only the Database and Result guardrails are used in this demo. Normally you would have all the guardrails enabled together.
            // The others are switched off here only to illustrate what this one guardrail does.
            // ResultGuardrails only checks the rows after they are returned and never connects to the database itself.
            // The simulated database returns the rows shown below, so the results are always the same.

            // QUICK START: masking sensitive data in query results takes four steps.
            //
            // Step 1. Enable the Result guardrail (and the Database guardrail that runs the query).
            //         options.ResultGuardrails.Enable();
            //         options.DatabaseGuardrails.Enable();
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
            //         result.Data holds the rows, with sensitive values already masked.
            //         When result.Approved is false, result.Violation holds the code, severity and message.

            using var connection = new SimulatedDatabaseConnectionObject(new DataTable());

            var options = new DataFenceGuardrailsOptions();
            options.DisableAllGuardrails();
            options.DatabaseGuardrails.Enable();
            options.ResultGuardrails.Enable();
            options.DatabaseGuardrails.Connection = connection;
            var guardrails = new DataFenceGuardrails(options);

            var table = new DataTable();
            var input = new DataFenceInput();
            DataFenceResult result;

            Console.WriteLine("1. Card number");
            table = new DataTable();
            table.Columns.Add("Comment", typeof(string));
            table.Rows.Add("Card 4111 1111 1111 1111");
            connection.Data = table;
            Console.WriteLine("Database returns: Comment = Card 4111 1111 1111 1111");
            input = new DataFenceInput();
            input.Sql = "SELECT * FROM SalesLT.Customer WHERE CustomerID = 1";
            result = guardrails.Validate(input);
            if (result.Approved) result = guardrails.Submit(result);
            if (result.Approved)
            {
                Console.WriteLine("Returned to the caller: " + result.Data?.Rows[0][0]);
                Console.WriteLine("Result: APPROVED\n");
            }
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("2. Social security number");
            table = new DataTable();
            table.Columns.Add("Comment", typeof(string));
            table.Rows.Add("SSN 123-45-6789");
            connection.Data = table;
            Console.WriteLine("Database returns: Comment = SSN 123-45-6789");
            input = new DataFenceInput();
            input.Sql = "SELECT * FROM SalesLT.Customer WHERE CustomerID = 1";
            result = guardrails.Validate(input);
            if (result.Approved) result = guardrails.Submit(result);
            if (result.Approved)
            {
                Console.WriteLine("Returned to the caller: " + result.Data?.Rows[0][0]);
                Console.WriteLine("Result: APPROVED\n");
            }
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("3. API key");
            table = new DataTable();
            table.Columns.Add("Comment", typeof(string));
            table.Rows.Add("key AKIAIOSFODNN7EXAMPLE");
            connection.Data = table;
            Console.WriteLine("Database returns: Comment = key AKIAIOSFODNN7EXAMPLE");
            input = new DataFenceInput();
            input.Sql = "SELECT * FROM SalesLT.Customer WHERE CustomerID = 1";
            result = guardrails.Validate(input);
            if (result.Approved) result = guardrails.Submit(result);
            if (result.Approved)
            {
                Console.WriteLine("Returned to the caller: " + result.Data?.Rows[0][0]);
                Console.WriteLine("Result: APPROVED\n");
            }
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("4. Sensitive column name: the whole column is masked");
            table = new DataTable();
            table.Columns.Add("PasswordHash", typeof(string));
            table.Rows.Add("hunter2");
            connection.Data = table;
            Console.WriteLine("Database returns: PasswordHash = hunter2");
            input = new DataFenceInput();
            input.Sql = "SELECT * FROM SalesLT.Customer WHERE CustomerID = 1";
            result = guardrails.Validate(input);
            if (result.Approved) result = guardrails.Submit(result);
            if (result.Approved)
            {
                Console.WriteLine("Returned to the caller: " + result.Data?.Rows[0][0]);
                Console.WriteLine("Result: APPROVED\n");
            }
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("5. Harmless text is left untouched");
            table = new DataTable();
            table.Columns.Add("Comment", typeof(string));
            table.Rows.Add("hello world");
            connection.Data = table;
            Console.WriteLine("Database returns: Comment = hello world");
            input = new DataFenceInput();
            input.Sql = "SELECT * FROM SalesLT.Customer WHERE CustomerID = 1";
            result = guardrails.Validate(input);
            if (result.Approved) result = guardrails.Submit(result);
            if (result.Approved)
            {
                Console.WriteLine("Returned to the caller: " + result.Data?.Rows[0][0]);
                Console.WriteLine("Result: APPROVED\n");
            }
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");
        }
    }
}
