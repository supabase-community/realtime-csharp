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

Realtime-csharp is written as a client library for [supabase/realtime](https://github.com/supabase/realtime).

Documentation can be found [here](https://supabase-community.github.io/realtime-csharp/api/Supabase.Realtime.Client.html).

The bulk of this library is a translation and c-sharp-ification of the [supabase/realtime-js](https://github.com/supabase/realtime-js) library.

**The Websocket-sharp implementation that Realtime-csharp is dependent on does _not_ support TLS1.3**

## Getting Started

Care was had to make this API as _easy<sup>tm</sup>_ to interact with as possible. `Connect()` and `Subscribe()` have `await`-able signatures
which allow Users to be assured that a connection exists prior to interacting with it.

```c#
var endpoint = "ws://localhost:3000";
client = Client.Initialize(endpoint);

await client.Connect();

var channel = client.Channel("realtime", "public", "users");

// Per Event Callbacks
channel.OnInsert += (sender, args) => Console.WriteLine("New item inserted: " + args.Response.Payload.Record);
channel.OnUpdate += (sender, args) => Console.WriteLine("Item updated: " + args.Response.Payload.Record);
channel.OnDelete += (sender, args) => Console.WriteLine("Item deleted");

// Callback for any event, INSERT, UPDATE, or DELETE
channel.OnMessage += (sender, args) => Debug.WriteLine(args.Message.Event);

await channel.Subscribe();
```

Leveraging `Postgrest.BaseModel`s, one ought to be able to coerce SocketResponse Records into their associated models by calling:
```c#
// ...
var channel = client.Channel("realtime", "public", "users");

channel.OnInsert += (sender, args) => {
    var model = args.Response.Model<User>();
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
