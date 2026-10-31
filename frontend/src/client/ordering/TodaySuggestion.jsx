import React, { useLayoutEffect, useRef, useState } from 'react';
import { ArrowLeft, ArrowUpRight, Check, Dices, RotateCw, Sparkles } from 'lucide-react';
import { productArt } from '../shared/demoData';
import { money } from '../shared/demoModel';
import { pickWeightedProduct, remainingStock } from './recommendation';
import SuggestionResultDialog from './SuggestionResultDialog';

const SPIN_DURATION = 4200;
const WINNER_INDEX = 27;

export function TodaySuggestionTeaser({ db, onOpen }) {
  const available = db.products.filter((product) => product.is_active !== false && remainingStock(db, product) > 0);
  const showcase = [1, 3, 5].map((id) => available.find((product) => product.id === id)).filter(Boolean);
  return <button type="button" className="today-teaser" onClick={onOpen} aria-label="Hôm nay ăn gì? Khám phá vòng quay gợi ý món">
    <span className="today-teaser-copy"><span className="today-eyebrow"><Sparkles size={13}/> CHƯA BIẾT CHỌN MÓN NÀO?</span><span className="today-teaser-title">Hôm nay <em>ăn gì?</em></span><span className="today-teaser-description">Một vòng quay, một món ngon.<br/>Để TRAM chọn giúp bạn nhé!</span><span className="today-teaser-cta">Khám phá ngay <ArrowUpRight size={18}/></span></span>
    <span className="today-teaser-art" aria-hidden="true"><span className="today-teaser-orbit"/>{showcase.map((product, index) => <span className={`today-teaser-food today-teaser-food-${index}`} key={product.id}><span>{productArt[product.id] || '🍽️'}</span><small>{product.name}</small></span>)}<span className="today-teaser-dice"><Dices size={23}/></span><span className="today-teaser-hint"><Sparkles size={11}/> Khám phá món mới</span></span>
  </button>;
}

