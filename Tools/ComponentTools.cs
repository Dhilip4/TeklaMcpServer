using System;
using System.Collections.Generic;
using System.ComponentModel;
using ModelContextProtocol.Server;
using TSM = Tekla.Structures.Model;

namespace TeklaMcpServer.Tools
{
    [McpServerToolType]
    public static class ComponentTools
    {
        // =====================================================================
        //  PHASE 3: COMPONENT ATTRIBUTE TOOLS
        // =====================================================================

        [McpServerTool, Description(
            "Read all attributes from a placed component (connection/detail) by its GUID. " +
            "Returns the component name, number, and all available attributes. " +
            "Also lists child objects (parts, bolts, welds) created by the component.")]
        public static string GetComponentAttributes(string guid)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var obj = model.SelectModelObject(model.GetIdentifierByGUID(guid));
                if (obj == null) return "❌ Object not found from GUID.";

                var comp = obj as TSM.BaseComponent;
                if (comp == null) return $"❌ Object is not a component (type: {obj.GetType().Name}).";
                comp.Select();

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"🔧 Component: {comp.Name} (#{comp.Number})");
                sb.AppendLine($"  GUID: {model.GetGUIDByIdentifier(comp.Identifier)}");

                // Try to read common attributes
                sb.AppendLine($"\n  Attributes:");
                var attrNames = new[] { "tpl1", "tpl2", "tpl3", "tpl4", "tpl5",
                    "diameter", "nb", "nv", "nh", "d_hole", "e1", "e2",
                    "p1", "p2", "dp", "hp", "wp", "gap", "eccentricity",
                    "angle", "cut_length", "weld_size", "stiffener_thickness" };

                foreach (var attrName in attrNames)
                {
                    // Try double
                    double dval = double.MinValue;
                    comp.GetAttribute(attrName, ref dval);
                    if (dval > -2000000000 && dval != 0)
                    {
                        sb.AppendLine($"    {attrName} = {dval}");
                        continue;
                    }

                    // Try int
                    int ival = int.MinValue;
                    comp.GetAttribute(attrName, ref ival);
                    if (ival > -2000000000 && ival != 0)
                    {
                        sb.AppendLine($"    {attrName} = {ival}");
                        continue;
                    }

                    // Try string
                    string sval = "";
                    comp.GetAttribute(attrName, ref sval);
                    if (!string.IsNullOrEmpty(sval))
                    {
                        sb.AppendLine($"    {attrName} = \"{sval}\"");
                    }
                }

                // List children
                var children = comp.GetChildren();
                sb.AppendLine($"\n  Child Objects:");
                int childCount = 0;
                while (children.MoveNext())
                {
                    var child = children.Current as TSM.ModelObject;
                    if (child == null) continue;
                    child.Select();
                    childCount++;

                    var part = child as TSM.Part;
                    var bolt = child as TSM.BoltGroup;

                    if (part != null)
                        sb.AppendLine($"    [{childCount}] {part.GetType().Name}: {part.Profile.ProfileString} ({part.Material.MaterialString})");
                    else if (bolt != null)
                        sb.AppendLine($"    [{childCount}] BoltGroup: M{bolt.BoltSize} ({bolt.BoltStandard}), {bolt.BoltPositions.Count} bolts");
                    else
                        sb.AppendLine($"    [{childCount}] {child.GetType().Name}");

                    if (childCount >= 30) { sb.AppendLine($"    ... and more"); break; }
                }
                if (childCount == 0) sb.AppendLine("    None");

                return sb.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Modify attributes on a placed component by its GUID. " +
            "Pass attribute name-value pairs as semicolon-separated entries: 'tpl1=20.0;nb=6;diameter=20.0'. " +
            "The tool auto-detects types: numbers with decimals → double, whole numbers → int, else string.")]
        public static string ModifyComponentAttributes(string guid, string attributes)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var obj = model.SelectModelObject(model.GetIdentifierByGUID(guid));
                if (obj == null) return "❌ Object not found from GUID.";

                var comp = obj as TSM.BaseComponent;
                if (comp == null) return $"❌ Object is not a component (type: {obj.GetType().Name}).";
                comp.Select();

                var pairs = attributes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                int setCount = 0;

                foreach (var pair in pairs)
                {
                    var eqIdx = pair.IndexOf('=');
                    if (eqIdx <= 0) continue;

                    var name = pair.Substring(0, eqIdx).Trim();
                    var value = pair.Substring(eqIdx + 1).Trim();

                    // Auto-detect type
                    if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double dval))
                    {
                        if (value.Contains("."))
                            comp.SetAttribute(name, dval);
                        else
                            comp.SetAttribute(name, (int)dval);
                    }
                    else
                    {
                        comp.SetAttribute(name, value);
                    }
                    setCount++;
                }

                if (!comp.Modify())
                    return "❌ Component.Modify() failed. Some attributes may not have been applied.";

                model.CommitChanges();
                return $"✅ Modified {setCount} attributes on component '{comp.Name}'.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Select and list all components (connections/details) in the model. " +
            "Optionally filter by component name. Returns count and summary.")]
        public static string SelectAllComponents(string componentName = "")
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var selector = model.GetModelObjectSelector();
                var types = new System.Type[] {
                    typeof(TSM.Connection),
                    typeof(TSM.Detail),
                    typeof(TSM.Component)
                };
                var enumerator = selector.GetAllObjectsWithType(types);

                var summary = new Dictionary<string, int>();
                int total = 0;

                while (enumerator.MoveNext())
                {
                    var comp = enumerator.Current as TSM.BaseComponent;
                    if (comp == null) continue;
                    comp.Select();

                    if (!string.IsNullOrEmpty(componentName) &&
                        !comp.Name.Equals(componentName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    total++;
                    var key = $"{comp.GetType().Name}: {comp.Name} (#{comp.Number})";
                    summary[key] = summary.ContainsKey(key) ? summary[key] + 1 : 1;
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"🔧 Components in Model ({total} total):");
                foreach (var kvp in summary)
                    sb.AppendLine($"  {kvp.Key} × {kvp.Value}");

                if (total == 0) sb.AppendLine("  No components found.");
                return sb.ToString();
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Remove all components (connections/details) attached to a part by its GUID. " +
            "Optionally filter by component name.")]
        public static string RemoveComponents(string guid, string componentName = "")
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                var part = model.SelectModelObject(model.GetIdentifierByGUID(guid));
                if (part == null) return "❌ Part not found from GUID.";

                var children = part.GetChildren();
                int removed = 0, matched = 0;

                while (children.MoveNext())
                {
                    var comp = children.Current as TSM.BaseComponent;
                    if (comp == null) continue;
                    comp.Select();

                    if (!string.IsNullOrEmpty(componentName) &&
                        !comp.Name.Equals(componentName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    matched++;
                    if (comp.Delete()) removed++;
                }

                model.CommitChanges();
                return $"✅ Removed {removed}/{matched} components from part.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }
    }
}
