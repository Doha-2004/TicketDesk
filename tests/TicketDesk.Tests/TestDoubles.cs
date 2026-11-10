using TicketDesk.Starter;

namespace TicketDesk.Tests;

// Test doubles. They all write to ONE shared trace so the order of calls can be asserted.
// None of them touches the file system or the console.

internal sealed class InMemoryBookingStore(List<string> trace) : IBookingStore
{
    public List<BookingReceipt> Saved { get; } = new();

    public void Save(BookingReceipt receipt)
    {
        trace.Add("save");
        Saved.Add(receipt);
    }
}

internal sealed class CapturingConfirmationSender(List<string> trace) : IConfirmationSender
{
    public List<(string Recipient, BookingReceipt Receipt)> Sent { get; } = new();

    public void Send(string recipientEmail, BookingReceipt receipt)
    {
        trace.Add("notify");
        Sent.Add((recipientEmail, receipt));
    }
}

internal sealed class ThrowingStore(List<string> trace, Exception failure) : IBookingStore
{
    public void Save(BookingReceipt receipt)
    {
        trace.Add("save");
        throw failure;
    }
}

internal sealed class ThrowingSender(List<string> trace, Exception failure) : IConfirmationSender
{
    public void Send(string recipientEmail, BookingReceipt receipt)
    {
        trace.Add("notify");
        throw failure;
    }
}