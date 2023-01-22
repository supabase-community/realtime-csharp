using Newtonsoft.Json;
using Supabase.Realtime.Models;
using Supabase.Realtime.Socket;
using System;
using System.Collections.Generic;
using System.Text;

namespace Supabase.Realtime.Presence.Responses
{
    public class PresenceStateSocketResponse<TPresence> : SocketResponse<Dictionary<string, PresenceStatePayload<TPresence>>> 
        where TPresence : BasePresence
    {
        public PresenceStateSocketResponse(JsonSerializerSettings serializerSettings) : base(serializerSettings) { }
    }

    public class PresenceStatePayload<TPresence> where TPresence : BasePresence
    {
        [JsonProperty("metas")]
        public List<TPresence>? Metas { get; set; }
    }
}
