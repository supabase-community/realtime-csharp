﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Postgrest.Interfaces;
using RealtimeTests.Models;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;
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

    [TestMethod("Channel: Payload returns a modeled response (if possible)")]
    public async Task ChannelPayloadReturnsModel()
    {
        var tsc = new TaskCompletionSource<bool>();

        await _socketClient!.Channel("example")
            .OnPostgresChange((_, changes) =>
            {
                var model = changes.Model<Todo>();
                tsc.SetResult(model != null);
            }, ListenType.Inserts)
            .Subscribe();

        await _restClient!.Table<Todo>().Insert(new Todo { UserId = 1, Details = "Client Models a response? ✅" });

        var check = await tsc.Task;
        Assert.IsTrue(check);
    }

    [TestMethod("Channel: Receives Insert Callback")]
    public async Task ChannelReceivesInsertCallback()
    {
        var tsc = new TaskCompletionSource<bool>();

        await _socketClient!.Channel("realtime:public:todos")
            .OnPostgresChange((_, _) => tsc.SetResult(true), ListenType.Inserts, table: "todos")
            .Subscribe();

        await _restClient!.Table<Todo>()
            .Insert(new Todo { UserId = 1, Details = "Client receives insert callback? ✅" });

        var check = await tsc.Task;
        Assert.IsTrue(check);
    }

    [TestMethod("Channel: Receives Update Callback")]
    public async Task ChannelReceivesUpdateCallback()
    {
        var tsc = new TaskCompletionSource<bool>();

        var response = await _restClient!.Table<Todo>()
            .Insert(new Todo { UserId = 1, Details = "Client receives insert callback? ✅" });

        var model = response.Models.First();
        var oldDetails = model.Details;
        var newDetails = $"I'm an updated item ✏️ - {DateTime.Now}";

        await _socketClient!.Channel("realtime:public:todos")
            .OnPostgresChange((_, changes) =>
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
        }, ListenType.Updates, table: "todos")
            .Subscribe();

        await _restClient.Table<Todo>()
            .Set(x => x.Details!, newDetails)
            .Match(model)
            .Update();

        var check = await tsc.Task;
        Assert.IsTrue(check);
    }

    [TestMethod("Channel: Receives Delete Callback")]
    public async Task ChannelReceivesDeleteCallback()
    {
        var tsc = new TaskCompletionSource<bool>();

        await _socketClient!.Channel("realtime:public:todos")
            .OnPostgresChange((_, _) => tsc.SetResult(true), ListenType.Deletes, table: "todos")
            .Subscribe();

        var result = await _restClient!.Table<Todo>().Get();
        var model = result.Models.Last();

        await _restClient.Table<Todo>().Match(model).Delete();

        var check = await tsc.Task;
        Assert.IsTrue(check);
    }

    [TestMethod("Channel: Receives '*' Callback")]
    public async Task ChannelReceivesWildcardCallback()
    {
        var insertTsc = new TaskCompletionSource<bool>();
        var updateTsc = new TaskCompletionSource<bool>();
        var deleteTsc = new TaskCompletionSource<bool>();

        List<Task> tasks = new List<Task> { insertTsc.Task, updateTsc.Task, deleteTsc.Task };

        await _socketClient!.Channel("realtime:public:todos").OnPostgresChange((_, changes) =>
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

        }, ListenType.All, table: "todos").Subscribe();

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

    [TestMethod("Channel: Receives Multiple Handlers")]
    public async Task ChannelReceivesMultipleHandlers()
    {
        var insertTsc = new TaskCompletionSource<bool>();
        var updateTsc = new TaskCompletionSource<bool>();
        var deleteTsc = new TaskCompletionSource<bool>();
        var allHandlerTsc = new TaskCompletionSource<bool>();
        var filterHandlerTsc = new TaskCompletionSource<bool>();

        var insertHandlerCalledCount = 0;
        var updateHandlerCalledCount = 0;
        var deleteHandlerCalledCount = 0;
        var allHandlerCalledCount = 0;
        var filterHandlerCalledCount = 0;

        var channel = _socketClient!.Channel("realtime:public:todos");

        channel.OnPostgresChange((_, changes) =>
        {
            if (changes.Payload?.Data?.Type == EventType.Insert)
            {
                insertHandlerCalledCount += 1;
                insertTsc.SetResult(true);
            }
        }, ListenType.Inserts, table: "todos");

        channel.OnPostgresChange((_, changes) =>
        {
            if (changes.Payload?.Data?.Type == EventType.Update)
            {
                updateHandlerCalledCount += 1;
                updateTsc.SetResult(true);
            }
        }, ListenType.Updates, table: "todos");

        channel.OnPostgresChange((_, changes) =>
        {
            if (changes.Payload?.Data?.Type == EventType.Delete)
            {
                deleteHandlerCalledCount += 1;
                deleteTsc.SetResult(true);
            }
        }, ListenType.Deletes, table: "todos");

        channel.OnPostgresChange((_, _) =>
        {
            allHandlerCalledCount += 1;
            allHandlerTsc.SetResult(true);
        }, ListenType.All, table: "todos");

        channel.OnPostgresChange((_, changes) =>
        {
            filterHandlerCalledCount += 1;
            filterHandlerTsc.SetResult(true);
        }, ListenType.Updates, table: "todos");

        await channel.Subscribe();

        var modeledResponse = await _restClient!.Table<Todo>().Insert(new Todo
        { UserId = 1, Details = "Testing multiple handlers" });
        var newModel = modeledResponse.Models.First();

        await _restClient.Table<Todo>().Set(x => x.Details!, "Filtered update").Match(newModel).Update();
        await _restClient.Table<Todo>().Set(x => x.Details!, "Another update").Match(newModel).Update();
        await _restClient.Table<Todo>().Match(newModel).Delete();

        await Task.WhenAll(insertTsc.Task, updateTsc.Task, deleteTsc.Task, allHandlerTsc.Task, filterHandlerTsc.Task);

        Assert.AreEqual(insertHandlerCalledCount, 1);
        Assert.AreEqual(updateHandlerCalledCount, 2);
        Assert.AreEqual(deleteHandlerCalledCount, 1);

        Assert.AreEqual(allHandlerCalledCount, 4);
        Assert.AreEqual(filterHandlerCalledCount, 1);
    }
}