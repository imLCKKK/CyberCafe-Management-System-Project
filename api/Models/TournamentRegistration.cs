using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("tournamentregistrations")]
public class TournamentRegistration
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int RegistrationId { get; set; }

    public int TournamentId { get; set; }

    public int CustomerId { get; set; }

    public int? RankId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string PreferredRole { get; set; } = string.Empty;

    [Column(TypeName = "decimal(5,2)")]
    public decimal WinRate { get; set; }

    public int HoursPlayed { get; set; }

    [Column(TypeName = "decimal(6,2)")]
    public decimal SkillScore { get; set; }

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string Status { get; set; } = string.Empty;

    [Column(TypeName = "datetime")]
    public DateTime RegisteredAt { get; set; }

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(TournamentId))]
    public Tournament Tournament { get; set; } = null!;

    [ForeignKey(nameof(CustomerId))]
    public Customer Customer { get; set; } = null!;

    [ForeignKey(nameof(RankId))]
    public GameRank? Rank { get; set; }

    // ----- Navigation: one-to-many -----
    public ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
}
