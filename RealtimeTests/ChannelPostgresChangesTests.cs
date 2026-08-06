using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Postgrest.Interfaces;
using RealtimeTests.Models;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.PostgresChanges;
using static Supabase.Realtime.Constants;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

namespace RealtimeTests;

[TestClass]
public class ChannelPostgresChangesTests
{
    private IPostgrestClient? _restClient;
    private IRealtimeClient<RealtimeSocket, RealtimeChannel>? _socketClient;

    [TestInitialize]
    public async Task InitializeTest()
    {
        _restClient = Helpers.RestClient();
        _socketClient = Helpers.SocketClient();
        await _socketClient!.ConnectAsync();
    }

    [TestCleanup]
    public void CleanupTest()
    {
        _socketClient!.Disconnect();
    }

    [TestMethod(DisplayName = "Channel: Payload returns a modeled response (if possible)")]
    public async Task ChannelPayloadReturnsModel()
    {
        var tsc = new TaskCompletionSource<bool>();

        var channel = _socketClient!.Channel("example");
        channel.OnPostgresChange((_, changes) =>
        {
            var model = changes.Model<Todo>();
            tsc.SetResult(model != null);
        }, ListenType.Inserts, new PostgresChangesFilter { Table = "*" });

        await channel.Subscribe();

        await _restClient!.Table<Todo>().Insert(new Todo { UserId = 1, Details = "Client Models a response? ✅" });

        var check = await tsc.Task;
        Assert.IsTrue(check);
    }

    [TestMethod(DisplayName = "Channel: Receives Insert Callback")]
    public async Task ChannelReceivesInsertCallback()
    {
        var tsc = new TaskCompletionSource<bool>();

        var channel = _socketClient!.Channel("realtime", "public", "todos");

        channel.OnPostgresChange((_, _) => tsc.SetResult(true), ListenType.Inserts,
            new PostgresChangesFilter { Table = "todos" });

        await channel.Subscribe();
        await _restClient!.Table<Todo>()
            .Insert(new Todo { UserId = 1, Details = "Client receives insert callback? ✅" });

        var check = await tsc.Task;
        Assert.IsTrue(check);
    }

    [TestMethod(DisplayName = "Channel: Receives Filtered Insert Callback")]
    public async Task ChannelReceivesInsertCallbackFiltered()
    {
        var tsc = new TaskCompletionSource<bool>();

        var channel = _socketClient!.Channel("realtime", "public", "todos");

        channel.OnPostgresChange((_, changes) =>
        {
            var oldModel = changes.Model<Todo>();

            Assert.AreEqual("Client receives filtered insert callback? ✅", oldModel?.Details);

            tsc.SetResult(true);
        }, ListenType.Inserts,
            new PostgresChangesFilter { Table = "todos", Filter = "details=eq.Client receives filtered insert callback? ✅" });

        await channel.Subscribe();
        await _restClient!.Table<Todo>()
            .Insert(new Todo { UserId = 1, Details = "Client receives insert callback? ✅" });

        await _restClient!.Table<Todo>()
            .Insert(new Todo { UserId = 2, Details = "Client receives filtered insert callback? ✅" });

        var check = await tsc.Task;
        Assert.IsTrue(check);
    }

    [TestMethod(DisplayName = "Channel: Receives Filtered Two Callback")]
    public async Task ChannelReceivesTwoCallbacks()
    {
        var tsc = new TaskCompletionSource<bool>();

        var response = await _restClient!.Table<Todo>()
            .Insert(new Todo { UserId = 1, Details = "Client receives insert callback? ✅" });
        await _restClient!.Table<Todo>()
            .Insert(new Todo { UserId = 2, Details = "Client receives filtered insert callback? ✅" });

        var model = response.Models.First();
        var oldDetails = model.Details;
        var newDetails = $"I'm an updated item ✏️ - {DateTime.Now}";

        var channel = _socketClient!.Channel("realtime", "public", "todos");
        channel.OnPostgresChange((_, changes) =>
        {
            var oldModel = changes.OldModel<Todo>();

            Assert.AreEqual(oldDetails, oldModel?.Details);

            var updated = changes.Model<Todo>();
            Assert.AreEqual(newDetails, updated?.Details);

            if (updated != null)
            {
                Assert.AreEqual(model.Id, updated.Id);
                Assert.AreEqual(model.UserId, updated.UserId);
            }

            tsc.SetResult(true);
        }, ListenType.Updates, new PostgresChangesFilter { Table = "todos" });

        const string filter = "Client receives filtered insert callback? ✅";
        channel.OnPostgresChange((_, changes) =>
        {
            var insertedModel = changes.Model<Todo>();

            Assert.AreEqual("Client receives filtered insert callback? ✅", insertedModel?.Details);

            tsc.SetResult(true);
        }, ListenType.Inserts, new PostgresChangesFilter { Table = "todos", Filter = $"details=eq.{filter}" });

        await channel.Subscribe();

        await _restClient.Table<Todo>()
            .Set(x => x.Details!, newDetails)
            .Match(model)
            .Update();

        var check = await tsc.Task;
        Assert.IsTrue(check);
    }

