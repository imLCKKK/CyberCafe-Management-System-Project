import React, { createContext, startTransition, useContext, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  AUTH_EXPIRED_EVENT,
  AUTH_STORAGE_KEY,
  clearStoredSessionIfMatches,
  clearVerifiedSession,
  getCurrentPrincipal,
  isSessionVerified,
  loginCustomer,
  loginEmployee,
  markSessionVerified,
  logout as logoutRequest,
  readStoredSession,
  storeSession,
} from './api';

const AuthContext = createContext(null);

export function getPrincipalDestination(principal) {
  if (principal?.type === 'customer') return '/client/home';
  const permissions = principal?.permissions || [];
  const destinations = [
    ['order.view', '/admin/orders'],
    ['computer.view', '/admin/computers'],
    ['customer.view', '/admin/customers'],
    ['product.view', '/admin/products'],
    ['stockreceipt.view', '/admin/inventory'],
    ['invoice.view', '/admin/invoices'],
    ['tournament.view', '/admin/tournaments'],
    ['employee.view', '/admin/staff'],
    ['report.view', '/admin/reports'],
  ];
  return destinations.find(([permission]) => permissions.includes(permission))?.[1] || '/admin/dashboard';
}

function identityType(accountType, identity) {
  return accountType === 'customer'
    ? { ...identity, type: 'customer', permissions: [] }
    : { ...identity, type: 'employee', permissions: identity.permissions || [] };
}

