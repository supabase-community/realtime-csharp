using System;

namespace Supabase.Realtime.Socket
{
    public class SocketResponseEventArgs : EventArgs
    {
        public SocketResponse Response { get; private set; }

        public SocketResponseEventArgs(SocketResponse response)
        {
            Response = response;
        }
    }
}
