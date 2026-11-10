using ModelContextProtocol.Server;
using System.ComponentModel;
using System.IO;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;

namespace TeklaMcpServer;

// ============================================================
//  MODEL SCANNER
//  Crawls C:\ and D:\ for all Tekla model folders (.db1 files)
//  Saves result to AGENT_MEMORY\tekla_model_registry.json
// ============================================================

public class ModelEntry
{
    public string ModelPath { get; set; }
    public string ModelName { get; set; }
    public string Db1File   { get; set; }
    public string ScannedAt { get; set; }
}

public class ModelRegistry
{
    public List<ModelEntry> Models    { get; set; } = new();
    public string           UpdatedAt { get; set; }
    public int              Count     => Models.Count;
}

[McpServerToolType]
public static class ModelScanner
{
    private static readonly string RegistryPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "AGENT_MEMORY", "tekla_model_registry.json");

    // ─────────────────────────────────────────────────────────
    //  SCAN DRIVES — finds every .db1 file (= Tekla model)
    // ─────────────────────────────────────────────────────────
    [McpServerTool, Description(
        "Scan drives for all Tekla model folders by finding .db1 files. " +
        "Pass drives as comma-separated letters, e.g. 'C,D'. " +
        "Result is saved to AGENT_MEMORY/tekla_model_registry.json.")]
    public static string ScanForTeklaModels(
        [Description("Drive letters to scan, comma-separated. Example: 'C,D'")] string drives = "C,D")
    {
        var registry = new ModelRegistry { UpdatedAt = DateTime.Now.ToString("o") };
        var errors   = new List<string>();

        foreach (var driveLetter in drives.Split(','))
        {
            var letter = driveLetter.Trim().TrimEnd('\\').TrimEnd(':');
            var root   = $"{letter}:\\";

            if (!Directory.Exists(root))
            {
                errors.Add($"Drive {root} not found or not accessible.");
                continue;
            }

            // Common Tekla root folders — check these first (fast)
            var commonRoots = new[]
            {
                Path.Combine(root, "TeklaStructuresModels"),
                Path.Combine(root, "TeklaModels"),
                Path.Combine(root, "Models"),
                Path.Combine(root, "Projects"),
                Path.Combine(root, "Structural"),
                Path.Combine(root, "PEB"),
            };

            foreach (var searchRoot in commonRoots)
            {
                if (!Directory.Exists(searchRoot)) continue;
                ScanDirectory(searchRoot, registry, errors);
            }

            // Also do a broader scan of user folders
            var userFolders = new[]
            {
                Path.Combine(root, "Users"),
                Path.Combine(root, "Work"),
                Path.Combine(root, "Dhilip Kumar M"),
            };

            foreach (var folder in userFolders)
            {
                if (!Directory.Exists(folder)) continue;
                ScanDirectory(folder, registry, errors);
            }
        }

        // Remove duplicates by path
        var seen  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dedup = new List<ModelEntry>();
        foreach (var m in registry.Models)
        {
            if (seen.Add(m.ModelPath)) dedup.Add(m);
        }
        registry.Models = dedup;

        SaveRegistry(registry);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"✅ Scan complete. Found {registry.Models.Count} Tekla models.");
        sb.AppendLine($"📁 Registry saved to: {RegistryPath}");
        sb.AppendLine();
        foreach (var m in registry.Models)
            sb.AppendLine($"  [{m.ModelName}]  →  {m.ModelPath}");

        if (errors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"⚠️  {errors.Count} access errors (skipped):");
            foreach (var e in errors) sb.AppendLine($"  {e}");
        }

        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────
    //  LIST REGISTRY — show what is currently stored
    // ─────────────────────────────────────────────────────────
    [McpServerTool, Description(
        "List all Tekla models currently stored in the model registry. " +
        "Run ScanForTeklaModels first to populate.")]
    public static string ListModelRegistry()
    {
        var reg = LoadRegistry();
        if (reg == null || reg.Models.Count == 0)
            return "❌ Registry is empty. Run ScanForTeklaModels('C,D') first.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"📋 Model Registry — {reg.Models.Count} models  (updated: {reg.UpdatedAt})");
        sb.AppendLine();
        for (int i = 0; i < reg.Models.Count; i++)
        {
            var m = reg.Models[i];
            sb.AppendLine($"  #{i + 1:D2}  {m.ModelName}");
            sb.AppendLine($"        Path: {m.ModelPath}");
            sb.AppendLine($"        .db1: {m.Db1File}");
        }
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────
    //  INTERNAL HELPERS
    // ─────────────────────────────────────────────────────────
    private static void ScanDirectory(string root, ModelRegistry registry, List<string> errors)
    {
        try
        {
            foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var db1Files = Directory.GetFiles(dir, "*.db1");
                    if (db1Files.Length == 0) continue;

                    var db1 = db1Files[0];
                    registry.Models.Add(new ModelEntry
                    {
                        ModelPath = dir,
                        ModelName = Path.GetFileName(dir),
                        Db1File   = Path.GetFileName(db1),
                        ScannedAt = DateTime.Now.ToString("o"),
                    });
                }
                catch { /* skip inaccessible subfolder */ }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"{root}: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────
    //  PUBLIC READ/WRITE — used by FingerprintEngine & SmartClone
    // ─────────────────────────────────────────────────────────
    public static void SaveRegistry(ModelRegistry registry)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RegistryPath)!);
        File.WriteAllText(RegistryPath, JsonConvert.SerializeObject(registry, Formatting.Indented));
    }

    public static ModelRegistry LoadRegistry()
    {
        if (!File.Exists(RegistryPath)) return null;
        return JsonConvert.DeserializeObject<ModelRegistry>(File.ReadAllText(RegistryPath));
    }
}
