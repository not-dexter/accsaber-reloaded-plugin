using Newtonsoft.Json;
using System;
using System.Net.Http;
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

        /// <summary>
        /// Configures the shared HTTP client with authentication headers using the provided access and refresh tokens.
        /// </summary>
        /// <remarks>Sets the Authorization header to a Bearer token and adds a Cookie header containing
        /// accessToken and refreshToken. Mutates the shared static HttpClient's DefaultRequestHeaders; calling
        /// repeatedly may append duplicate Cookie headers.</remarks>
        /// <param name="authInfo">Authentication information containing AccessToken and RefreshToken used to set the Authorization and Cookie
        /// headers.</param>
        internal static void SetAuthForClient(AccsaberAPI.AuthInfo authInfo)
        {
            client.DefaultRequestHeaders.Add("Cookie", $"accessToken={authInfo.AccessToken}; refreshToken={authInfo.RefreshToken}");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authInfo.AccessToken);
        }
        /// <summary>
        /// Sends the given <see cref="HttpRequestMessage"/> using the shared HTTP client with built-in
        /// throttling, retry and timeout handling.
        /// </summary>
        /// <remarks>
        /// Behaviour details:
        /// - If a <see cref="Throttler"/> is provided it will be awaited before each request attempt.
        /// - Respects the provided <paramref name="ct"/> (<see cref="CancellationToken"/>); if cancelled the
        ///   call returns immediately with failure.
        /// - HTTP 4xx responses are considered client errors and will not be retried (the method will return failure).
        /// - On <see cref="TaskCanceledException"/> (typically a request timeout) the method will attempt to
        ///   recover by forcing the connection to close on the next attempt (by setting the request header
        ///   <c>Connection: close</c>) and adjusting the retry counter to allow an additional attempt.
        /// - For other transient exceptions the method will perform exponential backoff retries. The initial
        ///   retry delay is 500 ms and doubles for each subsequent retry.
        /// - Logging is performed through <c>Plugin.Log</c>. Set <paramref name="quiet"/> to true to suppress
        ///   non-critical logs (info/error/debug messages will be minimized).
        /// </remarks>
        /// <param name="request">The prepared <see cref="HttpRequestMessage"/> to send. Must be    non-null.</param>
        /// <param name="throttler">Optional throttler used to rate-limit API calls. If provided, <c>Call()</c> is awaited before sending.</param>
        /// <param name="quiet">When true, reduce logging output (useful for bulk or background requests).</param>
        /// <param name="maxRetries">Maximum number of attempts (including the initial attempt). Defaults to 3.</param>
        /// <param name="ct">Cancellation token used to cancel the operation.</param>
        /// <returns>
        /// A tuple where <c>Success</c> is true when a successful HTTP response was received and <c>Content</c>
        /// contains the response content. On failure <c>Success</c> is false and <c>Content</c> is null.
        /// </returns>
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

        public static async Task<T?> CallAPI_Json<T>(string path, Throttler? t = null, bool quiet = false, int maxRetries = 3, CancellationToken ct = default)
        {
            string? dataStr = await CallAPI_String(path, t, quiet, maxRetries, ct).ConfigureAwait(false);

            if (string.IsNullOrEmpty(dataStr))
                return default;

            return JsonConvert.DeserializeObject<T>(dataStr!);
        }
    }
}
