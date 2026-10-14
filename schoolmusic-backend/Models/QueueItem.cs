using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace schoolmusic_backend.Models;

[Table("queue_items")]
[Index("BlacklistId", Name = "idx_queue_black")]
[Index("GreenlistId", Name = "idx_queue_green")]
[Index("ModeratorId", Name = "idx_queue_mod")]
[Index("SongId", Name = "idx_queue_song")]
[Index("UserId", Name = "idx_queue_user")]
[Index("BreakId", "OrderIndex", Name = "uniq_break_order", IsUnique = true)]
[Index("BreakId", "SongId", Name = "uniq_break_song", IsUnique = true)]
public partial class QueueItem
{
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    [Column("break_id", TypeName = "int(11)")]
    public int BreakId { get; set; }

    [Column("song_id", TypeName = "int(11)")]
    public int SongId { get; set; }

    [Column("user_id", TypeName = "int(11)")]
    public int? UserId { get; set; }

    [Column("greenlist_id", TypeName = "int(11)")]
    public int? GreenlistId { get; set; }

    [Column("blacklist_id", TypeName = "int(11)")]
    public int? BlacklistId { get; set; }

    [Column("moderator_id", TypeName = "int(11)")]
    public int? ModeratorId { get; set; }

    [Column("moderation_status", TypeName = "enum('approved','rejected')")]
    public string? ModerationStatus { get; set; }

    [Column("status", TypeName = "enum('queued','playing','played','skipped')")]
    public string Status { get; set; } = null!;

    [Column("order_index", TypeName = "int(11)")]
    public int OrderIndex { get; set; }

    [Column("created_at", TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("BlacklistId")]
    [InverseProperty("QueueItems")]
    public virtual SongBlacklist? Blacklist { get; set; }

    [ForeignKey("BreakId")]
    [InverseProperty("QueueItems")]
    public virtual Break Break { get; set; } = null!;

    [ForeignKey("GreenlistId")]
    [InverseProperty("QueueItems")]
    public virtual SongGreenlist? Greenlist { get; set; }

    [ForeignKey("ModeratorId")]
    [InverseProperty("QueueItems")]
    public virtual Moderator? Moderator { get; set; }

    [InverseProperty("CurrentQueueItem")]
    public virtual ICollection<PlaybackState> PlaybackStates { get; set; } = new List<PlaybackState>();

    [ForeignKey("SongId")]
    [InverseProperty("QueueItems")]
    public virtual Song Song { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("QueueItems")]
    public virtual User? User { get; set; }

    [InverseProperty("QueueItem")]
    public virtual ICollection<Vote> Votes { get; set; } = new List<Vote>();
}
