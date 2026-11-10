using System;
using System.Collections.Generic;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Tekla.Structures.Geometry3d;
using TSM = Tekla.Structures.Model;

namespace TeklaMcpServer.Tools
{
    [McpServerToolType]
    public static class SteelConnectionTools
    {
        // =====================================================================
        //  PHASE 7: ADVANCED STEEL CONNECTION TOOLS
        // =====================================================================

        [McpServerTool, Description(
            "Create a full bolt array between two parts with custom spacing. " +
            "BoltDistX/Y are comma-separated spacing strings (e.g. '60,60,60' for 4 bolts). " +
            "FirstPosition/SecondPosition define the bolt group's local coordinate system. " +
            "BoltStandard must match catalog. BoltType: 'SITE' or 'WORKSHOP'.")]
        public static string CreateBoltGroup(
            string primaryGuid, string secondaryGuid,
            double firstPosX, double firstPosY, double firstPosZ,
            double secondPosX, double secondPosY, double secondPosZ,
            double boltSize = 20.0,
            string boltStandard = "7990",
            string boltType = "SITE",
            string boltDistX = "60",
            string boltDistY = "60",
            double tolerance = 2.0)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var partToBeBolted = model.SelectModelObject(model.GetIdentifierByGUID(primaryGuid)) as TSM.Part;
                var partToBoltTo = model.SelectModelObject(model.GetIdentifierByGUID(secondaryGuid)) as TSM.Part;
                if (partToBeBolted == null) return "❌ Primary part not found from GUID.";
                if (partToBoltTo == null) return "❌ Secondary part not found from GUID.";

                var boltArray = new TSM.BoltArray();
                boltArray.PartToBeBolted = partToBeBolted;
                boltArray.PartToBoltTo = partToBoltTo;
                boltArray.FirstPosition = new Point(firstPosX, firstPosY, firstPosZ);
                boltArray.SecondPosition = new Point(secondPosX, secondPosY, secondPosZ);
                boltArray.BoltSize = boltSize;
                boltArray.BoltStandard = boltStandard;
                boltArray.Tolerance = tolerance;
                boltArray.Bolt = true;

                if (boltType.Equals("WORKSHOP", StringComparison.OrdinalIgnoreCase))
                    boltArray.BoltType = TSM.BoltGroup.BoltTypeEnum.BOLT_TYPE_WORKSHOP;
                else
                    boltArray.BoltType = TSM.BoltGroup.BoltTypeEnum.BOLT_TYPE_SITE;

                // Parse X distances
                if (!string.IsNullOrEmpty(boltDistX))
                {
                    foreach (var d in boltDistX.Split(','))
                    {
                        if (double.TryParse(d.Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double dist))
                            boltArray.AddBoltDistX(dist);
                    }
                }

                // Parse Y distances
                if (!string.IsNullOrEmpty(boltDistY))
                {
                    foreach (var d in boltDistY.Split(','))
                    {
                        if (double.TryParse(d.Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double dist))
                            boltArray.AddBoltDistY(dist);
                    }
                }

                if (!boltArray.Insert())
                    return "❌ Failed to insert bolt group. Check parts overlap at bolt position and bolt standard is valid.";

