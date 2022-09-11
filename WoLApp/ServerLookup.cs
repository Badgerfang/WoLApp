using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WoLApp
{
    /// <summary>
    /// A simple class to lookup a name to a computer's endpoint
    /// </summary>
    internal class ServerLookup
    {
        public static ServerLookup Instance { get; private set; } = new ServerLookup();

        private Dictionary<string, IPEndPointInformation> lookup = null;

        private object mutex = new object();

        /// <summary>
        /// Look up a compute
        /// </summary>
        /// <param name="computer">The computer to look up</param>
        /// <returns>The computer's endpoint or null</returns>
        /// <remarks>
        /// Uses the command line server lookup file parameter
        /// </remarks>
        public IPEndPoint Lookup(string computer)
        {
            if (string.IsNullOrEmpty(computer) == true)
                return null;

            if (lookup == null)
            {
                lock (mutex)
                {
                    // Could have de-sceduled between the if and lock
                    if (lookup == null)
                    {
                        lookup = new Dictionary<string, IPEndPointInformation>();

                        if (string.IsNullOrEmpty(Settings.ServerLookup) == false)
                        {
                            try
                            {
                                lookup = KeyEndPointFromFile.Read(Settings.MacLookup);
                            }
                            catch (Exception ex)
                            {
                                Logger.Instance.Error(ex.Message);
                            }
                        }
                    }

                }
            }

            if (lookup.TryGetValue(computer, out var endPoint) == true)
                return endPoint.Remote;

            return null;
        }
    }
}
