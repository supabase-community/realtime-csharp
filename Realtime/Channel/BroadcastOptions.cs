using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Supabase.Realtime.Channel
{
    public class BroadcastOptions
    {
        /// <summary>
        /// self option enables client to receive message it broadcast
        /// </summary>
        [JsonProperty("self")]
        public bool BroadcastSelf { get; set; } = false;

        /// <summary>
        /// ack option instructs server to acknowledge that broadcast message was received
        /// </summary>
        [JsonProperty("ack")]
        public bool BroadcastAck { get; set; } = false;

        public BroadcastOptions(bool broadcastSelf = false, bool broadcastAck = false)
        {
            BroadcastSelf = broadcastSelf;
            BroadcastAck = broadcastAck;
        }
    }
}
