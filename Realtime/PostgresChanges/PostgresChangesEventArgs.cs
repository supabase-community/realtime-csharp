using Supabase.Realtime.Socket;
using System;
using System.Collections.Generic;
using System.Text;

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
