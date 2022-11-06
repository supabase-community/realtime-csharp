using System;

namespace Supabase.Realtime.Interfaces
{
    public interface IRealtimePush<TChannel, TSocketResponse>
        where TChannel : IRealtimeChannel
        where TSocketResponse: IRealtimeSocketResponse
    {
        TChannel Channel { get; }
        string EventName { get; }
        bool IsSent { get; }
        SocketRequest? Message { get; }
        object? Payload { get; }
        string? Ref { get; }
        IRealtimeSocketResponse? Response { get; }

        event EventHandler<SocketResponseEventArgs>? OnMessage;
        event EventHandler? OnTimeout;

        void Resend(int timeoutMs = 10000);
        void Send();
    }
}