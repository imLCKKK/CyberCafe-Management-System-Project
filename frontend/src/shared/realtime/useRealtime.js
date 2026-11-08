import { useEffect, useRef, useState } from 'react';
import { subscribeRealtime } from './sharedRealtime.js';

const noEvents = [];

export function useRealtime({ enabled, accessToken, events = noEvents, onEvent, onReady, onAuthFailure }) {
  const callbacks = useRef({ accessToken, onEvent, onReady, onAuthFailure });
  callbacks.current = { accessToken, onEvent, onReady, onAuthFailure };
  const [state, setState] = useState({ token: null, status: 'idle' });
  const eventKey = JSON.stringify(events);
  useEffect(() => {
    if (!enabled || !accessToken) return;
    const invoke = (name, ...args) => {
      if (callbacks.current.accessToken === accessToken) callbacks.current[name]?.(...args);
    };
    return subscribeRealtime(accessToken, {
      events: JSON.parse(eventKey),
      onStatus: (status) => {
        if (callbacks.current.accessToken === accessToken) setState({ token: accessToken, status });
      },
      onEvent: (...args) => invoke('onEvent', ...args),
      onReady: () => invoke('onReady'),
      onAuthFailure: (code) => invoke('onAuthFailure', code),
    });
  }, [enabled, accessToken, eventKey]);
  return !enabled || !accessToken ? 'idle' : state.token === accessToken ? state.status : 'connecting';
}
