# Changelog

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
