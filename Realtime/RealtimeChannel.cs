using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json;
using Supabase.Realtime.Broadcast;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Models;
using Supabase.Realtime.PostgresChanges;
using Supabase.Realtime.Presence;
using Supabase.Realtime.Socket;
using Supabase.Realtime.Socket.Responses;
using static Supabase.Realtime.Constants;
using Timer = System.Timers.Timer;

[assembly: InternalsVisibleTo("RealtimeTests")]
namespace Supabase.Realtime
{
	/// <summary>
	/// Class representation of a channel subscription
	/// </summary>
	public class RealtimeChannel : IRealtimeChannel
	{
		/// <summary>
		/// Invoked when the `INSERT` event is raised.
		/// </summary>
		public event EventHandler<PostgresChangesEventArgs>? OnInsert;

		/// <summary>
		/// Invoked when the `UPDATE` event is raised.
		/// </summary>
		public event EventHandler<PostgresChangesEventArgs>? OnUpdate;

		/// <summary>
		/// Invoked when the `DELETE` event is raised.
		/// </summary>
		public event EventHandler<PostgresChangesEventArgs>? OnDelete;

		/// <summary>
		/// Invoked when an Postgres Change event is raised.
		/// </summary>
		public event EventHandler<PostgresChangesEventArgs>? OnPostgresChange;

		/// <summary>
		/// Invoked anytime a message is decoded within this topic.
		/// </summary>
		public event EventHandler<SocketResponseEventArgs>? OnMessage;

		/// <summary>
		/// Invoked when this channel listener is closed
		/// </summary>
		public event EventHandler<ChannelStateChangedEventArgs>? StateChanged;

		/// <summary>
		/// Invoked when the socket drops or crashes.
		/// </summary>
		public event EventHandler<ChannelStateChangedEventArgs>? OnError;

		/// <summary>
		/// Invoked when the channel is explicitly closed by the client.
		/// </summary>
		public event EventHandler<ChannelStateChangedEventArgs>? OnClose;

		public bool IsClosed => State == ChannelState.Closed;
		public bool IsErrored => State == ChannelState.Errored;
		public bool IsJoined => State == ChannelState.Joined;
		public bool IsJoining => State == ChannelState.Joining;
		public bool IsLeaving => State == ChannelState.Leaving;

		/// <summary>
		/// The channel's topic (identifier)
		/// </summary>
		public string Topic { get; private set; }

		/// <summary>
		/// The Channel's current state.
		/// </summary>
		public ChannelState State { get; private set; } = ChannelState.Closed;

		/// <summary>
		/// Options passed to this channel instance.
		/// </summary>
		public Channel.ChannelOptions Options { get; private set; }

		/// <summary>
		/// The saved Broadcast Options, set in <see cref="Register{TBroadcastResponse}(BroadcastOptions)"/>
		/// </summary>
		public BroadcastOptions? BroadcastOptions { get; protected set; } = new BroadcastOptions(false, false);

		/// <summary>
		/// The saved Presence Options, set in <see cref="Register{TPresenceResponse}(PresenceOptions)"/>
		/// </summary>
		public PresenceOptions? PresenceOptions { get; protected set; } = new PresenceOptions(string.Empty);

		/// <summary>
		/// The saved Postgres Changes Options, set in <see cref="Register(PostgresChanges.PostgresChangesOptions)"/>
		/// </summary>
		public List<PostgresChangesOptions> PostgresChangesOptions { get; private set; } = new List<PostgresChangesOptions>();

		/// <summary>
		/// Flag stating whether a channel has been joined once or not.
		/// </summary>
		public bool HasJoinedOnce { get; private set; }

		/// <summary>
		/// Flag stating if a channel is currently subscribed.
		/// </summary>
		public bool IsSubscribed = false;

		/// <summary>
		/// Returns the <see cref="IRealtimeBroadcast"/> instance.
		/// </summary>
		/// <returns></returns>
		public IRealtimeBroadcast? Broadcast() => broadcast;

