using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Newtonsoft.Json;

namespace Supabase.Realtime
{
    public class ClientOptions
    {
        /// <summary>
        /// The function to encode outgoing messages. Defaults to JSON
        /// </summary>
        public Action<object, Action<string>> Encode { get; set; } = (payload, callback) => callback(JsonConvert.SerializeObject(payload,Client.Instance.SerializerSettings));

        /// <summary>
        /// The function to decode incoming messages.
        /// </summary>
        public Action<string, Action<SocketResponse>> Decode { get; set; } = (payload, callback) => callback(JsonConvert.DeserializeObject<SocketResponse>(payload, Client.Instance.SerializerSettings));

        /// <summary>
        /// Logging function
        /// </summary>
        public Action<string, string, object> Logger { get; set; } = (kind, msg, data) => Debug.WriteLine($"{kind}: {msg}, {JsonConvert.SerializeObject(data, Formatting.Indented)}");

        /// <summary>
        /// The Websocket Transport, for example WebSocket.
        /// </summary>
        public string Transport { get; set; } = Constants.TRANSPORT_WEBSOCKET;

        /// <summary>
        /// The default timeout in milliseconds to trigger push timeouts.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(Constants.DEFAULT_TIMEOUT);

        /// <summary>
        /// The interval to send a heartbeat message
        /// </summary>
        public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The interval to reconnect
        /// </summary>
        public Func<int, TimeSpan> ReconnectAfterInterval { get; set; } = (tries) =>
        {
            var intervals = new int[] { 1, 2, 5, 10 };
            return TimeSpan.FromSeconds(tries < intervals.Length ? tries - 1 : 10);
        };

        /// <summary>
        /// The maximum timeout of a long poll AJAX request.
        /// </summary>
        public TimeSpan LongPollerTimeout = TimeSpan.FromSeconds(20);

        /// <summary>
        /// Request headers to be appended to the connection string.
        /// </summary>
        public Dictionary<string, object> Headers = new Dictionary<string, object>();

        /// <summary>
        /// The optional params to pass when connecting
        /// </summary>
        public SocketOptionsParameters Parameters = new SocketOptionsParameters();

        /// <summary>
        /// Datetime Style for JSON Deserialization of Models
        /// </summary>
        public DateTimeStyles DateTimeStyles = DateTimeStyles.AdjustToUniversal;

        /// <summary>
        /// Datetime format for JSON Deserialization of Models (Postgrest style)
        /// </summary>
        public string DateTimeFormat = "yyyy'-'MM'-'dd' 'HH':'mm':'ss.FFFFFFK";
    }
}
