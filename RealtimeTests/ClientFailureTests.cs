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
        var client = new Client("ws://localhost:4000/socket");
        await Assert.ThrowsExceptionAsync<RealtimeException>(() => client.ConnectAsync());
    }
}