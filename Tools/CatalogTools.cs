using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using Tekla.Structures.Catalogs;
using TeklaMcpServer.Utils;

namespace TeklaMcpServer.Tools
{
    [McpServerToolType]
    public static class CatalogTools
    {
        private const int MaxResults = 500;

        // Cached catalog data — catalogs don't change during a Tekla session
        private static List<string> _cachedProfiles;
        private static List<string> _cachedParametricPrefixes;
        private static List<string> _cachedMaterials;

        // -----------------------------------------------------------------------
        // ListProfiles
        // -----------------------------------------------------------------------

        [McpServerTool, Description(
            "List available profile names from the Tekla profile catalog. " +
            "Returns library profiles (e.g. 'HEB300') and parametric prefixes (e.g. 'CHS', 'IPE'). " +
            "Use a search pattern to filter (supports * wildcard). " +
            "Examples: 'HEB*' → all HEB profiles, 'IPE*' → IPE profiles. " +
            "Profile strings must match catalog exactly when creating parts.")]
        public static string ListProfiles(string pattern = "")
        {
            try
            {
                if (!EnsureProfileCache())
                    return "❌ Cannot connect to Tekla catalog. Is Tekla running?";

                var matchedProfiles = _cachedProfiles
                    .Where(p => MatchesWildcard(p, pattern))
                    .Take(MaxResults)
                    .ToList();

                var matchedPrefixes = _cachedParametricPrefixes
                    .Where(p => MatchesWildcard(p, pattern))
                    .ToList();

                // If no match, try extracting prefix from pattern (e.g. "CHS168*" → "CHS")
                if (matchedProfiles.Count == 0 && matchedPrefixes.Count == 0 && !string.IsNullOrEmpty(pattern))
                {
                    var prefix = ExtractPrefix(pattern);
                    if (!string.IsNullOrEmpty(prefix))
                        matchedPrefixes = _cachedParametricPrefixes
                            .Where(p => MatchesWildcard(p, prefix + "*"))
                            .ToList();
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"📋 PROFILE CATALOG (pattern: '{(string.IsNullOrEmpty(pattern) ? "*" : pattern)}')");

                if (matchedPrefixes.Count > 0)
                {
                    sb.AppendLine($"\n🔧 PARAMETRIC PREFIXES ({matchedPrefixes.Count}):");
                    sb.AppendLine("  (Use PREFIX + dimensions, e.g. CHS168.3*5.0, SHS100*100*5)");
                    foreach (var p in matchedPrefixes) sb.AppendLine($"  - {p}");
                }

                sb.AppendLine($"\n📐 LIBRARY PROFILES ({matchedProfiles.Count}):");
                foreach (var p in matchedProfiles) sb.AppendLine($"  - {p}");

                if (matchedProfiles.Count >= MaxResults)
                    sb.AppendLine($"\n  ⚠️ Results truncated at {MaxResults}. Use a more specific pattern.");

                return sb.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // -----------------------------------------------------------------------
        // ListMaterials
        // -----------------------------------------------------------------------

        [McpServerTool, Description(
            "List available material names from the Tekla material catalog. " +
            "Use a search pattern to filter (supports * wildcard). " +
            "Examples: 'S355*' → all S355 grades, 'C25*' → concrete grades. " +
            "Material strings must match catalog exactly when creating parts.")]
        public static string ListMaterials(string pattern = "")
        {
            try
            {
                if (_cachedMaterials == null)
                {
                    var catalog = new CatalogHandler();
                    if (!catalog.GetConnectionStatus())
                        return "❌ Cannot connect to Tekla catalog. Is Tekla running?";

                    var materials = new List<string>();
                    var en = catalog.GetMaterialItems();
                    while (en.MoveNext())
                    {
                        var item = en.Current as MaterialItem;
                        if (item != null && !string.IsNullOrEmpty(item.MaterialName))
                            materials.Add(item.MaterialName);
                    }
                    materials.Sort(StringComparer.OrdinalIgnoreCase);
                    _cachedMaterials = materials;
                }

                var matched = _cachedMaterials
                    .Where(m => MatchesWildcard(m, pattern))
                    .Take(MaxResults)
                    .ToList();

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"📋 MATERIAL CATALOG ({matched.Count} results, pattern: '{(string.IsNullOrEmpty(pattern) ? "*" : pattern)}')");
                foreach (var m in matched) sb.AppendLine($"  - {m}");

                if (matched.Count >= MaxResults)
                    sb.AppendLine($"\n  ⚠️ Results truncated at {MaxResults}. Use a more specific pattern.");

                return sb.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // -----------------------------------------------------------------------
        // ListBoltAssemblies
        // -----------------------------------------------------------------------

        [McpServerTool, Description(
            "List available bolt standards/assemblies from the Tekla bolt catalog. " +
            "Returns standard name, size, and type. " +
            "Use a search pattern to filter (supports * wildcard), e.g. 'M20*', '7990'. " +
            "Bolt standard strings must match catalog exactly when creating bolt groups.")]
        public static string ListBoltAssemblies(string pattern = "")
        {
            try
            {
                var catalog = new CatalogHandler();
                if (!catalog.GetConnectionStatus())
                    return "❌ Cannot connect to Tekla catalog. Is Tekla running?";

                var results = new List<string>();
                var en = catalog.GetBoltItems();
                int count = 0;
                while (en.MoveNext() && count < MaxResults)
                {
                    var item = en.Current as BoltItem;
                    if (item == null) continue;
                    var combined = $"{item.Standard} M{item.Size:F1}";
                    if (MatchesWildcard(combined, pattern) || MatchesWildcard(item.Standard, pattern))
                    {
                        results.Add($"  - {item.Standard}  |  M{item.Size:F0}  |  {item.Type}");
                        count++;
                    }
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"📋 BOLT CATALOG ({results.Count} results, pattern: '{(string.IsNullOrEmpty(pattern) ? "*" : pattern)}')");
                sb.AppendLine("  Standard  |  Size  |  Type");
                sb.AppendLine("  ----------|--------|------");
                foreach (var r in results) sb.AppendLine(r);
                return sb.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // -----------------------------------------------------------------------
        // ListComponentCatalog
        // -----------------------------------------------------------------------

        [McpServerTool, Description(
            "List all available Tekla system components (connections, details, seams, custom parts). " +
            "Returns component name, number, and type. Use component numbers with AddConnection. " +
            "Use a search pattern to filter (supports * wildcard), e.g. '*plate*', '*haunch*'.")]
        public static string ListComponentCatalog(string pattern = "", string typeFilter = "")
        {
            try
            {
                var catalog = new CatalogHandler();
                if (!catalog.GetConnectionStatus())
                    return "❌ Cannot connect to Tekla catalog. Is Tekla running?";

                var results = new List<string>();
                var en = catalog.GetComponentItems();
                int count = 0;
                while (en.MoveNext() && count < MaxResults)
                {
                    var item = en.Current as ComponentItem;
                    if (item == null) continue;
                    var name = item.UIName ?? item.Name ?? "";
                    var typeName = item.Type.ToString();

                    if (!MatchesWildcard(name, pattern) && !MatchesWildcard(item.Name, pattern))
                        continue;
                    if (!string.IsNullOrEmpty(typeFilter) && !typeName.Equals(typeFilter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    results.Add($"  - #{item.Number,-6} | {typeName,-14} | {name}");
                    count++;
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"📋 COMPONENT CATALOG ({results.Count} results)");
                sb.AppendLine("  Number  |  Type           |  Name");
                sb.AppendLine("  --------|-----------------|------");
                foreach (var r in results) sb.AppendLine(r);
                return sb.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private static bool EnsureProfileCache()
        {
            if (_cachedProfiles != null) return true;

            var catalog = new CatalogHandler();
            if (!catalog.GetConnectionStatus()) return false;

            var library = new List<string>();
            var libEn = catalog.GetLibraryProfileItems();
            while (libEn.MoveNext())
            {
                var item = libEn.Current as LibraryProfileItem;
                if (item != null && !string.IsNullOrEmpty(item.ProfileName))
                    library.Add(item.ProfileName);
            }
            library.Sort(StringComparer.OrdinalIgnoreCase);

            var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var paramEn = catalog.GetParametricProfileItems();
            while (paramEn.MoveNext())
            {
                var item = paramEn.Current as ParametricProfileItem;
                if (item != null && !string.IsNullOrEmpty(item.ProfilePrefix))
                    prefixes.Add(item.ProfilePrefix);
            }
            _cachedParametricPrefixes = prefixes.OrderBy(p => p).ToList();
            _cachedProfiles = library;
            return true;
        }

        private static string ExtractPrefix(string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return null;
            int i = 0;
            while (i < pattern.Length && char.IsLetter(pattern[i])) i++;
            return i > 0 ? pattern.Substring(0, i) : null;
        }

        private static bool MatchesWildcard(string value, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return true;
            if (string.IsNullOrEmpty(value)) return false;
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase);
        }
    }
}
