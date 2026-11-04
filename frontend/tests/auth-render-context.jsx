import React, { createContext, useContext } from 'react';
import { useLocation } from 'react-router-dom';

const AuthContext = createContext(null);
const employee = {
  type: 'employee',
  username: 'render-test',
  role: 'Admin',
  permissions: [
    'order.view',
    'computer.view',
    'customer.view',
    'product.view',
    'stockreceipt.view',
    'inventory.adjust',
    'invoice.view',
    'tournament.view',
    'employee.view',
    'report.view',
    'promotion.manage',
  ],
};
const customer = {
  type: 'customer',
  fullName: 'Render Test Customer',
  phone: '0900000000',
  permissions: [],
};

export function AuthProvider({ children }) {
  const { pathname } = useLocation();
  const principal = pathname.startsWith('/client') ? customer : employee;
  const value = { principal, loading: false, signIn: async () => principal, signOut: async () => {} };
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function getPrincipalDestination(principal) {
  if (principal?.type === 'customer') return '/client/home';
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
  return destinations.find(([permission]) => principal?.permissions?.includes(permission))?.[1] || '/admin/dashboard';
}

export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used within AuthProvider.');
  return value;
}
