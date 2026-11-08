import React, { useCallback } from 'react';
import { Link } from 'react-router-dom';
import { Phone, ShieldCheck, UserRound } from 'lucide-react';
import { useAuth } from '../../shared/auth/AuthContext';
import { customerDate, customerStatus, getCustomerProfile } from '../../shared/customerWalletApi.js';
import { useCustomerQuery } from '../../shared/useCustomerQuery.js';
import { PageHeading } from '../shared/ClientUI';

export function AccountPage() {
  const { principal } = useAuth();
  const load = useCallback((signal) => getCustomerProfile(undefined, signal), []);
  const { data: profile, loading, error, reload } = useCustomerQuery(`own-profile:${principal?.customerId}`, load, principal?.type === 'customer');
  return <div className="cafe-home client-module account-page">
    <PageHeading eyebrow="HỒ SƠ NGƯỜI CHƠI" title={<>Tài khoản của <span>bạn.</span></>} description="Thông tin hồ sơ được lưu cho tài khoản đang đăng nhập.">
      <button className="home-button" disabled={loading} onClick={reload}>Tải lại hồ sơ</button>
    </PageHeading>
    {loading ? <section className="home-panel home-empty" role="status">Đang tải hồ sơ…</section>
      : error ? <section className="home-panel home-empty"><p className="home-alert" role="alert">{error.message}</p><button className="home-button" onClick={reload}>Thử lại</button></section>
        : profile && <section className="home-panel account-profile"><div className="account-avatar"><UserRound size={35}/></div><h2>{profile.fullName}</h2><div className="account-id">ID #{profile.customerId}</div>
          <div className="account-summary-line"><Phone size={15}/><span>{profile.phone}</span></div>
          <div className="account-summary-line"><ShieldCheck size={15}/><span>{customerStatus(profile.status)}</span></div>
          <div className="home-detail-row"><span>Email</span><strong>{profile.email || 'Chưa cung cấp'}</strong></div>
          <div className="home-detail-row"><span>Ngày tạo</span><strong>{customerDate(profile.createdAtUtc)}</strong></div>
          <p className="home-fine-print">Liên hệ nhân viên nếu cần điều chỉnh thông tin hồ sơ.</p><Link className="home-button" to="/client/wallet">Xem số dư và lịch sử ví</Link>
        </section>}
  </div>;
}
