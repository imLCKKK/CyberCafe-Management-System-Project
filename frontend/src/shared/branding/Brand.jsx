import React from 'react';
import './brand.css';

export default function Brand({ area, compact = false, className = '' }) {
  return (
    <span className={`nexora-brand${compact ? ' nexora-brand--compact' : ''}${className ? ` ${className}` : ''}`}>
      <span className="nexora-brand__mark"><img src="/branding/nexora-mark.svg" width="40" height="44" alt=""/></span>
      <span className="nexora-brand__copy">
        <strong className="nexora-brand__name">NEXORA CYBER</strong>
        {area && <small className="nexora-brand__area">{area}</small>}
      </span>
    </span>
  );
}
