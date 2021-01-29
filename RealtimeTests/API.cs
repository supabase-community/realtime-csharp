using System;
using System.Collections.Generic;
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
        private Postgrest.Client RestClient => Postgrest.Client.Instance.Initialize(restEndpoint, new Postgrest.ClientAuthorization());

        public API()
        {
            SocketClient = Client.Instance.Initialize(socketEndpoint);
        }


        [TestInitialize]
        public async Task InitializeTest()
        {
            var tsc = new TaskCompletionSource<bool>();

            EventHandler<SocketStateChangedEventArgs> callback = null;
            callback = (sender, args) =>
            {
                SocketClient.OnOpen -= callback;
                tsc.SetResult(true);
            };
            SocketClient.OnOpen += callback;
            SocketClient.Connect();
            await tsc.Task;
        }

        [TestCleanup]
        public void CleanupTest()
        {
            SocketClient.Disconnect();
        }


        [TestMethod("Join channels of format: {database}")]
        public Task ClientJoinsChannel_DB()
        {

            var tsc = new TaskCompletionSource<bool>();

            var channel = SocketClient.Channel("realtime", "*");
            channel.StateChanged += (s, args) =>
            {
                tsc.SetResult(args.State == Channel.ChannelState.Joined);
            };

            channel.Subscribe();

            return tsc.Task;
        }

        [TestMethod("Join channels of format: {database}:{schema}")]
        public Task ClientJoinsChannel_DB_Schema()
        {

            var tsc = new TaskCompletionSource<bool>();

            var channel = SocketClient.Channel("realtime", "public");
            channel.StateChanged += (s, args) =>
            {
                tsc.SetResult(args.State == Channel.ChannelState.Joined);
            };

            channel.Subscribe();

            return tsc.Task;
        }

        [TestMethod("Join channels of format: {database}:{schema}:{table}")]
        public Task ClientJoinsChannel_DB_Schema_Table()
        {

            var tsc = new TaskCompletionSource<bool>();

            var channel = SocketClient.Channel("realtime", "public", "users");
            channel.StateChanged += (s, args) =>
            {
                tsc.SetResult(args.State == Channel.ChannelState.Joined);
            };

            channel.Subscribe();

            return tsc.Task;
        }

        [TestMethod("Join channels of format: {database}:{schema}:{table}:{col}.eq.{val}")]
        public Task ClientJoinsChannel_DB_Schema_Table_Query()
        {

            var tsc = new TaskCompletionSource<bool>();

            var channel = SocketClient.Channel("realtime", "public", "users", "id", "1");
            channel.StateChanged += (s, args) =>
            {
                tsc.SetResult(args.State == Channel.ChannelState.Joined);
            };

            channel.Subscribe();

            return tsc.Task;
        }

        [TestMethod("Channel: Receives Insert Callback")]
        public Task ChannelReceivesInsertCallback()
        {
            var tsc = new TaskCompletionSource<bool>();

            var channel = SocketClient.Channel("realtime", "public", "todos");

            EventHandler<ItemInsertedEventArgs> callback = null;
            callback = (s, args) =>
            {
                channel.OnInsert -= callback;
                tsc.SetResult(true);
            };
            channel.OnInsert += callback;

            channel.StateChanged += async (os, args) =>
            {
                if (args.State == Channel.ChannelState.Joined)
                {
                    var result = await RestClient.Table<Todo>().Insert(new Todo { UserId = 1, Details = "Client receives insert callback? ✅" });
                }
            };

            channel.Subscribe();

            return tsc.Task;
        }

        [TestMethod("Channel: Receives Update Callback")]
        public Task ChannelReceivesUpdateCallback()
        {
            var tsc = new TaskCompletionSource<bool>();

            var channel = SocketClient.Channel("realtime", "public", "todos");

            EventHandler<ItemUpdatedEventArgs> callback = null;
            callback = (s, args) =>
            {
                channel.OnUpdate -= callback;
                tsc.SetResult(true);
            };
            channel.OnUpdate += callback;
            channel.StateChanged += async (os, args) =>
            {
                if (args.State == Channel.ChannelState.Joined)
                {
                    var item = await RestClient.Table<Todo>().Get();
                    var model = item.Models.Last();
                    model.Details = "I'm an updated item ✏️";
                    await model.Update<Todo>();
                }
            };

            channel.Subscribe();

            return tsc.Task;
        }

        [TestMethod("Channel: Receives Delete Callback")]
        public Task ChannelReceivesDeleteCallback()
        {
            var tsc = new TaskCompletionSource<bool>();

            var channel = SocketClient.Channel("realtime", "public", "todos");

            EventHandler<ItemDeletedEventArgs> callback = null;
            callback = (s, args) =>
            {
                channel.OnDelete -= callback;
                tsc.SetResult(true);
            };
            channel.OnDelete += callback;

            channel.StateChanged += async (os, args) =>
            {
                if (args.State == Channel.ChannelState.Joined)
                {
                    var item = await RestClient.Table<Todo>().Get();
                    var model = item.Models.Last();
                    await model.Delete<Todo>();
                }
            };

            channel.Subscribe();

            return tsc.Task;
        }

        [TestMethod("Channel: Receives '*' Callback")]
        public Task ChannelReceivesWildcardCallback()
        {
            var insertTsc = new TaskCompletionSource<bool>();
            var updateTsc = new TaskCompletionSource<bool>();
            var deleteTsc = new TaskCompletionSource<bool>();
            List<Task> tasks = new List<Task> { insertTsc.Task, updateTsc.Task, deleteTsc.Task };

            var channel = SocketClient.Channel("realtime", "public", "todos");

            channel.OnMessage += (object sender, SocketResponseEventArgs e) =>
            {
                if (e.Message.Event == "*")
                {
                    switch (e.Message.Payload.Type)
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
                }
            };

            channel.StateChanged += async (os, args) =>
            {
                if (args.State == Channel.ChannelState.Joined)
                {
                    var modeledResponse = await RestClient.Table<Todo>().Insert(new Todo { UserId = 1, Details = "Client receives wildcard callbacks? ✅" });
                    var newModel = modeledResponse.Models.First();
                    newModel.Details = "And edits.";
                    await newModel.Update<Todo>();
                    await newModel.Delete<Todo>();
                }
            };

            channel.Subscribe();

            return Task.WhenAll(tasks);
        }
    }
}