		/// <summary>
		/// Returns a typed <see cref="RealtimeBroadcast{TBroadcastModel}" /> instance.
		/// </summary>
		/// <typeparam name="TBroadcastModel"></typeparam>
		/// <returns></returns>
		public RealtimeBroadcast<TBroadcastModel>? Broadcast<TBroadcastModel>() where TBroadcastModel : BaseBroadcast => broadcast != null ? (RealtimeBroadcast<TBroadcastModel>)broadcast : default;

		/// <summary>
		/// Returns the <see cref="IRealtimePresence"/> instance.
		/// </summary>
		/// <returns></returns>
		public IRealtimePresence? Presence() => presence;

		/// <summary>
		/// Returns a typed <see cref="RealtimePresence{T}"/> instance.
		/// </summary>
		/// <typeparam name="TPresenceModel">Model representing a Presence payload</typeparam>
		/// <returns></returns>
		public RealtimePresence<TPresenceModel>? Presence<TPresenceModel>() where TPresenceModel : BasePresence => presence != null ? (RealtimePresence<TPresenceModel>)presence : default;

		/// <summary>
		/// The initial request to join a channel (repeated on channel disconnect)
		/// </summary>
		internal Push? JoinPush;
		internal Push? LastPush;

		// Event handlers that pass events to typed instances for broadcast and presence.
		internal event EventHandler<SocketResponseEventArgs>? OnBroadcast;
		internal event EventHandler<SocketResponseEventArgs>? OnPresenceDiff;
		internal event EventHandler<SocketResponseEventArgs>? OnPresenceSync;

		/// <summary>
		/// Buffer of Pushes held because of Socket availablity
		/// </summary>
		internal List<Push> buffer = new List<Push>();

		private IRealtimeSocket socket;
		private IRealtimePresence? presence;
		private IRealtimeBroadcast? broadcast;
		private bool canPush => IsJoined && socket.IsConnected;
		private bool hasJoinedOnce = false;
		private Timer rejoinTimer;
		private bool isRejoining = false;

		/// <summary>
		/// Initializes a Channel - must call `Subscribe()` to receive events.
		/// </summary>
		/// <param name="database"></param>
		/// <param name="schema"></param>
		/// <param name="table"></param>
		/// <param name="col"></param>
		/// <param name="value"></param>
		public RealtimeChannel(IRealtimeSocket socket, string channelName, Channel.ChannelOptions options)
		{
			Topic = channelName;

			this.socket = socket;

			if (options.Parameters == null)
				options.Parameters = new Dictionary<string, string>();

			Options = options;

			socket.StateChanged += (sender, args) =>
			{
				if (args.State == SocketStateChangedEventArgs.ConnectionState.Reconnected && IsSubscribed)
				{
					IsSubscribed = false;
					Rejoin(DEFAULT_TIMEOUT);
				}
			};

			rejoinTimer = new Timer(options.ClientOptions.Timeout.TotalMilliseconds);
			rejoinTimer.Elapsed += HandleRejoinTimerElapsed;
			rejoinTimer.AutoReset = true;
		}

		/// <summary>
		/// Registers a <see cref="RealtimeBroadcast{TBroadcastModel}"/> instance - allowing broadcast responses to be parsed.
		/// </summary>
		/// <typeparam name="TBroadcastResponse"></typeparam>
		/// <param name="broadcastSelf">enables client to receive message it broadcasted</param>
		/// <param name="broadcastAck">instructs server to acknowledge that broadcast message was received</param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		public RealtimeBroadcast<TBroadcastResponse> Register<TBroadcastResponse>(bool broadcastSelf = false, bool broadcastAck = false) where TBroadcastResponse : BaseBroadcast
		{
			if (broadcast != null)
				throw new InvalidOperationException("Register can only be called with broadcast options for a channel once.");

			BroadcastOptions = new BroadcastOptions(broadcastSelf, broadcastAck);

			var instance = new RealtimeBroadcast<TBroadcastResponse>(this, BroadcastOptions, Options.SerializerSettings);
			broadcast = instance;

			OnBroadcast += (sender, args) => broadcast.TriggerReceived(args);

			return instance;
		}

