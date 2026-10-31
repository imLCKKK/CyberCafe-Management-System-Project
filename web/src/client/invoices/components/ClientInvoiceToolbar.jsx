import React from 'react';
import { Search } from 'lucide-react';

const FILTERS = [
  { id: 'all', label: 'Tất cả' },
  { id: 'order', label: 'Gọi món' },
  { id: 'topup', label: 'Nạp tiền ví' },
  { id: 'playtime', label: 'Giờ chơi máy' },
  { id: 'combo', label: 'Combo' },
  { id: 'pending', label: 'Chờ thanh toán' },
];

export function ClientInvoiceToolbar({
  activeFilter,
  setActiveFilter,
  searchQuery,
  setSearchQuery,
  unpaidCount,
}) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '12px', marginBottom: '14px', flexWrap: 'wrap' }}>
      {/* Tabs */}
      <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap' }}>
        {FILTERS.map((tab) => {
          const isActive = activeFilter === tab.id;
          const isPending = tab.id === 'pending';
          const showAlert = isPending && unpaidCount > 0;

          return (
            <button
              key={tab.id}
              type="button"
              onClick={() => setActiveFilter(tab.id)}
              style={{
                height: '30px',
                padding: '0 12px',
                borderRadius: '6px',
                border: '1px solid #303638',
                background: isActive ? '#25391d' : '#1a1f21',
                color: isActive ? '#baf34c' : '#8b9497',
                fontSize: '11px',
                fontWeight: isActive ? 700 : 500,
                cursor: 'pointer',
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
              }}
            >
              <span>{tab.label}</span>
              {showAlert && (
                <span
                  style={{
                    width: '14px',
                    height: '14px',
                    borderRadius: '50%',
                    background: '#fbbf24',
                    color: '#000',
                    fontSize: '9px',
                    fontWeight: 900,
                    display: 'grid',
                    placeItems: 'center',
                  }}
                >
                  {unpaidCount}
                </span>
              )}
            </button>
          );
        })}
      </div>

      {/* Search */}
      <div style={{ position: 'relative', minWidth: '220px' }}>
        <Search
          size={13}
          style={{ position: 'absolute', left: '10px', top: '50%', transform: 'translateY(-50%)', color: '#737c7f', pointerEvents: 'none' }}
        />
        <input
          type="text"
          placeholder="Tìm mã đơn, tên món..."
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          style={{
            width: '100%',
            height: '30px',
            padding: '0 10px 0 30px',
            borderRadius: '6px',
            background: '#1a1f21',
            border: '1px solid #303638',
            color: '#e8ecee',
            fontSize: '11px',
            outline: 'none',
          }}
        />
      </div>
    </div>
  );
}
