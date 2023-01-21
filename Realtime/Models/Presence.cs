using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Supabase.Realtime.Models
{
	public class Presence
	{
		[JsonProperty("presence_ref")]
		public string? PresenceRef { get; set; }
	}

	public class RawPresence
	{
		[JsonProperty("metas")]
		public List<RawPresenceMeta> Metas { get; set; } = new List<RawPresenceMeta>();
	}

	public class RawPresenceMeta
	{
		[JsonProperty("phx_ref")]
		public string? PheonixRef { get; set;}

		[JsonProperty("phx_ref_prev")]
		public string? PheonixPrevRef { get; set;}
	}
}
