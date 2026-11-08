import React, { useCallback, useState } from 'react';
import { Plus, Users } from 'lucide-react';
import { PageHeading, StatCard } from '../computers/components/CafeUI';
import { DetailDialog, FormDialog, Notice, Panel, Row } from '../shared/AdminUI';
import { useAuth } from '../../shared/auth/AuthContext';
import { createCustomer, customerDate, customerStatus, getCustomerProfile, getCustomers, getCustomerWallet, walletMoney } from '../../shared/customerWalletApi.js';
import { useCustomerQuery } from '../../shared/useCustomerQuery.js';
import { CustomerPagination, WalletHistory } from '../../shared/WalletHistory';

function CustomerDetails({ customerId, canViewWallet, onClose }) {
  const [page, setPage] = useState(1);
  const loadProfile = useCallback((signal) => getCustomerProfile(customerId, signal), [customerId]);
  const loadWallet = useCallback((signal) => getCustomerWallet({ customerId, page, signal }), [customerId, page]);
  const profile = useCustomerQuery(`customer:${customerId}`, loadProfile);
  const wallet = useCustomerQuery(`customer-wallet:${customerId}:${page}`, loadWallet, canViewWallet);
  return <DetailDialog title="Hồ sơ khách hàng" onClose={onClose}>
    {profile.loading ? <p role="status">Đang tải hồ sơ…</p> : profile.error ? <><p role="alert" className="cafe-error">{profile.error.message}</p><button className="cafe-button" onClick={profile.reload}>Thử lại hồ sơ</button></> : profile.data && <>
      <Row label="Mã khách">KH{profile.data.customerId}</Row><Row label="Họ và tên">{profile.data.fullName}</Row><Row label="Số điện thoại">{profile.data.phone}</Row>
      <Row label="Email">{profile.data.email || 'Chưa cung cấp'}</Row><Row label="Trạng thái">{customerStatus(profile.data.status)}</Row><Row label="Ngày tạo">{customerDate(profile.data.createdAtUtc)}</Row>
    </>}
    {canViewWallet && <><h3 className="admin-section-title">Ví khách hàng</h3>{wallet.loading ? <p role="status">Đang tải ví…</p> : wallet.error ? <><p role="alert" className="cafe-error">{wallet.error.message}</p><button className="cafe-button" onClick={wallet.reload}>Thử lại ví</button></> : wallet.data && <><Row label="Số dư ví">{walletMoney(wallet.data.balance)}</Row><WalletHistory wallet={wallet.data} onPage={setPage}/></>}</>}
  </DetailDialog>;
}

export function CustomersPage() {
  const { principal } = useAuth();
  const permissions = principal?.permissions || [];
  const canView = principal?.type === 'employee' && permissions.includes('customer.view');
  const canCreate = permissions.includes('customer.create');
  const canViewWallet = permissions.includes('wallet.view');
  const [query, setQuery] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [creating, setCreating] = useState(false);
  const [selected, setSelected] = useState(null);
  const [notice, setNotice] = useState('');
  const load = useCallback((signal) => getCustomers({ page, search, signal }), [page, search]);
  const { data, loading, error, reload } = useCustomerQuery(`customers:${principal?.employeeId}:${page}:${search}`, load, canView);
  return <section className="cafe-page admin-module">
    <PageHeading eyebrow="QUẢN TRỊ / KHÁCH HÀNG" title="Quản lý khách hàng" description="Tạo và tra cứu hồ sơ khách hàng, số dư cùng lịch sử ví.">
      {canCreate && <button className="cafe-button primary" onClick={() => setCreating(true)}><Plus size={16}/>Thêm khách hàng</button>}
    </PageHeading>
    <Notice notice={notice} clear={() => setNotice('')}/>
    {!canView ? <p className="cafe-error" role="alert">Bạn không có quyền xem khách hàng.</p> : <>
      <Panel title="Danh sách khách hàng" actions={<button className="cafe-button" disabled={loading} onClick={reload}>Tải lại danh sách</button>}>
        <form className="admin-toolbar" onSubmit={(event) => { event.preventDefault(); setPage(1); setSearch(query.trim()); }}>
          <label className="cafe-search"><input aria-label="Tìm tên, số điện thoại khách hàng" placeholder="Tìm tên, số điện thoại…" value={query} onChange={(event) => setQuery(event.target.value)} maxLength={150}/></label>
          <button className="cafe-button" type="submit">Tìm kiếm</button><button className="cafe-button" type="button" onClick={() => { setQuery(''); setSearch(''); setPage(1); }}>Xóa tìm kiếm</button>
        </form>
        {loading ? <div className="cafe-empty" role="status">Đang tải khách hàng…</div> : error ? <div className="cafe-empty"><p className="cafe-error" role="alert">{error.message}</p><button className="cafe-button" onClick={reload}>Thử lại</button></div> : data && <>
          <div className="cafe-stats"><StatCard label="Khách hàng phù hợp" value={data.totalCount} icon={Users}/></div>
          <div className="cafe-table-wrap"><table className="cafe-table admin-table"><thead><tr><th>Khách hàng</th><th>Điện thoại</th><th>Email</th><th>Trạng thái</th><th>Ngày tạo</th></tr></thead><tbody>{data.items.map((row) => <tr key={row.customerId}>
            <td><button className="admin-link" onClick={() => setSelected(row.customerId)}>{row.fullName}</button><small>#KH{row.customerId}</small></td><td>{row.phone}</td><td>{row.email || '—'}</td><td>{customerStatus(row.status)}</td><td>{customerDate(row.createdAtUtc)}</td>
          </tr>)}</tbody></table></div>
          {!data.items.length && <div className="cafe-empty"><h3>Chưa có khách hàng phù hợp</h3><p>Thử tìm kiếm khác hoặc thêm hồ sơ khách hàng.</p></div>}
          <CustomerPagination data={data} onPage={setPage}/>
        </>}
      </Panel>
    </>}
    {creating && canCreate && <FormDialog title="Thêm khách hàng" description="Tài khoản mới có số dư ví bằng 0. Đặt mật khẩu ban đầu và giao trực tiếp cho khách."
      fields={[{ name: 'fullName', label: 'Họ và tên', maxLength: 150 }, { name: 'phone', label: 'Số điện thoại', type: 'tel', maxLength: 20 }, { name: 'email', label: 'Email', type: 'email', maxLength: 150, required: false }, { name: 'password', label: 'Mật khẩu ban đầu', type: 'password', maxLength: 128, help: '12–128 ký tự.' }]}
      initial={{ fullName: '', phone: '', email: '', password: '' }} onClose={() => setCreating(false)} submit="Tạo khách hàng"
      onSave={async (form) => { const saved = await createCustomer(form); setNotice(`Đã tạo khách hàng ${saved.fullName}.`); setQuery(''); setSearch(''); setPage(1); reload(); }}/>}
    {selected && <CustomerDetails key={selected} customerId={selected} canViewWallet={canViewWallet} onClose={() => setSelected(null)}/>}
  </section>;
}
