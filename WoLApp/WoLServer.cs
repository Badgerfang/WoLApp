using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WoLApp
{
    class WoLServer : IDisposable
    {
        private const int HeartbeatTimerDurationInMilliseconds = 1000;
        public static WoLServer Instance { get; } = new WoLServer();

        private ConcurrentDictionary<string, WoLClient> bridges = new ConcurrentDictionary<string, WoLClient>();

        private int listenPort;

        private TcpListener server;

        private Task listenTask;

        private System.Timers.Timer heartbeatTimer;
        private int heartbeatMutex;


        private object mutex = new object();


        private CancellationTokenSource listenCancellationTokenSource;
        private bool disposedValue;

        public void Start(int port)
        {
            if (listenCancellationTokenSource == null)
            {
                listenPort = port;
                listenCancellationTokenSource = new CancellationTokenSource();

                listenTask = Task.Run(() => Listen());
            }
        }

        public void Stop()
        {
            try
            {
                listenCancellationTokenSource?.Cancel();
                server?.Stop();
            }
            catch { }

            server = null;
            StopClients();
        }

        private void StopClients()
        {
            WoLClient.StopAll();
        }

        private async void Listen()
        {
            server = null;

            try
            {
                server = new TcpListener(IPAddress.Any, listenPort);
                server.Start();

                // Now that the listener has started initialise any bridge connections
                var bridges = BridgeLookup.Instance.Bridges();

                if (bridges.Count > 0)
                {
                    foreach (var bridge in bridges)
                    {
                        Logger.Instance.Info($"'{bridge.Key}' bridge defined as a connection to {bridge.Value.Remote}");
                        var connectionCommands = new List<KeyValuePair<int, string>>() { new KeyValuePair<int, string>(WoLHelper.Name, bridge.Key) };

                        if (bridge.Value.Additional != null && bridge.Value.Additional.Length > 0)
                        {
                            if (uint.TryParse(bridge.Value.Additional[0], out var heartbeat) == true)
                                connectionCommands.Add(new KeyValuePair<int, string>(WoLHelper.HeartbeatRequest, bridge.Value.Additional[0]));
                        }

                        var bridgeClient = new WoLClient(bridge.Value.Remote, WoLClient.ClientType.BridgeClientSide, connectionCommands);

                        bridgeClient.ReadBridgeCommands(bridge.Key);
                    }
                }


                bool loop = true;
                do
                {
                    try
                    {
                        var socket = await server.AcceptSocketAsync();

                        if (socket != null)
                        {
                            var client = new WoLClient(socket);
                            client.Read();
                        }
                    }
                    catch
                    {
                        loop = false;
                    }
                }
                while (loop == true);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"Failed to listen. {ex.Message}");
            }

            if (listenCancellationTokenSource.IsCancellationRequested == false)
            {
                try
                {
                    server?.Stop();
                }
                catch { }

                StopClients();
            }
        }

        /// <summary>
        /// Adds a bridge connection
        /// </summary>
        /// <param name="name">The name of the bridge</param>
        /// <param name="bridge">The client supporting the bridge</param>
        public void AddBridge(string name, WoLClient bridge)
        {
            if (string.IsNullOrEmpty(name) == false)
            {
                bridges[name] = bridge;
                Logger.Instance.Info($"Connection changed to a bridge {bridge.Remote}");
            }
        }

        /// <summary>
        /// Removes a bridge connection
        /// </summary>
        /// <param name="name">The name of the bridge</param>
        /// <param name="bridge">The client that supported the bridge</param>
        public void RemoveBridge(string name, WoLClient bridge)
        {
            try
            {
                if (string.IsNullOrEmpty(name) == false)
                {
                    if (bridges.TryRemove(name, out var _) == true)
                        Logger.Instance.Info($"Removed Bridge connection from {bridge.Remote}");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"Failed to remove bridge. {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a client that is supporting the bridge
        /// </summary>
        /// <param name="name">The name of the bridge</param>
        /// <returns>A client associated with the bridge name or null</returns>
        public WoLClient GetBridge(string name)
        {
            if (bridges.TryGetValue(name, out var client) == true)
                return client;

            return null;
        }

        public void StartTimer()
        {
            lock (mutex)
            {
                if (heartbeatTimer == null)
                {
                    heartbeatTimer = new System.Timers.Timer(HeartbeatTimerDurationInMilliseconds);
                    heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;
                    heartbeatTimer.Start();
                }
            }
        }

        private void HeartbeatTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            // Ensure only one is performed
            if (Interlocked.Increment(ref heartbeatMutex) == 1)
            {
                try
                {
                    var heartbeatClients = bridges.Values.Where(x => x.HeartbeatRequired == true);

                    foreach (var client in heartbeatClients)
                        client.Heartbeat(HeartbeatTimerDurationInMilliseconds);
                }
                finally
                {
                    heartbeatMutex = 0;
                }
            }
        }

        private void Timer()
        {
            for (; ; )
            {
                WoLClient.CancelToken.WaitHandle.WaitOne(1000);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (heartbeatTimer != null)
                    {
                        heartbeatTimer.Stop();
                        heartbeatTimer.Elapsed -= HeartbeatTimer_Elapsed;
                        heartbeatTimer.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
