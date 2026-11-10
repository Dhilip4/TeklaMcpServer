using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using Tekla.Structures;
using Tekla.Structures.Model;
using Tekla.Structures.Model.Operations;
using TeklaMcpServer.Utils;

namespace TeklaMcpServer.Tools
{
    [McpServerToolType]
    public static class MacroTools
    {
        // -----------------------------------------------------------------------
        // RunMacro — Enhanced with bundled macro fallback
        // -----------------------------------------------------------------------

        [McpServerTool, Description(
            "Execute a Tekla macro by name. Macros are .cs files in Tekla's macro directories " +
            "OR bundled with the MCP server in the macros/ folder. " +
            "Bundled macros: NumberModifiedObjects, NumberAllParts, NumberSelectedParts. " +
            "Tekla's internal macros (e.g. 'drawings\\ADD-PART.cs') also work directly. " +
            "Use ListMacros to see all available macros. " +
            "The macro runs asynchronously inside Tekla — this tool returns immediately after launching it.")]
        public static string RunTeklaMacro(
            [Description("Macro filename with or without .cs extension, e.g. 'MyMacro' or 'drawings\\ADD-PART.cs'")] string macroName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(macroName))
                    return "❌ macroName cannot be empty.";

                // Check if another macro is already running
                if (Operation.IsMacroRunning())
                    return "⏳ Tekla is busy running another macro. Wait for it to finish.";

                // Ensure .cs extension
                var fullName = macroName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    ? macroName
                    : macroName + ".cs";

                // 1. Try standard Tekla macro resolution first (covers XS_MACRO_DIRECTORY paths)
                //    This handles Tekla's internal macros like "drawings\ADD-PART.cs"
                var result = Operation.RunMacro(fullName);

                // 2. If not found by Tekla, check our bundled macros/ folder
                //    and copy to a Tekla-accessible macro directory, then retry
                if (!result)
                {
                    var bundledPath = FindBundledMacro(fullName);
                    if (bundledPath != null)
                    {
                        var macroDir = GetPreferredRuntimeMacroDirectory();
                        Directory.CreateDirectory(macroDir);
                        var destPath = Path.Combine(macroDir, Path.GetFileName(fullName));
                        File.Copy(bundledPath, destPath, true);
                        result = Operation.RunMacro(Path.GetFileName(fullName));
                    }
                }

                if (result)
                    return $"✅ Macro '{fullName}' launched in Tekla. Check Tekla UI for results.";
                return $"❌ Macro '{fullName}' failed to launch. Check name exists in XS_MACRO_DIRECTORY or bundled macros/ folder.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // -----------------------------------------------------------------------
        // ListMacros — Search all macro directories including bundled
        // -----------------------------------------------------------------------

