import React, { useState, useMemo } from 'react';
import {
  REPORT_INVOICES_DATA,
  calculateReportData,
} from './mockData';
import { ReportPeriodFilter } from './components/ReportPeriodFilter';
import { ReportKpiCards } from './components/ReportKpiCards';
import { ReportRevenueBreakdown } from './components/ReportRevenueBreakdown';
import { ReportTopSellingTable } from './components/ReportTopSellingTable';

export function ReportsPage() {
  const [selectedPeriod, setSelectedPeriod] = useState('all');

  const reportData = useMemo(() => {
    return calculateReportData(REPORT_INVOICES_DATA, selectedPeriod);
  }, [selectedPeriod]);

  return (
    <div className="page" style={{ display: 'block' }}>
      {/* Period Filter Header */}
      <ReportPeriodFilter
        selectedPeriod={selectedPeriod}
        onSelectPeriod={setSelectedPeriod}
      />

      {/* KPI Cards */}
      <ReportKpiCards kpis={reportData.kpis} />

      {/* Revenue Breakdown */}
      <ReportRevenueBreakdown
        typeSegments={reportData.typeSegments}
        methodSegments={reportData.methodSegments}
      />

      {/* Top Selling Items Table */}
      <ReportTopSellingTable topItems={reportData.topItems} />
    </div>
  );
}

export default ReportsPage;
