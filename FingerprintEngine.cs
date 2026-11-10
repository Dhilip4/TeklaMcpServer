using ModelContextProtocol.Server;
using System.ComponentModel;
using System.IO;
using System.Collections.Generic;
using System;
using System.Linq;
using Newtonsoft.Json;
using Tekla.Structures.Model;
using TSM = Tekla.Structures.Model;
using Tekla.Structures.Geometry3d;

namespace TeklaMcpServer;

// ============================================================
//  FINGERPRINT ENGINE
//  Extracts assembly DNA from the open Tekla model and
//  accumulates it into AGENT_MEMORY\fingerprint_db.json
//  Uses the same 8 properties Tekla 2025 AI uses internally.
// ============================================================

public class DrawingFingerprint
{
    // ── Source info ──
    public string ModelPath    { get; set; }
    public string ModelName    { get; set; }
    public string DrawingMark  { get; set; }
    public string DrawingName  { get; set; }
    public string DrawingTitle { get; set; }
    public string DrawingType  { get; set; }   // Assembly / SinglePart

    // ── Matching properties (same as Tekla AI) ──
    public string Profile      { get; set; }
    public string ProfileFamily{ get; set; }   // e.g. "HEA" from "HEA300"
    public string Material     { get; set; }
    public string Name         { get; set; }   // Part name: COLUMN, BEAM…
    public string Class        { get; set; }
    public double Weight       { get; set; }   // kg
    public double Length       { get; set; }   // mm
    public double Volume       { get; set; }   // mm³
    public int    BoltCount    { get; set; }
    public int    WeldCount    { get; set; }
    public int    SubPartCount { get; set; }
    public string UdaType      { get; set; }   // USER_FIELD_1 / ASSEMBLY_TYPE
    public string IndexedAt    { get; set; }
}

public class FingerprintDatabase
{
    public List<DrawingFingerprint> Drawings  { get; set; } = new();
    public string                   UpdatedAt { get; set; }
    public int                      Count     => Drawings.Count;
}

