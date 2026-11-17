import React, { useCallback, useState } from 'react';
import { WalletCards } from 'lucide-react';
import { useAuth } from '../../shared/auth/AuthContext';
import { getCustomerWallet, walletMoney } from '../../shared/customerWalletApi.js';
import { useCustomerQuery } from '../../shared/useCustomerQuery.js';
import { WalletHistory } from '../../shared/WalletHistory';
import { PageHeading } from '../shared/ClientUI';
import { CounterTopUpPanel } from './CounterTopUpPanel';
import { ZaloPayTopUpPanel } from './ZaloPayTopUpPanel';
import { useRealtime } from '../../shared/realtime/useRealtime';

const walletEvents = ['WalletChanged', 'ZaloPayPaymentChanged'];

export function WalletPage() {
  const { principal, accessToken } = useAuth();
  const customerId = Number(principal?.customerId);
  const isCustomer = principal?.type === 'customer' && Number.isSafeInteger(customerId) && customerId > 0;
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
            <p className="home-fine-print">Chọn nạp tiền tại quầy hoặc thanh toán bằng ZaloPay Sandbox bên dưới.</p>
          </section>
        </>}
    {isCustomer && <>
      <ZaloPayTopUpPanel key={`zalopay:${customerId}`} customerId={customerId} onChanged={reload}/>
      <CounterTopUpPanel key={`counter:${customerId}`} customerId={customerId} onChanged={reload}/>
    </>}
    {wallet && !loading && !error && <><div className="home-section-heading"><h2>Lịch sử ví</h2></div><section className="home-panel"><WalletHistory wallet={wallet} onPage={setPage}/></section></>}
  </div>;
}
