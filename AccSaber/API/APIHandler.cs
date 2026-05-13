using AccSaber;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AccSaber.API
{
    internal static class APIHandler
    {
        private static TimeSpan clientTimeout = TimeSpan.FromSeconds(5);
        internal static TimeSpan ClientTimeout
        {
            get => clientTimeout;
            set { clientTimeout = value; client.Timeout = value; }
        }
        private static readonly HttpClient client = new()
        {
            Timeout = ClientTimeout
        };

        internal static void SetAuthForClient(AccsaberAPI.AuthInfo authInfo)
        {
            client.DefaultRequestHeaders.Add("Cookie", $"accessToken={authInfo.AccessToken}; refreshToken={authInfo.RefreshToken}");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authInfo.AccessToken);
        }
        public static async Task<(bool Success, HttpContent? Content)> CallAPI(HttpRequestMessage request, Throttler? throttler = null, bool quiet = false, int maxRetries = 3, CancellationToken ct = default)
        {
            const int initialRetryDelayMs = 500;
            bool closeRequest = false;
            if (ct.IsCancellationRequested)
            {
                Plugin.Log.Warn("API call skipped due to CancellationToken.");
                return (false, null);
            }
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (throttler != null)
                        await throttler.Call();

                    Plugin.Log.Debug("API Call: " + request.RequestUri);

                    HttpResponseMessage response;
                    if (closeRequest)
                    {
                        request.Headers.ConnectionClose = true;
                        response = await client.SendAsync(request, ct).ConfigureAwait(false);
                        request.Headers.ConnectionClose = false;
                        closeRequest = false;
                    }
                    else response = await client.SendAsync(request, ct).ConfigureAwait(false);

                    int status = (int)response.StatusCode;
                    if (status >= 400 && status < 500)
                    {
                        if (!quiet)
                            Plugin.Log.Error("API request failed, skipping retries due to error code (" + status + ").\nPath: " + request.RequestUri);
                        break;
                    }
                    response.EnsureSuccessStatusCode();

                    return (true, response.Content);
                }
                catch (Exception e)
                {
                    if (e is TaskCanceledException)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            Plugin.Log.Warn("API call has been canceled through cancel token.");
                            break;
                        }
                        if (!quiet)
                        {
                            Plugin.Log.Error($"API request failed with a TaskCanceledException, meaning the request almost certainly timed out. Clearing pool and retrying one more time.");
                            Plugin.Log.Debug(e);
                        }

                        if (closeRequest)
                            continue;

                        attempt = Math.Max(maxRetries - 2, 0);
                        closeRequest = true;
                        continue;
                    }
                    if (!quiet)
                    {
                        Plugin.Log.Error($"API request failed (attempt {attempt}/{maxRetries})\nPath: {request.RequestUri}\nError: {e.Message}");
                        Plugin.Log.Debug(e);
                    }

                    if (attempt < maxRetries)
                    {
                        // Exponential backoff delay
                        int delay = initialRetryDelayMs * (int)Math.Pow(2, attempt - 1);
                        if (!quiet) Plugin.Log.Info($"Retrying in {delay} ms...");
                        await Task.Delay(delay).ConfigureAwait(false);
                    }
                    else
                    {
                        if (!quiet)
                            Plugin.Log.Error($"API request failed after {maxRetries} attempts. Returning failure.");
                        return (false, null);
                    }
                }
            }
            return (false, null);
        }
        public static async Task<(bool Success, HttpContent? Content)> CallAPI(string path, Throttler? throttler = null, bool quiet = false, int maxRetries = 3, CancellationToken ct = default)
        {
            HttpRequestMessage request = new(HttpMethod.Get, new Uri(path));
            return await CallAPI(request, throttler, quiet, maxRetries, ct);
        }
        public static async Task<string?> CallAPI_String(string path, Throttler? t = null, bool quiet = false, int maxRetries = 3, CancellationToken ct = default)
        {
            var (Success, Content) = await CallAPI(path, t, quiet, maxRetries, ct).ConfigureAwait(false);
            if (!Success) return null;
            return await Content!.ReadAsStringAsync().ConfigureAwait(false);
        }
        public static async Task<byte[]?> CallAPI_Bytes(string path, Throttler? t = null, bool quiet = false, int maxRetries = 3, CancellationToken ct = default)
        {
            var (Success, Content) = await CallAPI(path, t, quiet, maxRetries, ct).ConfigureAwait(false);
            if (!Success) return null;
            return await Content!.ReadAsByteArrayAsync().ConfigureAwait(false);
        }
    }
}
