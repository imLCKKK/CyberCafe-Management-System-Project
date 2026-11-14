import React from 'react';
import { walletMoney } from './customerWalletApi.js';
import './counter-topups.css';

export function CounterTopUpAmounts({ request }) {
  return <dl className="counter-topup-amounts">
    <div><dt>Tiền mặt cần thu</dt><dd>{walletMoney(request.amount)}</dd></div>
    <div><dt>Tiền tặng</dt><dd>{walletMoney(request.bonusAmount)}</dd></div>
    <div><dt>Tổng cộng vào ví</dt><dd>{walletMoney(request.totalAmount)}</dd></div>
  </dl>;
}
