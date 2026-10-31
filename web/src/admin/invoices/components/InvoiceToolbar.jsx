import React from 'react';
import { Search } from 'lucide-react';

const STATUS_OPTIONS = [
  { id: 'all', label: 'Tất cả trạng thái' },
  { id: 'paid', label: 'Đã thanh toán' },
  { id: 'pending', label: 'Chờ xử lý' },
  { id: 'cancelled', label: 'Đã hủy' },
];

const METHOD_OPTIONS = [
  { id: 'all', label: 'Tất cả phương thức' },
  { id: 'Tiền mặt', label: 'Tiền mặt' },
  { id: 'Chuyển khoản', label: 'Chuyển khoản' },
  { id: 'Ví hội viên', label: 'Ví hội viên' },
];

export function InvoiceToolbar({
  searchQuery,
  setSearchQuery,
  statusFilter,
  setStatusFilter,
  methodFilter,
  setMethodFilter,
}) {
  return (
    <div
      style={{
        background: '#FFFFFF',
        border: '1px solid var(--border)',
        borderRadius: '10px',
        padding: '14px',
        marginBottom: '16px',
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
      }}
    >
      {/* Row 1: Search & Status Pills */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '12px', flexWrap: 'wrap' }}>
        <div style={{ position: 'relative', minWidth: '240px', flex: 1, maxWidth: '360px' }}>
          <Search size={14} style={{ position: 'absolute', left: '10px', top: '50%', transform: 'translateY(-50%)', color: '#8993A4', pointerEvents: 'none' }} />
          <input
            type="text"
            placeholder="Tìm theo mã HĐ, mã order, tên khách..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            style={{
              width: '100%',
              padding: '8px 10px 8px 30px',
              border: '1px solid var(--border)',
              borderRadius: '6px',
              fontSize: '12px',
              background: '#F8FAFC',
              outline: 'none',
              boxSizing: 'border-box',
            }}
          />
        </div>

        <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
          {STATUS_OPTIONS.map((opt) => {
            const isActive = statusFilter === opt.id;
            return (
              <button
                key={opt.id}
                type="button"
                onClick={() => setStatusFilter(opt.id)}
                style={{
                  padding: '6px 12px',
                  borderRadius: '16px',
                  border: isActive ? '1px solid #111C30' : '1px solid var(--border)',
                  background: isActive ? '#111C30' : '#F1F5F9',
                  color: isActive ? '#FFFFFF' : '#475569',
                  fontSize: '11px',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                {opt.label}
              </button>
            );
          })}
        </div>
      </div>

      {/* Row 2: Payment Method */}
      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
        <span style={{ fontSize: '11px', fontWeight: 700, color: '#8993A4' }}>Phương thức:</span>
        <div style={{ display: 'flex', gap: '6px' }}>
          {METHOD_OPTIONS.map((opt) => {
            const isActive = methodFilter === opt.id;
            return (
              <button
                key={opt.id}
                type="button"
                onClick={() => setMethodFilter(opt.id)}
                style={{
                  padding: '4px 10px',
                  borderRadius: '6px',
                  border: isActive ? '1px solid #2563EB' : '1px solid var(--border)',
                  background: isActive ? '#EFF6FF' : '#FFFFFF',
                  color: isActive ? '#1D4ED8' : '#475569',
                  fontSize: '11px',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                {opt.label}
              </button>
            );
          })}
        </div>
      </div>
    </div>
  );
}
