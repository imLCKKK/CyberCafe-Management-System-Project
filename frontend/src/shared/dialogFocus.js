// Keep Tab inside the modal, including browsers that otherwise reach browser chrome.
export function trapDialogFocus(event) {
  if (event.key !== 'Tab' || event.defaultPrevented) return;
  const dialog = event.currentTarget;
  const controls = [...dialog.querySelectorAll(
    'button:not([disabled]), a[href], input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), summary, [tabindex]:not([tabindex="-1"])'
  )].filter(element => element.tabIndex >= 0 && !element.matches(':disabled') && element.getClientRects().length && !element.closest('[hidden], [inert]'));
  const first = controls[0];
  const last = controls.at(-1);
  const active = dialog.ownerDocument.activeElement;
  if (!first) {
    event.preventDefault();
    dialog.focus();
  } else if (!dialog.contains(active) || active === dialog || (event.shiftKey ? active === first : active === last)) {
    event.preventDefault();
    (event.shiftKey ? last : first).focus();
  }
}
