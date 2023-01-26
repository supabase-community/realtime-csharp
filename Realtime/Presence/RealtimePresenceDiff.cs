using Newtonsoft.Json;
using Supabase.Realtime.Models;
using Supabase.Realtime.Presence.Responses;
using Supabase.Realtime.Socket;
using System.Collections.Generic;

namespace Supabase.Realtime.Presence
{
	/// <summary>
	/// Represents a presence_diff response
	/// </summary>
	/// <typeparam name="TPresence"></typeparam>
    public class RealtimePresenceDiff<TPresence> : SocketResponse<PresenceDiffPayload<TPresence>> where TPresence : BasePresence
    {
		public RealtimePresenceDiff(JsonSerializerSettings serializerSettings) : base(serializerSettings)
		{}
    }

	public class PresenceDiffPayload<TPresence> where TPresence : BasePresence
	{
		[JsonProperty("joins")]
		public Dictionary<string, PresenceDiffPayloadMeta<TPresence>>? Joins { get; set; }

		[JsonProperty("leaves")]
		public Dictionary<string, PresenceDiffPayloadMeta<TPresence>>? Leaves { get; set; }
	}

	public class PresenceDiffPayloadMeta<TPresence> where TPresence : BasePresence
	{
		[JsonProperty("metas")]
		public List<TPresence>? Metas { get; set; }
	}
}
