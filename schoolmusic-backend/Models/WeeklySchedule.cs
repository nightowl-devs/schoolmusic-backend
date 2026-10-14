using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace schoolmusic_backend.Models;

[Table("weekly_schedule")]
public partial class WeeklySchedule
{
    [Key]
    [Column("weekday")]
    public bool Weekday { get; set; }

    [Required]
    [Column("music_enabled")]
    public bool? MusicEnabled { get; set; }

    [InverseProperty("WeekdayNavigation")]
    public virtual ICollection<WeeklyBreakSetting> WeeklyBreakSettings { get; set; } = new List<WeeklyBreakSetting>();
}
