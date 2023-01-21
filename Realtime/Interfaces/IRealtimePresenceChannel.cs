using Supabase.Realtime.Models;
using System;
using System.Threading.Tasks;

namespace Supabase.Realtime.Interfaces
{
	public interface IRealtimePresenceChannel<TPresence> where TPresence : Presence
	{
		TPresence Presence { get; }
	}
}