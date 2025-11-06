using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using RealtimeTests.Models;
using Supabase.Postgrest.Interfaces;
using Supabase.Realtime;
using Supabase.Realtime.Broadcast;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Models;
using Supabase.Realtime.PostgresChanges;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

namespace RealtimeTests;

public class BroadcastExample : BaseBroadcast
{
    [JsonProperty("userId")]
    public string? UserId { get; set; }
}

[TestClass]
public class ChannelBroadcastTests
{
    private IPostgrestClient? _restClient;
    private IRealtimeClient<RealtimeSocket, RealtimeChannel>? _socketClient;

    [TestInitialize]
    public async Task InitializeTest()
    {
        _restClient = Helpers.RestClient();
        _socketClient = Helpers.SocketClient();
        await _socketClient!.ConnectAsync();
    }

    [TestCleanup]
    public void CleanupTest()
    {
        _socketClient!.Disconnect();
    }

    [TestMethod("Channel: Can listen for broadcast")]
    public async Task ClientCanListenForBroadcast()
    {
        var tsc = new TaskCompletionSource<bool>();
        var tsc2 = new TaskCompletionSource<bool>();

        var guid1 = Guid.NewGuid().ToString();
        var guid2 = Guid.NewGuid().ToString();

        var channel1 = _socketClient!.Channel("online-users");
        var broadcast1 = channel1.Register<BroadcastExample>(true, true);
        broadcast1.AddBroadcastEventHandler(
            (_, _) =>
            {
                var broadcast = broadcast1.Current();
                if (broadcast?.UserId != guid1 && broadcast?.Event == "user")
                    tsc.TrySetResult(true);
            }
        );

        var client2 = Helpers.SocketClient();
        await client2.ConnectAsync();
        var channel2 = client2.Channel("online-users");
        var broadcast2 = channel2.Register<BroadcastExample>(true, true);
        broadcast2.AddBroadcastEventHandler(
            (_, _) =>
            {
                var broadcast = broadcast2.Current();
                if (broadcast?.UserId != guid2 && broadcast?.Event == "user")
                    tsc2.TrySetResult(true);
            }
        );

        await channel1.Subscribe();
        await channel2.Subscribe();

        await broadcast1.Send("user", new BroadcastExample { UserId = guid1 });
        await broadcast2.Send("user", new BroadcastExample { UserId = guid2 });

        await Task.WhenAll(new[] { tsc.Task, tsc2.Task });
    }

    [TestMethod("Channel: Can listen for broadcast")]
    public async Task ClientCanListenForBroadcastPrivate()
    {
        var tsc = new TaskCompletionSource<bool>();
        var tsc2 = new TaskCompletionSource<bool>();

        var guid1 = Guid.NewGuid().ToString();
        var guid2 = Guid.NewGuid().ToString();

        var client1 = Helpers.PrivateSocketClient();
        await client1.ConnectAsync();
        var options1 = new ChannelOptions(
            client1.Options,
            () => Helpers.ApiKey,
            new JsonSerializerSettings(),
            true
        );
        var channel1 = client1.Channel("online-users", options1);
        var broadcast1 = channel1.Register<BroadcastExample>(true, true);
        broadcast1.AddBroadcastEventHandler(
            (_, _) =>
            {
                var broadcast = broadcast1.Current();
                if (broadcast?.UserId != guid1 && broadcast?.Event == "user")
                    tsc.TrySetResult(true);
            }
        );

        var client2 = Helpers.PrivateSocketClient();
        await client2.ConnectAsync();
        var options2 = new ChannelOptions(
            _socketClient!.Options,
            () => Helpers.ApiKey,
            new JsonSerializerSettings(),
            true
        );
        var channel2 = client2.Channel("online-users", options2);
        var broadcast2 = channel2.Register<BroadcastExample>(true, true);
        broadcast2.AddBroadcastEventHandler(
            (_, _) =>
            {
                var broadcast = broadcast2.Current();
                if (broadcast?.UserId != guid2 && broadcast?.Event == "user")
                    tsc2.TrySetResult(true);
            }
        );

        await channel1.Subscribe();
        await channel2.Subscribe();

        await broadcast1.Send("user", new BroadcastExample { UserId = guid1 });
        await broadcast2.Send("user", new BroadcastExample { UserId = guid2 });

        await Task.WhenAll(new[] { tsc.Task, tsc2.Task });
    }

