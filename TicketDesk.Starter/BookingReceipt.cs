namespace TicketDesk.Starter;

// Structured result of a booking. Text formatting is NOT done here; adapters format it.
public sealed record BookingReceipt(
    string Reference,
    string Customer,
    int Quantity,
    decimal BaseAmount,
    decimal Fee,
    decimal Total,
    string Currency);