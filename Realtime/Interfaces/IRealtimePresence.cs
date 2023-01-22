using Supabase.Realtime.Models;
using Supabase.Realtime.Presence;
using System;
using System.Collections.Generic;
using System.Text;

namespace Supabase.Realtime.Interfaces
{
    public interface IRealtimePresence
	{
		event EventHandler<RealtimePresenceEventArgs>? OnJoin;
		event EventHandler<RealtimePresenceEventArgs>? OnLeave;
		event EventHandler<RealtimePresenceSyncArgs>? OnSync;
	}
}
