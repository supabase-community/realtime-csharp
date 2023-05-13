using RealtimeExample.Models;
using Supabase.Realtime;
using Supabase.Realtime.Channel;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Supabase.Realtime.Interfaces;
using static Supabase.Realtime.Constants;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

namespace RealtimeExample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Connect to db and web socket server
            var postgrestClient = new Postgrest.Client("http://localhost:3000");
            var realtimeClient = new Client("ws://localhost:4000/socket");

            realtimeClient.AddStateChangedListener(SocketEventHandler);

            await realtimeClient.ConnectAsync();

            // Subscribe to a channel and events
            var channelUsers = realtimeClient.Channel("realtime", "public", "users");
            channelUsers.AddPostgresChangeHandler(ListenType.Inserts,
                (_, change) => { Console.WriteLine($"New item inserted: {change.Model<User>()}"); });
            channelUsers.AddPostgresChangeHandler(ListenType.Updates,
                (_, change) => { Console.WriteLine($"Item Updated: {change.Model<User>()}"); });
            channelUsers.AddPostgresChangeHandler(ListenType.Deletes,
                (_, change) => { Console.WriteLine($"Item Deleted"); });

            Console.WriteLine("Subscribing to users channel");
            await channelUsers.Subscribe();

            //Subscribing to another channel
            var channelTodos = realtimeClient.Channel("realtime", "public", "todos");
            
            channelTodos.AddStateChangedHandler((_, state) =>
            {
                Console.WriteLine($"Channel todos {state}!!");
            });
            Console.WriteLine("Subscribing to todos channel");
            await channelTodos.Subscribe();

            //Unsubscribing from channelTodos to trigger the OnClose event
            channelTodos.Unsubscribe();

            Console.WriteLine($"Users channel state after unsubscribing from todos channel: {channelUsers.State}");

            var response = await postgrestClient.Table<User>().Insert(new User { Name = "exampleUser" });
            var user = response.Models.FirstOrDefault();
            user.Name = "exampleUser2.0";
            await user.Update<User>();
            await user.Delete<User>();

            Console.ReadKey();
        }

        private static void SocketEventHandler(IRealtimeClient<RealtimeSocket, RealtimeChannel> sender,
            SocketState state)
        {
            Debug.WriteLine($"Socket is ${state.ToString()}");
        }
    }
}