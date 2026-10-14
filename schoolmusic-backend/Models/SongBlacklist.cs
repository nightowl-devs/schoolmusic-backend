using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace schoolmusic_backend.Models;

[Table("song_blacklist")]
[Index("ArtistId", Name = "idx_black_artist")]
[Index("ModId", Name = "idx_black_mod")]
[Index("SongId", Name = "uniq_black_song", IsUnique = true)]
public partial class SongBlacklist
{
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    [Column("song_id", TypeName = "int(11)")]
    public int SongId { get; set; }

    [Column("artist_id", TypeName = "int(11)")]
    public int ArtistId { get; set; }

    [Column("mod_id", TypeName = "int(11)")]
    public int ModId { get; set; }

    [ForeignKey("ArtistId")]
    [InverseProperty("SongBlacklists")]
    public virtual Artist Artist { get; set; } = null!;

    [ForeignKey("ModId")]
    [InverseProperty("SongBlacklists")]
    public virtual Moderator Mod { get; set; } = null!;

    [InverseProperty("Blacklist")]
    public virtual ICollection<QueueItem> QueueItems { get; set; } = new List<QueueItem>();

    [ForeignKey("SongId")]
    [InverseProperty("SongBlacklist")]
    public virtual Song Song { get; set; } = null!;
}
