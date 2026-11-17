import React from 'react';
import { AdminPage } from '../shared/AdminUI';
import InvoiceWorkspace from '../../shared/invoices/InvoiceWorkspace';

export function InvoicesPage() {
  return <AdminPage title="Hóa đơn" description="Phát hành và đối chiếu chứng từ món ăn, tiền giờ theo khoản đã ghi nhận." live actions={<span className="cafe-system">Hóa đơn đã phát hành</span>}><InvoiceWorkspace admin/></AdminPage>;
}
