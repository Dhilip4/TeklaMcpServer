using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Tekla.Structures.Geometry3d;
using TSM = Tekla.Structures.Model;
using TeklaMcpServer.Utils;

namespace TeklaMcpServer.Tools
{
    [McpServerToolType]
    public static class ModelTools
    {
        [McpServerTool, Description("Force a connection check to the active Tekla 2022 instance")]
        public static string GetModelStatus()
        {
            try
            {
                var model = TeklaConnectionGuard.TryConnect();
                if (model != null)
                {
                    var info = model.GetInfo();
                    return $"Success: Model connected. Project: {info.ModelName}";
                }
                return "❌ No active model connection. Please open a model in Tekla 2022.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }



        [McpServerTool, Description("Create a steel Beam/Column between two 3D points. Provide profile (e.g. HEA300), material (e.g. S235JR), and start/end X,Y,Z coordinates.")]
        public static string ModelBeam(string profile, string material, double x1, double y1, double z1, double x2, double y2, double z2)
        {
            try
            {
                var beam = new TSM.Beam();
                beam.Profile.ProfileString = profile;
                beam.Material.MaterialString = material;
                beam.StartPoint = new Point(x1, y1, z1);
                beam.EndPoint = new Point(x2, y2, z2);
                beam.Class = "1";
                beam.Position.Depth = TSM.Position.DepthEnum.MIDDLE;
                beam.Position.Plane = TSM.Position.PlaneEnum.MIDDLE;
                beam.Position.Rotation = TSM.Position.RotationEnum.TOP;

                if (beam.Insert())
                {
                    var model = new TSM.Model();
                    model.CommitChanges();
                    var guid = model.GetGUIDByIdentifier(beam.Identifier);
                    return $"✅ Beam '{profile}' modeled. GUID: {guid}";
                }
                return "❌ Failed to model beam.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Spawn a 3D Contour Plate by passing semi-colon separated X,Y,Z coordinate boundaries (e.g. 0,0,0;1000,0,0;1000,1000,0;0,1000,0)")]
        public static string ModelContourPlate(string profile, string material, string pointCoordinates)
        {
            try
            {
                var plate = new TSM.ContourPlate();
                plate.Profile.ProfileString = profile;
                plate.Material.MaterialString = material;
                var points = pointCoordinates.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in points)
                {
                    var coords = p.Split(',');
                    if (coords.Length >= 3 && double.TryParse(coords[0], out double x) && double.TryParse(coords[1], out double y) && double.TryParse(coords[2], out double z))
                        plate.AddContourPoint(new TSM.ContourPoint(new Point(x, y, z), null));
                }
                if (plate.Insert())
                {
                    new TSM.Model().CommitChanges();
                    return $"✅ Contour Plate '{profile}' modeled successfully.";
                }
                return "❌ Failed to model Contour Plate.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Create a curved/haunched PolyBeam by passing semi-colon separated X,Y,Z contour points (minimum 3 points)")]
        public static string ModelPolyBeam(string profile, string material, string pointCoordinates)
        {
            try
            {
                var polybeam = new TSM.PolyBeam();
                polybeam.Profile.ProfileString = profile;
                polybeam.Material.MaterialString = material;
                var points = pointCoordinates.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in points)
                {
                    var c = p.Split(',');
                    if (c.Length >= 3 && double.TryParse(c[0], out double x) && double.TryParse(c[1], out double y) && double.TryParse(c[2], out double z))
                        polybeam.AddContourPoint(new TSM.ContourPoint(new Point(x, y, z), null));
                }
                if (polybeam.Insert())
                {
                    new TSM.Model().CommitChanges();
                    return $"✅ PolyBeam '{profile}' modeled with {points.Length} points.";
                }
                return "❌ Failed to model PolyBeam.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Models a physical 3D Weld between two parts using their GUIDs")]
        public static string AddWeld(string primaryGuid, string secondaryGuid, double weldSize)
        {
            try
            {
                var model = new TSM.Model();
                var primary = model.SelectModelObject(model.GetIdentifierByGUID(primaryGuid)) as TSM.Part;
                var secondary = model.SelectModelObject(model.GetIdentifierByGUID(secondaryGuid)) as TSM.Part;
                if (primary == null || secondary == null) return "❌ One or both parts not found from GUID.";

                var weld = new TSM.Weld();
                weld.MainObject = primary;
                weld.SecondaryObject = secondary;
                weld.TypeAbove = TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_FILLET;
                weld.SizeAbove = weldSize;
                weld.TypeBelow = TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_NONE;

                if (weld.Insert()) { model.CommitChanges(); return $"✅ {weldSize}mm Fillet Weld inserted successfully."; }
                return "❌ Failed to insert weld.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Creates a basic BoltArray connecting two parts. Bolts are placed near the secondary part's origin.")]
        public static string AddBolts(string primaryGuid, string secondaryGuid, double boltSize, string boltStandard)
        {
            try
            {
                var model = new TSM.Model();
                var primary = model.SelectModelObject(model.GetIdentifierByGUID(primaryGuid)) as TSM.Part;
                var secondary = model.SelectModelObject(model.GetIdentifierByGUID(secondaryGuid)) as TSM.Part;
                if (primary == null || secondary == null) return "❌ One or both parts not found from GUID.";

                var bolt = new TSM.BoltArray();
                bolt.PartToBeBolted = secondary;
                bolt.PartToBoltTo = primary;
                bolt.BoltSize = boltSize;
                bolt.BoltStandard = boltStandard;
                var cs = secondary.GetCoordinateSystem();
                bolt.FirstPosition = cs.Origin;
                bolt.SecondPosition = new Point(cs.Origin.X, cs.Origin.Y, cs.Origin.Z + 100);
                bolt.AddBoltDistX(0.0);
                bolt.AddBoltDistY(0.0);

                if (bolt.Insert()) { model.CommitChanges(); return $"✅ {boltSize}mm Bolt group added."; }
                return "❌ Failed to insert bolts.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Create a BoltCircle between two parts using their GUIDs")]
        public static string AddBoltCircle(string primaryGuid, string secondaryGuid, double boltSize, string boltStandard, int numberOfBolts, double diameter)
        {
            try
            {
                var model = new TSM.Model();
                var primary = model.SelectModelObject(model.GetIdentifierByGUID(primaryGuid)) as TSM.Part;
                var secondary = model.SelectModelObject(model.GetIdentifierByGUID(secondaryGuid)) as TSM.Part;
                if (primary == null || secondary == null) return "❌ Parts not found from GUID.";

                var bc = new TSM.BoltCircle();
                bc.PartToBoltTo = primary;
                bc.PartToBeBolted = secondary;
                bc.BoltSize = boltSize;
                bc.BoltStandard = boltStandard;
                bc.NumberOfBolts = numberOfBolts;
                bc.Diameter = diameter;
                var cs = secondary.GetCoordinateSystem();
                bc.FirstPosition = cs.Origin;
                bc.SecondPosition = new Point(cs.Origin.X, cs.Origin.Y, cs.Origin.Z + 100);

                if (bc.Insert()) { model.CommitChanges(); return $"✅ BoltCircle ({numberOfBolts} x {boltSize}mm) created."; }
                return "❌ Failed to insert BoltCircle.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Creates a System Component, Plugin, or Custom Component between two parts using GUIDs. Set connectionNumber to >0 for system macros, -100000 for Plugins (PEB), or -1 for Custom. Optional: define attributeFile to load preset (e.g., 'standard').")]
        public static string AddConnection(string primaryGuid, string secondaryGuid, string connectionName, int connectionNumber, string attributeFile = "standard")
        {
            try
            {
                var model = new TSM.Model();
                var primary = model.SelectModelObject(model.GetIdentifierByGUID(primaryGuid)) as TSM.ModelObject;
                var secondary = model.SelectModelObject(model.GetIdentifierByGUID(secondaryGuid)) as TSM.ModelObject;
                if (primary == null || secondary == null) return "❌ One or both parts not found from GUID.";

                var conn = new TSM.Connection();
                conn.Name = connectionName;
                conn.Number = connectionNumber;
                if (!string.IsNullOrEmpty(attributeFile)) conn.LoadAttributesFromFile(attributeFile);
                conn.SetPrimaryObject(primary);
                conn.SetSecondaryObject(secondary);

                if (conn.Insert()) { model.CommitChanges(); return $"✅ Connection '{connectionName}' (#{connectionNumber}) added."; }
                return "❌ Failed to insert connection.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Applies a physical Cut/Notch using a solid Boolean operation. The cutting part is consumed/deleted afterwards.")]
        public static string ApplyPartCut(string targetGuid, string cuttingPartGuid)
        {
            try
            {
                var model = new TSM.Model();
                var target = model.SelectModelObject(model.GetIdentifierByGUID(targetGuid)) as TSM.Part;
                var cut = model.SelectModelObject(model.GetIdentifierByGUID(cuttingPartGuid)) as TSM.Part;
                if (target == null || cut == null) return "❌ Target or Cut part not found from GUID.";

                var boolPart = new TSM.BooleanPart();
                boolPart.Father = target;
                boolPart.OperativePart = cut;
                if (boolPart.Insert()) { cut.Delete(); model.CommitChanges(); return "✅ Part cut applied."; }
                return "❌ Failed to apply boolean part cut.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Apply a CutPlane to a part at a specific point and direction using its GUID")]
        public static string AddCutPlane(string guid, double planeX, double planeY, double planeZ, double normalX, double normalY, double normalZ)
        {
            try
            {
                var model = new TSM.Model();
                var part = model.SelectModelObject(model.GetIdentifierByGUID(guid)) as TSM.Part;
                if (part == null) return "❌ Part not found.";
                var cut = new TSM.CutPlane();
                cut.Father = part;
                cut.Plane = new TSM.Plane();
                cut.Plane.Origin = new Point(planeX, planeY, planeZ);
                cut.Plane.AxisX = new Vector(normalX, normalY, normalZ);
                if (cut.Insert()) { model.CommitChanges(); return "✅ Cut plane applied."; }
                return "❌ Failed to apply cut plane.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Apply a Fitting (line cut) to trim a part end using its GUID")]
        public static string AddFitting(string guid, double planeX, double planeY, double planeZ, double normalX, double normalY, double normalZ)
        {
            try
            {
                var model = new TSM.Model();
                var part = model.SelectModelObject(model.GetIdentifierByGUID(guid)) as TSM.Part;
                if (part == null) return "❌ Part not found.";
                var fit = new TSM.Fitting();
                fit.Father = part;
                fit.Plane = new TSM.Plane();
                fit.Plane.Origin = new Point(planeX, planeY, planeZ);
                fit.Plane.AxisX = new Vector(normalX, normalY, normalZ);
                if (fit.Insert()) { model.CommitChanges(); return "✅ Fitting applied."; }
                return "❌ Failed to apply fitting.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Move a model object by a displacement vector (dX, dY, dZ) using its GUID")]
        public static string MoveObject(string guid, double dX, double dY, double dZ)
        {
            try
            {
                var model = new TSM.Model();
                var obj = model.SelectModelObject(model.GetIdentifierByGUID(guid));
                if (obj == null) return "❌ Object not found.";
                if (TSM.Operations.Operation.MoveObject(obj, new Vector(dX, dY, dZ)))
                { model.CommitChanges(); return $"✅ Object moved by ({dX}, {dY}, {dZ})."; }
                return "❌ Move operation failed.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Copy a model object at a displacement offset (dX, dY, dZ) using its GUID. Returns the new copy's GUID.")]
        public static string CopyObject(string guid, double dX, double dY, double dZ)
        {
            try
            {
                var model = new TSM.Model();
                var obj = model.SelectModelObject(model.GetIdentifierByGUID(guid));
                if (obj == null) return "❌ Object not found.";
                var copy = TSM.Operations.Operation.CopyObject(obj, new Vector(dX, dY, dZ));
                if (copy != null) { model.CommitChanges(); return $"✅ Object copied. New GUID: {model.GetGUIDByIdentifier(copy.Identifier)}"; }
                return "❌ Copy operation failed.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Split a beam into two parts at a specific 3D point using its GUID")]
        public static string SplitBeam(string guid, double splitX, double splitY, double splitZ)
        {
            try
            {
                var model = new TSM.Model();
                var beam = model.SelectModelObject(model.GetIdentifierByGUID(guid)) as TSM.Beam;
                if (beam == null) return "❌ Beam not found from GUID.";
                TSM.Operations.Operation.Split(beam, new Point(splitX, splitY, splitZ));
                model.CommitChanges();
                return $"✅ Beam split at ({splitX}, {splitY}, {splitZ}).";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Merge two collinear beams into one using their GUIDs")]
        public static string CombineBeams(string guid1, string guid2)
        {
            try
            {
                var model = new TSM.Model();
                var beam1 = model.SelectModelObject(model.GetIdentifierByGUID(guid1)) as TSM.Beam;
                var beam2 = model.SelectModelObject(model.GetIdentifierByGUID(guid2)) as TSM.Beam;
                if (beam1 == null || beam2 == null) return "❌ One or both beams not found.";
                TSM.Operations.Operation.Combine(beam1, beam2);
                model.CommitChanges();
                return "✅ Beams combined successfully.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        //[McpServerTool, Description("Apply a surface treatment (paint/finish) to a part by GUID")]
        public static string AddSurfaceTreatment(string guid, string surfaceName, double thickness)
        {
            try
            {
                var model = new TSM.Model();
                var part = model.SelectModelObject(model.GetIdentifierByGUID(guid)) as TSM.Part;
                if (part == null) return "❌ Part not found.";
                var st = new TSM.SurfaceTreatment();
                st.Name = surfaceName; st.Thickness = thickness; st.Father = part;
                if (st.Insert()) { model.CommitChanges(); return $"✅ Surface treatment '{surfaceName}' applied."; }
                return "❌ Failed to apply surface treatment.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Place a single reinforcing bar along a part by GUID. Provide start/end X,Y,Z and rebar size (e.g. 12).")]
        public static string AddSingleRebar(string partGuid, string rebarSize, string rebarGrade, double x1, double y1, double z1, double x2, double y2, double z2)
        {
            try
            {
                var model = new TSM.Model();
                var part = model.SelectModelObject(model.GetIdentifierByGUID(partGuid)) as TSM.Part;
                if (part == null) return "❌ Part not found.";
                var rebar = new TSM.SingleRebar();
                rebar.Father = part; rebar.Size = rebarSize; rebar.Grade = rebarGrade;
                rebar.Name = "Rebar"; rebar.Class = 3;
                rebar.Polygon.Points.Add(new Point(x1, y1, z1));
                rebar.Polygon.Points.Add(new Point(x2, y2, z2));
                rebar.StartHook.Shape = TSM.RebarHookData.RebarHookShapeEnum.NO_HOOK;
                rebar.EndHook.Shape = TSM.RebarHookData.RebarHookShapeEnum.NO_HOOK;
                if (rebar.Insert()) { model.CommitChanges(); return $"✅ Single rebar (size {rebarSize}) inserted."; }
                return "❌ Failed to insert single rebar.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Create a rebar group (cage) inside a part by GUID with specified spacing")]
        public static string AddRebarGroup(string partGuid, string rebarSize, string rebarGrade, double spacing, double x1, double y1, double z1, double x2, double y2, double z2)
        {
            try
            {
                var model = new TSM.Model();
                var part = model.SelectModelObject(model.GetIdentifierByGUID(partGuid)) as TSM.Part;
                if (part == null) return "❌ Part not found.";
                var group = new TSM.RebarGroup();
                group.Father = part; group.Size = rebarSize; group.Grade = rebarGrade;
                group.Name = "RebarGroup"; group.Class = 3;
                group.SpacingType = TSM.BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_TARGET_SPACE;
                var poly1 = new TSM.Polygon();
                poly1.Points.Add(new Point(x1, y1, z1));
                poly1.Points.Add(new Point(x2, y2, z2));
                group.Polygons.Add(poly1);
                group.Spacings.Add(spacing);
                group.StartHook.Shape = TSM.RebarHookData.RebarHookShapeEnum.NO_HOOK;
                group.EndHook.Shape = TSM.RebarHookData.RebarHookShapeEnum.NO_HOOK;
                if (group.Insert()) { model.CommitChanges(); return $"✅ Rebar group (spacing {spacing}mm) inserted."; }
                return "❌ Failed to insert rebar group.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        //[McpServerTool, Description("Insert a construction reference line between two 3D points")]
        public static string AddControlLine(double x1, double y1, double z1, double x2, double y2, double z2)
        {
            try
            {
                var cl = new TSM.ControlLine();
                cl.Line = new LineSegment(new Point(x1, y1, z1), new Point(x2, y2, z2));
                if (cl.Insert()) { new TSM.Model().CommitChanges(); return "✅ Control line inserted."; }
                return "❌ Failed to insert control line.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        //[McpServerTool, Description("Insert a construction reference plane at a point with a normal direction")]
        public static string AddControlPlane(double originX, double originY, double originZ, double normalX, double normalY, double normalZ, double extent)
        {
            try
            {
                var cp = new TSM.ControlPlane();
                cp.Plane = new TSM.Plane();
                cp.Plane.Origin = new Point(originX, originY, originZ);
                cp.Plane.AxisX = new Vector(normalX, normalY, normalZ);
                if (cp.Insert()) { new TSM.Model().CommitChanges(); return "✅ Control plane inserted."; }
                return "❌ Failed to insert control plane.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        //[McpServerTool, Description("Insert a construction reference circle through three 3D points")]
        public static string AddControlCircle(double x1, double y1, double z1, double x2, double y2, double z2, double x3, double y3, double z3)
        {
            try
            {
                var cc = new TSM.ControlCircle(new Point(x1, y1, z1), new Point(x2, y2, z2), new Point(x3, y3, z3));
                if (cc.Insert()) { new TSM.Model().CommitChanges(); return "✅ Control circle inserted."; }
                return "❌ Failed to insert control circle.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Place a labeled reference point in the 3D model")]
        public static string AddControlPoint(double x, double y, double z)
        {
            try
            {
                var cp = new TSM.ControlPoint(new Point(x, y, z));
                if (cp.Insert()) { new TSM.Model().CommitChanges(); return $"✅ Control point inserted at ({x}, {y}, {z})."; }
                return "❌ Failed to insert control point.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Define a geo-located base point for BIM coordination exports")]
        public static string AddBasePoint(string name, double x, double y, double z, double latitude, double longitude, double altitude)
        {
            try
            {
                var bp = new TSM.BasePoint(name);
                bp.LocationInModelX = x; bp.LocationInModelY = y; bp.LocationInModelZ = z;
                bp.Latitude = latitude; bp.Longitude = longitude; bp.Elevation = altitude;
                if (bp.Insert()) { new TSM.Model().CommitChanges(); return $"✅ Base point '{name}' created."; }
                return "❌ Failed to create base point.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        //[McpServerTool, Description("Insert an external reference model (IFC/DWG/DGN) into the Tekla model")]
        public static string InsertReferenceModel(string filePath, double x, double y, double z)
        {
            try
            {
                var refModel = new TSM.ReferenceModel();
                refModel.Filename = filePath;
                refModel.Position = new Point(x, y, z);
                if (refModel.Insert()) { new TSM.Model().CommitChanges(); return $"✅ Reference model inserted from '{filePath}'."; }
                return "❌ Failed to insert reference model.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Check if numbered parts are up to date in the model")]
        public static string CheckNumberingStatus()
        {
            try
            {
                return TSM.Operations.Operation.IsNumberingUpToDateAll()
                    ? "✅ All numbering is up to date."
                    : "⚠️ Numbering is NOT up to date. Some parts need renumbering.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        //[McpServerTool, Description("Force Tekla to auto-calculate all pour unit boundaries")]
        public static string CalculatePourUnits()
        {
            try { TSM.Operations.Operation.CalculatePourUnits(); return "✅ Pour units calculated."; }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        //[McpServerTool, Description("Export the entire model for Trimble Connect web viewer")]
        public static string SaveAsWebModel(string outputPath)
        {
            try
            {
                if (TSM.Operations.Operation.SaveAsWebModel(outputPath))
                    return $"✅ Web model saved to '{outputPath}'.";
                return "❌ Failed to save web model.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // =====================================================================
        //  PHASE 1: BATCH CREATION & POWER TOOLS
        // =====================================================================

        [McpServerTool, Description(
            "Create multiple beams in one call. Pass semicolon-separated coordinate sets: " +
            "'x1,y1,z1,x2,y2,z2;x1,y1,z1,x2,y2,z2;...' All beams share the same profile and material.")]
        public static string CreateBeams(string profile, string material, string coordinateSets)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var sets = coordinateSets.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                int created = 0;
                var guids = new List<string>();

                foreach (var set in sets)
                {
                    var c = set.Split(',');
                    if (c.Length < 6) continue;
                    if (!double.TryParse(c[0], out double x1) || !double.TryParse(c[1], out double y1) ||
                        !double.TryParse(c[2], out double z1) || !double.TryParse(c[3], out double x2) ||
                        !double.TryParse(c[4], out double y2) || !double.TryParse(c[5], out double z2)) continue;

                    var beam = new TSM.Beam();
                    beam.Profile.ProfileString = profile;
                    beam.Material.MaterialString = material;
                    beam.StartPoint = new Point(x1, y1, z1);
                    beam.EndPoint = new Point(x2, y2, z2);
                    beam.Class = "1";
                    beam.Position.Depth = TSM.Position.DepthEnum.MIDDLE;
                    beam.Position.Plane = TSM.Position.PlaneEnum.MIDDLE;
                    beam.Position.Rotation = TSM.Position.RotationEnum.TOP;

                    if (beam.Insert())
                    {
                        created++;
                        guids.Add(model.GetGUIDByIdentifier(beam.Identifier));
                    }
                }
                model.CommitChanges();
                return $"✅ Created {created}/{sets.Length} beams ({profile}). GUIDs:\n{string.Join("\n", guids)}";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Create multiple vertical columns in one call. Pass semicolon-separated base points: " +
            "'x,y,z;x,y,z;...' All columns share the same profile, material, and height.")]
        public static string CreateColumns(string profile, string material, double height, string basePoints)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var points = basePoints.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                int created = 0;
                var guids = new List<string>();

                foreach (var pt in points)
                {
                    var c = pt.Split(',');
                    if (c.Length < 3) continue;
                    if (!double.TryParse(c[0], out double x) || !double.TryParse(c[1], out double y) ||
                        !double.TryParse(c[2], out double z)) continue;

                    var col = new TSM.Beam(TSM.Beam.BeamTypeEnum.COLUMN);
                    col.Profile.ProfileString = profile;
                    col.Material.MaterialString = material;
                    col.StartPoint = new Point(x, y, z);
                    col.EndPoint = new Point(x, y, z + height);
                    col.Class = "2";
                    col.Position.Depth = TSM.Position.DepthEnum.MIDDLE;
                    col.Position.Plane = TSM.Position.PlaneEnum.MIDDLE;
                    col.Position.Rotation = TSM.Position.RotationEnum.TOP;

                    if (col.Insert())
                    {
                        created++;
                        guids.Add(model.GetGUIDByIdentifier(col.Identifier));
                    }
                }
                model.CommitChanges();
                return $"✅ Created {created}/{points.Length} columns ({profile}, H={height}mm). GUIDs:\n{string.Join("\n", guids)}";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Create multiple contour plates in one call. Each plate is a set of corner points. " +
            "Pass pipe-separated plates, each with semicolon-separated X,Y,Z points: " +
            "'x,y,z;x,y,z;x,y,z|x,y,z;x,y,z;x,y,z;x,y,z'")]
        public static string CreatePlates(string profile, string material, string plateSets)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var plates = plateSets.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                int created = 0;
                var guids = new List<string>();

                foreach (var plateStr in plates)
                {
                    var plate = new TSM.ContourPlate();
                    plate.Profile.ProfileString = profile;
                    plate.Material.MaterialString = material;
                    plate.Class = "1";

                    var points = plateStr.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var pt in points)
                    {
                        var c = pt.Split(',');
                        if (c.Length >= 3 && double.TryParse(c[0], out double x) &&
                            double.TryParse(c[1], out double y) && double.TryParse(c[2], out double z))
                            plate.AddContourPoint(new TSM.ContourPoint(new Point(x, y, z), null));
                    }

                    if (plate.Insert())
                    {
                        created++;
                        guids.Add(model.GetGUIDByIdentifier(plate.Identifier));
                    }
                }
                model.CommitChanges();
                return $"✅ Created {created}/{plates.Length} plates ({profile}). GUIDs:\n{string.Join("\n", guids)}";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Auto-place columns at all grid intersections. Reads the existing model grid and " +
            "creates a column at every X-Y intersection at the specified Z elevation.")]
        public static string CreateColumnsOnGrid(string profile, string material, double height, double baseElevation = 0)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                // Find the grid in the model
                var enumerator = model.GetModelObjectSelector().GetAllObjectsWithType(
                    TSM.ModelObject.ModelObjectEnum.GRID);

                TSM.Grid grid = null;
                while (enumerator.MoveNext())
                {
                    grid = enumerator.Current as TSM.Grid;
                    if (grid != null) break;
                }
                if (grid == null) return "❌ No grid found in model. Create a grid first.";

                // Parse grid coordinates
                var xCoords = ParseGridCoordinates(grid.CoordinateX);
                var yCoords = ParseGridCoordinates(grid.CoordinateY);

                int created = 0;
                var guids = new List<string>();

                foreach (double x in xCoords)
                {
                    foreach (double y in yCoords)
                    {
                        var col = new TSM.Beam(TSM.Beam.BeamTypeEnum.COLUMN);
                        col.Profile.ProfileString = profile;
                        col.Material.MaterialString = material;
                        col.StartPoint = new Point(x, y, baseElevation);
                        col.EndPoint = new Point(x, y, baseElevation + height);
                        col.Class = "2";
                        col.Position.Depth = TSM.Position.DepthEnum.MIDDLE;
                        col.Position.Plane = TSM.Position.PlaneEnum.MIDDLE;
                        col.Position.Rotation = TSM.Position.RotationEnum.TOP;

                        if (col.Insert())
                        {
                            created++;
                            guids.Add(model.GetGUIDByIdentifier(col.Identifier));
                        }
                    }
                }
                model.CommitChanges();
                return $"✅ Created {created} columns at grid intersections ({xCoords.Count}x × {yCoords.Count}y). " +
                       $"Profile: {profile}, Height: {height}mm.\nGUIDs:\n{string.Join("\n", guids)}";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        /// <summary>Parse Tekla grid coordinate string "0 6000 12000 18000" into a list of doubles.</summary>
        private static List<double> ParseGridCoordinates(string coordString)
        {
            var result = new List<double>();
            if (string.IsNullOrEmpty(coordString)) return result;
            var parts = coordString.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                if (double.TryParse(p, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double val))
                    result.Add(val);
            }
            return result;
        }

        [McpServerTool, Description(
            "Create a curved beam through 3 points defining an arc. " +
            "Provide start, mid-arc, and end XYZ coordinates.")]
        public static string CreateCurvedBeam(string profile, string material,
            double x1, double y1, double z1,
            double xMid, double yMid, double zMid,
            double x2, double y2, double z2)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                // Use PolyBeam with arc chamfer for curved geometry
                var polybeam = new TSM.PolyBeam();
                polybeam.Profile.ProfileString = profile;
                polybeam.Material.MaterialString = material;
                polybeam.Class = "1";

                polybeam.AddContourPoint(new TSM.ContourPoint(new Point(x1, y1, z1), null));
                polybeam.AddContourPoint(new TSM.ContourPoint(new Point(xMid, yMid, zMid),
                    new TSM.Chamfer(0, 0, TSM.Chamfer.ChamferTypeEnum.CHAMFER_ARC_POINT)));
                polybeam.AddContourPoint(new TSM.ContourPoint(new Point(x2, y2, z2), null));

                if (polybeam.Insert())
                {
                    model.CommitChanges();
                    var guid = model.GetGUIDByIdentifier(polybeam.Identifier);
                    return $"✅ Curved beam '{profile}' created through 3 points. GUID: {guid}";
                }
                return "❌ Failed to create curved beam.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Create segmented straight beams along an arc path. Specify center, radius, " +
            "start angle, end angle (degrees), number of segments, and elevation Z.")]
        public static string CreateBeamsOnArc(string profile, string material,
            double centerX, double centerY, double z,
            double radius, double startAngleDeg, double endAngleDeg, int segments)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";
                if (segments < 1) return "❌ Need at least 1 segment.";

                double startRad = startAngleDeg * Math.PI / 180.0;
                double endRad = endAngleDeg * Math.PI / 180.0;
                double step = (endRad - startRad) / segments;

                int created = 0;
                var guids = new List<string>();

                for (int i = 0; i < segments; i++)
                {
                    double a1 = startRad + i * step;
                    double a2 = startRad + (i + 1) * step;

                    var beam = new TSM.Beam();
                    beam.Profile.ProfileString = profile;
                    beam.Material.MaterialString = material;
                    beam.StartPoint = new Point(
                        centerX + radius * Math.Cos(a1),
                        centerY + radius * Math.Sin(a1), z);
                    beam.EndPoint = new Point(
                        centerX + radius * Math.Cos(a2),
                        centerY + radius * Math.Sin(a2), z);
                    beam.Class = "1";
                    beam.Position.Depth = TSM.Position.DepthEnum.MIDDLE;
                    beam.Position.Plane = TSM.Position.PlaneEnum.MIDDLE;
                    beam.Position.Rotation = TSM.Position.RotationEnum.TOP;

                    if (beam.Insert())
                    {
                        created++;
                        guids.Add(model.GetGUIDByIdentifier(beam.Identifier));
                    }
                }
                model.CommitChanges();
                return $"✅ Created {created}/{segments} beams on arc (R={radius}, {startAngleDeg}°→{endAngleDeg}°).\n" +
                       $"GUIDs:\n{string.Join("\n", guids)}";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Create a concrete pad footing. Provide center X,Y, base elevation Z, " +
            "width (X), length (Y), and thickness (Z depth).")]
        public static string CreatePadFooting(string material, double centerX, double centerY, double baseZ,
            double width, double length, double thickness)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                double hw = width / 2.0;
                double hl = length / 2.0;

                var pad = new TSM.ContourPlate();
                pad.Profile.ProfileString = thickness.ToString("0");
                pad.Material.MaterialString = material;
                pad.Class = "8";  // Concrete class
                pad.Name = "PAD FOOTING";

                pad.AddContourPoint(new TSM.ContourPoint(new Point(centerX - hw, centerY - hl, baseZ), null));
                pad.AddContourPoint(new TSM.ContourPoint(new Point(centerX + hw, centerY - hl, baseZ), null));
                pad.AddContourPoint(new TSM.ContourPoint(new Point(centerX + hw, centerY + hl, baseZ), null));
                pad.AddContourPoint(new TSM.ContourPoint(new Point(centerX - hw, centerY + hl, baseZ), null));

                pad.Position.Depth = TSM.Position.DepthEnum.BEHIND;

                if (pad.Insert())
                {
                    model.CommitChanges();
                    var guid = model.GetGUIDByIdentifier(pad.Identifier);
                    return $"✅ Pad footing created ({width}x{length}x{thickness}mm, {material}). GUID: {guid}";
                }
                return "❌ Failed to create pad footing.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Move multiple model objects by the same displacement vector. " +
            "Pass semicolon-separated GUIDs: 'guid1;guid2;guid3'")]
        public static string MoveParts(string guids, double dX, double dY, double dZ)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var guidList = guids.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                int moved = 0;
                var displacement = new Vector(dX, dY, dZ);

                foreach (var g in guidList)
                {
                    var obj = model.SelectModelObject(model.GetIdentifierByGUID(g.Trim()));
                    if (obj != null && TSM.Operations.Operation.MoveObject(obj, displacement))
                        moved++;
                }
                model.CommitChanges();
                return $"✅ Moved {moved}/{guidList.Length} objects by ({dX}, {dY}, {dZ}).";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Copy multiple model objects by the same displacement vector. " +
            "Pass semicolon-separated GUIDs. Returns new GUIDs.")]
        public static string CopyParts(string guids, double dX, double dY, double dZ)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var guidList = guids.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                int copied = 0;
                var newGuids = new List<string>();
                var displacement = new Vector(dX, dY, dZ);

                foreach (var g in guidList)
                {
                    var obj = model.SelectModelObject(model.GetIdentifierByGUID(g.Trim()));
                    if (obj == null) continue;
                    var copy = TSM.Operations.Operation.CopyObject(obj, displacement);
                    if (copy != null)
                    {
                        copied++;
                        newGuids.Add(model.GetGUIDByIdentifier(copy.Identifier));
                    }
                }
                model.CommitChanges();
                return $"✅ Copied {copied}/{guidList.Length} objects by ({dX}, {dY}, {dZ}).\n" +
                       $"New GUIDs:\n{string.Join("\n", newGuids)}";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Create multiple welds in one call. Pass semicolon-separated pairs of 'primaryGUID,secondaryGUID': " +
            "'guid1,guid2;guid3,guid4;...' All welds share the same size.")]
        public static string CreateWelds(double weldSize, string guidPairs)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var pairs = guidPairs.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                int created = 0;

                foreach (var pair in pairs)
                {
                    var gs = pair.Split(',');
                    if (gs.Length < 2) continue;
                    var primary = model.SelectModelObject(model.GetIdentifierByGUID(gs[0].Trim())) as TSM.Part;
                    var secondary = model.SelectModelObject(model.GetIdentifierByGUID(gs[1].Trim())) as TSM.Part;
                    if (primary == null || secondary == null) continue;

                    var weld = new TSM.Weld();
                    weld.MainObject = primary;
                    weld.SecondaryObject = secondary;
                    weld.TypeAbove = TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_FILLET;
                    weld.SizeAbove = weldSize;
                    weld.TypeBelow = TSM.BaseWeld.WeldTypeEnum.WELD_TYPE_NONE;
                    if (weld.Insert()) created++;
                }
                model.CommitChanges();
                return $"✅ Created {created}/{pairs.Length} welds ({weldSize}mm fillet).";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }
    }
}
