using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace Supabase.Realtime.Interfaces
{
    public interface IRealtimeClient<TSocket, TChannel>
        where TSocket: IRealtimeSocket
        where TChannel: IRealtimeChannel
    {
        ClientOptions Options { get; }
        JsonSerializerSettings SerializerSettings { get; }
        IRealtimeSocket? Socket { get; }
        ReadOnlyDictionary<string, TChannel> Subscriptions { get; }

        event EventHandler<SocketStateChangedEventArgs> OnClose;
        event EventHandler<SocketStateChangedEventArgs> OnError;
        event EventHandler<SocketStateChangedEventArgs> OnMessage;
        event EventHandler<SocketStateChangedEventArgs> OnOpen;

        Channel Channel(string database = "realtime", string? schema = null, string? table = null, string? column = null, string? value = null, Dictionary<string, string>? parameters = null);
        IRealtimeClient<TSocket, TChannel> Connect(Action<IRealtimeClient<TSocket, TChannel>>? callback = null);
        Task<IRealtimeClient<TSocket, TChannel>> ConnectAsync();
        IRealtimeClient<TSocket, TChannel> Disconnect(WebSocketCloseStatus code = WebSocketCloseStatus.NormalClosure, string reason = "Programmatic Disconnect");
        void Remove(TChannel channel);
        void SetAuth(string jwt);
    }
}