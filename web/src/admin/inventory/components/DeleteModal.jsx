import React from 'react';
import { AlertTriangle, X } from 'lucide-react';

export function DeleteModal({ isOpen, item, onClose, onConfirm }) {
  if (!isOpen || !item) return null;

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        background: 'rgba(17, 28, 48, 0.6)',
        backdropFilter: 'blur(2px)',
        display: 'grid',
        placeItems: 'center',
        zIndex: 1000,
        padding: '16px',
      }}
      onClick={onClose}
    >
      <div
        style={{
          background: '#FFFFFF',
          borderRadius: '12px',
          width: '100%',
          maxWidth: '380px',
          overflow: 'hidden',
          boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1)',
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <div style={{ padding: '14px 18px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <h3 style={{ margin: 0, fontSize: '14px', fontWeight: 800, color: '#DC2626', display: 'flex', alignItems: 'center', gap: '6px' }}>
            <AlertTriangle size={16} />
            Xác nhận xóa mặt hàng
          </h3>
          <button onClick={onClose} style={{ background: 'none', border: 'none', color: '#8993A4', cursor: 'pointer' }}>
            <X size={16} />
          </button>
        </div>

        <div style={{ padding: '16px 18px', fontSize: '13px', color: '#334155', lineHeight: 1.5 }}>
          Bạn có chắc chắn muốn xóa <strong>[{item.id}] {item.name}</strong> khỏi kho hàng?
          <p style={{ margin: '6px 0 0', fontSize: '11px', color: '#8993A4' }}>
            Hành động này không thể hoàn tác.
          </p>
        </div>

        <div style={{ padding: '12px 18px', background: '#F8FAFC', borderTop: '1px solid var(--border)', display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
          <button
            type="button"
            onClick={onClose}
            style={{ padding: '7px 14px', borderRadius: '6px', border: '1px solid var(--border)', background: '#FFFFFF', fontSize: '12px', fontWeight: 600, cursor: 'pointer' }}
          >
            Hủy
          </button>
          <button
            type="button"
            onClick={() => onConfirm(item.id)}
            style={{ padding: '7px 16px', borderRadius: '6px', border: 'none', background: '#DC2626', color: '#FFFFFF', fontSize: '12px', fontWeight: 700, cursor: 'pointer' }}
          >
            Xóa
          </button>
        </div>
      </div>
    </div>
  );
}
