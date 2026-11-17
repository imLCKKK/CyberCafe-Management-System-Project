import QRCode from 'qrcode';
import { paymentCanBePaid } from '../../shared/zaloPayPaymentApi.js';

export function createZaloPayPaymentQr(payment, enabled, now = Date.now()) {
  if (!paymentCanBePaid(payment, enabled, now)) return null;
  try {
    // Dynamic QR contains the exact provider order_url; never decode or rebuild its opaque query.
    const { modules } = QRCode.create(payment.paymentUrl, { errorCorrectionLevel: 'M' });
    const paths = [];
    for (let row = 0; row < modules.size; row++) {
      for (let column = 0; column < modules.size;) {
        if (!modules.get(row, column)) { column++; continue; }
        const start = column;
        while (column < modules.size && modules.get(row, column)) column++;
        const width = column - start;
        paths.push(`M${start + 4} ${row + 4}h${width}v1h-${width}z`);
      }
    }
    return { size: modules.size + 8, path: paths.join('') };
  } catch { return null; }
}
