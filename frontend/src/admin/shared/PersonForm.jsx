import React from 'react';
import { useAdmin } from './AdminContext';
import { FormDialog } from './AdminUI';
import { hashInitialPassword, saveAccount } from './adminModel';

export default function PersonForm({ account, kind, onClose, onSaved }) {
  const { db, commit } = useAdmin();
  const profile = db.staff_profiles?.find((row) => row.account_id === account?.id);
  const fields = [{ name: 'display_name', label: 'Họ và tên', maxLength: 100 }, { name: 'phone', label: 'Số điện thoại', type: 'tel', maxLength: 11 }];
  if (kind === 'staff') fields.push({ name: 'role', label: 'Phân quyền', options: [['Staff', 'Nhân viên'], ['Admin', 'Quản trị viên']] }, { name: 'position', label: 'Chức danh' }, { name: 'shift', label: 'Ca làm việc', help: 'Ví dụ: 08:00–16:00 hoặc Linh hoạt.' });
  if (!account && kind === 'staff') fields.push({ name: 'password', label: 'Mật khẩu ban đầu', type: 'password', help: '8–128 ký tự. Nhân viên phải đổi mật khẩu lần đầu.' });
  return <FormDialog title={`${account ? 'Chỉnh sửa' : 'Thêm'} ${kind === 'staff' ? 'nhân viên' : 'khách hàng'}`} fields={fields} initial={account ? { ...account, ...profile } : { display_name: '', phone: '', role: 'Staff', position: 'Thu ngân', shift: '08:00–16:00' }} description={!account && kind === 'customer' ? 'Mật khẩu ban đầu là số điện thoại; khách cần đổi mật khẩu lần đầu. Số dư ban đầu bằng 0, nạp tiền tại quầy trước khi sử dụng.' : undefined} onClose={onClose} onSave={async (form) => { const hash = account ? null : await hashInitialPassword(kind === 'customer' ? form.phone : form.password); commit((data) => saveAccount(data, form, kind, hash)); onSaved?.(); }}/>;
}
