using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Supabase.Realtime;
using Supabase.Realtime.Broadcast;
using Supabase.Realtime.Channel;

namespace RealtimeTests;

/// <summary>
/// Client-side validation of broadcast replay registration. These tests exercise the guard on
/// <see cref="RealtimeChannel.Register{TBroadcastResponse}(BroadcastOptions)"/> without a live
/// server: the channel is built directly against an unconnected socket, so no stack is required.
/// </summary>
[TestClass]
public class ChannelBroadcastReplayTests
{
    [TestMethod("Channel: Registering broadcast replay on a public channel throws")]
    public void ClientCannotRegisterReplayOnPublicChannel()
    {
        var channel = PublicChannel();

        Assert.ThrowsException<InvalidOperationException>(
            () => channel.Register<BroadcastExample>(WithReplay()));
    }

    [TestMethod("Channel: Registering broadcast replay on a private channel is allowed")]
    public void ClientCanRegisterReplayOnPrivateChannel()
    {
        var channel = PrivateChannel();

        var broadcast = channel.Register<BroadcastExample>(WithReplay());

        Assert.IsNotNull(broadcast);
    }

    private static BroadcastOptions WithReplay() => new()
    {
        Replay = new BroadcastOptions.ReplayOptions { Limit = 10, Since = 0 }
    };

    private static RealtimeChannel PublicChannel() =>
        Channel(ChannelOptions.Public(ClientOptions(), () => null, new JsonSerializerSettings()));

    private static RealtimeChannel PrivateChannel() =>
        Channel(ChannelOptions.Private(ClientOptions(), () => null, new JsonSerializerSettings()));

    private static ClientOptions ClientOptions() => new();

    private static RealtimeChannel Channel(ChannelOptions options)
    {
        var socket = new RealtimeSocket("ws://localhost:54321/realtime/v1", options.ClientOptions);

        return new RealtimeChannel(socket, "realtime:online-users", options);
    }
}
