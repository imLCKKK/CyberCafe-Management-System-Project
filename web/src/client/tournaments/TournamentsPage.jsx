import React, { useState } from 'react';
import { Trophy, Calendar, Users, ChevronRight } from 'lucide-react';
import { mockTournaments } from './mockData';

export default function TournamentsPage() {
    const [tournaments] = useState(mockTournaments);

    const formatCurrency = (amount) => {
        return amount === 0 ? 'Miễn phí' : amount.toLocaleString('vi-VN') + 'đ';
    };

    const formatDate = (dateString) => {
        const date = new Date(dateString);
        return date.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }) + ' - ' + date.toLocaleDateString('vi-VN');
    };

    const handleRegister = (tournamentName) => {
        alert(`Mở form đăng ký cho giải: ${tournamentName}`);
    };

    return (
        <div className="client-tournament-container">
            <div className="client-tournament-header">
                <h1 className="client-page-title">Giải đấu</h1>
                <p className="client-page-subtitle">Tham gia các giải đấu hấp dẫn và rinh giải thưởng lớn.</p>
            </div>

            <div className="client-tournament-grid">
                {tournaments.map((tour) => (
                    <div key={tour.TournamentId} className="client-tournament-card">
                        <div className="client-tournament-card-header">
                            <div className="client-tournament-badge">
                                <Trophy size={16} />
                                <span>{tour.GameName}</span>
                            </div>
                            <span className={`client-tournament-status ${tour.Status.toLowerCase()}`}>
                                {tour.Status === 'Open' ? 'Đang mở đăng ký' : 'Đã kết thúc'}
                            </span>
                        </div>

                        <h3 className="client-tournament-name">{tour.TournamentName}</h3>

                        <div className="client-tournament-info-list">
                            <div className="client-tournament-info-item">
                                <Calendar size={16} className="client-icon-gray" />
                                <span>Bắt đầu: {formatDate(tour.StartTime)}</span>
                            </div>
                            <div className="client-tournament-info-item">
                                <Users size={16} className="client-icon-gray" />
                                <span>Đội hình: {tour.TeamSize} người</span>
                            </div>
                            <div className="client-tournament-info-item">
                                <span className="client-tournament-fee">Lệ phí: {formatCurrency(tour.EntryFee)}</span>
                            </div>
                        </div>

                        <button
                            className="client-btn-outline"
                            onClick={() => handleRegister(tour.TournamentName)}
                            disabled={tour.Status !== 'Open'}
                        >
                            Xem chi tiết & Đăng ký
                            <ChevronRight size={16} />
                        </button>
                    </div>
                ))}
            </div>
        </div>
    );
}