using System;
using System.Collections.Generic;
using System.Timers;
using Newtonsoft.Json;
using Supabase.Realtime.Attributes;
using WebSocketSharp;

namespace Supabase.Realtime
{
    public class Channel
    {
        public enum ChannelState
        {
            [MapTo("closed")]
            Closed,
            [MapTo("errored")]
            Errored,
            [MapTo("joined")]
            Joined,
            [MapTo("joining")]
            Joining,
            [MapTo("leaving")]
            Leaving
        }

        public EventHandler<ItemInsertedEventArgs> OnInsert;
        public EventHandler<ItemUpdatedEventArgs> OnUpdated;
        public EventHandler<ItemDeletedEventArgs> OnDelete;
        public EventHandler<MessageEventArgs> OnMessage;

        public EventHandler OnClosed;
        public EventHandler OnError;

        public bool IsClosed => State == ChannelState.Closed;
        public bool IsErrored => State == ChannelState.Errored;
        public bool IsJoined => State == ChannelState.Joined;
        public bool IsJoining => State == ChannelState.Joining;
        public bool IsLeaving => State == ChannelState.Leaving;

        public Socket Socket { get => Client.Instance.Socket; }
        public ChannelState State { get; private set; } = ChannelState.Closed;
        public string Topic { get => Utils.GenerateChannelTopic(database, schema, table, col, value); }

        private string database;
        private string schema;
        private string table;
        private string col;
        private string value;

        private Push joinPush;
        private bool canPush => IsJoined && Socket.IsConnected;
        private bool hasJoinedOnce = false;
        private List<Push> buffer = new List<Push>();
        private Timer rejoinTimer;

        public Channel(string database, string schema, string table, string col, string value)
        {
            this.database = database;
            this.schema = schema;
            this.table = table;

            this.col = col;
            this.value = value;

            joinPush = new Push(this, Constants.CHANNEL_EVENT_JOIN, null);
            rejoinTimer = new Timer(Client.Instance.Options.Timeout.TotalMilliseconds);
        }

        public void Subscribe(int timeoutMs = Constants.DEFAULT_TIMEOUT)
        {
            if (hasJoinedOnce)
            {
                throw new Exception("`Subscribe` can only be called a single time per channel instance.");
            }
            else
            {
                hasJoinedOnce = true;
                Rejoin(timeoutMs);
            }
        }

        public void Unsubscribe()
        {
            State = ChannelState.Leaving;

            var leavePush = new Push(this, Constants.CHANNEL_EVENT_LEAVE, null);
            leavePush.Send();
        }

        public void Push(string eventName, object payload, int timeoutMs = Constants.DEFAULT_TIMEOUT)
        {
            if (!hasJoinedOnce)
            {
                throw new Exception($"Tried to push '{eventName}' to '{Topic}' before joining. Use `Channel.Subscribe()` before pushing events");
            }
            var pushEvent = new Push(this, eventName, payload, timeoutMs);
            if (canPush)
            {
                pushEvent.Send();
            }
            else
            {
                pushEvent.StartTimeout();
                buffer.Add(pushEvent);
            }
        }

        public void Rejoin(int timeoutMs = Constants.DEFAULT_TIMEOUT)
        {
            if (IsLeaving) return;
            SendJoin(timeoutMs);
        }

        private void SendJoin(int timeoutMs = Constants.DEFAULT_TIMEOUT)
        {
            State = ChannelState.Joining;
            joinPush.Resend(timeoutMs);
        }

        public class ItemInsertedEventArgs : EventArgs { }
        public class ItemUpdatedEventArgs : EventArgs { }
        public class ItemDeletedEventArgs : EventArgs { }
    }

    public class ChannelResponse
    {
        [JsonProperty("commit_timestamp")]
        public string CommitTimestamp { get; set; }

        [JsonProperty("schema")]
        public string Schema { get; set; }

        [JsonProperty("table")]
        public string Table { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("columns")]
        public List<ChannelColumnResponse> Columns { get; set; }

        [JsonProperty("record")]
        public object Record { get; set; }

        [JsonProperty("old_record")]
        public object OldRecord { get; set; }
    }

    public class ChannelColumnResponse
    {
        [JsonProperty("flags")]
        public List<string> Flags { get; set; }

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("type_modifier")]
        public int TypeModifier { get; set; }
    }
}
