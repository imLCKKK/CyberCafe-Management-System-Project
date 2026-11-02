import React from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { AdminLayout } from './AdminLayout';
import { ClientLayout } from './ClientLayout';
import HomePage from '../client/home/HomePage';
import { OrderingPage } from '../client/ordering/OrderingPage';
import { TournamentsPage } from '../client/tournaments/TournamentsPage';
import { WalletPage } from '../client/wallet/WalletPage';
import { InvoicesPage } from '../client/invoices/InvoicesPage';
import { AccountPage } from '../client/account/AccountPage';
import { DashboardPage } from '../admin/dashboard/DashboardPage';
import { ComputersPage } from '../admin/computers/ComputersPage';
import { CustomersPage } from '../admin/customers/CustomersPage';
import { OrdersPage } from '../admin/orders/OrdersPage';
import { ProductsPage } from '../admin/products/ProductsPage';
import { InventoryPage } from '../admin/inventory/InventoryPage';
import { InvoicesPage as AdminInvoicesPage } from '../admin/invoices/InvoicesPage';
import { TournamentsPage as AdminTournamentsPage } from '../admin/tournaments/TournamentsPage';
import { StaffPage } from '../admin/staff/StaffPage';
import { ReportsPage } from '../admin/reports/ReportsPage';
import ClientPlaceholder from '../client/ClientPlaceholder';

const admin={
  dashboard:['Tổng quan','C'],computers:['Máy','C'],customers:['Khách hàng','B'],orders:['Order','D'],products:['Sản phẩm','D'],inventory:['Kho','E'],invoices:['Hóa đơn & thanh toán','E'],tournaments:['Giải đấu','B'],staff:['Nhân viên','A'],reports:['Báo cáo','E']
};

const clientPages={
  tournaments:['Giải đấu','Danh sách giải, đăng ký và team của tôi.'],
  wallet:['Ví','Nạp tiền và xem lịch sử giao dịch.'],
  ordering:['Gọi món','Menu đồ ăn, giỏ hàng và theo dõi order.'],
  invoices:['Hóa đơn','Hóa đơn và lịch sử chơi.'],
  account:['Tài khoản','Hồ sơ và cài đặt tài khoản.'],
};

const clientComponents = { tournaments: TournamentsPage, ordering: OrderingPage, wallet: WalletPage, invoices: InvoicesPage, account: AccountPage };
const adminComponents = { dashboard: DashboardPage, computers: ComputersPage, customers: CustomersPage, orders: OrdersPage, products: ProductsPage, inventory: InventoryPage, invoices: AdminInvoicesPage, tournaments: AdminTournamentsPage, staff: StaffPage, reports: ReportsPage };

const adminPage=(title,owner)=><div className="page"><div className="placeholder"><h2>{title}</h2><p>Module của thành viên {owner}. Hãy triển khai tại thư mục tương ứng.</p><span>CHƯA TRIỂN KHAI</span></div></div>;

export function AppRoutes(){
  return <Routes>
    <Route path="/" element={<Navigate to="/client/home" replace/>}/>
    <Route path="/admin" element={<AdminLayout/>}>
      <Route index element={<Navigate to="dashboard" replace/>}/>
      {Object.keys(admin).map((path) => { const Page = adminComponents[path]; return <Route key={path} path={path} element={<Page/>}/>; })}
      <Route path="auth" element={adminPage('Admin Auth','A')}/>
      <Route path="promotions" element={adminPage('Khuyến mãi','B')}/>
    </Route>
    <Route path="/client" element={<ClientLayout/>}>
      <Route index element={<Navigate to="home" replace/>}/>
      <Route path="home" element={<HomePage/>}/>
      {Object.keys(clientPages).map((path) => { const Page = clientComponents[path]; return <Route key={path} path={path} element={<Page/>}/>; })}
      <Route path="auth" element={<ClientPlaceholder title="Đăng nhập" description="Khung xác thực client."/>}/>
    </Route>
    <Route path="*" element={<Navigate to="/client/home" replace/>}/>
  </Routes>
}
