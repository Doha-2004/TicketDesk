namespace TicketDesk.Starter;

// Coordinator only: validate -> fee -> receipt -> save -> notify.
public sealed class TicketBookingService
{
    private readonly IBookingStore _store;
    private readonly IConfirmationSender _sender;

    public TicketBookingService(IBookingStore store, IConfirmationSender sender)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    public BookingReceipt Book(TicketRequest request, string feeMode)
    {
        BookingRequestValidator.Validate(request);

        var baseAmount = request.Quantity * request.UnitPrice;

        // Temporary: this switch is removed in Phase 5 (fee registry).
        IBookingFee feeCalculator = feeMode?.Trim().ToLowerInvariant() switch
        {
            "standard" => new StandardBookingFee(),
            "member" => new MemberBookingFee(),
            _ => throw new ArgumentException("Unknown booking-fee mode.", nameof(feeMode))
        };

        var fee = feeCalculator.Calculate(baseAmount);
        var receipt = new BookingReceipt(
            request.Reference.Trim(),
            request.CustomerName.Trim(),
            request.Quantity,
            baseAmount,
            fee,
            baseAmount + fee,
            "EGP");

        _store.Save(receipt);
        _sender.Send(request.Email.Trim(), receipt);

        return receipt;
    }
}