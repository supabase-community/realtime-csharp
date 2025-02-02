using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RealtimeExample.Models;

[Table("revit_events")]
public class RevitEvent : BaseModel
{
    [PrimaryKey("id")] public int Id { get; set; }

    public string event_name { get; set; }

    public string info { get; set; }

    public string secret_key { get; set; }
    public bool from_frontend { get; set; }
}