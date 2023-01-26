using Supabase.Realtime.Socket;
using System;

namespace Supabase.Realtime.Interfaces
{
    public interface IRealtimeBroadcast
    {
        event EventHandler<EventArgs?>? OnBroadcast;

        void TriggerReceived(SocketResponseEventArgs args);
    }
}