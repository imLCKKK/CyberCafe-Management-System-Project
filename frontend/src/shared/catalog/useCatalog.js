import { useCallback, useEffect, useState } from 'react';
import { getCatalog } from './catalogApi';

const empty = { products: [], product_categories: [], order_items: [] };

export function useCatalog(admin = false) {
  const [db, setDb] = useState(empty);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  const reload = useCallback(() => setRevision(value => value + 1), []);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError('');
    setDb(empty);
    getCatalog(admin, controller.signal)
      .then(data => { if (!controller.signal.aborted) setDb(data); })
      .catch(reason => { if (!controller.signal.aborted) setError(reason.message || 'Không tải được thực đơn. Vui lòng thử lại.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [admin, revision]);

  useEffect(() => {
    window.addEventListener('focus', reload);
    return () => window.removeEventListener('focus', reload);
  }, [reload]);

  return { db, loading, error, reload };
}
