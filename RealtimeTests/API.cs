using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RealtimeTests.Models;
using Supabase.Realtime;
using static Supabase.Realtime.Channel;

namespace RealtimeTests
{
    [TestClass]
    public class API
    {
        private string socketEndpoint = "ws://localhost:4000/socket";
        private string restEndpoint = "http://localhost:3000";

        private Client SocketClient;
        private Postgrest.Client RestClient => Postgrest.Client.Initialize(restEndpoint);

        public API()
        {
            SocketClient = Client.Initialize(socketEndpoint);
        }


        [TestInitialize]
        public async Task InitializeTest()
        {
            await SocketClient.Connect();
        }

        [TestCleanup]
        public void CleanupTest()
        {
            SocketClient.Disconnect();
        }


        [TestMethod("Client: Join channels of format: {database}")]
        public async Task ClientJoinsChannel_DB()
        {
            var channel = SocketClient.Channel("realtime", "*");
            await channel.Subscribe();

            Assert.AreEqual("realtime:*", channel.Topic);
        }

        [TestMethod("Client: Join channels of format: {database}:{schema}")]
        public async Task ClientJoinsChannel_DB_Schema()
        {
            var channel = SocketClient.Channel("realtime", "public");
            await channel.Subscribe();

            Assert.AreEqual("realtime:public", channel.Topic);
        }

        [TestMethod("Client: Join channels of format: {database}:{schema}:{table}")]
        public async Task ClientJoinsChannel_DB_Schema_Table()
        {
            var channel = SocketClient.Channel("realtime", "public", "users");
            await channel.Subscribe();

            Assert.AreEqual("realtime:public:users", channel.Topic);
        }

        [TestMethod("Client: Join channels of format: {database}:{schema}:{table}:{col}.eq.{val}")]
        public async Task ClientJoinsChannel_DB_Schema_Table_Query()
        {
            var channel = SocketClient.Channel("realtime", "public", "users", "id", "1");
            await channel.Subscribe();

            Assert.AreEqual("realtime:public:users:id.eq.1", channel.Topic);
        }

        [TestMethod("Channel: Receives Insert Callback")]
        public async Task ChannelReceivesInsertCallback()
        {
            var tsc = new TaskCompletionSource<bool>();

            var channel = SocketClient.Channel("realtime", "public", "todos");

            channel.OnInsert += (s, args) => tsc.SetResult(true);

            await channel.Subscribe();
            await RestClient.Table<Todo>().Insert(new Todo { UserId = 1, Details = "Client receives insert callback? ✅" });

            Assert.IsTrue(await tsc.Task);
        }

        [TestMethod("Channel: Receives Update Callback")]
        public async Task ChannelReceivesUpdateCallback()
        {
            var tsc = new TaskCompletionSource<bool>();

            var channel = SocketClient.Channel("realtime", "public", "todos");

            channel.OnUpdate += (s, args) => tsc.SetResult(true);

            await channel.Subscribe();

            var result = await RestClient.Table<Todo>().Get();
            var model = result.Models.Last();
            model.Details = "I'm an updated item ✏️";

            await model.Update<Todo>();

            Assert.IsTrue(await tsc.Task);
        }

        [TestMethod("Channel: Receives Delete Callback")]
        public async Task ChannelReceivesDeleteCallback()
        {
            var tsc = new TaskCompletionSource<bool>();

            var channel = SocketClient.Channel("realtime", "public", "todos");

            channel.OnDelete += (s, args) => tsc.SetResult(true);

            await channel.Subscribe();

            var result = await RestClient.Table<Todo>().Get();
            var model = result.Models.Last();

            await model.Delete<Todo>();

            Assert.IsTrue(await tsc.Task);
        }

