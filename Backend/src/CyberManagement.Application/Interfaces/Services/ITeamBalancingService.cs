using CyberManagement.Application.DTOs.Tournaments;

namespace CyberManagement.Application.Interfaces.Services;

public interface ITeamBalancingService
{
    TeamBalanceProposal Propose(TeamBalanceInput input);
}
