using CyberManagement.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvoicesController : ControllerBase
    {
        private readonly InvoiceService _invoiceService;
        public InvoicesController(InvoiceService invoiceService)
        {
            _invoiceService = invoiceService;
        }

        // GET: api/<InvoicesController>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var invoices = await _invoiceService.GetAllAsync();
            return Ok(invoices);
        }

        // GET api/<InvoicesController>/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var invoice = await _invoiceService.GetByIdAsync(id);
            if (invoice == null)
                return NotFound();
            return Ok(invoice);
        }

        // GET /api/invoices/order/10
        [HttpGet("order/{orderId}")]
        public async Task<IActionResult> GetInvoicesByOrder(int orderId)
        {
            var invoices = await _invoiceService.GetByOrderIdAsync(orderId);
            if(invoices == null) return NotFound();
            return Ok(invoices);
        }

    }
}
