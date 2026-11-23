using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Agent.Tools
{
    /// <summary>
    /// Optional extra for a tool: write your own short stub of an old result.
    /// (C# 7.3 has no default interface methods, so this is a second interface instead of a
    /// method on ITool.) Tools without it get ToolResultSummariser's default stub.
    /// </summary>
    public interface ISummarisingTool
    {
        /// <summary>A short version of the tool's output that keeps the ids, so the model can look things up again.</summary>
        string Summarise(string output);
    }

    /// <summary>
    /// SHRINKING OLD TOOL RESULTS (memory, BRD R12).
    ///
    /// A product search can return a few thousand characters. Once the answer is written, the user
    /// has seen the facts, but the full result would be re-sent with EVERY later question in the chat,
    /// costing tokens each time. So at the end of each answer, the saved conversation keeps only a stub:
    ///
    ///     [search_products: 10 items, ids 12, 45, 88, ...]
    ///
    /// The ids let the model call get_product_details again if the user asks for more.
    /// The CURRENT answer always works with the full results.
    /// </summary>
    public static class ToolResultSummariser
    {
        /// <summary>Results this short are kept as they are.</summary>
        public const int KeepWholeBelow = 200;

        private const int MaxIds = 20;

        /// <summary>Makes the stub for one tool result.</summary>
        public static string Summarise(ITool tool, string toolName, string output)
        {
            if (string.IsNullOrEmpty(output) || output.Length <= KeepWholeBelow)
                return output ?? "";

            ISummarisingTool custom = tool as ISummarisingTool;
            if (custom != null)
                return custom.Summarise(output);

            return DefaultStub(toolName, output);
        }

        /// <summary>JSON with ids -> "[name: N items, ids ...]"; anything else -> its first 200 characters.</summary>
        internal static string DefaultStub(string toolName, string output)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(output))
                {
                    List<string> ids = new List<string>();
                    int items = CountItemsAndCollectIds(document.RootElement, ids);
                    if (ids.Count > 0)
                    {
                        string idText = string.Join(", ", ids.Count > MaxIds ? ids.GetRange(0, MaxIds) : ids);
                        if (ids.Count > MaxIds)
                            idText += ", ...";
                        return "[" + toolName + ": " + items + " item" + (items == 1 ? "" : "s") + ", ids " + idText +
                               ". Full result shortened; call the tool again for details.]";
                    }
                }
            }
            catch (JsonException)
            {
                // Not JSON (e.g. policy text): fall through to the plain cut.
            }

            return "[" + toolName + ", shortened: " + output.Substring(0, KeepWholeBelow).TrimEnd() + "...]";
        }

        /// <summary>Finds the first list of objects (e.g. "items", "orders") and collects their "id" values.</summary>
        private static int CountItemsAndCollectIds(JsonElement element, List<string> ids)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in element.EnumerateArray())
                {
                    JsonElement id;
                    if (item.ValueKind == JsonValueKind.Object && TryGetId(item, out id))
                        ids.Add(id.ValueKind == JsonValueKind.String ? id.GetString() : id.GetRawText());
                }
                return element.GetArrayLength();
            }

            if (element.ValueKind == JsonValueKind.Object)
            {
                // A single object with an id (e.g. get_product_details, get_order).
                JsonElement ownId;
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        int count = CountItemsAndCollectIds(property.Value, ids);
                        if (ids.Count > 0)
                            return count;
                    }
                }

                if (TryGetId(element, out ownId))
                {
                    ids.Add(ownId.ValueKind == JsonValueKind.String ? ownId.GetString() : ownId.GetRawText());
                    return 1;
                }
            }

            return 0;
        }

        private static bool TryGetId(JsonElement element, out JsonElement id)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, "id", StringComparison.OrdinalIgnoreCase))
                {
                    id = property.Value;
                    return true;
                }
            }

            id = default(JsonElement);
            return false;
        }
    }
}
