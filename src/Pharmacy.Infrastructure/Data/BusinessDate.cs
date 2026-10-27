using Pharmacy.Core.Exceptions;

namespace Pharmacy.Infrastructure.Data
{
    public static class BusinessDate
    {
        public static DateTime Today => DateTime.UtcNow.AddHours(7).Date;

        public static DateTime Start(DateTime value)
        {
            return new DateTimeOffset(DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified), TimeSpan.FromHours(7)).UtcDateTime;
        }

        public static void Check(DateTime start, DateTime end)
        {
            if (start == default || end == default || start.Date > end.Date || end.Date >= DateTime.MaxValue.Date)
            {
                throw new PharmacyValidationException("Khoảng ngày không hợp lệ.");
            }
        }
    }
}
