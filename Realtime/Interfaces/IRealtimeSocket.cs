using Supabase.Realtime.Socket;
using System;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace Supabase.Realtime.Interfaces
{
    public interface IRealtimeSocket
    {
        bool IsConnected { get; }

        event EventHandler<SocketResponseEventArgs> OnHeartbeat;
        event EventHandler<SocketResponseEventArgs> OnMessage;
        event EventHandler<SocketStateChangedEventArgs> StateChanged;

        Task<double> GetLatency();
		Task Connect();
        void Disconnect(WebSocketCloseStatus code = WebSocketCloseStatus.NormalClosure, string reason = "");
        string MakeMsgRef();
        void Push(SocketRequest data);
        string ReplyEventName(string msgRef);
    }
}