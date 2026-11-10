using System;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Newtonsoft.Json.Linq;

namespace TeklaMcpServer.Tools
{
    /// <summary>
    /// MCP tools for semantic search over Tekla attributes and API members.
    /// Delegates to SemanticSearchBridge which runs a Python subprocess.
    /// </summary>
    [McpServerToolType]
    public static class SemanticSearchTools
    {
        private static readonly Lazy<Utils.SemanticSearchBridge> Bridge =
            new Lazy<Utils.SemanticSearchBridge>(() => new Utils.SemanticSearchBridge());

        [McpServerTool, Description(
            "Search Tekla template attributes by natural language. " +
            "Finds the correct attribute key for GetReportProperty() calls. " +
            "Uses local MiniLM model + ChromaDB — instant, fully offline. " +
            "Examples: 'flange thickness' -> FLANGE_THICKNESS, " +
            "'bolt edge distance' -> BOLT_EDGE_DISTANCE, " +
            "'assembly weight' -> ASSEMBLY.WEIGHT_GROSS")]
        public static string SearchAttributes(
            [Description("Natural language description of the attribute you need")] string query,
            [Description("Number of results to return (default 10)")] int topK = 10)
        {
            try
            {
                var response = Bridge.Value.SearchAttributes(query, topK);
                var result = response["result"];
                return result != null ? result.ToString() : response.ToString();
            }
            catch (Exception ex)
            {
                return JObject.FromObject(new
                {
                    error = ex.Message,
                    hint = "Install Python 3.11+ and run: pip install -r semantic-service/requirements.txt. " +
                           "If Python is not on PATH, set PYTHON_PATH env var to python.exe or py.exe."
                }).ToString();
            }
        }

        [McpServerTool, Description(
            "Search the Tekla Open API (14,380 members) by natural language. " +
            "Finds API classes, methods, and properties for unimplemented features. " +
            "Uses local MiniLM model + ChromaDB — instant, fully offline. " +
            "Examples: 'create contour plate' -> ContourPlate.Insert(), " +
            "'get solid geometry' -> Part.GetSolid(), " +
            "'clash detection' -> ClashCheckData")]
        public static string SearchApi(
            [Description("Natural language description of what you want to do")] string query,
            [Description("Number of results to return (default 10)")] int topK = 10)
        {
            try
            {
                var response = Bridge.Value.SearchApi(query, topK);
                var result = response["result"];
                return result != null ? result.ToString() : response.ToString();
            }
            catch (Exception ex)
            {
                return JObject.FromObject(new
                {
                    error = ex.Message,
                    hint = "Install Python 3.11+ and run: pip install -r semantic-service/requirements.txt. " +
                           "If Python is not on PATH, set PYTHON_PATH env var to python.exe or py.exe."
                }).ToString();
            }
        }
    }
}
