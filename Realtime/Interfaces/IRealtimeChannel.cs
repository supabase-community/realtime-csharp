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

namespace Supabase.Realtime.Interfaces
{
    public interface IRealtimeChannel
	{
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

		event EventHandler<SocketResponseEventArgs> OnMessage;
		event EventHandler<ChannelStateChangedEventArgs> StateChanged;
		event EventHandler<ChannelStateChangedEventArgs> OnClose;
		event EventHandler<ChannelStateChangedEventArgs> OnError;
		event EventHandler<PostgresChangesEventArgs> OnDelete;
		event EventHandler<PostgresChangesEventArgs> OnInsert;
		event EventHandler<PostgresChangesEventArgs> OnUpdate;
		event EventHandler<PostgresChangesEventArgs> OnPostgresChange;

		IRealtimeBroadcast? Broadcast();
		IRealtimePresence? Presence();

		Push Push(string eventName, string? type = null, object? payload = null, int timeoutMs = DEFAULT_TIMEOUT);
		void Rejoin(int timeoutMs = DEFAULT_TIMEOUT);
		Task<bool> Send(ChannelEventName eventType, string? type, object payload, int timeoutMs = DEFAULT_TIMEOUT);

		RealtimeBroadcast<TBroadcastResponse> Register<TBroadcastResponse>(bool broadcastSelf = false, bool broadcastAck = false) where TBroadcastResponse : BaseBroadcast;
		RealtimePresence<TPresenceResponse> Register<TPresenceResponse>(string presenceKey) where TPresenceResponse : BasePresence;
		IRealtimeChannel Register(PostgresChangesOptions postgresChangesOptions);

		Task<IRealtimeChannel> Subscribe(int timeoutMs = DEFAULT_TIMEOUT);

		void Unsubscribe();
	}
}