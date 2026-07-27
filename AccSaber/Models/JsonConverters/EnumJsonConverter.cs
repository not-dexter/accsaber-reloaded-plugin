using System;
using System.Linq;
using Newtonsoft.Json;

namespace AccSaber.Models.JsonConverters
{
    public sealed class EnumJsonConverter<T>(bool ignoreCase) : JsonConverter<T?> where T : struct, Enum
    {
        private readonly bool _ignoreCase = ignoreCase;

        public EnumJsonConverter() : this(false) { }

        public override T? ReadJson(JsonReader reader, Type objectType, T? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            T? fallbackValue = hasExistingValue ? existingValue : null;

            if (reader.TokenType == JsonToken.Null)
                return fallbackValue;

            if (reader.TokenType != JsonToken.String)
                return fallbackValue;

            string? value = reader.Value as string;

            if (string.IsNullOrWhiteSpace(value))
                return fallbackValue;

            StringComparison comparison = _ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            bool nameExists = Enum.GetNames(typeof(T)).Any(name => string.Equals(name, value, comparison));

            if (!nameExists)
                return fallbackValue;

            return (T)Enum.Parse(typeof(T), value, _ignoreCase);
        }

        public override void WriteJson(JsonWriter writer, T? value, JsonSerializer serializer)
        {
            if (value.HasValue)
                writer.WriteValue(value.Value.ToString());
            else
                writer.WriteNull();
        }
    }
}
