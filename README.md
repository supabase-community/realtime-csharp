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

Documentation can be found [here](https://supabase.github.io/realtime-csharp/api/Supabase.Realtime.Client.html).

The bulk of this library is a translation and c-sharp-ification of the [supabase/realtime-js](https://github.com/supabase/realtime-js) library.

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

| <img src="https://github.com/acupofjose.png" width="150" height="150"> |
| :--------------------------------------------------------------------: |
|              [acupofjose](https://github.com/acupofjose)               |

## Contributing

We are more than happy to have contributions! Please submit a PR.
