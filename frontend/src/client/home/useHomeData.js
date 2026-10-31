import { useCallback, useEffect, useRef, useState } from 'react';
import { createDemoData } from './mockData.js';
import { isValidHomeData } from './homeModel.js';

export const STORAGE_KEY = 'tram.client.home.demo.v1';
function loadData() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return { db: createDemoData(), raw, warning: '' };
    const db = JSON.parse(raw);
    if (!isValidHomeData(db)) throw new Error('Invalid demo data');
    return { db, raw, warning: '' };
  } catch {
    return { db: createDemoData(), raw: null, warning: 'Không đọc được dữ liệu đã lưu. Đang hiển thị dữ liệu mẫu; hãy khôi phục bản mẫu trước khi thao tác.' };
  }
}

export function useHomeData() {
  const [state, setState] = useState(loadData);
  const current = useRef(state);
  const update = useCallback((value) => { current.current = value; setState(value); }, []);
  useEffect(() => {
    const listener = (event) => { if (event.key === STORAGE_KEY || event.key === null) update(loadData()); };
    window.addEventListener('storage', listener);
    return () => window.removeEventListener('storage', listener);
  }, [update]);
  const commit = useCallback((transform) => {
    if (current.current.warning) throw new Error(current.current.warning);
    let raw;
    try { raw = localStorage.getItem(STORAGE_KEY); } catch { throw new Error('Trình duyệt không cho phép lưu dữ liệu. Hãy bật quyền lưu trữ để thử lại.'); }
    if (raw !== current.current.raw) {
      update(loadData());
      throw new Error('Dữ liệu vừa thay đổi ở tab khác. Hãy kiểm tra và thử lại.');
    }
    const db = transform(current.current.db);
    const serialized = JSON.stringify(db);
    try { localStorage.setItem(STORAGE_KEY, serialized); } catch { throw new Error('Không thể lưu thay đổi. Kiểm tra dung lượng hoặc quyền lưu trữ của trình duyệt.'); }
    update({ db, raw: serialized, warning: '' });
    return db;
  }, [update]);
  const reset = useCallback(() => {
    const db = createDemoData();
    const raw = JSON.stringify(db);
    try { localStorage.setItem(STORAGE_KEY, raw); } catch { throw new Error('Không thể lưu dữ liệu mẫu vào trình duyệt.'); }
    update({ db, raw, warning: '' });
  }, [update]);
  return { db: state.db, warning: state.warning, commit, reset };
}
