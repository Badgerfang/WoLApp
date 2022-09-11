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
    internal class WoLHelper
    {
        internal const int Name = 1;
        internal const int Wakeup = 2;
        internal const int WakeupAck = 3;
        internal const int HeartbeatRequest = 4;
        internal const int Heartbeat = 5;


        /// <summary>
        /// Generate a byte array to transmit using the command and payload as a KLV
        /// </summary>
        /// <param name="command">The key</param>
        /// <param name="payload">The value</param>
        /// <returns>a byte array for transmission</returns>
        internal static byte[] Generate(int command, string payload)
        {
            byte[] payloadArray = null;
            int payloadLength = 0;

            if (string.IsNullOrEmpty(payload) == false)
            {
                payloadArray = Encoding.UTF8.GetBytes(payload);
                payloadLength = payloadArray.Length;
            }

            var commandArray = new byte[3 + payloadLength];

            commandArray[0] = (byte)command;
            commandArray[1] = (byte)(payloadLength >> 8);
            commandArray[2] = (byte)(payloadLength & 0xFF);

            if (payloadLength > 0)
                Array.Copy(payloadArray, 0, commandArray, 3, payloadLength);

            return commandArray;
        }
    }
}
