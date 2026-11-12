import React, { useRef, useState } from 'react';
import { NavLink, Outlet, useLocation } from 'react-router-dom';
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
import Brand from '../shared/branding/Brand';

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
  const menuToggle = useRef(null);
  const { pathname } = useLocation();
  const { principal, signOut } = useAuth();
  const visibleMenu = principal?.type === 'employee'
    ? menu.filter(([path]) => {
      const permissions = {
        '/admin/computers': ['computer.view'], '/admin/customers': ['customer.view'], '/admin/orders': ['order.view'],
        '/admin/products': ['product.view'], '/admin/inventory': ['stockreceipt.view'],
        '/admin/invoices': ['invoice.view'], '/admin/tournaments': ['tournament.view'], '/admin/staff': ['employee.view'],
        '/admin/reports': ['report.view'],
      }[path];
      return !permissions || permissions.some((permission) => principal.permissions.includes(permission));
    })
    : menu;
  const displayName = principal?.type === 'employee' ? principal.username : 'Bản demo';
  const displayRole = principal?.type === 'employee' ? principal.role : 'Chưa đăng nhập';
  const pageTitle = menu.find(([path]) => path === pathname)?.[1] || 'Quản trị';

  return (
    <div className={`admin-shell ${collapsed ? 'collapsed' : ''}`}>
      <a className="skip-link" href="#admin-content">Đến nội dung chính</a>
      {/* Sidebar */}
      <aside className="sidebar" onKeyDown={(event) => {
        if (event.key === 'Escape' && !collapsed) {
          setCollapsed(true);
          menuToggle.current?.focus();
        }
      }}>
        <div className="sidebar-brand">
          <Brand area="Quản trị" compact={collapsed}/>
        </div>

        <nav className="nav" id="admin-navigation" aria-label="Điều hướng quản trị">
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
          <button ref={menuToggle} aria-controls="admin-navigation" aria-label={collapsed ? 'Mở rộng menu' : 'Thu gọn menu'} aria-expanded={!collapsed} onClick={() => setCollapsed(v => !v)}>
            <Menu size={19} />
          </button>
        </div>
      </aside>

      {/* Main Content Area */}
      <main className="main" id="admin-content" tabIndex={-1}>
        <header className="topbar">
          <Brand compact/>
          <div className="topbar-copy">
            <strong>NEXORA CYBER</strong>
            <div className="breadcrumb">
              Quản trị <ChevronRight size={14} /> {pageTitle}
            </div>
          </div>
        </header>
        <Outlet />
      </main>
    </div>
  );
}
