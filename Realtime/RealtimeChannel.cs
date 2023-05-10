using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json;
using Supabase.Realtime.Broadcast;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Exceptions;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Models;
using Supabase.Realtime.PostgresChanges;
using Supabase.Realtime.Presence;
using Supabase.Realtime.Socket;
using Supabase.Realtime.Socket.Responses;
using static Supabase.Realtime.Constants;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;
using Timer = System.Timers.Timer;

// ReSharper disable InvalidXmlDocComment

[assembly: InternalsVisibleTo("RealtimeTests")]

namespace Supabase.Realtime;

/// <summary>
/// Class representation of a channel subscription
/// </summary>
public class RealtimeChannel : IRealtimeChannel
{
    public bool IsClosed => State == ChannelState.Closed;
    public bool IsErrored => State == ChannelState.Errored;
    public bool IsJoined => State == ChannelState.Joined;
    public bool IsJoining => State == ChannelState.Joining;
    public bool IsLeaving => State == ChannelState.Leaving;

    private readonly List<IRealtimeChannel.StateChangedHandler> _stateChangedHandlers = new();
    private readonly List<IRealtimeChannel.MessageReceivedHandler> _messageReceivedHandlers = new();

    private readonly Dictionary<PostgresChangesOptions.ListenType, List<IRealtimeChannel.PostgresChangesHandler>>
        _postgresChangesHandlers = new();

    /// <summary>
    /// The channel's topic (identifier)
    /// </summary>
    public string Topic { get; }

    /// <summary>
    /// The Channel's current state.
    /// </summary>
    public ChannelState State { get; private set; } = ChannelState.Closed;

    /// <summary>
    /// Options passed to this channel instance.
    /// </summary>
    public ChannelOptions Options { get; }

    /// <summary>
    /// The saved Broadcast Options, set in <see cref="Register{TBroadcastResponse}(BroadcastOptions)"/>
    /// </summary>
    public BroadcastOptions? BroadcastOptions { get; private set; } = new();

    /// <summary>
    /// The saved Presence Options, set in <see cref="Register{TPresenceResponse}(PresenceOptions)"/>
    /// </summary>
    public PresenceOptions? PresenceOptions { get; private set; } = new(string.Empty);

    /// <summary>
    /// The saved Postgres Changes Options, set in <see cref="Register(PostgresChanges.PostgresChangesOptions)"/>
    /// </summary>
    public List<PostgresChangesOptions> PostgresChangesOptions { get; } = new();

    /// <summary>
    /// Flag stating whether a channel has been joined once or not.
    /// </summary>
    public bool HasJoinedOnce { get; private set; }

    /// <summary>
    /// Flag stating if a channel is currently subscribed.
    /// </summary>
    public bool IsSubscribed;

    /// <summary>
    /// Returns the <see cref="IRealtimeBroadcast"/> instance.
    /// </summary>
    /// <returns></returns>
    public IRealtimeBroadcast? Broadcast() => _broadcast;

    /// <summary>
    /// Returns a typed <see cref="RealtimeBroadcast{TBroadcastModel}" /> instance.
    /// </summary>
    /// <typeparam name="TBroadcastModel"></typeparam>
    /// <returns></returns>
    public RealtimeBroadcast<TBroadcastModel>? Broadcast<TBroadcastModel>() where TBroadcastModel : BaseBroadcast =>
        _broadcast != null ? (RealtimeBroadcast<TBroadcastModel>)_broadcast : default;

    /// <summary>
    /// Returns the <see cref="IRealtimePresence"/> instance.
    /// </summary>
    /// <returns></returns>
    public IRealtimePresence? Presence() => _presence;

    /// <summary>
    /// Returns a typed <see cref="RealtimePresence{T}"/> instance.
    /// </summary>
    /// <typeparam name="TPresenceModel">Model representing a Presence payload</typeparam>
    /// <returns></returns>
    public RealtimePresence<TPresenceModel>? Presence<TPresenceModel>() where TPresenceModel : BasePresence =>
        _presence != null ? (RealtimePresence<TPresenceModel>)_presence : default;

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
    /// Buffer of Pushes held because of Socket availability
    /// </summary>
    private readonly List<Push> _buffer = new();

    private readonly IRealtimeSocket _socket;
    private IRealtimePresence? _presence;
    private IRealtimeBroadcast? _broadcast;
    private bool CanPush => IsJoined && _socket.IsConnected;
    private bool _hasJoinedOnce;
    private readonly Timer _rejoinTimer;
    private bool _isRejoining;

