using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace schoolmusic_backend.Models;

[Table("ranks")]
[Index("Rank1", Name = "uniq_rank_name", IsUnique = true)]
public partial class Rank
{
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    [Column("rank")]
    public string Rank1 { get; set; } = null!;

    [InverseProperty("Rank")]
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
