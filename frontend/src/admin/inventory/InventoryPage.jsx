import React, { useEffect, useState } from 'react';
import { Boxes, Package, RefreshCw } from 'lucide-react';
import { AdminPage, StatCard, Panel, Table, Toolbar, Filter } from '../shared/AdminUI';
import { matches, money } from '../shared/adminModel';
import { useAuth } from '../../shared/auth/AuthContext';
import { getInventory } from './inventoryApi';

export function InventoryPage() {
  const { principal } = useAuth();
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [query, setQuery] = useState('');
  const [stockFilter, setStockFilter] = useState('all');
  const [reload, setReload] = useState(0);
  const canRead = principal?.type === 'employee' && principal.permissions.includes('stockreceipt.view');

  useEffect(() => {
    if (!canRead) { setItems([]); setLoading(false); return undefined; }
    const controller = new AbortController();
    setLoading(true);
    setError('');
    setItems([]);
    getInventory(controller.signal)
      .then(data => { if (!controller.signal.aborted) setItems(data); })
      .catch(reason => {
        if (!controller.signal.aborted) setError(reason.status === 403
          ? 'Bạn không có quyền xem tồn kho.'
          : 'Không tải được tồn kho. Vui lòng thử lại.');
      })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [canRead, principal, reload]);

  const filtered = items.filter(item => matches(query, item.name, item.id)
    && (stockFilter !== 'empty' || item.stockQuantity === 0)
    && (stockFilter !== 'inactive' || !item.isActive));

  return <AdminPage title="Quản lý kho" description="Xem số lượng tồn và trạng thái bán của sản phẩm."
    actions={canRead && <button className="cafe-button" disabled={loading} onClick={() => setReload(value => value + 1)}><RefreshCw size={16}/>Tải lại</button>}>
    {!canRead ? <div className="cafe-error" role="alert">Bạn không có quyền xem tồn kho.</div>
      : loading ? <p role="status">Đang tải tồn kho…</p>
      : error ? <div className="cafe-error" role="alert">{error}</div>
      : <>
        <div className="cafe-stats">
          <StatCard label="Mặt hàng trong kho" value={items.length} icon={Package}/>
          <StatCard label="Tổng số lượng" value={items.reduce((sum, item) => sum + item.stockQuantity, 0)} icon={Boxes}/>
          <StatCard label="Hết hàng" value={items.filter(item => item.stockQuantity === 0).length} icon={Package}/>
        </div>
        <Panel title="Danh sách tồn kho">
          <Toolbar query={query} onQuery={setQuery} placeholder="Tìm mã hoặc tên sản phẩm…" onReset={() => { setQuery(''); setStockFilter('all'); }}>
            <Filter label="Trạng thái" value={stockFilter} onChange={setStockFilter}
              options={[['all', 'Tất cả'], ['empty', 'Hết hàng'], ['inactive', 'Ngừng bán']]}/>
          </Toolbar>
          {items.length === 0 ? <p role="status">Chưa có sản phẩm trong kho.</p>
            : filtered.length === 0 ? <p role="status">Không có sản phẩm phù hợp bộ lọc.</p>
            : <Table rows={filtered} resetKey={query + stockFilter} columns={[
              { key: 'name', label: 'Sản phẩm', render: item => <><strong>{item.name}</strong><small>#SP{item.id}</small></> },
              { key: 'stockQuantity', label: 'Số lượng tồn', render: item => item.stockQuantity },
              { key: 'currentPrice', label: 'Giá bán', render: item => money(item.currentPrice) },
              { key: 'isActive', label: 'Trạng thái bán', render: item => item.isActive ? 'Đang bán' : 'Ngừng bán' },
            ]}/>}
        </Panel>
      </>}
  </AdminPage>;
}
