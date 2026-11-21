using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Data.Config
{
    /// <summary>
    /// Stores a list property as one JSON value in a <c>jsonb</c> column, so a whole agent run
    /// (its steps, guardrail checks, sources) can be read back in one row, without extra tables.
    /// </summary>
    internal static class JsonColumn
    {
        // camelCase names and enums as text, so the JSON is easy to read in the database.
        private static readonly JsonSerializerOptions Options = CreateOptions();

        /// <summary>Maps a <c>List&lt;T&gt;</c> property to a <c>jsonb</c> column.</summary>
        public static PropertyBuilder<List<T>> HasJsonColumn<T>(this PropertyBuilder<List<T>> property)
        {
            // The lambdas call small helper methods on purpose: EF turns them into expression trees,
            // and calling JsonSerializer directly there can pick an overload that trees don't support.
            var converter = new ValueConverter<List<T>, string>(
                list => ToJson(list),
                json => FromJson<T>(json));

            // Tells EF how to spot a change INSIDE the list (otherwise it only sees a new list object).
            var comparer = new ValueComparer<List<T>>(
                (a, b) => ToJson(a) == ToJson(b),
                list => ToJson(list).GetHashCode(),
                list => FromJson<T>(ToJson(list)));

            property.HasConversion(converter, comparer).HasColumnType("jsonb").IsRequired();
            return property;
        }

        private static string ToJson<T>(List<T> list)
        {
            return JsonSerializer.Serialize(list ?? new List<T>(), Options);
        }

        private static List<T> FromJson<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<T>();

            return JsonSerializer.Deserialize<List<T>>(json, Options) ?? new List<T>();
        }

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
    }
}
