using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Realtime;
using Supabase.Realtime.Exceptions;

namespace RealtimeTests;

[TestClass]
public class ClientFailureTests
{
    [TestMethod("Client throws exception when unable to initially connect.")]
    public async Task ClientThrowsExceptionOnInitialConnectionFailure()
    {
        var client = new Client("ws://localhost");
        client.AddDebugHandler((sender, message, exception) => Debug.WriteLine(message));
        Assert.ThrowsExceptionAsync<RealtimeException>(client.ConnectAsync);
        client.Disconnect();

        await Task.Delay(500);
    }
}