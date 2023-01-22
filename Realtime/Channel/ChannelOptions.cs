using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Supabase.Realtime.Channel
{
    public class ChannelOptions
    {
        public Dictionary<string, string>? Parameters { get; set; }

        public ClientOptions ClientOptions { get; set; }

        public JsonSerializerSettings SerializerSettings { get; set; }

        public ChannelOptions(ClientOptions clientOptions, JsonSerializerSettings serializerSettings)
        {
            ClientOptions = clientOptions;
            SerializerSettings = serializerSettings;
        }
    }
}
