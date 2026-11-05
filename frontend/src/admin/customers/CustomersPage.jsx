import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Plus, UserCheck, Users, WalletCards } from 'lucide-react';
import { PageHeading, StatCard } from '../computers/components/CafeUI';
import { DetailDialog, Filter, FormDialog, Notice, Panel, Row, Status, Table, Toolbar } from '../shared/AdminUI';
import { dateTime, matches, money } from '../shared/adminModel';
import {
  changeCustomerStatus, createCustomer, listCustomers, listWalletTransactions, newIdempotencyKey, staffTopUp, updateCustomer,
} from '../../services/customerAdminApi';
import { walletTypeLabel } from '../../services/walletTypes';

// Khách hàng đọc/ghi qua API + SQL Server (S1-M-1). Không dùng dữ liệu demo: API lỗi thì hiện lỗi và nút thử lại.
// Quyền do server kiểm tra (customer.*, wallet.*); thiếu quyền sẽ hiện thông báo 403 từ server.
const methods = [['Cash', 'Tiền mặt'], ['BankTransfer', 'Chuyển khoản đã đối soát']];

export function CustomersPage() {
  const [state, setState] = useState({ status: 'loading', customers: [], error: '' });
  const [query, setQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [notice, setNotice] = useState('');
  const [dialog, setDialog] = useState(null); // { kind: 'create' | 'edit' | 'lock' | 'topup', customer }
  const [selectedId, setSelectedId] = useState(null);
  const [historyVersion, setHistoryVersion] = useState(0);
  const [history, setHistory] = useState({ status: 'idle', rows: [], error: '' });
  // Khóa chống nạp lặp: cùng số tiền + cùng phương thức thì retry dùng lại khóa cũ; đổi nội dung thì sang khóa mới.
  const topUpAttempt = useRef(null);

  const load = useCallback(async () => {
    try {
      setState({ status: 'ready', customers: await listCustomers(), error: '' });
    } catch (err) {
      setState({ status: 'error', customers: [], error: err.message });
    }
  }, []);
  useEffect(() => { load(); }, [load]);

  useEffect(() => {
    if (selectedId == null) return undefined;
    let cancelled = false;
    setHistory({ status: 'loading', rows: [], error: '' });
    listWalletTransactions(selectedId)
      .then((rows) => { if (!cancelled) setHistory({ status: 'ready', rows, error: '' }); })
      .catch((err) => { if (!cancelled) setHistory({ status: 'error', rows: [], error: err.message }); });
    return () => { cancelled = true; };
  }, [selectedId, historyVersion]);

  const customers = state.customers;
  const filtered = customers.filter((row) =>
    matches(query, row.customerId, row.fullName, row.phone) && (statusFilter === 'all' || row.status === statusFilter));
  const selected = customers.find((row) => row.customerId === selectedId);
  const saved = async (message) => { await load(); setHistoryVersion((v) => v + 1); setNotice(message); };

  const header = <PageHeading eyebrow="NET CAFE / QUẢN TRỊ" title="Quản lý khách hàng" description="Hồ sơ và số dư ví của khách, lưu trong cơ sở dữ liệu.">
    <button className="cafe-button primary" onClick={() => setDialog({ kind: 'create' })}><Plus size={16}/>Thêm khách hàng</button>
  </PageHeading>;

  if (state.status === 'loading') {
    return <section className="cafe-page admin-module">{header}<p role="status">Đang tải danh sách khách hàng...</p></section>;
  }
  if (state.status === 'error') {
    return <section className="cafe-page admin-module">{header}
      <div className="admin-message error" role="alert">{state.error}</div>
      <button className="cafe-button primary" onClick={() => { setState({ status: 'loading', customers: [], error: '' }); load(); }}>Thử lại</button>
    </section>;
  }

  return <section className="cafe-page admin-module">
    {header}
    <Notice notice={notice} clear={() => setNotice('')}/>
    <div className="cafe-stats">
      <StatCard label="Tổng khách hàng" value={customers.length} icon={Users}/>
      <StatCard label="Đang hoạt động" value={customers.filter((row) => row.status === 'Active').length} icon={UserCheck}/>
      <StatCard label="Tổng số dư ví" value={money(customers.reduce((sum, row) => sum + row.walletBalance, 0))} icon={WalletCards}/>
    </div>
    <Panel title="Danh sách khách hàng" description="Chọn khách để xem ví và lịch sử giao dịch.">
      <Toolbar query={query} onQuery={setQuery} placeholder="Tìm tên, số điện thoại, mã khách…" onReset={() => { setQuery(''); setStatusFilter('all'); }}>
        <Filter label="Trạng thái khách" value={statusFilter} onChange={setStatusFilter}
          options={[['all', 'Tất cả trạng thái'], ['Active', 'Hoạt động'], ['Locked', 'Đã khóa']]}/>
      </Toolbar>
      <Table rows={filtered} idKey="customerId" resetKey={query + statusFilter} columns={[
        { key: 'name', label: 'Khách hàng', sortValue: (row) => row.fullName, render: (row) => <>
          <button className="admin-link" onClick={() => setSelectedId(row.customerId)}>{row.fullName}</button>
          <small>#KH{row.customerId} · {row.phone}</small></> },
        { key: 'balance', label: 'Số dư ví', sortValue: (row) => row.walletBalance, render: (row) => money(row.walletBalance) },
        { key: 'status', label: 'Trạng thái', render: (row) => <Status value={row.status === 'Active' ? 'Active' : 'Inactive'}/> },
        { key: 'created', label: 'Ngày tạo', render: (row) => dateTime(row.createdAt) },
        { key: 'actions', label: 'Thao tác', render: (row) => <div className="admin-row-actions">
          <button className="cafe-button" onClick={() => { topUpAttempt.current = null; setDialog({ kind: 'topup', customer: row }); }}>Nạp ví</button>
          <button className="cafe-button" onClick={() => setDialog({ kind: 'edit', customer: row })}>Sửa</button>
          <button className="cafe-button" onClick={() => setDialog({ kind: 'lock', customer: row })}>{row.status === 'Active' ? 'Khóa' : 'Mở khóa'}</button>
        </div> },
      ]}/>
    </Panel>

    {dialog?.kind === 'create' && <FormDialog title="Thêm khách hàng" submit="Tạo khách hàng" onClose={() => setDialog(null)}
      description="Khách đăng nhập bằng số điện thoại và mật khẩu ban đầu này."
      fields={[
        { name: 'fullName', label: 'Họ tên', maxLength: 150 },
        { name: 'phone', label: 'Số điện thoại', maxLength: 11, help: 'Bắt đầu bằng 0, gồm 10-11 chữ số.' },
        { name: 'email', label: 'Email (không bắt buộc)', type: 'email', required: false, maxLength: 150 },
        { name: 'initialPassword', label: 'Mật khẩu ban đầu', type: 'password', help: 'Từ 8 đến 128 ký tự.' },
      ]}
      initial={{ fullName: '', phone: '', email: '', initialPassword: '' }}
      onSave={async (form) => {
        await createCustomer({ fullName: form.fullName, phone: form.phone, email: form.email || null, initialPassword: form.initialPassword });
        await saved('Đã tạo khách hàng.');
      }}/>}

    {dialog?.kind === 'edit' && <FormDialog title={`Sửa hồ sơ · ${dialog.customer.fullName}`} onClose={() => setDialog(null)}
      fields={[
        { name: 'fullName', label: 'Họ tên', maxLength: 150 },
        { name: 'phone', label: 'Số điện thoại', maxLength: 11 },
        { name: 'email', label: 'Email (không bắt buộc)', type: 'email', required: false, maxLength: 150 },
      ]}
      initial={{ fullName: dialog.customer.fullName, phone: dialog.customer.phone, email: dialog.customer.email || '' }}
      onSave={async (form) => {
        await updateCustomer(dialog.customer.customerId, { fullName: form.fullName, phone: form.phone, email: form.email || null });
        await saved('Đã lưu thông tin khách hàng.');
      }}/>}

    {dialog?.kind === 'lock' && <FormDialog title={`${dialog.customer.status === 'Active' ? 'Khóa' : 'Mở khóa'} ${dialog.customer.fullName}?`}
      description="Không thể khóa khách đang có phiên chơi. Lý do được lưu vào lịch sử trạng thái." submit="Xác nhận" onClose={() => setDialog(null)}
      fields={[{ name: 'reason', label: 'Lý do', type: 'textarea', full: true, maxLength: 255 }]} initial={{ reason: '' }}
      onSave={async (form) => {
        await changeCustomerStatus(dialog.customer.customerId, dialog.customer.status === 'Active' ? 'Locked' : 'Active', form.reason);
        await saved('Đã cập nhật trạng thái khách hàng.');
      }}/>}

    {dialog?.kind === 'topup' && <FormDialog title={`Nạp ví · ${dialog.customer.fullName}`} submit="Xác nhận đã thu tiền và nạp" onClose={() => setDialog(null)}
      description="Chỉ nạp sau khi đã nhận đủ tiền. Ví được cộng ngay khi xác nhận."
      fields={[
        { name: 'amount', label: 'Số tiền (VNĐ)', type: 'number', min: 10000, max: 2000000, step: 1000 },
        { name: 'method', label: 'Phương thức đã nhận', options: methods },
      ]}
      initial={{ amount: 100000, method: 'Cash' }}
      onSave={async (form) => {
        const signature = `${Number(form.amount)}|${form.method}`;
        if (topUpAttempt.current?.signature !== signature) topUpAttempt.current = { signature, key: newIdempotencyKey() };
        await staffTopUp(dialog.customer.customerId, Number(form.amount), form.method, topUpAttempt.current.key);
        topUpAttempt.current = null;
        await saved(`Đã nạp ${money(Number(form.amount))} vào ví ${dialog.customer.fullName}.`);
      }}/>}

    {selected && <DetailDialog title={selected.fullName} onClose={() => setSelectedId(null)}>
      <Row label="Số điện thoại">{selected.phone}</Row>
      <Row label="Email">{selected.email || '—'}</Row>
      <Row label="Trạng thái"><Status value={selected.status === 'Active' ? 'Active' : 'Inactive'}/></Row>
      <Row label="Số dư ví">{money(selected.walletBalance)}</Row>
      <Row label="Ngày tạo">{dateTime(selected.createdAt)}</Row>
      <h3 className="admin-section-title">Lịch sử ví</h3>
      {history.status === 'loading' && <p role="status">Đang tải lịch sử ví...</p>}
      {history.status === 'error' && <div className="admin-message error" role="alert">{history.error}</div>}
      {history.status === 'ready' && (history.rows.length
        ? <div className="cafe-table-wrap"><table className="cafe-table"><thead><tr><th>Thời gian</th><th>Loại</th><th>Số tiền</th><th>Số dư sau</th></tr></thead>
          <tbody>{history.rows.map((row) => <tr key={row.id}>
            <td>{dateTime(row.recorded_at)}</td><td>{walletTypeLabel(row.transaction_type)}</td>
            <td>{row.amount > 0 ? '+' : '−'}{money(Math.abs(row.amount))}</td><td>{money(row.balance_after)}</td></tr>)}</tbody></table></div>
        : <p>Chưa có giao dịch ví.</p>)}
    </DetailDialog>}
  </section>;
}
