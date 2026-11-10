using System.Collections.Generic;
using System.Linq;

namespace TeklaMcpServer.Utils
{
    /// <summary>
    /// Accumulates tool operations between CommitChanges calls.
    /// CommitChanges flushes the log and returns a summary.
    /// </summary>
    public static class OperationLog
    {
        private static readonly List<string> _entries = new List<string>();

        /// <summary>Add an operation entry, e.g. "Created 5 columns (RHS90*50*5.0)"</summary>
        public static void Add(string entry)
        {
            lock (_entries)
                _entries.Add(entry);
        }

        /// <summary>Returns all entries since last flush and clears the log.</summary>
        public static List<string> Flush()
        {
            lock (_entries)
            {
                var copy = _entries.ToList();
                _entries.Clear();
                return copy;
            }
        }

        /// <summary>Returns true if there are pending entries.</summary>
        public static bool HasEntries
        {
            get { lock (_entries) return _entries.Count > 0; }
        }
    }
}
