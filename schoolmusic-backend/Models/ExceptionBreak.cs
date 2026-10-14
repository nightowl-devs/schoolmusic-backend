using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace schoolmusic_backend.Models;

[Table("exception_break")]
[Index("BreakDate", "BreakNumber", Name = "uniq_exc_break", IsUnique = true)]
public partial class ExceptionBreak
{
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    [Column("break_date")]
    public DateOnly BreakDate { get; set; }

    [Column("break_number", TypeName = "tinyint(4)")]
    public sbyte BreakNumber { get; set; }

    [Column("music_enabled")]
    public bool MusicEnabled { get; set; }

    [Column("description", TypeName = "text")]
    public string? Description { get; set; }
}
