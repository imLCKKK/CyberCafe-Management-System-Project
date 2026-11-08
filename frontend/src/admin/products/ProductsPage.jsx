import React, { useState } from 'react';
import { Package, Plus, Tag, Utensils, RefreshCw } from 'lucide-react';
import { useAuth } from '../../shared/auth/AuthContext';
import { useCatalog } from '../../shared/catalog/useCatalog';
import { disableCatalogItem, saveCategory, saveProduct } from '../../shared/catalog/catalogApi';
import { AdminPage, StatCard, Panel, Table, Toolbar, Filter, Status, Notice, FormDialog, DetailDialog, ExportButton } from '../shared/AdminUI';
import { matches, money } from '../shared/adminModel';

export function ProductsPage() {
  const { principal } = useAuth();
  const { db, loading, error, reload } = useCatalog(true);
  const canManage = principal?.permissions.includes('product.manage');
  const [query, setQuery] = useState(''), [category, setCategory] = useState('all'), [status, setStatus] = useState('all');
  const [editing, setEditing] = useState(undefined), [categoriesOpen, setCategoriesOpen] = useState(false), [categoryEdit, setCategoryEdit] = useState(undefined);
  const [notice, setNotice] = useState(''), [actionError, setActionError] = useState('');
  const rows = db.products.filter(row => matches(query, row.name, row.id)
    && (category === 'all' || row.category_id === Number(category))
    && (status === 'all' || row.is_active === (status === 'active')));
  const fields = [
    { name: 'name', label: 'Tên sản phẩm', maxLength: 150 },
    { name: 'category_id', label: 'Danh mục', options: db.product_categories.filter(c => c.is_active || c.id === editing?.category_id).map(c => [c.id, c.name + (c.is_active ? '' : ' (ngừng dùng — hãy chọn danh mục khác)')]) },
    { name: 'current_price', label: 'Giá bán (VNĐ)', type: 'number', min: 0, max: 999999999, step: 1 },
    { name: 'is_active', label: 'Đang kinh doanh', type: 'checkbox' },
  ];

  async function save(form, categoryForm = false) {
    await (categoryForm ? saveCategory(form) : saveProduct(form));
    setNotice(categoryForm ? 'Đã lưu danh mục.' : 'Đã lưu sản phẩm.');
    setActionError('');
    reload();
  }
  async function disable(kind, id) {
    try { await disableCatalogItem(kind, id); setNotice('Đã ngừng sử dụng.'); setActionError(''); reload(); }
    catch (reason) { setActionError(reason.message); }
  }

  return <AdminPage live title="Quản lý sản phẩm" description="Cập nhật thực đơn và giá bán; số lượng được quản lý tại Kho." actions={<div className="admin-actions">
    <button className="cafe-button" disabled={loading} onClick={reload}><RefreshCw size={15}/>Tải lại</button>
    <button className="cafe-button" disabled={loading || !!error} onClick={() => setCategoriesOpen(true)}><Tag size={15}/>Danh mục</button>
    {canManage && <button className="cafe-button primary" disabled={loading || !!error || !db.product_categories.some(c => c.is_active)} onClick={() => setEditing(null)}><Plus size={16}/>Thêm sản phẩm</button>}
  </div>}>
    <Notice notice={notice} error={actionError} clear={() => { setNotice(''); setActionError(''); }}/>
    {loading ? <p role="status">Đang tải sản phẩm…</p> : error ? <div className="cafe-error" role="alert">{error}</div> : <>
      <div className="cafe-stats"><StatCard label="Tổng sản phẩm" value={db.products.length} icon={Package}/><StatCard label="Đang kinh doanh" value={db.products.filter(p => p.is_active).length} icon={Utensils}/><StatCard label="Danh mục" value={db.product_categories.length} icon={Tag}/></div>
      <Panel title="Danh sách sản phẩm" description="Đổi giá không làm thay đổi giá của đơn đã đặt." actions={<ExportButton filename="san-pham.csv" headers={['Mã', 'Tên', 'Danh mục', 'Giá bán', 'Tồn kho']} rows={rows.map(p => [p.id, p.name, db.product_categories.find(c => c.id === p.category_id)?.name, p.current_price, p.stock_quantity])} onError={setActionError}/>}>
        <Toolbar query={query} onQuery={setQuery} placeholder="Tìm tên hoặc mã sản phẩm…" onReset={() => { setQuery(''); setCategory('all'); setStatus('all'); }}>
          <Filter label="Danh mục sản phẩm" value={category} onChange={setCategory} options={ [['all', 'Tất cả danh mục'], ...db.product_categories.map(c => [c.id, c.name])] }/>
          <Filter label="Trạng thái sản phẩm" value={status} onChange={setStatus} options={ [['all', 'Tất cả trạng thái'], ['active', 'Đang bán'], ['inactive', 'Ngừng bán']] }/>
        </Toolbar>
        <Table rows={rows} resetKey={query + category + status} columns={[
          { key: 'name', label: 'Sản phẩm', sortValue: p => p.name, render: p => <><strong>{p.name}</strong><small>#SP{p.id} · {db.product_categories.find(c => c.id === p.category_id)?.name}</small></> },
          { key: 'price', label: 'Giá bán', sortValue: p => p.current_price, render: p => money(p.current_price) },
          { key: 'stock', label: 'Tồn khả dụng', sortValue: p => p.stock_quantity, render: p => `${p.stock_quantity} phần` },
          { key: 'status', label: 'Trạng thái', render: p => <Status value={p.is_active ? 'Active' : 'Inactive'}>{p.is_active ? 'Đang bán' : 'Ngừng bán'}</Status> },
          ...(canManage ? [{ key: 'action', label: '', render: p => <div className="admin-actions"><button className="cafe-button" onClick={() => setEditing(p)}>Chỉnh sửa</button>{p.is_active && <button className="cafe-button" onClick={() => disable('products', p.id)}>Ngừng bán</button>}</div> }] : []),
        ]}/>
      </Panel>
    </>}
    {canManage && editing !== undefined && <FormDialog title={editing ? 'Chỉnh sửa sản phẩm' : 'Thêm sản phẩm'} fields={fields} initial={editing || { name: '', category_id: db.product_categories.find(c => c.is_active)?.id, current_price: 20000, is_active: true }} description="Sản phẩm mới có tồn bằng 0. Ngừng bán sẽ ẩn món khỏi menu và gợi ý." onClose={() => setEditing(undefined)} onSave={form => save(form)}/>}
    {categoriesOpen && <DetailDialog title="Danh mục sản phẩm" onClose={() => setCategoriesOpen(false)}>
      {!db.product_categories.length && <p>Chưa có danh mục. Hãy thêm danh mục trước khi tạo món.</p>}
      {db.product_categories.map(c => <div className="admin-detail-line" key={c.id}><strong>{c.name}{!c.is_active && ' · Ngừng dùng'}</strong>{canManage && <div className="admin-actions"><button className="cafe-button" onClick={() => setCategoryEdit(c)}>Chỉnh sửa danh mục</button>{c.is_active && <button className="cafe-button" onClick={() => disable('categories', c.id)}>Ngừng dùng</button>}</div>}</div>)}
      {canManage && <div className="cafe-dialog-actions"><button className="cafe-button primary" onClick={() => setCategoryEdit(null)}>Thêm danh mục</button></div>}
    </DetailDialog>}
    {canManage && categoryEdit !== undefined && <FormDialog title="Lưu danh mục" fields={[{ name: 'name', label: 'Tên danh mục', maxLength: 150 }, { name: 'is_active', label: 'Đang sử dụng', type: 'checkbox' }]} initial={categoryEdit || { name: '', is_active: true }} description="Ngừng dùng danh mục sẽ ẩn toàn bộ món trong danh mục khỏi menu." onClose={() => setCategoryEdit(undefined)} onSave={form => save(form, true)}/>}
  </AdminPage>;
}
