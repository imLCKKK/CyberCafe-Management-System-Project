import React, { createContext, useContext, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  AUTH_EXPIRED_EVENT,
  AUTH_STORAGE_KEY,
  clearStoredSession,
  getCurrentPrincipal,
  loginCustomer,
  loginEmployee,
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

  useEffect(() => {
    let active = true;
    const session = readStoredSession();
    if (!session) {
      setLoading(false);
      return () => { active = false; };
    }
    if (session.expiresAtUtc && new Date(session.expiresAtUtc).getTime() <= Date.now()) {
      clearStoredSession();
      setLoading(false);
      navigate(session.accountType === 'customer' ? '/client/auth' : '/admin/auth', {
        replace: true,
        state: { sessionExpired: true },
      });
      return () => { active = false; };
    }

    getCurrentPrincipal(session.accountType)
      .then((identity) => {
        if (active) {
          setPrincipal(identityType(session.accountType, identity));
          setExpiresAtUtc(session.expiresAtUtc || null);
        }
      })
      .catch(() => {
        clearStoredSession();
        if (active) {
          setPrincipal(null);
          setExpiresAtUtc(null);
        }
      })
      .finally(() => { if (active) setLoading(false); });

    return () => { active = false; };
  }, []);

  useEffect(() => {
    const onExpired = (event) => {
      const expiredType = principal?.type || event.detail?.accountType || readStoredSession()?.accountType;
      setPrincipal(null);
      if (expiredType) navigate(expiredType === 'customer' ? '/client/auth' : '/admin/auth', {
        replace: true,
        state: { sessionExpired: true },
      });
    };
    window.addEventListener(AUTH_EXPIRED_EVENT, onExpired);
    return () => window.removeEventListener(AUTH_EXPIRED_EVENT, onExpired);
  }, [navigate, principal]);

  useEffect(() => {
    if (!expiresAtUtc) return undefined;
    const delay = new Date(expiresAtUtc).getTime() - Date.now();
    if (delay <= 0) {
      const session = readStoredSession();
      clearStoredSession();
      setPrincipal(null);
      navigate(session?.accountType === 'customer' ? '/client/auth' : '/admin/auth', {
        replace: true,
        state: { sessionExpired: true },
      });
      return undefined;
    }
    const timer = window.setTimeout(() => {
      const session = readStoredSession();
      clearStoredSession();
      setPrincipal(null);
      navigate(session?.accountType === 'customer' ? '/client/auth' : '/admin/auth', {
        replace: true,
        state: { sessionExpired: true },
      });
    }, delay);
    return () => window.clearTimeout(timer);
  }, [expiresAtUtc, navigate]);

  useEffect(() => {
    const onStorage = (event) => {
      if (event.key !== AUTH_STORAGE_KEY || event.newValue) return;
      const accountType = principal?.type;
      setPrincipal(null);
      setExpiresAtUtc(null);
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
    setExpiresAtUtc(result.expiresAtUtc || null);
    try {
      const identity = await getCurrentPrincipal(accountType);
      const current = identityType(accountType, identity);
      setPrincipal(current);
      return current;
    } catch (error) {
      clearStoredSession();
      setExpiresAtUtc(null);
      throw error;
    }
  }

  async function signOut() {
    const session = readStoredSession();
    try {
      if (session) await logoutRequest(session.accountType);
    } catch {
      // The local JWT must be removed even when the API is unavailable.
    } finally {
      clearStoredSession();
      setPrincipal(null);
      setExpiresAtUtc(null);
      navigate(session?.accountType === 'customer' ? '/client/auth' : '/admin/auth', { replace: true });
    }
  }

  return <AuthContext.Provider value={{ principal, loading, signIn, signOut }}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used within AuthProvider.');
  return value;
}