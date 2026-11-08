// Quantities are provisional. Checkout must validate price and stock again on the server.
export function reconcileCart(db, cart) {
  const result = {};
  for (const [id, quantity] of Object.entries(cart)) {
    const product = db.products.find(p => p.id === Number(id) && p.is_active
      && db.product_categories.some(c => c.id === p.category_id && c.is_active));
    if (!product || !Number.isSafeInteger(quantity)) continue;
    const amount = Math.min(quantity, 99, product.stock_quantity);
    if (amount > 0) result[id] = amount;
  }
  return result;
}
