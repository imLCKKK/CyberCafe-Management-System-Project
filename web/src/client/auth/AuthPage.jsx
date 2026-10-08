import React, { useState } from 'react';
import { User, Lock, Mail, LogIn, UserPlus, AlertCircle, CheckCircle2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';

export default function AuthPage() {
    const [isLogin, setIsLogin] = useState(true);
    const [isLoading, setIsLoading] = useState(false);
    const [message, setMessage] = useState({ type: '', text: '' }); // type: 'error' | 'success'
    const navigate = useNavigate();

    // Quản lý state của các ô input
    const [formData, setFormData] = useState({
        fullName: '',
        username: '',
        email: '',
        password: ''
    });

    const handleChange = (e) => {
        setFormData({ ...formData, [e.target.name]: e.target.value });
        // Người dùng đang gõ lại thì tắt thông báo lỗi đi
        if (message.text) setMessage({ type: '', text: '' });
    };

    const toggleMode = () => {
        setIsLogin(!isLogin);
        setMessage({ type: '', text: '' });
        // Reset form khi chuyển đổi qua lại, nhưng giữ lại username nếu đang có
        setFormData({ ...formData, fullName: '', email: '', password: '' });
    };

    const handleSubmit = (e) => {
        e.preventDefault();
        setIsLoading(true);
        setMessage({ type: '', text: '' });

        // Giả lập thời gian chờ Call API (1 giây)
        setTimeout(() => {
            setIsLoading(false);

            if (isLogin) {
                // --- XỬ LÝ ĐĂNG NHẬP ---
                if (formData.password.length < 6) {
                    setMessage({ type: 'error', text: 'Mật khẩu hoặc tài khoản không chính xác!' });
                    return;
                }
                alert(`Đăng nhập thành công! Chào mừng ${formData.username} đến với TRAM.`);
                navigate('/client/home');
            } else {
                // --- XỬ LÝ ĐĂNG KÝ ---
                if (formData.password.length < 6) {
                    setMessage({ type: 'error', text: 'Mật khẩu phải có ít nhất 6 ký tự!' });
                    return;
                }
                setMessage({ type: 'success', text: 'Đăng ký thành công! Vui lòng đăng nhập để tiếp tục.' });
                setIsLogin(true); // Tự động đẩy qua tab Đăng nhập
                setFormData({ ...formData, password: '' }); // Xóa mật khẩu đi để bắt nhập lại
            }
        }, 1000);
    };

    return (
        <div className="client-auth-wrapper">
            <div className="client-auth-card">
                <div className="client-auth-header">
                    <div className="client-auth-logo">
                        <span className="logo-icon">⚡</span> TRAM
                    </div>
                    <h2>{isLogin ? 'Đăng nhập vào trạm' : 'Đăng ký hội viên'}</h2>
                    <p>
                        {isLogin ? 'Chưa có tài khoản? ' : 'Đã có tài khoản? '}
                        <span className="client-auth-toggle" onClick={toggleMode}>
                            {isLogin ? 'Đăng ký ngay' : 'Đăng nhập'}
                        </span>
                    </p>
                </div>

                {/* Khung hiển thị thông báo lỗi/thành công */}
                {message.text && (
                    <div className={`auth-message ${message.type}`}>
                        {message.type === 'error' ? <AlertCircle size={18} /> : <CheckCircle2 size={18} />}
                        <span>{message.text}</span>
                    </div>
                )}

                <form className="client-auth-form" onSubmit={handleSubmit}>
                    {!isLogin && (
                        <div className="client-form-group">
                            <label>Họ và tên</label>
                            <div className="client-input-wrapper">
                                <User size={18} className="input-icon" />
                                <input type="text" name="fullName" value={formData.fullName} onChange={handleChange} placeholder="Nguyễn Minh Anh" required={!isLogin} className="client-input-icon" />
                            </div>
                        </div>
                    )}

                    <div className="client-form-group">
                        <label>Tên đăng nhập (Username)</label>
                        <div className="client-input-wrapper">
                            <User size={18} className="input-icon" />
                            <input type="text" name="username" value={formData.username} onChange={handleChange} placeholder="Nhập tên đăng nhập" required className="client-input-icon" />
                        </div>
                    </div>

                    {!isLogin && (
                        <div className="client-form-group">
                            <label>Email</label>
                            <div className="client-input-wrapper">
                                <Mail size={18} className="input-icon" />
                                <input type="email" name="email" value={formData.email} onChange={handleChange} placeholder="example@gmail.com" required={!isLogin} className="client-input-icon" />
                            </div>
                        </div>
                    )}

                    <div className="client-form-group">
                        <label>Mật khẩu</label>
                        <div className="client-input-wrapper">
                            <Lock size={18} className="input-icon" />
                            <input type="password" name="password" value={formData.password} onChange={handleChange} placeholder="••••••••" required className="client-input-icon" />
                        </div>
                    </div>

                    <button type="submit" className="client-btn-primary client-btn-full client-auth-btn" disabled={isLoading}>
                        {isLoading ? (
                            <span className="loading-spinner"> Đang xử lý...</span>
                        ) : (
                            <>
                                {isLogin ? <LogIn size={18} /> : <UserPlus size={18} />}
                                {isLogin ? 'Đăng nhập' : 'Tạo tài khoản'}
                            </>
                        )}
                    </button>
                </form>
            </div>
        </div>
    );
}