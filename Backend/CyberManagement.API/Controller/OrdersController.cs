using CyberManagement.Application.DTOs;
using CyberManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace CyberManagement.API.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        // GET api/<OrdersController>/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var order = await _orderService.GetByIdAsync(id);
            if(order == null)
                return NotFound();
            return Ok(order);

        }

        // POST api/<OrdersController>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
        {
            var createdOrder = await _orderService.CreateOrderAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = createdOrder.OrderId }, createdOrder);
        }

        // POST api/<OrdersController>/5/confirm
        [HttpPost("{id}/confirm")]
        public async Task<IActionResult> Confirm(int id , [FromBody] ConfirmOrderRequest request)
        {
            var result = await _orderService.ConfirmOrderAsync(id, request.PaymentMethod);
            if (!result)
                return BadRequest("Order confirmation failed.");
            return Ok("Order confirmed successfully.");
        }

        //POST /api/orders/5/cancel
        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            var result = await _orderService.CancelOrderAsync(id);
            if (!result)
                return BadRequest("Order cancellation failed.");
            return Ok("Order cancelled successfully.");
        }

    }
}
