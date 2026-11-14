import React, { useCallback, useState } from 'react';
import { WalletCards } from 'lucide-react';
import { useAuth } from '../../shared/auth/AuthContext';
import { getCustomerWallet, walletMoney } from '../../shared/customerWalletApi.js';
import { useCustomerQuery } from '../../shared/useCustomerQuery.js';
import { WalletHistory } from '../../shared/WalletHistory';
import { PageHeading } from '../shared/ClientUI';
import { CounterTopUpPanel } from './CounterTopUpPanel';
import { useRealtime } from '../../shared/realtime/useRealtime';

const walletEvents = ['WalletChanged'];

export function WalletPage() {
  const { principal, accessToken } = useAuth();
  const [page, setPage] = useState(1);
  const load = useCallback((signal) => getCustomerWallet({ page, signal }), [page]);
  const { data: wallet, loading, error, reload } = useCustomerQuery(`own-wallet:${principal?.customerId}:${page}`, load, principal?.type === 'customer');
  useRealtime({ enabled: principal?.type === 'customer', accessToken, events: walletEvents, onEvent: reload, onReady: reload, onAuthFailure: reload });
  return <div className="cafe-home client-module wallet-page">
    <PageHeading eyebrow="VÍ NGƯỜI CHƠI" title={<>Ví của <span>bạn.</span></>} description="Số dư và lịch sử giao dịch của tài khoản đang đăng nhập.">
      <button className="home-button" disabled={loading} onClick={reload}>Tải lại ví</button>
    </PageHeading>
    {loading ? <section className="home-panel home-empty" role="status">Đang tải ví…</section>
      : error ? <section className="home-panel home-empty"><p className="home-alert" role="alert">{error.message}</p><button className="home-button" onClick={reload}>Thử lại</button></section>
        : wallet && <>
          <section className="home-panel wallet-hero" aria-label="Số dư ví"><div className="wallet-hero-top"><span><WalletCards size={20}/> VÍ #{wallet.customerId}</span></div>
            <span>Số dư khả dụng</span><div className="home-balance">{walletMoney(wallet.balance)}</div><p>{principal.fullName}</p>
            <p className="home-fine-print">Gửi yêu cầu nạp bên dưới, sau đó mang tiền mặt tới quầy để nhân viên xác nhận.</p>
          </section>
          <CounterTopUpPanel key={wallet.customerId} customerId={wallet.customerId} onChanged={reload}/>
          <div className="home-section-heading"><h2>Lịch sử ví</h2></div><section className="home-panel"><WalletHistory wallet={wallet} onPage={setPage}/></section>
        </>}
  </div>;
}