        [McpServerTool, Description(
            "List available Tekla macros (.cs files) from the model's macro directory, " +
            "Tekla's standard macro directories (XS_MACRO_DIRECTORY), and macros bundled with the MCP server. " +
            "Use these names with RunTeklaMacro. Optional: pass a search pattern (e.g. 'Number*').")]
        public static string ListMacros(string pattern = "")
        {
            try
            {
                var model = TeklaConnectionGuard.TryConnect();
                if (model == null) return "❌ No active model connection.";

                var info = model.GetInfo();
                var searchDirs = GetMacroSearchDirectories(info.ModelPath);

                var results = new System.Text.StringBuilder();
                results.AppendLine($"📋 MACRO SEARCH DIRECTORIES ({searchDirs.Count}):");
                foreach (var dir in searchDirs)
                    results.AppendLine($"  📁 {dir}");

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int count = 0;
                results.AppendLine("\n🔧 AVAILABLE MACROS:");

                foreach (var dir in searchDirs)
                {
                    try
                    {
                        foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                        {
                            var fname = Path.GetFileNameWithoutExtension(file);
                            if (seen.Contains(fname)) continue;
                            seen.Add(fname);

                            if (!string.IsNullOrEmpty(pattern) && !MatchesWildcard(fname, pattern))
                                continue;

                            // Show relative path from the search dir for clarity
                            var relativePath = file.Substring(dir.Length).TrimStart('\\');
                            results.AppendLine($"  - {relativePath}");
                            count++;
                        }
                    }
                    catch { /* skip inaccessible directories */ }
                }

                results.Insert(0, $"Found {count} macros\n\n");
                return results.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // -----------------------------------------------------------------------
        // IsMacroRunning
        // -----------------------------------------------------------------------

        [McpServerTool, Description("Check if a previously triggered macro is still running")]
        public static string IsMacroRunning()
        {
            try
            {
                return Operation.IsMacroRunning()
                    ? "⏳ A macro is currently running."
                    : "✅ No macro is running.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // -----------------------------------------------------------------------
        // DisplayPrompt
        // -----------------------------------------------------------------------

        [McpServerTool, Description("Display a custom message in Tekla's status bar")]
        public static string DisplayPrompt(string message)
        {
            try
            {
                Operation.DisplayPrompt(message);
                return $"✅ Prompt displayed: '{message}'";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Finds a macro in the bundled macros/ directory shipped with the MCP server.
        /// Walks up from the exe directory to find the macros/ folder.
        /// </summary>
        private static string FindBundledMacro(string macroFileName)
        {
            // Extract just the filename (strip subdirectory like "drawings\")
            var justName = Path.GetFileName(macroFileName);
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var dir = baseDir;

            for (int i = 0; i < 8; i++)
            {
                var candidate = Path.Combine(dir, "macros", justName);
                if (File.Exists(candidate)) return candidate;

                // Also check subdirectories
                var macrosDir = Path.Combine(dir, "macros");
                if (Directory.Exists(macrosDir))
                {
                    var found = Directory.GetFiles(macrosDir, justName, SearchOption.AllDirectories);
                    if (found.Length > 0) return found[0];
                }

                var parent = Path.GetDirectoryName(dir);
                if (parent == null || parent == dir) break;
                dir = parent;
            }
            return null;
        }

        /// <summary>
        /// Returns all directories where Tekla macros can be found:
        /// 1. Model's own macros/ folder
        /// 2. XS_MACRO_DIRECTORY paths (Tekla's global macro paths)
        /// 3. XS_SYSTEM macros folder (Tekla's system macros)
        /// 4. Bundled macros/ folder (shipped with MCP server)
        /// </summary>
        private static List<string> GetMacroSearchDirectories(string modelPath)
        {
            var searchDirs = new List<string>();

            // 1. Model-specific macros
            var modelMacroDir = Path.Combine(modelPath, "macros");
            if (Directory.Exists(modelMacroDir))
                searchDirs.Add(modelMacroDir);

            // 2. XS_MACRO_DIRECTORY (semicolon-separated list of macro folders)
            string xsDir = "";
            try { TeklaStructuresSettings.GetAdvancedOption("XS_MACRO_DIRECTORY", ref xsDir); } catch { }
            if (string.IsNullOrEmpty(xsDir))
                xsDir = Environment.GetEnvironmentVariable("XS_MACRO_DIRECTORY") ?? "";
            if (!string.IsNullOrEmpty(xsDir))
            {
                foreach (var d in xsDir.Split(';'))
                {
                    var trimmed = d.Trim();
                    if (!string.IsNullOrEmpty(trimmed) && Directory.Exists(trimmed))
                        searchDirs.Add(trimmed);
                }
            }

            // 3. XS_SYSTEM macros (Tekla's standard system macros)
            string xsSystem = "";
            try { TeklaStructuresSettings.GetAdvancedOption("XS_SYSTEM", ref xsSystem); } catch { }
            if (string.IsNullOrEmpty(xsSystem))
                xsSystem = Environment.GetEnvironmentVariable("XS_SYSTEM") ?? "";
            if (!string.IsNullOrEmpty(xsSystem))
            {
                var stdMacroDir = Path.Combine(xsSystem, "macros");
                if (Directory.Exists(stdMacroDir))
                    searchDirs.Add(stdMacroDir);
            }

            // 4. Bundled macros (shipped with MCP server)
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var dir = baseDir;
            for (int i = 0; i < 8; i++)
            {
                var candidate = Path.Combine(dir, "macros");
                if (Directory.Exists(candidate)) { searchDirs.Add(candidate); break; }
                var parent = Path.GetDirectoryName(dir);
                if (parent == null || parent == dir) break;
                dir = parent;
            }

            return searchDirs;
        }

        /// <summary>
        /// Gets the best directory to copy bundled macros into so Tekla can find them.
        /// Prefers XS_MACRO_DIRECTORY, falls back to model's macros/ folder.
        /// </summary>
        private static string GetPreferredRuntimeMacroDirectory()
        {
            string xsDir = "";
            try { TeklaStructuresSettings.GetAdvancedOption("XS_MACRO_DIRECTORY", ref xsDir); } catch { }
            if (string.IsNullOrEmpty(xsDir))
                xsDir = Environment.GetEnvironmentVariable("XS_MACRO_DIRECTORY") ?? "";

            if (!string.IsNullOrEmpty(xsDir))
            {
                foreach (var d in xsDir.Split(';'))
                {
                    var trimmed = d.Trim();
                    if (!string.IsNullOrEmpty(trimmed)) return trimmed;
                }
            }

            // Fallback: model's own macros folder
            return Path.Combine(new Model().GetInfo().ModelPath, "macros");
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
