using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Pharmacy.Core.Exceptions;

namespace Pharmacy.Api.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var statusCode = HttpStatusCode.InternalServerError;
            var message = "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.";

            switch (exception)
            {
                case PharmacyValidationException validationEx:
                    statusCode = HttpStatusCode.BadRequest;
                    message = validationEx.Message;
                    break;

                case InsufficientStockException stockEx:
                    statusCode = HttpStatusCode.Conflict; // 409 Conflict is ideal for concurrency/resource state conflicts
                    message = stockEx.Message;
                    break;

                default:
                    message = exception.Message; // In dev we can return exception message, in prod we can customize
                    break;
            }

            context.Response.StatusCode = (int)statusCode;

            var response = new { error = message };
            var json = JsonSerializer.Serialize(response);
            await context.Response.WriteAsync(json);
        }
    }
}
