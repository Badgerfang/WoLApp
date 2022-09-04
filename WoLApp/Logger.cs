using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WoLApp
{
    /// <summary>
    /// A simple logger class
    /// </summary>
    internal class Logger
    {
        public static Logger Instance { get; } = new Logger();

        private object mutex = new object();

        /// <summary>
        /// Generates the UTC time as a string
        /// </summary>
        /// <returns>The UTC time as a string</returns>
        private string Timestamp()
        {
            var now = DateTime.UtcNow;

            return $"{now:yyyy-MM-ddTHH:mm:ssZ}";
        }

        /// <summary>
        /// Logs an error line
        /// </summary>
        /// <param name="msg">The error message</param>
        public void Error(string msg)
        {
            lock (mutex)
            {
                Console.WriteLine($"{Timestamp()} ERR: {msg}");
            }
        }
        
        /// <summary>
        /// Logs an information line
        /// </summary>
        /// <param name="msg">The information message</param>
        public void Info(string msg)
        {
            lock (mutex)
            {
                Console.WriteLine($"{Timestamp()} INF: {msg}");
            }
        }
    }
}
