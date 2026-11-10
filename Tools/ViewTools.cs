using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model.UI;
using TSM = Tekla.Structures.Model;

namespace TeklaMcpServer.Tools
{
    [McpServerToolType]
    public static class ViewTools
    {
        [McpServerTool, Description("List all currently available model views and their names (including closed ones)")]
        public static string ListModelViews()
        {
            try
            {
                var views = ViewHandler.GetAllViews();
                var results = new List<string>();
                while (views.MoveNext())
                {
                    var v = views.Current as Tekla.Structures.Model.UI.View;
                    if (v != null)
                    {
                        string status = v.IsVisible() ? "[OPEN]" : "[CLOSED]";
                        results.Add($"{status} {v.Name}");
                    }
                }
                return results.Count > 0
                    ? $"✅ Available Views ({results.Count}):\n{string.Join("\n", results)}"
                    : "⚠️ No views found in the model.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Generate a structural Grid by providing X, Y, Z coordinates and labels")]
        public static string CreateGrid(string xOffsets, string yOffsets, string zOffsets, string xLabels, string yLabels, string zLabels)
        {
            try
            {
                var grid = new TSM.Grid();
                grid.CoordinateX = xOffsets;
                grid.CoordinateY = yOffsets;
                grid.CoordinateZ = zOffsets;
                grid.LabelX = xLabels;
                grid.LabelY = yLabels;
                grid.LabelZ = zLabels;
                if (grid.Insert())
                {
                    new TSM.Model().CommitChanges();
                    return "✅ Structural Grid generated successfully.";
                }
                return "❌ Failed to generate Grid.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // =====================================================================
        //  PHASE 5: VISUALIZATION & UI TOOLS
        // =====================================================================

        [McpServerTool, Description(
            "Redraw/refresh all open Tekla views. " +
            "Call after making visual changes to update the display.")]
        public static string RedrawView()
        {
            try
            {
                var views = ViewHandler.GetVisibleViews();
                int count = 0;
                while (views.MoveNext())
                {
                    ViewHandler.RedrawView(views.Current);
                    count++;
                }
                return $"✅ Redrawn {count} visible views.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Set a temporary color on selected model objects by GUIDs. " +
            "Color is temporary and does not persist. Pass clearAll=true to remove all colors. " +
            "RGB values 0-255. Call RedrawView after to update.")]
        public static string ColorSelected(
            string guids = "",
            int red = 255, int green = 0, int blue = 0,
            double transparency = 0.0,
            bool clearAll = false)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                if (clearAll)
                {
                    ModelObjectVisualization.ClearAllTemporaryStates();
                    return "✅ All temporary colors cleared.";
                }

                if (string.IsNullOrEmpty(guids)) return "❌ Provide GUIDs or set clearAll=true.";

                var guidList = guids.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                var objects = new List<TSM.ModelObject>();
                foreach (var g in guidList)
                {
                    var obj = model.SelectModelObject(model.GetIdentifierByGUID(g.Trim()));
                    if (obj != null) objects.Add(obj);
                }

                var color = new Color(red / 255.0, green / 255.0, blue / 255.0, transparency);
                ModelObjectVisualization.SetTemporaryState(objects, color);

                return $"✅ Colored {objects.Count} objects (R={red}, G={green}, B={blue}).";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Draw temporary text at a 3D coordinate in the active view. " +
            "Labels are temporary — disappear on view redraw. All coordinates in mm.")]
        public static string DrawTemporaryText(
            string text, double x, double y, double z,
            int red = 0, int green = 0, int blue = 0)
        {
            try
            {
                var drawer = new GraphicsDrawer();
                var color = new Color(red / 255.0, green / 255.0, blue / 255.0);
                drawer.DrawText(new Point(x, y, z), text, color);
                return $"✅ Temporary text '{text}' drawn at ({x}, {y}, {z}).";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Draw a temporary line between two 3D points in the active view. " +
            "Useful for visualizing distances or alignments. Temporary — disappears on redraw.")]
        public static string DrawTemporaryLine(
            double startX, double startY, double startZ,
            double endX, double endY, double endZ,
            int red = 255, int green = 0, int blue = 0)
        {
            try
            {
                var drawer = new GraphicsDrawer();
                var color = new Color(red / 255.0, green / 255.0, blue / 255.0);
                drawer.DrawLineSegment(
                    new Point(startX, startY, startZ),
                    new Point(endX, endY, endZ), color);
                return $"✅ Temporary line drawn from ({startX},{startY},{startZ}) to ({endX},{endY},{endZ}).";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Draw temporary 3D labels on model parts showing profile/name. " +
            "Labels appear at each part's centroid. Pass semicolon-separated GUIDs.")]
        public static string DrawElementsLabels(
            string guids,
            bool showName = true, bool showProfile = true,
            bool showMaterial = false, bool showWeight = false,
            int red = 0, int green = 0, int blue = 200)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var guidList = guids.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                var drawer = new GraphicsDrawer();
                var color = new Color(red / 255.0, green / 255.0, blue / 255.0);
                int labeled = 0;

                foreach (var g in guidList)
                {
                    var part = model.SelectModelObject(model.GetIdentifierByGUID(g.Trim())) as TSM.Part;
                    if (part == null) continue;

                    var lines = new List<string>();
                    if (showName) lines.Add(part.Name);
                    if (showProfile) lines.Add(part.Profile.ProfileString);
                    if (showMaterial) lines.Add(part.Material.MaterialString);
                    if (showWeight)
                    {
                        double w = 0;
                        part.GetReportProperty("WEIGHT", ref w);
                        lines.Add($"{w:F1}kg");
                    }

                    var labelText = string.Join(" | ", lines);

                    // Get centroid for label placement
                    Point labelPos = null;
                    var beam = part as TSM.Beam;
                    if (beam != null)
                    {
                        labelPos = new Point(
                            (beam.StartPoint.X + beam.EndPoint.X) / 2,
                            (beam.StartPoint.Y + beam.EndPoint.Y) / 2,
                            (beam.StartPoint.Z + beam.EndPoint.Z) / 2);
                    }
                    else
                    {
                        var solid = part.GetSolid();
                        if (solid == null) continue;
                        var mn = solid.MinimumPoint;
                        var mx = solid.MaximumPoint;
                        labelPos = new Point((mn.X + mx.X) / 2, (mn.Y + mx.Y) / 2, (mn.Z + mx.Z) / 2);
                    }

                    drawer.DrawText(labelPos, labelText, color);
                    labeled++;
                }
                return $"✅ Labeled {labeled} parts. Temporary — disappears on redraw.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Force Tekla UI to select and highlight parts by their GUIDs. " +
            "The objects will be highlighted in the active view.")]
        public static string SelectInUI(string guids)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var guidList = guids.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                var objects = new ArrayList();
                foreach (var g in guidList)
                {
                    var obj = model.SelectModelObject(model.GetIdentifierByGUID(g.Trim()));
                    if (obj != null) objects.Add(obj);
                }

                new ModelObjectSelector().Select(objects);
                return $"✅ Selected {objects.Count} objects in Tekla UI.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }
    }
}