export default function TodaySuggestion({ db, category, onCategoryChange, cart, onAdd, onBack, onViewOrders }) {
  const [sequence, setSequence] = useState([]);
  const [winnerId, setWinnerId] = useState(null);
  const [phase, setPhase] = useState('ready');
  const [resultOpen, setResultOpen] = useState(false);
  const viewportRef = useRef(null);
  const trackRef = useRef(null);
  const frameRef = useRef(null);
  const available = db.products.filter((product) =>
    product.is_active !== false && (category === 'all' || product.category_id === category) && remainingStock(db, product) > 0
  );
  const winner = phase === 'done' ? db.products.find((product) => product.id === winnerId) : null;
  const visible = sequence.length ? sequence : available;

  useLayoutEffect(() => {
    setSequence([]);
    setWinnerId(null);
    setPhase('ready');
    setResultOpen(false);
    if (trackRef.current) {
      trackRef.current.style.transition = 'none';
      trackRef.current.style.transform = 'translateX(0px)';
    }
  }, [category]);

  useLayoutEffect(() => {
    const viewport = viewportRef.current;
    const track = trackRef.current;
    const winningCard = track?.children[sequence.length === 1 ? 0 : WINNER_INDEX];
    if (!viewport || !winningCard) return undefined;
    const alignWinner = () => {
      const distance = winningCard.offsetLeft + winningCard.offsetWidth / 2 - viewport.clientWidth / 2;
      track.style.transform = `translateX(${-distance}px)`;
    };
    if (phase === 'done') {
      track.style.transition = 'none';
      alignWinner();
      const observer = new ResizeObserver(alignWinner);
      observer.observe(viewport);
      return () => observer.disconnect();
    }
    if (phase !== 'spinning' || !sequence.length) return undefined;
    track.style.transition = 'none';
    track.style.transform = 'translateX(0px)';
    // Let the reset paint before starting the long, gradually slowing slide.
    frameRef.current = requestAnimationFrame(() => {
      frameRef.current = requestAnimationFrame(() => {
        track.style.transition = `transform ${SPIN_DURATION}ms cubic-bezier(.12,.72,.12,1)`;
        alignWinner();
      });
    });
    return () => cancelAnimationFrame(frameRef.current);
  }, [phase, sequence]);

  function spin() {
    if (!available.length || phase === 'spinning') return;
    const selected = pickWeightedProduct(db, category);
    if (!selected) return;
    setWinnerId(selected.id);
    setResultOpen(false);
    if (window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) {
      setSequence([selected]);
      setPhase('done');
      setResultOpen(true);
      return;
    }
    setSequence([
      ...Array.from({ length: WINNER_INDEX }, (_, index) => available[index % available.length]),
      selected,
      ...Array.from({ length: 5 }, (_, index) => available[(index + 2) % available.length]),
    ]);
    setPhase('spinning');
  }

  return <section className="today-experience home-panel" aria-label="Vòng quay hôm nay ăn gì">
    <header className="today-experience-header"><div><span className="today-eyebrow"><Sparkles size={13}/> MỘT VÒNG QUAY · MỘT MÓN NGON</span><h2>Hôm nay <span>ăn gì?</span></h2><p>Chọn khẩu vị, quay một vòng và tìm món ngon tiếp sức.</p></div><Dices className="today-experience-mark" size={60} strokeWidth={1.2} aria-hidden="true"/></header>
    <div className="today-experience-body">
    <div className="client-category-tabs today-category-tabs" role="group" aria-label="Nhóm món gợi ý">{[{ id: 'all', name: 'Tất cả' }, ...db.product_categories].map((item) => <button type="button" key={item.id} disabled={phase === 'spinning'} className={category === item.id ? 'selected' : ''} aria-pressed={category === item.id} onClick={() => onCategoryChange(item.id)}>{item.name}</button>)}</div>
    <div className="today-machine-heading"><span><i/>{phase === 'spinning' ? 'Đang tìm món ngon…' : 'Sẵn sàng khám phá'}</span><span>{available.length} món còn hàng</span></div>
    <div className={`today-wheel ${phase === 'spinning' ? 'spinning' : ''}`} ref={viewportRef} aria-hidden="true">
      <div className="today-wheel-track" ref={trackRef} onTransitionEnd={(event) => { if (phase === 'spinning' && event.target === trackRef.current && event.propertyName === 'transform') { setPhase('done'); setResultOpen(true); } }}>
        {visible.map((product, index) => <div className={`today-wheel-card ${winner && index === (sequence.length === 1 ? 0 : WINNER_INDEX) ? 'is-winner' : ''}`} key={`${product.id}-${index}`}><span className="today-wheel-art">{productArt[product.id] || '🍽️'}</span><span>{product.name}</span><small>{money(product.current_price)}</small></div>)}
      </div>
      <div className="today-wheel-focus"/>
    </div>
    <div className={`today-outcome ${winner ? 'has-winner' : ''}`} role="status" aria-live="polite">
      {winner ? <><span className="today-outcome-label"><Check size={14}/> MÓN NGON DÀNH CHO BẠN</span><strong>{winner.name}</strong><span>{money(winner.current_price)} · Còn {remainingStock(db, winner)} phần</span></> : <><strong>{phase === 'spinning' ? 'Món nào sẽ được chọn đây?' : available.length ? 'Món ngon tiếp theo của bạn là…' : 'Nhóm món này đã hết hàng'}</strong><span>{phase === 'spinning' ? 'Chờ một chút, vòng quay đang chậm dần.' : available.length ? 'Bấm quay và để TRAM tạo một bất ngờ nho nhỏ.' : 'Hãy chọn nhóm khác để khám phá món còn hàng.'}</span></>}
    </div>
    <div className="today-experience-actions"><button className="home-button primary" type="button" disabled={!available.length || phase === 'spinning'} onClick={spin}>{phase === 'done' ? <RotateCw size={18}/> : <Dices size={18}/>} {phase === 'spinning' ? 'Đang quay…' : phase === 'done' ? 'Thử món khác' : 'Quay chọn món ngay'}</button>{winner && <button className="home-button" type="button" onClick={() => setResultOpen(true)}>Xem món & thanh toán <ArrowUpRight size={17}/></button>}</div>
    <p className="today-stock-note">Món còn nhiều có cơ hội được chọn cao hơn. Bạn luôn có thể quay lại để thử món khác.</p>
    <button type="button" className="home-text-button today-back" onClick={onBack}><ArrowLeft size={14}/> Về thực đơn</button>
    </div>
    {resultOpen && winner && <SuggestionResultDialog productId={winner.id} cart={cart} onAdd={onAdd} onClose={() => setResultOpen(false)} onViewOrders={onViewOrders}/>}
  </section>;
}
