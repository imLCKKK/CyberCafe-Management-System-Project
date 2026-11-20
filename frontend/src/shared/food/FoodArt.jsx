import React, { useState } from 'react';
import { ImageOff } from 'lucide-react';
import foodImageSources from '../catalog/foodImageSources.json';
import './FoodArt.css';

function FoodPhoto({ src, name, small, eager }) {
  const [failed, setFailed] = useState(false);
  if (!src || failed) {
    return <span className="food-art-placeholder" role="img" aria-label={`${name}: chưa có ảnh`}>
      <ImageOff size={small ? 18 : 30} aria-hidden="true"/>
      {!small && <span>Chưa có ảnh</span>}
    </span>;
  }
  const thumbnail = foodImageSources[src];
  return <img src={src} srcSet={thumbnail ? `${thumbnail} 320w, ${src} 640w` : undefined}
    sizes={thumbnail ? (small ? '48px' : eager ? '270px' : '(max-width: 480px) 98px, 320px') : undefined}
    alt={`Ảnh minh họa ${name}`} width={small ? 96 : 640} height={small ? 96 : 640}
    loading={eager ? 'eager' : 'lazy'} decoding="async" onError={() => setFailed(true)}/>;
}

export function FoodArt({ product, small = false, eager = false, inline = false }) {
  const src = typeof product?.image_url === 'string' ? product.image_url.trim() : '';
  const name = product?.name || 'Món ăn';
  const Container = inline ? 'span' : 'div';
  // A changed URL gets fresh load/error state. A failed URL never retries itself.
  return <Container className={`home-food-art food-photo${small ? ' food-art-small' : ''}`}>
    <FoodPhoto key={src} src={src} name={name} small={small} eager={eager}/>
  </Container>;
}
