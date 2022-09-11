using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WoLApp
{
    internal static class Settings
    {
        public static int TxCount { get; private set; } = 5;

        public static int WoLPort { get; private set; } = 7;

        public static int ServerPort { get; private set; } = 12000;

        public static bool SilentMode { get; private set; }

        public static int BootDelay { get; private set; }

        public static bool Broadcast { get; private set; }

        public static bool Pause { get; private set; } = false;

        public static bool ServerMode { get; private set; }

        public static string Wakeup { get; private set; }

        public static string MacLookup { get; private set; }
        public static string ServerLookup { get; private set; }
        public static string BridgeLookup { get; private set; }

        public static string ServerName { get; private set; }

        public static int DelayAfterWoL { get; private set; }

        public static bool DisableHeartbeats { get; set; }


        private const string PauseCommand = "p";
        private const string BroadcastCommand = "b";
        private const string SeverPortCommand = "sp";
        private const string ServerModeCommand = "sm";
        private const string ServerNameCommand = "sn";
        private const string RemoteServerCommand = "rs";
        private const string MacLookupCommand = "ml";
        private const string ServerLookupCommand = "sl";
        private const string WakeCommand = "w";
        private const string NoWakeCommand = "nw";
        private const string DelayAfterWoLCommand = "dl";
        private const string BridgeCommand = "br";
        private const string TxCommand = "tx";
        private const string WakePortCommand = "wp";
        public const string DisableHeartbeatCommand = "dh";

        private const string HelpCommand = "?";

        public static IPEndPoint RemoteServer { get; private set; }

        public static void ProcessSettings(string[] args)
        {
            try
            {
                string command = null;

                foreach (var arg in args)
                {

                    if (command == null)
                    {
                        if (arg.StartsWith('-') == true)
                        {
                            var cmd = arg.Substring(1);
                            switch (cmd)
                            {
                                case BroadcastCommand:
                                    Broadcast = true;
                                    break;

                                case NoWakeCommand:
                                    SilentMode = true;
                                    break;

                                case PauseCommand:
                                    Pause = true;
                                    break;

                                case DisableHeartbeatCommand:
                                    DisableHeartbeats = true;
                                    break;

                                case WakeCommand:
                                case SeverPortCommand:
                                case ServerNameCommand:
                                case RemoteServerCommand:
                                case MacLookupCommand:
                                case ServerLookupCommand:
                                case DelayAfterWoLCommand:
                                case BridgeCommand:
                                case TxCommand:
                                case WakePortCommand:
                                    command = cmd;
                                    break;

                                case ServerModeCommand:
                                    ServerMode = true;
                                    break;

                                case HelpCommand:
                                    DisplayHelp();
                                    break;

                            }
                        }
                    }
                    else
                    {
                        switch (command)
                        {
                            case SeverPortCommand:
                                if (int.TryParse(arg, out var sp) == true)
                                    ServerPort = sp;
                                break;

                            case RemoteServerCommand:
                                if (IPEndPoint.TryParse(arg, out var rs) == true && rs.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                    RemoteServer = rs;
                                break;

                            case WakeCommand:
                                Wakeup = arg;
                                break;

                            case ServerNameCommand:
                                ServerName = arg;
                                break;

                            case MacLookupCommand:
                                MacLookup = arg;
                                break;

                            case ServerLookupCommand:
                                ServerLookup = arg;
                                break;

                            case BridgeCommand:
                                BridgeLookup = arg;
                                break;

                            case DelayAfterWoLCommand:
                                if (int.TryParse(arg, out var milliseconds) == true)
                                    DelayAfterWoL = milliseconds;
                                break;

                            case TxCommand:
                                if (int.TryParse(arg, out var txCount) == true && txCount > 0)
                                    TxCount = txCount;
                                break;

                            case WakePortCommand:
                                if (ushort.TryParse(arg, out var wakePort) == true && wakePort > 0)
                                    WoLPort = wakePort;
                                break;
                        }

                        command = null;
                    }
                }
            }
            catch { }

            if (ServerMode == true && ServerName == null)
                ServerName = Environment.MachineName;
        }

        private static void DisplayHelp()
        {
            Console.WriteLine($"-{PauseCommand} Pause the application before running in client/server mode.");
            Console.WriteLine($"-{BroadcastCommand} Use a broadcast packets for Wake on Lan rather than the default of packets per NIC");
            Console.WriteLine($"-{SeverPortCommand} <port> Specify the server port to listen on. Default 12000");
            Console.WriteLine($"-{ServerModeCommand} Run as a server rather than a client");
            Console.WriteLine($"-{ServerNameCommand} <name> Specify the name to use in servermode. Default is the computer's name");
            Console.WriteLine($"-{RemoteServerCommand} <ip_address> Specify the remote server to commicate with to perform the Wake on Lan");
            Console.WriteLine($"-{MacLookupCommand} <filename> Specify a file to use to change a name into a MAC address");
            Console.WriteLine($"-{ServerLookupCommand} <filename> Specify a file to use to change a name into an end point");
            Console.WriteLine($"-{WakeCommand} <wakeup_text> Wake up a computer on the current network or on another network ");
            Console.WriteLine($"-{NoWakeCommand} Disable any Wake on Lan packets from being transmitted, (diagnostics mode)");
            Console.WriteLine($"-{DelayAfterWoLCommand} <milliseconds> Delay for a number of milliseconds after sending a Wake on Lan to another computer, (diagnostics mode)");
            Console.WriteLine($"-{BridgeCommand} <filename> Specify a file that defines bridges to create when in server mode");
            Console.WriteLine($"-{TxCommand} <count> Specify the number of magic packets to transmit. Default 5");
            Console.WriteLine($"-{DisableHeartbeats} Disable the server from transmitting a heart beat over all bridge connections. Default false");
            Console.WriteLine($"-{WakePortCommand} <port> Specify the port to transmit the magic packet on. Default 7");
        }
    }
}