    [TestMethod(DisplayName = "Channel: Receives Update Callback")]
    public async Task ChannelReceivesUpdateCallback()
    {
        var tsc = new TaskCompletionSource<bool>();

        var response = await _restClient!.Table<Todo>()
            .Insert(new Todo { UserId = 1, Details = "Client receives insert callback? ✅" });

        var model = response.Models.First();
        var oldDetails = model.Details;
        var newDetails = $"I'm an updated item ✏️ - {DateTime.Now}";

        var channel = _socketClient!.Channel("realtime", "public", "todos");

        channel.OnPostgresChange((_, changes) =>
        {
            var oldModel = changes.OldModel<Todo>();

            Assert.AreEqual(oldDetails, oldModel?.Details);

            var updated = changes.Model<Todo>();
            Assert.AreEqual(newDetails, updated?.Details);

            if (updated != null)
            {
                Assert.AreEqual(model.Id, updated.Id);
                Assert.AreEqual(model.UserId, updated.UserId);
            }

            tsc.SetResult(true);
        }, ListenType.Updates, new PostgresChangesFilter { Table = "todos" });

        await channel.Subscribe();

        await _restClient.Table<Todo>()
            .Set(x => x.Details!, newDetails)
            .Match(model)
            .Update();

        var check = await tsc.Task;
        Assert.IsTrue(check);
    }

    [TestMethod(DisplayName = "Channel: Receives Delete Callback")]
    public async Task ChannelReceivesDeleteCallback()
    {
        var tsc = new TaskCompletionSource<bool>();

        var channel = _socketClient!.Channel("realtime", "public", "todos");

        channel.OnPostgresChange((_, _) => tsc.SetResult(true), ListenType.Deletes,
            new PostgresChangesFilter { Table = "todos" });

        await channel.Subscribe();

        var result = await _restClient!.Table<Todo>().Get();
        var model = result.Models.Last();

        await _restClient.Table<Todo>().Match(model).Delete();

        var check = await tsc.Task;
        Assert.IsTrue(check);
    }

    [TestMethod(DisplayName = "Channel: Receives Delete Callback")]
    public async Task ChannelReceivesFilteredDeleteCallback()
    {
        var tsc = new TaskCompletionSource<bool>();
        var channel = _socketClient!.Channel("realtime", "public", "todos");

        var todo1 = await _restClient!.Table<Todo>().Insert(new Todo
            { UserId = 1, Details = "Client receives callbacks 1? ✅" });
        var todo2 = await _restClient!.Table<Todo>().Insert(new Todo
            { UserId = 2, Details = "Client receives callbacks 2? ✅" });
        await _restClient!.Table<Todo>().Insert(new Todo
            { UserId = 3, Details = "Client receives callbacks 3? ✅" });

        channel.OnPostgresChange((_, removed) =>
        {
            var result = removed.OldModel<Todo>();
            Assert.AreEqual(result?.Details, todo1.Model?.Details);
            Assert.AreNotEqual(result?.Details, todo2.Model?.Details);

            tsc.SetResult(true);
        }, ListenType.Deletes,
            new PostgresChangesFilter { Table = "todos", Filter = $"details=eq.{todo1.Model?.Details}" });

        await channel.Subscribe();

        await _restClient.Table<Todo>().Match(todo1.Models.First()).Delete();
        await _restClient.Table<Todo>().Match(todo2.Models.First()).Delete();

        var check = await tsc.Task;
        Assert.IsTrue(check);
    }