                model.CommitChanges();
                return $"✅ Bolt group created (GUID: {model.GetGUIDByIdentifier(boltArray.Identifier)}).";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Create a detailed weld between two parts with full control over weld type, size, and placement. " +
            "WeldType: FILLET (default), BEVEL_GROOVE_SINGLE_BEVEL_BUTT, SQUARE_GROOVE_SQUARE_BUTT, " +
            "PLUG, SLOT, SPOT, SEAM, EDGE. Size is throat thickness in mm.")]
        public static string CreateDetailedWeld(
            string primaryGuid, string secondaryGuid,
            double sizeAbove = 6.0, double sizeBelow = 0.0,
            string typeAbove = "FILLET", string typeBelow = "NONE",
            bool shopWeld = false, bool aroundWeld = false)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var mainPart = model.SelectModelObject(model.GetIdentifierByGUID(primaryGuid));
                var secondaryPart = model.SelectModelObject(model.GetIdentifierByGUID(secondaryGuid));
                if (mainPart == null) return "❌ Primary part not found from GUID.";
                if (secondaryPart == null) return "❌ Secondary part not found from GUID.";

                var weld = new TSM.Weld();
                weld.MainObject = mainPart;
                weld.SecondaryObject = secondaryPart;
                weld.SizeAbove = sizeAbove;
                weld.SizeBelow = sizeBelow;
                weld.ShopWeld = shopWeld;
                weld.AroundWeld = aroundWeld;
                weld.TypeAbove = ParseWeldType(typeAbove);
                weld.TypeBelow = ParseWeldType(typeBelow);

                if (!weld.Insert())
                    return "❌ Failed to insert weld. Ensure parts are adjacent/touching.";

                model.CommitChanges();
                return $"✅ Weld created (Type: {typeAbove}, Size: {sizeAbove}mm, GUID: {model.GetGUIDByIdentifier(weld.Identifier)}).";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Create a fitting (end cut/trim) on a part using a cutting plane. " +
            "The plane is defined by a point (planeX/Y/Z) and a normal direction (normalX/Y/Z). " +
            "The part is trimmed at this plane. Useful for angled end cuts and cope cuts.")]
        public static string CreateFitting(
            string guid,
            double planeX, double planeY, double planeZ,
            double normalX, double normalY, double normalZ)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var part = model.SelectModelObject(model.GetIdentifierByGUID(guid));
                if (part == null) return "❌ Part not found from GUID.";

                var fitting = new TSM.Fitting();
                fitting.Father = part;
                fitting.Plane = new TSM.Plane();
                fitting.Plane.Origin = new Point(planeX, planeY, planeZ);

                // Convert normal to AxisX/AxisY for Fitting plane
                var normal = new Vector(normalX, normalY, normalZ);
                // Find an arbitrary perpendicular vector for AxisX
                Vector axisX;
                if (Math.Abs(normal.X) < 0.9)
                    axisX = Vector.Cross(normal, new Vector(1, 0, 0));
                else
                    axisX = Vector.Cross(normal, new Vector(0, 1, 0));
                var axisY = Vector.Cross(normal, axisX);

                fitting.Plane.AxisX = axisX;
                fitting.Plane.AxisY = axisY;

                if (!fitting.Insert())
                    return "❌ Failed to insert fitting. Check that the plane intersects the part.";

                model.CommitChanges();
                return $"✅ Fitting created on part.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Create batch welds between multiple part pairs using semicolon-separated GUID pairs. " +
            "Format: 'primaryGUID1,secondaryGUID1;primaryGUID2,secondaryGUID2;...' " +
            "All welds share the same type, size, and shop/around settings.")]
        public static string CreateBatchWelds(
            string guidPairs,
            double sizeAbove = 6.0, double sizeBelow = 0.0,
            string typeAbove = "FILLET", string typeBelow = "NONE",
            bool shopWeld = false, bool aroundWeld = false)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var pairs = guidPairs.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                var weldTypeAbove = ParseWeldType(typeAbove);
                var weldTypeBelow = ParseWeldType(typeBelow);
                int created = 0, failed = 0;

                foreach (var pair in pairs)
                {
                    var parts = pair.Split(',');
                    if (parts.Length < 2) { failed++; continue; }

                    var mainPart = model.SelectModelObject(model.GetIdentifierByGUID(parts[0].Trim()));
                    var secondaryPart = model.SelectModelObject(model.GetIdentifierByGUID(parts[1].Trim()));
                    if (mainPart == null || secondaryPart == null) { failed++; continue; }

                    var weld = new TSM.Weld();
                    weld.MainObject = mainPart;
                    weld.SecondaryObject = secondaryPart;
                    weld.SizeAbove = sizeAbove;
                    weld.SizeBelow = sizeBelow;
                    weld.ShopWeld = shopWeld;
                    weld.AroundWeld = aroundWeld;
                    weld.TypeAbove = weldTypeAbove;
                    weld.TypeBelow = weldTypeBelow;

                    if (weld.Insert()) created++;
                    else failed++;
                }

                model.CommitChanges();
                return $"✅ Created {created} welds, {failed} failed.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static TSM.BaseWeld.WeldTypeEnum ParseWeldType(string type)
        {
            if (string.IsNullOrEmpty(type) || type.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                return TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_NONE;

            switch (type.Trim().ToUpperInvariant())
            {
                case "FILLET": return TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_FILLET;
                case "BEVEL_GROOVE_SINGLE_BEVEL_BUTT": return TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_BEVEL_GROOVE_SINGLE_BEVEL_BUTT;
                case "SQUARE_GROOVE_SQUARE_BUTT": return TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_SQUARE_GROOVE_SQUARE_BUTT;
                case "PLUG": return TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_PLUG;
                case "SLOT": return TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_SLOT;
                case "SPOT": return TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_SPOT;
                case "SEAM": return TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_SEAM;
                case "EDGE": return TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_EDGE;
                default: return TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_FILLET;
            }
        }
    }
}
