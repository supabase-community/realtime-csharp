using Newtonsoft.Json;
using System;

namespace Supabase.Realtime.Socket
{
	public class SocketResponseEventArgs<T> : EventArgs where T : class
	{
		public SocketResponse<T> Response { get; private set; }

		public SocketResponseEventArgs(SocketResponse response)
		{
			if (response.Json == null) 
				throw new ArgumentException(string.Format("Invalid SocketResponse.Json, expected parsable string, instead received `{0}`", response.Json));

			var data = JsonConvert.DeserializeObject<SocketResponse<T>>(response.Json);

			if (data == null)
				throw new ArgumentException(string.Format("Invalid Json, expected object conforming to `SocketResponse`, instead received `{0}`", response.Json));

			data.Json = response.Json;
			Response = data;
		}
	}

	public class SocketResponseEventArgs : EventArgs
	{
		public SocketResponse Response { get; private set; }

		public SocketResponseEventArgs(SocketResponse response)
		{
			Response = response;
		}
	}
}
