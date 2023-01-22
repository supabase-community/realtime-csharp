using Postgrest.Models;
using Supabase.Realtime.Socket;

namespace Supabase.Realtime.Interfaces
{
    public interface IRealtimeSocketResponse
    {
        string? _event { get; set; }
        Constants.EventType Event { get; }
        SocketResponsePayload? Payload { get; set; }
        string? Ref { get; set; }
        string? Topic { get; set; }

        TModel? Model<TModel>() where TModel: BaseModel, new();
        TModel? OldModel<TModel>() where TModel: BaseModel, new();
    }
}