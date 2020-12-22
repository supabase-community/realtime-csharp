using System;
using Postgrest.Models;

namespace Supabase.Realtime
{
    public class Channel<T> where T : BaseModel, new()
    {
        public EventHandler<ItemInsertedEventArgs> OnInsert;
        public EventHandler<ItemUpdatedEventArgs> OnUpdated;
        public EventHandler<ItemDeletedEventArgs> OnDelete;

        public Channel(string database)
        {
        }

        public Channel(string database, string schema)
        {
        }

        public Channel(string database, string schema, string table)
        {
        }

        public Channel(string database, string schema, string table, string col, string value)
        {
        }

        public class ItemInsertedEventArgs : EventArgs { }
        public class ItemUpdatedEventArgs : EventArgs { }
        public class ItemDeletedEventArgs : EventArgs { }
    }
}
