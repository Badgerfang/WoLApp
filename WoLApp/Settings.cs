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

                                case WakeCommand:
                                case SeverPortCommand:
                                case ServerNameCommand:
                                case RemoteServerCommand:
                                case MacLookupCommand:
                                case ServerLookupCommand:
                                case DelayAfterWoLCommand:
                                case BridgeCommand:
                                    command = cmd;
                                    break;

                                case ServerModeCommand:
                                    ServerMode = true;
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

                        }

                        command = null;
                    }
                }
            }
            catch { }

            if (ServerMode == true && ServerName == null)
                ServerName = Environment.MachineName;
        }
    }
}
