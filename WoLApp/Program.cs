using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace WoLApp
{
    /// <summary>
    /// The class that runs the application
    /// </summary>
    class Program
    {
        private static WoLServer server;

        static void Main(string[] args)
        {
            Settings.ProcessSettings(args);

            if (Settings.Pause == true)
            {
                Console.Write("Press any key to continue ");
                Console.ReadKey(true);
                Console.WriteLine();
            }

            var delay = Settings.BootDelay;

            if (delay > 0)
                Thread.Sleep(delay);                                                                // The user may want to delay the boot up for diagnostics

            if (Settings.ServerMode == true)
            {
                Logger.Instance.Info($"Running as a Server using the name {Settings.ServerName} on Port {Settings.ServerPort}");
                server = new WoLServer();
                server.Start(Settings.ServerPort);

                Console.CancelKeyPress += delegate
                {
                    Shutdown();
                };

                for (; ; )
                {
                    var cmd = Console.ReadLine();

                    if (cmd == null)
                        break;

                    switch (cmd)
                    {
                        case Settings.DisableHeartbeatCommand:
                            Settings.DisableHeartbeats = true;
                            break;
                    }
                }

                Shutdown();
            }
            else
            {
                // Client mode
                var text = Settings.Wakeup;
                if (string.IsNullOrEmpty(text) == false)
                {
                    var parts = text.Split(',');

                    if (parts.Length == 1)
                        WakeOnLan.ProcessWakeup(parts[0]);
                    else
                    {
                        var server = Settings.RemoteServer;

                        if (server == null)
                            Logger.Instance.Error("No remote server specified");
                        else
                        {
                            // Need to connect to the remote PC and send the WoL command
                            var client = new WoLClient(server, WoLClient.ClientType.Foreground);
                            client.Send(WoLHelper.Wakeup, text);
                        }

                    }
                }
                else
                    Logger.Instance.Error("No wakeup command specified");
            }
        }

        private static void Shutdown()
        {
            StopController.Stop.Set();

            StopAll();

            server?.Stop();
            Environment.Exit(1);
        }

        public static void StopAll() => WoLClient.StopAll();
    }
}

