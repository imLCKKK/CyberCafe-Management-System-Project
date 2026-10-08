import React from 'react';
import { useNavigate } from 'react-router-dom';
import { Shield, User, Lock, LogIn } from 'lucide-react';

export default function AdminAuthPage() {
    const navigate = useNavigate();

    const handleSubmit = (e) => {
        e.preventDefault();
        alert("Giao diện tĩnh: Đăng nhập Admin thành công!");
        navigate('/admin/dashboard'); // Chuyển hướng vào trang Tổng quan
    };

    return (
        <div className="admin-auth-wrapper">
            <div className="admin-auth-card">
                <div className="admin-auth-header">
                    <div className="admin-auth-logo">
                        <Shield size={40} color="#1e293b" />
                    </div>
                    <h2>NET CAFE MANAGEMENT</h2>
                    <p>Hệ thống dành riêng cho nhân viên</p>
                </div>

                <form className="admin-auth-form" onSubmit={handleSubmit}>
                    <div className="admin-form-group">
                        <label>Tên đăng nhập</label>
                        <div className="admin-input-wrapper">
                            <User size={18} className="admin-input-icon-svg" />
                            <input type="text" placeholder="Nhập username..." required className="admin-input-field" />
                        </div>
                    </div>

                    <div className="admin-form-group">
                        <label>Mật khẩu</label>
                        <div className="admin-input-wrapper">
                            <Lock size={18} className="admin-input-icon-svg" />
                            <input type="password" placeholder="••••••••" required className="admin-input-field" />
                        </div>
                    </div>

                    <button type="submit" className="admin-btn-primary admin-auth-btn">
                        <LogIn size={18} />
                        Đăng nhập hệ thống
                    </button>
                </form>
            </div>
        </div>
    );
}