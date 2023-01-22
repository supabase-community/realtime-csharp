﻿using Newtonsoft.Json;
using Postgrest.Models;
using Supabase.Realtime.Interfaces;
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
        private JsonSerializerSettings serializerSettings;
        public SocketResponse(JsonSerializerSettings serializerSettings)
        {
            this.serializerSettings = serializerSettings;
        }

        /// <summary>
        /// Hydrates the referenced record into a Model (if possible).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public TModel? Model<TModel>() where TModel : BaseModel, new()
        {
            if (Json != null && Payload != null && Payload.Record != null)
            {
                var response = JsonConvert.DeserializeObject<SocketResponse<SocketResponsePayload<TModel>>>(Json, serializerSettings);
                return response?.Payload?.Record;
            }
            else
            {
                return default;
            }
        }

        /// <summary>
        /// Hydrates the old_record into a Model (if possible).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public TModel? OldModel<TModel>() where TModel : BaseModel, new()
		{
            if (Json != null && Payload != null && Payload.OldRecord != null)
            {
                var response = JsonConvert.DeserializeObject<SocketResponse<SocketResponsePayload<TModel>>>(Json, serializerSettings);
                return response?.Payload?.OldRecord;
            }
            else
            {
                return default;
            }
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
                switch(_event)
                {
                    case "presence_state":
                        return EventType.PresenceState;
                    case "presence_diff":
                        return EventType.PresenceDiff;
                    case "broadcast":
                        return EventType.Broadcast;
                }

                if (Payload == null) return EventType.Unknown;

                switch (Payload.Type)
                {
                    case "INSERT":
                        return EventType.Insert;
                    case "UPDATE":
                        return EventType.Update;
                    case "DELETE":
                        return EventType.Delete;
                }

                return EventType.Unknown;
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
