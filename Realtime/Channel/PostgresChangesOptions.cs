using Newtonsoft.Json;
using System.Collections.Generic;

namespace Supabase.Realtime.Channel
{
    /// <summary>
    /// Handles a `postgres_changes` channel
    /// 
    /// For Example in the js client: 
    /// 
    ///		const databaseFilter = {
    ///			schema: 'public',
    ///			table: 'messages',
    ///			filter: `room_id=eq.${channelId}`,
    ///			event: 'INSERT',
    ///		}
    ///	
    /// Would translate to:
    /// 
    ///		new PostgresChangesOptions("public", "messages", $"room_id=eq.{channelId}");
    /// </summary>
    public class PostgresChangesOptions
    {
        [JsonProperty("schema")]
        public string Schema { get; set; }

        [JsonProperty("table")]
        public string? Table { get; set; }

        [JsonProperty("filter")]
        public string? Filter { get; set; }

        public Dictionary<string, string>? Parameters { get; set; }

        public PostgresChangesOptions(string schema, string? table = null, string? filter = null, Dictionary<string, string>? parameters = null)
        {
            Schema = schema;
            Table = table;
            Filter = filter;
            Parameters = parameters;
        }
    }
}
