using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Supabase.Realtime
{
    public class ChannelOptions
    {
        public string Database { get; set; }
        public string? Schema { get; set; }
        public string? Table { get; set; }

        public string? Column { get; set; }

        public string? Value { get; set; }

        public Dictionary<string, string>? Parameters { get; set; }

        public ClientOptions ClientOptions { get; set; }

        public JsonSerializerSettings SerializerSettings { get; set; }

        public ChannelOptions(string database, ClientOptions clientOptions, JsonSerializerSettings serializerSettings)
        {
            Database = database;
            ClientOptions = clientOptions;
            SerializerSettings = serializerSettings;
        }
    }
}