    /// <summary>
    /// Initializes a Channel - must call `Subscribe()` to receive events.
    /// </summary>
    public RealtimeChannel(IRealtimeSocket socket, string channelName, ChannelOptions options)
    {
        Topic = channelName;
        Options = options;
        Options.Parameters ??= new Dictionary<string, string>();

        _socket = socket;
        _socket.AddStateChangedListener(HandleSocketStateChanged);

        _rejoinTimer = new Timer(options.ClientOptions.Timeout.TotalMilliseconds);
        _rejoinTimer.Elapsed += HandleRejoinTimerElapsed;
        _rejoinTimer.AutoReset = true;
    }

    /// <summary>
    /// Handles socket state changes, specifically when a socket reconnects this channel (if subscribed) should also
    /// rejoin.
    /// </summary>
    /// <param name="_"></param>
    /// <param name="state"></param>
    private void HandleSocketStateChanged(IRealtimeSocket _, SocketState state)
    {
        if (state != SocketState.Reconnect || !IsSubscribed) return;

        IsSubscribed = false;
        Rejoin();
    }

    /// <summary>
    /// Registers a <see cref="RealtimeBroadcast{TBroadcastModel}"/> instance - allowing broadcast responses to be parsed.
    /// </summary>
    /// <typeparam name="TBroadcastResponse"></typeparam>
    /// <param name="broadcastSelf">enables client to receive message it has broadcast</param>
    /// <param name="broadcastAck">instructs server to acknowledge that broadcast message was received</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public RealtimeBroadcast<TBroadcastResponse> Register<TBroadcastResponse>(bool broadcastSelf = false,
        bool broadcastAck = false) where TBroadcastResponse : BaseBroadcast
    {
        if (_broadcast != null)
            throw new InvalidOperationException(
                "Register can only be called with broadcast options for a channel once.");

        BroadcastOptions = new BroadcastOptions(broadcastSelf, broadcastAck);

        var instance =
            new RealtimeBroadcast<TBroadcastResponse>(this, BroadcastOptions, Options.SerializerSettings);
        _broadcast = instance;

        OnBroadcast += (_, args) => _broadcast.TriggerReceived(args);

        return instance;
    }

    /// <summary>
    /// Registers a <see cref="RealtimePresence{TPresenceResponse}"/> instance - allowing presence responses to be parsed and state to be tracked.
    /// </summary>
    /// <typeparam name="TPresenceResponse">The model representing a presence payload.</typeparam>
    /// <param name="presenceKey">used to track presence payload across clients</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Thrown if called multiple times.</exception>
    public RealtimePresence<TPresenceResponse> Register<TPresenceResponse>(string presenceKey)
        where TPresenceResponse : BasePresence
    {
        if (_presence != null)
            throw new InvalidOperationException(
                "Register can only be called with presence options for a channel once.");

        PresenceOptions = new PresenceOptions(presenceKey);
        var instance = new RealtimePresence<TPresenceResponse>(this, PresenceOptions, Options.SerializerSettings);
        _presence = instance;

        OnPresenceSync += (_, args) => _presence.TriggerSync(args);
        OnPresenceDiff += (_, args) => _presence.TriggerDiff(args);

        return instance;
    }

    public void AddStateChangedListener(IRealtimeChannel.StateChangedHandler stateChangedHandler)
    {
        if (!_stateChangedHandlers.Contains(stateChangedHandler))
            _stateChangedHandlers.Add(stateChangedHandler);
    }

    public void RemoveStateChangedListener(IRealtimeChannel.StateChangedHandler stateChangedHandler)
    {
        if (_stateChangedHandlers.Contains(stateChangedHandler))
            _stateChangedHandlers.Remove(stateChangedHandler);
    }

    public void ClearStateChangedListeners() =>
        _stateChangedHandlers.Clear();

    private void NotifyStateChanged(ChannelState state, bool shouldRejoin = true)
    {
        State = state;

        _isRejoining = shouldRejoin;
        if (shouldRejoin)
            _rejoinTimer.Start();
        else
            _rejoinTimer.Stop();

        foreach (var handler in _stateChangedHandlers)
            handler.Invoke(this, state);
    }

    public void AddMessageReceivedHandler(IRealtimeChannel.MessageReceivedHandler messageReceivedHandler)
    {
        if (!_messageReceivedHandlers.Contains(messageReceivedHandler))
            _messageReceivedHandlers.Add(messageReceivedHandler);
    }

    public void RemoveMessageReceivedHandler(IRealtimeChannel.MessageReceivedHandler messageReceivedHandler)
    {
        if (_messageReceivedHandlers.Contains(messageReceivedHandler))
            _messageReceivedHandlers.Remove(messageReceivedHandler);
    }

    public void ClearMessageReceivedListeners() =>
        _messageReceivedHandlers.Clear();

    private void NotifyMessageReceived(SocketResponse message)
    {
        foreach (var handler in _messageReceivedHandlers)
            handler.Invoke(this, message);
    }

    public void AddPostgresChangeListener(PostgresChangesOptions.ListenType listenType,
        IRealtimeChannel.PostgresChangesHandler postgresChangeHandler)
    {
        if (_postgresChangesHandlers[listenType] == null)
            _postgresChangesHandlers[listenType] = new List<IRealtimeChannel.PostgresChangesHandler>();

        if (!_postgresChangesHandlers[listenType].Contains(postgresChangeHandler))
            _postgresChangesHandlers[listenType].Add(postgresChangeHandler);
    }

    public void RemovePostgresChangeListener(PostgresChangesOptions.ListenType listenType,
        IRealtimeChannel.PostgresChangesHandler postgresChangeHandler)
    {
        if (_postgresChangesHandlers.ContainsKey(listenType) &&
            _postgresChangesHandlers[listenType].Contains(postgresChangeHandler))
            _postgresChangesHandlers[listenType].Remove(postgresChangeHandler);
    }

    public void ClearPostgresChangeListeners() =>
        _postgresChangesHandlers.Clear();

    private void NotifyPostgresChanges(EventType eventType, PostgresChangesResponse response)
    {
        var listenType = ListenType.All;

        switch (eventType)
        {
            case EventType.Insert:
                listenType = ListenType.Inserts;
                break;
            case EventType.Delete:
                listenType = ListenType.Deletes;
                break;
            case EventType.Update:
                listenType = ListenType.Updates;
                break;
        }
        
        // Invoke the wildcard listener (but only once)
        if (listenType != ListenType.All)
            foreach (var handler in _postgresChangesHandlers[ListenType.All])
                handler.Invoke(this, response);

        foreach (var handler in _postgresChangesHandlers[listenType])
            handler.Invoke(this, response);
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
    public Task<IRealtimeChannel> Subscribe(int timeoutMs = DefaultTimeout)
    {
        var tsc = new TaskCompletionSource<IRealtimeChannel>();

        if (IsSubscribed)
            return Task.FromResult(this as IRealtimeChannel);

        JoinPush = GenerateJoinPush();
        IRealtimeChannel.StateChangedHandler? channelCallback = null;
        EventHandler? joinPushTimeoutCallback = null;

        channelCallback = (sender, state) =>
        {
            switch (state)
            {
                // Success!
                case ChannelState.Joined:
                    HasJoinedOnce = true;
                    IsSubscribed = true;

                    sender.RemoveStateChangedListener(channelCallback!);
                    JoinPush.OnTimeout -= joinPushTimeoutCallback;

                    // Clear buffer
                    foreach (var item in _buffer)
                        item.Send();
                    _buffer.Clear();

                    tsc.TrySetResult(this);
                    break;
                // Failure
                case ChannelState.Closed:
                case ChannelState.Errored:
                    IsSubscribed = false;
                    sender.RemoveStateChangedListener(channelCallback!);
                    JoinPush.OnTimeout -= joinPushTimeoutCallback;

                    tsc.TrySetException(new Exception("Error occurred connecting to channel. Check logs."));
                    break;
            }
        };

        // Throw an exception if there is a problem receiving a join response
        joinPushTimeoutCallback = (_, _) =>
        {
            RemoveStateChangedListener(channelCallback);
            JoinPush.OnTimeout -= joinPushTimeoutCallback;

            tsc.TrySetException(new RealtimeException("Push Timeout")
            {
                Reason = FailureHint.Reason.PushTimeout
            });
        };

        AddStateChangedListener(channelCallback);

        // Set a flag to prevent multiple join attempts.
        _hasJoinedOnce = true;

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
        NotifyStateChanged(ChannelState.Leaving);

        var leavePush = new Push(_socket, this, ChannelEventLeave);
        leavePush.Send();

        NotifyStateChanged(ChannelState.Closed, false);

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
    public Push Push(string eventName, string? type = null, object? payload = null, int timeoutMs = DefaultTimeout)
    {
        if (!_hasJoinedOnce)
            throw new Exception(
                $"Tried to push '{eventName}' to '{Topic}' before joining. Use `Channel.Subscribe()` before pushing events");

        var push = new Push(_socket, this, eventName, type, payload, timeoutMs);
        Enqueue(push);

        return push;
    }

    /// <summary>
    /// Sends an arbitrary payload with a given payload type (<see cref="ChannelEventName"/>)
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="payload"></param>
    /// <param name="timeoutMs"></param>
    public Task<bool> Send(ChannelEventName eventName, string? type, object payload, int timeoutMs = DefaultTimeout)
    {
        var tsc = new TaskCompletionSource<bool>();

        var ev = Core.Helpers.GetMappedToAttr(eventName).Mapping;
        var push = Push(ev, type, payload, timeoutMs);

        IRealtimePush<RealtimeChannel, SocketResponse>.MessageEventHandler? messageCallback = null;

        messageCallback = (_, message) =>
        {
            tsc.SetResult(message.Event != EventType.Unknown);
            push.RemoveMessageReceivedListener(messageCallback!);
        };

        push.AddMessageReceivedListener(messageCallback);
        return tsc.Task;
    }

    /// <summary>
    /// Rejoins the channel.
    /// </summary>
    /// <param name="timeoutMs"></param>
    public void Rejoin(int timeoutMs = DefaultTimeout)
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

        if (CanPush)
        {
            LastPush.Send();
        }
        else
        {
            LastPush.StartTimeout();
            _buffer.Add(LastPush);
        }
    }

    /// <summary>
    /// Generates the Join Push message by merging broadcast, presence, and postgres_changes options.
    /// </summary>
    /// <returns></returns>
    private Push GenerateJoinPush() => new(_socket, this, ChannelEventJoin,
        payload: new JoinPush(BroadcastOptions, PresenceOptions, PostgresChangesOptions));

    /// <summary>
    /// Generates an auth push.
    /// </summary>
    /// <returns></returns>
    private Push? GenerateAuthPush()
    {
        var accessToken = Options.RetrieveAccessToken();

        if (!string.IsNullOrEmpty(accessToken))
        {
            return new Push(_socket, this, ChannelAccessToken, payload: new Dictionary<string, string>
            {
                { "access_token", accessToken! }
            });
        }

        return null;
    }

    /// <summary>
    /// If the channel errors internally (phoenix error, not transport) attempt rejoining.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void HandleRejoinTimerElapsed(object sender, ElapsedEventArgs e)
    {
        if (_isRejoining) return;
        _isRejoining = true;

        if (State != ChannelState.Closed && State != ChannelState.Errored)
            return;

        Options.ClientOptions.Logger(Topic, "attempting to rejoin", null);

        // Reset join push instance
        JoinPush = GenerateJoinPush();

        Rejoin();
    }

    /// <summary>
    /// Sends the phoenix server a join message.
    /// </summary>
    /// <param name="timeoutMs"></param>
    private void SendJoin(int timeoutMs = DefaultTimeout)
    {
        NotifyStateChanged(ChannelState.Joining);

        // Remove handler if exists
        if (JoinPush != null)
            JoinPush.RemoveMessageReceivedListener(HandleJoinResponse);

        JoinPush = GenerateJoinPush();
        JoinPush.AddMessageReceivedListener(HandleJoinResponse);
        JoinPush.Resend(timeoutMs);
    }

    /// <summary>
    /// Handles a received join response (received after sending on subscribe/reconnection)
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="message"></param>
    private void HandleJoinResponse(IRealtimePush<RealtimeChannel, SocketResponse> sender, SocketResponse message)
    {
        if (message._event != ChannelEventReply) return;

        var obj = JsonConvert.DeserializeObject<PheonixResponse>(
            JsonConvert.SerializeObject(message.Payload, Options.SerializerSettings),
            Options.SerializerSettings);

        if (obj == null) return;

        switch (obj.Status)
        {
            case PhoenixStatusOk:
                // Disable Rejoin Timeout
                _rejoinTimer.Stop();
                _isRejoining = false;

                var authPush = GenerateAuthPush();
                authPush?.Send();

                NotifyStateChanged(ChannelState.Joined);
                break;
            case PheonixStatusError:
                _rejoinTimer.Stop();
                _isRejoining = false;

                NotifyStateChanged(ChannelState.Errored);
                break;
        }
    }

    /// <summary>
    /// Called when a socket message is received, parses the correct event handler to pass to.
    /// </summary>
    /// <param name="message"></param>
    internal void HandleSocketMessage(SocketResponse message)
    {
        if (message.Ref == JoinPush?.Ref) return;

        // If we don't ignore this event we'll end up with double callbacks.
        if (message._event == "*") return;

        NotifyMessageReceived(message);

        switch (message.Event)
        {
            case EventType.PostgresChanges:
                var deserialized =
                    JsonConvert.DeserializeObject<PostgresChangesResponse>(message.Json!,
                        Options.SerializerSettings);

                if (deserialized?.Payload?.Data == null) return;

                deserialized.Json = message.Json;
                deserialized.serializerSettings = Options.SerializerSettings;

                var newArgs = new PostgresChangesEventArgs(deserialized);

                // Invoke '*' listener
                NotifyPostgresChanges(deserialized.Payload!.Data!.Type, deserialized);

                break;
            case EventType.Broadcast:
                //OnBroadcast?.Invoke(this, message);
                break;
            case EventType.PresenceState:
                //OnPresenceSync?.Invoke(this, message);
                break;
            case EventType.PresenceDiff:
                //OnPresenceDiff?.Invoke(this, message);
                break;
        }
    }
}