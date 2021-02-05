using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using WebSocketSharp;

namespace Supabase.Realtime
{
    /// <summary>
    /// Singleton that represents a Client connection to a Realtime Server.
    ///
    /// It maintains a singular Websocket with asynchronous listeners (Channels).
    /// </summary>
    /// <example>
    ///     client = Client.Instance
    /// </example>
    public class Client
    {
        /// <summary>
        /// Contains all Realtime Channel Subscriptions - state managed internally.
        ///
        /// Keys are of encoded value: `{database}{:schema?}{:table?}{:col.eq.:value?}`
        /// Values are of type `Channel<T> where T : BaseModel, new()`;
        /// </summary>
        private Dictionary<string, Channel> subscriptions { get; set; }

        /// <summary>
        /// The backing Socket class.
        ///
        /// Most methods of the Client act as proxies to the Socket class.
        /// </summary>
        public Socket Socket { get; private set; }

        /// <summary>
        /// Client Options - most of which are regarding Socket connection options
        /// </summary>
        public ClientOptions Options { get; private set; }

        private static Client instance;
        /// <summary>
        /// The Client Instance (singleton)
        /// </summary>
        public static Client Instance
        {
            get
            {
                if (instance == null)
                    instance = new Client();

                return instance;
            }
        }

        /// <summary>
        /// Invoked when the socket raises the `open` event.
        /// </summary>
        public event EventHandler<SocketStateChangedEventArgs> OnOpen;

        /// <summary>
        /// Invoked when the socket raises the `close` event.
        /// </summary>
        public event EventHandler<SocketStateChangedEventArgs> OnClose;

        /// <summary>
        /// Invoked when the socket raises the `error` event.
        /// </summary>
        public event EventHandler<SocketStateChangedEventArgs> OnError;

        /// <summary>
        /// Invoked when the socket raises the `message` event.
        /// </summary>
        public event EventHandler<SocketStateChangedEventArgs> OnMessage;

        private string realtimeUrl;

        /// <summary>
        /// Initializes a Client instance, this method should be called prior to any other method.
        /// </summary>
        /// <param name="realtimeUrl">The connection url (ex: "ws://localhost:4000/socket" - no trailing slash required)</param>
        /// <param name="options"></param>
        /// <returns>Client</returns>
        public static Client Initialize(string realtimeUrl, ClientOptions options = null)
        {
            instance = new Client();
            instance.realtimeUrl = realtimeUrl;

            if (options == null)
            {
                options = new ClientOptions();
            }

            instance.Options = options;
            instance.subscriptions = new Dictionary<string, Channel>();

            return instance;
        }

        /// <summary>
        /// Attempts to connect to the socket given the params specified in `Initialize`
        /// </summary>
        /// <returns></returns>
        public Task<Client> Connect()
        {
            var tsc = new TaskCompletionSource<Client>();

            try
            {
                if (Socket != null)
                {
                    Debug.WriteLine("Socket already exists.");
                    tsc.SetResult(this);
                }

                Socket = new Socket(realtimeUrl, Options);
                Socket.StateChanged += HandleSocketStateChanged;
                Socket.OnMessage += HandleSocketMessage;

                EventHandler<SocketStateChangedEventArgs> callback = null;
                callback = (object sender, SocketStateChangedEventArgs args) =>
                {
                    switch (args.State)
                    {
                        case SocketStateChangedEventArgs.ConnectionState.Open:
                            Socket.StateChanged -= callback;
                            tsc.SetResult(this);
                            break;
                        case SocketStateChangedEventArgs.ConnectionState.Close:
                        case SocketStateChangedEventArgs.ConnectionState.Error:
                            Socket.StateChanged -= callback;
                            tsc.SetException(new Exception("Error occurred connecting to Socket. Check logs."));
                            break;
                    }
                };
                Socket.StateChanged += callback;
                Socket.Connect();
            }
            catch (Exception ex)
            {
                tsc.SetException(ex);
            }

            return tsc.Task;
        }

        /// <summary>
        /// Disconnects from the socket server (if connected).
        /// </summary>
        /// <param name="code">Status Code</param>
        /// <param name="reason">Reason for disconnect</param>
        /// <returns></returns>
        public Client Disconnect(CloseStatusCode code = WebSocketSharp.CloseStatusCode.Normal, string reason = "Programmatic Disconnect")
        {
            try
            {
                if (Socket != null)
                {
                    Socket.StateChanged -= HandleSocketStateChanged;
                    Socket.OnMessage -= HandleSocketMessage;
                    Socket.Disconnect(code, reason);
                    Socket = null;
                }
                return this;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to disconnect socket.");
                throw ex;
            }
        }

        /// <summary>
        /// Adds a Channel subscription - if a subscription exists with the same signature, the existing subscription will be returned.
        /// </summary>
        /// <param name="database">Database to connect to, with Supabase this will likely be `realtime`.</param>
        /// <param name="schema">Postgres schema, for example, `public`</param>
        /// <param name="table">Postgres table name</param>
        /// <param name="column">Postgres column name</param>
        /// <param name="value">Value the specified column should have</param>
        /// <returns></returns>
        public Channel Channel(string database = "realtime", string schema = null, string table = null, string column = null, string value = null)
        {
            var key = Utils.GenerateChannelTopic(database, schema, table, column, value);

            if (subscriptions.ContainsKey(key))
            {
                return subscriptions[key] as Channel;
            }

            var subscription = new Channel(database, schema, table, column, value);
            subscriptions.Add(key, subscription);

            return subscription;
        }

        /// <summary>
        /// Removes a channel subscription.
        /// </summary>
        /// <param name="channel"></param>
        public void Remove(Channel channel)
        {
            if (subscriptions.ContainsKey(channel.Topic))
            {
                if (channel.IsJoined)
                    channel.Unsubscribe();

                subscriptions.Remove(channel.Topic);
            }
        }

        private void HandleSocketMessage(object sender, SocketResponseEventArgs args)
        {
            if (subscriptions.ContainsKey(args.Response.Topic))
            {
                subscriptions[args.Response.Topic].HandleSocketMessage(args);
            }
        }

        private void HandleSocketStateChanged(object sender, SocketStateChangedEventArgs args)
        {
            Debug.WriteLine($"STATE CHANGED: {args.State}");
            switch (args.State)
            {
                case SocketStateChangedEventArgs.ConnectionState.Open:
                    OnOpen?.Invoke(this, args);
                    break;
                case SocketStateChangedEventArgs.ConnectionState.Close:
                    HandleSocketClosed(args);
                    break;
                case SocketStateChangedEventArgs.ConnectionState.Error:
                    HandleSocketError(args);
                    break;
                case SocketStateChangedEventArgs.ConnectionState.Message:
                    OnMessage?.Invoke(this, args);
                    break;
            }
        }

        private void HandleSocketClosed(SocketStateChangedEventArgs args)
        {
            OnClose?.Invoke(this, null);

            foreach (var kvp in subscriptions)
                kvp.Value.TriggerChannelClosed(args);
        }

        private void HandleSocketError(SocketStateChangedEventArgs args)
        {
            OnError?.Invoke(this, null);
            foreach (var kvp in subscriptions)
                kvp.Value.TriggerChannelErrored(args);
        }
    }
}
