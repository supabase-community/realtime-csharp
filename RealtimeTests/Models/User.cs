﻿using Postgrest.Attributes;
using Postgrest.Models;

namespace RealtimeTests.Models;

[Table("users")]
public class User : BaseModel
{
    [PrimaryKey("id", false)]
    public string? Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}