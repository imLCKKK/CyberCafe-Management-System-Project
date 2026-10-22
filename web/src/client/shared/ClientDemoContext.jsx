import React, { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { createClientDemo } from './demoData';
import { currentClient, isValidClientDemo } from './demoModel';

export const CLIENT_DEMO_KEY = 'tram.client.dbml.demo.v2';
const ClientDemoContext = createContext(null);
function load() {
  try {
    const raw = localStorage.getItem(CLIENT_DEMO_KEY);
    if (!raw) return { db: createClientDemo(), raw, warning: '' };
    const db = JSON.parse(raw);
    if (!isValidClientDemo(db)) throw new Error();
    return { db, raw, warning: '' };
  } catch { return { db: createClientDemo(), raw: null, warning: 'Không đọc được dữ liệu demo. Hãy khôi phục bản mẫu trước khi thao tác.' }; }
}
export function ClientDemoProvider({ children }) {
  const [state, setState] = useState(load);
  const current = useRef(state);
  const update = useCallback((next) => { current.current = next; setState(next); }, []);
  useEffect(() => {
    const listener = (event) => { if (event.key === CLIENT_DEMO_KEY || event.key === null) update(load()); };
    window.addEventListener('storage', listener);
    return () => window.removeEventListener('storage', listener);
  }, [update]);
  const commit = useCallback((transform) => {
    if (current.current.warning) throw new Error(current.current.warning);
    let raw;
    try { raw = localStorage.getItem(CLIENT_DEMO_KEY); } catch { throw new Error('Trình duyệt chưa cho phép lưu dữ liệu demo.'); }
    if (raw !== current.current.raw) { update(load()); throw new Error('Dữ liệu vừa thay đổi ở tab khác. Hãy kiểm tra và thử lại.'); }
    const db = transform(current.current.db);
    const serialized = JSON.stringify(db);
    try { localStorage.setItem(CLIENT_DEMO_KEY, serialized); } catch { throw new Error('Không thể lưu thay đổi. Hãy kiểm tra quyền lưu trữ của trình duyệt.'); }
    update({ db, raw: serialized, warning: '' });
    return db;
  }, [update]);
  const reset = useCallback(() => {
    const db = createClientDemo();
    const raw = JSON.stringify(db);
    try { localStorage.setItem(CLIENT_DEMO_KEY, raw); } catch { throw new Error('Không thể khôi phục dữ liệu.'); }
    update({ db, raw, warning: '' });
  }, [update]);
  return <ClientDemoContext.Provider value={{ ...state, ...currentClient(state.db), commit, reset }}>{children}</ClientDemoContext.Provider>;
}
export function useClientDemo() {
  const value = useContext(ClientDemoContext);
  if (!value) throw new Error('ClientDemoProvider is required.');
  return value;
}
