using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace schoolmusic_backend.Models;

[Table("banned_users")]
[Index("ModeratorId", Name = "idx_banned_mod")]
[Index("UserId", Name = "idx_banned_user")]
public partial class BannedUser
{
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    [Column("user_id", TypeName = "int(11)")]
    public int UserId { get; set; }

    [Column("reason", TypeName = "text")]
    public string? Reason { get; set; }

    [Column("moderator_id", TypeName = "int(11)")]
    public int ModeratorId { get; set; }

    [Column("banned_at", TypeName = "datetime")]
    public DateTime BannedAt { get; set; }

    [ForeignKey("ModeratorId")]
    [InverseProperty("BannedUsers")]
    public virtual Moderator Moderator { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("BannedUsers")]
    public virtual User User { get; set; } = null!;
}
