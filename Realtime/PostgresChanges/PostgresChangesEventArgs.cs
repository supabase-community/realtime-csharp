using System;

namespace Supabase.Realtime.PostgresChanges
{
	public class PostgresChangesEventArgs : EventArgs
	{
		public PostgresChangesResponse? Response { get; private set; }

		public PostgresChangesEventArgs(PostgresChangesResponse? response)
		{
			Response = response;
		}
	}
}
