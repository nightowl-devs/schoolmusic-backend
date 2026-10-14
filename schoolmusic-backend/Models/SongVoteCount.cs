using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace schoolmusic_backend.Models;

[Keyless]
public partial class SongVoteCount
{
    [Column("song_id", TypeName = "int(11)")]
    public int SongId { get; set; }

    [Column("title")]
    [StringLength(500)]
    public string Title { get; set; } = null!;

    [Column("total_votes", TypeName = "bigint(21)")]
    public long TotalVotes { get; set; }
}