		/// <summary>
		/// Registers a <see cref="RealtimePresence{TPresenceResponse}"/> instance - allowing presence responses to be parsed and state to be tracked.
		/// </summary>
		/// <typeparam name="TPresenceResponse">The model representing a presence payload.</typeparam>
		/// <param name="presenceKey">used to track presence payload across clients</param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException">Thrown if called multiple times.</exception>
		public RealtimePresence<TPresenceResponse> Register<TPresenceResponse>(string presenceKey) where TPresenceResponse : BasePresence
		{
			if (presence != null)
				throw new InvalidOperationException("Register can only be called with presence options for a channel once.");

			PresenceOptions = new PresenceOptions(presenceKey);
			var instance = new RealtimePresence<TPresenceResponse>(this, PresenceOptions, Options.SerializerSettings);
			presence = instance;

			OnPresenceSync += (sender, args) => presence.TriggerSync(args);
			OnPresenceDiff += (sender, args) => presence.TriggerDiff(args);

			return instance;
		}

		/// <summary>
		/// Registers postgres_changes options, can be called multiple times.
		/// </summary>
		/// <param name="postgresChangesOptions"></param>
		/// <returns></returns>
		public IRealtimeChannel Register(PostgresChangesOptions postgresChangesOptions)
		{
			PostgresChangesOptions.Add(postgresChangesOptions);
			return this;
		}

		/// <summary>
		/// Subscribes to the channel given supplied Options/params.
		/// </summary>
		/// <param name="timeoutMs"></param>
		public Task<IRealtimeChannel> Subscribe(int timeoutMs = DEFAULT_TIMEOUT)
		{
			var tsc = new TaskCompletionSource<IRealtimeChannel>();

			if (IsSubscribed)
			{
				return Task.FromResult(this as IRealtimeChannel);
			}

			JoinPush = GenerateJoinPush();
			EventHandler<ChannelStateChangedEventArgs>? channelCallback = null;
			EventHandler? joinPushTimeoutCallback = null;

			channelCallback = (object sender, ChannelStateChangedEventArgs e) =>
			{
				switch (e.State)
				{
					// Success!
					case ChannelState.Joined:
						HasJoinedOnce = true;
						IsSubscribed = true;

						StateChanged -= channelCallback;
						JoinPush.OnTimeout -= joinPushTimeoutCallback;

						// Clear buffer
						foreach (var item in buffer)
							item.Send();
						buffer.Clear();

						tsc.TrySetResult(this);
						break;
					// Failure
					case ChannelState.Closed:
					case ChannelState.Errored:
						IsSubscribed = false;
						StateChanged -= channelCallback;
						JoinPush.OnTimeout -= joinPushTimeoutCallback;

						tsc.TrySetException(new Exception("Error occurred connecting to channel. Check logs."));
						break;
				}
			};

			// Throw an exception if there is a problem receiving a join response
			joinPushTimeoutCallback = (object sender, EventArgs e) =>
			{
				StateChanged -= channelCallback;
				JoinPush.OnTimeout -= joinPushTimeoutCallback;

				tsc.TrySetException(new PushTimeoutException());
			};

			StateChanged += channelCallback;

			// Set a flag to prevent multiple join attempts.
			hasJoinedOnce = true;

			// Init and send join.
			Rejoin(timeoutMs);
			JoinPush.OnTimeout += joinPushTimeoutCallback;

			return tsc.Task;
		}

		/// <summary>
		/// Unsubscribes from the channel.
		/// </summary>
		public IRealtimeChannel Unsubscribe()
		{
			IsSubscribed = false;
			SetState(ChannelState.Leaving);

			var leavePush = new Push(socket, this, CHANNEL_EVENT_LEAVE);
			leavePush.Send();

			TriggerChannelStateEvent(new ChannelStateChangedEventArgs(ChannelState.Closed), false);

			return this;
		}

