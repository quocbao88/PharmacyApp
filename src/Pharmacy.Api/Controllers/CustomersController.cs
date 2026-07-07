using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Core.DTOs;
using Pharmacy.Core.Entities;
using Pharmacy.Core.Interfaces;

namespace Pharmacy.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CustomersController : ControllerBase
    {
        private readonly ICustomerRepository _customerRepository;

        public CustomersController(ICustomerRepository customerRepository)
        {
            _customerRepository = customerRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var customers = await _customerRepository.GetAllAsync();
            var dtos = customers.Select(c => MapToDto(c)).ToList();
            return Ok(dtos);
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchByPhone([FromQuery] string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return BadRequest(new { error = "Vui lòng cung cấp số điện thoại cần tìm." });
            }

            var customer = await _customerRepository.GetByPhoneAsync(phone.Trim());
            if (customer == null)
            {
                return NotFound(new { error = "Không tìm thấy thông tin khách hàng." });
            }

            return Ok(MapToDto(customer));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request)
        {
            var existingCustomer = await _customerRepository.GetByPhoneAsync(request.Phone.Trim());
            if (existingCustomer != null)
            {
                return Conflict(new { error = "Số điện thoại này đã được đăng ký bởi một khách hàng khác." });
            }

            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                FullName = request.FullName.Trim(),
                Phone = request.Phone.Trim(),
                AllergyNotes = request.AllergyNotes?.Trim(),
                DateOfBirth = request.DateOfBirth,
                RewardPoints = 0
            };

            await _customerRepository.AddAsync(customer);
            await _customerRepository.SaveChangesAsync();

            return Ok(MapToDto(customer));
        }

        [HttpGet("{id}/history")]
        public async Task<IActionResult> GetHistory(Guid id)
        {
            var customer = await _customerRepository.GetByIdAsync(id);
            if (customer == null)
            {
                return NotFound(new { error = "Không tìm thấy thông tin khách hàng." });
            }

            var orders = await _customerRepository.GetCustomerOrderHistoryAsync(id);

            var historyDto = new CustomerHistoryDto
            {
                Customer = MapToDto(customer),
                Orders = orders.Select(o => new CustomerOrderDto
                {
                    OrderId = o.Id,
                    OrderCode = o.OrderCode,
                    CreatedAt = o.CreatedAt,
                    TotalAmount = o.TotalAmount,
                    Items = o.Details.Select(d => new CustomerOrderItemDto
                    {
                        ProductName = d.Product?.Name ?? "Sản phẩm",
                        BatchNumber = d.Batch?.BatchNumber ?? "Lô",
                        Quantity = d.Quantity,
                        UnitPrice = d.UnitPrice
                    }).ToList()
                }).ToList()
            };

            return Ok(historyDto);
        }

        private CustomerDto MapToDto(Customer c)
        {
            return new CustomerDto
            {
                Id = c.Id,
                FullName = c.FullName,
                Phone = c.Phone,
                AllergyNotes = c.AllergyNotes,
                DateOfBirth = c.DateOfBirth,
                RewardPoints = c.RewardPoints
            };
        }
    }
}
