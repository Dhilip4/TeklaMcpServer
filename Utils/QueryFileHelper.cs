using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TeklaMcpServer.Utils
{
    /// <summary>
    /// Disk-backed query pipeline. Saves selection results to JSON files,
    /// returns queryId tokens for chaining operations. Survives server restarts.
    /// </summary>
    public static class QueryFileHelper
    {
        private static readonly string QueryDir;

        static QueryFileHelper()
        {
            QueryDir = Path.Combine(Path.GetTempPath(), "TeklaMcpServer", "queries");
            Directory.CreateDirectory(QueryDir);
        }

        /// <summary>
        /// Save a list of object dictionaries and return a queryId for future reference.
        /// </summary>
        public static string SaveQuery(List<Dictionary<string, object>> rows, string prefix = "q")
        {
            var queryId = $"{prefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";
            var path = Path.Combine(QueryDir, queryId + ".json");

            var json = JsonConvert.SerializeObject(new
            {
                queryId,
                timestamp = DateTime.UtcNow.ToString("o"),
                count = rows.Count,
                rows
            }, Formatting.Indented);

            File.WriteAllText(path, json);
            return queryId;
        }

        /// <summary>
        /// Load a previously saved query by its queryId.
        /// Returns null if not found.
        /// </summary>
        public static List<Dictionary<string, object>> LoadQuery(string queryId)
        {
            var path = Path.Combine(QueryDir, queryId + ".json");
            if (!File.Exists(path)) return null;

            var json = File.ReadAllText(path);
            var obj = JObject.Parse(json);
            var rows = obj["rows"]?.ToObject<List<Dictionary<string, object>>>();
            return rows;
        }

        /// <summary>
        /// Get distinct values of a field from a saved query.
        /// </summary>
        public static List<string> GetDistinctValues(string queryId, string fieldName)
        {
            var rows = LoadQuery(queryId);
            if (rows == null) return new List<string>();

            return rows
                .Where(r => r.ContainsKey(fieldName) && r[fieldName] != null)
                .Select(r => r[fieldName].ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v)
                .ToList();
        }

        /// <summary>
        /// Filter a saved query by field value and save as a new query.
        /// </summary>
        public static string FilterQuery(string queryId, string fieldName, string value)
        {
            var rows = LoadQuery(queryId);
            if (rows == null) return null;

            var filtered = rows
                .Where(r => r.ContainsKey(fieldName) &&
                            r[fieldName] != null &&
                            string.Equals(r[fieldName].ToString(), value, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return SaveQuery(filtered, "filtered");
        }

        /// <summary>
        /// Project specific fields from a saved query.
        /// </summary>
        public static string ProjectFields(string queryId, List<string> fields)
        {
            var rows = LoadQuery(queryId);
            if (rows == null) return null;

            var projected = rows.Select(r =>
            {
                var newRow = new Dictionary<string, object>();
                foreach (var f in fields)
                    if (r.ContainsKey(f))
                        newRow[f] = r[f];
                return newRow;
            }).ToList();

            return SaveQuery(projected, "proj");
        }

        /// <summary>
        /// Get a summary of a saved query (count, fields, sample).
        /// </summary>
        public static string GetQuerySummary(string queryId)
        {
            var rows = LoadQuery(queryId);
            if (rows == null) return $"Query '{queryId}' not found.";

            var fields = rows.Count > 0
                ? string.Join(", ", rows[0].Keys)
                : "(empty)";

            return $"QueryId: {queryId}\nRows: {rows.Count}\nFields: {fields}";
        }

        /// <summary>
        /// List all saved queries.
        /// </summary>
        public static List<string> ListQueries()
        {
            return Directory.GetFiles(QueryDir, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderByDescending(f => f)
                .ToList();
        }

        /// <summary>
        /// Clean up old queries (older than specified hours).
        /// </summary>
        public static int CleanupOldQueries(int maxAgeHours = 24)
        {
            var cutoff = DateTime.UtcNow.AddHours(-maxAgeHours);
            int removed = 0;
            foreach (var file in Directory.GetFiles(QueryDir, "*.json"))
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    try { File.Delete(file); removed++; } catch { }
                }
            }
            return removed;
        }
    }
}
