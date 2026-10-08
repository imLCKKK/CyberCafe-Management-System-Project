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
  BadgePercent,
  UserCog, 
  BarChart3, 
  Menu, 
  ChevronRight 
} from 'lucide-react';
import './layout.css';

const menu = [
  ['/admin/dashboard', 'Tổng quan', LayoutDashboard],
  ['/admin/computers', 'Máy', Monitor],
  ['/admin/customers', 'Khách hàng', Users],
  ['/admin/orders', 'Order', ShoppingCart],
  ['/admin/products', 'Sản phẩm', Package],
  ['/admin/promotions', 'Khuyến mãi', BadgePercent],
  ['/admin/inventory', 'Kho', Package],
  ['/admin/invoices', 'Hóa đơn & thanh toán', Receipt],
  ['/admin/tournaments', 'Giải đấu', Trophy],
  ['/admin/staff', 'Nhân viên', UserCog],
  ['/admin/reports', 'Báo cáo', BarChart3]
];

export function AdminLayout() {
  const [collapsed, setCollapsed] = useState(false);

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
          {menu.map(([to, label, Icon]) => (
            <NavLink 
              key={to} 
              to={to} 
              title={collapsed ? label : ''} 
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
                <b>Admin</b>
                <small>Quản trị viên</small>
              </div>
            </div>
          )}
          <button onClick={() => setCollapsed(v => !v)}>
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