using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace schoolmusic_backend.Models;

[Table("playback_state")]
[Index("CurrentQueueItemId", Name = "fk_playback_queue")]
public partial class PlaybackState
{
    [Key]
    [Column("id")]
    public bool? Id { get; set; }

    [Column("current_queue_item_id", TypeName = "int(11)")]
    public int? CurrentQueueItemId { get; set; }

    [Column("started_at", TypeName = "datetime")]
    public DateTime? StartedAt { get; set; }

    [Column("is_paused")]
    public bool IsPaused { get; set; }

    [Column("updated_at", TypeName = "datetime")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey("CurrentQueueItemId")]
    [InverseProperty("PlaybackStates")]
    public virtual QueueItem? CurrentQueueItem { get; set; }
}
