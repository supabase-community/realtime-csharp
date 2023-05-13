using Supabase.Realtime.Socket;
using System;
using Supabase.Realtime.Models;
using static Supabase.Realtime.Constants;

namespace Supabase.Realtime.Interfaces
{
    public interface IRealtimePresence
    {
        delegate void PresenceEventHandler(IRealtimePresence sender, EventType eventType);

        public enum EventType
        {
            Sync,
            Join,
            Leave
        }

        void Track(object? payload, int timeoutMs = DefaultTimeout);

        void TriggerSync(SocketResponse response);
        void TriggerDiff(SocketResponse args);

        void AddPresenceEventHandler(EventType eventType, PresenceEventHandler presenceEventHandler);

        void RemovePresenceEventHandlers(EventType eventType, PresenceEventHandler presenceEventHandler);

        void ClearPresenceEventHandlers(EventType? eventType = null);
    }
}