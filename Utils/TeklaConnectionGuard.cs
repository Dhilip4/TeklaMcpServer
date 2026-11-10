using System;
using System.Collections.Generic;
using Tekla.Structures.Model;

namespace TeklaMcpServer.Utils
{
    /// <summary>
    /// Helper that checks Tekla connectivity and returns a consistent error JSON on failure.
    /// Always creates a fresh Model() per call — never cache across tool invocations.
    /// </summary>
    public static class TeklaConnectionGuard
    {
        /// <summary>
        /// Returns a connected Model, or null if not connected.
        /// </summary>
        public static Model TryConnect()
        {
            var model = new Model();
            return model.GetConnectionStatus() ? model : null;
        }

        /// <summary>
        /// Look up a single ModelObject by its integer ID using O(1) SelectModelObject.
        /// Returns null if not found.
        /// </summary>
        public static ModelObject SelectById(Model model, int id)
        {
            var obj = model.SelectModelObject(new Tekla.Structures.Identifier(id));
            if (obj == null) return null;
            obj.Select();
            return obj;
        }

        /// <summary>
        /// Look up multiple ModelObjects by integer IDs.
        /// </summary>
        public static List<ModelObject> SelectByIds(Model model, List<int> ids)
        {
            var results = new List<ModelObject>(ids.Count);
            foreach (var id in ids)
                results.Add(SelectById(model, id));
            return results;
        }

        /// <summary>
        /// Returns the standard "not connected" error JSON.
        /// </summary>
        public static string NotConnectedError()
            => "{\"error\": \"Not connected to Tekla Structures. Make sure Tekla is running with a model open.\"}";

        /// <summary>
        /// Returns a JSON-formatted error for unexpected exceptions.
        /// </summary>
        public static string ExceptionError(Exception ex)
            => $"{{\"error\": \"{EscapeJson(ex.Message)}\"}}";

        public static string EscapeJsonPublic(string s) => EscapeJson(s);

        private static string EscapeJson(string s)
            => s?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "") ?? "";
    }
}
