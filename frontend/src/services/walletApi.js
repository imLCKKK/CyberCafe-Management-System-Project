// API hồ sơ/ví của KHÁCH đăng nhập (S1-M-1). customerId luôn lấy từ token ở server.
import { request } from './http';
import { toWalletTransaction } from './walletTypes';

export { ApiError } from './http';

export const getClientProfile = () => request('/api/client/profile');

export async function getClientWallet() {
  const data = await request('/api/client/wallet');
  return {
    walletBalance: Number(data.walletBalance),
    transactions: data.transactions.map(toWalletTransaction),
  };
}
