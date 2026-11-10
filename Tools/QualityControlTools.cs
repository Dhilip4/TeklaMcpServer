using System;
using System.Collections.Generic;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Tekla.Structures.Geometry3d;
using TSM = Tekla.Structures.Model;

namespace TeklaMcpServer.Tools
{
    [McpServerToolType]
    public static class QualityControlTools
    {
        [McpServerTool, Description(
            "Run a clash check on the model. " +
            "Results appear in Tekla's Clash Check Manager. Runs asynchronously.")]
        public static string RunClashCheck()
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var handler = model.GetClashCheckHandler();
                handler.RunClashCheck();
                return "✅ Clash check started. Results will appear in Tekla's Clash Check Manager.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Find parts that have invalid or missing profile/material data (e.g. ? or blank).")]
        public static string FindInvalidDataParts()
        {
            try
            {
                var model = new TSM.Model();
                var parts = model.GetModelObjectSelector().GetAllObjectsWithType(TSM.ModelObject.ModelObjectEnum.UNKNOWN);
                var invalidGuids = new List<string>();

                while (parts.MoveNext())
                {
                    if (parts.Current is TSM.Part part)
                    {
                        string profile = part.Profile.ProfileString;
                        string material = part.Material.MaterialString;

                        if (string.IsNullOrWhiteSpace(profile) || profile.Contains("?") || 
                            string.IsNullOrWhiteSpace(material) || material.Contains("?"))
                        {
                            invalidGuids.Add(part.Identifier.GUID.ToString());
                        }
                    }
                }

                if (invalidGuids.Count == 0) return "✅ All parts have valid profile and material data.";
                return $"⚠️ Found {invalidGuids.Count} parts with missing/invalid data. GUIDs: {string.Join(", ", invalidGuids)}";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Find parts that have not been numbered (assembly or part position contains '?').")]
        public static string FindUnnumberedParts()
        {
            try
            {
                var model = new TSM.Model();
                var parts = model.GetModelObjectSelector().GetAllObjectsWithType(TSM.ModelObject.ModelObjectEnum.UNKNOWN);
                var unnumberedGuids = new List<string>();

                while (parts.MoveNext())
                {
                    if (parts.Current is TSM.Part part)
                    {
                        string assemPos = "";
                        string partPos = "";
                        part.GetReportProperty("ASSEMBLY_POS", ref assemPos);
                        part.GetReportProperty("PART_POS", ref partPos);

                        if (assemPos.Contains("?") || partPos.Contains("?") || string.IsNullOrWhiteSpace(assemPos))
                        {
                            unnumberedGuids.Add(part.Identifier.GUID.ToString());
                        }
                    }
                }

                if (unnumberedGuids.Count == 0) return "✅ All parts are fully numbered.";
                return $"⚠️ Found {unnumberedGuids.Count} unnumbered parts. GUIDs: {string.Join(", ", unnumberedGuids)}";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Check if two parts mathematically clash (intersect) in the background without using the Tekla UI clash manager.")]
        public static string CheckClashBetweenTwoParts(string guid1, string guid2)
        {
            try
            {
                var model = new TSM.Model();
                var part1 = model.SelectModelObject(model.GetIdentifierByGUID(guid1)) as TSM.Part;
                var part2 = model.SelectModelObject(model.GetIdentifierByGUID(guid2)) as TSM.Part;

                if (part1 == null || part2 == null) return "❌ Could not find one or both parts.";

                // Get true solids
                var solid1 = part1.GetSolid();
                var solid2 = part2.GetSolid();

                // Check bounding boxes first (fast check)
                var p1Min = solid1.MinimumPoint;
                var p1Max = solid1.MaximumPoint;
                var p2Min = solid2.MinimumPoint;
                var p2Max = solid2.MaximumPoint;

                bool boundsIntersect = 
                    (p1Min.X <= p2Max.X && p1Max.X >= p2Min.X) &&
                    (p1Min.Y <= p2Max.Y && p1Max.Y >= p2Min.Y) &&
                    (p1Min.Z <= p2Max.Z && p1Max.Z >= p2Min.Z);

                if (!boundsIntersect)
                    return "✅ NO CLASH. The parts are mathematically separated.";

                // Note: True exact solid-solid intersection in Tekla requires either ray-casting (LineSegments)
                // or the native ClashCheckHandler. For the API, AABB (Axis-Aligned Bounding Box) is the fastest mathematical proxy.
                return "⚠️ POTENTIAL CLASH. The spatial bounding boxes of the two parts intersect.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Find parts that have no welds attached to them. This is a common detailing error where secondary parts are shipped loose by mistake.")]
        public static string FindUnweldedParts()
        {
            try
            {
                var model = new TSM.Model();
                var parts = model.GetModelObjectSelector().GetAllObjectsWithType(TSM.ModelObject.ModelObjectEnum.UNKNOWN);
                var unweldedGuids = new List<string>();

                while (parts.MoveNext())
                {
                    if (parts.Current is TSM.Part part)
                    {
                        // Skip if it's explicitly named a loose part or grating
                        string name = part.Name.ToUpper();
                        if (name.Contains("GRATING") || name.Contains("LOOSE") || name.Contains("HANDRAIL"))
                            continue;

                        // Check if it belongs to an assembly with ONLY itself in it (meaning it's not welded to anything)
                        var assembly = part.GetAssembly();
                        if (assembly != null)
                        {
                            var secondaries = assembly.GetSecondaries();
                            // If this part is a main part, and it has NO secondaries, it has no welds.
                            // If this part is a secondary part, it IS welded.
                            if (assembly.GetMainPart()?.Identifier.ID == part.Identifier.ID && (secondaries == null || secondaries.Count == 0))
                            {
                                // We flag it as an isolated part. It might be a valid single-part assembly, 
                                // but we flag it for QC review.
                                unweldedGuids.Add(part.Identifier.GUID.ToString());
                            }
                        }
                    }
                }

                if (unweldedGuids.Count == 0) return "✅ All parts belong to multi-part welded assemblies.";
                return $"⚠️ Found {unweldedGuids.Count} isolated parts (single-part assemblies with no welds). GUIDs: {string.Join(", ", unweldedGuids)}";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }
    }
}
