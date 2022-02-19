using System;
using System.Timers;

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
        public event EventHandler<SocketResponseEventArgs> OnMessage;

        /// <summary>
        /// Invoked when this `Push` has not been responded to within the timeout interval.
        /// </summary>
        public event EventHandler OnTimeout;
        public SocketResponse Response { get; private set; }

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

        /// <summary>
        /// Represents the Pushed (sent) Message
        /// </summary>
        public SocketRequest Message { get; private set; }

        /// <summary>
        /// Ref Of this Message
        /// </summary>
        public string Ref { get; private set; }

        private int timeoutMs;
        private Timer timer;

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
            Ref = null;
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
            Message = new SocketRequest
            {
                Topic = Channel.Topic,
                Event = EventName,
                Payload = Payload,
                Ref = Ref
            };
            Channel.Socket.Push(Message);
        }

        /// <summary>
        /// Keeps an internal timer for raising an event if this message is not responded to.
        /// </summary>
        internal void StartTimeout()
        {
            timer.Stop();
            timer.Start();
            Ref = Client.Instance.Socket.MakeMsgRef();
            msgRefEvent = Client.Instance.Socket.ReplyEventName(Ref);
        }

        /// <summary>
        /// Verifies that the request `ref` matches the response `ref`.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void HandleSocketMessage(object sender, SocketResponseEventArgs args)
        {
            if (args.Response.Ref == Ref)
            {
                CancelTimeout();
                Response = args.Response;
                OnMessage?.Invoke(this, args);
                Channel.Socket.OnMessage -= HandleSocketMessage;
            }
        }

        private void TimeoutReached(object sender, ElapsedEventArgs e) => OnTimeout?.Invoke(this, null);

        private void CancelTimeout() => timer.Stop();
    }

    public class PushTimeoutException : Exception { }
}
