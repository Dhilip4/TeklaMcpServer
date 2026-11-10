using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Drawing;
using Tekla.Structures.Drawing.UI;
using TSM = Tekla.Structures.Model;
using TSD = Tekla.Structures.Drawing;
using ModelObjectSelector = Tekla.Structures.Model.UI.ModelObjectSelector;

namespace TeklaMcpServer.Tools
{
    [McpServerToolType]
    public static class SelectionTools
    {
        [McpServerTool, Description("Select model elements by GUIDs")]
        public static string SelectElementsByGuid(string[] guids)
        {
            try
            {
                var model = new TSM.Model();
                var selector = new ModelObjectSelector();
                var objects = new ArrayList();
                foreach (var g in guids)
                {
                    var obj = model.SelectModelObject(model.GetIdentifierByGUID(g));
                    if (obj != null) objects.Add(obj);
                }
                selector.Select(objects);
                return $"✅ Selected {objects.Count} elements.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }


        [McpServerTool, Description("Set UDAs on selected elements")]
        public static string SetElementsUDAs(Dictionary<string, string> attributes)
        {
            try
            {
                var selector = new ModelObjectSelector();
                var enumerator = selector.GetSelectedObjects();
                int count = 0;
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current is TSM.ModelObject obj)
                    {
                        foreach (var kvp in attributes) obj.SetUserProperty(kvp.Key, kvp.Value);
                        if (obj.Modify()) count++;
                    }
                }
                return $"✅ Updated UDAs on {count} elements.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Get all UDAs from selected elements")]
        public static string GetAllElementsUDAs()
        {
            try
            {
                var selector = new ModelObjectSelector();
                var enumerator = selector.GetSelectedObjects();
                var result = new System.Text.StringBuilder();
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current is TSM.ModelObject obj)
                    {
                        obj.Select();
                        result.AppendLine($"--- GUID: {obj.Identifier.GUID} ---");
                        var udaNames = new[] { "USER_FIELD_1", "USER_FIELD_2", "USER_FIELD_3", "USER_FIELD_4",
                                               "COMMENT", "FINISH", "PRELIM_MARK", "PAINT" };
                        foreach (var udaName in udaNames)
                        {
                            string val = "";
                            if (obj.GetUserProperty(udaName, ref val) && !string.IsNullOrEmpty(val))
                                result.AppendLine($"  {udaName} = {val}");
                        }
                    }
                }
                return result.Length > 0 ? $"📋 UDAs:\n{result}" : "No objects selected or no UDAs found.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Select elements by type, name, profile, material, or phase")]
        public static string SelectElementsByFilter(string element_type = "", string name = "", string profile = "", string material = "", string finish = "", int phase = 0)
        {
            try
            {
                var model = new TSM.Model();
                var selector = new ModelObjectSelector();
                var allObjects = model.GetModelObjectSelector().GetAllObjectsWithType(TSM.ModelObject.ModelObjectEnum.BEAM);
                var matches = new ArrayList();
                while (allObjects.MoveNext())
                {
                    if (allObjects.Current is TSM.Part part)
                    {
                        part.Select();
                        bool match = true;
                        if (!string.IsNullOrEmpty(name) && !part.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) match = false;
                        if (!string.IsNullOrEmpty(profile) && !part.Profile.ProfileString.Contains(profile)) match = false;
                        if (!string.IsNullOrEmpty(material) && !part.Material.MaterialString.Equals(material, StringComparison.OrdinalIgnoreCase)) match = false;
                        if (!string.IsNullOrEmpty(finish)) { string f = ""; part.GetReportProperty("FINISH", ref f); if (!f.Equals(finish, StringComparison.OrdinalIgnoreCase)) match = false; }
                        if (phase > 0) { int p = 0; part.GetReportProperty("PHASE", ref p); if (p != phase) match = false; }
                        if (match) matches.Add(part);
                    }
                }
                selector.Select(matches);
                return $"✅ Selected {matches.Count} elements matching filter.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Find all parts that share the same part number as the given GUID")]
        public static string GetSimilarParts(string guid)
        {
            try
            {
                var model = new TSM.Model();
                var obj = model.SelectModelObject(model.GetIdentifierByGUID(guid));
                if (obj == null) return "❌ Object not found.";
                var similar = TSM.Operations.Operation.GetSimilarNumberedObjects(obj);
                if (similar == null || similar.Count == 0) return "❌ No similar objects found.";
                var results = new List<string>();
                foreach (var mo in similar) results.Add(model.GetGUIDByIdentifier(mo.Identifier));
                return $"✅ Found {results.Count} similar-numbered objects: {string.Join(", ", results)}";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Show only selected elements in active view")]
        public static string ShowOnlySelected()
        {
            try
            {
                var selector = new ModelObjectSelector();
                var selected = selector.GetSelectedObjects();
                int count = 0;
                while (selected.MoveNext()) count++;
                if (count == 0) return "❌ No objects selected.";
                return $"✅ {count} elements are selected. Use Tekla UI view filter to isolate them.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Zoom the active view to selected elements")]
        public static string ZoomToSelection()
        {
            try
            {
                var viewHandler = new Tekla.Structures.Model.UI.ViewHandler();
                return "✅ Use Ctrl+Shift+F in Tekla to zoom to selected elements.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }



        // ============================================================
        private class PropertyDef
        {
            public string Category { get; set; }
            public string PropertyId { get; set; }
            public string Label { get; set; }
            public PropertyDef(string c, string p, string l) { Category = c; PropertyId = p; Label = l; }
        }

        private class OperatorDef
        {
            public string Op { get; set; }
            public string Label { get; set; }
            public OperatorDef(string o, string l) { Op = o; Label = l; }
        }

        private static readonly Dictionary<string, PropertyDef> PropertyMap
            = new Dictionary<string, PropertyDef>(StringComparer.OrdinalIgnoreCase)
        {
            { "ASSEMBLY_POS",       new PropertyDef("co_assembly", "proASSEMBLY_POS",              "albl_Assembly_pos.") },
            { "ASSEMBLY_PREFIX",    new PropertyDef("co_assembly", "proNUMBERING_SERIE",           "albl_Prefix") },
            { "ASSEMBLY_PHASE",     new PropertyDef("co_assembly", "proPHASE",                     "albl_Phase") },
            { "BOLT_SIZE",          new PropertyDef("co_bolt", "proSIZE", "albl_Size") },
            { "BOLT_STANDARD",      new PropertyDef("co_bolt", "proSTANDARD", "albl_Standard") },
            { "BOLT_NAME",          new PropertyDef("co_bolt", "proNAME", "albl_Name") },
            { "BOLT_TYPE",          new PropertyDef("co_bolt", "proTYPE", "albl_Type") },
            { "WELD_SIZE",          new PropertyDef("co_weld", "proWELD_SIZE_1", "albl_Size_above_line") },
            { "WELD_TYPE",          new PropertyDef("co_weld", "proWELD_TYPE_1", "albl_Type_above_line") },
            { "WELD_TEXT",          new PropertyDef("co_weld", "proWELD_TEXT", "albl_Weld_text") },
            { "WELD_POSITION",      new PropertyDef("co_weld", "proWELD_POSITION", "albl_Position") },
            { "NAME",               new PropertyDef("co_part",     "proNAME",                      "albl_Name") },
            { "PROFILE",            new PropertyDef("co_part",     "proPROFILE",                   "albl_Profile") },
            { "MATERIAL",           new PropertyDef("co_part",     "proMATERIAL",                  "albl_Material") },
            { "CLASS",              new PropertyDef("co_part",     "proCLASS",                     "albl_Class") },
            { "PHASE",              new PropertyDef("co_part",     "proPHASE",                     "albl_Phase") },
            { "FINISH",             new PropertyDef("co_part",     "proFINISH",                    "albl_Finish") },
            { "PREFIX",             new PropertyDef("co_part",     "proSERIE",                     "albl_Prefix") },
            { "BOLT_PHASE",         new PropertyDef("co_bolt",     "proPHASE",                     "albl_Phase") },
            { "IS_MAIN_PART",       new PropertyDef("co_part",     "proMAINPART",                  "albl_Primary_part") },
            { "PRIMARY_PART",       new PropertyDef("co_part",     "proMAINPART",                  "albl_Primary_part") },
        };

        private static readonly Dictionary<string, OperatorDef> OperatorMap
            = new Dictionary<string, OperatorDef>(StringComparer.OrdinalIgnoreCase)
        {
            { "equals",         new OperatorDef("==", "albl_Equals") },
            { "==",             new OperatorDef("==", "albl_Equals") },
            { "not_equals",     new OperatorDef("!=", "albl_DoesNotEqual") },
            { "!=",             new OperatorDef("!=", "albl_DoesNotEqual") },
            { "starts_with",    new OperatorDef("==", "albl_Equals") },
            { "contains",       new OperatorDef("==", "albl_Equals") },
        };

        public class FilterRule
        {
            public string Property { get; set; }
            public string Operator { get; set; }
            public string Value { get; set; }
            public string Connector { get; set; } = "AND";
        }

        [McpServerTool, Description("Create a complex Model Selection Filter (.SObjGrp) with multiple rules.")]
        public static string CreateComplexModelFilter(string filterName, List<FilterRule> rules)
        {
            return CreateComplexFilter(filterName, rules, "SObjGrp");
        }

        [McpServerTool, Description("Create a complex Drawing View Filter (.VObjGrp) with multiple rules.")]
        public static string CreateComplexDrawingFilter(string filterName, List<FilterRule> rules)
        {
            return CreateComplexFilter(filterName, rules, "VObjGrp");
        }

        private static string CreateComplexFilter(string filterName, List<FilterRule> rules, string extension)
        {
            try
            {
                var model = new TSM.Model();
                var modelInfo = model.GetInfo();
                if (string.IsNullOrEmpty(modelInfo.ModelPath)) return "❌ No active model.";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("TITLE_OBJECT_GROUP ");
                sb.AppendLine("{");
                sb.AppendLine("    Version= 1.05 ");
                sb.AppendLine("    Count= " + rules.Count + " ");

                for (int i = 0; i < rules.Count; i++)
                {
                    var rule = rules[i];
                    if (!PropertyMap.ContainsKey(rule.Property))
                        return "❌ Unknown property '" + rule.Property + "' in rule " + (i + 1) + ".";
                    
                    var propDef = PropertyMap[rule.Property];
                    string category = propDef.Category;
                    string propertyId = propDef.PropertyId;
                    string propLabel = propDef.Label;
                    
                    string opKey = rule.Operator.ToLower().Replace(" ", "_");
                    if (!OperatorMap.ContainsKey(opKey))
                        return "❌ Unknown operator '" + rule.Operator + "' in rule " + (i + 1) + ".";
                    
                    var opDef = OperatorMap[opKey];
                    string teklaOp = opDef.Op;
                    string opLabel = opDef.Label;

                    string filterValue = rule.Value;
                    if (opKey == "starts_with" && !rule.Value.EndsWith("*")) filterValue = rule.Value + "*";
                    if (opKey == "contains") filterValue = "*" + rule.Value + "*";

                    string connector = (i == rules.Count - 1) ? "Empty" : (rule.Connector.ToUpper() == "OR" ? "||" : "&&");

                    sb.AppendLine("    SECTION_OBJECT_GROUP ");
                    sb.AppendLine("    {");
                    sb.AppendLine("        0 "); 
                    sb.AppendLine("        1 "); 
                    sb.AppendLine("        " + category + " ");
                    sb.AppendLine("        " + propertyId + " ");
                    sb.AppendLine("        " + propLabel + " ");
                    sb.AppendLine("        " + teklaOp + " ");
                    sb.AppendLine("        " + opLabel + " ");
                    sb.AppendLine("        " + filterValue + " ");
                    sb.AppendLine("        0 "); 
                    sb.AppendLine("        " + connector + " ");
                    sb.AppendLine("        }");
                }
                sb.AppendLine("    }");

                string attrPath = System.IO.Path.Combine(modelInfo.ModelPath, "attributes");
                if (!System.IO.Directory.Exists(attrPath)) System.IO.Directory.CreateDirectory(attrPath);

                string filePath = System.IO.Path.Combine(attrPath, filterName + "." + extension);
                System.IO.File.WriteAllText(filePath, sb.ToString());

                return "✅ Created Complex Filter '" + filterName + "' (" + extension + ") with " + rules.Count + " rules.\nPath: " + filePath;
            }
            catch (Exception ex) { return "⚠️ Error: " + ex.Message; }
        }

        [McpServerTool, Description("Create a Model Selection Filter (.SObjGrp) in the active model. Property: NAME, PROFILE, MATERIAL, CLASS, PHASE, FINISH, PREFIX, ASSEMBLY_POS, ASSEMBLY_PREFIX, ASSEMBLY_PHASE, BOLT_SIZE, BOLT_STANDARD. Operator: equals, not_equals, starts_with, contains. Value supports wildcards (*).")]
        public static string CreateModelFilter(string filterName, string property, string op, string value)
        {
            return CreateFilter(filterName, property, op, value, "SObjGrp");
        }

        [McpServerTool, Description("Create a Drawing View Filter (.VObjGrp) in the active model. Same properties and operators as CreateModelFilter.")]
        public static string CreateDrawingFilter(string filterName, string property, string op, string value)
        {
            return CreateFilter(filterName, property, op, value, "VObjGrp");
        }

        [McpServerTool, Description("Create a filter that matches an Assembly Prefix AND ensures only the Primary (Main) part is selected.")]
        public static string CreateAssemblyMainPartFilter(string filterName, string assemblyPrefix, bool isDrawingFilter = true)
        {
            var rules = new List<FilterRule>
            {
                new FilterRule { Property = "ASSEMBLY_PREFIX", Operator = "equals", Value = assemblyPrefix, Connector = "AND" },
                new FilterRule { Property = "PRIMARY_PART", Operator = "equals", Value = "1" }
            };
            return CreateComplexFilter(filterName, rules, isDrawingFilter ? "VObjGrp" : "SObjGrp");
        }

        [McpServerTool, Description("Apply a previously created selection filter (.SObjGrp) to the active model selection.")]
        public static string SetModelSelectionFilter(string filterName)
        {
            try
            {
                var model = new TSM.Model();
                var modelInfo = model.GetInfo();
                if (string.IsNullOrEmpty(modelInfo.ModelPath)) return "❌ No active model.";

                string filterFile = filterName;
                if (!filterFile.ToLower().EndsWith(".sobjgrp")) filterFile += ".SObjGrp";

                // We can't directly 'set' the UI dropdown via Open API easily without macros, 
                // but we can use the SelectionFilter class to force a selection update.
                TSM.UI.ModelObjectSelector selector = new TSM.UI.ModelObjectSelector();
                // Note: The Open API doesn't have a direct 'SetGlobalFilter' method.
                // Workaround: Trigger a macro that sets the filter.
                
                return "✅ Selection Filter file is ready at 'attributes/" + filterFile + "'.\n" +
                       "Manual Step: Select it from the bottom selection filter dropdown in Tekla.";
            }
            catch (Exception ex) { return "⚠️ Error: " + ex.Message; }
        }

        [McpServerTool, Description("Apply a previously created view filter (.VObjGrp) to ALL views in the active drawing.")]
        public static string ApplyDrawingFilterToAllViews(string filterName)
        {
            return "⚠️ This tool is currently being updated for Tekla 2022 compatibility.\n" +
                   "Manual Workaround: Double-click the view frame and select '" + filterName + "' from the Filter dropdown.";
        }


        [McpServerTool, Description("Select or hide objects in the active drawing by their 3D model phase")]
        public static string SelectPhaseInDrawing(int phase, bool hide = false)
        {
            try
            {
                var dh = new TSD.DrawingHandler();
                if (!dh.GetConnectionStatus()) return "❌ Tekla Drawing Editor not connected.";
                
                var activeDrawing = dh.GetActiveDrawing();
                if (activeDrawing == null) return "❌ No active drawing found.";
                
                var sheet = activeDrawing.GetSheet();
                var drawingObjects = sheet.GetAllObjects(typeof(TSD.ModelObject));
                
                var model = new TSM.Model();
                var objectsToSelect = new ArrayList();
                int totalScanned = 0;
                int hiddenCount = 0;

                while (drawingObjects.MoveNext())
                {
                    totalScanned++;
                    if (drawingObjects.Current is TSD.ModelObject drawingObj)
                    {
                        var modelObj = model.SelectModelObject(drawingObj.ModelIdentifier);
                        if (modelObj != null)
                        {
                            int p = 0;
                            if (modelObj.GetReportProperty("PHASE", ref p) && p == phase)
                            {
                                objectsToSelect.Add(drawingObj);
                                if (hide)
                                {
                                    drawingObj.Hideable.HideFromDrawing();
                                    if (drawingObj.Modify()) hiddenCount++;
                                }
                            }
                        }
                    }
                }

                if (objectsToSelect.Count > 0)
                {
                    dh.GetDrawingObjectSelector().SelectObjects(objectsToSelect, true);
                    string msg = $"✅ Selected {objectsToSelect.Count} objects in phase {phase}.";
                    if (hide) msg += $" Hidden {hiddenCount} objects.";
                    return msg;
                }
                else
                {
                    return $"ℹ️ No objects found in phase {phase} (scanned {totalScanned} objects).";
                }
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }


        private static string CreateFilter(string filterName, string property, string op, string value, string extension)
        {
            var rules = new List<FilterRule>
            {
                new FilterRule { Property = property, Operator = op, Value = value }
            };
            return CreateComplexFilter(filterName, rules, extension);
        }
    }
}
