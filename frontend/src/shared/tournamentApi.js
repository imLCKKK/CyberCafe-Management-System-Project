import { apiRequest } from './apiClient.js';

export const getTournaments = (audience, signal) =>
  apiRequest(`/${audience}/tournaments`, { signal });

export const getTournament = (audience, tournamentId, signal) =>
  apiRequest(`/${audience}/tournaments/${tournamentId}`, { signal });

export const getTournamentRegistrations = (audience, tournamentId, signal) =>
  apiRequest(`/${audience}/tournaments/${tournamentId}/registrations`, { signal });

export const getTournamentGames = (audience, signal) =>
  apiRequest(`/${audience}/games`, { signal });

export const getTournamentRanks = (audience, gameId, signal) =>
  apiRequest(`/${audience}/games/${gameId}/ranks`, { signal });

export const createTournament = (request) =>
  apiRequest('/admin/tournaments', { method: 'POST', body: request });

export const updateTournament = (tournamentId, request) =>
  apiRequest(`/admin/tournaments/${tournamentId}`, { method: 'PUT', body: request });

export const transitionTournament = (tournamentId, toStatus) =>
  apiRequest(`/admin/tournaments/${tournamentId}/transitions`, {
    method: 'POST', body: { toStatus },
  });

export const registerTournament = (tournamentId, request) =>
  apiRequest(`/client/tournaments/${tournamentId}/registrations`, {
    method: 'POST', body: request,
  });
