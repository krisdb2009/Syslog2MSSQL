using Microsoft.Data.SqlClient;
using SyslogDecode.Common;
using SyslogDecode.Model;
using SyslogDecode.Udp;
using System.Net;
using System.Reflection;

namespace Syslog2MSSQL
{
    internal class Program
    {
        static SyslogUdpPipeline Pipeline = new(IPAddress.Any);
        static void Main(string[] args)
        {
            Assembly self = Assembly.GetExecutingAssembly();
            Console.WriteLine(
                self.GetCustomAttribute<AssemblyTitleAttribute>().Title +
                " v" +
                self.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion
            );
            Console.WriteLine("Starting listener...");
            Pipeline.StreamParser.ItemProcessed += StreamParser_ItemProcessed;
            Pipeline.Start();
            Console.WriteLine("Running.");
            Thread.Sleep(-1);
        }
        private static void StreamParser_ItemProcessed(object? sender, ItemEventArgs<ParsedSyslogMessage> e)
        {
            Console.Write("*");
            Task.Run(() => {
                try
                {
                    SqlConnection sql = new(Environment.GetEnvironmentVariable("S2M_CONNECTIONSTRING"));
                    sql.Open();
                    SqlCommand cmd = sql.CreateCommand();
                    cmd.CommandText = "INSERT INTO [logs] ([time], [host], [severity], [facility], [application], [process], [message]) VALUES (@TIME@, @HOST@, @SEVERITY@, @FACILITY@, @APPLICATION@, @PROCESS@, @MESSAGE@)";
                    cmd.Parameters.AddWithValue("@TIME@", (object)e.Item.Header.Timestamp ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@HOST@", (object)e.Item.Header.HostName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SEVERITY@", (object)e.Item.Severity.ToString() ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@FACILITY@", (object)e.Item.Facility.ToString() ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@APPLICATION@", (object)e.Item.Header.AppName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@PROCESS@", (object)e.Item.Header.ProcId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@MESSAGE@", (object)e.Item.Message ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                    sql.Close();
                    Console.Write("#");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                }
            });
        }
    }
}