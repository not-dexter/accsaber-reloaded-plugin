using AccSaber.API;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AccSaber.Tests
{
    // APIHandler owns static client and health-cache state; keep its tests serialized.
    [CollectionDefinition("APIHandler", DisableParallelization = true)]
    public sealed class APIHandlerCollection
    {
        public const string Name = "APIHandler";
    }

    [Collection(APIHandlerCollection.Name)]
    public sealed class APIHandlerTests
    {
        private static int nextHost;

        private static string NewUrl() => "https://api-handler-test-" + Interlocked.Increment(ref nextHost) + ".invalid/resource";

        private static HttpResponseMessage Response(HttpStatusCode status, string content = "") =>
            new(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };

        private static void UseHandler(HttpMessageHandler handler) => APIHandler.SetHttpMessageHandlerForTests(handler);

        [Fact]
        public void ClientTimeout_ReadsAndWritesTheRealSharedClient()
        {
            UseHandler(new StubHandler((_, __) => Task.FromResult(Response(HttpStatusCode.OK))));

            Assert.Equal(TimeSpan.FromSeconds(5), APIHandler.ClientTimeout);
            APIHandler.ClientTimeout = TimeSpan.FromSeconds(12);
            Assert.Equal(TimeSpan.FromSeconds(12), APIHandler.ClientTimeout);
        }

        [Fact]
        public async Task CallAPI_String_ReturnsResponseBody()
        {
            UseHandler(new StubHandler((_, __) => Task.FromResult(Response(HttpStatusCode.OK, "hello from handler"))));

            string? result = await APIHandler.CallAPI_String(NewUrl(), null, quiet: true);

            Assert.Equal("hello from handler", result);
        }

        [Fact]
        public async Task CallAPI_Json_DeserializesTheResponseBody()
        {
            UseHandler(new StubHandler((_, __) => Task.FromResult(Response(HttpStatusCode.OK, "{\"value\":42}"))));

            TestPayload? result = await APIHandler.CallAPI_Json<TestPayload>(NewUrl(), null, quiet: true);

            Assert.NotNull(result);
            Assert.Equal(42, result!.Value);
        }

        [Fact]
        public async Task CallAPI_Bytes_ReturnsBodyAndContentHeaders()
        {
            const string body = "binary payload";
            UseHandler(new StubHandler((_, __) => Task.FromResult(Response(HttpStatusCode.OK, body))));

            var (data, headers) = await APIHandler.CallAPI_Bytes(NewUrl(), null, quiet: true);

            Assert.Equal(Encoding.UTF8.GetBytes(body), data);
            Assert.NotNull(headers);
            Assert.Equal("application/json; charset=utf-8", headers!.ContentType!.ToString());
        }

        [Fact]
        public async Task CallAPI_DoesNotRetryAClientError()
        {
            int sends = 0;
            UseHandler(new StubHandler((_, __) =>
            {
                sends++;
                return Task.FromResult(Response(HttpStatusCode.NotFound));
            }));

            var (success, content) = await APIHandler.CallAPI(NewUrl(), null, quiet: true, maxRetries: 3);

            Assert.False(success);
            Assert.Null(content);
            Assert.Equal(1, sends);
        }

        [Fact]
        public async Task CallAPI_RetriesServerErrorAndReturnsLaterSuccess()
        {
            int sends = 0;
            UseHandler(new StubHandler((_, __) =>
            {
                sends++;
                return Task.FromResult(sends == 1
                    ? Response(HttpStatusCode.ServiceUnavailable)
                    : Response(HttpStatusCode.OK, "recovered"));
            }));

            string? result = await APIHandler.CallAPI_String(NewUrl(), null, quiet: true, maxRetries: 2);

            Assert.Equal("recovered", result);
            Assert.Equal(2, sends);
        }

        [Fact]
        public async Task CallAPI_CanceledTokenDoesNotSendARequest()
        {
            int sends = 0;
            UseHandler(new StubHandler((_, __) =>
            {
                sends++;
                return Task.FromResult(Response(HttpStatusCode.OK));
            }));
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            var (success, content) = await APIHandler.CallAPI(NewUrl(), null, quiet: true, ct: cancellation.Token);

            Assert.False(success);
            Assert.Null(content);
            Assert.Equal(0, sends);
        }

        [Fact]
        public async Task CallAPI_SendsRequestMethodAndUriThroughRealHandler()
        {
            HttpMethod? method = null;
            Uri? uri = null;
            UseHandler(new StubHandler((request, __) =>
            {
                method = request.Method;
                uri = request.RequestUri;
                return Task.FromResult(Response(HttpStatusCode.OK));
            }));
            string url = NewUrl();

            var (success, _) = await APIHandler.CallAPI(new HttpRequestMessage(HttpMethod.Post, url), null, quiet: true);

            Assert.True(success);
            Assert.Equal(HttpMethod.Post, method);
            Assert.Equal(new Uri(url), uri);
        }

        [Fact]
        public async Task ConcurrentDomainChecksSerializeHealthPingAndKeepDomainResultsIsolated()
        {
            const int callCount = 32;
            int healthCheckCount = 0;
            TaskCompletionSource<bool> healthPingStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> releaseHealthPing = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> startChecks = new(TaskCreationOptions.RunContinuationsAsynchronously);

            UseHandler(new StubHandler(async (request, _) =>
            {
                Assert.Equal("/v1/health/ping", request.RequestUri!.AbsolutePath);
                Interlocked.Increment(ref healthCheckCount);
                healthPingStarted.TrySetResult(true);
                await releaseHealthPing.Task;
                return Response(HttpStatusCode.OK, "healthy");
            }));

            Task<bool>[] checks = [.. Enumerable.Range(0, callCount)
                .Select(index => Task.Run(async () =>
                {
                    await startChecks.Task;
                    string domain = index == 0
                        ? HelpfulPaths.APAPI_DOMAIN
                        : "concurrent-test-" + index + ".invalid";
                    return await APIHandler.CheckDomain(domain);
                }))];

            startChecks.TrySetResult(true);
            await healthPingStarted.Task;
            // Keep the ping in flight while the other callers contend for the health lock.
            await Task.Delay(50);
            releaseHealthPing.TrySetResult(true);
            bool[] results = await Task.WhenAll(checks);

            Assert.Equal(1, healthCheckCount);
            Assert.All(results, Assert.True);
        }

        private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send = send;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                send(request, cancellationToken);
        }

        private sealed class TestPayload
        {
            public int Value { get; set; }
        }
    }
}
