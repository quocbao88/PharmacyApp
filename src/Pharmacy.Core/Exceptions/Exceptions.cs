using System;

namespace Pharmacy.Core.Exceptions
{
    public class InsufficientStockException : Exception
    {
        public InsufficientStockException(string message) : base(message)
        {
        }
    }

    public class PharmacyValidationException : Exception
    {
        public PharmacyValidationException(string message) : base(message)
        {
        }
    }

    public class ShiftAlreadyOpenException : Exception
    {
        public ShiftAlreadyOpenException(string message) : base(message)
        {
        }
    }

    public class ShiftClosedException : Exception
    {
        public ShiftClosedException(string message) : base(message)
        {
        }
    }
}
