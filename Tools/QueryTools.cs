using System;
using System.Collections.Generic;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Tekla.Structures.Geometry3d;
using TSM = Tekla.Structures.Model;

namespace TeklaMcpServer.Tools
{
    [McpServerToolType]
    public static class QueryTools
    {
        // =====================================================================
        //  PHASE 3: QUERY & INSPECTION TOOLS
        // =====================================================================

        [McpServerTool, Description(
            "Get full properties of selected model elements. " +
            "Returns profile, material, name, class, finish, weight, phase, and positions for all currently selected objects (Parts, Bolts, Welds, Assemblies, etc).")]
        public static string GetElementsProperties()
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                var selected = selector.GetSelectedObjects();

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("📋 SELECTED OBJECTS PROPERTIES:");
                int count = 0;

                while (selected.MoveNext())
                {
                    var obj = selected.Current as TSM.ModelObject;
                    if (obj == null) continue;
                    obj.Select();
                    count++;

                    sb.AppendLine($"\n  [{count}] Type: {obj.GetType().Name}");
                    sb.AppendLine($"    GUID: {obj.Identifier.GUID}");

                    // Handle Parts (Beams, Plates, etc.)
                    if (obj is TSM.Part part)
                    {
                        double weight = 0;
                        part.GetReportProperty("WEIGHT", ref weight);
                        var ph = new TSM.Phase();
                        part.GetPhase(out ph);

                        sb.AppendLine($"    Name: {part.Name}");
                        sb.AppendLine($"    Profile: {part.Profile.ProfileString}");
                        sb.AppendLine($"    Material: {part.Material.MaterialString}");
                        sb.AppendLine($"    Class: {part.Class}");
                        sb.AppendLine($"    Phase: {ph?.PhaseNumber} ({ph?.PhaseName})");
                        sb.AppendLine($"    Weight: {weight:F1} kg");

                        if (part is TSM.Beam beam)
                        {
                            sb.AppendLine($"    Points: ({beam.StartPoint.X:F0}, {beam.StartPoint.Y:F0}, {beam.StartPoint.Z:F0}) -> ({beam.EndPoint.X:F0}, {beam.EndPoint.Y:F0}, {beam.EndPoint.Z:F0})");
                        }

                        // Inspect attached bolts/holes on this part
                        var boltEnum = part.GetBolts();
                        int attachedHoles = 0;
                        var boltSummary = new List<string>();
                        while (boltEnum.MoveNext())
                        {
                            if (boltEnum.Current is TSM.BoltGroup bg)
                            {
                                bg.Select();
                                int bCount = bg.BoltPositions != null ? bg.BoltPositions.Count : 0;
                                attachedHoles += bCount;
                                boltSummary.Add($"{bCount}x M{bg.BoltSize:0.##} (Dia {bg.BoltSize + bg.Tolerance:0.##}mm, Tol {bg.Tolerance:0.##}mm)");
                            }
                        }
                        if (attachedHoles > 0)
                        {
                            sb.AppendLine($"    Attached Holes: {attachedHoles} nos ({string.Join(", ", boltSummary)})");
                        }
                    }
                    // Handle Bolts
                    else if (obj is TSM.BoltGroup bolt)
                    {
                        sb.AppendLine($"    Standard: {bolt.BoltStandard}");
                        sb.AppendLine($"    Size: {bolt.BoltSize}mm");
                        sb.AppendLine($"    Bolt Count: {bolt.BoltPositions.Count}");
                    }
                    // Handle Welds
                    else if (obj is TSM.Weld weld)
                    {
                        sb.AppendLine($"    Size Above: {weld.SizeAbove}");
                        sb.AppendLine($"    Type Above: {weld.TypeAbove}");
                        sb.AppendLine($"    Shop/Site: {(weld.ShopWeld ? "SHOP" : "SITE")}");
                    }
                    // Handle Assemblies
                    else if (obj is TSM.Assembly assembly)
                    {
                        string prefix = "", pos = "";
                        assembly.GetReportProperty("ASSEMBLY_PREFIX", ref prefix);
                        assembly.GetReportProperty("ASSEMBLY_POS", ref pos);
                        sb.AppendLine($"    Assembly Mark: {prefix}{pos}");
                        sb.AppendLine($"    Main Part GUID: {assembly.GetMainPart()?.Identifier.GUID}");
                    }
                    // Handle Components (Connections, Details, etc.)
                    else if (obj is TSM.Component component)
                    {
                        sb.AppendLine($"    Component Name: {component.Name}");
                        sb.AppendLine($"    Component Number: {component.Number}");
                    }
                    else
                    {
                        // Generic report properties for anything else
                        string name = "";
                        obj.GetReportProperty("NAME", ref name);
                        if (!string.IsNullOrEmpty(name)) sb.AppendLine($"    Name: {name}");
                    }
                }

