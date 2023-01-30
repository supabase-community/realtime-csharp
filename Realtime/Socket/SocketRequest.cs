using Newtonsoft.Json;

namespace Supabase.Realtime.Socket
{
    /// <summary>
    /// Representation of a Socket Request.
    /// </summary>
    public class SocketRequest
    {
        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("topic")]
        public string? Topic { get; set; }

        [JsonProperty("event")]
        public string? Event { get; set; }

        [JsonProperty("payload")]
        public object? Payload { get; set; }

        [JsonProperty("ref")]
        public string? Ref { get; set; }
    }
}
