import React from 'react';
import InvoiceWorkspace from '../../shared/invoices/InvoiceWorkspace';

export function InvoicesPage() {
  return <section className="invoices-page"><header className="home-page-heading"><h1>Hóa đơn của tôi</h1><p>Xem chi tiết giá, giảm, thu và hoàn tiền theo chứng từ đã phát hành.</p></header><InvoiceWorkspace/></section>;
}
