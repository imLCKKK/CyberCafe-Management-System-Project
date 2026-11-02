// Older browser demo records have no stock field. Keep their existing orders and
// derive a remaining quantity from the original demo stock until each product is updated.
const initialStock = { 1: 16, 2: 9, 3: 24, 4: 12, 5: 7, 6: 20, 7: 5, 8: 13, 9: 18 };

export function remainingStock(db, product) {
  if (Number.isSafeInteger(product.stock_quantity)) return Math.max(0, product.stock_quantity);
  const ordered = db.order_items
    .filter((item) => item.order_id > 1048 && item.product_id === product.id && item.status !== 'Cancelled')
    .reduce((sum, item) => sum + item.quantity, 0);
  return Math.max(0, (initialStock[product.id] ?? 0) - ordered);
}

export function pickWeightedProduct(db, categoryId = 'all', random = Math.random) {
  const available = db.products.filter((product) =>
    product.is_active !== false && (categoryId === 'all' || product.category_id === categoryId) && remainingStock(db, product) > 0
  );
  const total = available.reduce((sum, product) => sum + remainingStock(db, product), 0);
  if (!total) return null;
  const value = random();
  let ticket = Math.min(Math.max(Number.isFinite(value) ? value : 0, 0), 1 - Number.EPSILON) * total;
  for (const product of available) {
    ticket -= remainingStock(db, product);
    if (ticket < 0) return product;
  }
  return available.at(-1);
}
