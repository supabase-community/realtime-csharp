using System;
using System.Collections.Generic;
using System.Timers;
using Newtonsoft.Json;
using Supabase.Realtime.Attributes;
using WebSocketSharp;
using static Supabase.Realtime.Channel;

namespace Supabase.Realtime
{
    /// <summary>
    /// Class representation of a channel subscription
    /// </summary>
    public class Channel
    {
        /// <summary>
        /// Channel state with associated string representations.
        /// </summary>
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

        /// <summary>
        /// Invoked when the `INSERT` event is raised.
        /// </summary>
        public EventHandler<ItemInsertedEventArgs> OnInsert;

        /// <summary>
        /// Invoked when the `UPDATE` event is raised.
        /// </summary>
        public EventHandler<ItemUpdatedEventArgs> OnUpdated;

        /// <summary>
        /// Invoked when the `DELETE` event is raised.
        /// </summary>
        public EventHandler<ItemDeletedEventArgs> OnDelete;

        /// <summary>
        /// Invoked anytime a message is decoded within this topic.
        /// </summary>
        public EventHandler<MessageEventArgs> OnMessage;

        /// <summary>
        /// Invoked when this channel listener is closed
        /// </summary>
        public EventHandler<ChannelStateChangedEventArgs> StateChanged;

        public bool IsClosed => State == ChannelState.Closed;
        public bool IsErrored => State == ChannelState.Errored;
        public bool IsJoined => State == ChannelState.Joined;
        public bool IsJoining => State == ChannelState.Joining;
        public bool IsLeaving => State == ChannelState.Leaving;

        /// <summary>
        /// Shorthand accessor for the Client's socket connection.
        /// </summary>
        public Socket Socket { get => Client.Instance.Socket; }

        /// <summary>
        /// The Channel's current state.
        /// </summary>
        public ChannelState State { get; private set; } = ChannelState.Closed;

        /// <summary>
        /// The Channel's (unique) topic indentifier.
        /// </summary>
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

        /// <summary>
        /// Initializes a Channel - must call `Subscribe()` to receive events.
        /// </summary>
        /// <param name="database"></param>
        /// <param name="schema"></param>
        /// <param name="table"></param>
        /// <param name="col"></param>
        /// <param name="value"></param>
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

        /// <summary>
        /// Subscribes to the channel given supplied options/params.
        /// </summary>
        /// <param name="timeoutMs"></param>
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

        /// <summary>
        /// Unsubscribes from the channel.
        /// </summary>
        public void Unsubscribe()
        {
            SetState(ChannelState.Leaving);
            var leavePush = new Push(this, Constants.CHANNEL_EVENT_LEAVE, null);
            leavePush.Send();
        }

        /// <summary>
        /// Sends a `Push` request under this channel.
        ///
        /// Maintains a buffer in the event push is called prior to the channel being joined.
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="payload"></param>
        /// <param name="timeoutMs"></param>
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

        /// <summary>
        /// Rejoins the channel.
        /// </summary>
        /// <param name="timeoutMs"></param>
        public void Rejoin(int timeoutMs = Constants.DEFAULT_TIMEOUT)
        {
            if (IsLeaving) return;
            SendJoin(timeoutMs);
        }

        private void SendJoin(int timeoutMs = Constants.DEFAULT_TIMEOUT)
        {
            SetState(ChannelState.Joining);
            // Remove handler if exists
            joinPush.OnMessage -= HandleJoinResponse;

            joinPush.OnMessage += HandleJoinResponse;
            joinPush.Resend(timeoutMs);
        }

        private void HandleJoinResponse(object sender, SocketMessageEventArgs args)
        {
            if (args.Message.Event == Constants.CHANNEL_EVENT_REPLY)
            {
                var obj = JsonConvert.DeserializeObject<PheonixResponse>(JsonConvert.SerializeObject(args.Message.Payload));
                if (obj.Status == Constants.PHEONIX_STATUS_OK)
                {
                    SetState(ChannelState.Joined);
                }
            }
        }

        private void SetState(ChannelState state)
        {
            State = state;
            StateChanged?.Invoke(this, new ChannelStateChangedEventArgs(state));
        }

        public class ItemInsertedEventArgs : EventArgs { }
        public class ItemUpdatedEventArgs : EventArgs { }
        public class ItemDeletedEventArgs : EventArgs { }
    }

    public class ChannelStateChangedEventArgs : EventArgs
    {
        public ChannelState State { get; private set; }

        public ChannelStateChangedEventArgs(ChannelState state)
        {
            State = state;
        }
    }

    public class PheonixResponse
    {
        [JsonProperty("response")]
        public object Response;

        [JsonProperty("status")]
        public string Status;
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
