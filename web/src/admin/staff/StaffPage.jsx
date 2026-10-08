import React, { useState } from 'react';
import { Users, Plus, Edit, Trash2, X } from 'lucide-react';
import { mockStaffData } from './mockData';

export default function StaffPage() {
    const [staffList, setStaffList] = useState(mockStaffData);
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [formData, setFormData] = useState({ StaffId: null, FullName: '', Role: 'Thu ngân', Phone: '', Email: '', Status: 'Active' });

    // 1. Xóa nhân viên
    const handleDelete = (id) => {
        if (window.confirm("Bạn có chắc chắn muốn xóa nhân viên này?")) {
            setStaffList(staffList.filter(staff => staff.StaffId !== id));
        }
    };

    // 2. Đổi trạng thái nhanh (Click thẳng vào nút Hoạt động/Nghỉ việc)
    const handleToggleStatus = (id) => {
        setStaffList(staffList.map(staff =>
            staff.StaffId === id
                ? { ...staff, Status: staff.Status === 'Active' ? 'Inactive' : 'Active' }
                : staff
        ));
    };

    // 3. Mở Modal Thêm hoặc Sửa
    const openModal = (staff = null) => {
        if (staff) {
            setFormData(staff); // Nếu truyền staff vào -> Sửa
        } else {
            setFormData({ StaffId: null, FullName: '', Role: 'Thu ngân', Phone: '', Email: '', Status: 'Active' }); // Form rỗng -> Thêm mới
        }
        setIsModalOpen(true);
    };

    // 4. Lưu thông tin (Thêm mới hoặc Cập nhật)
    const handleSave = (e) => {
        e.preventDefault();
        if (formData.StaffId) {
            // Đang Sửa
            setStaffList(staffList.map(staff => staff.StaffId === formData.StaffId ? formData : staff));
        } else {
            // Đang Thêm mới (tự tăng ID)
            const newId = staffList.length > 0 ? Math.max(...staffList.map(s => s.StaffId)) + 1 : 1;
            setStaffList([...staffList, { ...formData, StaffId: newId }]);
        }
        setIsModalOpen(false); // Đóng modal
    };

    return (
        <div className="admin-page-container">
            <div className="admin-page-header">
                <div className="header-title">
                    <Users size={24} className="header-icon" />
                    <h2>Quản lý nhân viên</h2>
                </div>
                <button className="admin-btn-primary" onClick={() => openModal()}>
                    <Plus size={18} /> Thêm nhân viên
                </button>
            </div>

            <div className="admin-table-container">
                <table className="admin-table">
                    <thead>
                        <tr>
                            <th>ID</th>
                            <th>Họ và tên</th>
                            <th>Vai trò</th>
                            <th>Số điện thoại</th>
                            <th>Trạng thái</th>
                            <th>Thao tác</th>
                        </tr>
                    </thead>
                    <tbody>
                        {staffList.map((staff) => (
                            <tr key={staff.StaffId}>
                                <td>#{staff.StaffId}</td>
                                <td>{staff.FullName}</td>
                                <td>{staff.Role}</td>
                                <td>{staff.Phone}</td>
                                <td>
                                    {/* Click vào badge để đổi trạng thái luôn cho tiện */}
                                    <span
                                        className={`status-badge clickable ${staff.Status === 'Active' ? 'active' : 'inactive'}`}
                                        onClick={() => handleToggleStatus(staff.StaffId)}
                                        title="Click để đổi trạng thái"
                                    >
                                        {staff.Status === 'Active' ? 'Hoạt động' : 'Nghỉ việc'}
                                    </span>
                                </td>
                                <td>
                                    <div className="action-buttons">
                                        <button className="btn-icon edit" onClick={() => openModal(staff)} title="Sửa"><Edit size={16} /></button>
                                        <button className="btn-icon delete" onClick={() => handleDelete(staff.StaffId)} title="Xóa"><Trash2 size={16} /></button>
                                    </div>
                                </td>
                            </tr>
                        ))}
                        {staffList.length === 0 && (
                            <tr><td colSpan="6" style={{ textAlign: 'center', color: '#94a3b8' }}>Không có dữ liệu</td></tr>
                        )}
                    </tbody>
                </table>
            </div>

            {/* --- MODAL THÊM / SỬA NHÂN VIÊN --- */}
            {isModalOpen && (
                <div className="admin-modal-overlay">
                    <div className="admin-modal">
                        <div className="admin-modal-header">
                            <h3>{formData.StaffId ? 'Cập nhật nhân viên' : 'Thêm nhân viên mới'}</h3>
                            <button className="btn-icon" onClick={() => setIsModalOpen(false)}><X size={20} /></button>
                        </div>

                        <form onSubmit={handleSave} className="admin-modal-form">
                            <div className="admin-form-group">
                                <label>Họ và tên</label>
                                <input type="text" className="admin-input-field" required value={formData.FullName} onChange={(e) => setFormData({ ...formData, FullName: e.target.value })} placeholder="Nhập họ và tên..." />
                            </div>

                            <div className="admin-form-group">
                                <label>Vai trò</label>
                                <select className="admin-input-field" value={formData.Role} onChange={(e) => setFormData({ ...formData, Role: e.target.value })}>
                                    <option value="Quản trị viên">Quản trị viên</option>
                                    <option value="Kỹ thuật viên">Kỹ thuật viên</option>
                                    <option value="Thu ngân">Thu ngân</option>
                                </select>
                            </div>

                            <div className="admin-form-group">
                                <label>Số điện thoại</label>
                                <input type="text" className="admin-input-field" required value={formData.Phone} onChange={(e) => setFormData({ ...formData, Phone: e.target.value })} placeholder="Ví dụ: 0912..." />
                            </div>

                            <div className="admin-modal-footer">
                                <button type="button" className="admin-btn-secondary" onClick={() => setIsModalOpen(false)}>Hủy</button>
                                <button type="submit" className="admin-btn-primary">Lưu thông tin</button>
                            </div>
                        </form>
                    </div>
                </div>
            )}
        </div>
    );
}