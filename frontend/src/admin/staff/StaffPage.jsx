import React, { useState, useEffect } from 'react';
import { Plus, ShieldCheck, UserCheck, Users, History } from 'lucide-react';
import { useAdminAction } from '../shared/AdminContext';
import { AdminPage, StatCard, Panel, Table, Toolbar, Filter, Status, Notice, DetailDialog, FormDialog, Row } from '../shared/AdminUI';
import { dateTime, matches } from '../shared/adminModel';
import { DonutChart } from '../shared/Charts';
import PersonForm from '../shared/PersonForm';
import { employeesApi } from '../shared/employeesApi';
import { useAuth } from '../../shared/auth/AuthContext';

export function StaffPage() {
  const { notice, error, clear, setNotice } = useAdminAction();
  const { principal } = useAuth();
  const canManage = principal?.type === 'employee' && principal.permissions.includes('employee.manage');
  const [query, setQuery] = useState(''), [role, setRole] = useState('all'), [editing, setEditing] = useState(undefined), [selected, setSelected] = useState(null), [locking, setLocking] = useState(null);
  const [employees, setEmployees] = useState(null);
  const [apiError, setApiError] = useState('');
  const [loading, setLoading] = useState(true);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    const loadEmployees = async () => {
      try {
        const data = await employeesApi.getAll({ signal: controller.signal });
        if (controller.signal.aborted) return;
        setEmployees(data);
        setApiError('');
      } catch (err) {
        if (controller.signal.aborted) return;
        setApiError(err.message || 'Không thể tải danh sách nhân viên từ máy chủ.');
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    };

    loadEmployees();
    return () => controller.abort();
  }, [reloadKey]);

  const reloadEmployees = () => {
    setEmployees(null);
    setLoading(true);
    setApiError('');
    setEditing(undefined);
    setSelected(null);
    setLocking(null);
    setReloadKey((key) => key + 1);
  };

  const hasServerData = !loading && employees !== null;
  const people = (employees ?? []).map(e => ({
        id: e.employeeId,
        display_name: e.fullName,
        phone: e.phone || '',
        role: e.roleName,
        version: e.version,
        is_active: e.isActive,
        created_at: e.createdAt
      }));

  const rows = people.filter((row) => matches(query, row.display_name, row.phone) && (role === 'all' || role === row.role));
  const detail = people.find((row) => row.id === selected);
  const roleOptions = [...new Set(people.map((row) => row.role))].map((name) => [name, { Admin: 'Quản trị viên', Staff: 'Nhân viên' }[name] || name]);

  const handleLockUnlock = async (employee) => {
    if (!hasServerData || !canManage) throw new Error('Bạn chưa có dữ liệu hoặc quyền quản lý nhân viên.');
    if (employee.is_active) {
      await employeesApi.lock(employee.id);
    } else {
      await employeesApi.unlock(employee.id);
    }
    setNotice(`Đã ${employee.is_active ? 'khóa' : 'mở khóa'} tài khoản nhân viên.`);
    reloadEmployees();
  };

  return <AdminPage live title="Quản lý nhân viên" description="Hồ sơ, vai trò và lịch sử thao tác của nhân viên." actions={canManage ? <button className="cafe-button primary" disabled={!hasServerData} onClick={() => setEditing(null)}><Plus size={16}/>Thêm nhân viên</button> : undefined}>
    <Notice notice={notice} error={error} clear={clear}/>
    {hasServerData && <div className="cafe-stats"><StatCard label="Nhân sự" value={people.length} icon={Users}/><StatCard label="Đang hoạt động" value={people.filter((row) => row.is_active).length} icon={UserCheck}/><StatCard label="Quản trị viên" value={people.filter((row) => row.role === 'Admin').length} icon={ShieldCheck}/><StatCard label="Thao tác đã ghi nhận" value="—" icon={History}/></div>}
    <Panel title="Danh sách nhân viên" description="Dữ liệu nhân viên được tải từ máy chủ.">
      {loading && <p role="status">Đang tải danh sách nhân viên…</p>}
      {!loading && !hasServerData && <><Notice error={apiError}/><button className="cafe-button" onClick={reloadEmployees}>Thử lại</button></>}
      {hasServerData && <>
      <Toolbar query={query} onQuery={setQuery} placeholder="Tìm tên, số điện thoại…" onReset={() => { setQuery(''); setRole('all'); }}><Filter label="Vai trò nhân viên" value={role} onChange={setRole} options={ [['all', 'Tất cả vai trò'], ...roleOptions] }/></Toolbar>
      <Table rows={rows} empty={people.length ? 'Không có dữ liệu phù hợp.' : 'Chưa có nhân viên trên máy chủ.'} resetKey={query + role} columns={[{ key: 'name', label: 'Nhân viên', sortValue: (row) => row.display_name, render: (row) => <><button className="admin-link" onClick={() => setSelected(row.id)}>{row.display_name}</button><small>#NV{row.id} · {row.phone}</small></> }, { key: 'role', label: 'Vai trò', render: (row) => <Status value={row.role}/> }, { key: 'status', label: 'Trạng thái', render: (row) => <Status value={row.is_active ? 'Active' : 'Inactive'}/> }, ...(canManage ? [{ key: 'actions', label: 'Thao tác', render: (row) => <div className="admin-actions"><button className="cafe-button" onClick={() => setEditing(row)}>Chỉnh sửa</button><button className="cafe-button" onClick={() => setLocking(row)}>{row.is_active ? 'Khóa' : 'Mở khóa'}</button></div> }] : [])]}/>
      </>}
    </Panel>
    {hasServerData && <div style={{ marginTop: 22 }}><Panel title="Cơ cấu vai trò" description="Số tài khoản theo từng vai trò hiện tại."><DonutChart label="nhân sự" data={roleOptions.map(([value, name]) => ({ name, value: people.filter((row) => row.role === value).length }))}/></Panel></div>}
    {hasServerData && canManage && editing !== undefined && <PersonForm account={editing} kind="staff" onClose={() => setEditing(undefined)} onReload={reloadEmployees} onSaved={() => { setNotice('Đã lưu hồ sơ nhân viên.'); reloadEmployees(); }}/>}
    {hasServerData && canManage && locking && <FormDialog title={`${locking.is_active ? 'Khóa' : 'Mở khóa'} ${locking.display_name}?`} description="Không thể khóa quản trị viên hoạt động cuối cùng." submit="Xác nhận" onClose={() => setLocking(null)} onSave={() => handleLockUnlock(locking)}/>}
    {hasServerData && detail && <DetailDialog title={detail.display_name} onClose={() => setSelected(null)}><Row label="Số điện thoại">{detail.phone}</Row><Row label="Vai trò"><Status value={detail.role}/></Row><Row label="Ngày bắt đầu">{dateTime(detail.created_at)}</Row><h3 className="admin-section-title">Lịch sử thao tác</h3><p className="admin-form-note">Chưa tải lịch sử thao tác từ máy chủ.</p></DetailDialog>}
  </AdminPage>;
}

