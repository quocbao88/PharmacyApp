using Pharmacy.Core.Entities;
using Pharmacy.Core.Exceptions;

namespace Pharmacy.Core.Rules
{
    public static class OrderAmounts
    {
        public static decimal NetLine(Order order, OrderDetail detail)
        {
            var gross = order.Details.Sum(x => x.Subtotal);
            if (gross == 0)
            {
                return 0;
            }

            decimal before = 0;
            foreach (var line in order.Details.OrderBy(x => x.Id))
            {
                if (line.Id == detail.Id)
                {
                    return Round(order.TotalAmount * (before + line.Subtotal) / gross) -
                        Round(order.TotalAmount * before / gross);
                }
                before += line.Subtotal;
            }
            throw new PharmacyValidationException("Không tìm thấy chi tiết đơn hàng.");
        }

        public static decimal Refund(Order order, OrderDetail detail, int quantity)
        {
            if (quantity <= 0 || quantity > detail.Quantity - detail.ReturnedQuantity)
            {
                throw new PharmacyValidationException("Số lượng trả vượt quá số lượng còn có thể trả.");
            }
            var net = NetLine(order, detail);
            return Round(net * (detail.ReturnedQuantity + quantity) / detail.Quantity) -
                Round(net * detail.ReturnedQuantity / detail.Quantity);
        }

        private static decimal Round(decimal value)
        {
            return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        }
    }
}
