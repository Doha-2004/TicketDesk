namespace TicketDesk.Starter;

public sealed class TicketRequest
{
    public string Reference { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string Email { get; init; } = "";
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}
