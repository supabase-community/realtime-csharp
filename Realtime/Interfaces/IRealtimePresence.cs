using Supabase.Realtime.Socket;
using System;
using static Supabase.Realtime.Constants;

namespace Supabase.Realtime.Interfaces
{
	public interface IRealtimePresence
	{
		event EventHandler<EventArgs?>? OnJoin;
		event EventHandler<EventArgs?>? OnLeave;
		event EventHandler<EventArgs?>? OnSync;

		void Track(object? payload, int timeoutMs = DefaultTimeout);

		void TriggerSync(SocketResponseEventArgs args);
		void TriggerDiff(SocketResponseEventArgs args);
	}
}
