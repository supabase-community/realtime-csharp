using RealtimeExample.Models;
using Supabase.Realtime;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.PostgresChanges;
using Supabase.Realtime.Socket;
using static Supabase.Realtime.Constants;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

namespace RealtimeExample
{
    class Program
    {
        private const string ApiKey =
            "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiIiLCJpYXQiOjE2NzEyMzc4NzMsImV4cCI6MjAwMjc3Mzk5MywiYXVkIjoiIiwic3ViIjoiIiwicm9sZSI6ImF1dGhlbnRpY2F0ZWQifQ.qoYdljDZ9rjfs1DKj5_OqMweNtj7yk20LZKlGNLpUO8";

        private const string SocketEndpoint = "ws://realtime-dev.localhost:4000/socket";

        static async Task Main(string[] args)
        {
            // Connect to db and web socket server
            var postgrestClient = new Supabase.Postgrest.Client("http://localhost:3000");
            var realtimeClient = new Client(SocketEndpoint, new ClientOptions
            {
                Parameters = new SocketOptionsParameters
                {
                    ApiKey = ApiKey
                }
            });

            realtimeClient.AddDebugHandler((sender, message, exception) => Console.WriteLine(message));
            realtimeClient.AddStateChangedHandler(SocketEventHandler);

            await realtimeClient.ConnectAsync();

            var secretKey = "61fa867d-a98c-41b1-a202-5588f879a0cc";
            var filterKey = "4f76aba7-89ed-41a7-80f0-a130333ab9f2";
            var channel = realtimeClient.Channel("realtime", "public", "revit_events");

            
           channel.Register(new PostgresChangesOptions("public", table: "revit_events", eventType: ListenType.Inserts, filter: $"secret_key=eq.{secretKey}"));
            channel.AddPostgresChangeHandler(ListenType.Inserts, PostgresInsertedHandler);
            channel.AddPostgresChangeHandler(ListenType.Updates, PostgresUpdatedHandler);
            channel.AddPostgresChangeHandler(ListenType.Deletes, PostgresDeletedHandler);
            await channel.Subscribe();

            // await RegisterRevit(channel);
            // Subscribe to a channel and events
            // var channelTodos = realtimeClient.Channel("public:todos");
            // channelTodos.Register(new PostgresChangesOptions("public", "todos"));
            // channelTodos.AddPostgresChangeHandler(ListenType.Inserts, PostgresInsertedHandler);
            // channelTodos.AddPostgresChangeHandler(ListenType.Updates, PostgresUpdatedHandler);
            // channelTodos.AddPostgresChangeHandler(ListenType.Deletes, PostgresDeletedHandler);
            // await channelTodos.Subscribe();

            Console.ReadKey();
        }

        private static void PostgresInsertedHandler(IRealtimeChannel _, PostgresChangesResponse change)
        {
            var secretKey = "61fa867d-a98c-41b1-a202-5588f879a0cc";
            Debug.WriteLine($"Event: {change.Event}");
            Debug.WriteLine(
                $"Data: {JsonConvert.SerializeObject(change.Model<RevitEvent>(), Formatting.Indented)}");
            var newRecord = change.Model<RevitEvent>();
            if (newRecord == null) return;

            if (newRecord.secret_key != secretKey)
            {
                Debug.WriteLine($"Secret key mismatch");
                return;
            }
        }

        private static void PostgresDeletedHandler(IRealtimeChannel _, PostgresChangesResponse change)
        {
            Console.WriteLine($"Item Deleted");
        }

        private static void PostgresUpdatedHandler(IRealtimeChannel _, PostgresChangesResponse change)
        {
            Console.WriteLine($"Item Updated: {change.Model<RevitEvent>()}");
        }

        // private static void PostgresInsertedHandler(IRealtimeChannel _, PostgresChangesResponse change)
        // {
        // Console.WriteLine($"New item inserted: {change.Model<RevitEvent>()}");
        // }

        private static void SocketEventHandler(IRealtimeClient<RealtimeSocket, RealtimeChannel> sender,
            SocketState state)
        {
            Debug.WriteLine($"Socket is ${state.ToString()}");
        }

        private static async Task RegisterRevit(Supabase.Realtime.RealtimeChannel client)
        {
            var revit1 = new RevitEvent()
            {
                Id = 1,
                secret_key = "61fa867d-a98c-41b1-a202-5588f879a0cc",
                BaseUrl = "https://localhost:5001",
                event_name = "revit_event",
                from_frontend = false,
            };
            var revit2 = new RevitEvent()
            {
                Id = 2,
                secret_key = Guid.NewGuid().ToString(),
                BaseUrl = "https://localhost:5001",
                event_name = "revit_event",
                from_frontend = false,
            };

            client.Push(ChannelEventName.PostgresChanges.ToString(), ListenType.Inserts.ToString(), revit2);

            //await client.Table<RevitEvent>().Insert(revit1);
            //await client.Table<RevitEvent>().Insert(revit2);
        }
    }
}