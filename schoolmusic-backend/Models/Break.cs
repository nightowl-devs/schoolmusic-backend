using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace schoolmusic_backend.Models;

[Table("break")]
[Index("StartAt", Name = "idx_break_start")]
[Index("BreakDate", "BreakNumber", Name = "uniq_break_day", IsUnique = true)]
public partial class Break
{
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    [Column("break_date")]
    public DateOnly BreakDate { get; set; }

    [Column("break_number", TypeName = "tinyint(4)")]
    public sbyte BreakNumber { get; set; }

    [Column("start_at", TypeName = "datetime")]
    public DateTime StartAt { get; set; }

    [Column("ends_at", TypeName = "datetime")]
    public DateTime EndsAt { get; set; }

    [Column("vote_ends", TypeName = "datetime")]
    public DateTime? VoteEnds { get; set; }

    [Required]
    [Column("music_enabled")]
    public bool? MusicEnabled { get; set; }

    [InverseProperty("Break")]
    public virtual ICollection<History> Histories { get; set; } = new List<History>();

    [InverseProperty("Break")]
    public virtual ICollection<QueueItem> QueueItems { get; set; } = new List<QueueItem>();
}
