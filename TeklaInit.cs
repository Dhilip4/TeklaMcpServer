using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace TeklaMcpServer
{
    /// <summary>
    /// Assembly resolution and Tekla initialization patterns.
    /// All methods here are called from Program.Main() BEFORE any Tekla/Trimble types are touched.
    /// NOTE: No Newtonsoft.Json or other NuGet dependencies here - this runs before assembly resolution.
    /// Adapted from reference for Tekla 2022.
    /// </summary>
    public static class TeklaInit
    {
        private static readonly string DefaultVersion = "2025.0";
        private static readonly string DefaultBinDir = @"C:\TeklaStructures\2025.0\bin";
        private static readonly bool StartupDebug =
            string.Equals(Environment.GetEnvironmentVariable("TEKLA_MCP_DEBUG_STARTUP"), "1", StringComparison.Ordinal);
        private static readonly object RuntimeSelectionLock = new object();

        // Assembly resolution cache and re-entrancy guard.
        private static readonly Dictionary<string, Assembly> ResolvedAssemblyCache =
            new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> CurrentlyResolving =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static RuntimeSelection CurrentRuntimeSelection;
        private static bool RuntimeIniPrepared;

        private sealed class RuntimeSelection
        {
            public string BinDir { get; set; } = "";
            public string Version { get; set; } = "";
            public string Source { get; set; } = "";
        }

        /// <summary>
        /// Resolve the configured Tekla version.
        /// </summary>
        public static string GetConfiguredVersion() => GetOrCreateRuntimeSelection().Version;

        /// <summary>
        /// Resolve Tekla bin directory.
        /// </summary>
        public static string GetBinDir() => GetOrCreateRuntimeSelection().BinDir;

        public static string GetRuntimeSelectionSource() => GetOrCreateRuntimeSelection().Source;

        public static string DescribeRuntimeSelection()
        {
            var selection = GetOrCreateRuntimeSelection();
            return $"version={selection.Version}, source={selection.Source}, binDir={selection.BinDir}";
        }

        private static RuntimeSelection GetOrCreateRuntimeSelection(bool prepareIni = false)
        {
            lock (RuntimeSelectionLock)
            {
                if (CurrentRuntimeSelection == null)
                {
                    CurrentRuntimeSelection = ResolveRuntimeSelection();
                    LogDebug(
                        $"Latched Tekla runtime: version={CurrentRuntimeSelection.Version}, " +
                        $"source={CurrentRuntimeSelection.Source}, binDir={CurrentRuntimeSelection.BinDir}");
                }

                if (prepareIni && !RuntimeIniPrepared)
                {
                    ConfigureIniFiles(CurrentRuntimeSelection.BinDir);
                    RuntimeIniPrepared = true;
                }

                return CurrentRuntimeSelection;
            }
        }

        private static RuntimeSelection ResolveRuntimeSelection()
        {
            // Priority 1: TEKLA_BIN_DIR environment variable
            var customBinDir = Environment.GetEnvironmentVariable("TEKLA_BIN_DIR");
            if (!string.IsNullOrWhiteSpace(customBinDir) && Directory.Exists(customBinDir))
            {
                var version = TryExtractVersionFromBinDir(customBinDir) ?? DefaultVersion;
                LogDebug($"Using TEKLA_BIN_DIR: {customBinDir} (version={version})");
                return new RuntimeSelection
                {
                    BinDir = customBinDir,
                    Version = version,
                    Source = "TEKLA_BIN_DIR"
                };
            }

            // Priority 2: TEKLA_VERSION environment variable
            var envVersion = Environment.GetEnvironmentVariable("TEKLA_VERSION");
            if (!string.IsNullOrWhiteSpace(envVersion))
            {
                var binDir = $@"C:\TeklaStructures\{envVersion}\bin";
                if (!Directory.Exists(binDir))
                    binDir = $@"C:\Program Files\Tekla Structures\{envVersion}\bin";
                LogDebug($"Using TEKLA_VERSION: {binDir}");
                return new RuntimeSelection
                {
                    BinDir = binDir,
                    Version = envVersion,
                    Source = "TEKLA_VERSION"
                };
            }

            // Priority 3: Running Tekla process
            var runningBinDir = TryGetRunningTeklaBinDir();
            if (!string.IsNullOrWhiteSpace(runningBinDir))
            {
                var version = TryExtractVersionFromBinDir(runningBinDir) ?? DefaultVersion;
                LogDebug($"Using running Tekla process bin dir: {runningBinDir}");
                return new RuntimeSelection
                {
                    BinDir = runningBinDir,
                    Version = version,
                    Source = "running Tekla instance"
                };
            }

            // Priority 4: settings.json
            var settingsSelection = TryGetSettingsRuntimeSelection();
            if (settingsSelection != null)
                return settingsSelection;

            // Priority 5: Default
            LogDebug($"Falling back to default: {DefaultBinDir}");
            return new RuntimeSelection
            {
                BinDir = DefaultBinDir,
                Version = DefaultVersion,
                Source = "default fallback"
            };
        }

        private static string TryGetRunningTeklaBinDir()
        {
            try
            {
                foreach (var process in Process.GetProcessesByName("TeklaStructures"))
                {
                    try
                    {
                        var exePath = process.MainModule?.FileName;
                        if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
                        {
                            var binDir = Path.GetDirectoryName(exePath);
                            if (!string.IsNullOrWhiteSpace(binDir) && Directory.Exists(binDir))
                                return binDir;
                        }
                    }
                    catch { }
                    finally { process.Dispose(); }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error probing running Tekla process: {ex.Message}");
            }
            return null;
        }

        private static RuntimeSelection TryGetSettingsRuntimeSelection()
        {
            try
            {
                var settingsPath = FindSettingsJson();
                if (settingsPath == null) return null;

                var text = File.ReadAllText(settingsPath);
                var binDirMatch = Regex.Match(text, @"""binDir""\s*:\s*""([^""]+)""");
                if (binDirMatch.Success)
                {
                    var binDir = binDirMatch.Groups[1].Value.Replace("\\\\", "\\");
                    if (Directory.Exists(binDir))
                    {
                        return new RuntimeSelection
                        {
                            BinDir = binDir,
                            Version = TryExtractVersionFromBinDir(binDir) ?? DefaultVersion,
                            Source = "settings.json binDir"
                        };
                    }
                }

                var versionMatch = Regex.Match(text, @"""version""\s*:\s*""([^""]+)""");
                if (versionMatch.Success)
                {
                    var version = versionMatch.Groups[1].Value;
                    var path = $@"C:\TeklaStructures\{version}\bin";
                    if (!Directory.Exists(path))
                        path = $@"C:\Program Files\Tekla Structures\{version}\bin";
                    return new RuntimeSelection
                    {
                        BinDir = path,
                        Version = version,
                        Source = "settings.json version"
                    };
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error reading settings: {ex.Message}");
            }
            return null;
        }

        private static string TryExtractVersionFromBinDir(string binDir)
        {
            if (string.IsNullOrWhiteSpace(binDir)) return null;
            try
            {
                var normalized = binDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var parent = Directory.GetParent(normalized);
                var versionCandidate = parent?.Name;
                return Regex.IsMatch(versionCandidate ?? "", @"^\d{4}\.\d+$") ? versionCandidate : null;
            }
            catch { return null; }
        }

        private static string FindSettingsJson()
        {
            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var currentDir = exeDir;

            for (var i = 0; i < 10 && !string.IsNullOrWhiteSpace(currentDir); i++)
            {
                foreach (var candidate in new[]
                {
                    Path.Combine(currentDir, "settings.json"),
                    Path.Combine(currentDir, "config", "settings.json")
                })
                {
                    var full = Path.GetFullPath(candidate);
                    if (visited.Add(full) && File.Exists(full))
                        return full;
                }

                var parent = Path.GetDirectoryName(currentDir);
                if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent, currentDir, StringComparison.OrdinalIgnoreCase))
                    break;
                currentDir = parent;
            }
            return null;
        }

        /// <summary>
        /// Resolve Tekla and Trimble assemblies from the Tekla bin directory.
        /// Registered as AppDomain.AssemblyResolve BEFORE any Tekla types are referenced.
        /// </summary>
        public static Assembly ResolveFromCurrentRuntime(ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);
            if (!ShouldResolveFromTeklaRuntime(name.Name))
                return null;

            var selection = GetOrCreateRuntimeSelection(prepareIni: true);
            return ResolveFromBin(args, selection.BinDir);
        }

        public static Assembly ResolveFromBin(ResolveEventArgs args, string binDir)
        {
            var name = new AssemblyName(args.Name);
            if (!ShouldResolveFromTeklaRuntime(name.Name))
                return null;

            // Return cached assembly immediately
            if (ResolvedAssemblyCache.TryGetValue(name.Name, out var cached))
                return cached;

            // Re-entrancy guard
            if (!CurrentlyResolving.Add(name.Name))
                return null;

            try
            {
                var resolved = ResolveFromBinCore(name.Name, binDir);
                if (resolved != null)
                    ResolvedAssemblyCache[name.Name] = resolved;
                return resolved;
            }
            finally
            {
                CurrentlyResolving.Remove(name.Name);
            }
        }

        private static Assembly ResolveFromBinCore(string assemblyName, string binDir)
        {
            // Direct match in bin dir
            var path = Path.Combine(binDir, assemblyName + ".dll");
            if (File.Exists(path))
            {
                LogDebug($"Resolved: {assemblyName} -> {path}");
                return Assembly.LoadFrom(path);
            }

            // Check plugins subfolder (Tekla 2022 specific)
            var pluginsPath = Path.Combine(binDir, "plugins", assemblyName + ".dll");
            if (File.Exists(pluginsPath))
            {
                LogDebug($"Resolved (plugins): {assemblyName} -> {pluginsPath}");
                return Assembly.LoadFrom(pluginsPath);
            }

            // Check exe directory
            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
            var exePath = Path.Combine(exeDir, assemblyName + ".dll");
            if (exePath != path && File.Exists(exePath))
            {
                LogDebug($"Resolved (exe dir): {assemblyName} -> {exePath}");
                return Assembly.LoadFrom(exePath);
            }

            LogDebug($"NOT FOUND: {assemblyName} in {binDir}");
            return null;
        }

        private static bool ShouldResolveFromTeklaRuntime(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName)) return false;
            return assemblyName.Equals("DotNetKit", StringComparison.OrdinalIgnoreCase)
                || assemblyName.StartsWith("Tekla.", StringComparison.OrdinalIgnoreCase)
                || assemblyName.StartsWith("Trimble.", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Set TS_OVERRIDE_INI_FILE to a temp copy of TeklaStructures.ini so the external
        /// MCP process has a writable run-path.
        /// </summary>
        public static void ConfigureIniFiles(string binDir)
        {
            if (string.Equals(Environment.GetEnvironmentVariable("TEKLA_MCP_SKIP_OVERRIDE_INI"), "1", StringComparison.Ordinal))
            {
                LogDebug("Skipping TS_OVERRIDE_INI_FILE because TEKLA_MCP_SKIP_OVERRIDE_INI=1.");
                return;
            }

            var sourceIni = Path.Combine(binDir, "TeklaStructures.ini");
            if (!File.Exists(sourceIni))
                return;

            var runPath = Path.Combine(Path.GetTempPath(), "TeklaMcpServer_RunPath");
            Directory.CreateDirectory(runPath);
            var destIni = Path.Combine(runPath, "TeklaStructures.ini");
            File.Copy(sourceIni, destIni, overwrite: true);
            Environment.SetEnvironmentVariable("TS_OVERRIDE_INI_FILE", destIni);
            LogDebug($"Set TS_OVERRIDE_INI_FILE={destIni}");
        }

        private static void LogDebug(string message)
        {
            if (!StartupDebug) return;
            Console.Error.WriteLine($"[TeklaInit] {message}");
        }
    }
}
