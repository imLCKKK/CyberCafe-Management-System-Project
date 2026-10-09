import test from "node:test";
import assert from "node:assert/strict";
import {
  createSeed,
  placeOrder,
  changeOrderStatus,
  saveProduct,
  fold,
} from "./model.js";

test("placing an order links session, snapshots prices and deducts stock atomically", () => {
  const initial = createSeed();
  const result = placeOrder(
    initial,
    [
      { ProductId: 1, Quantity: 2 },
      { ProductId: 3, Quantity: 1 },
    ],
    1,
    1,
  );
  const order = result.orders[0];
  assert.equal(order.CustomerId, 1);
  assert.equal(order.SessionId, 1);
  assert.equal(order.Status, "Pending");
  assert.equal(order.TotalAmount, 71000);
  assert.equal(order.InvoiceId, null);
  assert.equal(result.products.find((item) => item.ProductId === 1).Stock, 22);
  assert.equal(initial.products.find((item) => item.ProductId === 1).Stock, 24);
  assert.equal(
    result.orderDetails.filter((item) => item.OrderId === order.OrderId).length,
    2,
  );
  assert.equal(
    result.inventoryTransactions.filter(
      (item) => item.ReferenceId === order.OrderId,
    ).length,
    2,
  );
  assert.equal(result.orderStatusHistory.at(-1).NewStatus, "Pending");
});

test("a failure on a later cart line does not partially change stock or orders", () => {
  const initial = createSeed(),
    snapshot = structuredClone(initial);
  assert.throws(
    () =>
      placeOrder(
        initial,
        [
          { ProductId: 1, Quantity: 2 },
          { ProductId: 10, Quantity: 1 },
        ],
        1,
        1,
      ),
    /chỉ còn 0/,
  );
  assert.deepEqual(initial, snapshot);
});

test("rejects empty carts, inactive products, overselling, duplicate lines and invalid quantities", () => {
  const initial = createSeed();
  for (const cart of [
    [],
    [{ ProductId: 11, Quantity: 1 }],
    [{ ProductId: 1, Quantity: 25 }],
    [{ ProductId: 1, Quantity: -1 }],
    [{ ProductId: 1, Quantity: 1.5 }],
    [{ ProductId: 1, Quantity: 0 }],
    [{ ProductId: 999, Quantity: 1 }],
    [
      { ProductId: 1, Quantity: 1 },
      { ProductId: 1, Quantity: 1 },
    ],
  ]) {
    assert.throws(() => placeOrder(initial, cart, 1, 1));
  }
});

test("requires an active gaming session owned by the customer", () => {
  const initial = createSeed(),
    cart = [{ ProductId: 1, Quantity: 1 }];
  assert.throws(() => placeOrder(initial, cart, 2, 1), /phiên chơi/);
  initial.gamingSessions[0].Status = "Closed";
  assert.throws(() => placeOrder(initial, cart, 1, 1), /phiên chơi/);
});

test("cannot order from an inactive category", () => {
  const initial = createSeed();
  initial.categories[0].IsActive = false;
  assert.throws(
    () => placeOrder(initial, [{ ProductId: 1, Quantity: 1 }], 1, 1),
    /ngừng bán/,
  );
});

test("cancelling returns stock once and records status / inventory history", () => {
  const initial = createSeed();
  const placed = placeOrder(initial, [{ ProductId: 1, Quantity: 2 }], 1, 1);
  const id = placed.orders[0].OrderId;
  const prepared = changeOrderStatus(placed, id, "Preparing");
  const cancelled = changeOrderStatus(prepared, id, "Cancelled");
  assert.equal(cancelled.products[0].Stock, initial.products[0].Stock);
  assert.equal(cancelled.orderStatusHistory.at(-1).OldStatus, "Preparing");
  assert.equal(cancelled.inventoryTransactions.at(-1).Quantity, 2);
  assert.throws(() => changeOrderStatus(cancelled, id, "Cancelled"));
  assert.throws(() => changeOrderStatus(cancelled, id, "Preparing"));
});

test("status progression prevents skips, duplicate transitions and cancellation after delivery", () => {
  let data = createSeed();
  assert.throws(() => changeOrderStatus(data, 1048, "Delivered"));
  for (const status of ["Preparing", "Ready", "Delivered"]) {
    data = changeOrderStatus(data, 1048, status);
    assert.throws(() => changeOrderStatus(data, 1048, status));
  }
  assert.throws(() => changeOrderStatus(data, 1048, "Cancelled"));
  assert.throws(() => changeOrderStatus(data, 1048, undefined));
  assert.equal(data.orders.find((item) => item.OrderId === 1048).EmployeeId, 1);
});

test("price edits do not change existing order detail prices or totals", () => {
  const initial = placeOrder(
    createSeed(),
    [{ ProductId: 1, Quantity: 1 }],
    1,
    1,
  );
  const id = initial.orders[0].OrderId;
  const updated = saveProduct(initial, {
    ...initial.products[0],
    Price: 45000,
  });
  assert.equal(updated.orders[0].TotalAmount, 28000);
  assert.equal(
    updated.orderDetails.find((item) => item.OrderId === id).UnitPrice,
    28000,
  );
  assert.equal(updated.products[0].Price, 45000);
});

test("product form validates database constraints and records stock adjustments", () => {
  const initial = createSeed(),
    product = initial.products[0];
  for (const invalid of [
    { ProductName: " " },
    { Price: -1 },
    { Price: Infinity },
    { Price: 10000000000 },
    { Stock: -1 },
    { Stock: "" },
    { Stock: 0.5 },
    { CategoryId: 999 },
  ]) {
    assert.throws(() => saveProduct(initial, { ...product, ...invalid }));
  }
  const added = saveProduct(initial, {
    ProductName: "  Trà tắc  ",
    CategoryId: 3,
    Price: 15000,
    Stock: 10,
    IsActive: true,
  });
  assert.equal(added.products.at(-1).ProductName, "Trà tắc");
  assert.equal(added.products.at(-1).ProductId, 13);
  assert.equal(added.inventoryTransactions.at(-1).Type, "Adjust");
  assert.equal(added.inventoryTransactions.at(-1).Quantity, 10);
});

test("Vietnamese search supports accent-free input", () => {
  assert.equal(fold("Đồ uống"), "do uong");
  assert.ok(fold("Mì xào bò").includes(fold("mi xao")));
});
