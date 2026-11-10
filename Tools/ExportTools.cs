using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using ModelContextProtocol.Server;
using TSM = Tekla.Structures.Model;
using Tekla.Structures.Drawing;

namespace TeklaMcpServer.Tools
{
    [McpServerToolType]
    public static class ExportTools
    {
        // =====================================================================
        //  EXISTING TOOLS (preserved)
        // =====================================================================

        [McpServerTool, Description("Force Tekla to run a .xsr Report template and output the data on selected parts")]
        public static string GenerateReport(string reportTemplateName, string outputFileName)
        {
            try
            {
                if (TSM.Operations.Operation.CreateReportFromSelected(reportTemplateName, outputFileName, "", "", ""))
                    return $"✅ Report '{reportTemplateName}' generated to '{outputFileName}'.";
                return "❌ Report generation failed.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Display a generated report file in Tekla's built-in viewer")]
        public static string DisplayReport(string reportFilePath)
        {
            try
            {
                TSM.Operations.Operation.DisplayReport(reportFilePath);
                return $"✅ Report displayed: {reportFilePath}";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Export selected profiles to NC (DSTV) files for CNC machinery")]
        public static string ExportNC()
        {
            try
            {
                if (TSM.Operations.Operation.CreateNCFilesFromSelected("DSTV for profiles", ""))
                    return "✅ NC Files generated successfully.";
                return "❌ NC File generation failed.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description("Export selected parts to MIS (DSTV/KISS/EJE) file format")]
        public static string CreateMISExport(string exportType, string outputPath)
        {
            try
            {
                var typeEnum = TSM.Operations.Operation.MISExportTypeEnum.DSTV;
                switch (exportType.ToUpper())
                {
                    case "KISS": typeEnum = TSM.Operations.Operation.MISExportTypeEnum.KISS; break;
                    case "EJE": typeEnum = TSM.Operations.Operation.MISExportTypeEnum.EJE; break;
                    case "EPC": typeEnum = TSM.Operations.Operation.MISExportTypeEnum.EPC; break;
                }
                if (TSM.Operations.Operation.CreateMISFileFromSelected(typeEnum, outputPath))
                    return $"✅ MIS export ({exportType}) saved to '{outputPath}'.";
                return "❌ MIS export failed.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // =====================================================================
        //  PHASE 8: EXPORT & REPORT EXPANSION
        // =====================================================================

        [McpServerTool, Description(
            "Export the model to IFC format. " +
            "Exports all objects or only selected objects. " +
            "Output file path must end with .ifc. Uses default IFC export settings.")]
        public static string ExportIFC(
            string outputPath = "",
            bool selectedOnly = false)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                // Default output to model folder
                if (string.IsNullOrEmpty(outputPath))
                {
                    var modelPath = model.GetInfo().ModelPath;
                    var modelName = model.GetInfo().ModelName;
                    outputPath = Path.Combine(modelPath, modelName + ".ifc");
                }

                // Build IFC flags using reflection for cross-version compat
                var flags = new TSM.Operations.Operation.IFCExportFlags();
                SetIfcFlag(flags, "IncludeAssemblyHierarchy", true);
                SetIfcFlag(flags, "IsAssembliesEnabled", true);
                SetIfcFlag(flags, "IsBoltsEnabled", true);
                SetIfcFlag(flags, "IsWeldsEnabled", true);
                SetIfcFlag(flags, "IsGridsEnabled", true);

                bool result;
                if (selectedOnly)
                    result = TryInvokeIfcExport("CreateIFC4ExportFromSelected", outputPath, flags);
                else
                    result = TryInvokeIfcExport("CreateIFC4ExportFromAll", outputPath, flags);

                if (!result)
                    return "❌ IFC export failed. Check output path and IFC export settings in Tekla.";

                return $"✅ IFC exported to: {outputPath} (selectedOnly={selectedOnly})";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Generate a report from ALL objects using a Tekla report template. " +
            "Template name is a .rpt file (e.g. 'Assembly_list', 'Material_list', 'Bolt_list'). " +
            "Output file is written asynchronously.")]
        public static string GenerateReportFromAll(
            string reportTemplateName, string outputFileName,
            string title1 = "", string title2 = "", string title3 = "")
        {
            try
            {
                if (TSM.Operations.Operation.CreateReportFromAll(
                    reportTemplateName, outputFileName,
                    title1 ?? "", title2 ?? "", title3 ?? ""))
                    return $"✅ Report '{reportTemplateName}' generated from all objects to '{outputFileName}'.";
                return "❌ Report generation failed. Check template name.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }



        [McpServerTool, Description(
            "Export NC (DSTV) files with full control over template and output directory. " +
            "Pass templateName (e.g. 'DSTV for profiles') and outputDirectory. " +
            "If outputDirectory is empty, defaults to <modelPath>/DSTV.")]
        public static string ExportNCFiles(
            string templateName = "DSTV for profiles",
            string outputDirectory = "",
            bool selectedOnly = true)
        {
            try
            {
                var model = new TSM.Model();
                if (!model.GetConnectionStatus()) return "❌ No active model connection.";

                if (string.IsNullOrEmpty(outputDirectory))
                    outputDirectory = Path.Combine(model.GetInfo().ModelPath, "DSTV");

                Directory.CreateDirectory(outputDirectory);

                bool result;
                if (selectedOnly)
                    result = TSM.Operations.Operation.CreateNCFilesFromSelected(templateName, outputDirectory);
                else
                    result = TSM.Operations.Operation.CreateNCFilesFromAll(templateName, outputDirectory);

                if (!result)
                    return $"❌ NC export failed. Check template '{templateName}' and output path '{outputDirectory}'.";

                return $"✅ NC files exported to: {outputDirectory} (template={templateName}, selectedOnly={selectedOnly})";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        [McpServerTool, Description(
            "Batch export multiple drawings to PDF. " +
            "Pass semicolon-separated drawing marks. Each drawing is opened, printed, and closed.")]
        public static string BatchExportPDF(string marks)
        {
            try
            {
                var handler = new DrawingHandler();
                if (!handler.GetConnectionStatus())
                    return "❌ Cannot connect to Drawing handler.";

                var markList = marks.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                // Build drawing lookup
                var drawingLookup = new Dictionary<string, Tekla.Structures.Drawing.Drawing>(StringComparer.OrdinalIgnoreCase);
                var drawings = handler.GetDrawings();
                while (drawings.MoveNext())
                    if (drawings.Current is Tekla.Structures.Drawing.Drawing d && !string.IsNullOrEmpty(d.Mark))
                        drawingLookup[d.Mark] = d;

                int exported = 0, notFound = 0, failed = 0;

                foreach (var m in markList)
                {
                    var key = m.Trim();
                    if (!drawingLookup.ContainsKey(key))
                    { notFound++; continue; }

                    try
                    {
                        var target = drawingLookup[key];

                        // Close any active drawing first
                        if (handler.GetActiveDrawing() != null)
                            handler.CloseActiveDrawing(true);

                        handler.SetActiveDrawing(target, true);
                        System.Threading.Thread.Sleep(1000);

                        // Use macro-based PDF export
                        bool macroResult = TSM.Operations.Operation.RunMacro(@"drawings\Print_Drawing.cs");
                        if (macroResult)
                        {
                            System.Threading.Thread.Sleep(1500);
                            exported++;
                        }
                        else failed++;

                        handler.CloseActiveDrawing(true);
                    }
                    catch { failed++; }
                }

                return $"✅ Batch PDF export: {exported} exported, {notFound} not found, {failed} failed.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        // -----------------------------------------------------------------------
        // IFC Helpers (cross-version compatible)
        // -----------------------------------------------------------------------

        private static void SetIfcFlag(TSM.Operations.Operation.IFCExportFlags flags, string name, bool value)
        {
            var type = flags.GetType();
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanWrite && property.PropertyType == typeof(bool))
            {
                property.SetValue(flags, value, null);
                return;
            }

            var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(bool))
                field.SetValue(flags, value);
        }

        private static bool TryInvokeIfcExport(string methodName, string outputPath, TSM.Operations.Operation.IFCExportFlags flags)
        {
            var operationType = typeof(TSM.Operations.Operation);

            // Try to find the method with reflection for cross-version compat
            MethodInfo bestMethod = null;
            foreach (var method in operationType.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name.Contains(methodName) || method.Name.Contains("IFC4Export"))
                {
                    var pars = method.GetParameters();
                    if (pars.Length >= 2 && pars[0].ParameterType == typeof(string))
                    {
                        bestMethod = method;
                        break;
                    }
                }
            }

            if (bestMethod == null)
            {
                // Fallback: try direct call for 2022
                try
                {
                    return (bool)operationType.InvokeMember(
                        methodName,
                        BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.Public,
                        null, null,
                        new object[] { outputPath, flags });
                }
                catch { return false; }
            }

            try
            {
                var pars = bestMethod.GetParameters();
                var args = new object[pars.Length];
                args[0] = outputPath;
                // Fill remaining args with defaults
                for (int i = 1; i < pars.Length; i++)
                {
                    if (pars[i].ParameterType == typeof(TSM.Operations.Operation.IFCExportFlags))
                        args[i] = flags;
                    else if (pars[i].ParameterType == typeof(string))
                        args[i] = "";
                    else if (pars[i].ParameterType == typeof(bool))
                        args[i] = false;
                    else if (pars[i].ParameterType.IsEnum)
                        args[i] = Enum.ToObject(pars[i].ParameterType, 0);
                    else if (pars[i].HasDefaultValue)
                        args[i] = pars[i].DefaultValue;
                    else
                        args[i] = null;
                }
                return (bool)bestMethod.Invoke(null, args);
            }
            catch { return false; }
        }
    }
}
