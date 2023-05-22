<p align="center">
<img width="300" src=".github/logo.png"/>
</p>
<p align="center">
  <img src="https://github.com/supabase/realtime-csharp/workflows/Build%20And%20Test/badge.svg"/>
  <a href="https://www.nuget.org/packages/realtime-csharp/">
    <img src="https://img.shields.io/badge/dynamic/json?color=green&label=Nuget%20Release&query=data[0].version&url=https%3A%2F%2Fazuresearch-usnc.nuget.org%2Fquery%3Fq%3Dpackageid%3Arealtime-csharp"/>
  </a>
</p>

---

## BREAKING CHANGES MOVING FROM v5.x.x to v6.x.x

- The realtime client now takes a "fail-fast" approach. On establishing an initial connection, client will throw
  a `RealtimeException` in `ConnectAsync()` if the socket server is unreachable. After an initial connection has been
  established, the **client will continue attempting reconnections indefinitely until disconnected.**
- [Major, New] C# `EventHandlers` have been changed to `delegates`. This should allow for cleaner event data access over
  the previous subclassed `EventArgs` setup. Events are scoped accordingly. For example, the `RealtimeSocket` error
  handlers will receive events regarding socket connectivity; whereas the `RealtimeChannel` error handlers will receive
  events according to `Channel` joining/leaving/etc. This is implemented with the following methods prefixed by (
  Add/Remove/Clear):
    - `RealtimeBroadcast.AddBroadcastEventHandler`
    - `RealtimePresence.AddPresenceEventHandler`
    - `RealtimeSocket.AddStateChangedHandler`
    - `RealtimeSocket.AddMessageReceivedHandler`
    - `RealtimeSocket.AddHeartbeatHandler`
    - `RealtimeSocket.AddErrorHandler`
    - `RealtimeClient.AddDebugHandler`
    - `RealtimeClient.AddStateChangedHandler`
    - `RealtimeChannel.AddPostgresChangeHandler`
    - `RealtimeChannel.AddMessageReceivedHandler`
    - `RealtimeChannel.AddErrorHandler`
    - `Push.AddMessageReceivedHandler`
- [Major, new] `ClientOptions.Logger` has been removed in favor of `Client.AddDebugHandler()` which allows for
  implementing custom logging solutions if desired.
  - A simple logger can be set up with the following:
  ```c#
  client.AddDebugHandler((sender, message, exception) => Debug.WriteLine(message));
  ```
- [Major] `Connect()` has been marked `Obsolete` in favor of `ConnectAsync()`
- Custom reconnection logic has been removed in favor of using the built-in logic from `Websocket.Client@4.6.1`.
- Exceptions that are handled within this library have been marked as `RealtimeException`s.
- The local, docker-composed test suite has been brought back (as opposed to remotely testing on live supabase servers)
  to test against.
- Comments have been added throughout the entire codebase and an `XML` file is now generated on build.

## BREAKING CHANGES MOVING FROM v4.x.x to v5.x.x

