using System.Globalization;
using System.Security.Claims;
using CyberManagement.API.Security;
using CyberManagement.Application.DTOs.Tournaments;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Application.Exceptions;
using CyberManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller;

[ApiController]
[Route("api")]
public sealed class TournamentsController(ITournamentService tournaments) : ControllerBase
{
    [HttpGet("client/games")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> GetClientGames(CancellationToken cancellationToken) =>
        Ok(await tournaments.GetGamesAsync(false, cancellationToken));

    [HttpGet("client/games/{gameId:int}/ranks")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> GetClientRanks(int gameId, CancellationToken cancellationToken) =>
        Ok(await tournaments.GetRanksAsync(gameId, cancellationToken));

    [HttpGet("client/tournaments")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> GetClientTournaments(CancellationToken cancellationToken) =>
        Ok(await tournaments.GetTournamentsAsync(false, cancellationToken));

    [HttpGet("client/tournaments/{tournamentId:int}")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> GetClientTournament(int tournamentId, CancellationToken cancellationToken) =>
        Ok(await tournaments.GetTournamentAsync(tournamentId, false, cancellationToken));

    [HttpGet("client/tournaments/{tournamentId:int}/registrations")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> GetClientRegistrations(int tournamentId, CancellationToken cancellationToken) =>
        Ok(await tournaments.GetClientRegistrationsAsync(tournamentId, GetOwnCustomerId(), cancellationToken));

    [HttpPost("client/tournaments/{tournamentId:int}/registrations")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Register(
        int tournamentId, [FromBody] TournamentRegistrationRequest request, CancellationToken cancellationToken)
    {
        var registration = await tournaments.RegisterAsync(
            tournamentId, GetOwnCustomerId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetClientRegistrations), new { tournamentId }, registration);
    }
    [HttpGet("admin/games")]
    [Authorize(Policy = ApiPolicies.TournamentView)]
    public async Task<IActionResult> GetAdminGames(CancellationToken cancellationToken)
    {
        await tournaments.RequireActiveEmployeeAsync(GetEmployeeId(), cancellationToken);
        return Ok(await tournaments.GetGamesAsync(true, cancellationToken));
    }

    [HttpGet("admin/games/{gameId:int}/ranks")]
    [Authorize(Policy = ApiPolicies.TournamentView)]
    public async Task<IActionResult> GetAdminRanks(int gameId, CancellationToken cancellationToken)
    {
        await tournaments.RequireActiveEmployeeAsync(GetEmployeeId(), cancellationToken);
        return Ok(await tournaments.GetRanksAsync(gameId, cancellationToken));
    }

    [HttpGet("admin/tournaments")]
    [Authorize(Policy = ApiPolicies.TournamentView)]
    public async Task<IActionResult> GetAdminTournaments(CancellationToken cancellationToken)
    {
        await tournaments.RequireActiveEmployeeAsync(GetEmployeeId(), cancellationToken);
        return Ok(await tournaments.GetTournamentsAsync(true, cancellationToken));
    }

    [HttpGet("admin/tournaments/{tournamentId:int}")]
    [Authorize(Policy = ApiPolicies.TournamentView)]
    public async Task<IActionResult> GetAdminTournament(int tournamentId, CancellationToken cancellationToken)
    {
        await tournaments.RequireActiveEmployeeAsync(GetEmployeeId(), cancellationToken);
        return Ok(await tournaments.GetTournamentAsync(tournamentId, true, cancellationToken));
    }

    [HttpPost("admin/tournaments")]
    [Authorize(Policy = ApiPolicies.TournamentManage)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTournamentRequest request, [FromHeader(Name = "Idempotency-Key")] string key,
        CancellationToken cancellationToken)
    {
        var result = await tournaments.CreateAsync(GetEmployeeId(), key, request, cancellationToken);
        if (result.Replayed)
        {
            Response.Headers["Idempotency-Replayed"] = "true";
            return Ok(result.Tournament);
        }
        return CreatedAtAction(nameof(GetAdminTournament), new { tournamentId = result.Tournament.Id }, result.Tournament);
    }

    [HttpPut("admin/tournaments/{tournamentId:int}")]
    [Authorize(Policy = ApiPolicies.TournamentManage)]
    public async Task<IActionResult> Update(
        int tournamentId, [FromBody] UpdateTournamentRequest request, CancellationToken cancellationToken) =>
        Ok(await tournaments.UpdateAsync(GetEmployeeId(), tournamentId, request, cancellationToken));

    [HttpPost("admin/tournaments/{tournamentId:int}/transitions")]
    [Authorize(Policy = ApiPolicies.TournamentManage)]
    public async Task<IActionResult> Transition(
        int tournamentId, [FromBody] TransitionTournamentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken cancellationToken)
    {
        var result = await tournaments.TransitionAsync(GetEmployeeId(), tournamentId, key, request, cancellationToken);
        if (result.Replayed) Response.Headers["Idempotency-Replayed"] = "true";
        return Ok(result.Tournament);
    }

    [HttpGet("admin/tournaments/{tournamentId:int}/registrations")]
    [Authorize(Policy = ApiPolicies.TournamentView)]
    public async Task<IActionResult> GetAdminRegistrations(int tournamentId, CancellationToken cancellationToken)
    {
        await tournaments.RequireActiveEmployeeAsync(GetEmployeeId(), cancellationToken);
        return Ok(await tournaments.GetAdminRegistrationsAsync(tournamentId, cancellationToken));
    }

    [HttpPost("client/tournaments/{tournamentId:int}/registrations/{registrationId:int}/opgg/refresh")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> RefreshOwnStats(int tournamentId, int registrationId,
        [FromBody] RefreshOpggRequest request, CancellationToken token) =>
        Ok(await tournaments.RefreshStatsAsync(tournamentId, registrationId, GetOwnCustomerId(), token));

    [HttpPost("admin/tournaments/{tournamentId:int}/registrations/{registrationId:int}/opgg/refresh")]
    [Authorize(Policy = ApiPolicies.TournamentManage)]
    public async Task<IActionResult> RefreshRegistrationStats(int tournamentId, int registrationId,
        [FromBody] RefreshOpggRequest request, CancellationToken token)
    {
        await tournaments.RequireActiveEmployeeAsync(GetEmployeeId(), token);
        return Ok(await tournaments.RefreshStatsAsync(tournamentId, registrationId, null, token));
    }

    [HttpPost("admin/tournaments/{tournamentId:int}/teams/preview")]
    [Authorize(Policy = ApiPolicies.TeamAssign)]
    public async Task<ActionResult<TeamPlanDto>> PreviewTeams(int tournamentId, [FromBody] TeamPreviewRequest request,
        [FromServices] ITeamPreviewRosterReader reader, [FromServices] ITeamBalancingService balancing,
        [FromServices] TeamPreviewPlans plans, CancellationToken token)
    {
        var employee = GetEmployeeId();
        await tournaments.RequireActiveEmployeeAsync(employee, token);
        var versionBytes = new byte[8];
        if (tournamentId <= 0 || request.ExpectedVersion is null || request.ExpectedVersion.Length != 12
            || !Convert.TryFromBase64String(request.ExpectedVersion, versionBytes, out var written) || written != 8
            || Convert.ToBase64String(versionBytes) != request.ExpectedVersion)
            throw new TeamBalanceException("ValidationError", "ID hoặc expectedVersion không hợp lệ.");
        var strategy = request.Strategy switch
        {
            "ML" => "ml", "Balanced" => TeamBalanceVersions.Baseline,
            _ => throw new TeamBalanceException("ValidationError", "Strategy phải là ML hoặc Balanced.")
        };
        var roster = await reader.ReadAsync(tournamentId, strategy, request.ExpectedVersion, token);
        var proposal = balancing.Propose(roster.Input);
        var current = await reader.ReadAsync(tournamentId, strategy, request.ExpectedVersion, token);
        if (current.Input.RosterVersion != proposal.RosterVersion)
            throw new TeamBalanceException("TeamPlanInvalid", "Danh sách hoặc snapshot đã đổi trong lúc chia đội.");
        var stats = TeamBalanceValidation.Roster(current.Input);
        var ids = proposal.Teams.SelectMany(t => t.Members).Select(m => m.RegistrationId).ToArray();
        if (proposal.Teams.Count != current.Input.TeamCount || proposal.Teams.Any(t => t.Members.Count != current.Input.TeamSize)
            || ids.Length != stats.Count || !stats.Keys.ToHashSet().SetEquals(ids))
            throw new TeamBalanceException("TeamPlanInvalid", "Đề xuất không khớp toàn bộ danh sách đăng ký.");
        var participants = current.Input.Participants.ToDictionary(p => p.RegistrationId);
        var teams = proposal.Teams.Select(t =>
        {
            var members = t.Members.Select(m => new TeamPreviewMember(m.RegistrationId, current.CustomerNames[m.RegistrationId],
                stats[m.RegistrationId].RankTier, participants[m.RegistrationId].Snapshot.Stats.GetProperty("rank").GetProperty("label").GetString()!,
                m.AssignedRole)).ToArray();
            return new TeamPreviewTeam($"Đội {t.TeamKey}", members.Average(m => m.SkillScore), members);
        }).ToArray();
        var expiry = DateTimeOffset.UtcNow.AddMinutes(10);
        var planToken = plans.Issue(new(employee, roster.Input, proposal, expiry));
        return Ok(new TeamPlanDto(request.ExpectedVersion, proposal.RosterVersion, proposal.ProposalId, planToken, expiry,
            request.Strategy, proposal.ModelVersion, proposal.BaselineVersion, teams, [], proposal.Quality.RankMeanGap,
            proposal.Quality, proposal.Warnings));
    }

    private int GetOwnCustomerId()
    {
        var claim = User.FindFirstValue("customer_id");
        if (User.FindFirstValue("account_type") != "Customer"
            || !int.TryParse(claim, NumberStyles.None, CultureInfo.InvariantCulture, out var customerId)
            || customerId <= 0 || User.FindFirstValue("sub") != claim)
            throw new UnauthorizedAccessException("Danh tính khách hàng không hợp lệ.");
        return customerId;
    }

    private int GetEmployeeId()
    {
        var claim = User.FindFirstValue("sub");
        if (User.FindFirstValue("account_type") is not null
            || !int.TryParse(claim, NumberStyles.None, CultureInfo.InvariantCulture, out var employeeId)
            || employeeId <= 0)
            throw new UnauthorizedAccessException("Danh tính nhân viên không hợp lệ.");
        return employeeId;
    }
}
