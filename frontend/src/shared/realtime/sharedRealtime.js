import { HubConnectionBuilder, LogLevel, DefaultHttpClient } from '@microsoft/signalr';
import { authFailureStatus, createRealtimeClient } from './realtimeClient.js';

const clients = new Map();
const hubUrl = import.meta.env?.VITE_REALTIME_URL?.trim() || '/hubs/realtime';

export function createSignalRClient(token, url = hubUrl) {
  let client;
  let authenticationStatus = null;
  function authenticationFailed(error) {
    const status = authFailureStatus(error);
    if (!status) return;
    authenticationStatus = status;
    client.authenticationFailed(error);
  }
  const http = new DefaultHttpClient({ log() {} });
  const connection = new HubConnectionBuilder()
    .withUrl(url, {
      accessTokenFactory: () => token, withCredentials: false,
      httpClient: {
        async send(request) {
          // The SDK wraps negotiate errors and drops statusCode; observe HTTP auth failures first.
          try {
            const response = await http.send(request);
            authenticationFailed(response);
            return response;
          } catch (error) {
            authenticationFailed(error);
            throw error;
          }
        },
        getCookieString: (requestUrl) => http.getCookieString(requestUrl),
      },
    })
    .configureLogging(LogLevel.None)
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds({ previousRetryCount, retryReason }) {
        authenticationFailed(retryReason);
        if (authenticationStatus) return null;
        return [0, 2000, 10000, 30000][previousRetryCount] ?? null;
      },
    }).build();
  client = createRealtimeClient({ connection });
  return client;
}

export function subscribeRealtime(token, subscriber) {
  let entry = clients.get(token);
  if (!entry) {
    entry = { client: createSignalRClient(token), references: 0 };
    clients.set(token, entry);
  }
  entry.references++;
  const unsubscribe = entry.client.subscribe(subscriber);
  let released = false;
  return () => {
    if (released) return;
    released = true;
    unsubscribe();
    entry.references--;
    if (!entry.references && clients.get(token) === entry) clients.delete(token);
  };
}
