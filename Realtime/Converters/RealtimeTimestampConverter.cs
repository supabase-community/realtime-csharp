using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Supabase.Realtime.Converters
{
    public class TimestampConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return false;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return DateTime.MinValue;
            }
            else
            {
                JObject obj = JObject.Load(reader);
            }

            return DateTime.MinValue;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
