using Newtonsoft.Json;

namespace Supabase.Realtime.Socket
{
    public class SocketOptionsParameters
    {
        [JsonProperty("token")]
        public string? Token { get; set; }

        [JsonProperty("apikey")]
        public string? ApiKey { get; set; }
    }
}
