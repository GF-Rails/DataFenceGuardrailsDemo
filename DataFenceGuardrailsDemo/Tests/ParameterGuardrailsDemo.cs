// DataFenceComponents Copyright (c) 2026 All rights reserved.

using System.Data;
using DataFenceGuardrailsEngine;
using ParameterGuardrailsEngine;

namespace Demo.Tests
{
    internal class ParameterGuardrailsDemo
    {
        // StartDemo is demo only, not part of the component. The menu in Program.cs calls StartDemo to start this demo.
        public static void StartDemo()
        {
            Console.WriteLine("=== ParameterGuardrails - Custom Test Console ===");
            // Only the Parameters guardrail is used in this demo. Normally you would have all the guardrails enabled together.
            // The others are switched off here only to illustrate what this one guardrail does.
            // Note that ParameterGuardrails only prepares typed parameters inside your application and never touches the database.

            // QUICK START: validating a request takes four steps, and the code below does each one.
            //
            // Step 1. Define the variables your SQL may use, with their SQL types.
            //         options.ParameterGuardrails.Define("CustomerID", SqlDbType.Int);
            //         This allows a variable named CustomerID and requires it to be a whole number.
            //
            // Step 2. Create the DataFenceGuardrails object from the options. Do this once, when your program starts.
            //         var guardrails = new DataFenceGuardrails(options);
            //
            // Step 3. For each request, put the SQL and the variables in a DataFenceInput and call Validate.
            //         input.Sql = sql;
            //         input.Variables = variables;
            //         var result = guardrails.Validate(input);
            //
            // Step 4. Read the result.
            //         result.Approved is true when every variable passed.
            //         When it is false, result.Violation holds the code, severity and message.
            //
            // DisableAllGuardrails and ParameterGuardrails.Enable are for this demo only.
            // In your own code you would enable every guardrail you use.
            //
            // Variables defined in this demo:
            // - CustomerID  Int
            // - Name        NVarChar(10), longer text is rejected
            // - Notes       NVarChar(10), longer text is cut to 10 characters
            // - Created     DateTime2
            // - Token       UniqueIdentifier
            // - Active      Bit
            var options = new DataFenceGuardrailsOptions();
            options.DisableAllGuardrails();
            options.ParameterGuardrails.Enable();
            options.ParameterGuardrails.Define("CustomerID", SqlDbType.Int);
            options.ParameterGuardrails.Define("Name", SqlDbType.NVarChar, 10);
            options.ParameterGuardrails.Define("Notes", SqlDbType.NVarChar, 10, OverLength.Truncate);
            options.ParameterGuardrails.Define("Created", SqlDbType.DateTime2);
            options.ParameterGuardrails.Define("Token", SqlDbType.UniqueIdentifier);
            options.ParameterGuardrails.Define("Active", SqlDbType.Bit);
            var guardrails = new DataFenceGuardrails(options);

            // Validate is given the SQL the variables belong to. Only the variables are checked, not the SQL.
            string sql = "SELECT SalesOrderID FROM SalesLT.SalesOrderHeader WHERE CustomerID = @CustomerID";

            // Every example below follows the same steps, so read the first one and the rest are the same:
            // 1. Create a new dictionary and add the variable.
            // 2. Create a new DataFenceInput and give it the SQL and the variables.
            // 3. Validate and print the result.

            Console.WriteLine("1. Valid: integer text is cast to an int (CustomerID is Int)");
            var variables = new Dictionary<string, object>();
            variables.Add("CustomerID", "101");
            Console.WriteLine("Variable: CustomerID = " + variables["CustomerID"]);
            var input = new DataFenceInput();
            input.Sql = sql;
            input.Variables = variables;
            var result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("2. Valid: date text is cast to a DateTime (Created is DateTime2)");
            variables = new Dictionary<string, object>();
            variables.Add("Created", "2026-01-15T10:00:00Z");
            Console.WriteLine("Variable: Created = " + variables["Created"]);
            input = new DataFenceInput();
            input.Sql = sql;
            input.Variables = variables;
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("3. Valid: guid text is cast to a Guid (Token is UniqueIdentifier)");
            variables = new Dictionary<string, object>();
            variables.Add("Token", "3f2504e0-4f89-11d3-9a0c-0305e82c3301");
            Console.WriteLine("Variable: Token = " + variables["Token"]);
            input = new DataFenceInput();
            input.Sql = sql;
            input.Variables = variables;
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("4. Valid: boolean text is cast to a bit (Active is Bit)");
            variables = new Dictionary<string, object>();
            variables.Add("Active", "true");
            Console.WriteLine("Variable: Active = " + variables["Active"]);
            input = new DataFenceInput();
            input.Sql = sql;
            input.Variables = variables;
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("5. Injection text in a string variable stays inside the value (Name is NVarChar)");
            variables = new Dictionary<string, object>();
            variables.Add("Name", "x'; DROP--");
            Console.WriteLine("Variable: Name = " + variables["Name"]);
            input = new DataFenceInput();
            input.Sql = sql;
            input.Variables = variables;
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("6. Injection text in a numeric variable cannot be cast (CustomerID is Int)");
            variables = new Dictionary<string, object>();
            variables.Add("CustomerID", "1 OR 1=1");
            Console.WriteLine("Variable: CustomerID = " + variables["CustomerID"]);
            input = new DataFenceInput();
            input.Sql = sql;
            input.Variables = variables;
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("7. Bad date text cannot be cast (Created is DateTime2)");
            variables = new Dictionary<string, object>();
            variables.Add("Created", "'; DROP TABLE x; --");
            Console.WriteLine("Variable: Created = " + variables["Created"]);
            input = new DataFenceInput();
            input.Sql = sql;
            input.Variables = variables;
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("8. Decimal point in an integer variable (CustomerID is Int)");
            variables = new Dictionary<string, object>();
            variables.Add("CustomerID", "1.5");
            Console.WriteLine("Variable: CustomerID = " + variables["CustomerID"]);
            input = new DataFenceInput();
            input.Sql = sql;
            input.Variables = variables;
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("9. Undefined variable (Salary was never defined)");
            variables = new Dictionary<string, object>();
            variables.Add("Salary", "100");
            Console.WriteLine("Variable: Salary = " + variables["Salary"]);
            input = new DataFenceInput();
            input.Sql = sql;
            input.Variables = variables;
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("10. Invalid variable name: SQL hidden in the name");
            variables = new Dictionary<string, object>();
            variables.Add("Name; DROP TABLE x", "1");
            Console.WriteLine("Variable: Name; DROP TABLE x = " + variables["Name; DROP TABLE x"]);
            input = new DataFenceInput();
            input.Sql = sql;
            input.Variables = variables;
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("11. String too long: rejected (Name is limited to 10 characters)");
            variables = new Dictionary<string, object>();
            variables.Add("Name", "A very long customer name");
            Console.WriteLine("Variable: Name = " + variables["Name"]);
            input = new DataFenceInput();
            input.Sql = sql;
            input.Variables = variables;
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("12. String too long: cut to 10 characters (Notes is defined with OverLength.Truncate)");
            variables = new Dictionary<string, object>();
            variables.Add("Notes", "A very long customer note");
            Console.WriteLine("Variable: Notes = " + variables["Notes"]);
            input = new DataFenceInput();
            input.Sql = sql;
            input.Variables = variables;
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");

            Console.WriteLine("13. Control character in a string value (Name is NVarChar)");
            variables = new Dictionary<string, object>();
            variables.Add("Name", "Ann" + (char)0 + "Lee");
            Console.WriteLine("Variable: Name = Ann, a NUL character, Lee");
            input = new DataFenceInput();
            input.Sql = sql;
            input.Variables = variables;
            result = guardrails.Validate(input);
            if (result.Approved) Console.WriteLine("Result: APPROVED\n");
            else Console.WriteLine("Result: BLOCKED " + result.Violation?.Code + " (" + result.Violation?.Severity + ") - " + result.Violation?.Message + "\n");
        }
    }
}
