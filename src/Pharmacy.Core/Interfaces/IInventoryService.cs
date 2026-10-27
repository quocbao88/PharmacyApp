using Pharmacy.Core.DTOs;

namespace Pharmacy.Core.Interfaces
{
    public interface IInventoryService
    {
        Task<BatchDto> ReceiveAsync(Guid productId, CreateBatchRequest request, Guid userId);
        Task<BatchDto> AdjustAsync(Guid batchId, StockAdjustmentRequest request, Guid userId);
        Task<List<StockMovementDto>> GetHistoryAsync(Guid batchId);
        Task<StockReportDto> GetReportAsync(DateTime start, DateTime end);
    }
}