		/// <summary>
		/// Sends a `Push` request under this channel.
		///
		/// Maintains a buffer in the event push is called prior to the channel being joined.
		/// </summary>
		/// <param name="eventName"></param>
		/// <param name="payload"></param>
		/// <param name="timeoutMs"></param>
		public Push Push(string eventName, string? type = null, object? payload = null, int timeoutMs = DEFAULT_TIMEOUT)
		{
			if (!hasJoinedOnce)
				throw new Exception($"Tried to push '{eventName}' to '{Topic}' before joining. Use `Channel.Subscribe()` before pushing events");

			var push = new Push(socket, this, eventName, type, payload, timeoutMs);
			Enqueue(push);

			return push;
		}

		/// <summary>
		/// Sends an arbitrary payload with a given payload type (<see cref="ChannelEventName"/>)
		/// </summary>
		/// <param name="eventName"></param>
		/// <param name="payload"></param>
		/// <param name="timeoutMs"></param>
		public Task<bool> Send(ChannelEventName eventName, string? type, object payload, int timeoutMs = DEFAULT_TIMEOUT)
		{
			var tsc = new TaskCompletionSource<bool>();

			var ev = Core.Helpers.GetMappedToAttr(eventName).Mapping;
			var push = Push(ev, type, payload, timeoutMs);

			EventHandler<SocketResponseEventArgs>? messageCallback = null;

			messageCallback = (object sender, SocketResponseEventArgs args) =>
			{
				tsc.SetResult(args.Response?.Event != EventType.Unknown);
				push.OnMessage -= messageCallback;
			};

			push.OnMessage += messageCallback;

			return tsc.Task;
		}

		/// <summary>
		/// Rejoins the channel.
		/// </summary>
		/// <param name="timeoutMs"></param>
		public void Rejoin(int timeoutMs = DEFAULT_TIMEOUT)
		{
			if (IsLeaving) return;
			SendJoin(timeoutMs);
		}

		/// <summary>
		/// Enqueues a message.
		/// </summary>
		/// <param name="push"></param>
		private void Enqueue(Push push)
		{
			LastPush = push;

			if (canPush)
			{
				LastPush.Send();
			}
			else
			{
				LastPush.StartTimeout();
				buffer.Add(LastPush);
			}
		}

		/// <summary>
		/// Generates the Join Push message by merging broadcast, presence, and postgres_changes options.
		/// </summary>
		/// <returns></returns>
		private Push GenerateJoinPush() => new Push(socket, this, CHANNEL_EVENT_JOIN, payload: new JoinPush(BroadcastOptions, PresenceOptions, PostgresChangesOptions));

		/// <summary>
		/// Generates an auth push.
		/// </summary>
		/// <returns></returns>
		private Push? GenerateAuthPush()
		{
			var accessToken = Options.RetrieveAccessToken();

			if (!string.IsNullOrEmpty(accessToken))
			{
				return new Push(socket, this, CHANNEL_ACCESS_TOKEN, payload: new Dictionary<string, string>
				{
					{ "access_token",  accessToken!}
				});
			}
			else
			{
				return null;
			}
		}

		/// <summary>
		/// If the channel errors internally (pheonix error, not transport) attempt rejoining.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void HandleRejoinTimerElapsed(object sender, ElapsedEventArgs e)
		{
			if (isRejoining) return;
			isRejoining = true;

			if (State != ChannelState.Closed && State != ChannelState.Errored)
				return;

			Options.ClientOptions.Logger?.Invoke(Topic, "attempting to rejoin", null);

			// Reset join push instance
			JoinPush = GenerateJoinPush();

			Rejoin();
		}

