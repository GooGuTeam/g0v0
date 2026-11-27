// Copyright (c) ppy Pty Ltd <contact@ppy.sh> & GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE & LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;

namespace osu.Game.IPC
{
    /// <summary>
    /// Implements a WebSocket server to be used for external integrations such as streaming overlays.
    /// The server can only listen on <c>localhost</c>, on the port given in the constructor.
    /// Only UTF-8 string-based messages are supported. Binary messages are not supported.
    /// String-based messages must not exceed <see cref="WebSocketChannel.max_message_size"/> bytes.
    /// </summary>
    /// <remarks>
    /// This implementation uses <see cref="HttpListener"/> internally.
    /// This is a frozen .NET API as per https://github.com/dotnet/runtime/issues/63941#issuecomment-1205259894.
    /// The reason of using this API instead of ASP.NET directly via frameworks like SignalR are as follows:
    /// <list type="bullet">
    /// <item>
    /// This is intended to be a <b>simple</b> server.
    /// There are no reliability guarantees, no delivery guarantees, no authorisation.
    /// The operation of this server is <b>best-effort</b>.
    /// Due to this, ASP.NET is surplus to requirements.
    /// </item>
    /// <item>Including ASP.NET wholesale would have a negative impact on binary size.</item>
    /// <item>
    /// Using ASP.NET could expose end users' PCs to having things enabled that shouldn't be enabled via little-known configuration toggles.
    /// One pertinent example is the <c>ASPNETCORE_URLS</c> environment variable which silently changes which endpoints an ASP.NET service listens on.
    /// </item>
    /// <item>
    /// ASP.NET does not generally fit into the paradigm of being <i>part</i> of an application.
    /// The way ASP.NET apps are structured, is that they generally <i>take over</i> the functioning of an application.
    /// Therefore, there is not necessarily a given that ASP.NET bundled inside the client will fully stop functioning even when explicitly asked.
    /// </item>
    /// </list>
    /// </remarks>
    public sealed class WebSocketServer : IDisposable
    {
        /// <summary>
        /// Whether the server is currently running and listening for connection requests.
        /// </summary>
        public bool IsRunning => handleRequestTask != null && !runningTokenSource.IsCancellationRequested;

        /// <summary>
        /// Invoked when a client is connected.
        /// The argument is the assigned ID of the client.
        /// </summary>
        public event Action<int>? ClientConnected;

        /// <summary>
        /// Invoked when a message is received.
        /// The first argument is the ID of the sender; the second is the content of the received message.
        /// </summary>
        public event Action<int, string>? MessageReceived;

        private readonly Lock syncRoot = new Lock();

        private readonly int port;
        private readonly Logger logger;

        private HttpListener? listener;
        private Task? handleRequestTask;

        private int channelCounter;
        private readonly ConcurrentDictionary<int, WebSocketChannel> channels = new ConcurrentDictionary<int, WebSocketChannel>();

        private readonly CancellationTokenSource runningTokenSource = new CancellationTokenSource();
        private bool isDisposed;

        public WebSocketServer(int port)
        {
            // Restricting to only providing a port is intentional for several reasons:
            // - Use of HTTP (no efforts are taken to make HTTPS work).
            // - Attack surface reduction (doesn't accidentally listen on all interfaces, potentially getting hit by something external).
            // Some users with setups that use a second "streaming PC" or similar will complain. They can set up proxies at their own peril.
            this.port = port;

            logger = Logger.GetLogger(@"websocket");
        }

        /// <summary>
        /// Starts the server.
        /// </summary>
        /// <param name="cancellationToken">Use this to cancel start-up.</param>
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
        {
            lock (syncRoot)
            {
                if (listener != null)
                    throw new InvalidOperationException($@"Cannot call {nameof(StartAsync)} multiple times.");

                listener = new HttpListener();
                listener.Prefixes.Add($@"http://127.0.0.1:{port}/");
                listener.Prefixes.Add($@"http://localhost:{port}/");
                listener.Start();
                handleRequestTask = Task.Run(handleRequests, cancellationToken);
                logger.Add($@"Listening on http://localhost:{port}/.");
            }
        }, cancellationToken);

