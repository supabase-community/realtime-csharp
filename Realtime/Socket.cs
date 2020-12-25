using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Postgrest.Models;
using WebSocketSharp;
using static Supabase.Realtime.SocketStateChangedEventArgs;

namespace Supabase.Realtime
{
    /// <summary>
    /// Socket connection handler.
    /// </summary>
    public class Socket
    {
        /// <summary>
        /// Returns whether or not the connection is alive.
        /// </summary>
        public bool IsConnected => connection.IsAlive;

        /// <summary>
        /// Invoked when the socket state changes.
        /// </summary>
        public EventHandler<SocketStateChangedEventArgs> StateChanged;

        /// <summary>
        /// Invoked when a message has been recieved and decoded.
        /// </summary>
        public EventHandler<SocketMessageEventArgs> OnMessage;

        private string endpoint;
        private ClientOptions options;
        private WebSocket connection;

        private Task heartbeatTask;
        private CancellationTokenSource heartbeatTokenSource;

        private bool hasPendingHeartbeat = false;
        private string pendingHeartbeatRef = "0";

        private Task reconnectTask;
        private CancellationTokenSource reconnectTokenSource;

        private List<Task> buffer = new List<Task>();
        private int reference = 0;

        private string endpointUrl
        {
            get
            {
                var parameters = new Dictionary<string, object> {
                    { "apikey", options.Parameters.ApiKey }
                };

                return string.Format($"{endpoint}?{Utils.QueryString(parameters)}");
            }
        }

        /// <summary>
        /// Initializes this Socket instance.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="options"></param>
        public Socket(string endpoint, ClientOptions options = null)
        {
            this.endpoint = $"{endpoint}/{Constants.TRANSPORT_WEBSOCKET}";

            if (options == null)
            {
                options = new ClientOptions();
            }

            this.options = options;
        }

        /// <summary>
        /// Connects to a socket server and registers event listeners.
        /// </summary>
        public void Connect()
        {
            if (connection != null) return;

            connection = new WebSocket(endpointUrl);
            connection.WaitTime = options.LongPollerTimeout;
            connection.OnOpen += OnConnectionOpened;
            connection.OnMessage += OnConnectionMessage;
            connection.OnError += OnConnectionError;
            connection.OnClose += OnConnectionClosed;
            connection.Connect();
        }

        /// <summary>
        /// Disconnects from the socket server.
        /// </summary>
        /// <param name="code"></param>
        /// <param name="reason"></param>
        public void Disconnect(CloseStatusCode code = CloseStatusCode.Normal, string reason = "")
        {
            if (connection != null)
            {
                connection.OnClose -= OnConnectionClosed;
                connection.Close(code, reason);
                connection = null;
            }
        }

        /// <summary>
        /// Pushes formatted data to the socket server.
        ///
        /// If the connection is not alive, the data will be placed into a buffer to be sent when reconnected.
        /// </summary>
        /// <param name="data"></param>
        public void Push(SocketMessage data)
        {
            options.Logger("push", $"{data.Topic} {data.Event} ({data.Ref})", data.Payload);

            var task = new Task(() => options.Encode(data, data => connection.Send(data)));

            if (connection.IsAlive)
            {
                task.Start();
            }
            else
            {
                buffer.Add(task);
            }
        }

        /// <summary>
        /// Maintains a heartbeat connection with the socket server to prevent disconnection.
        /// </summary>
        private void SendHeartbeat()
        {
            if (!connection.IsAlive) return;
            if (hasPendingHeartbeat)
            {
                hasPendingHeartbeat = false;
                options.Logger("transport", "heartbeat timeout. Attempting to re-establish connection.", null);
                connection.Close(CloseStatusCode.Normal, "heartbeat timeout");
                return;
            }
            pendingHeartbeatRef = MakeMsgRef();

            Push(new SocketMessage { Topic = "pheonix", Event = "heartbeat", Ref = pendingHeartbeatRef.ToString() });
        }

