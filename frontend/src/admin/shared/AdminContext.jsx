import React, { createContext, useCallback, useContext, useMemo, useState } from 'react';
import { ClientDemoProvider, useClientDemo } from '../../client/shared/ClientDemoContext';
import { useHomeData } from '../../client/home/useHomeData';
import { ensureAdminData } from './adminData';

const AdminContext = createContext(null);
function AdminData({ children }) {
  const shared = useClientDemo();
  const tournaments = useHomeData();
  const db = useMemo(() => ensureAdminData(shared.db), [shared.db]);
  const commit = useCallback((transform) => shared.commit((latest) => transform(ensureAdminData(latest))), [shared.commit]);
  return <AdminContext.Provider value={{ db, commit, warning: shared.warning, tournamentDb: tournaments.db, commitTournament: tournaments.commit, tournamentWarning: tournaments.warning }}>{children}</AdminContext.Provider>;
}
export function AdminProvider({ children }) { return <ClientDemoProvider><AdminData>{children}</AdminData></ClientDemoProvider>; }
export function useAdmin() { const value = useContext(AdminContext); if (!value) throw new Error('AdminProvider is required'); return value; }
export function useAdminAction() {
  const context = useAdmin();
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');
  const run = (transform, message = 'Đã lưu thay đổi.') => { try { const result = context.commit(transform); setNotice(message); setError(''); return result; } catch (err) { setError(err.message); return null; } };
  return { ...context, run, notice, error: error || context.warning, setNotice, setError, clear: () => { setNotice(''); setError(''); } };
}