**See realtime-csharp in action [here](https://multiplayer-csharp.azurewebsites.net/).**

## Changes:

- [Major, New] `Channel.PostgresChanges` event will receive the wildcard `*` changes event, not `Channel.OnMessage`.
- [Major] `Channel.OnInsert`, `Channel.OnUpdate`, and `Channel.OnDelete` now conform to the server's payload
  of `Response.Payload.**Data**`
- [Major] `Channel.OnInsert`, `Channel.OnUpdate`, and `Channel.OnDelete` now return `PostgresChangesEventArgs`
- [Minor] Rename `Channel` to `RealtimeChannel`
- Supports better handling of disconnects in `RealtimeSocket` and adds a `Client.OnReconnect` event.
- [Minor] Moves `ChannelOptions` to `Channel.ChannelOptions`
- [Minor] Moves `ChannelStateChangedEventArgs` to `Channel.ChannelStateChangedEventArgs`
- [Minor] Moves `Push` to `Channel.Push`
- [Minor] Moves `Channel.ChannelState` to `Constants.ChannelState`
- [Minor] Moves `SocketResponse`, `SocketRequest`, `SocketResponsePayload`, `SocketResponseEventArgs`,
  and `SocketStateChangedEventArgs` to `Socket` namespace.
- [New] Adds `RealtimeBroadcast`
- [New] Adds `RealtimePresence`
- [Improvement] Better handling of disconnection/reconnection

---

`realtime-csharp` is written as a client library for [supabase/realtime](https://github.com/supabase/realtime).

Documentation can be
found [here](https://supabase-community.github.io/realtime-csharp/api/Supabase.Realtime.Client.html).

The bulk of this library is a translation and c-sharp-ification of
the [supabase/realtime-js](https://github.com/supabase/realtime-js) library.

**The Websocket-sharp implementation that Realtime-csharp is dependent on does _not_ support TLS1.3**

## Getting Started

Care was had to make this API as _easy<sup>tm</sup>_ to interact with as possible. `Connect()` and `Subscribe()`
have `await`-able signatures
which allow Users to be assured that a connection exists prior to interacting with it.

```c#
var endpoint = "ws://localhost:3000";
client = new Client(endpoint);

await client.Connect();

var channel = client.Channel("realtime", "public", "users");

// Per Event Callbacks
channel.OnInsert += (sender, args) => Console.WriteLine("New item inserted: " + args.Response.Payload.Record);
channel.OnUpdate += (sender, args) => Console.WriteLine("Item updated: " + args.Response.Payload.Record);
channel.OnDelete += (sender, args) => Console.WriteLine("Item deleted");

// Callback for any event, INSERT, UPDATE, or DELETE
channel.OnPostgresChange += (sender, args) => Debug.WriteLine(args.Message.Event);

await channel.Subscribe();
```

Leveraging `Postgrest.BaseModel`s, one ought to be able to coerce SocketResponse Records into their associated models by
calling:

```c#
// ...
var channel = client.Channel("realtime", "public", "users");

channel.OnInsert += (sender, args) => {
    var model = args.Response.Model<User>();
};

await channel.Subscribe();
```

## Broadcast

"Broadcast follows the publish-subscribe pattern where a client publishes messages to a channel with a unique
identifier. For example, a user could send a message to a channel with id room-1.

Other clients can elect to receive the message in real-time by subscribing to the channel with id room-1. If these
clients are online and subscribed then they will receive the message.

Broadcast works by connecting your client to the nearest Realtime server, which will communicate with other servers to
relay messages to other clients.

A common use-case is sharing a user's cursor position with other clients in an online game."

[Find more information here](https://supabase.com/docs/guides/realtime#broadcast)

**Given the following model (`CursorBroadcast`):**

```c#
class MouseBroadcast : BaseBroadcast<MouseStatus> { }
class MouseStatus
{
	[JsonProperty("mouseX")]
	public float MouseX { get; set; }

	[JsonProperty("mouseY")]
	public float MouseY { get; set; }

	[JsonProperty("userId")]
	public string UserId { get; set; }
}
```

**Listen for typed broadcast events**:

```c#
var channel = supabase.Realtime.Channel("cursor");

var broadcast = channel.Register<MouseBroadcast>(false, true);
broadcast<MouseBroadcast>().OnBroadcast += (sender, args) =>
{
	var state = broadcast.Current();
	Debug.WriteLine($"{state.Payload}: {state.Payload.MouseX}:{state.Payload.MouseY}");
};
await channel.Subscribe();
```

**Broadcast an event**:

```c#
var channel = supabase.Realtime.Channel("cursor");
var data = new CursorBroadcast { Event = "cursor", Payload = new MouseStatus { MouseX = 123, MouseY = 456 } };
channel.Send(ChannelType.Broadcast, data);
```

## Presence

"Presence utilizes an in-memory conflict-free replicated data type (CRDT) to track and synchronize shared state in an
eventually consistent manner. It computes the difference between existing state and new state changes and sends the
necessary updates to clients via Broadcast.

When a new client subscribes to a channel, it will immediately receive the channel's latest state in a single message
instead of waiting for all other clients to send their individual states.

Clients are free to come-and-go as they please, and as long as they are all subscribed to the same channel then they
will all have the same Presence state as each other.

The neat thing about Presence is that if a client is suddenly disconnected (for example, they go offline), their state
will be automatically removed from the shared state. If you've ever tried to build an “I'm online” feature which handles
unexpected disconnects, you'll appreciate how useful this is."

[Find more information here](https://supabase.com/docs/guides/realtime#presence)

**Given the following model: (`UserPresence`)**

```c#
class UserPresence: BasePresence
{
    [JsonProperty("lastSeen")]
    public DateTime LastSeen { get; set; }
}
```

**Listen for typed presence events**:

```c#
var presenceId = Guid.NewGuid().ToString();

var channel = supabase.Realtime.Channel("last-seen");
var presence = channel.Register<UserPresence>(presenceId);
presence.OnSync += (sender, args) =>
{
	foreach (var state in presence.CurrentState)
	{
                var userId = state.Key;
                var lastSeen = state.Value.First().LastSeen;
		Debug.WriteLine($"{userId}: {lastSeen}");
	}
};
await channel.Subscribe();
```

**Track a user presence event**:

```c#
var presenceId = Guid.NewGuid().ToString();
var channel = supabase.Realtime.Channel("last-seen");

var presence = channel.Register<UserPresence>(presenceId);
presence.Track(new UserPresence { LastSeen = DateTime.Now });
```

## Postgres Changes

"Postgres Changes enable you to listen to database changes and have them broadcast to authorized clients based
on [Row Level Security (RLS)](https://supabase.com/docs/guides/auth/row-level-security) policies.

This works by Realtime polling your database's logical replication slot for changes, passing those changes to
the [apply_rls](https://github.com/supabase/walrus#reading-wal) SQL function to determine which clients have permission,
and then using Broadcast to send those changes to clients.

Realtime requires a publication called `supabase_realtime` to determine which tables to poll. You must add tables to
this publication prior to clients subscribing to channels that want to listen for database changes.

We strongly encourage you to enable RLS on your database tables and have RLS policies in place to prevent unauthorized
parties from accessing your data."

[Find More Information here](https://supabase.com/docs/guides/realtime#postgres-changes)

**Using the new `Register` method:**

```c#
var channel = supabase.Realtime.Channel("public-users");
channel.Register(new PostgresChangesOptions("public", "users"));
channel.PostgresChanges += (sender, args) =>
{
	switch (args.Response.Data.Type)
	{
		case EventType.Insert:
			// Handle user created
			break;
		case EventType.Update:
			// Handle user updated
			break;
		case EventType.Delete:
			// Handle user deleted
			break;
	}
};
await channel.Subscribe();
```

## Status

- [x] Client Connects to Websocket
- [x] Socket Event Handlers
    - [x] Open
    - [x] Close - when channel is explicitly closed by server or by calling `Channel.Unsubscribe()`
    - [x] Error
- [x] Realtime Event Handlers
    - [x] `INSERT`
    - [x] `UPDATE`
    - [x] `DELETE`
    - [x] `*`
- [x] Join channels of format:
    - [x] `{database}`
    - [x] `{database}:{schema}`
    - [x] `{database}:{schema}:{table}`
    - [x] `{database}:{schema}:{table}:{col}.eq.{val}`
- [x] Responses supply a Generically Typed Model derived from `BaseModel`
- [x] Ability to remove subscription to Realtime Events
- [x] Ability to disconnect from socket.
- [x] Socket reconnects when possible
- [x] Unit Tests
- [x] Documentation
- [x] Nuget Release

## Package made possible through the efforts of:

Join the ranks! See a problem? Help fix it!

<a href="https://github.com/supabase-community/realtime-csharp/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=supabase-community/realtime-csharp" />
</a>

Made with [contrib.rocks](https://contrib.rocks/preview?repo=supabase-community%2Frealtime-csharp).

## Contributing

We are more than happy to have contributions! Please submit a PR.

## Testing

Note that the latest versions of `supabase/realtime` expect to be able to access a subdomain matching the tenant. For
the case of testing, this means that `realtime-dev.localhost:4000` should be available. To have tests run locally,
please add a hosts entry on your system for: `127.0.0.1  realtime-dev.localhost`
