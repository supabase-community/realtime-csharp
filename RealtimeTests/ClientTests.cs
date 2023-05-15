using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Gotrue;
using static Supabase.Realtime.Constants;

namespace RealtimeTests
{
	[TestClass]
	public class ClientTests
	{
		private Supabase.Realtime.Client? socketClient;

		[TestInitialize]
		public async Task InitializeTest()
		{
			socketClient = Helpers.SocketClient();
			await socketClient!.ConnectAsync();
		}

		[TestCleanup]
		public void CleanupTest()
		{
			socketClient?.Disconnect();
		}


		[TestMethod("Client: Join channels of format: {database}")]
		public async Task ClientJoinsChannel_DB()
		{
			var channel = socketClient!.Channel("realtime", "*");
			await channel.Subscribe();

			Assert.AreEqual("realtime:*", channel.Topic);
		}

		[TestMethod("Client: Join channels of format: {database}:{schema}")]
		public async Task ClientJoinsChannel_DB_Schema()
		{
			var channel = socketClient!.Channel(schema: "public");
			await channel.Subscribe();

			Assert.AreEqual("realtime:public", channel.Topic);
		}

		[TestMethod("Client: Join channels of format: {database}:{schema}:{table}")]
		public async Task ClientJoinsChannel_DB_Schema_Table()
		{
			var channel = socketClient!.Channel("realtime", "public", "users");
			await channel.Subscribe();

			Assert.AreEqual("realtime:public:users", channel.Topic);
		}

		[TestMethod("Client: Join channels of format: {database}:{schema}:{table}:{col}=eq.{val}")]
		public async Task ClientJoinsChannel_DB_Schema_Table_Query()
		{
			var channel = socketClient!.Channel("realtime", "public", "users", "id", "1");
			await channel.Subscribe();

			Assert.AreEqual("realtime:public:users:id=eq.1", channel.Topic);
		}

		[TestMethod("Client: Returns a single instance of a channel based on topic")]
		public async Task ClientReturnsSingleChannelInstance()
		{
			var channel1 = socketClient!.Channel("realtime", "public", "todos");

			await channel1.Subscribe();

			// Client should return an instance of `realtime:public:todos` that is already joined.
			var channel2 = socketClient!.Channel("realtime", "public", "todos");

			Assert.AreEqual(true, channel2.IsJoined);
		}

		[TestMethod("Client: Removes Channel Subscriptions")]
		public async Task ClientCanRemoveChannelSubscription()
		{
			var channel1 = socketClient!.Channel("realtime", "public", "todos");
			await channel1.Subscribe();

			// Removing channel should remove the stored instance, so a future instance would need
			// to resubscribe.
			socketClient!.Remove(channel1);

			var channel2 = socketClient!.Channel("realtime", "public", "todos");
			Assert.AreEqual(ChannelState.Closed, channel2.State);
		}

		[TestMethod("Client: SetsAuth")]
		public async Task ClientSetsAuth()
		{
			var channel = socketClient!.Channel("realtime", "public", "todos");
			var channel2 = socketClient!.Channel("realtime", "public", "users");

			var token = @"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.C8oVtF5DICct_4HcdSKt8pdrxBFMQOAnPpbiiUbaXAY";

			// No subscriptions should show a push
			socketClient!.SetAuth(token);
			foreach (var subscription in socketClient!.Subscriptions.Values)
			{
				Assert.IsNull(subscription.LastPush);
			}

			await channel.Subscribe();
			await channel2.Subscribe();

			socketClient!.SetAuth(token);
			foreach (var subscription in socketClient!.Subscriptions.Values)
			{
				Assert.IsTrue(subscription?.LastPush?.EventName == ChannelAccessToken);
			}
		}

		[TestMethod("Client: Can reconnect after programmatic disconnect")]
		public async Task ClientCanReconnectAfterProgrammaticDisconnect()
		{
			socketClient!.Disconnect();
			await socketClient!.ConnectAsync();
		}
	}
}
