using Supabase.Gotrue;
using Supabase.Realtime;
using Supabase.Realtime.Socket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RealtimeTests
{
	internal static class Helpers
	{
		private static string SupabasePublicKey => Environment.GetEnvironmentVariable("SUPABASE_PUBLIC_KEY") ?? string.Empty;
		private static string SupabaseUrl => Environment.GetEnvironmentVariable("SUPABASE_URL") ?? "http://localhost:4000";

		private static string SupabaseUsername => Environment.GetEnvironmentVariable("SUPABASE_USERNAME") ?? string.Empty;
		private static string SupabasePassword => Environment.GetEnvironmentVariable("SUPABASE_PASSWORD") ?? string.Empty;

		private static readonly string SocketEndpoint = $"{SupabaseUrl}/realtime/v1".Replace("https", "wss");
		private static readonly string RestEndpoint = $"{SupabaseUrl}/rest/v1";
		private static readonly string AuthEndpoint = $"{SupabaseUrl}/auth/v1";

		private static Supabase.Gotrue.Client AuthClient => new(new ClientOptions<Session>
		{
			Url = AuthEndpoint,
			Headers = new Dictionary<string, string> { { "apiKey", SupabasePublicKey } }
		});

		public static Postgrest.Client RestClient(string userToken) => new(RestEndpoint, new Postgrest.ClientOptions
		{
			Headers = new Dictionary<string, string>
			{
				{ "Authorization", $"Bearer {userToken}" },
				{ "apiKey", SupabasePublicKey }
			}
		});

		public static Task<Session?> GetSession() => AuthClient.SignInWithPassword(SupabaseUsername, SupabasePassword);

		public static Supabase.Realtime.Client SocketClient()
		{
			var client = new Supabase.Realtime.Client(SocketEndpoint, new ClientOptions
            {
				Parameters = new SocketOptionsParameters
                {
					ApiKey = SupabasePublicKey
                }
			});

			return client;
		}
	}
}
