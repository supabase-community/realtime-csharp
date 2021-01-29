# realtime-csharp (WIP)

---

Realtime-csharp is written as a client library for [supabase/realtime](https://github.com/supabase/realtime).

Documentation can be found [here](https://supabase.github.io/realtime-csharp/api/Supabase.Realtime.Client.html).

The bulk of this library is a translation and c-sharp-ification of the [supabase/realtime-js](https://github.com/supabase/realtime-js) library.

## Status

- [x] Client Connects to Websocket
- [x] Socket Event Handlers
  - [x] Open
  - [x] Close - when channel is explicitly closed by server or by calling `Channel.Unsubscribe()`
  - [x] Error
- [ ] Realtime Event Handlers
  - [x] `INSERT`
  - [ ] `UPDATE`
  - [ ] `DELETE`
  - [x] `*`
- [x] Join channels of format:
  - [x] `{database}`
  - [x] `{database}:{schema}`
  - [x] `{database}:{schema}:{table}`
  - [x] `{database}:{schema}:{table}:{col}.eq.{val}`
- [ ] Responses supply a Generically Typed Model derived from `BaseModel`
- [ ] Ability to remove subscription to Realtime Events
- [x] Ability to disconnect from socket.
- [x] Socket reconnects when possible
- [ ] Unit Tests
- [x] Documentation
- [ ] Nuget Release

## Contributing

We are more than happy to have contributions! Please submit a PR.
