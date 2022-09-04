using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace WoLApp
{
    /// <summary>
    /// Class to perform a Wake on LAN
    /// </summary>
    public class WakeOnLan : IDisposable
    {
        private readonly Socket socket;

        private int wolPort;
        private bool broadcast;
        private int txCount;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="port">The port transmit on</param>
        /// <param name="broadcast">Transmit a single broadcast packet rather than a packet per NIC</param>
        /// <param name="txCount">The number of packets to transmit to perform a wake up</param>
        public WakeOnLan(int port, bool broadcast, int txCount)
        {
            this.wolPort = port;
            this.broadcast = broadcast;
            this.txCount = txCount;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            {
                EnableBroadcast = true
            };
        }

        /// <summary>
        /// Perform a wake up for a local computer
        /// </summary>
        /// <param name="args">Wake up arguments</param>
        /// <returns>An array of arguments to relay if the wakeup couldn't be performed locally</returns>
        public static string[] ProcessWakeup(string[] args)
        {
            if (args.Length == 1)
            {
                ProcessWakeup(args[0]);
                return null;
            }
            else if (args.Length == 2 && args[0] == Settings.ServerName)
            {
                // Command targeted at this PC
                ProcessWakeup(args[1]);
                return null;
            }

            // Need to relay the command
            return args;
        }

        /// <summary>
        /// Wake up a computer
        /// </summary>
        /// <param name="computer">Either the computer's MAC assress or a name to look up using the command line MAC lookup file</param>
        /// <returns></returns>
        public static bool ProcessWakeup(string computer)
        {
            PhysicalAddress mac = null;

            if (PhysicalAddress.TryParse(computer, out mac) == false)
                mac = ComputerLookup.Instance.Lookup(computer);

            if (mac != null)
            {
                var wol = new WakeOnLan(Settings.WoLPort, Settings.Broadcast, Settings.TxCount);

                if (wol.Send(mac) == false)
                {
                    Logger.Instance.Error($"Failed to send WoL packet to {computer}");
                    return false;
                }

                return true;
            }

            Logger.Instance.Error($"Cannot resolve {computer} to a MAC address");
            return false;
        }


        /// <summary>
        /// Transmit the required number of packets to wake up a local network computer
        /// </summary>
        /// <param name="mac"></param>
        /// <returns></returns>
        public bool Send(PhysicalAddress mac)
        {
            if (Settings.SilentMode == true)
            {
                Logger.Instance.Info($"Would have woken up {mac}");
                return true;
            }

            bool success = false;

            try
            {
                byte[] magicPacket = BuildMagicPacket(mac); // Get magic packet byte array based on MAC Address

                List<IPEndPoint> endpoints = new List<IPEndPoint>();

                if (broadcast == true)
                {
                    var broadcast = IPAddress.Parse("255.255.255.255");
                    var ep = new IPEndPoint(broadcast, wolPort);
                    endpoints.Add(ep);
                }
                else
                {
                    // Broadcast can fail in computers with 2 or more NICS (I know from experience)
                    // Find all available NIC broadcasts every time as NICs can go up and down
                    // IP address or subnet mask can also be changed by a user at any time


                    var nics = NetworkInterface.GetAllNetworkInterfaces();

                    foreach (var nic in nics)
                    {
                        if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet && nic.OperationalStatus == OperationalStatus.Up && nic.Supports(NetworkInterfaceComponent.IPv4) == true)
                        {
                            var props = nic.GetIPProperties();

                            foreach (var unicast in props.UnicastAddresses)
                            {
                                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                                {
                                    var broadcast = GetBroadcastAddress(unicast.Address, unicast.IPv4Mask);

                                    var found = endpoints.FirstOrDefault(x => x.Address == broadcast);

                                    if (found == null)
                                    {
                                        var ep = new IPEndPoint(broadcast, wolPort);
                                        endpoints.Add(ep);
                                    }
                                }
                            }
                        }
                    }
                }

                bool failed = false;

                // Send the magic packet to each NIC's broadcast address
                for (int i = 0; i < txCount; i++)
                {
                    foreach (var ep in endpoints)
                    {
                        try
                        {
                            if (socket.SendTo(magicPacket, ep) != magicPacket.Length)
                                failed = true;
                        }
                        catch
                        {
                            failed = true;
                        }
                    }
                }

                success = !failed;
            }
            catch
            {
            }

            if (success == true)
            {
                Logger.Instance.Info($"Sent WoL packet to {mac}");
            }

            return success;
        }

        /// <summary>
        /// Gets the broadcast address for a NIC's address
        /// </summary>
        /// <param name="address">The NIC's address</param>
        /// <param name="mask">The NIC's subnet mask</param>
        /// <returns>The NIC's broadcast address</returns>
        private IPAddress GetBroadcastAddress(IPAddress address, IPAddress mask)
        {
            uint ipAddress = BitConverter.ToUInt32(address.GetAddressBytes(), 0);
            uint ipMaskV4 = BitConverter.ToUInt32(mask.GetAddressBytes(), 0);
            uint broadCastIpAddress = ipAddress | ~ipMaskV4;

            return new IPAddress(BitConverter.GetBytes(broadCastIpAddress));
        }

        /// <summary>
        /// Builds  a WoL magic packet for the MAC address
        /// </summary>
        /// <param name="macAddress">The computer's MAC address</param>
        /// <returns>A magic packet to transmit</returns>
        private static byte[] BuildMagicPacket(PhysicalAddress macAddress)
        {
            byte[] macBytes = macAddress.GetAddressBytes(); // Convert 48 bit MAC Address to array of bytes
            byte[] magicPacket = new byte[102];
            for (int i = 0; i < 6; i++) // 6 times 0xFF
            {
                magicPacket[i] = 0xFF;
            }
            for (int i = 6; i < 102; i += 6) // 16 times MAC Address
            {
                Buffer.BlockCopy(macBytes, 0, magicPacket, i, 6);
            }
            return magicPacket; // 102 Byte Magic Packet
        }

        // Public implementation of Dispose pattern callable by consumers.
        private bool _disposed = false;
        public void Dispose() => Dispose(true);

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose managed state (managed objects).
                socket?.Dispose();
            }

            _disposed = true;
        }
    }
}
