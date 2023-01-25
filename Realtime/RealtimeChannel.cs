using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json;
using Postgrest.Models;
using Postgrest.Responses;
using Supabase.Realtime.Broadcast;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Models;
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
		public event EventHandler<SocketResponseEventArgs>? OnInsert;

		/// <summary>
		/// Invoked when the `UPDATE` event is raised.
		/// </summary>
		public event EventHandler<SocketResponseEventArgs>? OnUpdate;

		/// <summary>
		/// Invoked when the `DELETE` event is raised.
		/// </summary>
		public event EventHandler<SocketResponseEventArgs>? OnDelete;

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

		public string Topic { get; private set; }

		/// <summary>
		/// The Channel's current state.
		/// </summary>
		public ChannelState State { get; private set; } = ChannelState.Closed;

		/// <summary>
		/// Options passed to this channel instance.
		/// </summary>
		public ChannelOptions Options { get; private set; }
		public BroadcastOptions? BroadcastOptions { get; private set; }
		public PresenceOptions? PresenceOptions { get; private set; }
		public List<PostgresChangesOptions> PostgresChangesOptions { get; private set; } = new List<PostgresChangesOptions>();

		/// <summary>
		/// Flag stating whether a channel has been joined once or not.
		/// </summary>
		public bool HasJoinedOnce { get; private set; }

		public RealtimePresence? Presence { get; private set; }
		/// <summary>
		/// Flag stating if a channel is currently subscribed.
		/// </summary>
		public bool IsSubscribed = false;
		internal event EventHandler<SocketResponseEventArgs>? OnPresenceDiff;
		internal event EventHandler<SocketResponseEventArgs>? OnPresenceSync;
		internal event EventHandler<SocketResponseEventArgs>? OnBroadcast;

		private IRealtimeSocket socket;

		/// <summary>
		/// The initial request to join a channel (repeated on channel disconnect)
		/// </summary>
		internal Push? JoinPush;
		internal Push? LastPush;

		/// <summary>
		/// Buffer of Pushes held because of Socket availablity
		/// </summary>
		internal List<Push> buffer = new List<Push>();

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
		public RealtimeChannel(IRealtimeSocket socket, string channelName, ChannelOptions options)
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

		public IRealtimeChannel Register<TBroadcastResponse>(BroadcastOptions broadcastOptions, string eventName) where TBroadcastResponse : struct
		{
			BroadcastOptions = broadcastOptions;

			return this;
		}

		public IRealtimeChannel Register(PresenceOptions presenceOptions)
		{
			PresenceOptions = presenceOptions;
			Presence = new RealtimePresence(this, presenceOptions);

			OnPresenceSync += (sender, args) => Presence.TriggerSync(args);
			OnPresenceDiff += (sender, args) => Presence.TriggerDiff(args);

			return this;
		}

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

			if (hasJoinedOnce && IsSubscribed)
			{
				tsc.SetException(new Exception("`Subscribe` can only be called a single time per channel instance."));
				return tsc.Task;
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
		public void Unsubscribe()
		{
			IsSubscribed = false;
			SetState(ChannelState.Leaving);

			var leavePush = new Push(socket, this, CHANNEL_EVENT_LEAVE);
			leavePush.Send();

			TriggerChannelStateEvent(new ChannelStateChangedEventArgs(ChannelState.Closed));
		}

		/// <summary>
		/// Sends a `Push` request under this channel.
		///
		/// Maintains a buffer in the event push is called prior to the channel being joined.
		/// </summary>
		/// <param name="eventName"></param>
		/// <param name="payload"></param>
		/// <param name="timeoutMs"></param>
		public void Push(string eventName, string? type = null, object? payload = null, int timeoutMs = DEFAULT_TIMEOUT)
		{
			if (!hasJoinedOnce)
				throw new Exception($"Tried to push '{eventName}' to '{Topic}' before joining. Use `Channel.Subscribe()` before pushing events");

			Enqueue(new Push(socket, this, eventName, type, payload, timeoutMs));
		}

		public void Send(ChannelType payloadType, Dictionary<string, object> payload, int timeoutMs = DEFAULT_TIMEOUT)
		{
			var type = Core.Helpers.GetMappedToAttr(payloadType).Mapping;
			Push(type, payload: payload, timeoutMs: timeoutMs);
		}

		public void Track(Dictionary<string, object> payload, int timeoutMs = DEFAULT_TIMEOUT)
		{
			var type = Core.Helpers.GetMappedToAttr(ChannelType.Presence).Mapping;
			Push(type, "track", payload, timeoutMs);
		}

		public void Untrack()
		{
			var type = Core.Helpers.GetMappedToAttr(ChannelType.Presence).Mapping;
			Push(type, "untrack");
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

		private Push GenerateJoinPush() => new Push(socket, this, CHANNEL_EVENT_JOIN, payload: new JoinPush(BroadcastOptions, PresenceOptions, PostgresChangesOptions));

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

		private void SendJoin(int timeoutMs = DEFAULT_TIMEOUT)
		{
			SetState(ChannelState.Joining);

			if (JoinPush != null)
			{
				// Remove handler if exists
				JoinPush.OnMessage -= HandleJoinResponse;
			}

			JoinPush = GenerateJoinPush();
			JoinPush.OnMessage += HandleJoinResponse;
			JoinPush.Resend(timeoutMs);
		}

		private void HandleJoinResponse(object sender, SocketResponseEventArgs args)
		{
			if (args.Response._event == CHANNEL_EVENT_REPLY)
			{
				var obj = JsonConvert.DeserializeObject<PheonixResponse>(JsonConvert.SerializeObject(args.Response.Payload, Options.SerializerSettings), Options.SerializerSettings);

				if (obj == null) return;

				if (obj.Status == PHEONIX_STATUS_OK)
				{
					SetState(ChannelState.Joined);

					// Disable Rejoin Timeout
					rejoinTimer?.Stop();
					isRejoining = false;
				}
				else if (obj.Status == PHEONIX_STATUS_ERROR)
				{
					SetState(ChannelState.Errored);

					rejoinTimer.Stop();
					isRejoining = false;
				}
			}
		}

		private void SetState(ChannelState state)
		{
			State = state;
			StateChanged?.Invoke(this, new ChannelStateChangedEventArgs(state));
		}

		internal void HandleSocketMessage(SocketResponseEventArgs args)
		{
			if (args.Response.Ref == JoinPush?.Ref) return;

			// If we don't ignore this event we'll end up with double callbacks.
			if (args.Response._event == "*") return;

			OnMessage?.Invoke(this, args);

			switch (args.Response.Event)
			{
				case EventType.Insert:
					OnInsert?.Invoke(this, args);
					break;
				case EventType.Update:
					OnUpdate?.Invoke(this, args);
					break;
				case EventType.Delete:
					OnDelete?.Invoke(this, args);
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