        /// <summary>
        /// Called when the socket opens, registers the heartbeat thread and cancels the reconnection timer.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnConnectionOpened(object sender, EventArgs args)
        {
            options.Logger("transport", $"connected to ${endpointUrl}", null);

            FlushBuffer();

            if (reconnectTokenSource != null)
                reconnectTokenSource.Cancel();

            if (heartbeatTokenSource != null)
                heartbeatTokenSource.Cancel();

            heartbeatTokenSource = new CancellationTokenSource();
            heartbeatTask = Task.Run(async () =>
            {
                while (!heartbeatTokenSource.IsCancellationRequested)
                {
                    SendHeartbeat();
                    await Task.Delay(options.HeartbeatInterval, heartbeatTokenSource.Token);
                }
            }, heartbeatTokenSource.Token);


            StateChanged?.Invoke(sender, new SocketStateChangedEventArgs(ConnectionState.Open, args));
        }

        /// <summary>
        /// Parses a recieved socket message into a non-generic type.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnConnectionMessage(object sender, MessageEventArgs args)
        {
            options.Decode(args.Data, decoded =>
            {
                options.Logger("receive", $"{decoded.Payload} {decoded.Topic} {decoded.Event} ({decoded.Ref})", decoded.Payload);
                OnMessage?.Invoke(sender, new SocketMessageEventArgs(decoded));
            });

            StateChanged?.Invoke(sender, new SocketStateChangedEventArgs(ConnectionState.Message, args));
        }

        private void OnConnectionError(object sender, ErrorEventArgs args)
        {
            StateChanged?.Invoke(sender, new SocketStateChangedEventArgs(ConnectionState.Error, args));
        }

        /// <summary>
        /// Begins the reconnection thread with a progressively increasing interval.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnConnectionClosed(object sender, CloseEventArgs args)
        {
            options.Logger("transport", "close", args);

            if (reconnectTokenSource != null)
                reconnectTokenSource.Cancel();

            reconnectTokenSource = new CancellationTokenSource();
            reconnectTask = Task.Run(async () =>
            {
                var tries = 1;
                while (!reconnectTokenSource.IsCancellationRequested)
                {
                    Disconnect();
                    Connect();
                    await Task.Delay(options.ReconnectAfterInterval(tries++), reconnectTokenSource.Token);
                }
            }, reconnectTokenSource.Token);

            StateChanged?.Invoke(sender, new SocketStateChangedEventArgs(ConnectionState.Close, args));
        }

        /// <summary>
        /// Generates an incrementing identifier for message references - this reference is used
        /// to coordinate requests with their responses.
        /// </summary>
        /// <returns></returns>
        internal string MakeMsgRef() => reference + 1 == reference ? 0.ToString() : (reference + 1).ToString();
        internal string ReplyEventName(string msgRef) => $"chan_reply_{msgRef}";

        /// <summary>
        /// Flushes `Push` requests added while a socket was disconnected.
        /// </summary>
        private void FlushBuffer()
        {
            foreach (var item in buffer)
            {
                item.Start();
            }
            buffer.Clear();
        }
    }

    public class SocketOptionsParameters
    {
        [JsonProperty("apikey")]
        public string ApiKey { get; set; }
    }

    /// <summary>
    /// Representation of a Socket Request.
    /// </summary>
    public class SocketMessage
    {
        [JsonProperty("topic")]
        public string Topic { get; set; }

        [JsonProperty("event")]
        public string Event { get; set; }

        [JsonProperty("payload")]
        public object Payload { get; set; }

        [JsonProperty("ref")]
        public string Ref { get; set; }
    }

    public class SocketStateChangedEventArgs : EventArgs
    {
        public enum ConnectionState
        {
            Open,
            Close,
            Error,
            Message
        }

        public ConnectionState State { get; set; }
        public EventArgs Args { get; set; }

        public SocketStateChangedEventArgs(ConnectionState state, EventArgs args)
        {
            State = state;
            Args = args;
        }
    }

    public class SocketMessageEventArgs : EventArgs
    {
        public SocketMessage Message { get; private set; }

        public SocketMessageEventArgs(SocketMessage message)
        {
            Message = message;
        }
    }
}