                if (count == 0) return "⚠️ No objects selected. Select parts/bolts/welds in Tekla first.";
                return sb.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Get a comprehensive summary of holes and bolts in selected model parts (or specific parts by GUIDs). " +
            "Summarizes hole diameters (BoltSize + Tolerance), bolt sizes, slotted/oversized hole details, quantities (NOS), and breaks them down by part profile.")]
        public static string GetHolesSummary(
            [Description("Optional semicolon-separated GUIDs of parts or bolts to inspect. If empty, uses currently selected objects in Tekla UI.")] string guids = "")
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var objectsToInspect = new List<TSM.ModelObject>();

                if (!string.IsNullOrWhiteSpace(guids))
                {
                    var splitGuids = guids.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var g in splitGuids)
                    {
                        var id = model.GetIdentifierByGUID(g.Trim());
                        var obj = model.SelectModelObject(id);
                        if (obj != null)
                        {
                            obj.Select();
                            objectsToInspect.Add(obj);
                        }
                    }
                }
                else
                {
                    var selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                    var selected = selector.GetSelectedObjects();
                    while (selected.MoveNext())
                    {
                        if (selected.Current is TSM.ModelObject obj)
                        {
                            obj.Select();
                            objectsToInspect.Add(obj);
                        }
                    }
                }

                if (objectsToInspect.Count == 0)
                    return "⚠️ No objects selected in Tekla or found from provided GUIDs. Please select baseplates or parts first.";

                var parts = new List<TSM.Part>();
                var directlySelectedBoltGroups = new List<TSM.BoltGroup>();

                foreach (var obj in objectsToInspect)
                {
                    if (obj is TSM.Part part) parts.Add(part);
                    else if (obj is TSM.BoltGroup bg) directlySelectedBoltGroups.Add(bg);
                }

                var seenBoltGuids = new HashSet<string>();
                var uniqueBoltGroups = new List<TSM.BoltGroup>();
                var totalHolesByDesc = new Dictionary<string, int>();
                var totalBoltsByDesc = new Dictionary<string, int>();
                var holesByPartProfile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                int partsWithHoles = 0;

                // 1. Process Parts
                foreach (var part in parts)
                {
                    var boltEnum = part.GetBolts();
                    int partHoleCount = 0;
                    string prof = part.Profile.ProfileString ?? "UNKNOWN";

                    while (boltEnum.MoveNext())
                    {
                        if (boltEnum.Current is TSM.BoltGroup bg)
                        {
                            bg.Select();
                            string guidStr = bg.Identifier.GUID.ToString();
                            int countInGroup = bg.BoltPositions != null ? bg.BoltPositions.Count : 0;
                            partHoleCount += countInGroup;

                            if (!seenBoltGuids.Contains(guidStr))
                            {
                                seenBoltGuids.Add(guidStr);
                                uniqueBoltGroups.Add(bg);

                                double boltSize = bg.BoltSize;
                                double tolerance = bg.Tolerance;
                                double holeDia = boltSize + tolerance;
                                bool isBolt = bg.Bolt;
                                string holeType = bg.HoleType.ToString();
                                string slotInfo = "";
                                if (bg.SlottedHoleX > 0 || bg.SlottedHoleY > 0)
                                    slotInfo = $" [Slot: {bg.SlottedHoleX}x{bg.SlottedHoleY}]";

                                string summaryKey = $"M{boltSize:0.##} (Dia {holeDia:0.##} mm, Tol {tolerance:0.##} mm, {holeType}{slotInfo})";
                                if (!totalHolesByDesc.ContainsKey(summaryKey)) totalHolesByDesc[summaryKey] = 0;
                                totalHolesByDesc[summaryKey] += countInGroup;

                                string usageKey = isBolt
                                    ? $"Bolt M{boltSize:0.##} with Hole Dia {holeDia:0.##} mm"
                                    : $"Hole Only (no bolt) Dia {holeDia:0.##} mm (M{boltSize:0.##})";
                                if (!totalBoltsByDesc.ContainsKey(usageKey)) totalBoltsByDesc[usageKey] = 0;
                                totalBoltsByDesc[usageKey] += countInGroup;
                            }
                        }
                    }

                    if (partHoleCount > 0)
                    {
                        partsWithHoles++;
                        if (!holesByPartProfile.ContainsKey(prof)) holesByPartProfile[prof] = 0;
                        holesByPartProfile[prof] += partHoleCount;
                    }
                }

                // 2. Process Directly Selected Bolt Groups
                foreach (var bg in directlySelectedBoltGroups)
                {
                    bg.Select();
                    string guidStr = bg.Identifier.GUID.ToString();
                    if (!seenBoltGuids.Contains(guidStr))
                    {
                        seenBoltGuids.Add(guidStr);
                        uniqueBoltGroups.Add(bg);

                        int countInGroup = bg.BoltPositions != null ? bg.BoltPositions.Count : 0;
                        double boltSize = bg.BoltSize;
                        double tolerance = bg.Tolerance;
                        double holeDia = boltSize + tolerance;
                        bool isBolt = bg.Bolt;
                        string holeType = bg.HoleType.ToString();
                        string slotInfo = "";
                        if (bg.SlottedHoleX > 0 || bg.SlottedHoleY > 0)
                            slotInfo = $" [Slot: {bg.SlottedHoleX}x{bg.SlottedHoleY}]";

                        string summaryKey = $"M{boltSize:0.##} (Dia {holeDia:0.##} mm, Tol {tolerance:0.##} mm, {holeType}{slotInfo})";
                        if (!totalHolesByDesc.ContainsKey(summaryKey)) totalHolesByDesc[summaryKey] = 0;
                        totalHolesByDesc[summaryKey] += countInGroup;

                        string usageKey = isBolt
                            ? $"Bolt M{boltSize:0.##} with Hole Dia {holeDia:0.##} mm"
                            : $"Hole Only (no bolt) Dia {holeDia:0.##} mm (M{boltSize:0.##})";
                        if (!totalBoltsByDesc.ContainsKey(usageKey)) totalBoltsByDesc[usageKey] = 0;
                        totalBoltsByDesc[usageKey] += countInGroup;
                    }
                }

                int grandTotalHoles = 0;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("🔩 HOLES & BOLTS SUMMARY:");
                sb.AppendLine($"  Selected Objects: {objectsToInspect.Count} (Parts: {parts.Count}, Direct Bolts: {directlySelectedBoltGroups.Count})");
                sb.AppendLine($"  Parts with Holes: {partsWithHoles}");
                sb.AppendLine($"  Unique Bolt/Hole Groups: {uniqueBoltGroups.Count}");
                sb.AppendLine();

                sb.AppendLine("📋 BREAKDOWN BY HOLE DIAMETER & BOLT SIZE:");
                foreach (var kvp in totalHolesByDesc)
                {
                    sb.AppendLine($"  • {kvp.Key} ➔ {kvp.Value} NOS");
                    grandTotalHoles += kvp.Value;
                }
                sb.AppendLine();
                sb.AppendLine($"  👉 GRAND TOTAL HOLES: {grandTotalHoles} NOS");
                sb.AppendLine();

                if (totalBoltsByDesc.Count > 0)
                {
                    sb.AppendLine("📌 USAGE TYPE:");
                    foreach (var kvp in totalBoltsByDesc)
                        sb.AppendLine($"  • {kvp.Key} ➔ {kvp.Value} NOS");
                    sb.AppendLine();
                }

                if (holesByPartProfile.Count > 0)
                {
                    sb.AppendLine("📐 HOLES COUNT PER PART PROFILE:");
                    foreach (var kvp in holesByPartProfile)
                        sb.AppendLine($"  • Profile {kvp.Key}: {kvp.Value} holes");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"⚠️ Error analyzing holes: {ex.Message}";
            }
        }

        [McpServerTool, Description(
            "Analyze selected base plates (or parts specified by GUIDs) and generate anchor bolt template groupings (e.g. T1 = 129 NOS, T2 = 4 NOS). " +
            "Identifies unique bolt placement patterns (Pitch x Gauge, Bolt Size, Hole Diameter, and Hole Count) and outputs a fabrication & site erection summary.")]
        public static string GetBasePlateTemplates(
            [Description("Optional semicolon-separated GUIDs of baseplates to inspect. If empty, uses currently selected objects in Tekla UI.")] string guids = "",
            [Description("Prefix for template names (default is 'T', giving T1, T2, etc.).")] string prefix = "T",
            [Description("Whether to group by base plate profile in addition to bolt pattern (default false = pattern only).")] bool groupByProfile = false)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var objectsToInspect = new List<TSM.ModelObject>();

                if (!string.IsNullOrWhiteSpace(guids))
                {
                    var splitGuids = guids.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var g in splitGuids)
                    {
                        var id = model.GetIdentifierByGUID(g.Trim());
                        var obj = model.SelectModelObject(id);
                        if (obj != null)
                        {
                            obj.Select();
                            objectsToInspect.Add(obj);
                        }
                    }
                }
                else
                {
                    var selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                    var selected = selector.GetSelectedObjects();
                    while (selected.MoveNext())
                    {
                        if (selected.Current is TSM.ModelObject obj)
                        {
                            obj.Select();
                            objectsToInspect.Add(obj);
                        }
                    }
                }

                if (objectsToInspect.Count == 0)
                    return "⚠️ No objects selected in Tekla or found from provided GUIDs. Please select base plates first.";

                var parts = new List<TSM.Part>();
                foreach (var obj in objectsToInspect)
                {
                    if (obj is TSM.Part part) parts.Add(part);
                }

                if (parts.Count == 0)
                    return "⚠️ None of the selected objects are parts/baseplates.";

                var templateGroups = new Dictionary<string, TemplateGroupData>();

                int partsWithBolts = 0;
                foreach (var part in parts)
                {
                    var boltEnum = part.GetBolts();
                    bool hasBolt = false;

                    while (boltEnum.MoveNext())
                    {
                        if (boltEnum.Current is TSM.BoltGroup bg)
                        {
                            bg.Select();
                            hasBolt = true;

                            double boltSize = bg.BoltSize;
                            double tolerance = bg.Tolerance;
                            double holeDia = boltSize + tolerance;
                            int holeCount = bg.BoltPositions != null ? bg.BoltPositions.Count : 0;
                            string holeType = bg.HoleType.ToString();

                            string pitchStr = "";
                            string gaugeStr = "";

                            if (bg is TSM.BoltArray ba)
                            {
                                pitchStr = FormatDistances(ba, true);
                                gaugeStr = FormatDistances(ba, false);
                            }
                            else if (bg.BoltPositions != null && bg.BoltPositions.Count > 0)
                            {
                                pitchStr = $"{holeCount} holes (Custom/Circular)";
                                gaugeStr = "-";
                            }
                            else
                            {
                                pitchStr = "Custom";
                                gaugeStr = "Custom";
                            }

                            string prof = part.Profile.ProfileString ?? "UNKNOWN";
                            string groupKey = groupByProfile
                                ? $"M{boltSize:0.##}|Hole:{holeDia:0.##}|P:{pitchStr}|G:{gaugeStr}|N:{holeCount}|Prof:{prof}"
                                : $"M{boltSize:0.##}|Hole:{holeDia:0.##}|P:{pitchStr}|G:{gaugeStr}|N:{holeCount}";

                            if (!templateGroups.ContainsKey(groupKey))
                            {
                                templateGroups[groupKey] = new TemplateGroupData
                                {
                                    BoltSize = boltSize,
                                    HoleDiameter = holeDia,
                                    Tolerance = tolerance,
                                    Pitch = pitchStr,
                                    Gauge = gaugeStr,
                                    HolesPerPlate = holeCount,
                                    HoleType = holeType,
                                    Profile = prof,
                                    Parts = new List<TSM.Part>()
                                };
                            }

                            templateGroups[groupKey].Parts.Add(part);
                            // Only first primary bolt group defines the anchor template
                            break;
                        }
                    }

                    if (hasBolt) partsWithBolts++;
                }

                if (templateGroups.Count == 0)
                    return "⚠️ No bolt groups or holes found on the selected base plates.";

                var cleanPrefix = string.IsNullOrWhiteSpace(prefix) ? "T" : prefix.Trim();
                var sb = new System.Text.StringBuilder();

                sb.AppendLine("📐 BASE PLATE ANCHOR BOLT TEMPLATES REPORT");
                sb.AppendLine($"  Total Baseplates Inspected: {parts.Count}");
                sb.AppendLine($"  Plates with Bolt/Hole Groups: {partsWithBolts}");
                sb.AppendLine($"  Unique Template Patterns: {templateGroups.Count}");
                sb.AppendLine();

                sb.AppendLine("================================================================================");
                sb.AppendLine("📋 TEMPLATE QUANTITIES SUMMARY (SITE ERECTION / FABRICATION)");
                sb.AppendLine("================================================================================");

                int templateIdx = 1;
                var templateNames = new Dictionary<string, string>();
                foreach (var kvp in templateGroups)
                {
                    string tName = $"{cleanPrefix}{templateIdx}";
                    templateNames[kvp.Key] = tName;
                    var data = kvp.Value;
                    sb.AppendLine($"  👉 {tName} = {data.Parts.Count} NOS  [M{data.BoltSize:0.##} | Hole Dia {data.HoleDiameter:0.##}mm | {data.Pitch} Pitch × {data.Gauge} Gauge ({data.HolesPerPlate} holes)]");
                    templateIdx++;
                }
                sb.AppendLine("================================================================================");
                sb.AppendLine();

                sb.AppendLine("🔍 DETAILED TEMPLATE SPECIFICATIONS:");
                foreach (var kvp in templateGroups)
                {
                    string tName = templateNames[kvp.Key];
                    var data = kvp.Value;

                    sb.AppendLine($"--- [Template {tName}] Quantity: {data.Parts.Count} NOS ---");
                    sb.AppendLine($"  • Anchor Bolt Size   : M{data.BoltSize:0.##}");
                    sb.AppendLine($"  • Hole Diameter      : {data.HoleDiameter:0.##} mm (Tolerance {data.Tolerance:0.##} mm)");
                    sb.AppendLine($"  • Hole Placement     : {data.Pitch} Pitch × {data.Gauge} Gauge");
                    sb.AppendLine($"  • Holes per Plate    : {data.HolesPerPlate} holes");
                    sb.AppendLine($"  • Hole Type          : {data.HoleType}");

                    var profCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (var p in data.Parts)
                    {
                        var pProf = p.Profile.ProfileString ?? "UNKNOWN";
                        if (!profCounts.ContainsKey(pProf)) profCounts[pProf] = 0;
                        profCounts[pProf]++;
                    }

                    sb.AppendLine("  • Associated Plate Profiles:");
                    foreach (var pKvp in profCounts)
                    {
                        sb.AppendLine($"      - {pKvp.Key}: {pKvp.Value} plates");
                    }
                    sb.AppendLine();
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"⚠️ Error calculating base plate templates: {ex.Message}";
            }
        }

        [McpServerTool, Description("Get the GUIDs of the objects currently selected by the user in the Tekla UI. Use this to perform actions on what the user is currently looking at or has highlighted.")]
        public static string GetUsersCurrentSelection()
        {
            try
            {
                var selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                var enumerator = selector.GetSelectedObjects();
                var guids = new List<string>();
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current is TSM.ModelObject obj)
                    {
                        guids.Add(obj.Identifier.GUID.ToString());
                    }
                }
                
                if (guids.Count == 0) return "⚠️ No objects selected in the Tekla Model. (Note: This tool does not work for objects selected inside Drawings).";
                return string.Join(";", guids);
            }
            catch (Exception ex)
            {
                return $"❌ Error reading selection: {ex.Message}";
            }
        }

        [McpServerTool, Description(
            "Get model-level information: project name, model path, and object counts by type " +
            "(beams, columns, plates, bolts, welds). Useful for a quick model overview.")]
        public static string GetModelInfo()
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var info = model.GetInfo();
                var pi = model.GetProjectInfo();

                int beamCount = 0, columnCount = 0, plateCount = 0, boltCount = 0, weldCount = 0;

                var en = model.GetModelObjectSelector()
                    .GetAllObjectsWithType(TSM.ModelObject.ModelObjectEnum.BEAM);
                while (en.MoveNext())
                {
                    var beam = en.Current as TSM.Beam;
                    if (beam == null) continue;
                    beam.Select();
                    if (beam.Type == TSM.Beam.BeamTypeEnum.COLUMN) columnCount++;
                    else beamCount++;
                }

                var enPlate = model.GetModelObjectSelector()
                    .GetAllObjectsWithType(TSM.ModelObject.ModelObjectEnum.CONTOURPLATE);
                while (enPlate.MoveNext()) plateCount++;

                foreach (var boltType in new[] {
                    TSM.ModelObject.ModelObjectEnum.BOLT_ARRAY,
                    TSM.ModelObject.ModelObjectEnum.BOLT_CIRCLE,
                    TSM.ModelObject.ModelObjectEnum.BOLT_XYLIST })
                {
                    var enBolt = model.GetModelObjectSelector().GetAllObjectsWithType(boltType);
                    while (enBolt.MoveNext()) boltCount++;
                }

                var enWeld = model.GetModelObjectSelector()
                    .GetAllObjectsWithType(TSM.ModelObject.ModelObjectEnum.WELD);
                while (enWeld.MoveNext()) weldCount++;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("🏗️ Model Information:");
                sb.AppendLine($"  Name: {info.ModelName}");
                sb.AppendLine($"  Path: {info.ModelPath}");
                sb.AppendLine($"  Type: {(info.SingleUserModel ? "Single-User" : "Multi-User")}");
                sb.AppendLine($"  Shared: {(info.SharedModel ? "Yes" : "No")}");
                sb.AppendLine($"  Project: {pi.Name} (#{pi.ProjectNumber})");
                sb.AppendLine($"  Builder: {pi.Builder}");
                sb.AppendLine($"  Designer: {pi.Designer}");
                sb.AppendLine($"  Location: {pi.Location}");
                sb.AppendLine($"  Description: {pi.Description}");
                sb.AppendLine($"\n📊 Object Counts:");
                sb.AppendLine($"  Beams: {beamCount}");
                sb.AppendLine($"  Columns: {columnCount}");
                sb.AppendLine($"  Plates: {plateCount}");
                sb.AppendLine($"  Bolts: {boltCount}");
                sb.AppendLine($"  Welds: {weldCount}");
                sb.AppendLine($"  Total Steel Parts: {beamCount + columnCount + plateCount}");
                return sb.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Get model statistics: total weight (kg), part counts grouped by profile and material. " +
            "Useful for quantity take-off and model review.")]
        public static string GetModelStatistics()
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var profileGroups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var materialGroups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                double totalWeight = 0;
                int totalParts = 0;

                foreach (var objType in new[] {
                    TSM.ModelObject.ModelObjectEnum.BEAM,
                    TSM.ModelObject.ModelObjectEnum.POLYBEAM,
                    TSM.ModelObject.ModelObjectEnum.CONTOURPLATE })
                {
                    var en = model.GetModelObjectSelector().GetAllObjectsWithType(objType);
                    while (en.MoveNext())
                    {
                        var part = en.Current as TSM.Part;
                        if (part == null) continue;
                        part.Select();

                        totalParts++;
                        var prof = part.Profile.ProfileString ?? "?";
                        var mat = part.Material.MaterialString ?? "?";

                        profileGroups[prof] = profileGroups.ContainsKey(prof) ? profileGroups[prof] + 1 : 1;
                        materialGroups[mat] = materialGroups.ContainsKey(mat) ? materialGroups[mat] + 1 : 1;

                        double weight = 0;
                        part.GetReportProperty("WEIGHT", ref weight);
                        totalWeight += weight;
                    }
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"📊 Model Statistics:");
                sb.AppendLine($"  Total Steel Parts: {totalParts}");
                sb.AppendLine($"  Total Weight: {totalWeight:F1} kg ({totalWeight / 1000.0:F3} tonnes)");

                sb.AppendLine($"\n  Profile Breakdown:");
                foreach (var kvp in profileGroups)
                    sb.AppendLine($"    {kvp.Key}: {kvp.Value}");

                sb.AppendLine($"\n  Material Breakdown:");
                foreach (var kvp in materialGroups)
                    sb.AppendLine($"    {kvp.Key}: {kvp.Value}");

                return sb.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Get all boolean cuts applied to a part by its GUID. " +
            "Returns a list of cutting operations with their types and cutting part profiles.")]
        public static string GetElementCutParts(string guid)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var part = model.SelectModelObject(model.GetIdentifierByGUID(guid)) as TSM.Part;
                if (part == null) return "❌ Part not found from GUID.";

                var booleans = part.GetBooleans();
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"✂️ Boolean Cuts on {part.Name} ({part.Profile.ProfileString}):");
                int count = 0;

                while (booleans.MoveNext())
                {
                    var bp = booleans.Current as TSM.BooleanPart;
                    if (bp == null) continue;
                    bp.Select();
                    count++;

                    var cuttingPart = bp.OperativePart as TSM.Part;
                    if (cuttingPart != null) cuttingPart.Select();

                    sb.AppendLine($"  [{count}] Type: {bp.Type}");
                    if (cuttingPart != null)
                        sb.AppendLine($"    Cutting Part: {cuttingPart.Profile.ProfileString}");
                }

                if (count == 0) sb.AppendLine("  No boolean cuts found.");
                return sb.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Batch modify properties on multiple parts by their GUIDs. " +
            "Pass semicolon-separated GUIDs. Only non-empty parameters are applied.")]
        public static string SetElementsProperties(
            string guids,
            string profile = "", string material = "", string name = "",
            string partClass = "", string finish = "")
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var guidList = guids.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                int modified = 0;

                foreach (var g in guidList)
                {
                    var part = model.SelectModelObject(model.GetIdentifierByGUID(g.Trim())) as TSM.Part;
                    if (part == null) continue;

                    if (!string.IsNullOrEmpty(profile)) part.Profile.ProfileString = profile;
                    if (!string.IsNullOrEmpty(material)) part.Material.MaterialString = material;
                    if (!string.IsNullOrEmpty(name)) part.Name = name;
                    if (!string.IsNullOrEmpty(partClass)) part.Class = partClass;
                    if (!string.IsNullOrEmpty(finish)) part.Finish = finish;

                    if (part.Modify()) modified++;
                }
                model.CommitChanges();
                return $"✅ Modified {modified}/{guidList.Length} parts.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------
        private static double Distance(Point a, Point b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, dz = b.Z - a.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private sealed class TemplateGroupData
        {
            public double BoltSize { get; set; }
            public double HoleDiameter { get; set; }
            public double Tolerance { get; set; }
            public string Pitch { get; set; }
            public string Gauge { get; set; }
            public int HolesPerPlate { get; set; }
            public string HoleType { get; set; }
            public string Profile { get; set; }
            public List<TSM.Part> Parts { get; set; }
        }

        private static string FormatDistances(TSM.BoltArray ba, bool isX)
        {
            int count = isX ? ba.GetBoltDistXCount() : ba.GetBoltDistYCount();
            if (count == 0) return "0mm";

            var dists = new List<double>();
            for (int i = 0; i < count; i++)
            {
                dists.Add(isX ? ba.GetBoltDistX(i) : ba.GetBoltDistY(i));
            }

            bool allEqual = true;
            for (int i = 1; i < dists.Count; i++)
            {
                if (Math.Abs(dists[i] - dists[0]) > 0.01)
                {
                    allEqual = false;
                    break;
                }
            }

            if (allEqual && dists.Count > 1)
            {
                return $"{dists.Count}*{dists[0]:0.##}mm";
            }
            else if (dists.Count == 1)
            {
                return $"{dists[0]:0.##}mm";
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < dists.Count; i++)
                {
                    if (i > 0) sb.Append("+");
                    sb.Append(dists[i].ToString("0.##"));
                }
                sb.Append("mm");
                return sb.ToString();
            }
        }
    }
}
