import React, { useState, useMemo } from 'react';
import {
  INITIAL_CLIENT_SESSION,
  INITIAL_CLIENT_INVOICES,
  calculateClientStats,
} from './mockData';
import { ClientInvoiceSummary } from './components/ClientInvoiceSummary';
import { ClientInvoiceToolbar } from './components/ClientInvoiceToolbar';
import { ClientInvoiceTable } from './components/ClientInvoiceTable';
import { ClientInvoiceDetailModal } from './components/ClientInvoiceDetailModal';

export function InvoicesPage() {
  const [session, setSession] = useState(INITIAL_CLIENT_SESSION);
  const [invoices, setInvoices] = useState(INITIAL_CLIENT_INVOICES);
  const [activeFilter, setActiveFilter] = useState('all');
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedInvoice, setSelectedInvoice] = useState(null);

  // Stats
  const stats = useMemo(() => {
    return calculateClientStats(invoices, session);
  }, [invoices, session]);

  // Unpaid invoices count
  const unpaidCount = useMemo(() => {
    return invoices.filter(
      (inv) =>
        inv.status === 'pending_payment' || inv.status === 'pending_confirmation'
    ).length;
  }, [invoices]);

  // Filtered invoices
  const filteredInvoices = useMemo(() => {
    return invoices.filter((inv) => {
      // Type/Status Tab Filter
      if (activeFilter === 'pending') {
        if (
          inv.status !== 'pending_payment' &&
          inv.status !== 'pending_confirmation'
        ) {
          return false;
        }
      } else if (activeFilter !== 'all') {
        if (inv.type !== activeFilter) {
          return false;
        }
      }

      // Search Query
      if (searchQuery.trim()) {
        const query = searchQuery.toLowerCase().trim();
        const matchId = inv.id.toLowerCase().includes(query);
        const matchTitle = inv.title.toLowerCase().includes(query);
        const matchItems = inv.items.some((i) =>
          i.name.toLowerCase().includes(query)
        );
        if (!matchId && !matchTitle && !matchItems) return false;
      }

      return true;
    });
  }, [invoices, activeFilter, searchQuery]);

  // Action: Pay with Wallet
  const handlePayWithWallet = (invoiceId) => {
    const target = invoices.find((i) => i.id === invoiceId);
    if (!target) return;

    if (session.walletBalance < target.amount) {
      alert('Số dư ví không đủ để thanh toán. Vui lòng nạp thêm tiền!');
      return;
    }

    // Deduct balance
    setSession((prev) => ({
      ...prev,
      walletBalance: prev.walletBalance - target.amount,
      unpaidOrderAmount: Math.max(0, prev.unpaidOrderAmount - target.amount),
    }));

    // Update invoice
    setInvoices((prev) =>
      prev.map((i) => {
        if (i.id === invoiceId) {
          const updated = {
            ...i,
            status: 'completed',
            statusLabel: 'Đã thanh toán',
            paymentMethod: 'Ví hội viên',
          };
          if (selectedInvoice && selectedInvoice.id === invoiceId) {
            setSelectedInvoice(updated);
          }
          return updated;
        }
        return i;
      })
    );
  };

  // Action: Request Cash Payment
  const handleRequestCashPayment = (invoiceId) => {
    setInvoices((prev) =>
      prev.map((i) => {
        if (i.id === invoiceId) {
          const updated = {
            ...i,
            status: 'pending_confirmation',
            statusLabel: 'Chờ NV xác nhận',
            paymentMethod: 'Tiền mặt',
            note: i.note
              ? `${i.note} (Yêu cầu thanh toán tiền mặt tại máy)`
              : 'Yêu cầu thanh toán tiền mặt tại máy',
          };
          if (selectedInvoice && selectedInvoice.id === invoiceId) {
            setSelectedInvoice(updated);
          }
          return updated;
        }
        return i;
      })
    );
  };

  return (
    <div className="client-page">
      {/* Page Heading matching client.css */}
      <div className="client-page-heading">
        <div>
          <h1>Hóa đơn & Lịch sử giao dịch</h1>
          <p>
            Theo dõi chi tiêu giờ chơi, đơn gọi món và nạp tiền ví tại {session.machineId}
          </p>
        </div>
      </div>

      {/* KPI Cards Summary */}
      <ClientInvoiceSummary
        session={session}
        stats={stats}
        onSelectFilter={setActiveFilter}
      />

      {/* Toolbar & Filters */}
      <ClientInvoiceToolbar
        activeFilter={activeFilter}
        setActiveFilter={setActiveFilter}
        searchQuery={searchQuery}
        setSearchQuery={setSearchQuery}
        unpaidCount={unpaidCount}
      />

      {/* Table */}
      <ClientInvoiceTable
        invoices={filteredInvoices}
        onOpenDetail={setSelectedInvoice}
        onPayWithWallet={(inv) => handlePayWithWallet(inv.id)}
      />

      {/* Receipt Modal */}
      <ClientInvoiceDetailModal
        isOpen={Boolean(selectedInvoice)}
        invoice={selectedInvoice}
        walletBalance={session.walletBalance}
        onClose={() => setSelectedInvoice(null)}
        onPayWithWallet={handlePayWithWallet}
        onRequestCashPayment={handleRequestCashPayment}
      />
    </div>
  );
}

export default InvoicesPage;
