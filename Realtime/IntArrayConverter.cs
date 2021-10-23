using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Supabase.Realtime
{
    public class IntArrayConverter : JsonConverter
    {
        public override bool CanRead => true;

        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType) => throw new NotImplementedException();

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.Value != null)
            {
                return Parse(reader.Value as string);
            }

            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        private List<int> Parse(string value)
        {
            var result = new List<int>();

            // {1,2,3}
            if (value.Contains("{"))
            {
                var array = value.Trim(new char[] { '{', '}' }).Split(',');
                foreach (var item in array)
                    result.Add(int.Parse(item));

                return result;
            }
            // [1,2,3]
            else if (value.Contains("["))
            {
                var array = value.Trim(new char[] { '[', ']' }).Split(',');
                foreach (var item in array)
                    result.Add(int.Parse(item));

                return result;
            }

            return result;
        }
    }
}
