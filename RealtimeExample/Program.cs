using RealtimeExample.Models;
using Supabase.Realtime;
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
            var postgrestClient = Postgrest.Client.Initialize("http://localhost:3000");
            var realtimeClient = Supabase.Realtime.Client.Initialize("ws://localhost:4000/socket");

            //Socket events
            realtimeClient.OnOpen += (s, args) => Console.WriteLine("OPEN");
            realtimeClient.OnClose += (s, args) => Console.WriteLine("CLOSED");
            realtimeClient.OnError += (s, args) => Console.WriteLine("ERROR");

            await realtimeClient.Connect();

            // Subscribe to a channel and events
            var channel = realtimeClient.Channel("realtime", "public", "users");
            channel.OnInsert += (object s, SocketResponseEventArgs args) => Console.WriteLine("New item inserted: " + args.Response.Payload.Record);
            channel.OnUpdate += (object s, SocketResponseEventArgs args) => Console.WriteLine("Item updated: " + args.Response.Payload.Record);
            channel.OnDelete += (object s, SocketResponseEventArgs args) => Console.WriteLine("Item deleted");

            await channel.Subscribe();

            var response = await postgrestClient.Table<User>().Insert(new User { Name = "exampleUser" });
            var user = response.Models.FirstOrDefault();
            user.Name = "exampleUser2.0";
            await user.Update<User>();
            await user.Delete<User>();

            Console.ReadKey();
        }
    }
}
