using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("RealtimeTests")]
namespace Supabase.Realtime.Converters
{
    public class StringArrayConverter : JsonConverter
    {
        public override bool CanRead => true;

        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType) => throw new NotImplementedException();

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            try
            {
                if (reader.Value != null)
                {
                    return Parse(reader.Value as string);
                }
                else
                {
                    JArray jo = JArray.Load(reader);
                    string json = jo.ToString(Formatting.None);
                    return jo.ToObject<List<string>>(serializer);
                }
            }
            catch
            {
                return null;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        internal List<string> Parse(string value)
        {
            var result = new List<string>();

            var firstChar = value[0];
            var lastChar = value[value.Length - 1];

            // {1,2,3}
            if (firstChar == '{' && lastChar == '}')
            {
                var array = value.Trim(new char[] { '{', '}' }).Split(',');
                foreach (var item in array)
                {
                    if (string.IsNullOrEmpty(item)) continue;
                    result.Add(item);
                }

                return result;
            }
            // [1,2,3]
            else if (firstChar == '[' && lastChar == ']')
            {
                var array = value.Trim(new char[] { '[', ']' }).Split(',');
                foreach (var item in array)
                {
                    if (string.IsNullOrEmpty(item)) continue;
                    result.Add(item);
                }

                return result;
            }

            return result;
        }
    }
}
