using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace schoolmusic_backend.Models;

[Table("songs")]
[Index("IsActive", Name = "idx_songs_active")]
[Index("ArtistId", Name = "idx_songs_artist")]
[Index("SpotifyId", Name = "uniq_spotify_song", IsUnique = true)]
public partial class Song
{
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    [Column("spotify_id")]
    public string? SpotifyId { get; set; }

    [Column("source", TypeName = "enum('spotify','youtube','local','other')")]
    public string Source { get; set; } = null!;

    [Column("title")]
    [StringLength(500)]
    public string Title { get; set; } = null!;

    [Column("artist_id", TypeName = "int(11)")]
    public int ArtistId { get; set; }

    [Column("cover", TypeName = "text")]
    public string? Cover { get; set; }

    [Column("song_url", TypeName = "text")]
    public string? SongUrl { get; set; }

    [Column("genre")]
    [StringLength(100)]
    public string? Genre { get; set; }

    [Column("duration_ms", TypeName = "int(11)")]
    public int DurationMs { get; set; }

    [Required]
    [Column("is_active")]
    public bool? IsActive { get; set; }

    [Column("created_at", TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("ArtistId")]
    [InverseProperty("Songs")]
    public virtual Artist Artist { get; set; } = null!;

    [InverseProperty("Song")]
    public virtual ICollection<DefaultSong> DefaultSongs { get; set; } = new List<DefaultSong>();

    [InverseProperty("Song")]
    public virtual ICollection<History> Histories { get; set; } = new List<History>();

    [InverseProperty("Song")]
    public virtual ICollection<QueueItem> QueueItems { get; set; } = new List<QueueItem>();

    [InverseProperty("Song")]
    public virtual SongBlacklist? SongBlacklist { get; set; }

    [InverseProperty("Song")]
    public virtual SongGreenlist? SongGreenlist { get; set; }

    [ForeignKey("SongId")]
    [InverseProperty("Songs")]
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
