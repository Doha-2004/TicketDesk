using TicketDesk.Starter;
using Xunit;

namespace TicketDesk.Tests;

// These tests run the REAL TicketBookingService with in-memory doubles.
// Expected values are fixed numbers from the task, never computed with the service's own logic.
public class TicketBookingServiceTests
{
    private static IReadOnlyDictionary<string, IBookingFee> Fees() => new Dictionary<string, IBookingFee>
    {
        ["standard"] = new StandardBookingFee(),
        ["member"] = new MemberBookingFee(),
        ["priority"] = new PriorityBookingFee(),
    };

    private static TicketRequest Request(
        string reference = "T-001",
        string name = "Amina",
        string email = "amina@example.test",
        int quantity = 2,
        decimal unitPrice = 100m) => new()
        {
            Reference = reference,
            CustomerName = name,
            Email = email,
            Quantity = quantity,
            UnitPrice = unitPrice
        };

    // ---- amounts -------------------------------------------------------------------------

    [Theory]
    [InlineData("standard", 2, 100, 200, 20, 220)]
    [InlineData("member", 2, 100, 200, 0, 200)]
    [InlineData("priority", 2, 100, 200, 50, 250)]
    [InlineData("member", 1, 50, 50, 0, 50)]
    [InlineData("member", 1, 100, 100, 0, 100)]
    [InlineData("  Member  ", 2, 100, 200, 0, 200)]
    public void Book_returns_the_expected_amounts(
        string feeMode, int quantity, int unitPrice, int expectedBase, int expectedFee, int expectedTotal)
    {
        var trace = new List<string>();
        var service = new TicketBookingService(new InMemoryBookingStore(trace), new CapturingConfirmationSender(trace), Fees());

        var receipt = service.Book(Request(quantity: quantity, unitPrice: unitPrice), feeMode);

        Assert.Equal((decimal)expectedBase, receipt.BaseAmount);
        Assert.Equal((decimal)expectedFee, receipt.Fee);
        Assert.Equal((decimal)expectedTotal, receipt.Total);
    }

    // ---- success path --------------------------------------------------------------------

    [Fact]
    public void Successful_booking_saves_once_then_notifies_once_with_a_full_receipt()
    {
        var trace = new List<string>();
        var store = new InMemoryBookingStore(trace);
        var sender = new CapturingConfirmationSender(trace);
        var service = new TicketBookingService(store, sender, Fees());

        var receipt = service.Book(
            Request(reference: " T-001 ", name: " Amina ", email: " amina@example.test ", quantity: 2, unitPrice: 100m),
            "standard");

        var expected = new BookingReceipt("T-001", "Amina", 2, 200m, 20m, 220m, "EGP");
        Assert.Equal(expected, receipt);
        Assert.Equal(new[] { "save", "notify" }, trace);
        Assert.Equal(expected, Assert.Single(store.Saved));
        var sent = Assert.Single(sender.Sent);
        Assert.Equal("amina@example.test", sent.Recipient);
        Assert.Equal(expected, sent.Receipt);
    }

    // ---- invalid input: every field on its own ---------------------------------------------

    private sealed record InvalidCase(Func<TicketRequest?> Build, Type Expected, string MessagePart);

    private static readonly Dictionary<string, InvalidCase> InvalidCases = new()
    {
        ["null request"] = new(() => null, typeof(ArgumentNullException), "request"),
        ["empty reference"] = new(() => Request(reference: ""), typeof(ArgumentException), "reference"),
        ["blank reference"] = new(() => Request(reference: "   "), typeof(ArgumentException), "reference"),
        ["empty name"] = new(() => Request(name: ""), typeof(ArgumentException), "customer name"),
        ["blank name"] = new(() => Request(name: "   "), typeof(ArgumentException), "customer name"),
        ["empty email"] = new(() => Request(email: ""), typeof(ArgumentException), "@"),
        ["blank email"] = new(() => Request(email: "   "), typeof(ArgumentException), "@"),
        ["email without @"] = new(() => Request(email: "amina.example.test"), typeof(ArgumentException), "@"),
        ["quantity zero"] = new(() => Request(quantity: 0), typeof(ArgumentOutOfRangeException), "Quantity"),
        ["quantity negative"] = new(() => Request(quantity: -1), typeof(ArgumentOutOfRangeException), "Quantity"),
        ["price zero"] = new(() => Request(unitPrice: 0m), typeof(ArgumentOutOfRangeException), "Unit price"),
        ["price negative"] = new(() => Request(unitPrice: -1m), typeof(ArgumentOutOfRangeException), "Unit price"),
    };

    public static IEnumerable<object[]> InvalidCaseNames() => InvalidCases.Keys.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(InvalidCaseNames))]
    public void Invalid_request_is_rejected_with_no_side_effects(string caseName)
    {
        var trace = new List<string>();
        var store = new InMemoryBookingStore(trace);
        var sender = new CapturingConfirmationSender(trace);
        var service = new TicketBookingService(store, sender, Fees());
        var invalid = InvalidCases[caseName];

        var failure = Assert.Throws(invalid.Expected, () => { service.Book(invalid.Build()!, "standard"); });

        Assert.Contains(invalid.MessagePart, failure.Message);
        Assert.Empty(trace);
        Assert.Empty(store.Saved);
        Assert.Empty(sender.Sent);
    }

    // ---- unknown fee selection -------------------------------------------------------------

    [Theory]
    [InlineData("gold")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Unknown_fee_mode_is_rejected_with_no_side_effects(string? feeMode)
    {
        var trace = new List<string>();
        var store = new InMemoryBookingStore(trace);
        var sender = new CapturingConfirmationSender(trace);
        var service = new TicketBookingService(store, sender, Fees());

        var failure = Assert.Throws<ArgumentException>(() => { service.Book(Request(), feeMode!); });

        Assert.Contains("fee", failure.Message);
        Assert.Empty(trace);
        Assert.Empty(store.Saved);
        Assert.Empty(sender.Sent);
    }

    // ---- failures of the collaborators -----------------------------------------------------

    [Fact]
    public void When_saving_fails_the_failure_reaches_the_caller_and_nobody_is_notified()
    {
        var trace = new List<string>();
        var sender = new CapturingConfirmationSender(trace);
        var boom = new IOException("disk full");
        var service = new TicketBookingService(new ThrowingStore(trace, boom), sender, Fees());

        var failure = Assert.Throws<IOException>(() => { service.Book(Request(), "standard"); });

        Assert.Same(boom, failure);
        Assert.Equal(new[] { "save" }, trace);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public void When_notifying_fails_the_failure_reaches_the_caller_and_the_saved_record_stays()
    {
        var trace = new List<string>();
        var store = new InMemoryBookingStore(trace);
        var boom = new InvalidOperationException("smtp down");
        var service = new TicketBookingService(store, new ThrowingSender(trace, boom), Fees());

        var failure = Assert.Throws<InvalidOperationException>(() => { service.Book(Request(), "standard"); });

        Assert.Same(boom, failure);
        Assert.Single(store.Saved);                       // record is kept, not rolled back
        Assert.Equal(new[] { "save", "notify" }, trace);  // one save, one notify attempt, no retry, no second save
    }
}