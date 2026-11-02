import React, { useId } from 'react';
import './riot-id-fields.css';

export function RiotIdFields({ value = '', onChange }) {
  const helpId = useId();
  const [gameName = '', tagLine = ''] = value.split('#');
  return <fieldset className="riot-id-fields" aria-describedby={helpId}>
    <legend>Riot ID</legend>
    <div className="riot-id-parts">
      <label>Tên trong game<input name="gameName" required maxLength={100} placeholder="Tên người chơi" value={gameName} autoComplete="off" onChange={(event) => {
        const text = event.target.value;
        onChange(text.includes('#') ? text : `${text}#${tagLine}`);
      }}/></label>
      <span className="riot-id-separator" aria-hidden="true">#</span>
      <label>Tag<input name="tagLine" required maxLength={20} placeholder="VN1 / 1234" value={tagLine} autoComplete="off" pattern="[^#\s]+" onChange={(event) => onChange(`${gameName}#${event.target.value.replace(/^#+/, '')}`)}/></label>
    </div>
    <small id={helpId}>Không cần nhập dấu #. Tag có thể là chữ hoặc số.</small>
  </fieldset>;
}

export function OpggPrivacyNote() {
  return <p className="opgg-privacy-note"><strong>Hồ sơ OP.GG cần ở chế độ công khai (Public).</strong> Nếu đang để riêng tư (Private), hãy mở hồ sơ của bạn trên OP.GG và chuyển sang Public để hệ thống truy vấn rank và thống kê.</p>;
}
