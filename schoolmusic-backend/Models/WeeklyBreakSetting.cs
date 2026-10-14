using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace schoolmusic_backend.Models;

[PrimaryKey("Weekday", "BreakNumber")]
[Table("weekly_break_settings")]
[Index("BreakNumber", Name = "idx_wbs_break")]
public partial class WeeklyBreakSetting
{
    [Key]
    [Column("weekday", TypeName = "tinyint(4)")]
    public sbyte Weekday { get; set; }

    [Key]
    [Column("break_number", TypeName = "tinyint(4)")]
    public sbyte BreakNumber { get; set; }

    [Column("music_enabled")]
    public bool MusicEnabled { get; set; }

    [ForeignKey("BreakNumber")]
    [InverseProperty("WeeklyBreakSettings")]
    public virtual BreakTemplate BreakNumberNavigation { get; set; } = null!;

    [ForeignKey("Weekday")]
    [InverseProperty("WeeklyBreakSettings")]
    public virtual WeeklySchedule WeekdayNavigation { get; set; } = null!;
}
