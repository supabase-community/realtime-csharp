using Newtonsoft.Json;
using System.Collections.Generic;

namespace Supabase.Realtime.Models;

/// <summary>
/// Represents a Broadcast response with a modeled payload.
/// </summary>
/// <typeparam name="T"></typeparam>
public class BaseBroadcast<T> : BaseBroadcast where T : class
{
	/// <summary>
	/// The typed payload.
	/// </summary>
	[JsonProperty("payload")]
	public new T? Payload { get; set; }
}

/// <summary>
/// Represents an arbitrary Broadcast response.
/// </summary>
public class BaseBroadcast
{
	/// <summary>
	/// The event.
	/// </summary>
	[JsonProperty("event")]
	public string? Event { get; set; }

	/// <summary>
	/// The payload.
	/// </summary>
	[JsonProperty("payload")]
	public Dictionary<string, object>? Payload { get; set; }

	/// <summary>
	/// Additional metadata associated with a broadcast event.
	/// </summary>
	[JsonProperty("meta", NullValueHandling = NullValueHandling.Ignore)]
	public Meta? Meta { get; set; }
}


public class Meta
{
	[JsonProperty("id")]
	public string Id { get; set; }
	
	[JsonProperty("replayed")]
	public bool Replayed { get; set; }
}