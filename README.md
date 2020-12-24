# realtime-csharp (WIP)

---

Realtime-csharp is written as a client library for [supabase/realtime](https://github.com/supabase/realtime).

The bulk of this library is a translation and c-sharp-ification of the [supabase/realtime-js](https://github.com/supabase/realtime-js) library.

## Status

- [x] Client Connects to Websocket
- [ ] Socket Event Handlers
  - [ ] Open
  - [ ] Close - when channel is explicitly closed by server or by calling `Channel.Unsubscribe()`
  - [ ] Error
- [ ] Realtime Event Handlers
  - [ ] `INSERT`
  - [ ] `UPDATE`
  - [ ] `DELETE`
  - [ ] `*`
- [ ] Join channels of format:
  - [ ] `{database}`
  - [ ] `{database}:{schema}`
  - [ ] `{database}:{schema}:{table}`
  - [ ] `{database}:{schema}:{table}:{col}.eq.{val}`
- [ ] Responses supply a Generically Typed Model derived from `BaseModel`
- [ ] Ability to remove subscription to Realtime Events
- [ ] Ability to disconnect from socket.
- [ ] Socket reconnects when possible
- [ ] Unit Tests
- [ ] Documentation
- [ ] Nuget Release

## Contributing

We are more than happy to have contributions! Please submit a PR.
