using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pharmacy.Core.DTOs;

namespace Pharmacy.Core.Interfaces
{
    public interface IReportService
    {
        Task<ReportSummaryDto> GetRevenueAndProfitReportAsync(DateTime start, DateTime end);
        Task<List<TopProductDto>> GetTopSellingProductsAsync(DateTime start, DateTime end, int limit);
    }
}
