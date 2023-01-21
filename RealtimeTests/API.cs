using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RealtimeTests.Models;
using Supabase.Realtime;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Socket;
using static Supabase.Realtime.Constants;

namespace RealtimeTests
{
    [TestClass]
    public class API
    {
        private string socketEndpoint = "ws://localhost:4000/socket";
        private string restEndpoint = "http://localhost:3000";

        private Postgrest.Client RestClient => new Postgrest.Client(restEndpoint);
        private Client SocketClient;


        [TestInitialize]
        public async Task InitializeTest()
        {
            SocketClient = new Client(socketEndpoint);
            await SocketClient.ConnectAsync();
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

        [TestMethod("Client: Join channels of format: {database}:{schema}:{table}:{col}=eq.{val}")]
        public async Task ClientJoinsChannel_DB_Schema_Table_Query()
        {
            var channel = SocketClient.Channel("realtime", "public", "users", "id", "1");
            await channel.Subscribe();

            Assert.AreEqual("realtime:public:users:id=eq.1", channel.Topic);
        }

        [TestMethod("Channel: Receives Insert Callback")]
        public async Task ChannelReceivesInsertCallback()
        {
            var tsc = new TaskCompletionSource<bool>();

            var channel = SocketClient.Channel("realtime", "public", "todos");

            channel.OnInsert += (s, args) => tsc.SetResult(true);

            await channel.Subscribe();
            await RestClient.Table<Todo>().Insert(new Todo { UserId = 1, Details = "Client receives insert callback? ✅" });

            var check = await tsc.Task;
            Assert.IsTrue(check);
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

            var check = await tsc.Task;
            Assert.IsTrue(check);
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

            var check = await tsc.Task;
            Assert.IsTrue(check);
        }

        [TestMethod("Channel: Supports WALRUS Array Changes")]
        public async Task ChannelSupportsWALRUSArray()
        {
            Todo result = null;
            var tsc = new TaskCompletionSource<bool>();

            var channel = SocketClient.Channel("realtime", "public", "todos");
            var numbers = new List<int> { 4, 5, 6 };

            await channel.Subscribe();

            channel.OnInsert += (s, args) =>
            {
                result = args.Response.Model<Todo>();
                tsc.SetResult(true);
            };

            await RestClient.Table<Todo>().Insert(new Todo { UserId = 1, Numbers = numbers });

            await tsc.Task;
            CollectionAssert.AreEqual(numbers, result.Numbers);
        }

        [TestMethod("Channel: Sends Join parameters")]
        public async Task ChannelSendsJoinParameters()
        {
            var parameters = new Dictionary<string, string> { { "key", "value" } };
            var channel = SocketClient.Channel("realtime", "public", "todos", parameters: parameters);

            await channel.Subscribe();

            Assert.AreEqual(parameters, channel.JoinPush.Message.Payload);
        }

        [TestMethod("Channel: Returns single subscription per unique topic.")]
        public async Task ChannelJoinsDuplicateSubscription()
        {
            var subscription1 = SocketClient.Channel("realtime", "public", "todos");
            var subscription2 = SocketClient.Channel("realtime", "public", "todos");
            var subscription3 = SocketClient.Channel("realtime", "public", "todos", "user_id", "1");

            Assert.AreEqual(subscription1.Topic, subscription2.Topic);

            await subscription1.Subscribe();

            Assert.AreEqual(subscription1.HasJoinedOnce, subscription2.HasJoinedOnce);
            Assert.AreNotEqual(subscription1.HasJoinedOnce, subscription3.HasJoinedOnce);

            var subscription4 = SocketClient.Channel("realtime", "public", "todos");

            Assert.AreEqual(subscription1.HasJoinedOnce, subscription4.HasJoinedOnce);
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

            var channel = SocketClient.Channel("realtime", "*");

            channel.OnInsert += (object sender, SocketResponseEventArgs e) =>
            {
                var model = e.Response.Model<Todo>();
                tsc.SetResult(model is Todo);
            };

            await channel.Subscribe();

            await RestClient.Table<Todo>().Insert(new Todo { UserId = 1, Details = "Client Models a response? ✅" });

            var check = await tsc.Task;
            Assert.IsTrue(check);
        }

        [TestMethod("Channel: Deserializes Datetimes in Model")]
        public async Task ChannelDeserializesDateTimes()
        {
            var tsc = new TaskCompletionSource<bool>();

            var insertedAt = DateTime.Now;
            var expected = insertedAt.ToLocalTime().ToLongTimeString();

            var channel = SocketClient.Channel("realtime", "public", "todos");

            channel.OnInsert += (object sender, SocketResponseEventArgs e) =>
            {
                var model = e.Response.Model<Todo>();
                var actual = model.InsertedAt.ToLocalTime().ToLongTimeString();

                Assert.AreEqual(expected, actual);

                tsc.SetResult(true);
            };

            await channel.Subscribe();

            await Task.Delay(2000);

            var todo = new Todo { UserId = 1, Details = "Client Models a response? ✅", InsertedAt = insertedAt };
            var dbResponse = await RestClient.Table<Todo>().Insert(todo);

            Assert.AreEqual(expected, dbResponse.Models[0].InsertedAt.ToLocalTime().ToLongTimeString());

            var check = await tsc.Task;
            Assert.IsTrue(check);
        }

        [TestMethod("Client: SetsAuth")]
        public async Task ClientSetsAuth()
        {
            var channel = SocketClient.Channel("realtime", "public", "todos");
            var channel2 = SocketClient.Channel("realtime", "public", "users");

            var token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.C8oVtF5DICct_4HcdSKt8pdrxBFMQOAnPpbiiUbaXAY";

            // No subscriptions should show a push
            SocketClient.SetAuth(token);
            foreach (var subscription in SocketClient.Subscriptions.Values)
            {
                Assert.IsNull(subscription.LastPush);
            }

            await channel.Subscribe();
            await channel2.Subscribe();

            SocketClient.SetAuth(token);
            foreach (var subscription in SocketClient.Subscriptions.Values)
            {
                Assert.IsTrue(subscription.LastPush.EventName == Constants.CHANNEL_ACCESS_TOKEN);
            }
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

            var check = await tsc.Task;
            Assert.IsTrue(check);
        }

        [TestMethod("Client: Can reconnect after programmatic disconnect")]
        public async Task ClientCanReconnectAfterProgrammaticDisconnect()
        {
            SocketClient.Disconnect();
            await SocketClient.ConnectAsync();
        }
    }
}
