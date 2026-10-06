import React, { useState, useEffect } from 'react';
import { X } from 'lucide-react';
import { INVENTORY_CATEGORIES } from '../mockData';

export function ItemModal({ isOpen, item, onClose, onSave }) {
  const [formData, setFormData] = useState({
    name: '',
    category: 'Nước uống',
    costPrice: '',
    salePrice: '',
    stock: '',
    unit: 'Lon',
  });

  useEffect(() => {
    if (item) {
      setFormData({
        name: item.name || '',
        category: item.category || 'Nước uống',
        costPrice: item.costPrice || '',
        salePrice: item.salePrice || '',
        stock: item.stock !== undefined ? item.stock : '',
        unit: item.unit || 'Lon',
      });
    } else {
      setFormData({
        name: '',
        category: 'Nước uống',
        costPrice: '',
        salePrice: '',
        stock: '',
        unit: 'Lon',
      });
    }
  }, [item, isOpen]);

  if (!isOpen) return null;

  const validCategories = INVENTORY_CATEGORIES.filter((c) => c !== 'Tất cả');

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!formData.name.trim()) return;

    onSave({
      ...item,
      name: formData.name.trim(),
      category: formData.category,
      costPrice: Number(formData.costPrice) || 0,
      salePrice: Number(formData.salePrice) || 0,
      stock: Number(formData.stock) || 0,
      unit: formData.unit.trim() || 'Cái',
    });
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
          <h3 style={{ margin: 0, fontSize: '15px', fontWeight: 800, color: 'var(--text)' }}>
            {item ? 'Chỉnh sửa mặt hàng' : 'Thêm mặt hàng mới'}
          </h3>
          <button onClick={onClose} style={{ background: 'none', border: 'none', color: '#8993A4', cursor: 'pointer' }}>
            <X size={16} />
          </button>
        </div>

        <form onSubmit={handleSubmit}>
          <div style={{ padding: '16px 18px', display: 'flex', flexDirection: 'column', gap: '12px', fontSize: '12px' }}>
            <div>
              <label style={{ display: 'block', fontWeight: 700, color: '#475569', marginBottom: '5px' }}>Tên mặt hàng *</label>
              <input
                type="text"
                required
                placeholder="Ví dụ: Sting dâu lon 330ml"
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                style={{ width: '100%', padding: '8px 10px', border: '1px solid var(--border)', borderRadius: '6px', fontSize: '12px', outline: 'none', boxSizing: 'border-box' }}
              />
            </div>

            <div style={{ display: 'flex', gap: '10px' }}>
              <div style={{ flex: 1 }}>
                <label style={{ display: 'block', fontWeight: 700, color: '#475569', marginBottom: '5px' }}>Danh mục</label>
                <select
                  value={formData.category}
                  onChange={(e) => setFormData({ ...formData, category: e.target.value })}
                  style={{ width: '100%', padding: '8px 10px', border: '1px solid var(--border)', borderRadius: '6px', fontSize: '12px', outline: 'none', boxSizing: 'border-box', background: '#FFF' }}
                >
                  {validCategories.map((c) => (
                    <option key={c} value={c}>{c}</option>
                  ))}
                </select>
              </div>

              <div style={{ flex: 1 }}>
                <label style={{ display: 'block', fontWeight: 700, color: '#475569', marginBottom: '5px' }}>Đơn vị tính</label>
                <input
                  type="text"
                  placeholder="Lon, Phần, Thẻ..."
                  value={formData.unit}
                  onChange={(e) => setFormData({ ...formData, unit: e.target.value })}
                  style={{ width: '100%', padding: '8px 10px', border: '1px solid var(--border)', borderRadius: '6px', fontSize: '12px', outline: 'none', boxSizing: 'border-box' }}
                />
              </div>
            </div>

            <div style={{ display: 'flex', gap: '10px' }}>
              <div style={{ flex: 1 }}>
                <label style={{ display: 'block', fontWeight: 700, color: '#475569', marginBottom: '5px' }}>Giá vốn (VNĐ)</label>
                <input
                  type="number"
                  min="0"
                  step="1000"
                  placeholder="0"
                  value={formData.costPrice}
                  onChange={(e) => setFormData({ ...formData, costPrice: e.target.value })}
                  style={{ width: '100%', padding: '8px 10px', border: '1px solid var(--border)', borderRadius: '6px', fontSize: '12px', outline: 'none', boxSizing: 'border-box' }}
                />
              </div>

              <div style={{ flex: 1 }}>
                <label style={{ display: 'block', fontWeight: 700, color: '#475569', marginBottom: '5px' }}>Giá bán (VNĐ) *</label>
                <input
                  type="number"
                  min="0"
                  step="1000"
                  required
                  placeholder="0"
                  value={formData.salePrice}
                  onChange={(e) => setFormData({ ...formData, salePrice: e.target.value })}
                  style={{ width: '100%', padding: '8px 10px', border: '1px solid var(--border)', borderRadius: '6px', fontSize: '12px', outline: 'none', boxSizing: 'border-box' }}
                />
              </div>
            </div>

            <div>
              <label style={{ display: 'block', fontWeight: 700, color: '#475569', marginBottom: '5px' }}>Số lượng tồn kho</label>
              <input
                type="number"
                min="0"
                placeholder="0"
                value={formData.stock}
                onChange={(e) => setFormData({ ...formData, stock: e.target.value })}
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
              {item ? 'Cập nhật' : 'Thêm mới'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
