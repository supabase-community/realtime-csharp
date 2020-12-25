using System;
using System.Timers;
using WebSocketSharp;

namespace Supabase.Realtime
{
    /// <summary>
    /// Class representation of a single request sent to the Socket server.
    ///
    /// `Push` also adds additional functionality for retrying, timeouts, and listeners
    /// for its associated response from the server.
    /// </summary>
    public class Push
    {
        /// <summary>
        /// Flag representing the `sent` state of a request.
        /// </summary>
        public bool IsSent { get; private set; } = false;

        /// <summary>
        /// Invoked when the server has responded to a request.
        /// </summary>
        public EventHandler<SocketMessageEventArgs> OnMessage;

        /// <summary>
        /// Invoked when this `Push` has not been responded to within the timeout interval.
        /// </summary>
        public EventHandler OnTimeout;
        public SocketMessage Response { get; private set; }

        /// <summary>
        /// The associated channel.
        /// </summary>
        public Channel Channel { get; private set; }

        /// <summary>
        /// The event requested.
        /// </summary>
        public string EventName { get; private set; }

        /// <summary>
        /// Payload of data to be sent.
        /// </summary>
        public object Payload { get; private set; }


        private int timeoutMs;
        private Timer timer;
        private string msgRef;
        private string msgRefEvent;

        /// <summary>
        /// Initilizes a single request that will be `Pushed` to the Socket server.
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="eventName"></param>
        /// <param name="payload"></param>
        /// <param name="timeoutMs"></param>
        public Push(Channel channel, string eventName, object payload, int timeoutMs = Constants.DEFAULT_TIMEOUT)
        {
            Channel = channel;
            EventName = eventName;
            Payload = payload;

            this.timeoutMs = timeoutMs;

            timer = new Timer(this.timeoutMs);
            timer.Elapsed += TimeoutReached;

            Channel.Socket.OnMessage += HandleSocketMessage;
        }

        /// <summary>
        /// Resends a `Push` request.
        /// </summary>
        /// <param name="timeoutMs"></param>
        public void Resend(int timeoutMs = Constants.DEFAULT_TIMEOUT)
        {
            this.timeoutMs = timeoutMs;
            msgRef = null;
            msgRefEvent = null;

            IsSent = false;
            Send();
        }

        /// <summary>
        /// Sends a `Push` request and initializes the Timeout timer.
        /// </summary>
        public void Send()
        {
            StartTimeout();
            IsSent = true;
            var message = new SocketMessage
            {
                Topic = Channel.Topic,
                Event = EventName,
                Payload = Payload,
                Ref = msgRef
            };
            Channel.Socket.Push(message);
        }

        /// <summary>
        /// Keeps an internal timer for raising an event if this message is not responded to.
        /// </summary>
        internal void StartTimeout()
        {
            timer.Stop();
            timer.Start();
            msgRef = Client.Instance.Socket.MakeMsgRef();
            msgRefEvent = Client.Instance.Socket.ReplyEventName(msgRef);
        }

        /// <summary>
        /// Verifies that the request `ref` matches the response `ref`.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void HandleSocketMessage(object sender, SocketMessageEventArgs args)
        {
            if (args.Message.Ref == msgRef && args.Message.Event == EventName)
            {
                CancelTimeout();
                Response = args.Message;
                OnMessage?.Invoke(this, args);
                Channel.Socket.OnMessage -= HandleSocketMessage;
            }
        }

        private void TimeoutReached(object sender, ElapsedEventArgs e) => OnTimeout?.Invoke(this, null);

        private void CancelTimeout() => timer.Stop();
    }
}
