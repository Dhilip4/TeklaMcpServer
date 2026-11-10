using System;
using System.Collections.Generic;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Tekla.Structures.Model;
using Tekla.Structures.Geometry3d;
using TSM = Tekla.Structures.Model;

namespace TeklaMcpServer.Tools
{
    [McpServerToolType]
    public static class CoreTools
    {
        // -----------------------------------------------------------------------
        // CommitChanges — Explicit commit for batch workflows
        // -----------------------------------------------------------------------

        [McpServerTool, Description(
            "Commit all pending changes to the Tekla model database. " +
            "Must be called after any create, modify, or delete operations to persist them. " +
            "Equivalent to Ctrl+S in the Tekla UI.")]
        public static string CommitChanges()
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";
                model.CommitChanges();
                return "✅ All pending changes committed to the model.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // -----------------------------------------------------------------------
        // GetProjectInfo — Project metadata
        // -----------------------------------------------------------------------

        [McpServerTool, Description(
            "Get project information from the open Tekla model: " +
            "project name, number, builder (contractor), designer, and address.")]
        public static string GetProjectInfo()
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var pi = model.GetProjectInfo();
                var info = model.GetInfo();
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("📋 PROJECT INFORMATION");
                sb.AppendLine($"  Model Name:      {info.ModelName}");
                sb.AppendLine($"  Model Path:      {info.ModelPath}");
                sb.AppendLine($"  Project Name:    {pi.Name}");
                sb.AppendLine($"  Project Number:  {pi.ProjectNumber}");
                sb.AppendLine($"  Builder:         {pi.Builder}");
                sb.AppendLine($"  Designer:        {pi.Designer}");
                sb.AppendLine($"  Address:         {pi.Address}");
                return sb.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // -----------------------------------------------------------------------
        // DeleteObject — Delete a single model object by GUID
        // -----------------------------------------------------------------------

        [McpServerTool, Description(
            "Delete a model object (beam, plate, bolt, weld, etc.) by its GUID. " +
            "The object is permanently removed from the model.")]
        public static string DeleteObject(
            [Description("GUID of the object to delete")] string guid)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var obj = model.SelectModelObject(model.GetIdentifierByGUID(guid));
                if (obj == null) return $"❌ Object with GUID '{guid}' not found.";

                if (obj.Delete())
                {
                    model.CommitChanges();
                    return $"✅ Object '{guid}' deleted.";
                }
                return $"❌ Failed to delete object '{guid}'.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // -----------------------------------------------------------------------
        // DeleteObjects — Delete multiple model objects by GUIDs
        // -----------------------------------------------------------------------

        [McpServerTool, Description(
            "Delete multiple model objects by their GUIDs. " +
            "Pass GUIDs as a semicolon-separated string. " +
            "Example: 'guid1;guid2;guid3'")]
        public static string DeleteObjects(
            [Description("Semicolon-separated GUIDs to delete")] string guids)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var guidList = guids.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                int deleted = 0, failed = 0;

                foreach (var g in guidList)
                {
                    try
                    {
                        var obj = model.SelectModelObject(model.GetIdentifierByGUID(g.Trim()));
                        if (obj != null && obj.Delete()) deleted++;
                        else failed++;
                    }
                    catch { failed++; }
                }

                model.CommitChanges();
                return $"✅ Deleted {deleted} objects. {(failed > 0 ? $"❌ {failed} failed." : "")}";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // -----------------------------------------------------------------------
        // ModifyPart — Change properties on an existing part
        // -----------------------------------------------------------------------

        [McpServerTool, Description(
            "Modify properties of an existing part (beam, column, plate) by GUID. " +
            "Change profile, material, name, class, or finish. " +
            "Only non-empty parameters are applied — omit fields you don't want to change.")]
        public static string ModifyPart(
            [Description("GUID of the part to modify")] string guid,
            [Description("New profile string, e.g. 'HEB300'. Omit to keep current.")] string profile = "",
            [Description("New material string, e.g. 'S355JR'. Omit to keep current.")] string material = "",
            [Description("New part name. Omit to keep current.")] string name = "",
            [Description("New class number (0-14). Omit to keep current.")] string partClass = "",
            [Description("New finish string. Omit to keep current.")] string finish = "")
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var part = model.SelectModelObject(model.GetIdentifierByGUID(guid)) as TSM.Part;
                if (part == null) return $"❌ Part with GUID '{guid}' not found.";

                var changes = new List<string>();

                if (!string.IsNullOrEmpty(profile))
                {
                    part.Profile.ProfileString = profile;
                    changes.Add($"Profile → {profile}");
                }
                if (!string.IsNullOrEmpty(material))
                {
                    part.Material.MaterialString = material;
                    changes.Add($"Material → {material}");
                }
                if (!string.IsNullOrEmpty(name))
                {
                    part.Name = name;
                    changes.Add($"Name → {name}");
                }
                if (!string.IsNullOrEmpty(partClass))
                {
                    part.Class = partClass;
                    changes.Add($"Class → {partClass}");
                }
                if (!string.IsNullOrEmpty(finish))
                {
                    part.Finish = finish;
                    changes.Add($"Finish → {finish}");
                }

                if (changes.Count == 0) return "⚠️ No properties specified to change.";

                if (part.Modify())
                {
                    model.CommitChanges();
                    return $"✅ Part modified: {string.Join(", ", changes)}";
                }
                return "❌ Modify failed. Check that profile/material strings match the catalog.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // -----------------------------------------------------------------------
        // GetModelExtents — Bounding box of the entire model
        // -----------------------------------------------------------------------

        [McpServerTool, Description(
            "Get the bounding box (extents) of the entire model. " +
            "Returns min/max X, Y, Z coordinates in millimeters.")]
        public static string GetModelExtents()
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
                int count = 0;

                var enumerator = model.GetModelObjectSelector().GetAllObjectsWithType(
                    ModelObject.ModelObjectEnum.BEAM);
                while (enumerator.MoveNext())
                {
                    var part = enumerator.Current as TSM.Part;
                    if (part == null) continue;
                    var solid = part.GetSolid();
                    if (solid == null) continue;

                    var mn = solid.MinimumPoint;
                    var mx = solid.MaximumPoint;
                    if (mn.X < minX) minX = mn.X;
                    if (mn.Y < minY) minY = mn.Y;
                    if (mn.Z < minZ) minZ = mn.Z;
                    if (mx.X > maxX) maxX = mx.X;
                    if (mx.Y > maxY) maxY = mx.Y;
                    if (mx.Z > maxZ) maxZ = mx.Z;
                    count++;
                }

                if (count == 0) return "⚠️ No parts found in model.";

                return $"📐 MODEL EXTENTS ({count} parts)\n" +
                       $"  Min: ({minX:F0}, {minY:F0}, {minZ:F0})\n" +
                       $"  Max: ({maxX:F0}, {maxY:F0}, {maxZ:F0})\n" +
                       $"  Size: {maxX - minX:F0} × {maxY - minY:F0} × {maxZ - minZ:F0} mm";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }



    }
}
