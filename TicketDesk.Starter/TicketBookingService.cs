namespace TicketDesk.Starter;

// Coordinator only: validate -> fee -> receipt -> save -> notify.
// Fee rules are injected, so adding a rule never requires editing this class.
public sealed class TicketBookingService
{
    private readonly IBookingStore _store;
    private readonly IConfirmationSender _sender;
    private readonly IReadOnlyDictionary<string, IBookingFee> _fees;

    public TicketBookingService(
        IBookingStore store,
        IConfirmationSender sender,
        IReadOnlyDictionary<string, IBookingFee> fees)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        ArgumentNullException.ThrowIfNull(fees);

        // Keys are normalized here (trim + case-insensitive), so callers of this class
        // do not have to care how the registry they pass in was built.
        _fees = fees.ToDictionary(pair => pair.Key.Trim(), pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    public BookingReceipt Book(TicketRequest request, string feeMode)
    {
        BookingRequestValidator.Validate(request);

        // Unknown, null or blank fee selections are rejected before any side effect.
        if (string.IsNullOrWhiteSpace(feeMode) || !_fees.TryGetValue(feeMode.Trim(), out var feeCalculator))
            throw new ArgumentException("Unknown booking-fee mode.", nameof(feeMode));

        var baseAmount = request.Quantity * request.UnitPrice;
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