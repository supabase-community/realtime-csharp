using Supabase;
using Supabase.Gotrue;
using Supabase.Realtime;
using Supabase.Realtime.Socket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Client = Supabase.Realtime.Client;

namespace RealtimeTests
{
	internal static class Helpers
	{
		private static string supabasePublicKey => Environment.GetEnvironmentVariable("SUPABASE_PUBLIC_KEY");
		private static string supabaseUrl => Environment.GetEnvironmentVariable("SUPABASE_URL");

		private static string supabaseUsername => Environment.GetEnvironmentVariable("SUPABASE_USERNAME");
		private static string supabasePassword => Environment.GetEnvironmentVariable("SUPABASE_PASSWORD");

		private static string socketEndpoint = string.Format("{0}/realtime/v1", supabaseUrl).Replace("https", "wss");
		private static string restEndpoint = string.Format("{0}/rest/v1", supabaseUrl);
		private static string authEndpoint = string.Format("{0}/auth/v1", supabaseUrl);

		public static Supabase.Gotrue.Client AuthClient => new Supabase.Gotrue.Client(new Supabase.Gotrue.ClientOptions<Supabase.Gotrue.Session>
		{
			Url = authEndpoint,
			Headers = new Dictionary<string, string> { { "apiKey", supabasePublicKey } }
		});

		public static Postgrest.Client RestClient(string userToken) => new Postgrest.Client(restEndpoint, new Postgrest.ClientOptions
		{
			Headers = new Dictionary<string, string>
			{
				{ "Authorization", $"Bearer {userToken}" },
				{ "apiKey", supabasePublicKey }
			}
		});

		public static Task<Session> GetSession() => AuthClient.SignInWithPassword(supabaseUsername, supabasePassword);

		public static Supabase.Realtime.Client SocketClient()
		{
			var client = new Supabase.Realtime.Client(socketEndpoint, new ClientOptions
            {
				Parameters = new SocketOptionsParameters
                {
					ApiKey = supabasePublicKey
                }
			});

			return client;
		}
	}
}