        [TestMethod("Channel: Receives '*' Callback")]
        public async Task ChannelReceivesWildcardCallback()
        {
            var insertTsc = new TaskCompletionSource<bool>();
            var updateTsc = new TaskCompletionSource<bool>();
            var deleteTsc = new TaskCompletionSource<bool>();

            List<Task> tasks = new List<Task> { insertTsc.Task, updateTsc.Task, deleteTsc.Task };

            var channel = SocketClient.Channel("realtime", "public", "todos");

            channel.OnMessage += (object sender, SocketResponseEventArgs e) =>
            {
                switch (e.Response.Payload.Type)
                {
                    case "INSERT":
                        insertTsc.SetResult(true);
                        break;
                    case "UPDATE":
                        updateTsc.SetResult(true);
                        break;
                    case "DELETE":
                        deleteTsc.SetResult(true);
                        break;
                }
            };

            await channel.Subscribe();

            var modeledResponse = await RestClient.Table<Todo>().Insert(new Todo { UserId = 1, Details = "Client receives wildcard callbacks? ✅" });
            var newModel = modeledResponse.Models.First();

            newModel.Details = "And edits.";

            await newModel.Update<Todo>();

            await newModel.Delete<Todo>();

            await Task.WhenAll(tasks);

            Assert.IsTrue(insertTsc.Task.Result);
            Assert.IsTrue(updateTsc.Task.Result);
            Assert.IsTrue(deleteTsc.Task.Result);
        }

        [TestMethod("Client: Returns a single instance of a channel based on topic")]
        public async Task ClientReturnsSingleChannelInstance()
        {
            var channel1 = SocketClient.Channel("realtime", "public", "todos");

            await channel1.Subscribe();

            // Client should return an instance of `realtime:public:todos` that is already joined.
            var channel2 = SocketClient.Channel("realtime", "public", "todos");

            Assert.AreEqual(true, channel2.IsJoined);
        }

        [TestMethod("Client: Removes Channel Subscriptions")]
        public async Task ClientCanRemoveChannelSubscription()
        {
            var channel1 = SocketClient.Channel("realtime", "public", "todos");
            await channel1.Subscribe();

            // Removing channel should remove the stored instance, so a future instance would need
            // to resubscribe.
            SocketClient.Remove(channel1);

            var channel2 = SocketClient.Channel("realtime", "public", "todos");
            Assert.AreEqual(ChannelState.Closed, channel2.State);
        }

        [TestMethod("Channel: Payload returns a modeled response (if possible)")]
        public async Task ChannelPayloadReturnsModel()
        {
            var tsc = new TaskCompletionSource<bool>();

            var channel = SocketClient.Channel("realtime", "public", "todos");

            channel.OnInsert += (object sender, SocketResponseEventArgs e) =>
            {
                var model = e.Response.Model<Todo>();
                tsc.SetResult(model is Todo);
            };

            await channel.Subscribe();

            await RestClient.Table<Todo>().Insert(new Todo { UserId = 1, Details = "Client Models a response? ✅" });

            var result = await tsc.Task;
            Assert.IsTrue(result);
        }

        [TestMethod("Channel: Payload Model parses a proper timestamp")]
        public async Task ChannelPayloadModelParsesTimestamp()
        {
            var tsc = new TaskCompletionSource<bool>();

            var timestamp = DateTime.UtcNow;

            var channel = SocketClient.Channel("realtime", "public", "todos");

            channel.OnInsert += (object sender, SocketResponseEventArgs e) =>
            {
                var model = e.Response.Model<Todo>();
                Debug.WriteLine($"{timestamp.ToLongTimeString()} should equal {model.InsertedAt.ToLongTimeString()}");
                tsc.SetResult(timestamp.ToLongTimeString() == model.InsertedAt.ToLongTimeString());
            };

            await channel.Subscribe();

            await RestClient.Table<Todo>().Insert(new Todo { UserId = 1, Details = "Client Receives Timestamp? ✅", InsertedAt = timestamp });

            var result = await tsc.Task;
            Assert.IsTrue(result);
        }

        [TestMethod("Channel: Close Event Handler")]
        public async Task ChannelCloseEventHandler()
        {
            var tsc = new TaskCompletionSource<bool>();

            var channel = SocketClient.Channel("realtime", "public", "todos");
            channel.OnClose += (object sender, ChannelStateChangedEventArgs args) =>
            {
                tsc.SetResult(ChannelState.Closed == args.State);
            };
            await channel.Subscribe();
            channel.Unsubscribe();

            var result = await tsc.Task;

            Assert.IsTrue(result);
        }
    }
}
