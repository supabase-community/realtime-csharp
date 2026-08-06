using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Supabase.Realtime.PostgresChanges;

namespace RealtimeTests;

/// <summary>
/// Wire-shape of <see cref="PostgresChangesOptions"/> as it is serialized into a channel's
/// join <c>config.postgres_changes</c> payload.
/// </summary>
[TestClass]
public class PostgresChangesOptionsTests
{
    [TestMethod]
    public void Table_ShouldBeOmittedFromJson_WhenNull()
    {
        // A schema-wide listener has no table; the server expects the key absent, not `"table": null`.
        var json = JsonConvert.SerializeObject(new PostgresChangesOptions("public"));
        Assert.IsFalse(JObject.Parse(json).ContainsKey("table"));
    }

    [TestMethod]
    public void Table_ShouldBeSerialized_WhenProvided()
    {
        var json = JsonConvert.SerializeObject(new PostgresChangesOptions("public", "todos"));
        Assert.AreEqual("todos", JObject.Parse(json)["table"]?.Value<string>());
    }
}
