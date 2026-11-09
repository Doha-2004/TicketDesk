namespace TicketDesk.Starter;

public interface IBookingFee
{
    // Every positive baseAmount is valid. Return a nonnegative EGP fee.
    // Implementations must not perform I/O.
    decimal Calculate(decimal baseAmount);
}

public sealed class StandardBookingFee : IBookingFee
{
    public decimal Calculate(decimal baseAmount)
    {
        if (baseAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(baseAmount));

        return 20m;
    }
}

public sealed class MemberBookingFee : IBookingFee
{
    public decimal Calculate(decimal baseAmount)
    {
        if (baseAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(baseAmount));

        return 0m;
    }
}
