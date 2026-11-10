namespace TicketDesk.Starter;

// Extension: a new fee rule added WITHOUT touching the booking workflow.
public sealed class PriorityBookingFee : IBookingFee
{
    public decimal Calculate(decimal baseAmount)
    {
        if (baseAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(baseAmount));

        return 50m;
    }
}