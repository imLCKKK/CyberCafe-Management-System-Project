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

export const createTournament = (request, key) =>
  apiRequest('/admin/tournaments', { method: 'POST', headers: { 'Idempotency-Key': key }, body: request });

export function tournamentCreateKey(employeeId, request, storage = globalThis.localStorage) {
  return tournamentIntentKey('create', employeeId, request, storage);
}

export function tournamentTransitionKey(employeeId, tournamentId, request, storage = globalThis.localStorage) {
  return tournamentIntentKey('transition', employeeId, { tournamentId, ...request }, storage);
}

function tournamentIntentKey(operation, employeeId, request, storage) {
  const normalizedId = typeof employeeId === 'string' && /^\d+$/.test(employeeId)
    ? Number(employeeId) : employeeId;
  if (!Number.isSafeInteger(normalizedId) || normalizedId <= 0) throw new Error('Không xác định được nhân viên tạo giải.');
  const storageKey = `cybermanagement.tournament-${operation}.v1:${normalizedId}:${JSON.stringify(request)}`;
  let key;
  try {
    key = storage.getItem(storageKey);
    if (!key) {
      key = Array.from(globalThis.crypto.getRandomValues(new Uint8Array(16)),
        (byte) => byte.toString(16).padStart(2, '0')).join('');
      storage.setItem(storageKey, key);
    }
  } catch {
    throw new Error('Không thể lưu khóa gửi lại. Chưa gửi yêu cầu tạo giải.');
  }
  return { key, storageKey };
}

export function clearTournamentCreateKey(intent, storage = globalThis.localStorage) {
  if (storage.getItem(intent.storageKey) === intent.key) storage.removeItem(intent.storageKey);
}

export const updateTournament = (tournamentId, request) =>
  apiRequest(`/admin/tournaments/${tournamentId}`, { method: 'PUT', body: request });

export const transitionTournament = (tournamentId, request, key) =>
  apiRequest(`/admin/tournaments/${tournamentId}/transitions`, {
    method: 'POST', headers: { 'Idempotency-Key': key }, body: request,
  });

export const registerTournament = (tournamentId, request) =>
  apiRequest(`/client/tournaments/${tournamentId}/registrations`, {
    method: 'POST', body: request,
  });
