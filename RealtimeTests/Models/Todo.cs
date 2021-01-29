using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace RealtimeTests.Models
{
    [Table("todos")]
    public class Todo : BaseModel
    {
        [PrimaryKey]
        public string Id { get; }

        [Column("details")]
        public string Details { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }
    }
}
