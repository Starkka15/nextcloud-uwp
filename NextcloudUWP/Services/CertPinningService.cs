using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Web.Http;
using Windows.Web.Http.Filters;

namespace NextcloudUWP.Services
{
    /// <summary>
    /// Checks and pins TLS certificate thumbprints for Nextcloud server connections.
    /// Uses Windows.Web.Http for cert inspection; does not replace the main HTTP clients.
    /// </summary>
    public class CertPinningService
    {
        public class CertInfo
        {
            public string Thumbprint { get; set; }   // SHA-256 hex
            public string Subject    { get; set; }
            public string Issuer     { get; set; }
            public DateTimeOffset Expiry { get; set; }
            public bool IsValid      { get; set; }
        }

        /// <summary>Fetches cert info for serverUrl without validating the chain.</summary>
        public static async Task<CertInfo> GetCertInfoAsync(string serverUrl)
        {
            CertInfo captured = null;

            var filter = new HttpBaseProtocolFilter();
            filter.ServerCustomValidationRequested += (sender, args) =>
            {
                var cert = args.ServerCertificate;
                if (cert != null)
                {
                    captured = new CertInfo
                    {
                        Subject  = cert.Subject,
                        Issuer   = cert.Issuer,
                        Expiry   = cert.ValidTo,
                        IsValid  = args.ServerCertificateErrors.Count == 0,
                        Thumbprint = ComputeThumbprint(cert)
                    };
                }
                // Not calling Reject() = connection accepted.
            };

            try
            {
                using (var client = new HttpClient(filter))
                {
                    var uri = new Uri(serverUrl.TrimEnd('/') + "/status.php");
                    var req = new HttpRequestMessage(HttpMethod.Get, uri);
                    var response = await client.SendRequestAsync(req, HttpCompletionOption.ResponseHeadersRead);
                    response.Dispose();
                }
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(CertPinningService), ex); }

            return captured;
        }

        /// <summary>
        /// Verifies the server's current cert thumbprint against the stored pin.
        /// Returns (matches: true, info) if no pin stored or if thumbprint matches.
        /// Returns (matches: false, info) if thumbprint has changed.
        /// </summary>
        public static async Task<(bool Matches, CertInfo Info)> VerifyPinnedAsync(
            string serverUrl, string pinnedThumbprint)
        {
            var info = await GetCertInfoAsync(serverUrl);
            if (info == null) return (true, null);                        // can't check — allow
            if (string.IsNullOrEmpty(pinnedThumbprint)) return (true, info); // nothing pinned
            return (info.Thumbprint == pinnedThumbprint, info);
        }

        private static string ComputeThumbprint(
            Windows.Security.Cryptography.Certificates.Certificate cert)
        {
            try
            {
                var derBytes = cert.GetCertificateBlob().ToArray();
                var input    = CryptographicBuffer.CreateFromByteArray(derBytes);
                var hasher   = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha256);
                var hash     = hasher.HashData(input);
                var bytes    = new byte[hash.Length];
                CryptographicBuffer.CopyToByteArray(hash, out bytes);
                return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }
            catch (Exception ex) { DebugLogger.LogException(nameof(CertPinningService), ex); return string.Empty; }
        }
    }
}
