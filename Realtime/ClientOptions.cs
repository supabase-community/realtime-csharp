using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;

namespace Supabase.Realtime
{
    public class ClientOptions
    {
        // The function to encode outgoing messages. Defaults to JSON:
        public Action<object, Action<string>> Encode { get; set; } = (payload, callback) => callback(JsonConvert.SerializeObject(payload));

        // The function to decode incoming messages.
        public Action<string, Action<SocketMessage>> Decode { get; set; } = (payload, callback) => callback(JsonConvert.DeserializeObject<SocketMessage>(payload));

        public Action<string, string, object> Logger { get; set; } = (kind, msg, data) => Debug.WriteLine($"{kind}: {msg}, {0}", JsonConvert.SerializeObject(data, Formatting.Indented));

        // The Websocket Transport, for example WebSocket.
        public string Transport { get; set; } = Constants.TRANSPORT_WEBSOCKET;

        // The default timeout in milliseconds to trigger push timeouts.
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(Constants.DEFAULT_TIMEOUT);

        // The interval to send a heartbeat message
        public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);

        // The interval to reconnect
        public Func<int, TimeSpan> ReconnectAfterInterval { get; set; } = (tries) =>
        {
            var intervals = new int[] { 1, 2, 5, 10 };
            return TimeSpan.FromSeconds(tries < intervals.Length ? tries - 1 : 10);
        };

        // The maximum timeout of a long poll AJAX request.
        public TimeSpan LongPollerTimeout = TimeSpan.FromSeconds(20);

        public Dictionary<string, object> Headers = new Dictionary<string, object>();

        // The optional params to pass when connecting
        public SocketOptionsParameters Parameters = new SocketOptionsParameters();
    }
}
