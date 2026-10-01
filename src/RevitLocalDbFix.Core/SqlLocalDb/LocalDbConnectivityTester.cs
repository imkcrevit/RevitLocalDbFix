using System;
using System.Data.SqlClient;
using System.Diagnostics;

namespace RevitLocalDbFix.Core.SqlLocalDb
{
    public sealed class ConnectivityResult
    {
        public bool Success { get; set; }
        public string ServerVersion { get; set; }
        public string Error { get; set; }
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Real connection check, a tool addition beyond the articles (SPEC §5.2-2.6):
    /// opens (localdb)\&lt;instance&gt; with integrated security and runs SELECT @@VERSION.
    /// [待实测 SPEC §13-5: System.Data.SqlClient against v15 on machines with only LocalDB 2019.]
    /// </summary>
    public sealed class LocalDbConnectivityTester
    {
        public ConnectivityResult Test(string instanceName, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                int seconds = Math.Max(1, (int)timeout.TotalSeconds);
                string cs = "Server=(localdb)\\" + instanceName + ";Integrated Security=True;Connection Timeout=" + seconds;
                using (var conn = new SqlConnection(cs))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT @@VERSION", conn))
                    {
                        var version = cmd.ExecuteScalar() as string;
                        sw.Stop();
                        return new ConnectivityResult
                        {
                            Success = true,
                            ServerVersion = (version ?? string.Empty).Trim(),
                            Duration = sw.Elapsed
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new ConnectivityResult
                {
                    Success = false,
                    Error = ex.GetType().Name + ": " + ex.Message,
                    Duration = sw.Elapsed
                };
            }
        }
    }
}