        private async Task handleRequests()
        {
            Debug.Assert(listener != null);

            while (!runningTokenSource.IsCancellationRequested)
            {
                HttpListenerContext context;

                try
                {
                    // `listener.Stop()`/`Dispose()` completes the outstanding accept with an exception, so the loop can
                    // unwind without leaving an APM callback alive after the listener has been torn down.
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is ObjectDisposedException || ex is HttpListenerException)
                {
                    // the listener was stopped or disposed while waiting for an incoming request.
                    return;
                }

                var request = context.Request;
                var response = context.Response;

                if (!request.IsWebSocketRequest)
                {
                    logger.Add($@"Received non-websocket request from {request.RemoteEndPoint}. Requesting upgrade.");
                    response.StatusCode = (int)HttpStatusCode.UpgradeRequired;
                    response.Headers.Add(HttpRequestHeader.Upgrade, @"websocket");
                    response.Close();
                    continue;
                }

                HttpListenerWebSocketContext wsContext;

                try
                {
                    wsContext = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.Add($@"Failed to accept websocket connection from {request.RemoteEndPoint}.", LogLevel.Error, ex);
                    continue;
                }

                int channelId = Interlocked.Increment(ref channelCounter);
                var wsChannel = new WebSocketChannel(wsContext.WebSocket);
                channels[channelId] = wsChannel;
                wsChannel.MessageReceived += msg => MessageReceived?.Invoke(channelId, msg);
                wsChannel.ClosedPrematurely += () => onChannelClosed(channelId);
                wsChannel.Start(runningTokenSource.Token);
                logger.Add($@"Accepted websocket connection from {request.RemoteEndPoint} as client #{channelId}.");
                ClientConnected?.Invoke(channelId);
            }
        }

        private void onChannelClosed(int channelId)
        {
            if (channels.TryRemove(channelId, out var channel))
                channel.Dispose();
            logger.Add($@"Connection with client #{channelId} closed.");
        }

        /// <summary>
        /// Sends <paramref name="message"/> to the specific client with the given <paramref name="clientId"/>.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="clientId"/> is not known.</exception>
        public async Task SendAsync(int clientId, string message)
        {
            if (!channels.TryGetValue(clientId, out var channel))
                throw new ArgumentException($@"Client {clientId} is not known.");

            logger.Add($@"Sending to client {clientId}: {message}");
            await channel.SendAsync(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends <paramref name="message"/> to all connected clients.
        /// </summary>
        public Task BroadcastAsync(string message)
        {
            logger.Add($@"Broadcasting to all clients: {message}");
            return Task.WhenAll(channels.Values.Select(ch => ch.SendAsync(message)).ToArray());
        }

        /// <summary>
        /// Stops the server.
        /// </summary>
        /// <param name="stoppingToken">Cancel this to transition from a graceful shutdown to a forced shutdown.</param>
        public Task StopAsync(CancellationToken stoppingToken = default) => Task.Run(async () =>
        {
            if (isDisposed)
                return;

            logger.Add(@"Stopping websocket server...");

            // of note, ordering here is important - the token is supposed to be cancelled *before* the channels are stopped.
            // see `readWriteTask()` and the treatment of early cancellation for answer why.
            await runningTokenSource.CancelAsync().ConfigureAwait(false);

            // channels have to be stopped before the listener. stopping the listener while a receive is still pending on
            // an upgraded connection leaves an internal socket operation unobserved on the managed `HttpListener`
            // implementation, which then surfaces as an unobserved `SocketException` (and aborts whatever test is running
            // when the task is finalised). See https://github.com/dotnet/runtime/issues/126148.
            try
            {
                await Task.WhenAll(channels.Values.Select(ch => ch.StopAsync(stoppingToken)).ToArray()).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // has to be caught manually because outer task isn't accepting `stoppingToken`.
            }

            try
            {
                listener?.Stop();
            }
            catch (ObjectDisposedException)
            {
                // observed to intermittently fire on unices in unclear circumstances. tragic, but also irrelevant at this point. the point is to stop.
            }

            // the listener stop above completes any outstanding accept, so the request loop is always joined here.
            if (handleRequestTask != null)
                await handleRequestTask.ConfigureAwait(false);

            logger.Add(@"Websocket server stopped.");
        }, CancellationToken.None); // we always want this task to start running. passing `stoppingToken` here would mean potentially never even scheduling it for execution.

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;

            // no clue why this isn't accessible without casting.
            // sidebar: `Stop()` unregisters addresses on Windows, but `Abort()` doesn't!
            // this `Dispose()` implementation calls the former.
            (listener as IDisposable)?.Dispose();

            foreach (var channel in channels.Values)
                channel.Dispose();

            runningTokenSource.Dispose();
        }
    }
}
