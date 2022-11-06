using System;
using System.Threading.Tasks;

namespace Supabase.Realtime.Interfaces
{
    public interface IRealtimeChannel
    {
        bool HasJoinedOnce { get; }
        bool IsClosed { get; }
        bool IsErrored { get; }
        bool IsJoined { get; }
        bool IsJoining { get; }
        bool IsLeaving { get; }
        ChannelOptions Options { get; }
        Channel.ChannelState State { get; }
        string Topic { get; }

        event EventHandler<ChannelStateChangedEventArgs> OnClose;
        event EventHandler<SocketResponseEventArgs> OnDelete;
        event EventHandler<ChannelStateChangedEventArgs> OnError;
        event EventHandler<SocketResponseEventArgs> OnInsert;
        event EventHandler<SocketResponseEventArgs> OnMessage;
        event EventHandler<SocketResponseEventArgs> OnUpdate;
        event EventHandler<ChannelStateChangedEventArgs> StateChanged;

        void Push(string eventName, object payload, int timeoutMs = 10000);
        void Rejoin(int timeoutMs = 10000);
        Task<IRealtimeChannel> Subscribe(int timeoutMs = 10000);
        void Unsubscribe();
    }
}