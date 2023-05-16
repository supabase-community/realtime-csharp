using Newtonsoft.Json;

namespace Supabase.Realtime.Socket.Responses
{
    public class PhoenixResponse
    {
        [JsonProperty("response")]
        public object? Response;

        [JsonProperty("status")]
        public string? Status;
    }
}
