import React from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { AdminLayout } from './AdminLayout';
import { ClientLayout } from './ClientLayout';
import HomePage from '../client/home/HomePage';
import { OrderingPage } from '../client/ordering/OrderingPage';
import { TournamentsPage } from '../client/tournaments/TournamentsPage';
import ClientPlaceholder from '../client/ClientPlaceholder';
import { DashboardPage } from '../admin/dashboard/DashboardPage';
import { ComputersPage } from '../admin/computers/ComputersPage';

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

const adminPage=(title,owner)=><div className="page"><div className="placeholder"><h2>{title}</h2><p>Module của thành viên {owner}. Hãy triển khai tại thư mục tương ứng.</p><span>CHƯA TRIỂN KHAI</span></div></div>;

export function AppRoutes(){
  return <Routes>
    <Route path="/" element={<Navigate to="/client/home" replace/>}/>
    <Route path="/admin" element={<AdminLayout/>}>
      {Object.entries(admin).map(([path,[title,owner]])=><Route key={path} path={path} element={path === 'dashboard' ? <DashboardPage/> : path === 'computers' ? <ComputersPage/> : adminPage(title,owner)}/>)}
      <Route path="auth" element={adminPage('Admin Auth','A')}/>
      <Route path="promotions" element={adminPage('Khuyến mãi','B')}/>
    </Route>
    <Route path="/client" element={<ClientLayout/>}>
      <Route path="home" element={<HomePage/>}/>
      {Object.entries(clientPages).map(([path,[title,description]])=><Route key={path} path={path} element={path === 'ordering' ? <OrderingPage/> : path === 'tournaments' ? <TournamentsPage/> : <ClientPlaceholder title={title} description={description}/>}/>)}
      <Route path="auth" element={<ClientPlaceholder title="Đăng nhập" description="Khung xác thực client."/>}/>
    </Route>
    <Route path="*" element={<Navigate to="/client/home" replace/>}/>
  </Routes>
}
