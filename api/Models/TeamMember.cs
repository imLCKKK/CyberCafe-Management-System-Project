using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CyberNet.Models;

[Table("teammembers")]
public class TeamMember
{
    public int TeamId { get; set; }

    public int RegistrationId { get; set; }

    [MaxLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string? AssignedRole { get; set; }

    // ----- Navigation: many-to-one -----
    [ForeignKey(nameof(TeamId))]
    public Team Team { get; set; } = null!;

    [ForeignKey(nameof(RegistrationId))]
    public TournamentRegistration Registration { get; set; } = null!;
}
