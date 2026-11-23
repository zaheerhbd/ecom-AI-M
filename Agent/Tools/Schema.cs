using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Agent.Tools
{
    /// <summary>The JSON Schema types a tool argument can have.</summary>
    public static class ToolParameterTypes
    {
        /// <summary>Text, e.g. "Hats".</summary>
        public const string String = "string";

        /// <summary>Any number, e.g. 49.99.</summary>
        public const string Number = "number";

        /// <summary>A whole number, e.g. 5.</summary>
        public const string Integer = "integer";

        /// <summary>true or false.</summary>
        public const string Boolean = "boolean";
    }

    /// <summary>Describes one argument of a tool, e.g. "maxPrice" (a number, optional).</summary>
    public sealed class ToolParameter
    {
        /// <summary>Creates a text parameter.</summary>
        public ToolParameter(string name, string description, bool required)
            : this(name, description, required, ToolParameterTypes.String, null)
        {
        }

        /// <summary>Creates a parameter of the given type.</summary>
        public ToolParameter(string name, string description, bool required, string type)
            : this(name, description, required, type, null)
        {
        }

        /// <summary>Creates a parameter with a fixed list of allowed values (an "enum"), e.g. Boards, Hats, Boots, Gloves.</summary>
        public ToolParameter(string name, string description, bool required, string type, IReadOnlyList<string> enumValues)
        {
            Name = name;
            Description = description;
            Required = required;
            Type = type ?? ToolParameterTypes.String;
            EnumValues = enumValues;
        }

        /// <summary>The argument's name, as the model must write it.</summary>
        public string Name { get; }

        /// <summary>Tells the model what to put in this argument.</summary>
        public string Description { get; }

        /// <summary>True if the model must always send it.</summary>
        public bool Required { get; }

        /// <summary>"string", "number", "integer" or "boolean" (see ToolParameterTypes).</summary>
        public string Type { get; }

        /// <summary>The only values allowed, or null for "anything of the right type".</summary>
        public IReadOnlyList<string> EnumValues { get; }
    }

    /// <summary>
    /// Small helpers so each tool's schema is short and readable, and so tools can read
    /// arguments safely. Models are sloppy: they may send 5, "5" or "5.0" for the same number,
    /// so the Get... helpers accept all of them and return null for anything they can't read.
    /// </summary>
    public static class Schema
    {
        /// <summary>Builds { "type":"object", "properties": {...}, "required": [...] }.</summary>
        public static JsonObject Object(params ToolParameter[] parameters)
        {
            JsonObject properties = new JsonObject();
            JsonArray required = new JsonArray();

            foreach (ToolParameter parameter in parameters)
            {
                JsonObject property = new JsonObject();
                property["type"] = parameter.Type;
                property["description"] = parameter.Description;

                if (parameter.EnumValues != null && parameter.EnumValues.Count > 0)
                {
                    JsonArray allowed = new JsonArray();
                    foreach (string value in parameter.EnumValues)
                        allowed.Add(value);
                    property["enum"] = allowed;
                }

                properties[parameter.Name] = property;

                if (parameter.Required)
                    required.Add(parameter.Name);
            }

            JsonObject schema = new JsonObject();
            schema["type"] = "object";
            schema["properties"] = properties;
            schema["required"] = required;
            return schema;
        }

        /// <summary>Reads a text argument. A number is returned as its text. Null if missing.</summary>
        public static string GetString(this JsonElement arguments, string name)
        {
            JsonElement value;
            if (!TryGetArgument(arguments, name, out value))
                return null;

            if (value.ValueKind == JsonValueKind.String)
                return value.GetString();

            if (value.ValueKind == JsonValueKind.Number)
                return value.GetRawText();

            return null;
        }

        /// <summary>Reads a number argument. Accepts 12.5 and "12.5". Null if missing or not a number.</summary>
        public static double? GetNumber(this JsonElement arguments, string name)
        {
            JsonElement value;
            if (!TryGetArgument(arguments, name, out value))
                return null;

            double number;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out number))
                return number;

            if (value.ValueKind == JsonValueKind.String && TryParseNumber(value.GetString(), out number))
                return number;

            return null;
        }

        /// <summary>Reads a whole-number argument. Accepts 5, "5" and 5.0. Null if missing or not a whole number.</summary>
        public static int? GetInt(this JsonElement arguments, string name)
        {
            double? number = GetNumber(arguments, name);
            if (!number.HasValue)
                return null;

            // 5.5 is not a whole number, and huge values don't fit in an int.
            double value = number.Value;
            if (Math.Floor(value) != value || value > int.MaxValue || value < int.MinValue)
                return null;

            return (int)value;
        }

        /// <summary>Reads a true/false argument. Accepts true, "true", "yes", "1" (and the opposites). Null if missing.</summary>
        public static bool? GetBool(this JsonElement arguments, string name)
        {
            JsonElement value;
            if (!TryGetArgument(arguments, name, out value))
                return null;

            if (value.ValueKind == JsonValueKind.True)
                return true;

            if (value.ValueKind == JsonValueKind.False)
                return false;

            if (value.ValueKind == JsonValueKind.Number)
            {
                double number;
                if (value.TryGetDouble(out number))
                {
                    if (number == 1)
                        return true;
                    if (number == 0)
                        return false;
                }

                return null;
            }

            if (value.ValueKind != JsonValueKind.String)
                return null;

            string text = (value.GetString() ?? "").Trim().ToLowerInvariant();
            switch (text)
            {
                case "true":
                case "yes":
                case "1":
                    return true;
                case "false":
                case "no":
                case "0":
                    return false;
                default:
                    return null;
            }
        }

        /// <summary>Parses a number written as text, always with a dot as the decimal point.</summary>
        internal static bool TryParseNumber(string text, out double number)
        {
            return double.TryParse((text ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        }

        /// <summary>Finds an argument by name. False when the arguments are not an object, or the value is missing or null.</summary>
        private static bool TryGetArgument(JsonElement arguments, string name, out JsonElement value)
        {
            value = default(JsonElement);
            if (arguments.ValueKind != JsonValueKind.Object)
                return false;

            if (!arguments.TryGetProperty(name, out value))
                return false;

            return value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined;
        }
    }
}
