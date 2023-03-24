# Changelog

## 5.0.4 - 2023-03-23

- Re: [#26](https://github.com/supabase-community/realtime-csharp/pull/26) - Fixes Connect() not returning callback result when the socket isn't null. Thanks [@BlueWaterCrystal](https://github.com/BlueWaterCrystal)!

## 5.0.3 - 2023-03-09

- Re: [#25](https://github.com/supabase-community/realtime-csharp/issues/25) - Support Channel being resubscribed after having been unsubscribed, fixes rejoin timer being erroneously called on channel `Unsubscribe`. Thanks [@Kuffs2205](https://github.com/Kuffs2205)!

## 5.0.2 - 2023-03-02

- Re: [#24](https://github.com/supabase-community/realtime-csharp/issues/24) - Fixes join failing until reconnect happened + adds access token push on channel join. Big thank you to [@Honeyhead](https://github.com/honeyhead) for the help debugging and identifying!

## 5.0.1 - 2023-02-06

- Re: [#22](https://github.com/supabase-community/realtime-csharp/issues/22) - `SerializerSettings` were not being passed to `PostgresChangesResponse` - Thanks [@Shenrak](https://github.com/Shenrak) for the help debugging!

## 5.0.0 - 2023-01-31

- Re: [#21](https://github.com/supabase-community/realtime-csharp/pull/21) Provide API for `presence`, `broadcast` and `postgres_changes`
	- [Major, New] `Channel.PostgresChanges` event will receive the wildcard `*` changes event, not `Channel.OnMessage`.
	- [Major] `Channel.OnInsert`, `Channel.OnUpdate`, and `Channel.OnDelete` now conform to the server's payload of `Response.Payload.**Data**`
	- [Major] `Channel.OnInsert`, `Channel.OnUpdate`, and `Channel.OnDelete` now return `PostgresChangesEventArgs`
	- [Minor] Rename `Channel` to `RealtimeChannel`
	- Supports better handling of disconnects in `RealtimeSocket` and adds a `Client.OnReconnect` event.
	- [Minor] Moves `ChannelOptions` to `Channel.ChannelOptions`
	- [Minor] Moves `ChannelStateChangedEventArgs` to `Channel.ChannelStateChangedEventArgs`
	- [Minor] Moves `Push` to `Channel.Push`
	- [Minor] Moves `Channel.ChannelState` to `Constants.ChannelState`
	- [Minor] Moves `SocketResponse`, `SocketRequest`, `SocketResponsePayload`, `SocketResponseEventArgs`, and `SocketStateChangedEventArgs` to `Socket` namespace.
	- [New] Adds `RealtimeBroadcast`
	- [New] Adds `RealtimePresence`
	- [Improvement] Better handling of disconnection/reconnection

## 4.0.1 - 2022-11-08

- Bugfixes on previous release.

## 4.0.0 - 2022-11-08

- Re: [#17](https://github.com/supabase-community/realtime-csharp/pull/17) Restructure Project to support Dependency Injection and Enable Nullity
	- `Client` is no longer a singleton class.
	- `Channel` has a new constructor that uses `ChannelOptions`
	- `Channel.Parameters` has been changed in favor of `Channel.Options`
	- `Channel` and `Push` are now directly dependent on having `Socket` and `SerializerSettings` passed in as opposed to referencing the `Singleton` instance.
	- All publicly facing classes (that offer functionality) now include an Interface.

## 3.0.1 - 2022-05-28

- Fixed deserialization of `DateTimes`

## 3.0.0 - 2022-02-18

- Exchange existing websocket client: [WebSocketSharp](https://github.com/sta/websocket-sharp) for [Marfusios/websocket-client](https://github.com/Marfusios/websocket-client) which adds support for Blazor WASM apps.
  Ref: [#14](https://github.com/supabase-community/realtime-csharp/pull/14)

## 2.0.8 - 2021-12-30

- [#12](https://github.com/supabase-community/realtime-csharp/issues/12): Implement Upstream Realtime RLS Error Broadcast Handling
- `SocketResponse` now exposes a method: `OldModel`, that hydrates the `OldRecord` property into a model.

## 2.0.7 - 2021-12-25

- [#11](https://github.com/supabase-community/realtime-csharp/issues/11) `user_token` Channel parameter is now set in the `SetAuth` call.

## 2.0.6 - 2021-11-29

- Bugfix introduced by 2.0.5, remove exposed `Client.Instance.subscriptions`

## 2.0.5 - 2021-11-29

- Fixed test for (`Client: Join channels of format: {database}:{schema}:{table}:{col}=eq.{val}`)
- Add support for WALRUS `AccessToken` Pushes on every heartbeat see [#12](https://github.com/supabase-community/supabase-csharp/issues/12)
