using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("tournaments")]
public class Tournament
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TournamentId { get; set; }

    public int GameId { get; set; }

    public int CreatedBy { get; set; }

    [Required]
    [MaxLength(150)]
    [Column(TypeName = "varchar(150)")]
    public string TournamentName { get; set; } = string.Empty;

    [Column(TypeName = "datetime")]
    public DateTime RegistrationStart { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime RegistrationEnd { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime StartTime { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal EntryFee { get; set; }

    [Required]
    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string Status { get; set; } = string.Empty;

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(GameId))]
    public Game Game { get; set; } = null!;

    [ForeignKey(nameof(CreatedBy))]
    public Employee CreatedByEmployee { get; set; } = null!;

    // ----- Navigation: one-to-many -----
    public ICollection<TournamentRegistration> TournamentRegistrations { get; set; } = new List<TournamentRegistration>();
    public ICollection<Team> Teams { get; set; } = new List<Team>();
}
