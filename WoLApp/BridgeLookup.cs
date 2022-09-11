using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WoLApp
{
    class BridgeLookup
    {
        public static BridgeLookup Instance { get; private set; } = new BridgeLookup();

        private Dictionary<string, IPEndPointInformation> lookup = null;

        private object mutex = new object();

        public IPEndPoint Lookup(string computer)
        {
            if (string.IsNullOrEmpty(computer) == true)
                return null;

            if (lookup == null)
                Load();

            if (lookup.TryGetValue(computer, out var endPoint) == true)
                return endPoint.Remote;

            return null;
        }

        private void Load()
        {
            lock (mutex)
            {
                // Could have de-sceduled betweek the if and lock
                if (lookup == null)
                {
                    lookup = new Dictionary<string, IPEndPointInformation>();

                    if (string.IsNullOrEmpty(Settings.BridgeLookup) == false)
                    {
                        try
                        {
                            lookup = KeyEndPointFromFile.Read(Settings.BridgeLookup);
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance.Error(ex.Message);
                        }
                    }
                }

            }
        }

        public List<KeyValuePair<string, IPEndPointInformation>> Bridges()
        {
            Load();

            return lookup.ToList();
        }
    }
}
