using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace RealtimeTests.Models
{
    [Table("todos")]
    public class Todo : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("details")]
        public string Details { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("inserted_at")]
        public DateTime InsertedAt { get; set; }
    }
}
