import React, { useEffect, useId, useRef } from 'react';
import { X } from 'lucide-react';

export default function HomeDialog({ title, onClose, children, wide = false }) {
  const ref = useRef(null);
  const titleId = useId();
  useEffect(() => {
    const dialog = ref.current;
    const previous = document.activeElement;
    dialog.showModal();
    return () => { dialog.close(); previous?.focus(); };
  }, []);
  return <dialog ref={ref} className={`home-dialog ${wide ? 'home-dialog-wide' : ''}`} aria-labelledby={titleId} onCancel={(event) => { event.preventDefault(); onClose(); }} onClick={(event) => { if (event.target === ref.current) { const box = ref.current.getBoundingClientRect(); if (event.clientX < box.left || event.clientX > box.right || event.clientY < box.top || event.clientY > box.bottom) onClose(); } }}>
    <div className="home-dialog-heading"><div><span className="home-eyebrow">TRAM / KHÔNG GIAN CHƠI</span><h2 id={titleId}>{title}</h2></div><button className="home-icon-button" onClick={onClose} aria-label="Đóng cửa sổ"><X size={20}/></button></div>
    <div className="home-dialog-body">{children}</div>
  </dialog>;
}
