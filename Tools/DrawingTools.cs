using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using ModelContextProtocol.Server;
using Tekla.Structures;
using Tekla.Structures.Model;
using Tekla.Structures.Drawing;
using Tekla.Structures.Drawing.UI;
using Tekla.Structures.Geometry3d;
using TSM = Tekla.Structures.Model;
using TSD = Tekla.Structures.Drawing;
using TeklaMcpServer.Utils;

namespace TeklaMcpServer.Tools
{
    [McpServerToolType]
    public static class DrawingTools
    {
        [McpServerTool, Description("List all drawings in the model (GA, Assembly, Single-Part) with their marks and names")]
        public static string ListDrawings()
        {
            try
            {
                var handler = new DrawingHandler();
                var drawings = handler.GetDrawings();
                var ga = new List<string>(); var assembly = new List<string>(); var singlePart = new List<string>();
                while (drawings.MoveNext())
                {
                    if (drawings.Current is TSD.Drawing d)
                    {
                        string entry = $"  Mark: {d.Mark} | Name: {d.Name}";
                        if (d is GADrawing) ga.Add(entry);
                        else if (d is AssemblyDrawing) assembly.Add(entry);
                        else if (d is SinglePartDrawing) singlePart.Add(entry);
                    }
                }
                int total = ga.Count + assembly.Count + singlePart.Count;
                if (total == 0) return "No drawings found in the model.";
                var result = new System.Text.StringBuilder();
                result.AppendLine($"📋 TOTAL DRAWINGS: {total}");
                if (ga.Count > 0) { result.AppendLine($"\n🏗️ GA DRAWINGS ({ga.Count}):"); ga.ForEach(x => result.AppendLine(x)); }
                if (assembly.Count > 0) { result.AppendLine($"\n🔩 ASSEMBLY DRAWINGS ({assembly.Count}):"); assembly.ForEach(x => result.AppendLine(x)); }
                if (singlePart.Count > 0) { result.AppendLine($"\n🔧 SINGLE-PART DRAWINGS ({singlePart.Count}):"); singlePart.ForEach(x => result.AppendLine(x)); }
                return result.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error listing drawings: {ex.Message}"; }
        }

        [McpServerTool, Description("Lists all drawings currently selected in the Tekla Drawing List")]
        public static string GetSelectedDrawings()
        {
            try
            {
                var handler = new DrawingHandler();
                var selected = handler.GetDrawingSelector().GetSelected();
                var results = new List<string>();
                while (selected.MoveNext())
                    if (selected.Current is TSD.Drawing d) results.Add($"Mark: {d.Mark} | Name: {d.Name}");
                return results.Count > 0
                    ? $"📋 SELECTED DRAWINGS ({results.Count}):\n{string.Join("\n", results)}"
                    : "No drawings selected in the Drawing List.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Open a drawing in Tekla by its mark (e.g. 'A1', 'C1', 'P-01')")]
        public static string OpenDrawing(string mark)
        {
            try
            {
                var handler = new DrawingHandler();
                var drawings = handler.GetDrawings();
                while (drawings.MoveNext())
                {
                    if (drawings.Current is TSD.Drawing d && string.Equals(d.Mark, mark, StringComparison.OrdinalIgnoreCase))
                    {
                        if (handler.SetActiveDrawing(d, true))
                            return $"✅ Opened drawing: [{d.Mark}] {d.Name}";
                    }
                }
                return $"❌ No drawing found with mark '{mark}'.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Save and close the currently active drawing")]
        public static string CloseActiveDrawing(bool save = true)
        {
            try
            {
                var handler = new DrawingHandler();
                if (handler.GetActiveDrawing() == null) return "❌ No active drawing open.";
                handler.CloseActiveDrawing(save);
                return $"✅ Active drawing closed (Save: {save}).";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Create an assembly drawing for selected model elements")]
        public static string CreateAssemblyDrawing()
        {
            try
            {
                var selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                var selected = selector.GetSelectedObjects();
                int count = 0;
                while (selected.MoveNext())
                {
                    if (selected.Current is TSM.Part part)
                    {
                        var assembly = part.GetAssembly();
                        if (assembly != null)
                        {
                            var assDrawing = new AssemblyDrawing(assembly.Identifier);
                            if (assDrawing.Insert()) count++;
                        }
                    }
                }
                new TSM.Model().CommitChanges();
                return count > 0 ? $"✅ Created {count} assembly drawings." : "❌ No assemblies found in selection.";
            }
            catch (Exception ex) { return $"⚠️ Creation error: {ex.Message}"; }
        }

        [McpServerTool, Description("Create single-part drawings for selected model elements")]
        public static string CreateSinglePartDrawing()
        {
            try
            {
                var selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                var selected = selector.GetSelectedObjects();
                int count = 0;
                while (selected.MoveNext())
                    if (selected.Current is TSM.Part part)
                        if (new SinglePartDrawing(part.Identifier).Insert()) count++;
                new TSM.Model().CommitChanges();
                return count > 0 ? $"✅ Created {count} single-part drawings." : "❌ No parts found in selection.";
            }
            catch (Exception ex) { return $"⚠️ Creation error: {ex.Message}"; }
        }

        [McpServerTool, Description("Create cast-unit drawings for selected model elements (Concrete)")]
        public static string CreateCastUnitDrawing()
        {
            try
            {
                var selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                var selected = selector.GetSelectedObjects();
                int count = 0;
                while (selected.MoveNext())
                    if (selected.Current is TSM.Part part)
                        if (new CastUnitDrawing(part.Identifier).Insert()) count++;
                new TSM.Model().CommitChanges();
                return count > 0 ? $"✅ Created {count} cast-unit drawings." : "❌ No parts found in selection.";
            }
            catch (Exception ex) { return $"⚠️ Creation error: {ex.Message}"; }
        }

        [McpServerTool, Description("Create a new General Arrangement drawing for selected parts or from a specific view name (e.g. 'FRAME CROSS SECTION AT G.L - 8')")]
        public static string CreateGADrawing(string name, string attributeFile = "standard", string viewNameFilter = "", string viewAttributeFile = "", double viewDepthUp = 650, double viewDepthDown = 650)
        {
            try
            {
                var myModel = new TSM.Model();
                var handler = new DrawingHandler();
                TransformationPlane savePlane = myModel.GetWorkPlaneHandler().GetCurrentTransformationPlane();
                myModel.GetWorkPlaneHandler().SetCurrentTransformationPlane(new TransformationPlane());

                string finalViewAttributeFile = viewAttributeFile;
                if (string.IsNullOrEmpty(finalViewAttributeFile))
                    finalViewAttributeFile = GetViewAttributeFromGADrawing(attributeFile);
                else
                {
                    string modelPath = myModel.GetInfo().ModelPath;
                    if (!File.Exists(Path.Combine(modelPath, "attributes", finalViewAttributeFile + ".vi")))
                        if (File.Exists(Path.Combine(modelPath, "attributes", " " + finalViewAttributeFile + ".vi")))
                            finalViewAttributeFile = " " + finalViewAttributeFile;
                }
                if (string.IsNullOrEmpty(finalViewAttributeFile)) finalViewAttributeFile = attributeFile;

                var gaDrawing = new GADrawing(attributeFile);
                string sanitizedName = name;
                if (sanitizedName.EndsWith("'")) sanitizedName = sanitizedName.TrimEnd('\'');
                gaDrawing.Name = sanitizedName;
                gaDrawing.Title1 = sanitizedName;

                if (handler.GetActiveDrawing() != null) handler.CloseActiveDrawing(true);

                if (gaDrawing.Insert())
                {
                    handler.SetActiveDrawing(gaDrawing, false);
                    System.Threading.Thread.Sleep(2000);

                    CoordinateSystem viewCS = null;
                    AABB restrictionBox = null;
                    ArrayList partIds = new ArrayList();

                    if (!string.IsNullOrEmpty(viewNameFilter))
                    {
                        var views = Tekla.Structures.Model.UI.ViewHandler.GetAllViews();
                        while (views.MoveNext())
                        {
                            var v = views.Current as Tekla.Structures.Model.UI.View;
                            if (v != null && v.Name != null && v.Name.IndexOf(viewNameFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                viewCS = v.ViewCoordinateSystem;
                                Matrix matrix = MatrixFactory.ToCoordinateSystem(viewCS);
                                Point localMin = matrix.Transform(v.WorkArea.MinPoint);
                                Point localMax = matrix.Transform(v.WorkArea.MaxPoint);
                                restrictionBox = new AABB(
                                    new Point(Math.Min(localMin.X, localMax.X), Math.Min(localMin.Y, localMax.Y), -Math.Abs(viewDepthDown)),
                                    new Point(Math.Max(localMin.X, localMax.X), Math.Max(localMin.Y, localMax.Y), Math.Abs(viewDepthUp)));
                                break;
                            }
                        }
                    }

                    if (viewCS == null)
                    {
                        var selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                        var selected = selector.GetSelectedObjects();
                        TSM.Part firstPart = null;
                        while (selected.MoveNext())
                        {
                            if (selected.Current is TSM.Part p) { partIds.Add(p.Identifier); if (firstPart == null) firstPart = p; }
                        }
                        if (firstPart != null) viewCS = firstPart.GetCoordinateSystem();
                    }

                    if (viewCS == null)
                    {
                        handler.CloseActiveDrawing(true);
                        myModel.GetWorkPlaneHandler().SetCurrentTransformationPlane(savePlane);
                        return "❌ Error: Could not determine coordinate system.";
                    }

                    var sheet = gaDrawing.GetSheet();
                    TSD.View view = restrictionBox != null
                        ? new TSD.View(sheet, viewCS, viewCS, restrictionBox, finalViewAttributeFile)
                        : new TSD.View(sheet, viewCS, viewCS, partIds, finalViewAttributeFile);

                    if (view == null || !view.Insert())
                    {
                        handler.CloseActiveDrawing(false);
                        myModel.GetWorkPlaneHandler().SetCurrentTransformationPlane(savePlane);
                        return "❌ Error: View insertion failed.";
                    }

                    if (!string.IsNullOrEmpty(viewNameFilter))
                    {
                        string sanitizedViewName = viewNameFilter.TrimEnd('\'');
                        view.Name = sanitizedViewName;
                        view.Modify();
                    }

                    gaDrawing.PlaceViews();
                    gaDrawing.Modify();
                    handler.SaveActiveDrawing();
                    handler.CloseActiveDrawing(true);
                    myModel.GetWorkPlaneHandler().SetCurrentTransformationPlane(savePlane);
                    return $"✅ Created GA drawing '{name}' using '{attributeFile}' settings.";
                }

                myModel.GetWorkPlaneHandler().SetCurrentTransformationPlane(savePlane);
                return "❌ Failed to insert GA drawing object.";
            }
            catch (Exception ex) { return $"⚠️ Creation error: {ex.Message}\nStack: {ex.StackTrace}"; }
        }

        [McpServerTool, Description("Delete a drawing by its mark")]
        public static string DeleteDrawing(string mark)
        {
            try
            {
                var handler = new DrawingHandler();
                var drawings = handler.GetDrawings();
                while (drawings.MoveNext())
                    if (drawings.Current is TSD.Drawing d && string.Equals(d.Mark, mark, StringComparison.OrdinalIgnoreCase))
                        if ((d as DatabaseObject).Delete()) return $"✅ Deleted drawing: [{mark}]";
                return $"❌ Drawing '{mark}' not found.";
            }
            catch (Exception ex) { return $"⚠️ Deletion error: {ex.Message}"; }
        }

        [McpServerTool, Description("Force a drawing to synchronize and update with the 3D model changes")]
        public static string UpdateDrawing(string mark)
        {
            try
            {
                var handler = new DrawingHandler();
                var drawings = handler.GetDrawings();
                while (drawings.MoveNext())
                    if (drawings.Current is TSD.Drawing d && string.Equals(d.Mark, mark, StringComparison.OrdinalIgnoreCase))
                    { handler.UpdateDrawing(d); return $"✅ Update command sent for drawing '{mark}'."; }
                return $"❌ Drawing '{mark}' not found.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Get metadata and status for a specific drawing")]
        public static string GetDrawingDetails(string mark)
        {
            try
            {
                var handler = new DrawingHandler();
                var drawings = handler.GetDrawings();
                while (drawings.MoveNext())
                {
                    if (drawings.Current is TSD.Drawing d && string.Equals(d.Mark, mark, StringComparison.OrdinalIgnoreCase))
                    {
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine($"--- DRAWING DETAILS: {mark} ---");
                        sb.AppendLine($"Type: {d.GetType().Name}"); sb.AppendLine($"Name: {d.Name}");
                        sb.AppendLine($"Title1: {d.Title1}"); sb.AppendLine($"Title2: {d.Title2}");
                        sb.AppendLine($"Title3: {d.Title3}"); sb.AppendLine($"Creation Date: {d.CreationDate}");
                        return sb.ToString();
                    }
                }
                return $"❌ Drawing '{mark}' not found.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("List all views in the active drawing")]
        public static string GetDrawingViews()
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing open.";
                var views = active.GetSheet().GetAllObjects(typeof(TSD.View));
                var results = new List<string>();
                while (views.MoveNext())
                    if (views.Current is TSD.View v)
                        results.Add($"- Name: {v.Name} | Type: {v.GetType().Name} | Origin: {v.Origin}");
                return results.Count > 0
                    ? $"📋 DRAWING VIEWS ({results.Count}):\n{string.Join("\n", results)}"
                    : "No views found in drawing.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Get counts of different object types in the active drawing")]
        public static string GetDrawingObjectCount()
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing open.";
                var allObjects = active.GetSheet().GetObjects();
                int parts = 0, marks = 0, dims = 0, texts = 0, welds = 0, bolts = 0, views = 0;
                while (allObjects.MoveNext())
                {
                    var c = allObjects.Current;
                    if (c is TSD.Part) parts++; else if (c is Mark) marks++; else if (c is StraightDimensionSet) dims++;
                    else if (c is TSD.Text) texts++; else if (c is TSD.Weld) welds++; else if (c is TSD.Bolt) bolts++;
                    else if (c is TSD.View) views++;
                }
                return $"📊 DRAWING OBJECT COUNTS:\n- Views: {views}\n- Parts: {parts}\n- Marks: {marks}\n- Dimensions: {dims}\n- Texts: {texts}\n- Welds: {welds}\n- Bolts: {bolts}";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Export a drawing to PDF by its mark. PDF is saved to the model folder.")]
        public static string ExportToPdf(string mark)
        {
            try
            {
                var handler = new DrawingHandler();
                var drawings = handler.GetDrawings();
                TSD.Drawing target = null;
                while (drawings.MoveNext())
                    if (drawings.Current is TSD.Drawing d && string.Equals(d.Mark, mark, StringComparison.OrdinalIgnoreCase))
                    { target = d; break; }
                if (target == null) return $"❌ Drawing '{mark}' not found.";

                // Use Tekla macro-based PDF export (avoids internal API dependency)
                handler.SetActiveDrawing(target, true);
                System.Threading.Thread.Sleep(1000);
                string macroPath = @"drawings\Print_Drawing.cs";
                bool macroResult = TSM.Operations.Operation.RunMacro(macroPath);
                handler.CloseActiveDrawing(true);
                if (macroResult) return $"✅ PDF export triggered for drawing '{mark}'.";
                return "⚠️ PDF export macro not found. Use File > Print in Tekla to export manually.";
            }
            catch (Exception ex) { return $"⚠️ Export error: {ex.Message}"; }
        }

        [McpServerTool, Description("Update revision UDAs on a drawing by its mark")]
        public static string UpdateRevision(string drawingMark, string revisionMark, string date, string description)
        {
            try
            {
                var handler = new DrawingHandler();
                var drawings = handler.GetDrawings();
                while (drawings.MoveNext())
                {
                    if (drawings.Current is TSD.Drawing d && string.Equals(d.Mark, drawingMark, StringComparison.OrdinalIgnoreCase))
                    {
                        d.SetUserProperty("REVISION_MARK", revisionMark);
                        d.SetUserProperty("REVISION_DATE", date);
                        d.SetUserProperty("REVISION_DESCRIPTION", description);
                        d.Modify();
                        return $"✅ Revision updated on [{drawingMark}].";
                    }
                }
                return $"❌ Drawing '{drawingMark}' not found.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Get a comprehensive status report of all drawings (Lock, Freeze, Issue, Status)")]
        public static string GetDrawingStatusReport()
        {
            try
            {
                var handler = new DrawingHandler();
                var drawings = handler.GetDrawings();
                var results = new List<string>();
                results.Add("MARK\tTYPE\tUP-TO-DATE\tLOCKED\tFROZEN\tISSUED");
                results.Add(new string('-', 60));
                while (drawings.MoveNext())
                    if (drawings.Current is TSD.Drawing d)
                        results.Add($"{d.Mark}\t{d.GetType().Name}\t{d.UpToDateStatus}\t{d.IsLocked}\t{d.IsFrozen}\t{d.IsIssued}");
                return results.Count > 2
                    ? $"📋 DRAWING STATUS REPORT:\n{string.Join("\n", results)}"
                    : "❌ No drawings found in the model.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Set the Lock, Freeze, and Issue status of a drawing by its mark")]
        public static string SetDrawingLifecycle(string mark, bool isLocked, bool isFrozen, bool isIssued)
        {
            try
            {
                var handler = new DrawingHandler();
                var drawings = handler.GetDrawings();
                while (drawings.MoveNext())
                {
                    if (drawings.Current is TSD.Drawing d && string.Equals(d.Mark, mark, StringComparison.OrdinalIgnoreCase))
                    {
                        d.IsLocked = isLocked; d.IsFrozen = isFrozen;
                        if (isIssued) handler.IssueDrawing(d); else handler.UnissueDrawing(d);
                        if (d.Modify()) return $"✅ Lifecycle updated for '{mark}'.";
                        return $"⚠️ Failed to modify lifecycle for '{mark}'.";
                    }
                }
                return $"❌ Drawing '{mark}' not found.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Get a specific User Defined Attribute (UDA) from a drawing")]
        public static string GetDrawingUDA(string mark, string udaName)
        {
            try
            {
                var handler = new DrawingHandler();
                var drawings = handler.GetDrawings();
                while (drawings.MoveNext())
                {
                    if (drawings.Current is TSD.Drawing d && string.Equals(d.Mark, mark, StringComparison.OrdinalIgnoreCase))
                    {
                        string value = "";
                        if (d.GetUserProperty(udaName, ref value)) return $"✅ UDA '{udaName}' on '{mark}' is: {value}";
                        return $"❌ UDA '{udaName}' not found or empty on '{mark}'.";
                    }
                }
                return $"❌ Drawing '{mark}' not found.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Set a specific User Defined Attribute (UDA) on a drawing")]
        public static string SetDrawingUDA(string mark, string udaName, string value)
        {
            try
            {
                var handler = new DrawingHandler();
                var drawings = handler.GetDrawings();
                while (drawings.MoveNext())
                {
                    if (drawings.Current is TSD.Drawing d && string.Equals(d.Mark, mark, StringComparison.OrdinalIgnoreCase))
                    {
                        d.SetUserProperty(udaName, value);
                        if (d.Modify()) return $"✅ UDA '{udaName}' set to '{value}' on '{mark}'.";
                        return $"⚠️ Failed to set UDA on '{mark}'.";
                    }
                }
                return $"❌ Drawing '{mark}' not found.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Copy titles 1, 2, and 3 from a source drawing to a target drawing")]
        public static string CopyDrawingTitles(string sourceMark, string targetMark)
        {
            try
            {
                var handler = new DrawingHandler();
                var drawings = handler.GetDrawings();
                TSD.Drawing source = null, target = null;
                while (drawings.MoveNext())
                {
                    if (drawings.Current is TSD.Drawing d)
                    {
                        if (string.Equals(d.Mark, sourceMark, StringComparison.OrdinalIgnoreCase)) source = d;
                        if (string.Equals(d.Mark, targetMark, StringComparison.OrdinalIgnoreCase)) target = d;
                    }
                }
                if (source == null) return $"❌ Source drawing '{sourceMark}' not found.";
                if (target == null) return $"❌ Target drawing '{targetMark}' not found.";
                target.Title1 = source.Title1; target.Title2 = source.Title2; target.Title3 = source.Title3;
                target.Modify();
                return $"✅ Copied titles from '{sourceMark}' to '{targetMark}'.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // =====================================================================
        //  PHASE 4: DRAWING AUTOMATION EXPANSION
        // =====================================================================

        [McpServerTool, Description(
            "Get information about the currently open drawing including sheet size, " +
            "view count, drawing type, and title information.")]
        public static string GetActiveDrawing()
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "ℹ️ No drawing is currently open.";

                var sheet = active.GetSheet();
                int viewCount = 0;
                if (sheet != null)
                {
                    var views = sheet.GetViews();
                    while (views.MoveNext()) viewCount++;
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("📐 Active Drawing:");
                sb.AppendLine($"  Mark: {active.Mark}");
                sb.AppendLine($"  Name: {active.Name}");
                sb.AppendLine($"  Type: {active.GetType().Name}");
                sb.AppendLine($"  Title1: {active.Title1}");
                sb.AppendLine($"  Title2: {active.Title2}");
                sb.AppendLine($"  Title3: {active.Title3}");
                sb.AppendLine($"  Sheet Width: {sheet?.Width ?? 0}");
                sb.AppendLine($"  Sheet Height: {sheet?.Height ?? 0}");
                sb.AppendLine($"  View Count: {viewCount}");
                sb.AppendLine($"  Up-To-Date: {active.UpToDateStatus}");
                return sb.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Get all parts visible in the active drawing's views. " +
            "Returns part profile, material, name, and model GUID for each drawing part object.")]
        public static string GetViewParts()
        {
            try
            {
                var handler = new DrawingHandler();
                var active = handler.GetActiveDrawing();
                if (active == null) return "❌ No active drawing open.";

                var myModel = new TSM.Model();
                var views = active.GetSheet().GetViews();
                var sb = new System.Text.StringBuilder();
                int totalParts = 0;

                while (views.MoveNext())
                {
                    var view = views.Current as TSD.View;
                    if (view == null) continue;

                    sb.AppendLine($"\n📋 View: {view.Name}");
                    var parts = view.GetAllObjects(typeof(TSD.Part));
                    int viewPartCount = 0;

                    while (parts.MoveNext())
                    {
                        if (parts.Current is TSD.Part drawingPart)
                        {
                            var modelObj = myModel.SelectModelObject(drawingPart.ModelIdentifier) as TSM.Part;
                            if (modelObj == null) continue;
                            modelObj.Select();
                            viewPartCount++;
                            totalParts++;

                            string guid = myModel.GetGUIDByIdentifier(modelObj.Identifier);
                            sb.AppendLine($"  [{viewPartCount}] {modelObj.Name} | {modelObj.Profile.ProfileString} | {modelObj.Material.MaterialString} | GUID: {guid}");

                            if (viewPartCount >= 50)
                            {
                                sb.AppendLine($"  ... (limited to 50 per view)");
                                break;
                            }
                        }
                    }
                }

                if (totalParts == 0) return "ℹ️ No parts found in any view of the active drawing.";
                sb.Insert(0, $"📊 Total parts across all views: {totalParts}\n");
                return sb.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Create an assembly drawing for a specific part by its GUID. " +
            "The part must belong to an assembly and be numbered. " +
            "Optionally specify an attribute file name for drawing settings.")]
        public static string CreateAssemblyDrawingByGuid(string guid, string attributeFile = "standard")
        {
            try
            {
                var myModel = new TSM.Model();
                if (!myModel.GetConnectionStatus()) return "❌ No active model connection.";

                var part = myModel.SelectModelObject(myModel.GetIdentifierByGUID(guid)) as TSM.Part;
                if (part == null) return "❌ Part not found from GUID.";

                var assembly = part.GetAssembly();
                if (assembly == null) return "❌ No assembly found for this part.";

                AssemblyDrawing drawing;
                if (string.IsNullOrEmpty(attributeFile) || attributeFile == "standard")
                    drawing = new AssemblyDrawing(assembly.Identifier);
                else
                    drawing = new AssemblyDrawing(assembly.Identifier, attributeFile);

                if (!drawing.Insert())
                    return "❌ Failed to create assembly drawing. Ensure part is numbered.";

                return $"✅ Assembly drawing created: Mark={drawing.Mark}, Name={drawing.Name}";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Create assembly drawings for multiple parts in batch. " +
            "Pass semicolon-separated GUIDs. Skips parts that already have drawings.")]
        public static string CreateBatchAssemblyDrawings(string guids, string attributeFile = "standard")
        {
            try
            {
                var myModel = new TSM.Model();
                if (!myModel.GetConnectionStatus()) return "❌ No active model connection.";

                var guidList = guids.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                int created = 0, skipped = 0, failed = 0;
                var processedAssemblies = new HashSet<int>();

                foreach (var g in guidList)
                {
                    var part = myModel.SelectModelObject(myModel.GetIdentifierByGUID(g.Trim())) as TSM.Part;
                    if (part == null) { failed++; continue; }

                    var assembly = part.GetAssembly();
                    if (assembly == null) { failed++; continue; }

                    // Skip if we already processed this assembly
                    if (processedAssemblies.Contains(assembly.Identifier.ID))
                    { skipped++; continue; }
                    processedAssemblies.Add(assembly.Identifier.ID);

                    AssemblyDrawing drawing;
                    if (string.IsNullOrEmpty(attributeFile) || attributeFile == "standard")
                        drawing = new AssemblyDrawing(assembly.Identifier);
                    else
                        drawing = new AssemblyDrawing(assembly.Identifier, attributeFile);

                    if (drawing.Insert()) created++;
                    else failed++;
                }

                return $"✅ Batch assembly drawings: {created} created, {skipped} skipped (duplicate assemblies), {failed} failed.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Set a User Defined Attribute (string) on a drawing. " +
            "If the drawing is currently open, it also commits and saves changes.")]
        public static string SetDrawingUserProperty(string mark, string propertyName, string value)
        {
            try
            {
                var handler = new DrawingHandler();
                var drawings = handler.GetDrawings();
                while (drawings.MoveNext())
                {
                    if (drawings.Current is TSD.Drawing d && string.Equals(d.Mark, mark, StringComparison.OrdinalIgnoreCase))
                    {
                        d.SetUserProperty(propertyName, value);
                        if (d.Modify())
                        {
                            // If this drawing is active, commit + save
                            var activeDrawing = handler.GetActiveDrawing();
                            if (activeDrawing != null && activeDrawing.Mark == d.Mark)
                            {
                                activeDrawing.CommitChanges();
                                handler.SaveActiveDrawing();
                            }
                            return $"✅ Set '{propertyName}' = '{value}' on drawing '{mark}'.";
                        }
                        return $"⚠️ Drawing found but Modify() failed for '{mark}'.";
                    }
                }
                return $"❌ Drawing '{mark}' not found.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Batch update (re-sync) multiple drawings with the 3D model. " +
            "Pass semicolon-separated drawing marks. Each drawing gets a model update command.")]
        public static string BatchUpdateDrawings(string marks)
        {
            try
            {
                var handler = new DrawingHandler();
                var markList = marks.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                // Build lookup of all drawings
                var drawingLookup = new Dictionary<string, TSD.Drawing>(StringComparer.OrdinalIgnoreCase);
                var drawings = handler.GetDrawings();
                while (drawings.MoveNext())
                    if (drawings.Current is TSD.Drawing d && !string.IsNullOrEmpty(d.Mark))
                        drawingLookup[d.Mark] = d;

                int updated = 0, notFound = 0;
                foreach (var m in markList)
                {
                    var key = m.Trim();
                    if (drawingLookup.ContainsKey(key))
                    {
                        handler.UpdateDrawing(drawingLookup[key]);
                        updated++;
                    }
                    else notFound++;
                }

                return $"✅ Batch update: {updated} drawings updated, {notFound} not found.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        /// <summary>Helper to parse a .gd file and find the referenced view attribute (.vi)</summary>
        internal static string GetViewAttributeFromGADrawing(string attributeFile)
        {
            try
            {
                if (string.IsNullOrEmpty(attributeFile) || attributeFile == "standard") return "standard";
                var model = new TSM.Model();
                string modelPath = model.GetInfo().ModelPath;
                string gdPath = Path.Combine(modelPath, "attributes", attributeFile + ".gd");
                if (File.Exists(gdPath))
                {
                    var lines = File.ReadAllLines(gdPath);
                    foreach (var line in lines)
                    {
                        if (line.TrimStart().StartsWith("dview.in_file"))
                        {
                            var parts = line.Split('"');
                            if (parts.Length >= 2)
                            {
                                string val = parts[1];
                                if (!File.Exists(Path.Combine(modelPath, "attributes", val + ".vi")))
                                    if (File.Exists(Path.Combine(modelPath, "attributes", " " + val + ".vi")))
                                        return " " + val;
                                return val;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Console.Error.WriteLine($"Error parsing GA attributes: {ex.Message}"); }
            return string.Empty;
        }
    }
}
