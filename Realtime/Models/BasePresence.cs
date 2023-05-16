using Newtonsoft.Json;

namespace Supabase.Realtime.Models
{
	/// <summary>
	/// Represents an arbitrary Presence response.
	/// </summary>
	public class BasePresence
	{
		[JsonProperty("phx_ref")]
		public string? PheonixRef { get; set; }

		[JsonProperty("phx_ref_prev")]
		public string? PheonixPrevRef { get; set; }

		public bool ShouldSerializePheonixRef() => false;
		public bool ShouldSerializePheonixPrevRef() => false;
	}
}
