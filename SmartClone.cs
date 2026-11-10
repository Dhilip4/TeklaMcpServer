using ModelContextProtocol.Server;
using System.ComponentModel;
using System.IO;
using System.Collections.Generic;
using System;
using System.Linq;
using Tekla.Structures.Model;
using Tekla.Structures.Geometry3d;
using TSD = Tekla.Structures.Drawing;
using TSM = Tekla.Structures.Model;

namespace TeklaMcpServer;

[McpServerToolType]
public static class SmartClone
{
    private const string MacroName   = "SmartCloneMCP";
    private const int    MacroTimeoutMs = 45000;

    [McpServerTool, Description("Find top 3 matches for a GUID.")]
    public static string FindTop3MatchingDrawings(string targetGuid)
    {
        try
        {
            var model = new TSM.Model();
            if (!model.GetConnectionStatus()) return "❌ Tekla not connected.";
            var db = FingerprintEngine.LoadDatabase();
            if (db == null || db.Count == 0) return "❌ DB empty.";
            var targetObj = model.SelectModelObject(model.GetIdentifierByGUID(targetGuid));
            if (targetObj == null) return $"❌ Object not found: {targetGuid}";
            var mainPart = ResolveMainPart(targetObj);
            if (mainPart == null) return "❌ Part not supported.";

            double weight = 0, length = 0;
            mainPart.GetReportProperty("WEIGHT", ref weight);
            try { length = Distance.PointToPoint(mainPart.StartPoint, mainPart.EndPoint); } catch { }

            var liveFp = new DrawingFingerprint {
                Profile = mainPart.Profile.ProfileString ?? "",
                ProfileFamily = new string((mainPart.Profile.ProfileString ?? "").TakeWhile(char.IsLetter).ToArray()),
                Material = mainPart.Material.MaterialString ?? "",
                Name = mainPart.Name ?? "", Class = mainPart.Class ?? "",
                Weight = weight, Length = length
            };

            var matches = db.Drawings
                .Select(fp => new { fp, Score = FingerprintEngine.ScoreMatch(liveFp, fp) })
                .OrderBy(x => x.Score).Take(3).ToList();

            if (matches.Count == 0) return "❌ No matches.";
            var results = new List<Top3Entry>();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"🔎 Top matches:");
            for (int i = 0; i < matches.Count; i++) {
                results.Add(new Top3Entry { Rank = i + 1, Score = matches[i].Score, Fingerprint = matches[i].fp });
                sb.AppendLine($"{i+1}. Score: {matches[i].Score:F1} | Mark: {matches[i].fp.DrawingMark}");
            }
            SaveTop3(results, targetGuid);
            return sb.ToString();
        } catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
    }

    [McpServerTool, Description("Clone from match.")]
    public static string CloneDrawingFromMatch(string targetGuid, int matchNumber = 1)
    {
        try {
            var top3 = LoadTop3(targetGuid);
            if (top3 == null) return "❌ No cache.";
            var chosen = top3.FirstOrDefault(m => m.Rank == matchNumber);
            if (chosen == null) return "❌ Match not found.";
            var model = new TSM.Model();
            if (!model.GetConnectionStatus()) return "❌ No connection.";
            var targetObj = model.SelectModelObject(model.GetIdentifierByGUID(targetGuid));
            if (targetObj == null) return "❌ Target lost.";
            return RunCloneMacro(chosen.Fingerprint, targetObj.Identifier.ID, targetGuid);
        } catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
    }

    private static string RunCloneMacro(DrawingFingerprint fp, int targetId, string targetGuid)
    {
        string baseMacroDir = @"C:\TeklaStructures\2022.0\environments\common\macros";
        string resultDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AGENT_MEMORY");
        string resultPath = Path.Combine(resultDir, "macro_result.txt");
        string safeMark = fp.DrawingMark.Replace("\"", "\"\"");
        string safeResult = resultPath.Replace("\"", "\"\"");

        string macroCode = $@"using System;
using System.IO;
using Tekla.Structures;
using Tekla.Structures.Model;
using Tekla.Structures.Drawing;

public class Script
{{
    public void Run()
    {{
        string resultFile = @""{safeResult}"";
        try
        {{
            Write(resultFile, ""STARTING"");
            var model = new Tekla.Structures.Model.Model();
            if (!model.GetConnectionStatus()) return;

            var handler = new DrawingHandler();
            Drawing sourceDrw = null;
            var en = handler.GetDrawings();
            while (en.MoveNext())
            {{
                var d = en.Current as Drawing;
                if (d != null && string.Equals(d.Mark, @""{safeMark}"", StringComparison.OrdinalIgnoreCase))
                {{
                    sourceDrw = d;
                    break;
                }}
            }}

            if (sourceDrw == null) {{ Write(resultFile, ""FAIL:Source not found""); return; }}
            var targetObj = model.SelectModelObject(new Tekla.Structures.Identifier({targetId}));
            if (targetObj == null) {{ Write(resultFile, ""FAIL:Target not found""); return; }}

            Write(resultFile, ""CLONING"");
            var cloned = handler.CloneDrawing(sourceDrw, targetObj);
            if (cloned == null) {{ Write(resultFile, ""FAIL:Clone failed""); return; }}

            model.CommitChanges();
            Write(resultFile, ""OK:"" + cloned.Mark);
        }}
        catch (Exception ex) {{ Write(resultFile, ""FAIL:"" + ex.Message); }}
    }}

    private static void Write(string path, string content)
    {{
        try {{ Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, content); }} catch {{ }}
    }}
}}";

        try { 
            File.WriteAllText(Path.Combine(baseMacroDir, MacroName + ".cs"), macroCode);
            File.WriteAllText(Path.Combine(baseMacroDir, "modeling", MacroName + ".cs"), macroCode);
        } catch { }

        Directory.CreateDirectory(resultDir);
        if (File.Exists(resultPath)) File.Delete(resultPath);
        System.Threading.Thread.Sleep(1000);

        if (!TSM.Operations.Operation.RunMacro(MacroName))
            if (!TSM.Operations.Operation.RunMacro(@"modeling\" + MacroName))
                return "❌ Trigger failed. Please run manually.";

        int waited = 0;
        while (waited < MacroTimeoutMs)
        {
            if (File.Exists(resultPath))
            {
                string content = File.ReadAllText(resultPath).Trim();
                if (content.StartsWith("OK:")) return $"✅ {content}";
                if (content.StartsWith("FAIL:")) return $"❌ {content}";
            }
            System.Threading.Thread.Sleep(1000);
            waited += 1000;
        }
        return "⚠️ Timeout.";
    }

    private static Beam ResolveMainPart(TSM.ModelObject obj) {
        if (obj is TSM.Assembly asm) return asm.GetMainPart() as Beam;
        if (obj is Beam b) return b;
        return null;
    }

    private static readonly string Top3Dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AGENT_MEMORY", "top3_cache");
    private static string Top3Path(string guid) => Path.Combine(Top3Dir, $"top3_{guid.Replace("-", "")}.json");
    private static void SaveTop3(List<Top3Entry> top3, string guid) { Directory.CreateDirectory(Top3Dir); File.WriteAllText(Top3Path(guid), Newtonsoft.Json.JsonConvert.SerializeObject(top3, Newtonsoft.Json.Formatting.Indented)); }
    private static List<Top3Entry> LoadTop3(string guid) { var path = Top3Path(guid); return File.Exists(path) ? Newtonsoft.Json.JsonConvert.DeserializeObject<List<Top3Entry>>(File.ReadAllText(path)) : null; }
}

public class Top3Entry { public int Rank { get; set; } public double Score { get; set; } public DrawingFingerprint Fingerprint { get; set; } }
