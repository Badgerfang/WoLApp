using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WoLApp
{
    /// <summary>
    /// A simple class to read endpoints from a file
    /// </summary>
    internal class KeyEndPointFromFile
    {
        /// <summary>
        /// Reads all lines in afile any parse the lines as a name and endpoint key value pair
        /// </summary>
        /// <param name="filename">The file to read</param>
        /// <returns>A dictionary of names and endpoints</returns>
        /// <remarks>
        /// Any line staring with a * is a comment line.
        /// </remarks>
        internal static Dictionary<string, IPEndPointInformation> Read(string filename)
        {
            var lookup = new Dictionary<string, IPEndPointInformation>();

            foreach (string line in System.IO.File.ReadAllLines(filename))
            {
                if (line.StartsWith("*") == false)
                {
                    var parts = line.Split();

                    if (parts.Length >= 2 && IPEndPoint.TryParse(parts[1], out var fileEndpoint) == true)
                    {
                        var remote = new IPEndPointInformation() { Remote = fileEndpoint };

                        if (parts.Length > 2)
                            remote.Additional = parts.Skip(2).ToArray();
                        
                        lookup[parts[0]] = remote;
                    }
                }
            }

            return lookup;
        }
    }
}
