using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using RealtimeTests.Models;
using Supabase.Postgrest.Exceptions;
using Supabase.Postgrest.Interfaces;
using Supabase.Realtime.PostgresChanges;

namespace RealtimeTests;

/// <summary>
/// Feature under test: realtime-csharp#35 - a model returned by <c>PostgresChangesResponse.Model&lt;TModel&gt;</c>/
/// <c>OldModel&lt;TModel&gt;</c> gets a configured <see cref="Supabase.Realtime.ClientOptions.PostgrestClient"/>'s
/// context attached to it (via <c>Attach&lt;T&gt;()</c>), so that <c>Update</c>/<c>Delete</c> can be called
/// directly on it. Reproduces the exact deserialization path RealtimeChannel.HandleSocketMessage() uses for
/// postgres_changes events, without needing a live socket connection.
///
/// Split into two groups:
/// - no PostgrestClient configured: the default/standalone case, which must stay backward-compatible.
/// - a PostgrestClient configured: the case the Supabase umbrella client wires up automatically.
/// </summary>
[TestClass]
public class PostgresChangesResponseAttachTests
{
    private const string BaseUrl = "http://localhost:54321/rest/v1";

    private static PostgresChangesResponse BuildResponse(IPostgrestClient? postgrestClient)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "PostgresChangesUpdateEvent.json"));
        var serializerSettings = Supabase.Postgrest.Client.SerializerSettings();

        // Mirrors RealtimeChannel.HandleSocketMessage() exactly: deserialize, then stamp Json, SerializerSettings,
        // and PostgrestClient for the later Model<T>() re-deserialization pass.
        var deserialized = JsonConvert.DeserializeObject<PostgresChangesResponse>(json, serializerSettings);
        deserialized!.Json = json;
        deserialized.SerializerSettings = serializerSettings;
        deserialized.PostgrestClient = postgrestClient;
        return deserialized;
    }

    private static void AssertDoesNotThrowBaseUrlException(Exception ex)
    {
        Assert.IsFalse(ex is PostgrestException { Message: var m } && m.Contains("should be set in the model"),
            $"Unexpectedly got the BaseUrl exception: {ex}");
    }

    [TestMethod(DisplayName = "Without a PostgrestClient, Model<T>() leaves BaseUrl/RequestClientOptions null")]
    public void GivenNoPostgrestClient_Model_LeavesClientContextNull()
    {
        var response = BuildResponse(postgrestClient: null);
        var model = response.Model<Todo>();

        Assert.IsNotNull(model, "Sanity check: the model itself should deserialize correctly.");
        Assert.AreEqual(12, model!.Id);
        Assert.IsNull(model.BaseUrl);
        Assert.IsNull(model.RequestClientOptions);
    }

    [TestMethod(DisplayName = "Without a PostgrestClient, Update() throws PostgrestException (loud, not silent)")]
    public async Task GivenNoPostgrestClient_Update_ThrowsPostgrestException()
    {
        var response = BuildResponse(postgrestClient: null);
        var model = response.Model<Todo>()!;

        var ex = await Assert.ThrowsAsync<PostgrestException>(() => model.Update<Todo>());
        StringAssert.Contains(ex.Message, "BaseUrl");
    }

    [TestMethod(DisplayName = "Without a PostgrestClient, Delete() throws PostgrestException (loud, not silent)")]
    public async Task GivenNoPostgrestClient_Delete_ThrowsPostgrestException()
    {
        var response = BuildResponse(postgrestClient: null);
        var model = response.Model<Todo>()!;

        var ex = await Assert.ThrowsAsync<PostgrestException>(() => model.Delete<Todo>());
        StringAssert.Contains(ex.Message, "BaseUrl");
    }

    [TestMethod(DisplayName = "With a PostgrestClient configured, Model<T>() attaches BaseUrl/RequestClientOptions")]
    public void GivenPostgrestClient_Model_AttachesClientContext()
    {
        var response = BuildResponse(new Supabase.Postgrest.Client(BaseUrl));
        var model = response.Model<Todo>()!;

        Assert.AreEqual(BaseUrl, model.BaseUrl);
        Assert.IsNotNull(model.RequestClientOptions);
    }

    [TestMethod(DisplayName = "With a PostgrestClient configured, Update() does not throw the BaseUrl exception")]
    public async Task GivenPostgrestClient_Update_DoesNotThrowBaseUrlException()
    {
        var response = BuildResponse(new Supabase.Postgrest.Client(BaseUrl));
        var model = response.Model<Todo>()!;

        try
        {
            await model.Update<Todo>();
        }
        catch (Exception ex)
        {
            AssertDoesNotThrowBaseUrlException(ex);
        }
    }

    [TestMethod(DisplayName = "With a PostgrestClient configured, Delete() does not throw the BaseUrl exception")]
    public async Task GivenPostgrestClient_Delete_DoesNotThrowBaseUrlException()
    {
        var response = BuildResponse(new Supabase.Postgrest.Client(BaseUrl));
        var model = response.Model<Todo>()!;

        try
        {
            await model.Delete<Todo>();
        }
        catch (Exception ex)
        {
            AssertDoesNotThrowBaseUrlException(ex);
        }
    }
}
