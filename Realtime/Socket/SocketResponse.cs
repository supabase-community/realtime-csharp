using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Postgrest.Models;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.PostgresChanges;
using static Supabase.Realtime.Constants;

namespace Supabase.Realtime.Socket
{
	/// <summary>
	/// A SocketResponse with support for Generically typed Payload
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class SocketResponse<T> : SocketResponse where T : class
	{
		public SocketResponse(JsonSerializerSettings serializerSettings) : base(serializerSettings)
		{ }

		[JsonProperty("payload")]
		public new T? Payload { get; set; }
	}

	/// <summary>
	/// Representation of a Socket Response.
	/// </summary>
	public class SocketResponse : IRealtimeSocketResponse
	{
		internal JsonSerializerSettings serializerSettings;

		public SocketResponse(JsonSerializerSettings serializerSettings)
		{
			this.serializerSettings = serializerSettings;
		}

		/// <summary>
		/// The internal realtime topic.
		/// </summary>
		[JsonProperty("topic")]
		public string? Topic { get; set; }

		[JsonProperty("event")]
		public string? _event { get; set; }

		[JsonIgnore]
		public EventType Event
		{
			get
			{
				switch (_event)
				{
					case "presence_state":
						return EventType.PresenceState;
					case "presence_diff":
						return EventType.PresenceDiff;
					case "broadcast":
						return EventType.Broadcast;
					case "postgres_changes":
						return EventType.PostgresChanges;
				}

				if (Payload == null) return EventType.Unknown;

				return Payload.Type;
			}
		}

		/// <summary>
		/// The payload/response.
		/// </summary>
		[JsonProperty("payload")]
		public SocketResponsePayload? Payload { get; set; }

		/// <summary>
		/// An internal reference to this particular feedback loop.
		/// </summary>
		[JsonProperty("ref")]
		public string? Ref { get; set; }

		[JsonIgnore]
		internal string? Json { get; set; }
	}
}
