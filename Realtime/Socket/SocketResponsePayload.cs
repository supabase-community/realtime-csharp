using Newtonsoft.Json;
using Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using static Supabase.Realtime.Constants;

namespace Supabase.Realtime.Socket
{
	public class SocketResponsePayload<T> : SocketResponsePayload where T : class
	{
		/// <summary>
		/// The record referenced.
		/// </summary>
		[JsonProperty("record")]
		public new T? Record { get; set; }

		/// <summary>
		/// The previous state of the referenced record.
		/// </summary>
		[JsonProperty("old_record")]
		public new T? OldRecord { get; set; }
	}
	public class SocketResponsePayload
	{
		/// <summary>
		/// Displays Column information from the Database.
		/// 
		/// Will always be an array but can be empty
		/// </summary>
		[JsonProperty("columns")]
		public List<object>? Columns { get; set; }

		/// <summary>
		/// The timestamp of the commit referenced.
		/// 
		/// Will either be a string or null
		/// </summary>
		[JsonProperty("commit_timestamp")]
		public DateTimeOffset? CommitTimestamp { get; set; }

		/// <summary>
		/// The record referenced.
		/// 
		/// Will always be an object but can be empty.
		/// </summary>
		[JsonProperty("record")]
		public object? Record { get; set; }

		/// <summary>
		/// The previous state of the referenced record.
		/// 
		/// Will always be an object but can be empty.
		/// </summary>
		[JsonProperty("old_record")]
		public object? OldRecord { get; set; }

		/// <summary>
		/// The Schema affected.
		/// </summary>
		[JsonProperty("schema")]
		public string? Schema { get; set; }

		/// <summary>
		/// The Table affected.
		/// </summary>
		[JsonProperty("table")]
		public string? Table { get; set; }

		/// <summary>
		/// The action type performed (INSERT, UPDATE, DELETE, etc.)
		/// </summary>
		[JsonProperty("type")]
		public string? _type { get; set; }

		[JsonIgnore]
		public EventType Type
		{
			get
			{
				switch (_type)
				{
					case "INSERT":
						return EventType.Insert;
					case "UPDATE":
						return EventType.Update;
					case "DELETE":
						return EventType.Delete;
				}

				return EventType.Unknown;
			}
		}

		[Obsolete("Property no longer used in responses.")]
		[JsonProperty("status")]
		public string? Status { get; set; }

		[JsonProperty("response")]
		public object? Response { get; set; }

		[JsonProperty("joins")]
		public object? Joins { get; set; }

		[JsonProperty("leaves")]
		public object? Leaves { get; set; }

		/// <summary>
		/// Either null or an array of errors.
		/// See: https://github.com/supabase/walrus/#error-states
		/// </summary>
		[JsonProperty("errors")]
		public List<string>? Errors { get; set; }
	}
}
