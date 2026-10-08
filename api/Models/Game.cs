using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("games")]
public class Game
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int GameId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column(TypeName = "varchar(100)")]
    public string GameName { get; set; } = string.Empty;

    public int TeamSize { get; set; }

    [Column(TypeName = "tinyint(1)")]
    public bool IsActive { get; set; } = true;

    // ----- Navigation: one-to-many -----
    public ICollection<GameRank> GameRanks { get; set; } = new List<GameRank>();
    public ICollection<Tournament> Tournaments { get; set; } = new List<Tournament>();
}
