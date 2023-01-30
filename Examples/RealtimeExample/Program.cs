using RealtimeExample.Models;
using Supabase.Realtime;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Socket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RealtimeExample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Connect to db and web socket server
            var postgrestClient = new Postgrest.Client("http://localhost:3000");
            var realtimeClient = new Client("ws://localhost:4000/socket");

            //Socket events
            realtimeClient.OnOpen += (s, args) => Console.WriteLine("OPEN");
            realtimeClient.OnClose += (s, args) => Console.WriteLine("CLOSED");
            realtimeClient.OnError += (s, args) => Console.WriteLine("ERROR");

            await realtimeClient.ConnectAsync();

            // Subscribe to a channel and events
            var channelUsers = realtimeClient.Channel("realtime", "public", "users");
            channelUsers.OnInsert += (s, args) => Console.WriteLine("New item inserted: " + args.Response.Payload.Data.Record);
            channelUsers.OnUpdate += (s, args) => Console.WriteLine("Item updated: " + args.Response.Payload.Data.Record);
            channelUsers.OnDelete += (s, args) => Console.WriteLine("Item deleted");

            Console.WriteLine("Subscribing to users channel");
            await channelUsers.Subscribe();

            //Subscribing to another channel
            var channelTodos = realtimeClient.Channel("realtime", "public", "todos");
            channelTodos.OnClose += (object sender, ChannelStateChangedEventArgs args) => Console.WriteLine($"Channel todos { args.State}!!");
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
    }
}
