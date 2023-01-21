using Newtonsoft.Json;

namespace Supabase.Realtime.Channel
{
    public class PheonixResponse
    {
        [JsonProperty("response")]
        public object? Response;

        [JsonProperty("status")]
        public string? Status;
    }
}
