import React, { useEffect, useRef, useState } from "react";
import {
  Check,
  Dices,
  PackageOpen,
  Settings2,
  ShoppingBag,
  Sparkles,
  Coins,
} from "lucide-react";
import { FoodArt, Modal } from "../../../shared/commerce/components";
import { money } from "../../../shared/commerce/model";
import {
  CASE_DURATION,
  CASE_START_INDEX,
  CASE_WINNER_INDEX,
  CASE_TYPES,
  CASE_RARITIES,
  casePosition,
  createFoodSpin,
  getFilteredCaseChoices,
} from "../foodCase";
import "../food-case.css";

export function FoodCaseButton({ disabled, onClick }) {
  return (
    <button
      type="button"
      className="cc-case-launcher"
      onClick={onClick}
      disabled={disabled}
      aria-haspopup="dialog"
      title={
        disabled
          ? "Chưa có món còn hàng để thêm vào giỏ"
          : "Chọn ngân sách và mở hòm món ngon"
      }
    >
      <span className="cc-case-launcher-icon">
        <PackageOpen size={24} />
      </span>
      <span>
        <small>MỞ HÒM CHỌN MÓN</small>
        <strong>Ăn gì cũm được</strong>
      </span>
      <Sparkles size={17} />
    </button>
  );
}

function CaseCard({ product, winner = false }) {
  return (
    <div
      className={`cc-case-item cc-rarity-${product.Rarity.key} ${winner ? "is-winner" : ""}`}
    >
      <span className="cc-case-item-category">{product.Rarity.label}</span>
      {product.FoodType === "combo" ? (
        <div className="cc-case-combo-art" aria-hidden="true">
          🍱<span>🥤</span>
        </div>
      ) : (
        <FoodArt product={product} />
      )}
      <strong>{product.ProductName}</strong>
      <small>{money(product.Price)}</small>
    </div>
  );
}

