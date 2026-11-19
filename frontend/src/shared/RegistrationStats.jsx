import React, { useEffect, useRef, useState } from 'react';
import { refreshRegistrationStats } from './tournamentApi';
import { dateTime } from '../client/home/homeModel';

export function RegistrationStats({ audience, tournamentId, registrationId, snapshot, canRefresh, onSaved }) {
  const request = useRef(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  useEffect(() => () => request.current?.abort(), []);
  const stats = snapshot?.stats;

  async function refresh() {
    if (request.current) return;
    const controller = new AbortController();
    request.current = controller;
    setBusy(true); setError('');
    try {
      const saved = await refreshRegistrationStats(audience, tournamentId, registrationId, controller.signal);
      if (!controller.signal.aborted) onSaved(saved);
    } catch (cause) {
      if (!controller.signal.aborted) setError(cause.message);
    } finally {
      request.current = null;
      if (!controller.signal.aborted) setBusy(false);
    }
  }

  return <div>
    {stats ? <>
      <small>OP.GG · {stats.scope} · {stats.matches == null ? 'Chưa có thống kê trận' : `${stats.matches} trận`}</small>
      <small>Lấy dữ liệu: {dateTime(stats.fetchedAt)}</small>
      <small>OP.GG cập nhật: {stats.sourceUpdatedAt ? dateTime(stats.sourceUpdatedAt) : 'Không rõ thời điểm'}</small>
    </> : <small>Chưa đồng bộ OP.GG</small>}
    {canRefresh && <button className={audience === 'admin' ? 'cafe-button' : 'tour-button'} type="button"
      disabled={busy} onClick={refresh}>{busy ? 'Đang đồng bộ…' : 'Đồng bộ OP.GG'}</button>}
    {error && <small role="alert">{stats ? 'Đang hiển thị bản đã lưu. ' : ''}{error}</small>}
  </div>;
}