export function AuthProvider({ children }) {
  const navigate = useNavigate();
  const [principal, setPrincipal] = useState(null);
  const [loading, setLoading] = useState(true);
  const [expiresAtUtc, setExpiresAtUtc] = useState(null);
  const verificationVersion = useRef(0);

  function isStoredSession(session) {
    const stored = readStoredSession();
    return stored?.accessToken === session?.accessToken && stored?.accountType === session?.accountType;
  }

  async function verifySession(session, { redirectOnFailure = true, throwOnFailure = false } = {}) {
    const version = ++verificationVersion.current;
    clearVerifiedSession();
    setPrincipal(null);
    setExpiresAtUtc(null);
    setLoading(true);
    try {
      const identity = await getCurrentPrincipal(session.accountType, session);
      if (version !== verificationVersion.current || !isStoredSession(session)) return null;
      if (!markSessionVerified(session)) return null;
      const current = identityType(session.accountType, identity);
      setPrincipal(current);
      setExpiresAtUtc(session.expiresAtUtc || null);
      return current;
    } catch (error) {
      if (version !== verificationVersion.current) return null;
      clearStoredSessionIfMatches(session);
      clearVerifiedSession(session);
      setPrincipal(null);
      setExpiresAtUtc(null);
      if (throwOnFailure) throw error;
      if (redirectOnFailure) {
        navigate(session.accountType === 'customer' ? '/client/auth' : '/admin/auth', {
          replace: true,
          state: { sessionExpired: true },
        });
      }
      return null;
    } finally {
      if (version === verificationVersion.current && (!readStoredSession() || isStoredSession(session))) {
        startTransition(() => setLoading(false));
      }
    }
  }

  useEffect(() => {
    const session = readStoredSession();
    if (!session) {
      clearVerifiedSession();
      startTransition(() => setLoading(false));
      return undefined;
    }
    if (session.expiresAtUtc && new Date(session.expiresAtUtc).getTime() <= Date.now()) {
      if (clearStoredSessionIfMatches(session)) {
        clearVerifiedSession(session);
        startTransition(() => {
          setLoading(false);
          navigate(session.accountType === 'customer' ? '/client/auth' : '/admin/auth', {
            replace: true,
            state: { sessionExpired: true },
          });
        });
      }
      return undefined;
    }

    void verifySession(session);
    return () => { verificationVersion.current += 1; };
  }, []);

  useEffect(() => {
    const onExpired = (event) => {
      const expiredType = principal?.type || event.detail?.accountType || readStoredSession()?.accountType;
      verificationVersion.current += 1;
      clearVerifiedSession();
      // Match BrowserRouter's transition so AuthArea cannot overwrite the expiry redirect.
      startTransition(() => {
        setPrincipal(null);
        setExpiresAtUtc(null);
        setLoading(false);
        if (expiredType) navigate(expiredType === 'customer' ? '/client/auth' : '/admin/auth', {
          replace: true,
          state: { sessionExpired: true },
        });
      });
    };
    window.addEventListener(AUTH_EXPIRED_EVENT, onExpired);
    return () => window.removeEventListener(AUTH_EXPIRED_EVENT, onExpired);
  }, [navigate, principal]);

  useEffect(() => {
    if (!expiresAtUtc) return undefined;
    const session = readStoredSession();
    if (!isSessionVerified(session)) return undefined;
    const delay = new Date(expiresAtUtc).getTime() - Date.now();
    if (delay <= 0) {
      if (!clearStoredSessionIfMatches(session)) return undefined;
      verificationVersion.current += 1;
      clearVerifiedSession(session);
      startTransition(() => {
        setPrincipal(null);
        setExpiresAtUtc(null);
        setLoading(false);
        navigate(session.accountType === 'customer' ? '/client/auth' : '/admin/auth', {
          replace: true,
          state: { sessionExpired: true },
        });
      });
      return undefined;
    }
    const timer = window.setTimeout(() => {
      if (!clearStoredSessionIfMatches(session)) return;
      verificationVersion.current += 1;
      clearVerifiedSession(session);
      startTransition(() => {
        setPrincipal(null);
        setExpiresAtUtc(null);
        setLoading(false);
        navigate(session.accountType === 'customer' ? '/client/auth' : '/admin/auth', {
          replace: true,
          state: { sessionExpired: true },
        });
      });
    }, delay);
    return () => window.clearTimeout(timer);
  }, [expiresAtUtc, navigate]);

  useEffect(() => {
    const onStorage = (event) => {
      if (event.key !== AUTH_STORAGE_KEY) return;
      const session = readStoredSession();
      if (session && isSessionVerified(session)) return;
      if (session) {
        void verifySession(session);
        return;
      }
      verificationVersion.current += 1;
      clearVerifiedSession();
      let oldAccountType;
      try {
        oldAccountType = JSON.parse(event.oldValue || 'null')?.accountType;
      } catch {
        oldAccountType = null;
      }
      const accountType = principal?.type || oldAccountType;
      setPrincipal(null);
      setExpiresAtUtc(null);
      setLoading(false);
      if (accountType) navigate(accountType === 'customer' ? '/client/auth' : '/admin/auth', { replace: true });
    };
    window.addEventListener('storage', onStorage);
    return () => window.removeEventListener('storage', onStorage);
  }, [navigate, principal]);

  async function signIn(accountType, credentials) {
    const result = accountType === 'customer'
      ? await loginCustomer(credentials)
      : await loginEmployee(credentials);
    const session = { accessToken: result.accessToken, accountType, expiresAtUtc: result.expiresAtUtc };
    storeSession(session);
    const current = await verifySession(session, { redirectOnFailure: false, throwOnFailure: true });
    if (!current) throw new Error('Phiên đăng nhập đã thay đổi. Vui lòng đăng nhập lại.');
    return current;
  }

  async function signOut() {
    const session = readStoredSession();
    if (!session || !isSessionVerified(session) || principal?.type !== session.accountType) return;
    try {
      if (session) await logoutRequest(session.accountType);
    } catch {
      // The local JWT must be removed even when the API is unavailable.
    } finally {
      if (session && clearStoredSessionIfMatches(session)) {
        verificationVersion.current += 1;
        clearVerifiedSession(session);
        setPrincipal(null);
        setExpiresAtUtc(null);
        setLoading(false);
        navigate(session.accountType === 'customer' ? '/client/auth' : '/admin/auth', { replace: true });
      }
    }
  }

  return <AuthContext.Provider value={{ principal, loading, signIn, signOut }}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used within AuthProvider.');
  return value;
}
