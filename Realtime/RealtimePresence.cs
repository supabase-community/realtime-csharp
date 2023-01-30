using Newtonsoft.Json;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Models;
using Supabase.Realtime.Presence;
using Supabase.Realtime.Presence.Responses;
using Supabase.Realtime.Socket;
using System;
using System.Collections.Generic;
using static Supabase.Realtime.Constants;

namespace Supabase.Realtime
{
	/// <summary>
	/// Represents a realtime presence client.
	/// 
	/// When a client subscribes to a channel, it will immediately receive the channel's latest state in a single message.
	/// Clients are free to come-and-go as they please, and as long as they are all subscribed to the same channel then they will all have the same Presence state as each other.
	/// If a client is suddenly disconnected (for example, they go offline), their state will be automatically removed from the shared state.
	/// </summary>
	/// <typeparam name="TPresenceModel">A model representing expected payload.</typeparam>
	public class RealtimePresence<TPresenceModel> : IRealtimePresence where TPresenceModel : BasePresence
	{
		/// <summary>
		/// The Last State of this Presence instance.
		/// </summary>
		public Dictionary<string, List<TPresenceModel>> LastState { get; private set; } = new Dictionary<string, List<TPresenceModel>>();

		/// <summary>
		/// The Current State of this Presence instance.
		/// </summary>
		public Dictionary<string, List<TPresenceModel>> CurrentState { get; private set; } = new Dictionary<string, List<TPresenceModel>>();

		/// <summary>
		/// Called when Presence Joins (incoming changes) are present in a websocket response.
		/// </summary>
		public event EventHandler<EventArgs?>? OnJoin;

		/// <summary>
		/// Called when Presence Leaves (previous state) are present in a websocket response.
		/// </summary>
		public event EventHandler<EventArgs?>? OnLeave;

		/// <summary>
		/// Called on every recieved Presence message after setting <see cref="LastState"/> and <see cref="CurrentState"/>
		/// </summary>
		public event EventHandler<EventArgs?>? OnSync;

		private RealtimeChannel channel;
		private PresenceOptions options;
		private JsonSerializerSettings serializerSettings;

		private SocketResponseEventArgs? currentResponse;

		public RealtimePresence(RealtimeChannel channel, PresenceOptions options, JsonSerializerSettings serializerSettings)
		{
			this.channel = channel;
			this.options = options;
			this.serializerSettings = serializerSettings;
		}

		/// <summary>
		/// Called in two cases:
		///		- By `RealtimeChannel` when it receives a `presence_state` initializing message.
		///		- By `RealtimeChannel` When a diff has been received and a new response is saved.
		/// </summary>
		/// <param name="args"></param>
		public void TriggerSync(SocketResponseEventArgs args)
		{
			var lastState = new Dictionary<string, List<TPresenceModel>>(LastState);

			currentResponse = args;
			SetState();

			OnSync?.Invoke(this, null);
		}

		/// <summary>
		/// Triggers a diff comparison and emits events accordingly.
		/// </summary>
		/// <param name="args"></param>
		/// <exception cref="ArgumentException"></exception>
		public void TriggerDiff(SocketResponseEventArgs args)
		{
			if (args.Response == null || args.Response.Json == null)
				throw new ArgumentException(string.Format("Expected parsable JSON response, instead recieved: `{0}`", JsonConvert.SerializeObject(args.Response)));

			var obj = JsonConvert.DeserializeObject<RealtimePresenceDiff<TPresenceModel>>(args.Response.Json, serializerSettings);

			if (obj == null || obj.Payload == null) return;

			TriggerSync(args);

			if (obj.Payload.Joins!.Count > 0)
				OnJoin?.Invoke(this, null);

			if (obj.Payload.Leaves!.Count > 0)
				OnLeave?.Invoke(this, null);
		}

		/// <summary>
		/// "Tracks" an event, used with <see cref="Presence"/>.
		/// </summary>
		/// <param name="payload"></param>
		/// <param name="timeoutMs"></param>
		public void Track(object? payload, int timeoutMs = DEFAULT_TIMEOUT)
		{
			var eventName = Core.Helpers.GetMappedToAttr(ChannelEventName.Presence).Mapping;
			channel.Push(eventName, "track", new Dictionary<string, object?> { { "event", "track" }, { "payload", payload } }, timeoutMs);
		}

		public void Untrack()
		{
			var eventName = Core.Helpers.GetMappedToAttr(ChannelEventName.Presence).Mapping;
			channel.Push(eventName, "untrack");
		}

		/// <summary>
		/// Sets the internal Presence State from the <see cref="currentResponse"/>
		/// </summary>
		private void SetState()
		{
			LastState = new Dictionary<string, List<TPresenceModel>>(CurrentState);

			if (currentResponse == null || currentResponse.Response.Json == null) return;

			// Is a diff response?
			if (currentResponse.Response.Payload!.Joins != null || currentResponse.Response.Payload!.Leaves != null)
			{
				var state = JsonConvert.DeserializeObject<RealtimePresenceDiff<TPresenceModel>>(currentResponse.Response.Json, serializerSettings)!;

				if (state == null || state.Payload == null) return;

				// Remove any result that has "left"
				foreach (var item in state.Payload.Leaves!)
					CurrentState.Remove(item.Key);

				// Add any results that have come in.
				foreach (var item in state.Payload.Joins!)
					CurrentState[item.Key] = item.Value.Metas!;
			}
			else
			{
				// It's a presence_state init response
				var state = JsonConvert.DeserializeObject<PresenceStateSocketResponse<TPresenceModel>>(currentResponse.Response.Json, serializerSettings)!;

				if (state == null || state.Payload == null) return;

				foreach (var item in state.Payload)
					CurrentState[item.Key] = item.Value.Metas!;
			}
		}
	}
}
