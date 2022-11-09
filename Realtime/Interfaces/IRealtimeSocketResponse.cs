using Postgrest.Models;

namespace Supabase.Realtime.Interfaces
{
    public interface IRealtimeSocketResponse
    {
        string? _event { get; set; }
        Constants.EventType Event { get; }
        SocketResponsePayload? Payload { get; set; }
        string? Ref { get; set; }
        string? Topic { get; set; }

        T? Model<T>() where T : BaseModel, new();
        T? OldModel<T>() where T : BaseModel, new();
    }
}