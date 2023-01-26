using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Supabase.Realtime.Models
{
	/// <summary>
	/// Represents a Broadcast response with a modeled payload.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class BaseBroadcast<T> : BaseBroadcast where T : class
	{
		[JsonProperty("payload")]
		public new T? Payload { get; set; }
	}

	/// <summary>
	/// Represents an arbitrary Broadcast response.
	/// </summary>
	public class BaseBroadcast
	{
		[JsonProperty("event")]
		public string? Event { get; set; }

		[JsonProperty("payload")]
		public Dictionary<string, object>? Payload { get; set; }
	}
}
