using CyberManagement.Application.DTOs;
using CyberManagement.Application.Interfaces.Repositories;
using CyberManagement.Application.Interfaces.Services;
using CyberManagement.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IProductRepository _productRepository;
        private readonly IInvoiceRepository _invoiceRepository;
        public OrderService(IOrderRepository orderRepository, IProductRepository productRepository, IInvoiceRepository invoiceRepository)
        {
            _orderRepository = orderRepository;
            _productRepository = productRepository;
            _invoiceRepository = invoiceRepository;
        }

        private OrderDTOs MapToDTOs(Order order)
        {
            return new OrderDTOs
            {
                OrderId = order.OrderId,
                CustomerId = order.CustomerId,
                OrderTime = order.OrderTime,
                Status = order.Status,
                Items = order.OrderDetails.Select(od => new OrderDetailsDTOs
                {
                    ProductId = od.ProductId,
                    ProductName = od.Product.ProductName,
                    Quantity = od.Quantity,
                    UnitPrice = od.UnitPrice,
                    SubTotal = od.Quantity * od.UnitPrice
                }).ToList(),
                TotalAmount = order.OrderDetails.Sum(od => od.Quantity * od.UnitPrice)
            };
        }

        public async Task<bool> CancelOrderAsync(int orderId)
        {
            var order = await _orderRepository.GetWithDetailsByIdAsync(orderId);
            if (order == null)
                return false;
            if (order.Status != "Pending")
                return false;
            order.Status = "Cancelled";
            await _orderRepository.UpdateAsync(order);
            return true;
        }

        // TODO: ConfirmOrder phải được thực thi trong một database transaction
        // để Stock, Invoice, Payment và Order Status cùng commit hoặc rollback.
        public async Task<bool> ConfirmOrderAsync(int orderId, string paymentMethod)
        {
            var order = await _orderRepository.GetWithDetailsByIdAsync(orderId);
            if(order == null)
                return false;
            if(order.Status != "Pending")
                return false;
            if(string.IsNullOrWhiteSpace(paymentMethod))
                throw new ArgumentException("Payment method is required.");
            var productToUpdate = new List<(Product product, int quantity)>();
            //vòng đầu kiểm tra dữ liệu, vòng 2 cập nhật dữ liệu
            foreach (var detail in order.OrderDetails)
            {
                var product = await _productRepository.GetByIdAsync(detail.ProductId);
                if (product == null)
                    throw new Exception($"Product with id {detail.ProductId} does not exist.");
                if (product.Stock < detail.Quantity)
                    throw new Exception($"Not enough stock for product {product.ProductName}. Available: {product.Stock}, Requested: {detail.Quantity}.");

                productToUpdate.Add((product, detail.Quantity));
            }

            foreach(var item in productToUpdate)
            {
                item.product.Stock -= item.quantity;
                await _productRepository.UpdateAsync(item.product);
            }

            var totalAmount = order.OrderDetails.Sum(od => od.Quantity * od.UnitPrice);
            var invoice = new Invoice
            {
                OrderId = order.OrderId,
                TotalAmount = totalAmount,
                PaymentTime = DateTime.UtcNow
            };

            var payment = new Payment
            {
                Amount = totalAmount,
                Method = paymentMethod,
                PaymentTime = DateTime.UtcNow
            };
            invoice.Payments.Add(payment);
            await _invoiceRepository.AddAsync(invoice);

            order.Status = "Completed";
            await _orderRepository.UpdateAsync(order);
            return true;
        }

        public async Task<OrderDTOs> CreateOrderAsync(CreateOrderRequest request)
        {
            if(request.Items == null || request.Items.Count == 0)
            {
                throw new ArgumentException("Order must contain at least one item.");
            }

            var orderDetails = new List<OrderDetail>();
            foreach(var item in request.Items)
            {
                if(item.Quantity <= 0)
                    throw new ArgumentException($"Quantity for product {item.ProductId} must be greater than zero.");
                if(item.ProductId <= 0 )
                    throw new ArgumentException("ProductId must be greater than zero.");

                //check if product exists
                var product = await _productRepository.GetByIdAsync(item.ProductId);
                if(product == null)
                    throw new ArgumentException($"Product with id {item.ProductId} does not exist.");

                //check if product has enough stock
                if(product.Stock < item.Quantity)
                    throw new ArgumentException($"Not enough stock for product {product.ProductName}. Available: {product.Stock}, Requested: {item.Quantity}.");

                //create order detail
                var orderDetail = new OrderDetail
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price
                };
                orderDetails.Add(orderDetail);
            }
            var order = new Order
            {
                CustomerId = request.CustomerId,
                OrderTime = DateTime.UtcNow,
                Status = "Pending",
                OrderDetails = orderDetails
            };

            var createdOrder = await _orderRepository.AddAsync(order);
            var saveOrder = await _orderRepository.GetWithDetailsByIdAsync(createdOrder.OrderId);
             
            if (saveOrder == null)
                throw new Exception("Failed to retrieve the created order.");

            return MapToDTOs(saveOrder);
        }

        public async Task<OrderDTOs?> GetByIdAsync(int id)
        {
            var order = await _orderRepository.GetWithDetailsByIdAsync(id);

            if (order == null)
                return null;
            
            return MapToDTOs(order);
        }
    }
}