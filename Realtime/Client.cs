using System;
using System.Collections.Generic;
using System.Linq;
using Postgrest.Attributes;
using Postgrest.Models;
using static Supabase.Realtime.Constants;

namespace Supabase.Realtime
{
    public class Client
    {
        public Dictionary<string, object> Subscriptions { get; private set; }

        private string realtimeUrl;
        private ClientAuthorization authorization;
        private ClientOptions options;

        private static Client instance;
        public static Client Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Client();
                }
                return instance;
            }
        }


        public Client Initialize(string realtimeUrl, ClientAuthorization authorization = null, ClientOptions options = null)
        {
            this.realtimeUrl = realtimeUrl;

            if (authorization == null)
            {
                authorization = new ClientAuthorization();
            }
            this.authorization = authorization;

            if (options == null)
            {
                options = new ClientOptions();
            }
            this.options = options;

            return this;
        }

        public Channel<T> Channel<T>(string database) where T : BaseModel, new() => Channel<T>(database, null, null, null, null);

        public Channel<T> Channel<T>(string database, string schema) where T : BaseModel, new() => Channel<T>(database, schema, null, null, null);

        public Channel<T> Channel<T>(string database, string schema, string table) where T : BaseModel, new() => Channel<T>(database, schema, table, null, null);

        public Channel<T> Channel<T>(string database, string schema = null, string table = null, string col = null, string value = null) where T : BaseModel, new()
        {
            var key = generateChannelString(database, schema, table, col, value);

            if (Subscriptions.ContainsKey(key))
            {
                return Subscriptions[key] as Channel<T>;
            }

            var subscription = new Channel<T>(database, schema, table, col, value);
            Subscriptions.Add(key, subscription);

            return subscription;
        }

        private static string generateChannelString(string database, string schema, string table, string col, string value)
        {
            var list = new List<String> { database, schema, table };
            string channel = String.Join(":", list.Where(s => string.IsNullOrEmpty(s)));

            if (!string.IsNullOrEmpty(col) && !string.IsNullOrEmpty(value))
            {
                channel += $":{col}.eq.{value}";
            }

            return channel;
        }
    }
}
