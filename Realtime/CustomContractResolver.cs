using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Postgrest.Converters;
using Supabase.Realtime.Converters;

namespace Supabase.Realtime
{
    /// <summary>
    /// A custom resolver that handles mapping column names and property names as well
    /// as handling the conversion of Postgrest Ranges to a C# `Range`.
    /// </summary>
    internal class CustomContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty prop = base.CreateProperty(member, memberSerialization);

            if (prop.PropertyType == typeof(List<int>))
            {
                prop.Converter = new Converters.IntArrayConverter();
            }
            else if (prop.PropertyType == typeof(List<string>))
            {
                prop.Converter = new Converters.StringArrayConverter();
            }
            else if (prop.PropertyType == typeof(DateTime))
            {
                prop.Converter = new TimestampConverter();
            }

            return prop;
        }
    }
}
