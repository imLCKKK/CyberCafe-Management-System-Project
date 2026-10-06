import React, { useState, useMemo } from 'react';
import {
  INITIAL_INVOICES,
  calculateInvoiceStats,
} from './mockData';
import { InvoiceStats } from './components/InvoiceStats';
import { InvoiceToolbar } from './components/InvoiceToolbar';
import { InvoiceTable } from './components/InvoiceTable';
import { InvoiceDetailModal } from './components/InvoiceDetailModal';

export function InvoicesPage() {
  const [invoices, setInvoices] = useState(INITIAL_INVOICES);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [methodFilter, setMethodFilter] = useState('all');

  const [selectedInvoice, setSelectedInvoice] = useState(null);
  const [isDetailOpen, setIsDetailOpen] = useState(false);

  // Compute stats
  const stats = useMemo(() => calculateInvoiceStats(invoices), [invoices]);

  // Filter invoices
  const filteredInvoices = useMemo(() => {
    return invoices.filter((inv) => {
      // Status filter
      if (statusFilter !== 'all' && inv.status !== statusFilter) {
        return false;
      }

      // Method filter
      if (methodFilter !== 'all' && inv.paymentMethod !== methodFilter) {
        return false;
      }

      // Search query
      if (searchQuery.trim()) {
        const query = searchQuery.toLowerCase().trim();
        const matchId = inv.id.toLowerCase().includes(query);
        const matchOrder = inv.orderCode.toLowerCase().includes(query);
        const matchCustomer = inv.customerName.toLowerCase().includes(query);
        if (!matchId && !matchOrder && !matchCustomer) return false;
      }

      return true;
    });
  }, [invoices, statusFilter, methodFilter, searchQuery]);

  // Handlers
  const handleOpenDetail = (inv) => {
    setSelectedInvoice(inv);
    setIsDetailOpen(true);
  };

  const handleMarkAsPaid = (invoiceId, paymentMethod = 'Tiền mặt') => {
    setInvoices((prev) =>
      prev.map((inv) => {
        if (inv.id === invoiceId) {
          const updated = {
            ...inv,
            status: 'paid',
            paymentMethod,
            note: inv.note ? `${inv.note} (Đã thu tại quầy: ${paymentMethod})` : `Đã thu tại quầy: ${paymentMethod}`,
          };
          if (selectedInvoice && selectedInvoice.id === invoiceId) {
            setSelectedInvoice(updated);
          }
          return updated;
        }
        return inv;
      })
    );
  };

  const handleQuickPayCash = (inv) => {
    handleMarkAsPaid(inv.id, 'Tiền mặt');
  };

  return (
    <div className="page" style={{ display: 'block' }}>
      {/* Module Title */}
      <div style={{ marginBottom: '18px' }}>
        <h2 style={{ fontSize: '18px', fontWeight: 800, color: 'var(--text)', margin: '0 0 4px 0' }}>
          Hóa đơn & thanh toán
        </h2>
        <p style={{ fontSize: '12px', color: '#8993A4', margin: 0 }}>
          Theo dõi doanh thu phòng máy, xác nhận thu tiền mặt và in biên lai đối soát
        </p>
      </div>

      {/* KPI Stats */}
      <InvoiceStats
        stats={stats}
        statusFilter={statusFilter}
        setStatusFilter={setStatusFilter}
      />

      {/* Toolbar */}
      <InvoiceToolbar
        searchQuery={searchQuery}
        setSearchQuery={setSearchQuery}
        statusFilter={statusFilter}
        setStatusFilter={setStatusFilter}
        methodFilter={methodFilter}
        setMethodFilter={setMethodFilter}
      />

      {/* Table */}
      <InvoiceTable
        invoices={filteredInvoices}
        onOpenDetail={handleOpenDetail}
        onQuickPayCash={handleQuickPayCash}
      />

      {/* Detail & Print Modal */}
      <InvoiceDetailModal
        isOpen={isDetailOpen}
        invoice={selectedInvoice}
        onClose={() => setIsDetailOpen(false)}
        onMarkAsPaid={handleMarkAsPaid}
      />
    </div>
  );
}

export default InvoicesPage;