export default function FoodCase({
  choices,
  categories,
  onResolve,
  onClose,
  onViewCart,
}) {
  const [filters, setFilters] = useState({ type: "all", maxPrice: 35000 });
  const [spin, setSpin] = useState(null);
  const [result, setResult] = useState(null);
  const trackRef = useRef(null),
    progressRef = useRef(null),
    frameRef = useRef(null);
  const runningRef = useRef(false);
  const resolveRef = useRef(onResolve);
  resolveRef.current = onResolve;
  const candidates = getFilteredCaseChoices(choices, categories, filters);
  const spinning = Boolean(spin && !result);

  useEffect(() => {
    if (!spin) return;
    const reducedMotion = window.matchMedia(
      "(prefers-reduced-motion: reduce)",
    ).matches;
    const duration = reducedMotion ? 200 : CASE_DURATION;
    let started,
      finished = false;
    function animate(now) {
      if (finished) return;
      if (started === undefined) started = now;
      const progress = Math.min((now - started) / duration, 1);
      trackRef.current?.style.setProperty(
        "--case-position",
        reducedMotion ? CASE_WINNER_INDEX : casePosition(progress),
      );
      if (progressRef.current)
        progressRef.current.style.transform = `scaleX(${progress})`;
      if (progress < 1) frameRef.current = requestAnimationFrame(animate);
      else {
        finished = true;
        runningRef.current = false;
        setResult(resolveRef.current(spin.winner, spin.filters));
      }
    }
    frameRef.current = requestAnimationFrame(animate);
    return () => {
      finished = true;
      cancelAnimationFrame(frameRef.current);
    };
  }, [spin]);

  function closeCase() {
    cancelAnimationFrame(frameRef.current);
    onClose();
  }
  function configure(key, value) {
    if (runningRef.current) return;
    setFilters((current) => ({ ...current, [key]: value }));
    setSpin(null);
    setResult(null);
  }
  function startSpin() {
    if (runningRef.current || !candidates.length) return;
    runningRef.current = true;
    setResult(null);
    setSpin({ ...createFoodSpin(candidates), filters: { ...filters } });
  }
  function resetSpin() {
    setSpin(null);
    setResult(null);
  }

  return (
    <Modal title="Ăn gì cũm được" onClose={closeCase} wide>
      <div
        className={`cc-food-case ${spinning ? "is-spinning" : result ? "is-revealed" : "is-configuring"}`}
      >
        <div className="cc-case-intro">
          <span>
            <Sparkles size={14} />
            HÒM MÓN NGON · GU CỦA BẠN
          </span>
          <h3>
            {result
              ? result.ok
                ? "Chốt món này nha!"
                : "Thực đơn vừa thay đổi"
              : spinning
                ? "Món ngon đang tới…"
                : "Chọn gu. Chọn giá. Mở hòm."}
          </h3>
          <p>
            Mở hòm miễn phí. Món trúng tính theo giá hiển thị và được thêm vào
            giỏ.
          </p>
        </div>
        <div className="cc-case-config">
          <label className="cc-case-budget">
            <span>
              <Coins size={16} />
              Ngân sách tối đa
            </span>
            <select
              aria-label="Ngân sách mở hòm"
              value={filters.maxPrice}
              disabled={spinning}
              onChange={(event) => configure("maxPrice", event.target.value)}
            >
              {[20000, 35000, 50000, 75000].map((value) => (
                <option key={value} value={value}>
                  {money(value)}
                </option>
              ))}
              <option value="all">Không giới hạn</option>
            </select>
            <small>Áp dụng cho cả tổng giá combo.</small>
          </label>
          <fieldset className="cc-case-type" disabled={spinning}>
            <legend>Bạn đang thèm gì?</legend>
            <div>
              {CASE_TYPES.map((type) => (
                <button
                  type="button"
                  key={type.value}
                  aria-pressed={filters.type === type.value}
                  className={filters.type === type.value ? "active" : ""}
                  onClick={() => configure("type", type.value)}
                >
                  <span aria-hidden="true">{type.emoji}</span>
                  {type.label}
                </button>
              ))}
            </div>
          </fieldset>
        </div>
        <div className="cc-case-legend" aria-label="Phân hạng món theo giá">
          {CASE_RARITIES.map((rarity) => (
            <span
              key={rarity.key}
              className={`cc-rarity-${rarity.key}`}
              title={rarity.description}
            >
              <i />
              {rarity.label}
            </span>
          ))}
          <small>Hạng theo giá món / combo</small>
        </div>
        {spin ? (
          <>
            <div className="cc-case-reel" aria-hidden="true">
              <div className="cc-case-pointer" />
              <div className="cc-case-window">
                <div
                  ref={trackRef}
                  className="cc-case-track"
                  style={{ "--case-position": CASE_START_INDEX }}
                >
                  {spin.items.map((product, index) => (
                    <CaseCard
                      key={index}
                      product={product}
                      winner={Boolean(result && index === CASE_WINNER_INDEX)}
                    />
                  ))}
                </div>
              </div>
              <div className="cc-case-marker" />
            </div>
            <div className="cc-case-progress" aria-hidden="true">
              <div ref={progressRef} />
            </div>
          </>
        ) : (
          <div className="cc-case-preview">
            <div className="cc-case-preview-heading">
              <span>
                <PackageOpen size={16} />
                Có thể trúng trong hòm
              </span>
              <strong>{candidates.length} lựa chọn</strong>
            </div>
            {candidates.length ? (
              <div className="cc-case-preview-cards">
                {candidates.map((product) => (
                  <CaseCard key={product.CaseId} product={product} />
                ))}
              </div>
            ) : (
              <div className="cc-case-empty">
                <PackageOpen size={32} />
                <strong>Chưa có món trong tầm giá này</strong>
                <p>Thử tăng ngân sách hoặc đổi loại món nhé.</p>
              </div>
            )}
          </div>
        )}
        <div
          className="cc-case-result"
          role="status"
          aria-live="polite"
          aria-atomic="true"
        >
          {spinning ? (
            <>
              <span className="cc-case-rolling">
                <Dices size={16} />
                Đang chọn món cho bạn…
              </span>
              <p>Món dừng ở vạch giữa sẽ vào giỏ hàng.</p>
            </>
          ) : result ? (
            <>
              <span
                className={`cc-case-result-icon ${result.ok ? `cc-rarity-${result.product.Rarity.key}` : "is-unavailable"}`}
              >
                {result.ok ? <Check size={22} /> : <PackageOpen size={22} />}
              </span>
              <h4>{result.product?.ProductName || spin.winner.ProductName}</h4>
              {result.ok && (
                <span
                  className={`cc-case-result-rarity cc-rarity-${result.product.Rarity.key}`}
                >
                  {result.product.Rarity.label}
                </span>
              )}
              <p>
                {result.ok
                  ? `${result.product.FoodType === "combo" ? "Đã thêm món chính và nước vào giỏ" : "Đã thêm 1 phần vào giỏ"} · ${money(result.product.Price)}`
                  : result.message}
              </p>
            </>
          ) : (
            <p>
              {filters.type === "combo"
                ? "Combo gồm 1 món chính + 1 nước. Trúng combo sẽ thêm cả hai vào giỏ."
                : "Chọn mức giá và loại món yêu thích, rồi bấm Mở hòm để bắt đầu."}
            </p>
          )}
        </div>
        <div className="cc-case-actions">
          {spinning ? (
            <button type="button" className="cc-button" onClick={closeCase}>
              Đóng & dừng quay
            </button>
          ) : result ? (
            <>
              <button type="button" className="cc-button" onClick={resetSpin}>
                <Settings2 size={16} />
                Chọn lại hòm
              </button>
              <button
                type="button"
                className="cc-button cc-gold"
                onClick={onViewCart}
              >
                <ShoppingBag size={16} />
                Xem giỏ hàng
              </button>
            </>
          ) : (
            <button
              type="button"
              className="cc-button cc-gold cc-case-start"
              onClick={startSpin}
              disabled={!candidates.length}
            >
              <PackageOpen size={18} />
              Mở hòm
              <Sparkles size={16} />
            </button>
          )}
        </div>
        <p className="cc-case-note">
          {spinning
            ? "Đóng khi đang quay sẽ không thêm món vào giỏ."
            : "Mỗi lựa chọn có cơ hội như nhau. Chỉ gửi đơn khi bạn bấm Đặt món."}
        </p>
      </div>
    </Modal>
  );
}
