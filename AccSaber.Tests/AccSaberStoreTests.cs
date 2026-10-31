using AccSaber.API;
using AccSaber.Managers;
using AccSaber.Models;
using System;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AccSaber.Tests
{
    public class AccSaberStoreTests
    {
        [Fact]
        public void CurrentRankedMap_RaisesUpdateForEveryAssignment()
        {
            var store = CreateStore();
            int calls = 0;
            AccSaberBasicDifficulty? observed = null;
            store.OnAccSaberRankedMapUpdated += value => { calls++; observed = value; };

            store.CurrentRankedMap = null;

            Assert.Equal(1, calls);
            Assert.Null(observed);
            Assert.Null(store.CurrentRankedMap);
        }

        [Fact]
        public async Task ListenForScores_ConnectsToConfiguredEndpointAndDispatchesTextScore()
        {
            var socket = new ScriptedWebSocket();
            var store = CreateStore(socket);
            var entry = new TaskCompletionSource<AccSaberLeaderboardEntry>(TaskCreationOptions.RunContinuationsAsynchronously);
            void handler(AccSaberLeaderboardEntry value) => entry.TrySetResult(value);
            AccSaberStore.OnScoreUpdated += handler;
            try
            {
                byte[] payload = Encoding.UTF8.GetBytes("{\"userId\":\"player-7\",\"userName\":\"Test Player\",\"score\":12345}");
                socket.Enqueue(payload, WebSocketMessageType.Text, true);
                socket.EnqueueClose();

                await CompletesWithin(store.ListenForScores(CancellationToken.None), TimeSpan.FromSeconds(3));
                AccSaberLeaderboardEntry score = await CompletesWithin(entry.Task, TimeSpan.FromSeconds(1));

                Assert.Equal(new Uri(HelpfulPaths.APAPI_WEBSOCKET), socket.ConnectedUri);
                Assert.Equal("player-7", score.PlayerId);
                Assert.Equal("Test Player", score.PlayerName);
                Assert.Equal(12345, score.Score);
                Assert.Equal(1, socket.CloseOutputCalls);
                Assert.True(socket.Disposed);
            }
            finally { AccSaberStore.OnScoreUpdated -= handler; }
        }

        [Fact]
        public async Task ListenForScores_ReassemblesFragmentedTextFrames()
        {
            var socket = new ScriptedWebSocket();
            var store = CreateStore(socket);
            var entry = new TaskCompletionSource<AccSaberLeaderboardEntry>(TaskCreationOptions.RunContinuationsAsynchronously);
            void handler(AccSaberLeaderboardEntry value) => entry.TrySetResult(value);
            AccSaberStore.OnScoreUpdated += handler;
            try
            {
                byte[] payload = Encoding.UTF8.GetBytes("{\"userId\":\"fragmented\",\"userName\":\"Frame\",\"score\":42}");
                int split = payload.Length / 2;
                socket.Enqueue(payload, 0, split, WebSocketMessageType.Text, false);
                socket.Enqueue(payload, split, payload.Length - split, WebSocketMessageType.Text, true);
                socket.EnqueueClose();

                await CompletesWithin(store.ListenForScores(CancellationToken.None), TimeSpan.FromSeconds(3));
                var score = await CompletesWithin(entry.Task, TimeSpan.FromSeconds(1));
                Assert.Equal("fragmented", score.PlayerId);
                Assert.Equal(42, score.Score);
                Assert.Equal(1, socket.ReceiveCallsForMessage);
            }
            finally { AccSaberStore.OnScoreUpdated -= handler; }
        }

        [Fact]
        public async Task ListenForScores_CancellationPropagatesAndDisposesSocket()
        {
            var socket = new ScriptedWebSocket();
            var store = CreateStore(socket);
            using var cts = new CancellationTokenSource();
            socket.EnqueueCancellation();
            socket.OnReceive = () => cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CompletesWithin(store.ListenForScores(cts.Token), TimeSpan.FromSeconds(3)));
            Assert.True(socket.Disposed);
        }

        private static AccSaberStore CreateStore(ScriptedWebSocket? socket = null)
        {
            var store = (AccSaberStore)FormatterServices.GetUninitializedObject(typeof(AccSaberStore));
            typeof(AccSaberStore).GetField("_webSocketFactory", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(store, new Func<AccSaberStore.IScoreWebSocket>(() => socket ?? new ScriptedWebSocket()));
            return store;
        }

        private static async Task CompletesWithin(Task task, TimeSpan timeout)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(timeout));
            Assert.Same(task, completed);
            await task;
        }

        private static async Task<T> CompletesWithin<T>(Task<T> task, TimeSpan timeout)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(timeout));
            Assert.Same(task, completed);
            return await task;
        }

        private sealed class ScriptedWebSocket : AccSaberStore.IScoreWebSocket
        {
            private readonly System.Collections.ArrayList _frames = [];
            private WebSocketState _state = WebSocketState.None;
            public Uri? ConnectedUri { get; private set; }
            public int CloseOutputCalls { get; private set; }
            public int ReceiveCallsForMessage { get; private set; }
            public bool Disposed { get; private set; }
            public Action? OnReceive { get; set; }
            public TimeSpan KeepAliveInterval { get; set; }

            public void Enqueue(byte[] data, WebSocketMessageType type, bool end) => Enqueue(data, 0, data.Length, type, end);
            public void Enqueue(byte[] data, int offset, int count, WebSocketMessageType type, bool end) => _frames.Add((data, offset, count, type, end, false));
            public void EnqueueClose() => _frames.Add(((byte[]?)null, 0, 0, WebSocketMessageType.Close, true, false));
            public void EnqueueCancellation() => _frames.Add(((byte[]?)null, 0, 0, WebSocketMessageType.Text, true, true));

            public WebSocketState State => _state;

            public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
            {
                ConnectedUri = uri;
                _state = WebSocketState.Open;
                return Task.CompletedTask;
            }

            public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            {
                OnReceive?.Invoke();
                var (data, offset, count, type, end, cancel) = ((byte[]? data, int offset, int count, WebSocketMessageType type, bool end, bool cancel))_frames[0];
                _frames.RemoveAt(0);
                if (cancel)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new OperationCanceledException(cancellationToken);
                }
                if (type == WebSocketMessageType.Close)
                {
                    _state = WebSocketState.CloseReceived;
                    return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true, WebSocketCloseStatus.NormalClosure, "server close"));
                }
                Buffer.BlockCopy(data, offset, buffer.Array, buffer.Offset, count);
                if (end) ReceiveCallsForMessage++;
                return Task.FromResult(new WebSocketReceiveResult(count, type, end));
            }

            public Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            {
                CloseOutputCalls++;
                _state = WebSocketState.Closed;
                return Task.CompletedTask;
            }

            public void Dispose() { Disposed = true; _state = WebSocketState.Closed; }
        }
    }
}
