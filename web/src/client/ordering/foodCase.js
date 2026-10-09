import { fold } from "../../shared/commerce/model.js";

export const CASE_TYPES = [
  { value: "all", label: "Tất cả", emoji: "🍽️" },
  { value: "noodles", label: "Mì", emoji: "🍜" },
  { value: "rice", label: "Cơm", emoji: "🍚" },
  { value: "drink", label: "Nước", emoji: "🥤" },
  { value: "combo", label: "Combo", emoji: "🍱" },
];
export const CASE_RARITIES = [
  { key: "common", label: "Thường", description: "Đến 20.000đ" },
  { key: "vip", label: "Vjp", description: "Trên 20.000đ đến 30.000đ" },
  { key: "epic", label: "Đỉnh cao", description: "Trên 30.000đ đến 45.000đ" },
  { key: "legendary", label: "Thượng hạng", description: "Trên 45.000đ" },
];
export function getCaseRarity(price) {
  return CASE_RARITIES[
    price <= 20000 ? 0 : price <= 30000 ? 1 : price <= 45000 ? 2 : 3
  ];
}
function getFoodType(product, categories) {
  const name = fold(product.ProductName);
  const category = fold(
    categories.find((item) => item.CategoryId === product.CategoryId)
      ?.CategoryName || "",
  );
  if (/nuoc|do uong|giai khat/.test(category)) return "drink";
  if (/\bmi\b/.test(name)) return "noodles";
  if (/\bcom\b/.test(name)) return "rice";
  return "other";
}

// Every eligible entry has the same chance; stock is checked again on reveal.
export function getCaseChoices(products, categories, cart) {
  return products.filter((product) => {
    const quantity =
      cart.find((line) => line.ProductId === product.ProductId)?.Quantity || 0;
    return (
      product.IsActive &&
      product.Stock > quantity &&
      categories.some(
        (category) =>
          category.CategoryId === product.CategoryId && category.IsActive,
      )
    );
  });
}

export function getFilteredCaseChoices(
  products,
  categories,
  { type = "all", maxPrice = 35000 } = {},
) {
  const singles = products.map((product) => ({
    ...product,
    CaseId: `product-${product.ProductId}`,
    FoodType: getFoodType(product, categories),
    Lines: [{ ProductId: product.ProductId, Quantity: 1 }],
  }));
  const meals = singles.filter((item) =>
    ["noodles", "rice"].includes(item.FoodType),
  );
  const drinks = singles.filter((item) => item.FoodType === "drink");
  const combos = meals.flatMap((meal) =>
    drinks.map((drink) => ({
      ...meal,
      CaseId: `combo-${meal.ProductId}-${drink.ProductId}`,
      ProductName: `${meal.ProductName} + ${drink.ProductName}`,
      Price: meal.Price + drink.Price,
      FoodType: "combo",
      Lines: [...meal.Lines, ...drink.Lines],
    })),
  );
  return [...singles, ...combos]
    .filter(
      (item) =>
        (type === "all" || item.FoodType === type) &&
        (maxPrice === "all" || item.Price <= Number(maxPrice)),
    )
    .map((item) => ({ ...item, Rarity: getCaseRarity(item.Price) }));
}

export function resolveCaseSelection(
  products,
  categories,
  cart,
  selected,
  filters,
) {
  const current = getFilteredCaseChoices(
    getCaseChoices(products, categories, cart),
    categories,
    filters,
  ).find((item) => item.CaseId === selected.CaseId);
  if (!current || current.Price !== selected.Price)
    return {
      ok: false,
      message:
        "Món vừa đổi giá, hết hàng hoặc đã đủ số lượng trong giỏ. Chọn lại hòm để quay nhé!",
    };
  const nextCart = cart.map((line) => ({ ...line }));
  current.Lines.forEach((line) => {
    const existing = nextCart.find((item) => item.ProductId === line.ProductId);
    if (existing) existing.Quantity += line.Quantity;
    else nextCart.push({ ...line });
  });
  return { ok: true, product: current, cart: nextCart };
}

export const CASE_START_INDEX = 3;
export const CASE_WINNER_INDEX = 40;
export const CASE_DURATION = 5800;

export function createFoodSpin(choices, random = Math.random) {
  if (!choices.length) return null;
  const pick = () => choices[Math.floor(random() * choices.length)];
  const winner = pick();
  const items = Array.from({ length: CASE_WINNER_INDEX + 6 }, pick);
  items[CASE_WINNER_INDEX] = winner;
  return { winner, items };
}

export function casePosition(progress) {
  const clamped = Math.min(1, Math.max(0, progress));
  const eased = 1 - (1 - clamped) ** 5;
  return CASE_START_INDEX + (CASE_WINNER_INDEX - CASE_START_INDEX) * eased;
}
