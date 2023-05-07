using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Supabase.Realtime.Channel
{
    public class ChannelOptions
    {
        public Func<string?> RetrieveAccessToken { get; private set; }

        public Dictionary<string, string>? Parameters { get; set; }

        public ClientOptions ClientOptions { get; set; }

        public JsonSerializerSettings SerializerSettings { get; set; }

        public ChannelOptions(ClientOptions clientOptions, Func<string?> retrieveAccessToken, JsonSerializerSettings serializerSettings)
        {
            ClientOptions = clientOptions;
            SerializerSettings = serializerSettings;
            RetrieveAccessToken = retrieveAccessToken;
        }
    }
}
