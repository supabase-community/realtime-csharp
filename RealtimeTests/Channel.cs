﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Postgrest.Interfaces;
using RealtimeTests.Models;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Models;
using static Supabase.Realtime.Constants;

namespace RealtimeTests
{
	public class TimePresence : BasePresence
	{
		[JsonProperty("time")]
		public DateTime? Time { get; set; }
	}

	public class BroadcastExample : BaseBroadcast
	{
		[JsonProperty("userId")]
		public string? UserId { get; set; }
	}

	[TestClass]
	public class Channel
	{
		private IPostgrestClient RestClient;
		private Supabase.Realtime.Client SocketClient;

		[TestInitialize]
		public async Task InitializeTest()
		{
			var session = await Helpers.GetSession();
			RestClient = Helpers.RestClient(session.AccessToken);
			SocketClient = Helpers.SocketClient();

			await SocketClient.ConnectAsync();
			SocketClient.SetAuth(session.AccessToken);
		}

		[TestCleanup]
		public void CleanupTest()
		{
			SocketClient.Disconnect();
		}

		[TestMethod("Channel: Can create presence")]
		public async Task ClientCanCreatePresence()
		{
			var tsc = new TaskCompletionSource<bool>();
			var tsc2 = new TaskCompletionSource<bool>();

			var guid1 = Guid.NewGuid().ToString();
			var guid2 = Guid.NewGuid().ToString();

			var channel1 = SocketClient.Channel("online-users");
			var presence1 = channel1.Register<TimePresence>(guid1);
			presence1.OnSync += (sender, args) =>
			{
				var state = presence1.CurrentState;
				if (state.ContainsKey(guid2) && state[guid2].First().Time != null)
				{
					tsc.SetResult(true);
				}
			};

			var client2 = Helpers.SocketClient();
			await client2.ConnectAsync();
			var channel2 = client2.Channel("online-users");
			var presence2 = channel2.Register<TimePresence>(guid2);
			presence2.OnSync += (sender, args) =>
			{
				var state = presence2.CurrentState;
				if (state.ContainsKey(guid1) && state[guid1].First().Time != null)
				{
					tsc2.SetResult(true);
				}
			};

			await channel1.Subscribe();
			await channel2.Subscribe();

			presence1.Track(new TimePresence { Time = DateTime.Now });
			presence2.Track(new TimePresence { Time = DateTime.Now });

			await Task.WhenAll(new[] { tsc.Task, tsc2.Task });
		}

		[TestMethod("Channel: Can listen for broadcast")]
		public async Task ClientCanListenForBroadcast()
		{
			var tsc = new TaskCompletionSource<bool>();
			var tsc2 = new TaskCompletionSource<bool>();

			var guid1 = Guid.NewGuid().ToString();
			var guid2 = Guid.NewGuid().ToString();

			var channel1 = SocketClient.Channel("online-users");
			var broadcast1 = channel1.Register<BroadcastExample>(true, true);
			broadcast1.OnBroadcast += (sender, args) =>
			{
				var broadcast = broadcast1.Current();
				if (broadcast.UserId != guid1 && broadcast.Event == "user")
				{
					tsc.TrySetResult(true);
				}
			};

			var client2 = Helpers.SocketClient();
			await client2.ConnectAsync();
			var channel2 = client2.Channel("online-users");
			var broadcast2 = channel2.Register<BroadcastExample>(true, true);
			broadcast2.OnBroadcast += (sender, args) =>
			{
				var broadcast = broadcast2.Current();
				if (broadcast.UserId != guid2 && broadcast.Event == "user")
				{
					tsc2.TrySetResult(true);
				}
			};

			await channel1.Subscribe();
			await channel2.Subscribe();

			await broadcast1.Send("user", new BroadcastExample { UserId = guid1 });
			await broadcast2.Send("user", new BroadcastExample { UserId = guid2 });

			await Task.WhenAll(new[] { tsc.Task, tsc2.Task });
		}

		[TestMethod("Channel: Payload returns a modeled response (if possible)")]
		public async Task ChannelPayloadReturnsModel()
		{
			var tsc = new TaskCompletionSource<bool>();

			var channel = SocketClient.Channel("realtime", "public", "*");

			channel.OnInsert += (sender, e) =>
			{
				var model = e.Response.Model<Todo>();
				tsc.SetResult(model is Todo);
			};

			await channel.Subscribe();

			await RestClient.Table<Todo>().Insert(new Todo { UserId = 1, Details = "Client Models a response? ✅" });

			var check = await tsc.Task;
			Assert.IsTrue(check);
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

			var result = await RestClient.Table<Todo>().Order(x => x.InsertedAt, Postgrest.Constants.Ordering.Descending).Get();
			var model = result.Models.First();
			var oldDetails = model.Details;
			var newDetails = $"I'm an updated item ✏️ - {DateTime.Now}";

			var channel = SocketClient.Channel("realtime", "public", "todos");

			channel.OnUpdate += (s, args) =>
			{
				var oldModel = args.Response.OldModel<Todo>();

				Assert.AreEqual(oldDetails, oldModel.Details);

				var updated = args.Response.Model<Todo>();
				Assert.AreEqual(newDetails, updated.Details);
				Assert.AreEqual(model.Id, updated.Id);
				Assert.AreEqual(model.UserId, updated.UserId);

				tsc.SetResult(true);
			};

			await channel.Subscribe();

			await RestClient.Table<Todo>()
				.Set(x => x.Details, newDetails)
				.Match(model)
				.Update();

			var check = await tsc.Task;
			Assert.IsTrue(check);
		}

		[TestMethod("Channel: Receives Delete Callback")]
		public async Task ChannelReceivesDeleteCallback()
		{
			var tsc = new TaskCompletionSource<bool>();

			var channel = SocketClient.Channel("realtime", "public", "todos");

			channel.OnDelete += (s, args) =>
			{
				tsc.SetResult(true);
			};

			await channel.Subscribe();

			var result = await RestClient.Table<Todo>().Get();
			var model = result.Models.Last();

			await RestClient.Table<Todo>().Match(model).Delete();

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

			var serialized = JsonConvert.SerializeObject(channel.JoinPush.Payload);
			Assert.IsTrue(serialized.Contains("\"key\":\"value\""));
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

			channel.OnPostgresChange += (sender, e) =>
			{
				switch (e.Response.Payload.Data.Type)
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
			};

			await channel.Subscribe();

			var modeledResponse = await RestClient.Table<Todo>().Insert(new Todo { UserId = 1, Details = "Client receives wildcard callbacks? ✅" });
			var newModel = modeledResponse.Models.First();

			await RestClient.Table<Todo>().Set(x => x.Details, "And edits.").Match(newModel).Update();
			await RestClient.Table<Todo>().Match(newModel).Delete();

			await Task.WhenAll(tasks);

			Assert.IsTrue(insertTsc.Task.Result);
			Assert.IsTrue(updateTsc.Task.Result);
			Assert.IsTrue(deleteTsc.Task.Result);
		}
	}
}