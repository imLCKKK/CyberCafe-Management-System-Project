import React from 'react';
import { Pencil, Trash2, ArrowDownToLine, AlertCircle } from 'lucide-react';
import { formatCurrency } from '../mockData';

export function InventoryTable({ items, onEdit, onImport, onDelete }) {
  const getStatusBadge = (status) => {
    switch (status) {
      case 'in_stock':
        return <span style={{ padding: '3px 8px', borderRadius: '4px', background: '#ECFDF5', color: '#059669', fontSize: '11px', fontWeight: 700 }}>Còn hàng</span>;
      case 'low_stock':
        return <span style={{ padding: '3px 8px', borderRadius: '4px', background: '#FFFBEB', color: '#D97706', fontSize: '11px', fontWeight: 700 }}>Sắp hết</span>;
      case 'out_of_stock':
        return <span style={{ padding: '3px 8px', borderRadius: '4px', background: '#FEF2F2', color: '#DC2626', fontSize: '11px', fontWeight: 700 }}>Hết hàng</span>;
      default:
        return null;
    }
  };

  return (
    <div style={{ background: '#FFFFFF', border: '1px solid var(--border)', borderRadius: '10px', overflow: 'hidden' }}>
      <div style={{ overflowX: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '12px' }}>
          <thead>
            <tr style={{ background: '#F8FAFC', borderBottom: '1px solid var(--border)', color: '#8993A4', fontSize: '11px', textTransform: 'uppercase', letterSpacing: '0.6px' }}>
              <th style={{ padding: '10px 14px' }}>Mã hàng</th>
              <th style={{ padding: '10px 14px' }}>Tên mặt hàng</th>
              <th style={{ padding: '10px 14px' }}>Danh mục</th>
              <th style={{ padding: '10px 14px' }}>Giá vốn</th>
              <th style={{ padding: '10px 14px' }}>Giá bán</th>
              <th style={{ padding: '10px 14px' }}>Tồn kho</th>
              <th style={{ padding: '10px 14px' }}>Trạng thái</th>
              <th style={{ padding: '10px 14px', textAlign: 'right' }}>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td colSpan="8" style={{ textAlign: 'center', padding: '40px 16px', color: '#8993A4' }}>
                  <div style={{ display: 'inline-flex', flexDirection: 'column', alignItems: 'center', gap: '6px' }}>
                    <AlertCircle size={24} />
                    <span>Không tìm thấy mặt hàng nào</span>
                  </div>
                </td>
              </tr>
            ) : (
              items.map((item) => (
                <tr key={item.id} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td style={{ padding: '11px 14px', fontWeight: 700, color: '#2563EB', fontFamily: 'monospace' }}>{item.id}</td>
                  <td style={{ padding: '11px 14px', fontWeight: 600, color: 'var(--text)' }}>{item.name}</td>
                  <td style={{ padding: '11px 14px' }}>
                    <span style={{ background: '#F1F5F9', padding: '2px 7px', borderRadius: '4px', fontSize: '11px', color: '#475569' }}>
                      {item.category}
                    </span>
                  </td>
                  <td style={{ padding: '11px 14px', color: '#8993A4' }}>{formatCurrency(item.costPrice)}</td>
                  <td style={{ padding: '11px 14px', fontWeight: 700, color: 'var(--text)' }}>{formatCurrency(item.salePrice)}</td>
                  <td style={{ padding: '11px 14px' }}>
                    <strong style={{ color: item.stock === 0 ? '#DC2626' : item.stock <= 5 ? '#D97706' : 'var(--text)' }}>
                      {item.stock}
                    </strong>{' '}
                    <span style={{ fontSize: '11px', color: '#8993A4' }}>{item.unit}</span>
                  </td>
                  <td style={{ padding: '11px 14px' }}>{getStatusBadge(item.status)}</td>
                  <td style={{ padding: '11px 14px', textAlign: 'right' }}>
                    <div style={{ display: 'inline-flex', gap: '5px' }}>
                      <button
                        type="button"
                        onClick={() => onImport(item)}
                        style={{ padding: '4px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: '#FFFFFF', fontSize: '11px', fontWeight: 600, cursor: 'pointer', display: 'inline-flex', alignItems: 'center', gap: '3px' }}
                      >
                        <ArrowDownToLine size={11} /> Nhập
                      </button>
                      <button
                        type="button"
                        onClick={() => onEdit(item)}
                        style={{ padding: '4px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: '#FFFFFF', fontSize: '11px', fontWeight: 600, cursor: 'pointer', display: 'inline-flex', alignItems: 'center', gap: '3px' }}
                      >
                        <Pencil size={11} /> Sửa
                      </button>
                      <button
                        type="button"
                        onClick={() => onDelete(item)}
                        style={{ padding: '4px 8px', borderRadius: '4px', border: '1px solid #FECACA', background: '#FEF2F2', color: '#DC2626', fontSize: '11px', fontWeight: 600, cursor: 'pointer', display: 'inline-flex', alignItems: 'center', gap: '3px' }}
                      >
                        <Trash2 size={11} /> Xóa
                      </button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
