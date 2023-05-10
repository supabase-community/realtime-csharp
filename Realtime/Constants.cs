using Supabase.Core.Attributes;

namespace Supabase.Realtime
{
    public static class Constants
    {
        public enum SocketState
        {
	        Open,
	        Close,
	        Reconnect,
	        Error
        }

        public enum EventType
        {
            Insert,
            Update,
            Delete,
            Broadcast,
            PresenceState,
            PresenceDiff,
            PostgresChanges,
            System,
            Internal,
            Unknown
        }

		public enum PresenceListenEventTypes
		{
			[MapTo("sync")]
			Sync,
			[MapTo("join")]
			Join,
			[MapTo("leave")]
			Leave
		}

        public enum ChannelEventName
        {
            [MapTo("broadcast")]
            Broadcast,
            [MapTo("presence")]
            Presence,
            [MapTo("postgres_changes")]
            PostgresChanges
        }

		/// <summary>
		/// Channel state with associated string representations.
		/// </summary>
		public enum ChannelState
		{
			[MapTo("closed")]
			Closed,
			[MapTo("errored")]
			Errored,
			[MapTo("joined")]
			Joined,
			[MapTo("joining")]
			Joining,
			[MapTo("leaving")]
			Leaving
		}

		/// <summary>
		/// Timeout interval for requests (used in Socket and Push)
		/// </summary>
		public const int DefaultTimeout = 10000;
        public const int WsCloseNormal = 1000;

        /// <summary>
        /// Phoenix Socket Server Event: CLOSE
        /// </summary>
        public static string CHANNEL_EVENT_CLOSE = "phx_close";

        /// <summary>
        /// Phoenix Socket Server Event: ERROR
        /// </summary>
        public static string CHANNEL_EVENT_ERROR = "phx_error";

        /// <summary>
        /// Phoenix Socket Server Event: JOIN
        /// </summary>
        public const string ChannelEventJoin = "phx_join";

        /// <summary>
        /// Phoenix Socket Server Event: REPLY
        /// </summary>
        public const string ChannelEventReply = "phx_reply";

        /// <summary>
        /// Phoenix Socket Server Event: LEAVE
        /// </summary>
        public const string ChannelEventLeave = "phx_leave";

        public const string PhoenixStatusOk = "ok";
        public const string PheonixStatusError = "error";

        public const string TransportWebsocket = "websocket";

        public const string ChannelAccessToken = "access_token";
    }
}
