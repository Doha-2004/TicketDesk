namespace TicketDesk.Starter;

// Holds the request validation rules only (same rules and exception types as the starter).
public static class BookingRequestValidator
{
    public static void Validate(TicketRequest request)
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
    }
}