[McpServerToolType]
public static class FingerprintEngine
{
    private static readonly string DbPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "AGENT_MEMORY", "fingerprint_db.json");

    // ─────────────────────────────────────────────────────────
    //  BUILD FINGERPRINTS — run once per model while it is open
    // ─────────────────────────────────────────────────────────
    [McpServerTool, Description(
        "Extract and index fingerprints from all drawings in the currently open Tekla model. " +
        "Open each of your old models one by one and run this. " +
        "Results accumulate in AGENT_MEMORY/fingerprint_db.json across all models. " +
        "Optional: pass nameFilter (e.g. 'HANDRAIL', 'COLUMN') to index only matching assemblies — much faster.")]
    public static string BuildFingerprintsFromOpenModel(
        [Description("Only index assemblies whose Name contains this keyword (case-insensitive). Leave empty to index ALL.")] string nameFilter = "")
    {
        try
        {
            var model = new TSM.Model();
            if (!model.GetConnectionStatus())
                return "❌ Tekla is not connected. Open a model first.";

            var info    = model.GetInfo();
            var handler = new Tekla.Structures.Drawing.DrawingHandler();
            var drawings = handler.GetDrawings();

            bool hasFilter = !string.IsNullOrWhiteSpace(nameFilter);

            var db        = LoadDatabase() ?? new FingerprintDatabase();
            int added     = 0;
            int skipped   = 0;
            int filtered  = 0;
            var newPrints = new List<DrawingFingerprint>();

            while (drawings.MoveNext())
            {
                if (drawings.Current is not Tekla.Structures.Drawing.AssemblyDrawing ad) continue;

                // Apply name filter if specified
                if (hasFilter &&
                    ad.Name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) < 0)
                { filtered++; continue; }

                // Skip if already indexed from this model+mark combo
                bool exists = db.Drawings.Any(d =>
                    d.ModelPath.Equals(info.ModelPath, StringComparison.OrdinalIgnoreCase) &&
                    d.DrawingMark.Equals(ad.Mark, StringComparison.OrdinalIgnoreCase));

                if (exists) { skipped++; continue; }

                // Get the model-side assembly
                TSM.ModelObject obj = model.SelectModelObject(ad.AssemblyIdentifier);
                if (obj == null) continue;

                var fp = ExtractFingerprint(obj, ad, info);
                if (fp != null)
                {
                    newPrints.Add(fp);
                    added++;
                }
            }

            // Merge and save
            db.Drawings.AddRange(newPrints);
            db.UpdatedAt = DateTime.Now.ToString("o");
            SaveDatabase(db);

            string filterNote = hasFilter
                ? $"   Filter   : '{nameFilter}' ({filtered} non-matching skipped)\n"
                : "";

            return $"✅ Fingerprinting complete for [{info.ModelName}]\n" +
                   filterNote +
                   $"   Added    : {added} new fingerprints\n" +
                   $"   Skipped  : {skipped} (already indexed)\n" +
                   $"   Total DB : {db.Count}\n" +
                   $"   DB path  : {DbPath}";
        }
        catch (Exception ex)
        {
            return $"⚠️ Error during fingerprinting: {ex.Message}";
        }
    }

    // ─────────────────────────────────────────────────────────
    //  STATUS — how many drawings are indexed
    // ─────────────────────────────────────────────────────────
    [McpServerTool, Description(
        "Show a summary of the fingerprint database — how many drawings are indexed, " +
        "which models they came from, and profile type breakdown.")]
    public static string GetFingerprintDatabaseStatus()
    {
        var db = LoadDatabase();
        if (db == null || db.Count == 0)
            return "❌ Fingerprint DB is empty. Run BuildFingerprintsFromOpenModel() for each old model.";

        var byModel = db.Drawings
            .GroupBy(d => d.ModelName)
            .OrderByDescending(g => g.Count());

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"📊 Fingerprint Database — {db.Count} drawings  (updated: {db.UpdatedAt})");
        sb.AppendLine();

        foreach (var grp in byModel)
        {
            sb.AppendLine($"  📁 {grp.Key}  ({grp.Count()} drawings)");
            var profileGroups = grp.GroupBy(d => d.ProfileFamily)
                                   .OrderByDescending(g => g.Count())
                                   .Take(5);
            foreach (var pg in profileGroups)
                sb.AppendLine($"       {pg.Key ?? "?"}  ×{pg.Count()}");
        }

        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────
    //  REMOVE MODEL — clear fingerprints from a specific model
    // ─────────────────────────────────────────────────────────
    [McpServerTool, Description(
        "Remove all fingerprints that came from a specific model name. " +
        "Useful when a model has been updated and needs re-indexing.")]
    public static string RemoveFingerprintsFromModel(
        [Description("The model name exactly as shown in GetFingerprintDatabaseStatus")] string modelName)
    {
        var db = LoadDatabase();
        if (db == null || db.Count == 0) return "❌ DB is empty.";

        int before = db.Count;
        db.Drawings.RemoveAll(d =>
            d.ModelName.Equals(modelName, StringComparison.OrdinalIgnoreCase));
        db.UpdatedAt = DateTime.Now.ToString("o");
        SaveDatabase(db);

        return $"✅ Removed {before - db.Count} fingerprints from [{modelName}]. DB now has {db.Count}.";
    }

    // ─────────────────────────────────────────────────────────
    //  INTERNAL — extract properties from one assembly
    // ─────────────────────────────────────────────────────────
    private static DrawingFingerprint ExtractFingerprint(
        TSM.ModelObject obj,
        Tekla.Structures.Drawing.AssemblyDrawing ad,
        ModelInfo info)
    {
        try
        {
            // Navigate to the main part
            Beam mainPart = null;
            if (obj is TSM.Assembly asm)
                mainPart = asm.GetMainPart() as Beam;
            else if (obj is Beam b)
                mainPart = b;

            if (mainPart == null) return null;
            mainPart.Select();

            // Weight & volume via report properties
            double weight = 0, volume = 0;
            mainPart.GetReportProperty("WEIGHT", ref weight);
            mainPart.GetReportProperty("VOLUME", ref volume);

            // UDA
            string udaType = "";
            mainPart.GetUserProperty("USER_FIELD_1", ref udaType);
            if (string.IsNullOrEmpty(udaType))
                mainPart.GetUserProperty("ASSEMBLY_TYPE", ref udaType);

            // Count bolts and welds belonging to this assembly
            int boltCount = 0, weldCount = 0, subCount = 0;
            if (obj is TSM.Assembly assembly)
            {
                var children = assembly.GetSecondaries();
                subCount = children?.Count ?? 0;

                // Count bolts
                var bolts = new TSM.Model().GetModelObjectSelector()
                                           .GetAllObjectsWithType(ModelObject.ModelObjectEnum.BOLT_ARRAY); // BOLT_GROUP not in 2022
                while (bolts.MoveNext())
                {
                    if (bolts.Current is TSM.BoltGroup bg &&
                        bg.PartToBoltTo?.Identifier.ID == mainPart.Identifier.ID)
                        boltCount++;
                }
            }

            // Profile family e.g. "HEA" from "HEA300"
            string profile = mainPart.Profile.ProfileString ?? "";
            string family  = new string(profile.TakeWhile(char.IsLetter).ToArray());

            double length = 0;
            try { length = Distance.PointToPoint(mainPart.StartPoint, mainPart.EndPoint); }
            catch { }

            return new DrawingFingerprint
            {
                ModelPath     = info.ModelPath,
                ModelName     = info.ModelName,
                DrawingMark   = ad.Mark,
                DrawingName   = ad.Name,
                DrawingTitle  = ad.Title1,
                DrawingType   = "Assembly",
                Profile       = profile,
                ProfileFamily = family,
                Material      = mainPart.Material.MaterialString ?? "",
                Name          = mainPart.Name ?? "",
                Class         = mainPart.Class ?? "",
                Weight        = Math.Round(weight, 2),
                Length        = Math.Round(length, 1),
                Volume        = Math.Round(volume, 1),
                BoltCount     = boltCount,
                WeldCount     = weldCount,
                SubPartCount  = subCount,
                UdaType       = udaType,
                IndexedAt     = DateTime.Now.ToString("o"),
            };
        }
        catch
        {
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────
    //  PUBLIC READ/WRITE — used by SmartClone
    // ─────────────────────────────────────────────────────────
    public static FingerprintDatabase LoadDatabase()
    {
        if (!File.Exists(DbPath)) return null;
        return JsonConvert.DeserializeObject<FingerprintDatabase>(File.ReadAllText(DbPath));
    }

    public static void SaveDatabase(FingerprintDatabase db)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
        File.WriteAllText(DbPath, JsonConvert.SerializeObject(db, Formatting.Indented));
    }

    // ─────────────────────────────────────────────────────────
    //  PUBLIC — scoring formula (also called by SmartClone)
    // ─────────────────────────────────────────────────────────
    public static double ScoreMatch(DrawingFingerprint target, DrawingFingerprint candidate)
    {
        double score = 0;

        // ── Profile (most important weight = 50 pts max) ──
        if (string.Equals(target.Profile, candidate.Profile, StringComparison.OrdinalIgnoreCase))
            score += 0;                             // exact → perfect
        else if (!string.IsNullOrEmpty(target.ProfileFamily) &&
                 target.ProfileFamily.Equals(candidate.ProfileFamily, StringComparison.OrdinalIgnoreCase))
            score += 20;                            // same family (HEA200 vs HEA300)
        else
            score += 100;                           // completely different

        // ── Name / structural role (30 pts) ──
        if (!string.Equals(target.Name, candidate.Name, StringComparison.OrdinalIgnoreCase))
            score += 30;

        // ── Weight % difference (up to 50 pts) ──
        if (target.Weight > 0)
            score += Math.Min(50, Math.Abs(target.Weight - candidate.Weight)
                                  / target.Weight * 50.0);

        // ── Length % difference (up to 30 pts) ──
        if (target.Length > 0)
            score += Math.Min(30, Math.Abs(target.Length - candidate.Length)
                                  / target.Length * 30.0);

        // ── Bolt count difference (2 pts each bolt) ──
        score += Math.Abs(target.BoltCount - candidate.BoltCount) * 2.0;

        // ── Sub-part count (5 pts each) ──
        score += Math.Abs(target.SubPartCount - candidate.SubPartCount) * 5.0;

        // ── Material (15 pts) ──
        if (!string.Equals(target.Material, candidate.Material, StringComparison.OrdinalIgnoreCase))
            score += 15;

        // ── UDA type (25 pts) ──
        if (!string.IsNullOrEmpty(target.UdaType) &&
            !string.Equals(target.UdaType, candidate.UdaType, StringComparison.OrdinalIgnoreCase))
            score += 25;

        return score;
    }
}
