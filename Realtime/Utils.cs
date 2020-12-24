using System;
using System.Collections.Generic;
using System.Linq;

namespace Supabase.Realtime
{
    internal static class Utils
    {
        public static string QueryString(IDictionary<string, object> dict)
        {
            var list = new List<string>();
            foreach (var item in dict)
            {
                list.Add(item.Key + "=" + item.Value);
            }
            return string.Join("&", list);
        }

        public static string GenerateChannelTopic(string database, string schema, string table, string col, string value)
        {
            var list = new List<String> { database, schema, table };
            string channel = String.Join(":", list.Where(s => !string.IsNullOrEmpty(s)));

            if (!string.IsNullOrEmpty(col) && !string.IsNullOrEmpty(value))
            {
                channel += $":{col}.eq.{value}";
            }

            return channel;
        }
    }
}
