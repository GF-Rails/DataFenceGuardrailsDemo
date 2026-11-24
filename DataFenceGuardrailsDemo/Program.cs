// DataFenceComponents Copyright (c) 2026 All rights reserved.

// SWITCHING .NET VERSIONS: this demo uses the DLLs in ..\DLL\<TargetFramework>. To test the net10.0 DLLs
// instead of net8.0, change <TargetFramework> in DataFenceGuardrailsDemo.csproj to net10.0 and rebuild. The menu header
// shows which version and runtime are running. To use the NuGet package instead of the DLLs, see the NuGet folder.

using DataFenceComponents.Core;
using Demo.Tests;
using SqlGuardrailsEngine;
using System.Data;
using System.Data.Common;

namespace Demo
{
    class Program
    {
        // ---------------------------------------------------------------------
        // The database connection, created once for the whole demo and used by the demos as Program.Connection.
        // This demo uses a simulated connection object so it runs without a database. To use your own SQL Server
        // database, comment out the simulated line below and use the commented line above it, with your own
        // connection string. For Windows authentication use Integrated Security=true instead of User Id and Password.
        // Demo examples that depend on the simulated rows and delay are skipped when you use a real connection.
        // A real application creates one connection per request, not one for the whole program as this demo does.
        // ---------------------------------------------------------------------
        // internal static readonly DbConnection Connection = new Microsoft.Data.SqlClient.SqlConnection("Server=YOUR_SERVER;Database=YOUR_DATABASE;User Id=YOUR_LOGIN;Password=YOUR_PASSWORD;TrustServerCertificate=true");
        // internal static readonly DbConnection Connection = new Microsoft.Data.SqlClient.SqlConnection("Server=localhost;Database=AdventureWorksLT2025;User Id=sa;Password=;TrustServerCertificate=true");
        internal static readonly DbConnection Connection = new SimulatedDatabaseConnectionObject();

        // True when Connection is the simulated one, false for a real database. This is for Demo Only  
        internal static bool Simulated => Connection is SimulatedDatabaseConnectionObject;

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            try
            {
                while (true)
                {
                    Console.WriteLine("=========================================================");
                    Console.WriteLine("=========== DataFenceComponents.com Demo ================");
                    Console.WriteLine("[Licensing Status] " + DataFenceLicense.Status);
                    Console.WriteLine("[Version] " + DataFenceInfo.Description);
                    Console.WriteLine("=========================================================");
                    Console.WriteLine("1. Quick Start (setup once, then Process each AI request)");
                    Console.WriteLine("2. PromptGuardrails");
                    Console.WriteLine("3. SqlGuardrails");
                    Console.WriteLine("4. SchemaGuardrails");
                    Console.WriteLine("5. ParameterGuardrails");
                    Console.WriteLine("6. DatabaseGuardrails");
                    Console.WriteLine("7. ResultGuardrails");
                    Console.WriteLine("8. AuditLogger (audit trail of every AI request)");
                    Console.WriteLine("9. Common Features (all components)");
                    Console.WriteLine("10. DataFenceGuardrails (all of the above in one call)");
                    Console.WriteLine("11. Manage License");
                    Console.WriteLine("12. Exit");
                    Console.Write("Select an option: ");

                    string? choice = Console.ReadLine()?.Trim();
                    Console.WriteLine();

                    try
                    {
                        switch (choice)
                        {
                            case "1":
                                Tests.QuickStartDemo.StartDemo();
                                break;
                            case "2":
                                Tests.PromptGuardrailsDemo.StartDemo();
                                break;
                            case "3":
                                Tests.SqlGuardrailsDemo.StartDemo();
                                break;
                            case "4":
                                Tests.SchemaGuardrailsDemo.StartDemo();
                                break;
                            case "5":
                                Tests.ParameterGuardrailsDemo.StartDemo();
                                break;
                            case "6":
                                Tests.DatabaseGuardrailsDemo.StartDemo();
                                break;
                            case "7":
                                Tests.ResultGuardrailsDemo.StartDemo();
                                break;
                            case "8":
                                Tests.AuditLoggerDemo.StartDemo();
                                break;
                            case "9":
                                await Tests.CommonFeaturesDemo.StartDemo();
                                break;
                            case "10":
                                Tests.DataFenceGuardrailsDemo.StartDemo();
                                break;
                            case "11":
                                ManageLicense();
                                break;
                            case "12":
                                return;
                            default:
                                Console.WriteLine("Invalid selection.\n");
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\n***The demo stopped with an error: {ex.GetType().Name}: {ex.Message}\n");
                    }
                }
            }
            finally
            {
                // You created the connection, so you dispose it. DatabaseGuardrails never creates or disposes it.
                Connection.Dispose();
            }
        }

        static void ManageLicense()
        {
            while (true)
            {
                Console.WriteLine("=============== Manage License ===============");
                Console.WriteLine(DataFenceLicense.LicenseSummary ?? "Not activated.");
                Console.WriteLine("1. Activate your License");
                Console.WriteLine("2. Deactivate your License");
                Console.WriteLine("3. About License (version and support report)");
                Console.WriteLine("4. Back");
                Console.Write("Select an option: ");

                string? choice = Console.ReadLine()?.Trim();
                Console.WriteLine();

                switch (choice)
                {
                    case "1":
                        Console.Write("Enter your license key: ");
                        DataFenceLicense.TryActivate(Console.ReadLine()?.Trim() ?? string.Empty, out string activateMessage);
                        Console.WriteLine(activateMessage + "\n");
                        break;
                    case "2":
                        DataFenceLicense.TryDeactivate(out string deactivateMessage);
                        Console.WriteLine(deactivateMessage + "\n");
                        break;
                    case "3":
                        Console.WriteLine(DataFenceInfo.Report());
                        break;
                    case "4":
                        return;
                    default:
                        Console.WriteLine("Invalid selection.\n");
                        break;
                }
            }
        }
    }
}
