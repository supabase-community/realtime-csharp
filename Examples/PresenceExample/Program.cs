using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Newtonsoft.Json;
using PresenceExample;
using Supabase.Realtime;
using Supabase.Realtime.Socket;
using System.Diagnostics;
using static System.Runtime.InteropServices.JavaScript.JSType;

var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL");
var supabasePublicKey = Environment.GetEnvironmentVariable("SUPABASE_PUBLIC_KEY");
var realtimeUrl = string.Format("{0}/realtime/v1", supabaseUrl).Replace("https", "wss");

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddSingleton(builder => new Client(realtimeUrl, new ClientOptions
{
	Logger = (kind, msg, data) => Debug.WriteLine($"{kind}: {msg}, {JsonConvert.SerializeObject(data, Formatting.Indented)}"),
	Parameters = new SocketOptionsParameters
	{
		ApiKey = supabasePublicKey,
	}
}));

var constructed = builder.Build();

await constructed.RunAsync();
