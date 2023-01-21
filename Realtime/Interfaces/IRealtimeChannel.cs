using Supabase.Realtime.Channel;
using Supabase.Realtime.Models;
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

		event EventHandler<ChannelStateChangedEventArgs> OnClose;
		event EventHandler<SocketResponseEventArgs> OnDelete;
		event EventHandler<ChannelStateChangedEventArgs> OnError;
		event EventHandler<SocketResponseEventArgs> OnInsert;
		event EventHandler<SocketResponseEventArgs> OnMessage;
		event EventHandler<SocketResponseEventArgs> OnUpdate;
		event EventHandler<ChannelStateChangedEventArgs> StateChanged;

		void Push(string eventName, string? type = null, object? payload = null, int timeoutMs = DEFAULT_TIMEOUT);
		void Rejoin(int timeoutMs = DEFAULT_TIMEOUT);
		void Send(ChannelType payloadType, Dictionary<string, object> payload, int timeoutMs = DEFAULT_TIMEOUT);

		IRealtimeChannel Register<TBroadcastResponse>(BroadcastOptions broadcastOptions, string eventName) where TBroadcastResponse : struct;
		IRealtimeChannel Register<TPresenceResponse>(PresenceOptions presenceOptions) where TPresenceResponse : Presence;
		IRealtimeChannel Register(PostgresChangesOptions postgresChangesOptions);

		Task<IRealtimeChannel> Subscribe(int timeoutMs = DEFAULT_TIMEOUT);

		void Unsubscribe();
	}
}