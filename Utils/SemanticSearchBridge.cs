using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TeklaMcpServer.Utils
{
    /// <summary>
    /// Manages a Python child process that provides semantic search over ChromaDB.
    /// Communicates via JSON-over-stdin/stdout (one JSON object per line).
    /// </summary>
    public sealed class SemanticSearchBridge : IDisposable
    {
        private Process _process;
        private readonly object _lock = new object();
        private bool _disposed;
        private bool _ready;

        private readonly string _pythonPath;
        private readonly string _scriptPath;

        private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(15);

        public SemanticSearchBridge()
        {
            _pythonPath = ResolvePythonPath();
            Console.Error.WriteLine($"[SemanticBridge] Using Python executable: {_pythonPath}");

            var serviceDir = Environment.GetEnvironmentVariable("SEMANTIC_SERVICE_DIR");
            if (string.IsNullOrEmpty(serviceDir))
            {
                var exeDir = Path.GetDirectoryName(typeof(SemanticSearchBridge).Assembly.Location) ?? ".";
                var repoRoot = FindRepoRoot(exeDir);
                serviceDir = Path.Combine(repoRoot, "semantic-service");
            }

            _scriptPath = Path.Combine(serviceDir, "semantic_bridge.py");

            if (!File.Exists(_scriptPath))
                Console.Error.WriteLine($"[SemanticBridge] WARNING: Script not found: {_scriptPath}");
        }

        public JObject SearchAttributes(string query, int topK = 10)
        {
            var request = new JObject
            {
                ["method"] = "search_attributes",
                ["query"] = query,
                ["top_k"] = topK
            };
            return SendRequest(request);
        }

        public JObject SearchApi(string query, int topK = 10)
        {
            var request = new JObject
            {
                ["method"] = "search_api",
                ["query"] = query,
                ["top_k"] = topK
            };
            return SendRequest(request);
        }

        public bool Ping()
        {
            try
            {
                var result = SendRequest(new JObject { ["method"] = "ping" });
                return result != null && result["result"] != null && result["result"].ToString() == "pong";
            }
            catch { return false; }
        }

        private JObject SendRequest(JObject request)
        {
            lock (_lock)
            {
                EnsureRunning();

                var json = request.ToString(Formatting.None);
                Console.Error.WriteLine($"[SemanticBridge] Sending: {json}");

                try
                {
                    _process.StandardInput.WriteLine(json);
                    _process.StandardInput.Flush();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[SemanticBridge] Write failed: {ex.Message}");
                    Kill();
                    throw new InvalidOperationException(
                        "Semantic search bridge process crashed. It will restart on next request.", ex);
                }

                // Read one line of response
                string responseLine = null;
                var readTask = System.Threading.Tasks.Task.Run(() => _process.StandardOutput.ReadLine());
                if (readTask.Wait(RequestTimeout))
                    responseLine = readTask.Result;
                else
                {
                    Console.Error.WriteLine("[SemanticBridge] Request timed out");
                    Kill();
                    throw new TimeoutException("Semantic search request timed out after " + RequestTimeout.TotalSeconds + "s");
                }

                if (string.IsNullOrEmpty(responseLine))
                {
                    Console.Error.WriteLine("[SemanticBridge] Empty response — process may have crashed");
                    Kill();
                    throw new InvalidOperationException("Semantic search bridge returned empty response.");
                }

                Console.Error.WriteLine($"[SemanticBridge] Response: {responseLine.Substring(0, Math.Min(200, responseLine.Length))}...");

                var response = JObject.Parse(responseLine);

                if (response["error"] != null)
                    throw new InvalidOperationException($"Semantic search error: {response["error"]}");

                return response;
            }
        }

        private void EnsureRunning()
        {
            if (_process != null && !_process.HasExited)
                return;

            if (_process != null)
            {
                Console.Error.WriteLine("[SemanticBridge] Process exited, restarting...");
                _process.Dispose();
                _process = null;
                _ready = false;
            }

            StartProcess();
        }

        private void StartProcess()
        {
            // Auto-rebuild ChromaDB if it doesn't exist
            var serviceDir = Path.GetDirectoryName(_scriptPath);
            var chromaDbDir = Path.Combine(serviceDir, "chroma_db");
            if (!HasBuiltDatabase(chromaDbDir))
            {
                var buildScript = Path.Combine(serviceDir, "build_local_db.py");
                if (File.Exists(buildScript))
                {
                    Console.Error.WriteLine("[SemanticBridge] chroma_db not found — building vector database (first run, may take 1-2 minutes)...");
                    var buildPsi = new ProcessStartInfo
                    {
                        FileName = _pythonPath,
                        Arguments = $"\"{buildScript}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = serviceDir,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                    };
                    try
                    {
                        using (var buildProc = Process.Start(buildPsi))
                        {
                            if (buildProc != null)
                            {
                                buildProc.WaitForExit((int)BuildTimeout.TotalMilliseconds);
                                if (buildProc.ExitCode == 0 && HasBuiltDatabase(chromaDbDir))
                                    Console.Error.WriteLine("[SemanticBridge] Vector database built successfully.");
                                else
                                    Console.Error.WriteLine($"[SemanticBridge] WARNING: build_local_db.py exited with code {buildProc.ExitCode}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[SemanticBridge] WARNING: Failed to run build_local_db.py: {ex.Message}");
                    }
                }
            }

            Console.Error.WriteLine($"[SemanticBridge] Starting: {_pythonPath} \"{_scriptPath}\"");

            var psi = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = $"\"{_scriptPath}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            _process = Process.Start(psi);

            if (_process == null)
                throw new InvalidOperationException("Failed to start semantic bridge process.");

            // Forward stderr to our stderr
            _process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.Error.WriteLine($"[SemanticBridge:py] {e.Data}");
            };
            _process.BeginErrorReadLine();

            // Wait for the {"ready": true} signal
            var readyTask = System.Threading.Tasks.Task.Run(() => _process.StandardOutput.ReadLine());
            if (readyTask.Wait(StartupTimeout))
            {
                var readyLine = readyTask.Result;
                if (!string.IsNullOrEmpty(readyLine))
                {
                    try
                    {
                        var readyJson = JObject.Parse(readyLine);
                        _ready = readyJson["ready"] != null && readyJson["ready"].Value<bool>() == true;
                    }
                    catch { _ready = false; }
                }
            }

            if (!_ready)
                Console.Error.WriteLine("[SemanticBridge] WARNING: Did not receive ready signal within timeout");
            else
                Console.Error.WriteLine("[SemanticBridge] Process ready.");
        }

        private void Kill()
        {
            if (_process != null && !_process.HasExited)
            {
                try { _process.Kill(); } catch { }
            }
            if (_process != null) _process.Dispose();
            _process = null;
            _ready = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_lock)
            {
                if (_process != null && !_process.HasExited)
                {
                    try
                    {
                        _process.StandardInput.WriteLine("{\"method\":\"shutdown\"}");
                        _process.StandardInput.Flush();
                        _process.WaitForExit(3000);
                    }
                    catch { }
                }
                Kill();
            }
        }

        private static string FindRepoRoot(string startDir)
        {
            string dir = startDir;
            for (int i = 0; i < 10 && dir != null; i++)
            {
                if (Directory.Exists(Path.Combine(dir, "semantic-service")))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return startDir;
        }

        private static bool HasBuiltDatabase(string chromaDbDir)
        {
            return Directory.Exists(chromaDbDir)
                && Directory.GetFiles(chromaDbDir, "*.sqlite3", SearchOption.AllDirectories).Length > 0;
        }

        private static string ResolvePythonPath()
        {
            var configured = Environment.GetEnvironmentVariable("PYTHON_PATH");
            if (!string.IsNullOrWhiteSpace(configured) && LooksRunnable(configured))
                return configured;

            foreach (var candidate in new[] { "py", "python", "python3" })
            {
                if (LooksRunnable(candidate))
                    return candidate;
            }

            // Try common locations
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                var launcher = Path.Combine(localAppData, "Programs", "Python", "Launcher", "py.exe");
                if (File.Exists(launcher)) return launcher;

                var pythonDir = Path.Combine(localAppData, "Programs", "Python");
                if (Directory.Exists(pythonDir))
                {
                    foreach (var dir in Directory.GetDirectories(pythonDir, "Python*"))
                    {
                        var pythonExe = Path.Combine(dir, "python.exe");
                        if (File.Exists(pythonExe)) return pythonExe;
                    }
                }
            }

            return "py";
        }

        private static bool LooksRunnable(string candidate)
        {
            try
            {
                if (Path.IsPathRooted(candidate) && !File.Exists(candidate))
                    return false;

                using (var process = Process.Start(new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }))
                {
                    if (process == null) return false;
                    if (!process.WaitForExit(5000))
                    {
                        try { process.Kill(); } catch { }
                        return false;
                    }
                    return true;
                }
            }
            catch { return false; }
        }
    }
}
