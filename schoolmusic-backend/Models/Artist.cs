using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace schoolmusic_backend.Models;

[Table("artists")]
[Index("SpotifyArtistId", Name = "uniq_spotify_artist", IsUnique = true)]
public partial class Artist
{
    [Key]
    [Column("id", TypeName = "int(11)")]
    public int Id { get; set; }

    [Column("spotify_artist_id")]
    public string? SpotifyArtistId { get; set; }

    [Column("name")]
    [StringLength(500)]
    public string Name { get; set; } = null!;

    [Column("source", TypeName = "enum('spotify','youtube','local','other')")]
    public string Source { get; set; } = null!;

    [InverseProperty("Artist")]
    public virtual ArtistBlacklist? ArtistBlacklist { get; set; }

    [InverseProperty("Artist")]
    public virtual ICollection<SongBlacklist> SongBlacklists { get; set; } = new List<SongBlacklist>();

    [InverseProperty("Artist")]
    public virtual ICollection<SongGreenlist> SongGreenlists { get; set; } = new List<SongGreenlist>();

    [InverseProperty("Artist")]
    public virtual ICollection<Song> Songs { get; set; } = new List<Song>();
}
