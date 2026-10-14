using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace schoolmusic_backend.Models;

[Table("break_templates")]
[Index("BreakNumber", Name = "uniq_break_number", IsUnique = true)]
public partial class BreakTemplate
{
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    [Column("break_number", TypeName = "tinyint(4)")]
    public sbyte BreakNumber { get; set; }

    [Column("starts_time", TypeName = "time")]
    public TimeOnly StartsTime { get; set; }

    [Column("ends_time", TypeName = "time")]
    public TimeOnly EndsTime { get; set; }

    [InverseProperty("BreakNumberNavigation")]
    public virtual ICollection<WeeklyBreakSetting> WeeklyBreakSettings { get; set; } = new List<WeeklyBreakSetting>();
}
