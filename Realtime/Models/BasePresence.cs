using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Supabase.Realtime.Models
{
	public class BasePresence
	{
		[JsonProperty("phx_ref")]
		public string? PheonixRef { get; set; }

		[JsonProperty("phx_ref_prev")]
		public string? PheonixPrevRef { get; set; }
	}
}
