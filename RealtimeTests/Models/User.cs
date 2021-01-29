using System;
using Postgrest.Attributes;

namespace RealtimeTests.Models
{
    [Table("users")]
    public class User
    {
        [PrimaryKey("id")]
        public string Id { get; set; }

        [Column("name")]
        public string Name { get; set; }
    }
}
