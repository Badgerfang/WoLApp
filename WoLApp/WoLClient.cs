using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WoLApp
{
    /// <summary>
    /// The WoL client connects to the PC and sends a serverr's wakeup comand
    /// </summary>
    internal class WoLClient
    {
        private Queue<KeyValuePair<int, string>> commands = new Queue<KeyValuePair<int, string>>();

        private object mutex = new object();

        public enum ClientType
        {
            /// <summary>
            /// When the application running in server mode has to connect to another server
            /// </summary>
            Background,

            /// <summary>
            /// When the application is used to wake a device on the same network
            /// </summary>
            Foreground,

            /// <summary>
            /// Establish a connection
            /// </summary>
            BridgeClientSide,

            /// <summary>
            /// Don't establish a connection
            /// </summary>
            BridgeServerSide,
        }

        private static CancellationTokenSource stopAllClients = new CancellationTokenSource();

        private IPEndPoint serverEndPoint;

        private NetworkStream stream;

        private List<byte[]> connectionPayloads;

        public static void StopAll() => stopAllClients.Cancel();

        public static CancellationToken CancelToken => stopAllClients.Token;



        private ClientType clientType;

        private bool NonBridgeClient => clientType == ClientType.Background || clientType == ClientType.Foreground;
        private bool BridgeClient => clientType == ClientType.BridgeClientSide || clientType == ClientType.BridgeServerSide;

        private Task bgTask;

        private Task readTask;

        private static byte[] wakeupAck = WoLHelper.Generate(WoLHelper.WakeupAck, null);

        private string bridgeName;

        internal string Remote
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(stream?.Socket?.RemoteEndPoint?.ToString() ?? "Unknown");

                if (string.IsNullOrEmpty(bridgeName) == false)
                    sb.Append($" ({bridgeName} bridge)");

                return sb.ToString();
            }
        }

        internal string RemoteInfo(IPEndPoint ep)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(ep.ToString() ?? "Unknown");

            if (string.IsNullOrEmpty(bridgeName) == false)
                sb.Append($" ({bridgeName} bridge)");

            return sb.ToString();
        }


        public WoLClient(IPEndPoint remotePC, ClientType client, List<KeyValuePair<int, string>> connectionCommands = null)
        {
            serverEndPoint = remotePC;

            if (connectionCommands != null && connectionCommands.Count > 0)
            {
                connectionPayloads = new List<byte[]>();

                foreach (var connectionCommand in connectionCommands)
                    connectionPayloads.Add(WoLHelper.Generate(connectionCommand.Key, connectionCommand.Value));
            }

            clientType = client;
        }

        public WoLClient(Socket socket)
        {
            stream = new NetworkStream(socket);
            clientType = ClientType.Background;
            Logger.Instance.Info($"Connection established from {Remote}");
        }

        /// <summary>
        /// Sends a command
        /// </summary>
        /// <param name="command">The command key</param>
        /// <param name="payload">The command's payload</param>
        /// <remarks>
        /// Since the server could accept multiple connections each of which could request to send a command over a bridge connection a mutual exclusive list is required.
        /// </remarks>
        public void Send(int command, string payload)
        {
            lock (mutex)
            {
                commands.Enqueue(new KeyValuePair<int, string>(command, payload));

                if (commands.Count == 1)
                {
                    if (clientType == ClientType.Foreground)
                    {
                        SendCommand(command, payload);                                                          // Foreground so run on the current thread

                        if (Settings.DelayAfterWoL > 0)
                            Thread.Sleep(Settings.DelayAfterWoL);

                        Disconnect();
                    }
                    else
                    {
                        if (bgTask == null)
                            bgTask = Task.Run(() => SendCommands());                                            // Send and keep the connection open
                    }
                }
            }
        }

        /// <summary>
        /// Send all available commands in the queue
        /// </summary>
        /// <remarks>
        /// This is called from the background task
        /// </remarks>
        private void SendCommands()
        {
            for (; ; )
            {
                try
                {
                    KeyValuePair<int, string>? command = null;

                    lock (mutex)
                    {
                        if (commands.Count == 0)
                        {
                            bgTask = null;
                            break;
                        }

                        command = commands.Dequeue();
                    }

                    if (command != null)
                        SendCommand(command.Value.Key, command.Value.Value);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error($"Bg task fault. {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Continue to attempt to send to the remote PC until connected and transmitted or until the connection needs to be closed
        /// </summary>
        /// <param name="command"></param>
        /// <param name="payload"></param>
        /// <param name="establishConnection"></param>
        private void SendCommand(int command, string payload)
        {
            byte[] toSend = null;

            var lastReport = DateTime.MinValue;

            const int ReportDurationInSeconds = 60;

            bool loop = true;

            while (loop == true && WoLClient.CancelToken.IsCancellationRequested == false)
            {
                loop = false;

                if (NonBridgeClient == true)
                {
                    // Need to establish a connection
                    while (stream == null)
                    {
                        var now = DateTime.UtcNow;

                        var lastReportDelta = now - lastReport;
                        var delta = lastReportDelta.TotalSeconds;
                        bool report = delta >= ReportDurationInSeconds;

                        Connect(report);

                        if (stream == null)
                        {
                            if (report == true)
                                lastReport = now;

                            Task.Delay(1000, WoLClient.CancelToken).Wait();

                            if (WoLClient.CancelToken.IsCancellationRequested == true)
                                return;
                        }
                        else
                        {
                            if (lastReport != DateTime.MinValue)
                                Logger.Instance.Info($"Connected to {Remote}");
                        }
                    }
                }

                try
                {
                    if (toSend == null)
                        toSend = WoLHelper.Generate(command, payload);

                    stream.WriteAsync(toSend, 0, toSend.Length, WoLClient.CancelToken).Wait();
                    stream.FlushAsync(WoLClient.CancelToken).Wait();

                    if (command == WoLHelper.Wakeup)
                        ReadWakeupAck();

                    if (lastReport != DateTime.MinValue)
                        Logger.Instance.Error($"Successfully sentd to {serverEndPoint}");
                }
                catch (Exception ex)
                {
                    if (lastReport == DateTime.MinValue)
                        Logger.Instance.Error($"Failed to send to {serverEndPoint}. {ex.Message}");

                    Disconnect();

                    loop = NonBridgeClient;                                                     // For a nonbridged client retry
                }
            }
        }

        /// <summary>
        /// The client is a bridge so just read commands
        /// </summary>
        public void ReadBridgeCommands(string name)
        {
            lock (mutex)
            {
                if (readTask != null)
                    return;

                bridgeName = name;
                readTask = Task.Run(ReadFromBridge);
            }
        }

        /// <summary>
        /// Constantly read from the bridge client until there's an error
        /// </summary>
        private void ReadFromBridge()
        {
            var lastReport = DateTime.MinValue;

            const int ReportDurationInSeconds = 60;

            while (WoLClient.CancelToken.IsCancellationRequested == false)
            {
                while (stream == null)
                {
                    var now = DateTime.UtcNow;

                    var lastReportDelta = now - lastReport;
                    var delta = lastReportDelta.TotalSeconds;
                    bool report = delta >= ReportDurationInSeconds;

                    Connect(report);

                    if (stream == null)
                    {
                        if (report == true)
                            lastReport = now;

                        Task.Delay(1000, WoLClient.CancelToken).Wait();

                        if (WoLClient.CancelToken.IsCancellationRequested == true)
                            return;
                    }
                }

                Logger.Instance.Info($"Connected to {Remote}");

                var headerPlusPayload = ReadCommand();

                var header = headerPlusPayload.Item1;

                if (header == null)
                {
                    Disconnect(true);

                    if (clientType == ClientType.BridgeClientSide)
                        continue;                                                           // As a bridge that connected to a service always reconnect
                            
                    WoLServer.Instance.RemoveBridge(bridgeName, this);
                    return;
                }

                var key = (int)header[0];

                var payload = headerPlusPayload.Item2;

                switch (key)
                {
                    case WoLHelper.Wakeup:
                        ProcessWakeup(payload);
                        break;

                    default:
                        Logger.Instance.Info($"Invalid command {key} received from bridge client {Remote}");
                        break;
                }
            }

            Disconnect();
        }

        public void Read()
        {
            if (stream  == null)
                return;

            lock (mutex)
            {
                if (readTask != null)
                    return;

                readTask = Task.Run(ReadTask);
            }
        }

        private void ReadTask()
        {
            bool readError = false;

            try
            {
                bool loop = false;

                do
                {
                    loop = false;

                    var headerPlusPayload = ReadCommand();

                    var header = headerPlusPayload.Item1;

                    if (header != null)
                    {
                        var key = (int)header[0];

                        var payload = headerPlusPayload.Item2;

                        switch (key)
                        {
                            case WoLHelper.Name:
                                loop = ProcessName(payload);                                            // client could be a bridged connection
                                break;

                            case WoLHelper.Wakeup:
                                ProcessWakeup(payload);
                                loop = string.IsNullOrEmpty(bridgeName) == false;
                                break;
                        }
                    }
                    else
                        readError = true;
                }
                while (loop == true && readError == false);

            }
            catch { }

            if (readError == true)
            {
                if (BridgeClient == true && string.IsNullOrEmpty(bridgeName) == false)
                    WoLServer.Instance.RemoveBridge(bridgeName, this);

                Disconnect();
            }
            else
            {
                if (NonBridgeClient == true)
                    Disconnect();
            }
        }

        private bool ProcessName(byte[] payload)
        {
            bool loop = false;

            if (string.IsNullOrEmpty(bridgeName) == true)
            {
                bridgeName = Encoding.UTF8.GetString(payload);

                WoLServer.Instance.AddBridge(bridgeName, this);
                clientType = ClientType.BridgeServerSide;                                   // Only a bridge connection that initialises the connection sends the name
                loop = true;
            }

            return loop;
        }


        private (byte[], byte[]) ReadCommand()
        {
            var header = Read(3);                                    // Key and length

            if (header != null)
            {
                int length = header[1] << 8 | header[2];

                var payload = Read(length);
                return (header, payload);
            }

            return (null, null);
        }

        private byte[] Read(int length)
        {
            var array = new byte[length];

            var success = Read(array, length);

            if (success == false)
                return null;

            return array;
        }

        private bool Read(byte[] array, int length)
        {
            int remaining = length;

            int offset = 0;

            while (remaining > 0)
            {
                try
                {
                    var task = stream.ReadAsync(array, offset, remaining, WoLClient.CancelToken);
                    task.Wait();

                    int read = task.Result;

                    if (read <= 0)
                        return false;

                    offset += read;
                    remaining -= read;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    return false;
                }
            }

            return true;
        }

        private void ProcessWakeup(byte[] payload)
        {
            var text = Encoding.UTF8.GetString(payload);

            Logger.Instance.Info($"Received wakeup command {text} from {Remote}");

            var parts = text.Split(',');
            var remaining = WakeOnLan.ProcessWakeup(parts);

            if (remaining != null)
            {
                // The WoL wasn't performed by this PC

                var remotePCName = remaining[0];

                try
                {
                    var excludeRemotePC = string.Join(',', remaining.Skip(1));

                    // First try and resolve the computer by any bridges that are connected

                    var bridgeClient = WoLServer.Instance.GetBridge(remotePCName);

                    if (bridgeClient != null)
                        bridgeClient.Send(WoLHelper.Wakeup, excludeRemotePC);
                    else
                    {

                        var remotePC = ServerLookup.Instance.Lookup(remotePCName);

                        if (remotePC == null)
                            Logger.Instance.Error($"Canot resolve end point for {remotePCName}");
                        else
                        {

                            // Short lived client
                            var client = new WoLClient(remotePC, ClientType.Background);
                            client.Send(WoLHelper.Wakeup, excludeRemotePC);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error($"Canot relay command to {remotePCName}. {ex.Message}");
                }
            }

            SendWakupeAck();
        }

        private void ReadWakeupAck()
        {
            if (BridgeClient == true)
                return;

            bool success = false;

            var headerPlusPayload = ReadCommand();

            var header = headerPlusPayload.Item1;

            success = (header != null && header[0] == WoLHelper.WakeupAck);

            if (success == true)
                Logger.Instance.Info($"Received wake up acknowledgement from {Remote}");
            else
                Logger.Instance.Error($"Invalid wake up acknowledgement received from {Remote}");
        }

        private void SendWakupeAck()
        {
            if (BridgeClient == true)
                return;

            // Whatever the outcome send an ACK as this ensures all data is received before the client closes the connection.
            // This is especially observable when debugging with a breakpoint in the read methods and running a client from a comand prompt

            try
            {
                stream.WriteAsync(wakeupAck, 0, wakeupAck.Length, WoLClient.CancelToken).Wait();
                stream.FlushAsync(WoLClient.CancelToken).Wait();

                Logger.Instance.Info($"Sent wake up acknowledgement to {Remote}");
            }
            catch { }
        }


        /// <summary>
        /// Attempt to connect and send all required payloads when connected
        /// </summary>
        /// <param name="logFailure"></param>
        /// <remarks>The steam variable will only be set when a connection is made and all initialisation payloads are sent.</remarks>
        protected void Connect(bool logFailure)
        {
            if (serverEndPoint == null)
            {
                if (logFailure == true)
                    Logger.Instance.Error("Can not connect as end point is invalid");

                return;
            }

            try
            {
                stream = null;
                var socket = new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                socket.Connect(serverEndPoint);
                var remotePC = new NetworkStream(socket);

                if (connectionPayloads != null)
                {
                    foreach (var connectionPayload in connectionPayloads)
                        remotePC.Write(connectionPayload, 0, connectionPayload.Length);
                }
                stream = remotePC;
            }
            catch
            {
                if (logFailure == true)
                    Logger.Instance.Error($"Failed to connect to the service {RemoteInfo(serverEndPoint)}");
            }
        }

        protected void Disconnect(bool disconnected = false)
        {
            if (CancelToken.IsCancellationRequested == false)
            {
                if (clientType != ClientType.Foreground)
                {
                    string trailing = disconnected == true ? "ed" : "ing";
                    Logger.Instance.Info($"Disconnect{trailing} from {Remote}");
                }
            }

            lock (mutex)
            {
                stream?.Close();
                stream = null;
            }
        }
    }
}
