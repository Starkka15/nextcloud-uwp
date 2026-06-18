using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage;

namespace NextcloudUWP.Services
{
    public class CertPinningHandler : DelegatingHandler
    {
        private readonly string _serverUrl;

        public CertPinningHandler(string serverUrl)
        {
            _serverUrl = serverUrl;
            InnerHandler = new HttpClientHandler();
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var pinned = ApplicationData.Current.LocalSettings.Values["PinnedCertThumbprint"] as string;
            if (!string.IsNullOrEmpty(pinned))
            {
                var (matches, _) = await CertPinningService.VerifyPinnedAsync(_serverUrl, pinned);
                if (!matches)
                    throw new SecurityException(
                        "Certificate mismatch — the server's TLS certificate has changed. " +
                        "Go to Settings → Certificate Pinning to verify or update the pin.");
            }
            return await base.SendAsync(request, cancellationToken);
        }
    }

    public class SecurityException : Exception
    {
        public SecurityException(string message) : base(message) { }
    }
}
