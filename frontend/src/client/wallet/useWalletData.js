import { useCallback, useEffect, useState } from 'react';
import { getClientProfile, getClientWallet } from '../../services/walletApi';

// status: 'loading' | 'ready' | 'error'. Khi lỗi, trang chỉ hiện lỗi + nút thử lại, không hiện số cũ hay số mẫu.
export function useWalletData() {
  const [state, setState] = useState({ status: 'loading', profile: null, wallet: null, error: '' });
  const load = useCallback(async () => {
    try {
      const [profile, wallet] = await Promise.all([getClientProfile(), getClientWallet()]);
      setState({ status: 'ready', profile, wallet, error: '' });
    } catch (err) {
      setState({ status: 'error', profile: null, wallet: null, error: err.message });
    }
  }, []);
  useEffect(() => { load(); }, [load]);
  return { ...state, reload: load };
}
