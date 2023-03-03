using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Supabase.Realtime.Broadcast;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Models;
using Supabase.Realtime.PostgresChanges;
using Supabase.Realtime.Presence;
using Supabase.Realtime.Socket;

namespace Supabase.Realtime
{
	/// <summary>
	/// Singleton that represents a Client connection to a Realtime Server.
	///
	/// It maintains a singular Websocket with asynchronous listeners (RealtimeChannels).
	/// </summary>
	/// <example>
	///     client = Client.Instance
	/// </example>
	public class Client : IRealtimeClient<RealtimeSocket, RealtimeChannel>
	{
		/// <summary>
		/// Contains all Realtime RealtimeChannel Subscriptions - state managed internally.
		///
		/// Keys are of encoded value: `{database}{:schema?}{:table?}{:col.eq.:value?}`
		/// Values are of type `RealtimeChannel<T> where T : BaseModel, new()`;
		/// </summary>
		private Dictionary<string, RealtimeChannel> subscriptions { get; set; }

		/// <summary>
		/// Exposes all Realtime RealtimeChannel Subscriptions for R/O public consumption 
		/// </summary>
		public ReadOnlyDictionary<string, RealtimeChannel> Subscriptions => new ReadOnlyDictionary<string, RealtimeChannel>(subscriptions);

		/// <summary>
		/// The backing Socket class.
		///
		/// Most methods of the Client act as proxies to the Socket class.
		/// </summary>
		public IRealtimeSocket? Socket { get => socket; }
		private IRealtimeSocket? socket;

		/// <summary>
		/// Client Options - most of which are regarding Socket connection Options
		/// </summary>
		public ClientOptions Options { get; private set; }

		/// <summary>
		/// Invoked when the socket raises the `open` event.
		/// </summary>
		public event EventHandler<SocketStateChangedEventArgs>? OnOpen;

		/// <summary>
		/// Invoked when the socket raises the `close` event.
		/// </summary>
		public event EventHandler<SocketStateChangedEventArgs>? OnClose;

		/// <summary>
		/// Invoked when the socket raises the `reconnected` event.
		/// </summary>
		public event EventHandler<SocketStateChangedEventArgs>? OnReconnect;

		/// <summary>
		/// Invoked when the socket raises the `error` event.
		/// </summary>
		public event EventHandler<SocketStateChangedEventArgs>? OnError;

