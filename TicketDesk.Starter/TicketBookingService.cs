namespace TicketDesk.Starter;

public sealed class TicketBookingService
{
    public decimal Book(TicketRequest request, string feeMode)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Reference))
            throw new ArgumentException("A booking reference is required.");
        if (string.IsNullOrWhiteSpace(request.CustomerName))
            throw new ArgumentException("A customer name is required.");
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            throw new ArgumentException("An email containing @ is required.");
        if (request.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.Quantity), "Quantity must be positive.");
        if (request.UnitPrice <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.UnitPrice), "Unit price must be positive.");

        var baseAmount = request.Quantity * request.UnitPrice;

        IBookingFee feeCalculator = feeMode?.Trim().ToLowerInvariant() switch
        {
            "standard" => new StandardBookingFee(),
            "member" => new MemberBookingFee(),
            _ => throw new ArgumentException("Unknown booking-fee mode.", nameof(feeMode))
        };

        var fee = feeCalculator.Calculate(baseAmount);
        var total = baseAmount + fee;
        var receipt = FormattableString.Invariant(
            $"Reference={request.Reference.Trim()} | Customer={request.CustomerName.Trim()} | Tickets={request.Quantity} | Base={baseAmount:0.00} | Fee={fee:0.00} | Total={total:0.00} | Currency=EGP");

        var store = new FileBookingStore();
        store.Save(receipt);

        IMessageGateway gateway = new EmailGateway();
        gateway.SendEmail(request.Email.Trim(), receipt);

        return total;
    }
}
