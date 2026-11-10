using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Tekla.Structures;
using Tekla.Structures.Model;
using Tekla.Structures.Drawing;
using Tekla.Structures.Drawing.UI;
using Tekla.Structures.Geometry3d;
using TSM = Tekla.Structures.Model;
using TSD = Tekla.Structures.Drawing;

namespace TeklaMcpServer.Tools
{
    [McpServerToolType]
    public static class DrawingDetailTools
    {
        [McpServerTool, Description("Add part marks to all parts in the currently active drawing")]
        public static string AddPartMarks()
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing open.";
                var allObjects = active.GetSheet().GetAllObjects(typeof(TSD.Part));
                int count = 0;
                while (allObjects.MoveNext())
                {
                    if (allObjects.Current is TSD.Part part)
                    {
                        Mark mark = new Mark(part);
                        mark.Attributes.Content.Clear();
                        mark.Attributes.Content.Add(new UserDefinedElement("PROFILE"));
                        mark.InsertionPoint = new Point(0, 0, 0);
                        if (mark.Insert()) count++;
                    }
                }
                active.CommitChanges();
                return $"✅ Added {count} part marks to active drawing.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Add part marks to parts matching an Assembly Position (e.g. 'LC*') and/or Name filter. Set onlyMainParts to true to mark only the assembly's primary part.")]
        public static string AddPartMarksFiltered(string positionFilter = "", string nameFilter = "", bool onlyMainParts = false)
        {
            try
            {
                var myModel = new TSM.Model();
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing open.";

                string posPattern = positionFilter.Replace("*", "").ToUpper();
                string namePattern = nameFilter.Replace("*", "").ToUpper();
                
                var views = active.GetSheet().GetViews();
                ArrayList partsToSelect = new ArrayList();
                int count = 0;

                while (views.MoveNext())
                {
                    if (views.Current is TSD.View view)
                    {
                        var parts = view.GetAllObjects(typeof(TSD.Part));
                        while (parts.MoveNext())
                        {
                            if (parts.Current is TSD.Part part)
                            {
                                var modelObj = myModel.SelectModelObject(part.ModelIdentifier);
                                if (modelObj == null) continue;

                                bool match = true;

                                // Filter by Assembly Position
                                if (!string.IsNullOrEmpty(posPattern))
                                {
                                    string assemblyPos = "";
                                    modelObj.GetReportProperty("ASSEMBLY_POS", ref assemblyPos);
                                    if (string.IsNullOrEmpty(assemblyPos) || !assemblyPos.ToUpper().StartsWith(posPattern))
                                        match = false;
                                }

                                // Filter by Name
                                if (match && !string.IsNullOrEmpty(namePattern))
                                {
                                    string name = "";
                                    modelObj.GetReportProperty("NAME", ref name);
                                    if (string.IsNullOrEmpty(name) || !name.ToUpper().Contains(namePattern))
                                        match = false;
                                }

                                // Filter by Main Part status
                                if (match && onlyMainParts)
                                {
                                    if (modelObj is TSM.Part mPart)
                                    {
                                        var assy = mPart.GetAssembly();
                                        var mainPart = assy?.GetMainPart();
                                        if (mainPart != null && mainPart.Identifier.ID != mPart.Identifier.ID)
                                            match = false;
                                    }
                                    else match = false;
                                }

                                if (match)
                                {
                                    partsToSelect.Add(part);
                                    count++;
                                }
                            }
                        }
                    }
                }

                if (partsToSelect.Count > 0)
                {
                    handler.GetDrawingObjectSelector().SelectObjects(partsToSelect, true);
                    Tekla.Structures.Model.Operations.Operation.RunMacro(@"..\drawings\ADD-PART.cs");
                    return $"✅ Selected {count} parts matching filters and applied marks.";
                }

                return $"ℹ️ No parts found matching Position:'{positionFilter}' Name:'{nameFilter}' (OnlyMain:{onlyMainParts}).";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("List all bolts in the active drawing grouped by size, standard, and length. Scans inside all views.")]
        public static string ListBoltsInDrawing()
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing open.";

                var myModel = new TSM.Model();
                var views = active.GetSheet().GetViews();
                var boltGroups = new Dictionary<string, int>();
                int totalBolts = 0;

                while (views.MoveNext())
                {
                    var view = views.Current as TSD.View;
                    if (view == null) continue;

                    var objects = view.GetAllObjects(typeof(TSD.Bolt));
                    while (objects.MoveNext())
                    {
                        if (objects.Current is TSD.Bolt bolt)
                        {
                            totalBolts++;
                            string key = "Unknown";
                            try
                            {
                                var modelObj = myModel.SelectModelObject(bolt.ModelIdentifier);
                                if (modelObj is TSM.BoltGroup bg)
                                {
                                    string name = "";
                                    bg.GetReportProperty("NAME", ref name);
                                    if (string.IsNullOrEmpty(name)) name = $"M{bg.BoltSize} | {bg.BoltStandard}";
                                    key = name;
                                }
                            }
                            catch { /* skip unresolvable bolts */ }

                            if (boltGroups.ContainsKey(key)) boltGroups[key]++;
                            else boltGroups[key] = 1;
                        }
                    }
                }

                if (totalBolts == 0) return "ℹ️ No bolts found in any view of this drawing.";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"🔩 BOLT SUMMARY — {totalBolts} total bolt objects");
                sb.AppendLine(new string('-', 50));
                foreach (var kvp in boltGroups)
                    sb.AppendLine($"  {kvp.Value,4}x  {kvp.Key}");
                return sb.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Add marks to bolts in the active drawing. Optional: minDiameter (e.g. 12.0), attributeFile (e.g. 'DKM-BOLT')")]
        public static string AddBoltMarks(double minDiameter = 0.0, string attributeFile = "", string nameFilter = "")
        {
            try
            {
                var myModel = new TSM.Model();
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing open.";

                // Parse name filters (comma-separated)
                var nameFilters = new List<string>();
                if (!string.IsNullOrEmpty(nameFilter))
                    foreach (var n in nameFilter.Split(','))
                        if (!string.IsNullOrWhiteSpace(n)) nameFilters.Add(n.Trim());

                var views = active.GetSheet().GetViews();
                int count = 0;
                int skipped = 0;
                ArrayList boltsToSelect = new ArrayList();

                while (views.MoveNext())
                {
                    var view = views.Current as TSD.View;
                    if (view == null) continue;
                    var bolts = view.GetAllObjects(typeof(TSD.Bolt));
                    while (bolts.MoveNext())
                    {
                        if (bolts.Current is TSD.Bolt bolt)
                        {
                            var modelObj = myModel.SelectModelObject(bolt.ModelIdentifier);
                            if (modelObj is TSM.BoltGroup bg)
                            {
                                // Filter by diameter
                                if (minDiameter > 0 && bg.BoltSize < minDiameter) { skipped++; continue; }

                                // Filter by name
                                if (nameFilters.Count > 0)
                                {
                                    string boltName = "";
                                    bg.GetReportProperty("NAME", ref boltName);
                                    bool match = false;
                                    foreach (var nf in nameFilters)
                                        if (boltName.IndexOf(nf, StringComparison.OrdinalIgnoreCase) >= 0) { match = true; break; }
                                    if (!match) { skipped++; continue; }
                                }
                            }
                            boltsToSelect.Add(bolt);
                            count++;
                        }
                    }
                }

                if (boltsToSelect.Count > 0)
                {
                    // Step 1: Select the filtered bolts in the drawing
                    handler.GetDrawingObjectSelector().SelectObjects(boltsToSelect, true);

                    // Step 2: Use Operation.RunMacro with the correct relative path to trigger the drawing macro
                    Tekla.Structures.Model.Operations.Operation.RunMacro(@"..\drawings\ADD-PART.cs");
                }

                return $"✅ Selected {count} bolts (skipped {skipped}) and applied marks via RunMacro.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Add weld marks to all welds in the active drawing")]
        public static string AddWeldMarks()
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing open.";
                var welds = active.GetSheet().GetAllObjects(typeof(TSD.Weld));
                int count = 0;
                while (welds.MoveNext())
                    if (welds.Current is TSD.Weld weld)
                        if (new Mark(weld).Insert()) count++;
                active.CommitChanges();
                return $"✅ Added {count} weld marks.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Add overall horizontal and vertical dimensions based on bounding box")]
        public static string AddOverallDimensions()
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing.";
                var myModel = new TSM.Model();
                var allParts = active.GetSheet().GetAllObjects(typeof(TSD.Part));
                int dimCount = 0;
                while (allParts.MoveNext())
                {
                    if (allParts.Current is TSD.Part drawingPart)
                    {
                        ViewBase viewBase = drawingPart.GetView();
                        View view = viewBase as View;
                        if (view == null) continue;
                        Identifier modelId = drawingPart.ModelIdentifier;
                        TSM.ModelObject modelObj = myModel.SelectModelObject(modelId);
                        TSM.Part modelPart = modelObj as TSM.Part;
                        if (modelPart == null) continue;
                        TransformationPlane savePlane = myModel.GetWorkPlaneHandler().GetCurrentTransformationPlane();
                        myModel.GetWorkPlaneHandler().SetCurrentTransformationPlane(new TransformationPlane(view.DisplayCoordinateSystem));
                        modelPart.Select();
                        Solid solid = modelPart.GetSolid();
                        Point pMin = solid.MinimumPoint; Point pMax = solid.MaximumPoint;
                        PointList hPoints = new PointList();
                        hPoints.Add(new Point(pMin.X, pMin.Y, 0)); hPoints.Add(new Point(pMax.X, pMin.Y, 0));
                        PointList vPoints = new PointList();
                        vPoints.Add(new Point(pMin.X, pMin.Y, 0)); vPoints.Add(new Point(pMin.X, pMax.Y, 0));
                        var dimHandler = new StraightDimensionSetHandler();
                        if (dimHandler.CreateDimensionSet(viewBase, hPoints, new Vector(0, -1, 0), 100) != null) dimCount++;
                        if (dimHandler.CreateDimensionSet(viewBase, vPoints, new Vector(-1, 0, 0), 100) != null) dimCount++;
                        myModel.GetWorkPlaneHandler().SetCurrentTransformationPlane(savePlane);
                    }
                }
                active.CommitChanges();
                return $"✅ Added {dimCount} dimension sets.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Place control diagonal dimensions across assembly extents")]
        public static string PlaceControlDiagonals()
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing.";
                var myModel = new TSM.Model();
                var allParts = active.GetSheet().GetAllObjects(typeof(TSD.Part));
                int count = 0;
                while (allParts.MoveNext())
                {
                    if (allParts.Current is TSD.Part drawingPart)
                    {
                        ViewBase viewBase = drawingPart.GetView();
                        View view = viewBase as View;
                        if (view == null) continue;
                        TSM.Part modelPart = myModel.SelectModelObject(drawingPart.ModelIdentifier) as TSM.Part;
                        if (modelPart == null) continue;
                        TransformationPlane savePlane = myModel.GetWorkPlaneHandler().GetCurrentTransformationPlane();
                        myModel.GetWorkPlaneHandler().SetCurrentTransformationPlane(new TransformationPlane(view.DisplayCoordinateSystem));
                        modelPart.Select();
                        Solid solid = modelPart.GetSolid();
                        PointList diagPoints = new PointList();
                        diagPoints.Add(new Point(solid.MinimumPoint.X, solid.MinimumPoint.Y, 0));
                        diagPoints.Add(new Point(solid.MaximumPoint.X, solid.MaximumPoint.Y, 0));
                        if (new StraightDimensionSetHandler().CreateDimensionSet(viewBase, diagPoints, new Vector(1, 1, 0), 50) != null) count++;
                        myModel.GetWorkPlaneHandler().SetCurrentTransformationPlane(savePlane);
                    }
                }
                active.CommitChanges();
                return $"✅ Placed {count} diagonal dimensions.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Arrange marks to resolve collisions in active drawing")]
        public static string ArrangeMarksNoCollisions()
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing open.";

                // Trigger the Akit command via our new drawing macro strategy
                bool success = Tekla.Structures.Model.Operations.Operation.RunMacro(@"..\drawings\ArrangeMarks.cs");
                
                if (success)
                {
                    active.CommitChanges();
                    return "✅ Successfully arranged marks to resolve collisions.";
                }
                
                return "⚠️ Failed to execute the arrange marks macro.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Auto-scale and arrange all views on the sheet")]
        public static string FitViewsToSheet()
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing.";
                if (active.PlaceViews()) { active.CommitChanges(); return "✅ Views fitted to sheet."; }
                return "⚠️ PlaceViews returned false.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Modifies the scale of all views in the active drawing")]
        public static string SetDrawingScale(double scale)
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing.";
                var views = active.GetSheet().GetAllObjects(typeof(TSD.View));
                int count = 0;
                while (views.MoveNext())
                    if (views.Current is TSD.View view) { view.Attributes.Scale = scale; if (view.Modify()) count++; }
                active.CommitChanges();
                return $"✅ Modified scale to 1:{scale} for {count} views.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Add a text note to the active drawing at specified coordinates")]
        public static string AddTextToDrawing(string text, double x = 100, double y = 100)
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing.";
                var views = active.GetSheet().GetAllObjects(typeof(TSD.View));
                ViewBase targetView = active.GetSheet();
                if (views.MoveNext()) targetView = views.Current as ViewBase;
                var textNote = new TSD.Text(targetView, new Point(x, y, 0), text);
                if (textNote.Insert()) { active.CommitChanges(); return $"✅ Text note added at ({x}, {y})."; }
                return "❌ Failed to insert text note.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        //[McpServerTool, Description("Insert a reference DWG object onto the active drawing sheet")]
        public static string InsertDwgObject(string filePath, double x = 0, double y = 0)
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing.";
                var views = active.GetSheet().GetAllObjects(typeof(TSD.View));
                ViewBase targetView = active.GetSheet();
                if (views.MoveNext()) targetView = views.Current as ViewBase;
                var dwg = new DwgObject(targetView, new Point(x, y, 0), filePath);
                if (dwg.Insert()) { active.CommitChanges(); return $"✅ Inserted DWG reference at ({x}, {y})."; }
                return "❌ Failed to insert DWG reference.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Update the label of a specific view in the active drawing (pass view mark/name)")]
        public static string ModifyViewLabel(string viewName, string newLabelText)
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing.";
                var views = active.GetSheet().GetAllObjects(typeof(TSD.View));
                while (views.MoveNext())
                    if (views.Current is TSD.View view && view.Name != null && view.Name.Contains(viewName))
                    { view.Name = newLabelText; view.Modify(); active.CommitChanges(); return $"✅ Updated label to '{newLabelText}'."; }
                return $"❌ View containing '{viewName}' not found.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Physically move a specific view to a new X/Y coordinate origin on the sheet")]
        public static string MoveView(string viewName, double x, double y)
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing.";
                var views = active.GetSheet().GetAllObjects(typeof(TSD.View));
                while (views.MoveNext())
                    if (views.Current is TSD.View view && view.Name != null && view.Name.Contains(viewName))
                    { view.Origin = new Point(x, y, 0); view.Modify(); active.CommitChanges(); return $"✅ Moved view '{viewName}' to ({x}, {y})."; }
                return $"❌ View '{viewName}' not found.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Create a section view from a cut line defined by two points")]
        public static string AddSectionView(double x1, double y1, double x2, double y2, double depthUp = 500, double depthDown = 500)
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing.";
                var views = active.GetSheet().GetAllObjects(typeof(TSD.View));
                TSD.View targetView = null;
                if (views.MoveNext()) targetView = views.Current as TSD.View;
                if (targetView == null) return "❌ No parent view found.";
                var p1 = new Point(x1, y1, 0); var p2 = new Point(x2, y2, 0);
                Vector direction = new Vector(p2.X - p1.X, p2.Y - p1.Y, 0);
                Vector perp = new Vector(-direction.Y, direction.X, 0).GetNormal();
                var p3 = new Point(p1.X + perp.X * depthUp, p1.Y + perp.Y * depthUp, 0);
                TSD.View sectionView; SectionMark sectionMark;
                if (TSD.View.CreateSectionView(targetView, p1, p2, p3, depthUp, depthDown,
                    new TSD.View.ViewAttributes(), new SectionMarkBase.SectionMarkAttributes(), out sectionView, out sectionMark))
                { active.CommitChanges(); return "✅ Section view created successfully."; }
                return "❌ Failed to create section view.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Create a circular detail view from a center point and radius")]
        public static string AddDetailView(double cx, double cy, double rx, double ry)
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing.";
                var views = active.GetSheet().GetAllObjects(typeof(TSD.View));
                TSD.View targetView = null;
                if (views.MoveNext()) targetView = views.Current as TSD.View;
                if (targetView == null) return "❌ No parent view found.";
                TSD.View detailView; DetailMark detailMark;
                if (TSD.View.CreateDetailView(targetView, new Point(cx, cy, 0), new Point(rx, ry, 0),
                    new Point(cx, cy + 100, 0), new Point(cx + 500, cy, 0),
                    new TSD.View.ViewAttributes(), new DetailMark.DetailMarkAttributes(), out detailView, out detailMark))
                { active.CommitChanges(); return "✅ Detail view created successfully."; }
                return "❌ Failed to create detail view.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        //[McpServerTool, Description("Draw a line on the active drawing between two coordinate pairs")]
        public static string AddDrawingLine(double x1, double y1, double x2, double y2)
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing.";
                var views = active.GetSheet().GetAllObjects(typeof(TSD.View));
                if (!views.MoveNext()) return "❌ No views found.";
                new TSD.Line(views.Current as ViewBase, new Point(x1, y1, 0), new Point(x2, y2, 0)).Insert();
                active.CommitChanges();
                return $"✅ Line drawn from ({x1},{y1}) to ({x2},{y2}).";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        //[McpServerTool, Description("Draw a circle on the active drawing at a center point with a radius")]
        public static string AddDrawingCircle(double centerX, double centerY, double radius)
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing.";
                var views = active.GetSheet().GetAllObjects(typeof(TSD.View));
                if (!views.MoveNext()) return "❌ No views found.";
                new Circle(views.Current as ViewBase, new Point(centerX, centerY, 0), radius).Insert();
                active.CommitChanges();
                return $"✅ Circle drawn at ({centerX},{centerY}) with radius {radius}.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        //[McpServerTool, Description("Draw a rectangle on the active drawing from two corner points")]
        public static string AddDrawingRectangle(double x1, double y1, double x2, double y2)
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing.";
                var views = active.GetSheet().GetAllObjects(typeof(TSD.View));
                if (!views.MoveNext()) return "❌ No views found.";
                new Rectangle(views.Current as ViewBase, new Point(x1, y1, 0), new Point(x2, y2, 0)).Insert();
                active.CommitChanges();
                return $"✅ Rectangle drawn from ({x1},{y1}) to ({x2},{y2}).";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        //[McpServerTool, Description("Add a revision cloud on the active drawing using corner coordinates (x1,y1 to x2,y2)")]
        public static string AddRevisionCloud(double x1, double y1, double x2, double y2)
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing.";
                var views = active.GetSheet().GetAllObjects(typeof(TSD.View));
                if (!views.MoveNext()) return "❌ No views found.";
                var points = new PointList();
                points.Add(new Point(x1, y1, 0)); points.Add(new Point(x2, y1, 0));
                points.Add(new Point(x2, y2, 0)); points.Add(new Point(x1, y2, 0));
                new Cloud(views.Current as ViewBase, points).Insert();
                active.CommitChanges();
                return $"✅ Revision cloud added ({x1},{y1}) to ({x2},{y2}).";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }
    }
}
