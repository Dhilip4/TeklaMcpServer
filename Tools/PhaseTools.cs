using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using ModelContextProtocol.Server;
using TSM = Tekla.Structures.Model;
using ModelObjectSelector = Tekla.Structures.Model.UI.ModelObjectSelector;

namespace TeklaMcpServer.Tools
{
    [McpServerToolType]
    public static class PhaseTools
    {
        [McpServerTool, Description("Auto-assign selected 3D model pieces to a specific Construction Phase")]
        public static string ManagePhases(int phaseNumber, string phaseName)
        {
            try
            {
                var phase = new TSM.Phase(phaseNumber);
                phase.PhaseName = phaseName;
                phase.Insert();

                var selector = new ModelObjectSelector();
                var selected = selector.GetSelectedObjects();
                int count = 0;
                while (selected.MoveNext())
                {
                    if (selected.Current is TSM.ModelObject obj)
                    {
                        if (obj.SetPhase(phase) && obj.Modify()) count++;
                    }
                }
                new TSM.Model().CommitChanges();
                return $"✅ Assigned {count} objects to Phase {phaseNumber}: {phaseName}.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // =====================================================================
        //  PHASE 6: EXPANDED PHASE & ASSEMBLY MANAGEMENT
        // =====================================================================

        [McpServerTool, Description(
            "List all phases in the current model with their numbers, names, and comments.")]
        public static string GetPhases()
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var phases = model.GetPhases();
                if (phases == null) return "⚠️ No phases found.";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("📋 Phases in Model:");
                int count = 0;
                foreach (TSM.Phase phase in phases)
                {
                    count++;
                    string current = phase.IsCurrentPhase == 1 ? " ⬅ CURRENT" : "";
                    sb.AppendLine($"  [{phase.PhaseNumber}] {phase.PhaseName}" +
                        (string.IsNullOrEmpty(phase.PhaseComment) ? "" : $" — {phase.PhaseComment}") +
                        current);
                }
                return count > 0 ? sb.ToString() : "⚠️ No phases found in model.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Set the phase of multiple objects by their GUIDs. " +
            "Pass semicolon-separated GUIDs. All objects get the same phase number.")]
        public static string SetObjectsPhase(int phaseNumber, string guids)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var guidList = guids.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                var phase = new TSM.Phase(phaseNumber);
                int updated = 0;

                foreach (var g in guidList)
                {
                    var obj = model.SelectModelObject(model.GetIdentifierByGUID(g.Trim()));
                    if (obj == null) continue;
                    obj.SetPhase(phase);
                    if (obj.Modify()) updated++;
                }
                model.CommitChanges();
                return $"✅ Set phase {phaseNumber} on {updated}/{guidList.Length} objects.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Get assembly information for a part by its GUID. " +
            "Returns the assembly hierarchy: main part, secondary parts, prefix, and number.")]
        public static string GetAssemblyInfo(string guid)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var part = model.SelectModelObject(model.GetIdentifierByGUID(guid)) as TSM.Part;
                if (part == null) return "❌ Part not found from GUID.";

                var assembly = part.GetAssembly();
                if (assembly == null) return "❌ No assembly found for this part.";
                assembly.Select();

                var mainPart = assembly.GetMainPart() as TSM.Part;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("🏗️ Assembly Information:");
                sb.AppendLine($"  Assembly Name: {assembly.Name}");
                sb.AppendLine($"  Prefix: {assembly.AssemblyNumber.Prefix}");
                sb.AppendLine($"  Start Number: {assembly.AssemblyNumber.StartNumber}");

                if (mainPart != null)
                {
                    mainPart.Select();
                    sb.AppendLine($"  Main Part: {mainPart.Name} ({mainPart.Profile.ProfileString})");
                    sb.AppendLine($"  Main Part GUID: {model.GetGUIDByIdentifier(mainPart.Identifier)}");
                }

                var secondaries = assembly.GetSecondaries();
                sb.AppendLine($"  Secondary Parts: {secondaries.Count}");
                int i = 0;
                foreach (var item in secondaries)
                {
                    var sec = item as TSM.Part;
                    if (sec == null) continue;
                    sec.Select();
                    i++;
                    sb.AppendLine($"    [{i}] {sec.Name} ({sec.Profile.ProfileString}) — GUID: {model.GetGUIDByIdentifier(sec.Identifier)}");
                    if (i >= 20) { sb.AppendLine($"    ... and {secondaries.Count - 20} more"); break; }
                }

                return sb.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Add or remove parts from an assembly. Provide the main part's GUID. " +
            "Pass comma-separated GUIDs of parts to add or remove. " +
            "Optionally change the assembly prefix.")]
        public static string ModifyAssembly(
            string mainPartGuid,
            string addPartGuids = "",
            string removePartGuids = "",
            string prefix = "")
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var mainPart = model.SelectModelObject(model.GetIdentifierByGUID(mainPartGuid)) as TSM.Part;
                if (mainPart == null) return "❌ Main part not found from GUID.";

                var assembly = mainPart.GetAssembly();
                if (assembly == null) return "❌ No assembly found for this part.";
                assembly.Select();

                int added = 0, removed = 0;
                var errors = new List<string>();

                // Add parts
                if (!string.IsNullOrEmpty(addPartGuids))
                {
                    foreach (var g in addPartGuids.Split(','))
                    {
                        var part = model.SelectModelObject(model.GetIdentifierByGUID(g.Trim()));
                        if (part != null)
                        {
                            try { assembly.Add(part as TSM.IAssemblable); added++; }
                            catch (Exception ex) { errors.Add($"Add failed: {ex.Message}"); }
                        }
                        else errors.Add($"Part not found: {g.Trim()}");
                    }
                }

                // Remove parts
                if (!string.IsNullOrEmpty(removePartGuids))
                {
                    foreach (var g in removePartGuids.Split(','))
                    {
                        var part = model.SelectModelObject(model.GetIdentifierByGUID(g.Trim()));
                        if (part != null)
                        {
                            try { assembly.Remove(part); removed++; }
                            catch (Exception ex) { errors.Add($"Remove failed: {ex.Message}"); }
                        }
                        else errors.Add($"Part not found: {g.Trim()}");
                    }
                }

                // Update prefix
                if (!string.IsNullOrEmpty(prefix))
                    assembly.AssemblyNumber.Prefix = prefix;

                assembly.Modify();
                model.CommitChanges();

                var result = $"✅ Assembly modified: added={added}, removed={removed}";
                if (!string.IsNullOrEmpty(prefix)) result += $", prefix={prefix}";
                if (errors.Count > 0) result += $"\n⚠️ Errors ({errors.Count}): {string.Join("; ", errors)}";
                return result;
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }
    }
}
