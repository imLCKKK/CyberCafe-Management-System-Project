import React, { useRef } from 'react';
import { FormDialog } from '../shared/AdminUI';
import { employeesApi, newEmployeeCreateKey } from '../shared/employeesApi';

export default function ChangePasswordDialog({ onClose, onSaved }) {
  const passwordIntent = useRef(null);

  return <FormDialog
    title="Đổi mật khẩu của tôi"
    description="Mật khẩu mới có 12–72 ký tự, ít nhất một chữ và một số, khác mật khẩu hiện tại."
    submit="Đổi mật khẩu"
    fields={[
      { name: 'currentPassword', label: 'Mật khẩu hiện tại', type: 'password', maxLength: 256 },
      { name: 'newPassword', label: 'Mật khẩu mới', type: 'password', maxLength: 72 },
      { name: 'confirmNewPassword', label: 'Xác nhận mật khẩu mới', type: 'password', maxLength: 72 },
    ]}
    initial={{ currentPassword: '', newPassword: '', confirmNewPassword: '' }}
    onClose={onClose}
    onSave={async (form) => {
      if (form.newPassword !== form.confirmNewPassword) throw new Error('Xác nhận mật khẩu mới không khớp.');
      const fingerprint = JSON.stringify(form);
      if (passwordIntent.current?.fingerprint !== fingerprint)
        passwordIntent.current = { fingerprint, key: newEmployeeCreateKey() };
      await employeesApi.changePassword(form, passwordIntent.current.key);
      passwordIntent.current = null;
      onSaved();
    }}
  />;
}
