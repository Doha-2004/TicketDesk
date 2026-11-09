namespace TicketDesk.Starter;

public interface IMessageGateway
{
    void SendEmail(string recipient, string body);
    void SendSms(string phoneNumber, string body);
    void Print(string body);
}

public sealed class EmailGateway : IMessageGateway
{
    public void SendEmail(string recipient, string body)
    {
        Console.WriteLine($"[Simulated email] To: {recipient}; {body}");
    }

    public void SendSms(string phoneNumber, string body)
    {
        throw new NotSupportedException("This gateway only supports email.");
    }

    public void Print(string body)
    {
        throw new NotSupportedException("This gateway cannot print.");
    }
}

public sealed class FileBookingStore
{
    public void Save(string receipt)
    {
        File.AppendAllText("ticket-bookings.log", receipt + Environment.NewLine);
    }
}
