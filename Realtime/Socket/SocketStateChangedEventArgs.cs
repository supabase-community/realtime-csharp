using System;

namespace Supabase.Realtime.Socket
{
    public class SocketStateChangedEventArgs : EventArgs
    {
        public enum ConnectionState
        {
            Open,
            Close,
            Error,
            Message
        }

        public ConnectionState State { get; set; }
        public EventArgs Args { get; set; }

        public SocketStateChangedEventArgs(ConnectionState state, EventArgs args)
        {
            State = state;
            Args = args;
        }
    }
}
