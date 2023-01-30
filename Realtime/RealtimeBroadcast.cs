using Newtonsoft.Json;
using Postgrest;
using Supabase.Realtime.Broadcast;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Models;
using Supabase.Realtime.Presence;
using Supabase.Realtime.Socket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static Supabase.Realtime.Constants;

namespace Supabase.Realtime
{

	/// <summary>
	/// Represents a realtime broadcast client.
	/// 
	/// Broadcast follows the publish-subscribe pattern where a client publishes messages to a channel with a unique identifier.
	/// Other clients can elect to receive the message in real-time by subscribing to the channel with the same unique identifier. If these clients are online and subscribed then they will receive the message.
	///
	/// Broadcast works by connecting your client to the nearest Realtime server, which will communicate with other servers to relay messages to other clients.
	/// A common use-case is sharing a user's cursor position with other clients in an online game.
	/// </summary>
	/// <typeparam name="TBroadcastModel">A model representing expected payload.</typeparam>
	public class RealtimeBroadcast<TBroadcastModel> : IRealtimeBroadcast where TBroadcastModel : BaseBroadcast
	{
		public event EventHandler<EventArgs?>? OnBroadcast;

		private RealtimeChannel channel;
		private BroadcastOptions options;
		private JsonSerializerSettings serializerSettings;

		private SocketResponse? lastSocketResponse;

		/// <summary>
		/// The last received broadcast.
		/// </summary>
		public TBroadcastModel? Current()
		{
			if (lastSocketResponse == null) return null;

			var obj = JsonConvert.DeserializeObject<SocketResponse<TBroadcastModel>>(lastSocketResponse.Json!, serializerSettings);

			if (obj == null || obj.Payload == null) return null;

			return obj.Payload;
		}

		public RealtimeBroadcast(RealtimeChannel channel, BroadcastOptions options, JsonSerializerSettings serializerSettings)
		{
			this.channel = channel;
			this.options = options;
			this.serializerSettings = serializerSettings;
		}

		/// <summary>
		/// Called by <see cref="RealtimeChannel"/> when a broadcast event is received, then parsed/typed here.
		/// </summary>
		/// <param name="args"></param>
		/// <exception cref="ArgumentException"></exception>
		public void TriggerReceived(SocketResponseEventArgs args)
		{
			if (args.Response == null || args.Response.Json == null)
				throw new ArgumentException(string.Format("Expected parsable JSON response, instead recieved: `{0}`", JsonConvert.SerializeObject(args.Response)));

			lastSocketResponse = args.Response;
			OnBroadcast?.Invoke(this, null);
		}

		/// <summary>
		/// Broadcasts an arbitrary payload
		/// </summary>
		/// <param name="payloadType"></param>
		/// <param name="payload"></param>
		/// <param name="timeoutMs"></param>
		public Task<bool> Send(string? type, object payload, int timeoutMs = 10000) => channel.Send(ChannelEventName.Broadcast, type, payload, timeoutMs);
	}
}
