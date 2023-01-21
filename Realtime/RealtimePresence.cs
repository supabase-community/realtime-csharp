using Supabase.Core.Attributes;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using static Supabase.Realtime.Constants;

namespace Supabase.Realtime
{
    public class RealtimePresence<T> where T : Presence
	{
		public EventHandler<RealtimePresenceEventArgs<T>>? OnJoin;
		public EventHandler<RealtimePresenceEventArgs<T>>? OnLeave;
		public EventHandler<EventArgs>? OnSync;

		private Dictionary<string, List<T>> state = new Dictionary<string, List<T>>();
		public ReadOnlyDictionary<string, List<T>> State { get => new ReadOnlyDictionary<string, List<T>>(state); }

		private RealtimeChannel? channel;
		private Push? joinPush;
		private string? joinRef;
		private List<RealtimeRawPresenceDiff<RawPresence>> pendingDiffs = new List<RealtimeRawPresenceDiff<RawPresence>>();

		public RealtimePresence(RealtimeChannel channel, string stateEvent = "presence_state", string diffEvent = "presence_diff")
		{
			
		}
	}

	public class RealtimePresenceDiff<T> where T : Presence
	{
		public ReadOnlyDictionary<string, T> Joins { get; private set; }
		public ReadOnlyDictionary<string, T> Leaves { get; private set; }

		public RealtimePresenceDiff(Dictionary<string, T> joins, Dictionary<string, T> leaves)
		{
			Joins = new ReadOnlyDictionary<string, T>(joins);
			Leaves = new ReadOnlyDictionary<string, T>(leaves);
		}
	}

	public class RealtimeRawPresenceDiff<T> where T : RawPresence
	{
		public ReadOnlyDictionary<string, T> Joins { get; private set; }
		public ReadOnlyDictionary<string, T> Leaves { get; private set; }

		public RealtimeRawPresenceDiff(Dictionary<string, T> joins, Dictionary<string, T> leaves)
		{
			Joins = new ReadOnlyDictionary<string, T>(joins);
			Leaves = new ReadOnlyDictionary<string, T>(leaves);
		}
	}

	public class RealtimePresenceEventArgs<T> : EventArgs where T : Presence
	{
		public PresenceListenEventTypes EventType { get; private set; }
		public string Key { get; private set; }

		public ReadOnlyCollection<T> CurrentPresences { get; private set; }

		public ReadOnlyCollection<T> LeftPresences { get; private set; }

		public RealtimePresenceEventArgs(PresenceListenEventTypes eventType, string key, List<T> current, List<T> left)
		{
			EventType = eventType;
			Key = key;
			CurrentPresences = new ReadOnlyCollection<T>(current);
			LeftPresences = new ReadOnlyCollection<T>(left);
		}
	}
}
