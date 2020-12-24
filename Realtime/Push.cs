using System;
using System.Timers;
using WebSocketSharp;

namespace Supabase.Realtime
{
    public class Push
    {
        public bool Sent { get; private set; } = false;

        public EventHandler<SocketMessageEventArgs> OnMessage;
        public EventHandler OnTimeout;
        public SocketMessage Response { get; private set; }

        public Channel Channel { get; private set; }
        public string EventName { get; private set; }
        public object Payload { get; private set; }


        private int timeoutMs;
        private Timer timer;
        private string msgRef;
        private string msgRefEvent;

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

        public void Resend(int timeoutMs = Constants.DEFAULT_TIMEOUT)
        {
            this.timeoutMs = timeoutMs;
            msgRef = null;
            msgRefEvent = null;

            Sent = false;
            Send();
        }

        public void Send()
        {
            StartTimeout();
            Sent = true;
            var message = new SocketMessage
            {
                Topic = Channel.Topic,
                Event = EventName,
                Payload = Payload,
                Ref = msgRef
            };
            Channel.Socket.Push(message);
        }

        internal void StartTimeout()
        {
            timer.Stop();
            timer.Start();
            msgRef = Client.Instance.Socket.MakeMsgRef();
            msgRefEvent = Client.Instance.Socket.ReplyEventName(msgRef);
        }

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
