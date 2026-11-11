using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoboEstepario
{
    /// <summary>Envoltorio de System.Text.Json con el comportamiento que tenía JavaScriptSerializer.</summary>
    public static class Json
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };

        public static string Serialize(object value) => JsonSerializer.Serialize(value, Options);

        public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);

        /// <summary>
        /// Devuelve un grafo de Dictionary&lt;string, object&gt;, object[], string, int/long/double, bool y null.
        /// </summary>
        public static object DeserializeObject(string json)
        {
            using var document = JsonDocument.Parse(json);
            return ToObject(document.RootElement);
        }

        private static object ToObject(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var map = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (var property in element.EnumerateObject()) map[property.Name] = ToObject(property.Value);
                    return map;
                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in element.EnumerateArray()) list.Add(ToObject(item));
                    return list.ToArray();
                case JsonValueKind.String: return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out var i)) return i;
                    if (element.TryGetInt64(out var l)) return l;
                    return element.GetDouble();
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                default: return null;
            }
        }
    }
}