    [TestMethod(DisplayName = "Channel: Receives '*' Callback")]
    public async Task ChannelReceivesWildcardCallback()
    {
        var insertTsc = new TaskCompletionSource<bool>();
        var updateTsc = new TaskCompletionSource<bool>();
        var deleteTsc = new TaskCompletionSource<bool>();

        List<Task> tasks = new List<Task> { insertTsc.Task, updateTsc.Task, deleteTsc.Task };

        var channel = _socketClient!.Channel("realtime", "public", "todos");

        channel.OnPostgresChange((_, changes) =>
        {
            switch (changes.Payload?.Data?.Type)
            {
                case EventType.Insert:
                    insertTsc.SetResult(true);
                    break;
                case EventType.Update:
                    updateTsc.SetResult(true);
                    break;
                case EventType.Delete:
                    deleteTsc.SetResult(true);
                    break;
            }
        }, ListenType.All, new PostgresChangesFilter { Table = "todos" });

        await channel.Subscribe();

        var modeledResponse = await _restClient!.Table<Todo>().Insert(new Todo
            { UserId = 1, Details = "Client receives wildcard callbacks? ✅" });
        var newModel = modeledResponse.Models.First();

        await _restClient.Table<Todo>().Set(x => x.Details!, "And edits.").Match(newModel).Update();
        await _restClient.Table<Todo>().Match(newModel).Delete();

        await Task.WhenAll(tasks);

        Assert.IsTrue(insertTsc.Task.Result);
        Assert.IsTrue(updateTsc.Task.Result);
        Assert.IsTrue(deleteTsc.Task.Result);
    }

    [TestMethod(DisplayName = "Channel: Receives Several Same Callback")]
    public async Task ChannelReceivesSeveralSameCallback()
    {
        var insertTask1 = new TaskCompletionSource<bool>();
        var insertTask2 = new TaskCompletionSource<bool>();
        var insertTask3 = new TaskCompletionSource<bool>();
        const string filter1 = "Client receives callbacks 1? ✅";
        const string filter2 = "Client receives callbacks 2? ✅";

        var channel = _socketClient!.Channel("realtime", "public", "todos");

        var count = 0;
        channel.OnPostgresChange((_, added) =>
        {
            count++;
            if (count == 3) insertTask1.TrySetResult(true);
        }, ListenType.Inserts, new PostgresChangesFilter { Table = "todos" });

        channel.OnPostgresChange((_, added) =>
        {
            var model = added.Model<Todo>();

            insertTask2.SetResult(model?.Details == filter1);
        }, ListenType.Inserts, new PostgresChangesFilter { Table = "todos", Filter = $"details=eq.{filter1}" });

        channel.OnPostgresChange((_, added) =>
        {
            var model = added.Model<Todo>();

            insertTask3.SetResult(model?.Details == filter2);
        }, ListenType.Inserts, new PostgresChangesFilter { Table = "todos", Filter = $"details=eq.{filter2}" });

       await channel.Subscribe();

       await _restClient!.Table<Todo>().Insert(new Todo { UserId = 1, Details = "Client receives wildcard callbacks? ✅" });
       await _restClient!.Table<Todo>().Insert(new Todo { UserId = 1, Details = filter1 });
       await _restClient!.Table<Todo>().Insert(new Todo { UserId = 1, Details = filter2 });

       await Task.WhenAll(insertTask1.Task, insertTask2.Task, insertTask3.Task);

       Assert.IsTrue(insertTask1.Task.Result);
       Assert.IsTrue(insertTask2.Task.Result);
       Assert.IsTrue(insertTask3.Task.Result);
    }

    [TestMethod]
    public async Task OnPostgresChange_ShouldRegisterAndDeliverToHandler_GivenChainedSubscribe()
    {
        var tsc = new TaskCompletionSource<bool>();

        await _socketClient!.Channel("public:todos")
            .OnPostgresChange((_, changes) => tsc.TrySetResult(changes.Model<Todo>() != null),
                ListenType.Inserts, new PostgresChangesFilter { Table = "todos" })
            .Subscribe();

        await _restClient!.Table<Todo>()
            .Insert(new Todo { UserId = 1, Details = "OnPostgresChange receives insert? ✅" });

        Assert.IsTrue(await WithinTimeout(tsc.Task),
            "OnPostgresChange should register the option and bind the handler in one call, and return the channel so Subscribe can be chained");
    }

    private static async Task<bool> WithinTimeout(Task<bool> task, int timeoutMs = 15000)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
        return completed == task && task.Result;
    }

}
