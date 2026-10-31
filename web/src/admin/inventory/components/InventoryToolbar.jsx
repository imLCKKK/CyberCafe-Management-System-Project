import React from 'react';
import { Search, Plus, ArrowDownToLine } from 'lucide-react';
import { INVENTORY_CATEGORIES } from '../mockData';

export function InventoryToolbar({
  searchQuery,
  setSearchQuery,
  selectedCategory,
  setSelectedCategory,
  onOpenAddItem,
  onOpenImport,
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
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: '12px',
        flexWrap: 'wrap',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap', flex: 1 }}>
        {/* Search */}
        <div style={{ position: 'relative', minWidth: '240px' }}>
          <Search size={14} style={{ position: 'absolute', left: '10px', top: '50%', transform: 'translateY(-50%)', color: '#8993A4', pointerEvents: 'none' }} />
          <input
            type="text"
            placeholder="Tìm theo tên món, mã hàng..."
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
            }}
          />
        </div>

        {/* Categories */}
        <div style={{ display: 'flex', gap: '6px' }}>
          {INVENTORY_CATEGORIES.map((cat) => {
            const isActive = selectedCategory === cat;
            return (
              <button
                key={cat}
                type="button"
                onClick={() => setSelectedCategory(cat)}
                style={{
                  padding: '6px 12px',
                  borderRadius: '6px',
                  border: isActive ? '1px solid #111C30' : '1px solid var(--border)',
                  background: isActive ? '#111C30' : '#FFFFFF',
                  color: isActive ? '#FFFFFF' : '#64748B',
                  fontSize: '12px',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                {cat}
              </button>
            );
          })}
        </div>
      </div>

      {/* Buttons */}
      <div style={{ display: 'flex', gap: '8px' }}>
        <button
          type="button"
          onClick={onOpenImport}
          style={{
            padding: '7px 12px',
            borderRadius: '6px',
            background: '#FFFFFF',
            color: '#111C30',
            border: '1px solid var(--border)',
            fontSize: '12px',
            fontWeight: 700,
            cursor: 'pointer',
            display: 'inline-flex',
            alignItems: 'center',
            gap: '5px',
          }}
        >
          <ArrowDownToLine size={13} />
          Nhập kho
        </button>
        <button
          type="button"
          onClick={onOpenAddItem}
          style={{
            padding: '7px 14px',
            borderRadius: '6px',
            background: '#111C30',
            color: '#FFFFFF',
            border: 'none',
            fontSize: '12px',
            fontWeight: 700,
            cursor: 'pointer',
            display: 'inline-flex',
            alignItems: 'center',
            gap: '5px',
          }}
        >
          <Plus size={13} />
          Thêm món mới
        </button>
      </div>
    </div>
  );
}
