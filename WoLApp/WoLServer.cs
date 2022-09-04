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
    class WoLServer
    {
        public static WoLServer Instance { get; } = new WoLServer();

        private ConcurrentDictionary<string, WoLClient> bridges = new ConcurrentDictionary<string, WoLClient>();

        private int listenPort;

        private TcpListener server;

        private Task listenTask;



        private CancellationTokenSource listenCancellationTokenSource;

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
                        Logger.Instance.Info($"'{bridge.Key}' bridge defined as a connection to {bridge.Value}");
                        var connectionCommands = new List<KeyValuePair<int, string>>() { new KeyValuePair<int, string>(WoLHelper.Name, bridge.Key) };
                        var bridgeClient = new WoLClient(bridge.Value, WoLClient.ClientType.BridgeClientSide, connectionCommands);
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
    }
}