    [TestMethod("Channel: Cannot listen for private broadcast")]
    public async Task ClientCannotListenForBroadcastPrivate()
    {
        var tsc = new TaskCompletionSource<bool>();
        var tsc2 = new TaskCompletionSource<bool>();

        var guid1 = Guid.NewGuid().ToString();
        var guid2 = Guid.NewGuid().ToString();

        var channel1 = _socketClient!.Channel("online-users");
        var broadcast1 = channel1.Register<BroadcastExample>(true, true);
        broadcast1.AddBroadcastEventHandler(
            (_, broadcast) =>
            {
                if (broadcast is not BroadcastExample broad1)
                {
                    tsc.TrySetResult(true);
                    return;
                }

                if (broad1.UserId == guid1 && broad1.Event == "user1")
                    tsc.TrySetResult(true);
            }
        );

        var client2 = Helpers.PrivateSocketClient();
        await client2.ConnectAsync();
        var options = new ChannelOptions(
            client2.Options,
            () => Helpers.ApiKey,
            new JsonSerializerSettings(),
            true
        );
        var channel2 = client2.Channel("online-users", options);
        var broadcast2 = channel2.Register<BroadcastExample>(true, true);
        broadcast2.AddBroadcastEventHandler(
            (sender, broad) =>
            {
                if (broad is not BroadcastExample broadcast)
                {
                    tsc2.TrySetResult(false);
                    return;
                }

                if (broadcast.UserId == guid2 && broadcast.Event == "user2")
                    tsc2.TrySetResult(true);
            }
        );

        await channel1.Subscribe();
        await channel2.Subscribe();

        await broadcast1.Send("user1", new BroadcastExample { UserId = guid1 });
        await broadcast2.Send("user2", new BroadcastExample { UserId = guid2 });

        await Task.WhenAll(new[] { tsc.Task, tsc2.Task });
    }

    [TestMethod("Channel: Can listen history for private broadcast")]
    public async Task ClientCanListenHistoryForBroadcastPrivate()
    {
        var send = new Dictionary<string, object>
        {
            { "event", "user" },
            { "topic", "online-users" },
            { "private", true }
        };
        await _restClient!.Rpc("send", send);
        
        var tsc = new TaskCompletionSource<bool>();

        var client1 = Helpers.PrivateSocketClient();
        await client1.ConnectAsync();
        var options1 = new ChannelOptions(
            client1.Options,
            () => null,
            new JsonSerializerSettings(),
            true
        );
        var broadcastOptions = new BroadcastOptions
        {
            BroadcastAck = true,
            BroadcastSelf = true,
            Replay = new BroadcastOptions.ReplayOptions
            {
                Limit = 10,
                Since = DateTimeOffset.UtcNow.AddDays(-3).ToUnixTimeMilliseconds()
            }
        };
        var channel1 = client1.Channel("online-users", options1);
        var broadcast1 = channel1.Register<BroadcastExample>(broadcastOptions);
        broadcast1.AddBroadcastEventHandler(
            (_, _) =>
            {
                
                var broadcast = broadcast1.Current();
                if (broadcast is { Event: "user", Meta.Replayed: true })
                    tsc.TrySetResult(true);
            }
        );

        await channel1.Subscribe();
        
        await Task.WhenAll(tsc.Task);
    }

    [TestMethod("Channel: Cannot listen broadcast replay on public channel")]
    public async Task ClientCannotListenForBroadcastReplay()
    {
        var client1 = Helpers.PrivateSocketClient();
        await client1.ConnectAsync();
        var options1 = new ChannelOptions(
            client1.Options,
            () => null,
            new JsonSerializerSettings(),
            false
        );
        var broadcastOptions = new BroadcastOptions
        {
            BroadcastAck = true,
            BroadcastSelf = true,
            Replay = new BroadcastOptions.ReplayOptions
            {
                Limit = 10,
                Since = DateTimeOffset.UtcNow.AddDays(-3).ToUnixTimeMilliseconds()
            }
        };
        var channel1 = client1.Channel("online-users", options1);
        var action = () => channel1.Register<BroadcastExample>(broadcastOptions);
        
        Assert.ThrowsException<InvalidOperationException>(action);
    }
    
    [TestMethod("Channel: Payload returns a modeled response (if possible)")]
    public async Task ChannelPayloadReturnsModel()
    {
        var tsc = new TaskCompletionSource<bool>();

        var channel = _socketClient!.Channel("example");
        channel.Register(new PostgresChangesOptions("public", "*"));
        channel.AddPostgresChangeHandler(
            ListenType.Inserts,
            (_, changes) =>
            {
                var model = changes.Model<Todo>();
                tsc.SetResult(model != null);
            }
        );

        await channel.Subscribe();

        await _restClient!
            .Table<Todo>()
            .Insert(new Todo { UserId = 1, Details = "Client Models a response? ✅" });

        var check = await tsc.Task;
        Assert.IsTrue(check);
    }
}
