import { useCallback, useEffect, useState } from 'react';
import { getClientProfile } from '../../services/walletApi';

// status: 'loading' | 'ready' | 'error'. Lỗi thì chỉ hiện lỗi + thử lại, không hiện hồ sơ mẫu.
export function useClientProfile() {
  const [state, setState] = useState({ status: 'loading', profile: null, error: '' });
  const load = useCallback(async () => {
    try {
      setState({ status: 'ready', profile: await getClientProfile(), error: '' });
    } catch (err) {
      setState({ status: 'error', profile: null, error: err.message });
    }
  }, []);
  useEffect(() => { load(); }, [load]);
  return { ...state, reload: load };
}
