using Supabase.Realtime.Socket;
using System;
using System.Threading.Tasks;
using static Supabase.Realtime.Constants;

namespace Supabase.Realtime.Interfaces
{
    public interface IRealtimeBroadcast
    {
        event EventHandler<EventArgs?>? OnBroadcast;
		Task<bool> Send(string? broadcastEventName, object payload, int timeoutMs = DefaultTimeout);

		void TriggerReceived(SocketResponseEventArgs args);
    }
}