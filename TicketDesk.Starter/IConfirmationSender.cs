namespace TicketDesk.Starter;

public interface IConfirmationSender
{
    void Send(string recipientEmail, BookingReceipt receipt);
}