		/// <summary>
		/// Sends the pheonix server a join message.
		/// </summary>
		/// <param name="timeoutMs"></param>
		private void SendJoin(int timeoutMs = DEFAULT_TIMEOUT)
		{
			SetState(ChannelState.Joining);

			// Remove handler if exists
			if (JoinPush != null)
				JoinPush.OnMessage -= HandleJoinResponse;

			JoinPush = GenerateJoinPush();
			JoinPush.OnMessage += HandleJoinResponse;
			JoinPush.Resend(timeoutMs);
		}

		/// <summary>
		/// Handles a recieved join response (received after sending on subscribe/reconnection)
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		private void HandleJoinResponse(object sender, SocketResponseEventArgs args)
		{
			if (args.Response._event == CHANNEL_EVENT_REPLY)
			{
				var obj = JsonConvert.DeserializeObject<PheonixResponse>(JsonConvert.SerializeObject(args.Response.Payload, Options.SerializerSettings), Options.SerializerSettings);

				if (obj == null) return;

				if (obj.Status == PHEONIX_STATUS_OK)
				{
					// Disable Rejoin Timeout
					rejoinTimer?.Stop();
					isRejoining = false;

					var authPush = GenerateAuthPush();

					if (authPush != null)
						authPush.Send();

					SetState(ChannelState.Joined);
				}
				else if (obj.Status == PHEONIX_STATUS_ERROR)
				{
					rejoinTimer.Stop();
					isRejoining = false;
					SetState(ChannelState.Errored);
				}
			}
		}

		/// <summary>
		/// Sets the instance's current state.
		/// </summary>
		/// <param name="state"></param>
		private void SetState(ChannelState state)
		{
			State = state;
			StateChanged?.Invoke(this, new ChannelStateChangedEventArgs(state));
		}

		/// <summary>
		/// Called when a socket message is recieved, parses the correct event handler to pass to.
		/// </summary>
		/// <param name="args"></param>
		internal void HandleSocketMessage(SocketResponseEventArgs args)
		{
			if (args.Response.Ref == JoinPush?.Ref) return;

			// If we don't ignore this event we'll end up with double callbacks.
			if (args.Response._event == "*") return;

			OnMessage?.Invoke(this, args);

			switch (args.Response.Event)
			{
				case EventType.PostgresChanges:
					var deserialize = JsonConvert.DeserializeObject<PostgresChangesResponse>(args.Response.Json!, Options.SerializerSettings);

					if (deserialize?.Payload?.Data == null) return;

					deserialize!.Json = args.Response.Json;
					deserialize.serializerSettings = Options.SerializerSettings;

					var newArgs = new PostgresChangesEventArgs(deserialize!);

					// Invoke '*' listener
					OnPostgresChange?.Invoke(this, newArgs);


					switch (deserialize!.Payload!.Data!.Type)
					{
						case EventType.Insert:
							OnInsert?.Invoke(this, newArgs);
							break;
						case EventType.Update:
							OnUpdate?.Invoke(this, newArgs);
							break;
						case EventType.Delete:
							OnDelete?.Invoke(this, newArgs);
							break;
					}
					break;
				case EventType.Broadcast:
					OnBroadcast?.Invoke(this, args);
					break;
				case EventType.PresenceState:
					OnPresenceSync?.Invoke(this, args);
					break;
				case EventType.PresenceDiff:
					OnPresenceDiff?.Invoke(this, args);
					break;
			}
		}

		/// <summary>
		/// Triggers events for a channel's state changing.
		/// </summary>
		/// <param name="args"></param>
		/// <param name="shouldRejoin"></param>
		private void TriggerChannelStateEvent(ChannelStateChangedEventArgs args, bool shouldRejoin = true)
		{
			SetState(args.State);

			if (shouldRejoin)
			{
				isRejoining = false;
				rejoinTimer.Start();
			}
			else rejoinTimer.Stop();

			switch (args.State)
			{
				case ChannelState.Closed:
					OnClose?.Invoke(this, args);
					break;
				case ChannelState.Errored:
					OnError?.Invoke(this, args);
					break;
				default:
					break;
			}
		}

	}
}
