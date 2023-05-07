﻿using Supabase.Core.Attributes;

namespace Supabase.Realtime
{
    public static class Constants
    {
        public enum SocketStates
        {
            connecting = 0,
            open = 1,
            closing = 2,
            closed = 3
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
		public const int DEFAULT_TIMEOUT = 10000;
        public const int WS_CLOSE_NORMAL = 1000;

        /// <summary>
        /// Pheonix Socket Server Event: CLOSE
        /// </summary>
        public static string CHANNEL_EVENT_CLOSE = "phx_close";

        /// <summary>
        /// Pheonix Socket Server Event: ERROR
        /// </summary>
        public static string CHANNEL_EVENT_ERROR = "phx_error";

        /// <summary>
        /// Pheonix Socket Server Event: JOIN
        /// </summary>
        public static string CHANNEL_EVENT_JOIN = "phx_join";

        /// <summary>
        /// Pheonix Socket Server Event: REPLY
        /// </summary>
        public static string CHANNEL_EVENT_REPLY = "phx_reply";

        /// <summary>
        /// Pheonix Socket Server Event: LEAVE
        /// </summary>
        public static string CHANNEL_EVENT_LEAVE = "phx_leave";

        public static string PHEONIX_STATUS_OK = "ok";
        public static string PHEONIX_STATUS_ERROR = "error";

        public static string TRANSPORT_WEBSOCKET = "websocket";

        public static string CHANNEL_ACCESS_TOKEN = "access_token";
    }
}
