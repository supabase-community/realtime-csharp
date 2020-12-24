using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Postgrest.Attributes;
using Postgrest.Models;
using static Supabase.Realtime.Constants;

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

        public ClientOptions Options { get; private set; }

        private string realtimeUrl;
        private ClientAuthorization authorization;

        private static Client instance;
        public static Client Instance
        {
            get
            {
                if (instance == null)
                    instance = new Client();

                return instance;
            }
        }

        public EventHandler<SocketStateChangedEventArgs> OnOpen;
        public EventHandler<SocketStateChangedEventArgs> OnClose;
        public EventHandler<SocketStateChangedEventArgs> OnError;
        public EventHandler<SocketStateChangedEventArgs> OnMessage;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="realtimeUrl">The connection url (ex: "ws://localhost:4000/socket" - no trailing slash required)</param>
        /// <param name="authorization"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public Client Initialize(string realtimeUrl, ClientAuthorization authorization = null, ClientOptions options = null)
        {
            this.realtimeUrl = realtimeUrl;

            if (authorization == null)
            {
                authorization = new ClientAuthorization();
            }
            this.authorization = authorization;

            if (options == null)
            {
                options = new ClientOptions();
            }
            this.Options = options;
            this.subscriptions = new Dictionary<string, Channel>();

            return this;
        }

        public Client Connect()
        {
            if (Socket != null)
            {
                Debug.WriteLine("Socket already exists.");
                return this;
            }

            Socket = new Socket(realtimeUrl, Options);
            Socket.StateChanged += HandleSocketStateChanged;
            Socket.Connect();

            return this;
        }

        public Client Disconnect(WebSocketSharp.CloseStatusCode code = WebSocketSharp.CloseStatusCode.Normal, string reason = "Programmatic Disconnect")
        {
            try
            {
                if (Socket != null)
                {
                    Socket.StateChanged -= HandleSocketStateChanged;
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

        private void HandleSocketStateChanged(object sender, SocketStateChangedEventArgs args)
        {
            Debug.WriteLine($"STATE CHANGED: {args.State}");
            switch (args.State)
            {
                case SocketStateChangedEventArgs.ConnectionState.Open:
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

        public Channel Channel(string database = "realtime", string schema = null, string table = null, string col = null, string value = null)
        {
            var key = Utils.GenerateChannelTopic(database, schema, table, col, value);

            if (subscriptions.ContainsKey(key))
            {
                return subscriptions[key] as Channel;
            }

            var subscription = new Channel(database, schema, table, col, value);
            subscriptions.Add(key, subscription);

            return subscription;
        }

        public void Remove(Channel channel)
        {
            if (subscriptions.ContainsKey(channel.Topic))
            {
                subscriptions.Remove(channel.Topic);
            }
        }
    }
}
