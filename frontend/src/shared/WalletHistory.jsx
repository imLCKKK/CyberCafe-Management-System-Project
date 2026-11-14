import React from 'react';
import { customerDate, walletMoney, walletTypeLabel } from './customerWalletApi.js';
import './wallet-history.css';

export function CustomerPagination({ data, onPage, label = 'Trang' }) {
  const pages = Math.max(1, Math.ceil(data.totalCount / data.pageSize));
  return <div className="admin-pagination"><span>{data.totalCount} bản ghi · {label} {data.page} / {pages}</span><div>
    <button className="cafe-button home-button" disabled={data.page <= 1} onClick={() => onPage(data.page - 1)} aria-label={`${label} trước`}>Trước</button>
    <button className="cafe-button home-button" disabled={data.page >= pages} onClick={() => onPage(data.page + 1)} aria-label={`${label} sau`}>Sau</button>
  </div></div>;
}

export function WalletHistory({ wallet, onPage }) {
  return <div className="customer-wallet-history"><div className="home-table-wrap cafe-table-wrap" tabIndex={0} role="region" aria-label="Lịch sử giao dịch ví"><table className="home-table cafe-table client-data-table"><thead><tr>
    <th>Giao dịch</th><th>Thời gian</th><th>Số tiền</th><th>Số dư sau</th>
  </tr></thead><tbody>{wallet.transactions.map((row) => <tr key={row.transactionId}>
    <td><strong>{walletTypeLabel(row.type)}</strong><small className="client-table-sub">GD{row.transactionId}{row.description ? ` · ${row.description}` : ''}</small></td>
    <td>{customerDate(row.createdAtUtc)}</td><td className={row.amount > 0 ? 'client-positive' : ''}>{row.amount > 0 ? '+' : ''}{walletMoney(row.amount)}</td><td>{walletMoney(row.balanceAfter)}</td>
  </tr>)}</tbody></table></div>
  {!wallet.transactions.length && <div className="home-empty cafe-empty"><h3>Chưa có giao dịch ví</h3><p>Lịch sử sẽ hiển thị khi có giao dịch được ghi nhận.</p></div>}
  <CustomerPagination data={wallet} onPage={onPage} label="Trang lịch sử"/></div>;
}
