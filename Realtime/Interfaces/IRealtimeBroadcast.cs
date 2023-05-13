using Supabase.Realtime.Socket;
using System;
using System.Threading.Tasks;
using Supabase.Realtime.Models;
using static Supabase.Realtime.Constants;

namespace Supabase.Realtime.Interfaces
{
    public interface IRealtimeBroadcast
    {
        delegate void BroadcastEventHandler(IRealtimeBroadcast sender, BaseBroadcast? broadcast);

        void AddBroadcastEventHandler(BroadcastEventHandler broadcastEventHandler);
        void RemoveBroadcastEventHandler(BroadcastEventHandler broadcastEventHandler);
        void ClearBroadcastEventHandlers();

        Task<bool> Send(string? broadcastEventName, object payload, int timeoutMs = DefaultTimeout);

        void TriggerReceived(SocketResponse response);
    }
}