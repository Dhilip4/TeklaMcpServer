using System;
using System.Collections.Generic;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Tekla.Structures.Geometry3d;
using TSM = Tekla.Structures.Model;

namespace TeklaMcpServer.Tools
{
    [McpServerToolType]
    public static class SpatialTools
    {
        // =====================================================================
        //  PHASE 2: SPATIAL & INSPECTION TOOLS
        // =====================================================================

        [McpServerTool, Description(
            "Get the axis-aligned bounding box (min/max XYZ in mm) of a part by its GUID. " +
            "Returns min/max corners and size in each axis.")]
        public static string GetPartBoundingBox(string guid)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var part = model.SelectModelObject(model.GetIdentifierByGUID(guid)) as TSM.Part;
                if (part == null) return "❌ Part not found from GUID.";

                var solid = part.GetSolid();
                if (solid == null) return "❌ Could not get solid geometry for this part.";

                var min = solid.MinimumPoint;
                var max = solid.MaximumPoint;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"📦 Bounding Box for {part.Profile.ProfileString} ({part.Name}):");
                sb.AppendLine($"  Min: ({min.X:F1}, {min.Y:F1}, {min.Z:F1})");
                sb.AppendLine($"  Max: ({max.X:F1}, {max.Y:F1}, {max.Z:F1})");
                sb.AppendLine($"  Size X: {(max.X - min.X):F1} mm");
                sb.AppendLine($"  Size Y: {(max.Y - min.Y):F1} mm");
                sb.AppendLine($"  Size Z: {(max.Z - min.Z):F1} mm");
                return sb.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Calculate the distance (in mm) between two parts by their GUIDs. " +
            "Uses centroid of each part (midpoint for beams, bounding box center for plates).")]
        public static string GetDistanceBetweenParts(string guid1, string guid2)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var part1 = model.SelectModelObject(model.GetIdentifierByGUID(guid1)) as TSM.Part;
                var part2 = model.SelectModelObject(model.GetIdentifierByGUID(guid2)) as TSM.Part;
                if (part1 == null) return "❌ Part 1 not found from GUID.";
                if (part2 == null) return "❌ Part 2 not found from GUID.";

                var c1 = GetCentroid(part1);
                var c2 = GetCentroid(part2);
                if (c1 == null || c2 == null) return "❌ Could not compute centroid for one or both parts.";

                double dx = c2.X - c1.X;
                double dy = c2.Y - c1.Y;
                double dz = c2.Z - c1.Z;
                double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"📏 Distance Between Parts:");
                sb.AppendLine($"  Part 1: {part1.Name} ({part1.Profile.ProfileString}) at ({c1.X:F1}, {c1.Y:F1}, {c1.Z:F1})");
                sb.AppendLine($"  Part 2: {part2.Name} ({part2.Profile.ProfileString}) at ({c2.X:F1}, {c2.Y:F1}, {c2.Z:F1})");
                sb.AppendLine($"  Distance: {dist:F1} mm ({dist / 1000.0:F4} m)");
                sb.AppendLine($"  Vector: dX={dx:F1}, dY={dy:F1}, dZ={dz:F1}");
                return sb.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Get all grid systems in the model with their labels and coordinate positions (in mm). " +
            "Returns X/Y/Z axis labels and their absolute positions.")]
        public static string GetGridInfo()
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var enumerator = model.GetModelObjectSelector()
                    .GetAllObjectsWithType(TSM.ModelObject.ModelObjectEnum.GRID);

                var sb = new System.Text.StringBuilder();
                int count = 0;

                while (enumerator.MoveNext())
                {
                    var grid = enumerator.Current as TSM.Grid;
                    if (grid == null) continue;
                    grid.Select();
                    count++;

                    sb.AppendLine($"📐 Grid #{count}:");
                    sb.AppendLine($"  X Labels: {grid.LabelX?.Trim()}");
                    sb.AppendLine($"  Y Labels: {grid.LabelY?.Trim()}");
                    sb.AppendLine($"  Z Labels: {grid.LabelZ?.Trim()}");
                    sb.AppendLine($"  X Coordinates: {grid.CoordinateX?.Trim()}");
                    sb.AppendLine($"  Y Coordinates: {grid.CoordinateY?.Trim()}");
                    sb.AppendLine($"  Z Coordinates: {grid.CoordinateZ?.Trim()}");
                }

                if (count == 0) return "⚠️ No grids found in the model.";
                return sb.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Compare properties of two parts side-by-side by their GUIDs. " +
            "Shows profile, material, class, name, phase, start/end points, and highlights differences.")]
        public static string CompareElements(string guid1, string guid2)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var part1 = model.SelectModelObject(model.GetIdentifierByGUID(guid1)) as TSM.Part;
                var part2 = model.SelectModelObject(model.GetIdentifierByGUID(guid2)) as TSM.Part;
                if (part1 == null) return "❌ Part 1 not found from GUID.";
                if (part2 == null) return "❌ Part 2 not found from GUID.";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("🔍 Element Comparison:");
                sb.AppendLine($"{"Property",-20} {"Part 1",-25} {"Part 2",-25} {"Match"}");
                sb.AppendLine(new string('-', 85));

                CompareField(sb, "Name", part1.Name, part2.Name);
                CompareField(sb, "Profile", part1.Profile.ProfileString, part2.Profile.ProfileString);
                CompareField(sb, "Material", part1.Material.MaterialString, part2.Material.MaterialString);
                CompareField(sb, "Class", part1.Class, part2.Class);
                CompareField(sb, "Finish", part1.Finish, part2.Finish);

                // Phase
                var ph1 = new TSM.Phase(); part1.GetPhase(out ph1);
                var ph2 = new TSM.Phase(); part2.GetPhase(out ph2);
                CompareField(sb, "Phase", ph1?.PhaseNumber.ToString() ?? "?", ph2?.PhaseNumber.ToString() ?? "?");

                // Positions for beams
                var beam1 = part1 as TSM.Beam;
                var beam2 = part2 as TSM.Beam;
                if (beam1 != null && beam2 != null)
                {
                    double len1 = Distance(beam1.StartPoint, beam1.EndPoint);
                    double len2 = Distance(beam2.StartPoint, beam2.EndPoint);
                    CompareField(sb, "Length (mm)", len1.ToString("F1"), len2.ToString("F1"));
                    CompareField(sb, "Start Point",
                        $"({beam1.StartPoint.X:F0},{beam1.StartPoint.Y:F0},{beam1.StartPoint.Z:F0})",
                        $"({beam2.StartPoint.X:F0},{beam2.StartPoint.Y:F0},{beam2.StartPoint.Z:F0})");
                    CompareField(sb, "End Point",
                        $"({beam1.EndPoint.X:F0},{beam1.EndPoint.Y:F0},{beam1.EndPoint.Z:F0})",
                        $"({beam2.EndPoint.X:F0},{beam2.EndPoint.Y:F0},{beam2.EndPoint.Z:F0})");
                }

                return sb.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static Point GetCentroid(TSM.Part part)
        {
            var beam = part as TSM.Beam;
            if (beam != null)
            {
                return new Point(
                    (beam.StartPoint.X + beam.EndPoint.X) / 2,
                    (beam.StartPoint.Y + beam.EndPoint.Y) / 2,
                    (beam.StartPoint.Z + beam.EndPoint.Z) / 2);
            }

            var solid = part.GetSolid();
            if (solid == null) return null;
            var mn = solid.MinimumPoint;
            var mx = solid.MaximumPoint;
            return new Point((mn.X + mx.X) / 2, (mn.Y + mx.Y) / 2, (mn.Z + mx.Z) / 2);
        }

        private static double Distance(Point a, Point b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, dz = b.Z - a.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static void CompareField(System.Text.StringBuilder sb, string name, string val1, string val2)
        {
            string match = (val1 == val2) ? "✅" : "❌";
            sb.AppendLine($"{name,-20} {val1,-25} {val2,-25} {match}");
        }
    }
}
