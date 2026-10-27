namespace Pharmacy.Core.Interfaces
{
    public interface IDatabaseBackupService
    {
        Task<Stream> ExportAsync(CancellationToken cancellationToken);
    }
}
