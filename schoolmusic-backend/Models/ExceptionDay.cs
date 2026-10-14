using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace schoolmusic_backend.Models;

[Table("exception_day")]
[Index("StartDate", "EndDate", Name = "idx_exc_day_range")]
public partial class ExceptionDay
{
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    [Column("start_date")]
    public DateOnly StartDate { get; set; }

    [Column("end_date")]
    public DateOnly EndDate { get; set; }

    [Column("day_type", TypeName = "enum('day_off','no_music')")]
    public string DayType { get; set; } = null!;

    [Column("description", TypeName = "text")]
    public string? Description { get; set; }
}
