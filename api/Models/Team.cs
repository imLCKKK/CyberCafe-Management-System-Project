using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("teams")]
public class Team
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TeamId { get; set; }

    public int TournamentId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column(TypeName = "varchar(100)")]
    public string TeamName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(6,2)")]
    public decimal AvgSkillScore { get; set; }

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(TournamentId))]
    public Tournament Tournament { get; set; } = null!;

    // ----- Navigation: one-to-many -----
    public ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
}
