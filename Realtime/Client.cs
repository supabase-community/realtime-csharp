using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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
        /// Exposes all Realtime Channel Subscriptions for R/O public consumption 
        /// </summary>
        public ReadOnlyDictionary<string, Channel> Subscriptions => new ReadOnlyDictionary<string, Channel>(subscriptions);

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

        /// <summary>
        /// Custom Serializer resolvers and converters that will be used for encoding and decoding Postgrest JSON responses.
        ///
        /// By default, Postgrest seems to use a date format that C# and Newtonsoft do not like, so this initial
        /// configuration handles that.
        /// </summary>
        public JsonSerializerSettings SerializerSettings
        {
            get
            {
                if (Options == null)
                    Options = new ClientOptions();

                return new JsonSerializerSettings
                {
                    ContractResolver = new CustomContractResolver(),
                    Converters =
                    {
                        // 2020-08-28T12:01:54.763231
                        new IsoDateTimeConverter
                        {
                            DateTimeStyles = Options.DateTimeStyles,
                            DateTimeFormat = Options.DateTimeFormat
                        }
                    }
                };
            }
        }

        private string realtimeUrl;

        /// <summary>
        /// JWT Access token for WALRUS security
        /// </summary>
        internal string AccessToken { get; private set; }

        /// <summary>
        /// Initializes a Client instance, this method should be called prior to any other method.
        /// </summary>
        /// <param name="realtimeUrl">The connection url (ex: "ws://localhost:4000/socket" - no trailing slash required)</param>
        /// <param name="options"></param>
        /// <returns>Client</returns>
        public static Client Initialize(string realtimeUrl, ClientOptions options = null)
        {
            if (options == null)
            {
                options = new ClientOptions();
            }

            instance = new Client
            {
                Options = options,
                realtimeUrl = realtimeUrl,
                subscriptions = new Dictionary<string, Channel>()
            };

            return instance;
        }

        /// <summary>
        /// Attempts to connect to the socket given the params specified in `Initialize`
        ///
        /// Returns when socket has successfully connected.
        /// </summary>
        /// <returns></returns>
        public Task<Client> ConnectAsync()
        {
            var tsc = new TaskCompletionSource<Client>();

            try
            {
                if (Socket != null)
                {
                    Debug.WriteLine("Socket already exists.");

                    tsc.TrySetResult(this);

                    return tsc.Task;
                }

                EventHandler<SocketStateChangedEventArgs> callback = null;
                callback = (object sender, SocketStateChangedEventArgs args) =>
                {
                    switch (args.State)
                    {
                        case SocketStateChangedEventArgs.ConnectionState.Open:
                            Socket.StateChanged -= callback;
                            tsc.TrySetResult(this);
                            break;
                        case SocketStateChangedEventArgs.ConnectionState.Close:
                        case SocketStateChangedEventArgs.ConnectionState.Error:
                            Socket.StateChanged -= callback;
                            tsc.TrySetException(new Exception("Error occurred connecting to Socket. Check logs."));
                            break;
                    }
                };

                Socket = new Socket(realtimeUrl, Options);

                Socket.StateChanged += HandleSocketStateChanged;
                Socket.OnMessage += HandleSocketMessage;

                Socket.StateChanged += callback;
                Socket.Connect();
            }
            catch (Exception ex)
            {
                tsc.TrySetException(ex);
            }

            return tsc.Task;
        }

        /// <summary>
        /// Attempts to connect to the socket given the params specified in `Initialize`
        ///
        /// Provides a callback for `Task` driven returns.
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        public Client Connect(Action<Client> callback = null)
        {
            if (Socket != null)
            {
                Debug.WriteLine("Socket already exists.");
                return this;
            }

            EventHandler<SocketStateChangedEventArgs> cb = null;
            cb = (object sender, SocketStateChangedEventArgs args) =>
            {
                switch (args.State)
                {
                    case SocketStateChangedEventArgs.ConnectionState.Open:
                        Socket.StateChanged -= cb;
                        callback?.Invoke(this);
                        break;
                    case SocketStateChangedEventArgs.ConnectionState.Close:
                    case SocketStateChangedEventArgs.ConnectionState.Error:
                        Socket.StateChanged -= cb;
                        throw new Exception("Error occurred connecting to Socket. Check logs.");
                }
            };

            Socket = new Socket(realtimeUrl, Options);

            Socket.StateChanged += HandleSocketStateChanged;
            Socket.OnMessage += HandleSocketMessage;

            Socket.StateChanged += cb;
            Socket.Connect();

            return this;
        }

        /// <summary>
        /// Disconnects from the socket server (if connected).
        /// </summary>
        /// <param name="code">Status Code</param>
        /// <param name="reason">Reason for disconnect</param>
        /// <returns></returns>
        public Client Disconnect(WebSocketCloseStatus code = WebSocketCloseStatus.NormalClosure, string reason = "Programmatic Disconnect")
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

        /// <summary>
        /// Sets the JWT access token used for channel subscription authorization and Realtime RLS.
        /// Ref: https://github.com/supabase/realtime-js/pull/117 | https://github.com/supabase/realtime-js/pull/117
        /// </summary>
        /// <param name="jwt"></param>
        public void SetAuth(string jwt)
        {
            AccessToken = jwt;

            try
            {
                foreach (var channel in subscriptions.Values)
                {
                    // See: https://github.com/supabase/realtime-js/pull/126
                    channel.Parameters["user_token"] = AccessToken;

                    if (channel.HasJoinedOnce && channel.IsJoined)
                    {
                        channel.Push(Constants.CHANNEL_ACCESS_TOKEN, new Dictionary<string, string>
                        {
                            { "access_token", AccessToken }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
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
        public Channel Channel(string database = "realtime", string schema = null, string table = null, string column = null, string value = null, Dictionary<string, string> parameters = null)
        {
            var key = Utils.GenerateChannelTopic(database, schema, table, column, value);

            if (subscriptions.ContainsKey(key))
            {
                return subscriptions[key];
            }

            var subscription = new Channel(database, schema, table, column, value, parameters);
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
            Options.Logger("socket", "state changed", args.State);
            switch (args.State)
            {
                case SocketStateChangedEventArgs.ConnectionState.Open:
                    // Ref: https://github.com/supabase/realtime-js/pull/116/files
                    SetAuth(AccessToken);

                    OnOpen?.Invoke(this, args);
                    break;
                case SocketStateChangedEventArgs.ConnectionState.Close:
                    OnClose?.Invoke(this, args);
                    break;
                case SocketStateChangedEventArgs.ConnectionState.Error:
                    OnError?.Invoke(this, args);
                    break;
                case SocketStateChangedEventArgs.ConnectionState.Message:
                    OnMessage?.Invoke(this, args);
                    break;
            }
        }
    }
}
