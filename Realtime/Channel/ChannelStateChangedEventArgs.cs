using System;
using static Supabase.Realtime.Constants;

namespace Supabase.Realtime.Channel
{
    public class ChannelStateChangedEventArgs : EventArgs
    {
        public ChannelState State { get; private set; }

        public ChannelStateChangedEventArgs(ChannelState state)
        {
            State = state;
        }
    }
}
