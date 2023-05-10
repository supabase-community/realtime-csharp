using Newtonsoft.Json;
using Supabase.Core.Attributes;
using System.Collections.Generic;

namespace Supabase.Realtime.PostgresChanges
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
        public enum ListenType
        {
            [MapTo("*")]
            All,
            [MapTo("INSERT")]
            Inserts,
            [MapTo("UPDATE")]
            Updates,
            [MapTo("DELETE")]
            Deletes
        }

        [JsonProperty("schema")]
        public string Schema { get; set; }

        [JsonProperty("table")]
        public string? Table { get; set; }

        [JsonProperty("filter", NullValueHandling = NullValueHandling.Ignore)]
        public string? Filter { get; set; }

        [JsonProperty("parameters", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string>? Parameters { get; set; }

        [JsonProperty("event")]
        public string Event => Core.Helpers.GetMappedToAttr(_listenType).Mapping!;

        private readonly ListenType _listenType;

        public PostgresChangesOptions(string schema, string? table = null, ListenType eventType = ListenType.All, string? filter = null, Dictionary<string, string>? parameters = null)
        {
            _listenType = eventType;
            Schema = schema;
            Table = table;
            Filter = filter;
            Parameters = parameters;
        }
    }
}
