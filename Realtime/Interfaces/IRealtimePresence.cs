using Supabase.Realtime.Models;
using Supabase.Realtime.Presence;
using Supabase.Realtime.Socket;
using System;
using System.Collections.Generic;
using System.Text;

namespace Supabase.Realtime.Interfaces
{
    public interface IRealtimePresence
	{
		event EventHandler<EventArgs?>? OnJoin;
		event EventHandler<EventArgs?>? OnLeave;
		event EventHandler<EventArgs?>? OnSync;

		void TriggerSync(SocketResponseEventArgs args);
		void TriggerDiff(SocketResponseEventArgs args);
	}
}
