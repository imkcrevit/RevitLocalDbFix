using System;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using RevitLocalDbFix.Core.Config;

namespace RevitLocalDbFix.Core.Reinstall
{
    public sealed class DownloadResult
    {
        public bool Success { get; set; }
        public string FilePath { get; set; }
        public long SizeBytes { get; set; }
        public bool? SignatureValid { get; set; }
        public string SignatureSubject { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Step 2.5.5 (SPEC §5.2): downloads the LocalDB installer. Network failure never blocks the
    /// wizard — the caller must offer "I already have the msi" and the official link (SPEC §9 rule 10).
    /// </summary>
    public sealed class LocalDbDownloader
    {
        static LocalDbDownloader()
        {
            // Older Windows defaults may lack TLS 1.2.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        /// <summary>SQL 2014 SP3 LocalDB msi via the direct Microsoft link (DL_LOCALDB_2014).</summary>
        public async Task<DownloadResult> Download2014(string targetDir, IProgress<double> progress)
        {
            var result = new DownloadResult();
            try
            {
                Directory.CreateDirectory(targetDir);
                result.FilePath = Path.Combine(targetDir, "SqlLocalDB_2014_SP3_x64.msi");

                using (var client = new WebClient())
                {
                    if (progress != null)
                        client.DownloadProgressChanged += (s, e) => progress.Report(e.ProgressPercentage / 100.0);
                    await client.DownloadFileTaskAsync(new Uri(Links.DL_LOCALDB_2014), result.FilePath)
                                .ConfigureAwait(false);
                }

                result.SizeBytes = new FileInfo(result.FilePath).Length;
                string subject;
                result.SignatureValid = VerifyMicrosoftSignature(result.FilePath, out subject);
                result.SignatureSubject = subject;
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.GetType().Name + ": " + ex.Message;
            }
            return result;
        }

        /// <summary>
        /// TODO(M4) [待实测 SPEC §13-3]: SSEI bootstrapper direct link and silent media download:
        /// SQL2019-SSEI-Expr.exe /ACTION=Download /MEDIATYPE=LocalDB /MEDIAPATH=&lt;dir&gt; /QUIET
        /// Until verified, this reports failure so the UI falls back to the manual msi path.
        /// </summary>
        public Task<DownloadResult> Download2019(string targetDir, IProgress<double> progress)
        {
            return Task.FromResult(new DownloadResult
            {
                Success = false,
                Error = "Automatic SQL 2019 LocalDB download is pending field verification (SPEC §13-3). " +
                        "Use the manual msi option or the official page: " + Links.DL_LOCALDB_2019
            });
        }

        public bool VerifyMicrosoftSignature(string filePath)
        {
            string subject;
            return VerifyMicrosoftSignature(filePath, out subject);
        }

        /// <summary>
        /// Basic Authenticode check: signed file, chain builds, subject is Microsoft Corporation.
        /// TODO(M4): replace with full WinVerifyTrust validation.
        /// </summary>
        public bool VerifyMicrosoftSignature(string filePath, out string subject)
        {
            subject = null;
            try
            {
                var signer = new X509Certificate2(X509Certificate.CreateFromSignedFile(filePath));
                subject = signer.Subject;

                bool chainOk;
                using (var chain = new X509Chain())
                {
                    chainOk = chain.Build(signer);
                }

                return chainOk &&
                       subject.IndexOf("O=Microsoft Corporation", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
