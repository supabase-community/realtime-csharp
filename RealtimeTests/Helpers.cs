using Supabase.Gotrue;
using Supabase.Realtime;
using Supabase.Realtime.Socket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client = Supabase.Realtime.Client;

namespace RealtimeTests
{
    internal static class Helpers
    {
        private const string ApiKey =
            "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiIiLCJpYXQiOjE2NzEyMzc4NzMsImV4cCI6MjAwMjc3Mzk5MywiYXVkIjoiIiwic3ViIjoiIiwicm9sZSI6ImF1dGhlbnRpY2F0ZWQifQ.qoYdljDZ9rjfs1DKj5_OqMweNtj7yk20LZKlGNLpUO8";

        private static readonly string SocketEndpoint = $"ws://realtime-dev.localhost:4000/socket";
        private static readonly string RestEndpoint = "http://localhost:3000";

        public static Postgrest.Client RestClient() => new(RestEndpoint, new Postgrest.ClientOptions());

        public static Client SocketClient()
        {
            var client = new Client(SocketEndpoint, new ClientOptions
            {
                Parameters = new SocketOptionsParameters
                {
                    ApiKey = ApiKey
                }
            });

            return client;
        }
    }
}