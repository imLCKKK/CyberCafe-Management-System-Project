import React, { useState } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import { 
  LayoutDashboard, 
  Monitor, 
  Users, 
  ShoppingCart, 
  Package, 
  Receipt, 
  Trophy, 
  UserCog, 
  BarChart3, 
  Menu, 
  ChevronRight,
  LogOut,
} from 'lucide-react';
import './layout.css';
import { AdminProvider } from '../admin/shared/AdminContext';
import { useAuth } from '../shared/auth/AuthContext';

const menu = [
  ['/admin/dashboard', 'Tổng quan', LayoutDashboard],
  ['/admin/computers', 'Máy', Monitor],
  ['/admin/customers', 'Khách hàng', Users],
  ['/admin/orders', 'Order', ShoppingCart],
  ['/admin/products', 'Sản phẩm', Package],
  ['/admin/inventory', 'Kho', Package],
  ['/admin/invoices', 'Hóa đơn & thanh toán', Receipt],
  ['/admin/tournaments', 'Giải đấu', Trophy],
  ['/admin/staff', 'Nhân viên', UserCog],
  ['/admin/reports', 'Báo cáo', BarChart3]
];

export function AdminLayout() {
  return <AdminProvider><AdminShell/></AdminProvider>;
}

function AdminShell() {
  const [collapsed, setCollapsed] = useState(false);
  const { principal, signOut } = useAuth();
  const visibleMenu = principal?.type === 'employee'
    ? menu.filter(([path]) => {
      const permissions = {
        '/admin/computers': ['computer.view'], '/admin/customers': ['customer.view'], '/admin/orders': ['order.view'],
        '/admin/products': ['product.view'], '/admin/inventory': ['stockreceipt.view', 'inventory.adjust'],
        '/admin/invoices': ['invoice.view'], '/admin/tournaments': ['tournament.view'], '/admin/staff': ['employee.view'],
        '/admin/reports': ['report.view'],
      }[path];
      return !permissions || permissions.some((permission) => principal.permissions.includes(permission));
    })
    : menu;
  const displayName = principal?.type === 'employee' ? principal.username : 'Bản demo';
  const displayRole = principal?.type === 'employee' ? principal.role : 'Chưa đăng nhập';

  return (
    <div className={`admin-shell ${collapsed ? 'collapsed' : ''}`}>
      {/* Sidebar */}
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-mark">N</div>
          {!collapsed && (
            <div className="brand-copy">
              <b>NET CAFE</b>
              <span>Management</span>
            </div>
          )}
        </div>

        <nav className="nav">
          {!collapsed && <div className="nav-label">QUẢN LÝ</div>}
          {visibleMenu.map(([to, label, Icon]) => (
            <NavLink 
              key={to} 
              to={to} 
              title={label}
              aria-label={label}
              className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}
            >
              <Icon size={19} />
              {!collapsed && <span>{label}</span>}
            </NavLink>
          ))}
        </nav>

        <div className="sidebar-footer">
          {!collapsed && (
            <div className="user">
              <div className="avatar">A</div>
              <div>
                <b>{displayName}</b>
                <small>{displayRole}</small>
              </div>
            </div>
          )}
          {principal?.type === 'employee' && <button className="admin-logout" aria-label="Đăng xuất" title="Đăng xuất" onClick={() => void signOut()}><LogOut size={18}/></button>}
          <button aria-label={collapsed ? 'Mở rộng menu' : 'Thu gọn menu'} aria-expanded={!collapsed} onClick={() => setCollapsed(v => !v)}>
            <Menu size={19} />
          </button>
        </div>
      </aside>

      {/* Main Content Area */}
      <main className="main">
        <header className="topbar">
          <div>
            <div className="breadcrumb">
              Quản lý <ChevronRight size={14} /> Admin
            </div>
            <h1>Net Cafe Management</h1>
          </div>
        </header>
        <Outlet />
      </main>
    </div>
  );
}
