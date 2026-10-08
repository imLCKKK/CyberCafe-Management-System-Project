using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("gameranks")]
public class GameRank
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int RankId { get; set; }

    public int GameId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string RankName { get; set; } = string.Empty;

    public int RankOrder { get; set; }

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(GameId))]
    public Game Game { get; set; } = null!;

    // ----- Navigation: one-to-many -----
    public ICollection<TournamentRegistration> TournamentRegistrations { get; set; } = new List<TournamentRegistration>();
}
