using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Postgrest.Interfaces;
using RealtimeTests.Models;
using Supabase.Realtime;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Models;
using Supabase.Realtime.Presence;

namespace RealtimeTests
{
	public class TimePresence : BasePresence
	{
		[JsonProperty("time")]
		public DateTime? Time { get; set; }
	}

	[TestClass]
	public class Channel
	{
		private IPostgrestClient RestClient;
		private Client SocketClient;

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

		[TestMethod("Client: Can create presence")]
		public async Task ClientCanCreatePresence()
		{
			var tsc = new TaskCompletionSource<bool>();
			var tsc2 = new TaskCompletionSource<bool>();

			var guid1 = Guid.NewGuid().ToString();
			var guid2 = Guid.NewGuid().ToString();

			var channel1 = SocketClient.Channel("online-users");
			channel1.Register<TimePresence>(new PresenceOptions(guid1));
			channel1.Presence<TimePresence>().OnSync += (sender, args) =>
			{
				if (channel1.Presence<TimePresence>().CurrentState.ContainsKey(guid2))
				{
					tsc.SetResult(true);
				}
			};

			var client2 = Helpers.SocketClient();
			await client2.ConnectAsync();
			var channel2 = client2.Channel("online-users");
			channel2.Register<TimePresence>(new PresenceOptions(guid2));
			channel2.Presence<TimePresence>().OnSync += (sender, args) =>
			{
				if (channel2.Presence<TimePresence>().CurrentState.ContainsKey(guid1))
				{
					tsc2.SetResult(true);
				}
			};

			await channel1.Subscribe();
			await channel2.Subscribe();

			channel1.Track(new TimePresence { Time = DateTime.Now });
			channel2.Track(new TimePresence { Time = DateTime.Now });

			await Task.WhenAll(new [] { tsc.Task, tsc2.Task });
		}
	}
}