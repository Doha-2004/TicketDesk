namespace TicketDesk.Starter;

public interface IBookingStore
{
    void Save(BookingReceipt receipt);
}