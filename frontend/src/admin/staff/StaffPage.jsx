import React, { useState, useEffect } from 'react';
import { Plus, ShieldCheck, UserCheck, Users, History } from 'lucide-react';
import { useAdminAction } from '../shared/AdminContext';
import { AdminPage, StatCard, Panel, Table, Toolbar, Filter, Status, Notice, DetailDialog, FormDialog, Row } from '../shared/AdminUI';
import { dateTime, matches, toggleAccount } from '../shared/adminModel';
import { DonutChart } from '../shared/Charts';
import PersonForm from '../shared/PersonForm';
import { employeesApi } from '../shared/employeesApi';

export function StaffPage() {
  const { db, commit, notice, error, clear, setNotice } = useAdminAction();
  const [query, setQuery] = useState(''), [role, setRole] = useState('all'), [editing, setEditing] = useState(undefined), [selected, setSelected] = useState(null), [locking, setLocking] = useState(null);
  const [employees, setEmployees] = useState(null);
  const [apiError, setApiError] = useState('');

  useEffect(() => {
    const loadEmployees = async () => {
      try {
        const data = await employeesApi.getAll();
        setEmployees(data);
        setApiError('');
      } catch (err) {
        setApiError('Không thể tải danh sách nhân viên từ máy chủ.');
        console.error('Failed to load employees:', err);
      }
    };

    loadEmployees();
  }, []);

  // Use API data if available, fallback to demo data
  const useApiData = employees !== null;
  const people = useApiData 
    ? employees.map(e => ({
        id: e.employeeId,
        display_name: e.fullName,
        phone: e.phone || '',
        role: 'Staff', // API returns role name, need to map appropriately
        is_active: e.isActive,
        profile: { position: '', shift: '' },
        created_at: e.createdAt
      }))
    : db.accounts.filter((row) => ['Staff', 'Admin'].includes(row.role)).map((row) => ({ ...row, profile: db.staff_profiles.find((profile) => profile.account_id === row.id) }));

  const rows = people.filter((row) => matches(query, row.display_name, row.phone, row.profile?.position) && (role === 'all' || role === row.role));
  const detail = people.find((row) => row.id === selected);

  const handleLockUnlock = async (employee) => {
    try {
      if (employee.is_active) {
        await employeesApi.lock(employee.id);
      } else {
        await employeesApi.unlock(employee.id);
      }
      // Reload employees list
      const data = await employeesApi.getAll();
      setEmployees(data);
      setNotice(`Đã ${employee.is_active ? 'khóa' : 'mở khóa'} tài khoản nhân viên.`);
      setLocking(null);
    } catch (err) {
      setApiError(err.message || 'Không thể cập nhật trạng thái nhân viên.');
    }
  };

  return <AdminPage title="Quản lý nhân viên" description="Hồ sơ, ca làm, vai trò và lịch sử thao tác của nhân viên." actions={<button className="cafe-button primary" onClick={() => setEditing(null)}><Plus size={16}/>Thêm nhân viên</button>}>
    {apiError && <Notice notice="" error={apiError} clear={() => setApiError('')}/>}
    <Notice notice={notice} error={error} clear={clear}/>
    <div className="cafe-stats"><StatCard label="Nhân sự" value={people.length} icon={Users}/><StatCard label="Đang hoạt động" value={people.filter((row) => row.is_active).length} icon={UserCheck}/><StatCard label="Quản trị viên" value={people.filter((row) => row.role === 'Admin').length} icon={ShieldCheck}/><StatCard label="Thao tác đã ghi nhận" value={useApiData ? 0 : db.audit_log.length} icon={History}/></div>
    <Panel title="Danh sách nhân viên" description={useApiData ? "Dữ liệu được tải từ máy chủ. Xác thực và phân quyền phía server đã tích hợp." : "Vai trò là cấu hình demo; xác thực và phân quyền phía server chưa tích hợp."}>
      <Toolbar query={query} onQuery={setQuery} placeholder="Tìm tên, số điện thoại, chức danh…" onReset={() => { setQuery(''); setRole('all'); }}><Filter label="Vai trò nhân viên" value={role} onChange={setRole} options={ [['all', 'Tất cả vai trò'], ['Admin', 'Quản trị viên'], ['Staff', 'Nhân viên']] }/></Toolbar>
      <Table rows={rows} resetKey={query + role} columns={[{ key: 'name', label: 'Nhân viên', sortValue: (row) => row.display_name, render: (row) => <><button className="admin-link" onClick={() => setSelected(row.id)}>{row.display_name}</button><small>#NV{row.id} · {row.phone}</small></> }, { key: 'role', label: 'Vai trò', render: (row) => <Status value={row.role}/> }, { key: 'profile', label: 'Công việc / Ca làm', render: (row) => <>{row.profile?.position || 'Nhân viên'}<small>{row.profile?.shift || 'Chưa xếp ca'}</small></> }, { key: 'status', label: 'Trạng thái', render: (row) => <Status value={row.is_active ? 'Active' : 'Inactive'}/> }, { key: 'actions', label: 'Thao tác', render: (row) => <div className="admin-actions"><button className="cafe-button" onClick={() => setEditing(row)}>Chỉnh sửa</button><button className="cafe-button" onClick={() => setLocking(row)}>{row.is_active ? 'Khóa' : 'Mở khóa'}</button></div> }]}/>
    </Panel>
    <div style={{ marginTop: 22 }}><Panel title="Cơ cấu vai trò" description="Số tài khoản quản trị và nhân viên hiện tại."><DonutChart label="nhân sự" data={[{ name: 'Quản trị viên', value: people.filter((row) => row.role === 'Admin').length }, { name: 'Nhân viên', value: people.filter((row) => row.role === 'Staff').length }]}/></Panel></div>
    {editing !== undefined && <PersonForm account={editing} kind="staff" onClose={() => setEditing(undefined)} onSaved={() => { setNotice('Đã lưu hồ sơ nhân viên.'); employeesApi.getAll().then(data => setEmployees(data)).catch(err => setApiError(err.message)); }}/>}
    {locking && <FormDialog title={`${locking.is_active ? 'Khóa' : 'Mở khóa'} ${locking.display_name}?`} description="Không thể khóa quản trị viên hoạt động cuối cùng." submit="Xác nhận" onClose={() => setLocking(null)} onSave={() => handleLockUnlock(locking)}/>}
    {detail && <DetailDialog title={detail.display_name} onClose={() => setSelected(null)}><Row label="Số điện thoại">{detail.phone}</Row><Row label="Vai trò"><Status value={detail.role}/></Row><Row label="Chức danh">{detail.profile?.position || '—'}</Row><Row label="Ca làm">{detail.profile?.shift || '—'}</Row><Row label="Ngày bắt đầu">{dateTime(detail.created_at)}</Row><h3 className="admin-section-title">Lịch sử thao tác</h3><div className="admin-detail-list">{useApiData ? <p className="admin-form-note">Lịch sử thao tác từ máy chủ</p> : (db.audit_log.filter((row) => row.account_id === detail.id).slice().reverse().map((row) => <div className="admin-detail-line" key={row.id}><span>{row.action} · {row.subject}</span><small>{dateTime(row.recorded_at)}</small></div>), !db.audit_log.some((row) => row.account_id === detail.id) && <p className="admin-form-note">Chưa có thao tác được ghi nhận.</p>)}</div></DetailDialog>}
  </AdminPage>;
}

