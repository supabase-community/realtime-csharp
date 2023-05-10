using Supabase.Realtime.Socket;
using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using static Supabase.Realtime.Constants;

namespace Supabase.Realtime.Interfaces
{
    public interface IRealtimeSocket
    {
        bool IsConnected { get; }

        delegate void StateEventHandler(IRealtimeSocket sender, SocketState state);

        delegate void MessageEventHandler(IRealtimeSocket sender, SocketResponse message);

        delegate void HeartbeatEventHandler(IRealtimeSocket sender, SocketResponse heartbeat);

        void AddStateChangedListener(StateEventHandler stateEventHandler);
        void RemoveStateChangedListener(StateEventHandler stateEventHandler);
        void ClearStateChangedListeners();
        
        void AddMessageReceivedListener(MessageEventHandler messageEventHandler);
        void RemoveMessageReceivedListener(MessageEventHandler heartbeatHandler);
        void ClearMessageReceivedListeners();
        
        void AddHeartbeatListener(HeartbeatEventHandler messageEventHandler);
        void RemoveHeartbeatListener(HeartbeatEventHandler messageEventHandler);
        void ClearHeartbeatListeners();

        Task<double> GetLatency();
		Task Connect();
        void Disconnect(WebSocketCloseStatus code = WebSocketCloseStatus.NormalClosure, string reason = "");
        string MakeMsgRef();
        void Push(SocketRequest data);
        string ReplyEventName(string msgRef);
    }
}