		/// <summary>
		/// Invoked when the socket raises the `message` event.
		/// </summary>
		public event EventHandler<SocketStateChangedEventArgs>? OnMessage;

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
					},
					MissingMemberHandling = MissingMemberHandling.Ignore
				};
			}
		}

		private string realtimeUrl;

		/// <summary>
		/// JWT Access token for WALRUS security
		/// </summary>
		internal string? AccessToken { get => accessToken; }
		private string? accessToken;

		/// <summary>
		/// Initializes a Client instance, this method should be called prior to any other method.
		/// </summary>
		/// <param name="realtimeUrl">The connection url (ex: "ws://localhost:4000/socket" - no trailing slash required)</param>
		/// <param name="options"></param>
		/// <returns>Client</returns>
		public Client(string realtimeUrl, ClientOptions? options = null)
		{
			this.realtimeUrl = realtimeUrl;

			if (options == null)
				options = new ClientOptions();

			if (options.Encode == null)
				options.Encode = (payload, callback) => callback(JsonConvert.SerializeObject(payload, SerializerSettings));

			if (options.Decode == null)
			{
				options.Decode = (payload, callback) =>
				{
					var response = new SocketResponse(SerializerSettings);
					JsonConvert.PopulateObject(payload, response, SerializerSettings);
					callback(response);
				};
			}

			Options = options;
			subscriptions = new Dictionary<string, RealtimeChannel>();
		}

		/// <summary>
		/// Attempts to connect to the socket given the params specified in `Initialize`
		///
		/// Returns when socket has successfully connected.
		/// </summary>
		/// <returns></returns>
		public Task<IRealtimeClient<RealtimeSocket, RealtimeChannel>> ConnectAsync()
		{
			var tsc = new TaskCompletionSource<IRealtimeClient<RealtimeSocket, RealtimeChannel>>();

			try
			{
				Connect(tsc.SetResult);
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
		public IRealtimeClient<RealtimeSocket, RealtimeChannel> Connect(Action<IRealtimeClient<RealtimeSocket, RealtimeChannel>>? callback = null)
		{
			if (socket != null)
			{
				Options.Logger("error", "Socket already exists.", null);
				return this;
			}

			EventHandler<SocketStateChangedEventArgs>? cb = null;

			cb = (object sender, SocketStateChangedEventArgs args) =>
			{
				switch (args.State)
				{
					case SocketStateChangedEventArgs.ConnectionState.Open:
						socket!.StateChanged -= cb;
						callback?.Invoke(this);
						break;
					case SocketStateChangedEventArgs.ConnectionState.Close:
					case SocketStateChangedEventArgs.ConnectionState.Error:
						socket!.StateChanged -= cb;
						throw new Exception("Error occurred connecting to Socket. Check logs.");
				}
			};

			socket = new RealtimeSocket(realtimeUrl, Options, SerializerSettings);

			socket.StateChanged += HandleSocketStateChanged;
			socket.OnMessage += HandleSocketMessage;
			socket.OnHeartbeat += HandleSocketHeartbeat;

			socket.StateChanged += cb;
			socket.Connect();

			return this;
		}

		/// <summary>
		/// Sets the current Access Token every heartbeat (see: https://github.com/supabase/realtime-js/blob/59bd47956ebe4e23b3e1a6c07f5fe2cfe943e8ad/src/RealtimeClient.ts#L437)
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void HandleSocketHeartbeat(object sender, SocketResponseEventArgs e)
		{
			if (!string.IsNullOrEmpty(accessToken))
				SetAuth(accessToken!);
		}

		/// <summary>
		/// Disconnects from the socket server (if connected).
		/// </summary>
		/// <param name="code">Status Code</param>
		/// <param name="reason">Reason for disconnect</param>
		/// <returns></returns>
		public IRealtimeClient<RealtimeSocket, RealtimeChannel> Disconnect(WebSocketCloseStatus code = WebSocketCloseStatus.NormalClosure, string reason = "Programmatic Disconnect")
		{
			if (socket != null)
			{
				socket.StateChanged -= HandleSocketStateChanged;
				socket.OnMessage -= HandleSocketMessage;
				socket.Disconnect(code, reason);
				socket = null;
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
			accessToken = jwt;

			try
			{
				foreach (var channel in subscriptions.Values)
				{
					// See: https://github.com/supabase/realtime-js/pull/126
					channel.Options.Parameters!["user_token"] = accessToken;

					if (channel.HasJoinedOnce && channel.IsJoined)
					{
						channel.Push(Constants.CHANNEL_ACCESS_TOKEN, payload: new Dictionary<string, string>
						{
							{ "access_token", accessToken }
						});
					}
				}
			}
			catch (Exception ex)
			{
				Options.Logger("exception", "Error in SetAuth()", ex);
			}
		}

		/// <summary>
		/// Adds a RealtimeChannel subscription - if a subscription exists with the same signature, the existing subscription will be returned.
		/// </summary>
		/// <param name="channelName">The name of the Channel to join (totally arbitrary)</param>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		public RealtimeChannel Channel(string channelName)
		{
			var topic = $"realtime:{channelName}";

			if (subscriptions.ContainsKey(topic))
				return subscriptions[topic];

			if (socket == null)
				throw new Exception("Socket must exist, was `Connect` called?");

			var subscription = new RealtimeChannel(socket!, topic, new ChannelOptions(Options, () => AccessToken, SerializerSettings));
			subscriptions.Add(topic, subscription);

			return subscription;
		}

		/// <summary>
		/// Adds a RealtimeChannel subscription - if a subscription exists with the same signature, the existing subscription will be returned.
		/// </summary>
		/// <param name="database">Database to connect to, with Supabase this will likely be `realtime`.</param>
		/// <param name="schema">Postgres schema, for example, `public`</param>
		/// <param name="table">Postgres table name</param>
		/// <param name="column">Postgres column name</param>
		/// <param name="value">Value the specified column should have</param>
		/// <returns></returns>
		public RealtimeChannel Channel(string database = "realtime", string schema = "public", string? table = null, string? column = null, string? value = null, Dictionary<string, string>? parameters = null)
		{
			var key = Utils.GenerateChannelTopic(database, schema, table, column, value);

			if (subscriptions.ContainsKey(key))
				return subscriptions[key];

			if (socket == null)
				throw new Exception("Socket must exist, was `Connect` called?");

			var changesOptions = new PostgresChangesOptions(schema, table, filter: column != null && value != null ? $"{column}=eq.{value}" : null, parameters: parameters);
			var options = new ChannelOptions(Options, () => AccessToken, SerializerSettings);

			var subscription = new RealtimeChannel(socket!, key, options);
			subscription.Register(changesOptions);

			subscriptions.Add(key, subscription);

			return subscription;
		}

		/// <summary>
		/// Removes a channel subscription.
		/// </summary>
		/// <param name="channel"></param>
		public void Remove(RealtimeChannel channel)
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
			if (args.Response.Topic != null && subscriptions.ContainsKey(args.Response.Topic))
			{
				subscriptions[args.Response.Topic].HandleSocketMessage(args);
			}
		}

		private void HandleSocketStateChanged(object sender, SocketStateChangedEventArgs args)
		{
			if (args.State != SocketStateChangedEventArgs.ConnectionState.Message)
				Options.Logger("socket", "state changed", args.State.ToString().ToLower());

			switch (args.State)
			{
				case SocketStateChangedEventArgs.ConnectionState.Open:
					// Ref: https://github.com/supabase/realtime-js/pull/116/files
					if (!string.IsNullOrEmpty(AccessToken))
						SetAuth(AccessToken!);

					OnOpen?.Invoke(this, args);
					break;
				case SocketStateChangedEventArgs.ConnectionState.Reconnected:
					// Ref: https://github.com/supabase/realtime-js/pull/116/files
					if (!string.IsNullOrEmpty(AccessToken))
						SetAuth(AccessToken!);

					OnReconnect?.Invoke(this, args);
					break;
				case SocketStateChangedEventArgs.ConnectionState.Message:
					OnMessage?.Invoke(this, args);
					break;
				case SocketStateChangedEventArgs.ConnectionState.Close:
					OnClose?.Invoke(this, args);
					break;
				case SocketStateChangedEventArgs.ConnectionState.Error:
					OnError?.Invoke(this, args);
					break;
			}
		}
	}
}
