using Newtonsoft.Json;
using Postgrest.Models;
using Supabase.Realtime.Socket;
using System;
using System.Collections.Generic;
using System.Text;

namespace Supabase.Realtime.PostgresChanges
{
	public class PostgresChangesResponse<T> : SocketResponse<PostgresChangesPayload<T>> where T : class
	{
		public PostgresChangesResponse(JsonSerializerSettings serializerSettings) : base(serializerSettings)
		{
		}
	}

	public class PostgresChangesResponse : SocketResponse<PostgresChangesPayload<SocketResponsePayload>>
	{
		public PostgresChangesResponse(JsonSerializerSettings serializerSettings) : base(serializerSettings)
		{ }

		/// <summary>
		/// Hydrates the referenced record into a Model (if possible).
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public virtual TModel? Model<TModel>() where TModel : BaseModel, new()
		{
			if (Json != null && Payload != null && Payload.Data?.Record != null)
			{
				var response = JsonConvert.DeserializeObject<PostgresChangesResponse<TModel>>(Json, serializerSettings);
				return response?.Payload?.Data?.Record;
			}
			else
			{
				return default;
			}
		}

		/// <summary>
		/// Hydrates the old_record into a Model (if possible).
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public virtual TModel? OldModel<TModel>() where TModel : BaseModel, new()
		{
			if (Json != null && Payload != null && Payload.Data?.OldRecord != null)
			{
				var response = JsonConvert.DeserializeObject<PostgresChangesResponse<TModel>>(Json, serializerSettings);
				return response?.Payload?.Data?.OldRecord;
			}
			else
			{
				return default;
			}
		}
	}

	public class PostgresChangesPayload<T> where T : class
	{
		[JsonProperty("data")]
		public SocketResponsePayload<T>? Data { get; set; }
	}
}
