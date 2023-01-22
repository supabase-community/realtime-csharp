using Newtonsoft.Json;
using Supabase.Core.Attributes;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Models;
using Supabase.Realtime.Presence;
using Supabase.Realtime.Presence.Responses;
using Supabase.Realtime.Socket;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Supabase.Realtime
{
	public class RealtimePresence
	{
		public event EventHandler<RealtimePresenceEventArgs>? OnJoin;
		public event EventHandler<RealtimePresenceEventArgs>? OnLeave;
		public event EventHandler<RealtimePresenceSyncArgs>? OnSync;

		private RealtimeChannel channel;
		private PresenceOptions options;

		private SocketResponseEventArgs? lastResponse;

		public Dictionary<string, object> LastState = new Dictionary<string, object>();

		public RealtimePresence(RealtimeChannel channel, PresenceOptions options)
		{
			this.channel = channel;
			this.options = options;
		}

		internal void TriggerSync(SocketResponseEventArgs args)
		{
			lastResponse = args;
			OnSync?.Invoke(this, new RealtimePresenceSyncArgs());
		}

		internal void TriggerDiff(SocketResponseEventArgs args)
		{
			if (args.Response == null || args.Response.Json == null)
				throw new ArgumentException(string.Format("Expected parsable JSON response, instead recieved: `{0}`", JsonConvert.SerializeObject(args.Response)));

			var obj = JsonConvert.DeserializeObject<RealtimePresenceDiff<BasePresence>>(args.Response.Json);

			if (obj == null || obj.Payload == null) return;

			TriggerSync(args);

			if (obj.Payload.Joins!.Count > 0)
				OnJoin?.Invoke(this, new RealtimePresenceEventArgs());

			if (obj.Payload.Leaves!.Count > 0)
				OnLeave?.Invoke(this, new RealtimePresenceEventArgs());

		}

		public Dictionary<string, List<T>> State<T>() where T : BasePresence
		{
			var result = new Dictionary<string, List<T>>();

			if (lastResponse == null || lastResponse.Response.Json == null) return result;

			// Is a diff response?
			if (lastResponse.Response.Payload!.Joins != null || lastResponse.Response.Payload!.Leaves != null)
			{
				var state = JsonConvert.DeserializeObject<RealtimePresenceDiff<T>>(lastResponse.Response.Json);

				if (state == null || state.Payload == null) return result;

				// Init with last state
				foreach (var item in LastState)
				{
					try
					{
						var conversion = (List<T>)Convert.ChangeType(item.Value, typeof(List<T>));
						if (conversion == null) continue;

						result.Add(item.Key, conversion);
					}
					catch { }
				}

				// Remove any result that has "left"
				foreach (var item in state.Payload.Leaves!)
				{
					result.Remove(item.Key);
				}

				// Add any results that have come in.
				foreach (var item in state.Payload.Joins!)
				{
					LastState[item.Key] = item.Value.Metas!;
					result.Add(item.Key, item.Value.Metas ?? new List<T>());
				}

				return result;
			}
			else
			{
				// It's a presence_state init response
				var state = JsonConvert.DeserializeObject<PresenceStateSocketResponse<T>>(lastResponse.Response.Json);

				if (state == null || state.Payload == null) return result;

				foreach (var item in state.Payload)
				{
					LastState[item.Key] = item.Value.Metas!;
					result.Add(item.Key, item.Value.Metas ?? new List<T>());
				}

				return result;
			}
		}
	}
}
