import React, { useState } from 'react';
import { ArrowRight, LoaderCircle, LockKeyhole, ShieldCheck, UserRound } from 'lucide-react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { getPrincipalDestination, useAuth } from './AuthContext';
import Brand from '../branding/Brand';
import './auth.css';

export function LoginPage({ accountType }) {
  const isCustomer = accountType === 'customer';
  const { principal, loading: sessionLoading, signIn } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [identifier, setIdentifier] = useState('');
  const [password, setPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');

  if (sessionLoading) return <main className="auth-loading" aria-label="Đang kiểm tra phiên"><LoaderCircle className="auth-spinner"/></main>;
  if (principal) return <Navigate to={getPrincipalDestination(principal)} replace/>;

  async function handleSubmit(event) {
    event.preventDefault();
    setError('');
    setSubmitting(true);
    try {
      const credentials = isCustomer
        ? { phone: identifier.trim(), password }
        : { username: identifier.trim(), password };
      const current = await signIn(accountType, credentials);
      navigate(getPrincipalDestination(current), { replace: true });
    } catch (cause) {
      setError(cause.message || 'Không thể đăng nhập. Vui lòng thử lại.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="auth-page">
      <img className="auth-backdrop" src="/branding/nexora-login-background.png" alt="" aria-hidden="true" fetchpriority="high"/>
      <section className="auth-panel" aria-labelledby="auth-title">
        <Link className="auth-brand" to={isCustomer ? '/client/home' : '/admin/dashboard'} aria-label={`NEXORA CYBER · ${isCustomer ? 'Khách hàng' : 'Nhân viên'}`}>
          <Brand area={isCustomer ? 'Khách hàng' : 'Nhân viên'}/>
        </Link>
        <div className="auth-heading">
          <p className="auth-area"><ShieldCheck size={15}/>{isCustomer ? 'KHU VỰC NGƯỜI CHƠI' : 'HỆ THỐNG QUẢN LÝ'}</p>
          <h1 id="auth-title">{isCustomer ? 'Đăng nhập tài khoản' : 'Đăng nhập nhân viên'}</h1>
          <span>{isCustomer ? 'Sử dụng số điện thoại đã đăng ký.' : 'Đăng nhập bằng tài khoản nhân viên được cấp.'}</span>
        </div>
        {location.state?.sessionExpired && <div className="auth-notice" role="status">Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.</div>}
        <form className="auth-form" onSubmit={handleSubmit}>
          <label htmlFor="auth-identifier">{isCustomer ? 'Số điện thoại' : 'Tên đăng nhập'}</label>
          <div className="auth-input-wrap"><UserRound size={17}/><input id="auth-identifier" autoComplete={isCustomer ? 'tel' : 'username'} inputMode={isCustomer ? 'tel' : undefined} placeholder={isCustomer ? 'Nhập số điện thoại' : 'Nhập tên đăng nhập'} value={identifier} onChange={(event) => setIdentifier(event.target.value)} required disabled={submitting}/></div>
          <label htmlFor="auth-password">Mật khẩu</label>
          <div className="auth-input-wrap"><LockKeyhole size={17}/><input id="auth-password" type="password" autoComplete="current-password" placeholder="Nhập mật khẩu" value={password} onChange={(event) => setPassword(event.target.value)} required disabled={submitting}/></div>
          {error && <div className="auth-error" role="alert">{error}</div>}
          <button className="auth-submit" type="submit" disabled={submitting}>
            {submitting ? <><LoaderCircle className="auth-spinner" size={17}/> Đang đăng nhập</> : <>Đăng nhập <ArrowRight size={17}/></>}
          </button>
        </form>
        <footer className="auth-footer">
          <span>{isCustomer ? 'Nhân viên tại quầy có thể hỗ trợ tài khoản.' : 'Chỉ tài khoản nhân viên được cấp quyền mới đăng nhập được.'}</span>
          <Link to={isCustomer ? '/admin/auth' : '/client/auth'}>{isCustomer ? 'Đăng nhập nhân viên' : 'Đăng nhập khách hàng'}</Link>
        </footer>
      </section>
    </main>
  );
}
