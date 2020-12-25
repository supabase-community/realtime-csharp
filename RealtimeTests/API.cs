using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RealtimeTests.Models;
using Supabase.Realtime;

namespace RealtimeTests
{
    [TestClass]
    public class API
    {
        private string endpoint = "ws://localhost:4000/socket";

        [TestMethod]
        public Task ClientConnects()
        {
            var tsc = new TaskCompletionSource<bool>();

            Task.Run(() =>
            {
                var client = Client.Instance.Initialize(endpoint);
                client.OnOpen += (sender, args) => tsc.SetResult(true);
                client.Connect();
            });

            return tsc.Task;
        }

        [TestMethod]
        public Task ClientJoinsChannel()
        {
            var tsc = new TaskCompletionSource<bool>();

            Task.Run(() =>
            {
                var client = Client.Instance.Initialize(endpoint);

                client.OnOpen += (sender, args) =>
                {
                    var channel = client.Channel("realtime", "*");
                    channel.Subscribe();
                };

                client.Connect();
            });

            return tsc.Task;
        }
    }
}
