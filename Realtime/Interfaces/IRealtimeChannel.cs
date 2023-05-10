using Supabase.Realtime.Broadcast;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Models;
using Supabase.Realtime.PostgresChanges;
using Supabase.Realtime.Presence;
using Supabase.Realtime.Socket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Supabase.Realtime.Constants;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

namespace Supabase.Realtime.Interfaces
{
    public interface IRealtimeChannel
    {
        delegate void MessageReceivedHandler(IRealtimeChannel sender, SocketResponse message);

        delegate void StateChangedHandler(IRealtimeChannel sender, ChannelState state);

        delegate void PostgresChangesHandler(IRealtimeChannel sender, PostgresChangesResponse change);

        bool HasJoinedOnce { get; }
        bool IsClosed { get; }
        bool IsErrored { get; }
        bool IsJoined { get; }
        bool IsJoining { get; }
        bool IsLeaving { get; }
        ChannelOptions Options { get; }
        BroadcastOptions? BroadcastOptions { get; }
        PresenceOptions? PresenceOptions { get; }
        List<PostgresChangesOptions> PostgresChangesOptions { get; }
        ChannelState State { get; }
        string Topic { get; }

        void AddStateChangedListener(StateChangedHandler stateChangedHandler);

        void RemoveStateChangedListener(StateChangedHandler stateChangedHandler);

        void ClearStateChangedListeners();

        void AddMessageReceivedHandler(MessageReceivedHandler messageReceivedHandler);

        void RemoveMessageReceivedHandler(MessageReceivedHandler messageReceivedHandler);

        void ClearMessageReceivedListeners();

        void AddPostgresChangeListener(ListenType listenType, PostgresChangesHandler postgresChangeHandler);

        void RemovePostgresChangeListener(ListenType listenType, PostgresChangesHandler postgresChangeHandler);

        void ClearPostgresChangeListeners();

        IRealtimeBroadcast? Broadcast();
        IRealtimePresence? Presence();

        Push Push(string eventName, string? type = null, object? payload = null, int timeoutMs = DefaultTimeout);
        void Rejoin(int timeoutMs = DefaultTimeout);
        Task<bool> Send(ChannelEventName eventType, string? type, object payload, int timeoutMs = DefaultTimeout);

        RealtimeBroadcast<TBroadcastResponse> Register<TBroadcastResponse>(bool broadcastSelf = false,
            bool broadcastAck = false) where TBroadcastResponse : BaseBroadcast;

        RealtimePresence<TPresenceResponse> Register<TPresenceResponse>(string presenceKey)
            where TPresenceResponse : BasePresence;

        IRealtimeChannel Register(PostgresChangesOptions postgresChangesOptions);
        Task<IRealtimeChannel> Subscribe(int timeoutMs = DefaultTimeout);
        IRealtimeChannel Unsubscribe();
    }
}