using CyberManagement.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberManagement.Application.Interfaces.Services
{
    public interface IOrderService
    {
        Task<OrderDTOs?> GetByIdAsync(int id);

        Task<OrderDTOs> CreateOrderAsync(CreateOrderRequest request); //createOrder -> check product -> check stock -> create orderdetail -> caculate total -> create order -> update stock

        Task<bool> ConfirmOrderAsync(int orderId); //confirmOrder -> subtract stock -> create invoice -> payment -> update order status

        Task<bool> CancelOrderAsync(int orderId);
    }
}
