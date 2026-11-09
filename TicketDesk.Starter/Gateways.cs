namespace TicketDesk.Starter;

// Adapters: the only place that touches the console / the file system.
// They also own the text format of a receipt.

internal static class ReceiptText
{
    public static string Format(BookingReceipt receipt) => FormattableString.Invariant(
        $"Reference={receipt.Reference} | Customer={receipt.Customer} | Tickets={receipt.Quantity} | Base={receipt.BaseAmount:0.00} | Fee={receipt.Fee:0.00} | Total={receipt.Total:0.00} | Currency={receipt.Currency}");
}

public sealed class ConsoleConfirmationSender : IConfirmationSender
{
    public void Send(string recipientEmail, BookingReceipt receipt)
    {
        Console.WriteLine($"[Simulated email] To: {recipientEmail}; {ReceiptText.Format(receipt)}");
    }
}

public sealed class FileBookingStore : IBookingStore
{
    public void Save(BookingReceipt receipt)
    {
        File.AppendAllText("ticket-bookings.log", ReceiptText.Format(receipt) + Environment.NewLine);
    }
}