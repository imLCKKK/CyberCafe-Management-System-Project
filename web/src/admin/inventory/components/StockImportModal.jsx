import React, { useState, useEffect } from 'react';
import { X, ArrowDownToLine } from 'lucide-react';

export function StockImportModal({ isOpen, item, allItems, onClose, onImport }) {
  const [selectedId, setSelectedId] = useState('');
  const [quantity, setQuantity] = useState('');
  const [costPrice, setCostPrice] = useState('');
  const [note, setNote] = useState('');

  useEffect(() => {
    if (item) {
      setSelectedId(item.id);
      setCostPrice(item.costPrice || '');
    } else if (allItems && allItems.length > 0) {
      setSelectedId(allItems[0].id);
      setCostPrice(allItems[0].costPrice || '');
    }
    setQuantity('');
    setNote('');
  }, [item, allItems, isOpen]);

  if (!isOpen) return null;

  const currentItem = item || allItems.find((i) => i.id === selectedId) || allItems[0];

  const handleItemSelectChange = (id) => {
    setSelectedId(id);
    const target = allItems.find((i) => i.id === id);
    if (target) {
      setCostPrice(target.costPrice || '');
    }
  };

  const handleSubmit = (e) => {
    e.preventDefault();
    const qty = parseInt(quantity, 10);
    if (!qty || qty <= 0) return;

    onImport(currentItem.id, qty, Number(costPrice) || currentItem.costPrice, note);
  };

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
          maxWidth: '440px',
          overflow: 'hidden',
          boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1)',
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <div style={{ padding: '14px 18px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <h3 style={{ margin: 0, fontSize: '15px', fontWeight: 800, color: 'var(--text)', display: 'flex', alignItems: 'center', gap: '6px' }}>
            <ArrowDownToLine size={16} color="#2563EB" />
            Nhập kho hàng hóa
          </h3>
          <button onClick={onClose} style={{ background: 'none', border: 'none', color: '#8993A4', cursor: 'pointer' }}>
            <X size={16} />
          </button>
        </div>

        <form onSubmit={handleSubmit}>
          <div style={{ padding: '16px 18px', display: 'flex', flexDirection: 'column', gap: '12px', fontSize: '12px' }}>
            <div>
              <label style={{ display: 'block', fontWeight: 700, color: '#475569', marginBottom: '5px' }}>Chọn mặt hàng nhập *</label>
              {item ? (
                <input
                  type="text"
                  disabled
                  value={`[${item.id}] ${item.name} (Tồn: ${item.stock} ${item.unit})`}
                  style={{ width: '100%', padding: '8px 10px', border: '1px solid var(--border)', borderRadius: '6px', fontSize: '12px', background: '#F8FAFC', color: '#64748B', boxSizing: 'border-box' }}
                />
              ) : (
                <select
                  value={selectedId}
                  onChange={(e) => handleItemSelectChange(e.target.value)}
                  style={{ width: '100%', padding: '8px 10px', border: '1px solid var(--border)', borderRadius: '6px', fontSize: '12px', outline: 'none', boxSizing: 'border-box', background: '#FFF' }}
                >
                  {allItems.map((i) => (
                    <option key={i.id} value={i.id}>
                      [{i.id}] {i.name} (Tồn: {i.stock} {i.unit})
                    </option>
                  ))}
                </select>
              )}
            </div>

            <div style={{ display: 'flex', gap: '10px' }}>
              <div style={{ flex: 1 }}>
                <label style={{ display: 'block', fontWeight: 700, color: '#475569', marginBottom: '5px' }}>Số lượng nhập thêm *</label>
                <input
                  type="number"
                  min="1"
                  required
                  autoFocus
                  placeholder="0"
                  value={quantity}
                  onChange={(e) => setQuantity(e.target.value)}
                  style={{ width: '100%', padding: '8px 10px', border: '1px solid var(--border)', borderRadius: '6px', fontSize: '12px', outline: 'none', boxSizing: 'border-box' }}
                />
              </div>

              <div style={{ flex: 1 }}>
                <label style={{ display: 'block', fontWeight: 700, color: '#475569', marginBottom: '5px' }}>Đơn giá vốn đợt này</label>
                <input
                  type="number"
                  min="0"
                  step="1000"
                  placeholder="0"
                  value={costPrice}
                  onChange={(e) => setCostPrice(e.target.value)}
                  style={{ width: '100%', padding: '8px 10px', border: '1px solid var(--border)', borderRadius: '6px', fontSize: '12px', outline: 'none', boxSizing: 'border-box' }}
                />
              </div>
            </div>

            {currentItem && (
              <div style={{ background: '#F8FAFC', border: '1px solid var(--border)', borderRadius: '6px', padding: '8px 12px', fontSize: '12px', color: '#475569' }}>
                Tồn kho sau khi nhập: <strong style={{ color: 'var(--text)' }}>{currentItem.stock + (parseInt(quantity, 10) || 0)} {currentItem.unit}</strong>
              </div>
            )}

            <div>
              <label style={{ display: 'block', fontWeight: 700, color: '#475569', marginBottom: '5px' }}>Ghi chú đợt nhập</label>
              <input
                type="text"
                placeholder="VD: Lô hàng đầu tháng..."
                value={note}
                onChange={(e) => setNote(e.target.value)}
                style={{ width: '100%', padding: '8px 10px', border: '1px solid var(--border)', borderRadius: '6px', fontSize: '12px', outline: 'none', boxSizing: 'border-box' }}
              />
            </div>
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
              type="submit"
              style={{ padding: '7px 16px', borderRadius: '6px', border: 'none', background: '#111C30', color: '#FFFFFF', fontSize: '12px', fontWeight: 700, cursor: 'pointer' }}
            >
              Xác nhận nhập kho
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
