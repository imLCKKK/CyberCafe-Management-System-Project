import React, { useEffect, useState } from 'react';
import { DetailDialog, FormDialog } from './AdminUI';
import { employeesApi } from './employeesApi';

export default function PersonForm({ account, kind, onClose, onSaved, onReload }) {
  const [roles, setRoles] = useState(null);
  const [rolesError, setRolesError] = useState('');
  const [conflict, setConflict] = useState(false);

  useEffect(() => {
    let active = true;
    employeesApi.getRoles().then((data) => {
      if (!data.length) throw new Error('Máy chủ chưa có vai trò để chọn.');
      if (active) setRoles(data);
    }).catch((error) => {
      if (active) setRolesError(error.message || 'Không thể tải danh sách vai trò.');
    });
    return () => { active = false; };
  }, []);

  if (!roles) {
    return <DetailDialog title="Phân quyền nhân viên" onClose={onClose}>
      {rolesError ? <p role="alert">{rolesError}</p> : <p>Đang tải danh sách vai trò…</p>}
    </DetailDialog>;
  }

  const currentRole = roles.find((role) => role.roleName === account?.role);
  const fields = [
    { name: 'display_name', label: 'Họ và tên', maxLength: 100 },
    { name: 'phone', label: 'Số điện thoại', type: 'tel', maxLength: 11 }
  ];

  if (kind === 'staff') {
    fields.push(
      { name: 'role', label: 'Phân quyền', options: [['', 'Chọn vai trò'], ...roles.map((role) => [String(role.roleId), role.roleName])] },
      { name: 'position', label: 'Chức danh' },
      { name: 'shift', label: 'Ca làm việc', help: 'Ví dụ: 08:00–16:00 hoặc Linh hoạt.' }
    );
  }

  if (!account && kind === 'staff') {
    fields.push({ name: 'password', label: 'Mật khẩu ban đầu', type: 'password', maxLength: 72, help: '12–72 ký tự, ít nhất một chữ và một số.' });
  }

  const handleSave = async (form) => {
    try {
      const selectedRole = roles.find((role) => String(role.roleId) === form.role);
      if (!selectedRole) throw new Error('Vui lòng chọn vai trò hợp lệ từ máy chủ.');
      if (account) {
        const updateData = {
          fullName: form.display_name,
          phone: form.phone,
          expectedVersion: account.version,
          ...(selectedRole.roleName !== account.role ? { roleId: selectedRole.roleId } : {})
        };
        await employeesApi.update(account.id, updateData);
      } else {
        const createData = {
          username: form.phone,
          fullName: form.display_name,
          phone: form.phone,
          password: form.password,
          roleId: selectedRole.roleId
        };
        await employeesApi.create(createData);
      }
      onSaved?.();
    } catch (error) {
      if (error.status === 409) setConflict(true);
      throw new Error(error.message || (account ? 'Không thể cập nhật nhân viên.' : 'Không thể tạo nhân viên.'));
    }
  };

  return (
    <FormDialog
      title={`${account ? 'Chỉnh sửa' : 'Thêm'} ${kind === 'staff' ? 'nhân viên' : 'khách hàng'}`}
      fields={fields}
      initial={
        account
          ? { ...account, role: currentRole ? String(currentRole.roleId) : '', display_name: account.display_name }
          : { display_name: '', phone: '', role: '', position: 'Thu ngân', shift: '08:00–16:00' }
      }
      description={
        conflict
          ? <>Hồ sơ đã được cập nhật bởi thao tác khác. Tải lại danh sách trước khi chỉnh sửa tiếp. <button type="button" className="cafe-button" onClick={onReload}>Tải lại danh sách</button></>
          : !account && kind === 'customer'
          ? 'Mật khẩu ban đầu là số điện thoại; khách cần đổi mật khẩu lần đầu. Số dư ban đầu bằng 0, nạp tiền tại quầy trước khi sử dụng.'
          : undefined
      }
      onClose={onClose}
      onSave={handleSave}
    />
  );
}
