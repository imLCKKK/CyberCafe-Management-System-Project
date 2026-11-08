import { useCallback, useEffect, useState } from 'react';

// Tag each response so a changed account, selection or page cannot display stale data.
export function useCustomerQuery(key, load, enabled = true) {
  const [revision, setRevision] = useState(0);
  const [state, setState] = useState({ key: null, data: null, loading: true, error: null });
  const requestKey = `${key}:${revision}`;
  useEffect(() => {
    if (!enabled) return undefined;
    const controller = new AbortController();
    setState({ key: requestKey, data: null, loading: true, error: null });
    Promise.resolve().then(() => load(controller.signal)).then((data) => {
      if (!controller.signal.aborted) setState({ key: requestKey, data, loading: false, error: null });
    }).catch((error) => {
      if (!controller.signal.aborted) setState({ key: requestKey, data: null, loading: false, error });
    });
    return () => controller.abort();
  }, [requestKey, load, enabled]);
  const reload = useCallback(() => setRevision((value) => value + 1), []);
  if (!enabled) return { data: null, loading: false, error: null, reload };
  return { ...(state.key === requestKey ? state : { data: null, loading: true, error: null }), reload };
}
