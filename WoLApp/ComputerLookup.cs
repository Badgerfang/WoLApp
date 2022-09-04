using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace WoLApp
{
    /// <summary>
    /// A simple class to lookup a name to a computer's MAC address
    /// </summary>
    internal class ComputerLookup
    {
        public static ComputerLookup Instance { get; private set; } = new ComputerLookup();

        private Dictionary<string, PhysicalAddress> lookup = null;

        private object mutex = new object();

        /// <summary>
        /// Lookup a computer and return the MAC address
        /// </summary>
        /// <param name="computer"></param>
        /// <returns>The MAC address associated with a computer or null</returns>
        /// <remarks>
        /// Uses the command line MAC lookup file parameter
        /// </remarks>
        public PhysicalAddress Lookup(string computer)
        {
            if (string.IsNullOrEmpty(computer) == true)
                return null;

            if (lookup == null)
            {
                lock (mutex)
                {
                    // Could have de-sceduled betweek the if and lock
                    if (lookup == null)
                    {
                        lookup = new Dictionary<string, PhysicalAddress>();

                        if (string.IsNullOrEmpty(Settings.MacLookup) == false)
                        {
                            try
                            {
                                foreach (string line in System.IO.File.ReadAllLines(Settings.MacLookup))
                                {
                                    if (line.StartsWith("*") == false)
                                    {
                                        var parts = line.Split();

                                        if (parts.Length == 2 && PhysicalAddress.TryParse(parts[1], out var fileMac) == true)
                                            lookup[parts[0]] = fileMac;
                                        else
                                            Logger.Instance.Error($"Invalid computer lookup line. {line}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Instance.Error(ex.Message);
                            }
                        }
                    }

                }
            }

            if (lookup.TryGetValue(computer, out var mac) == true)
                return mac;

            return null;
        }
    }
}
