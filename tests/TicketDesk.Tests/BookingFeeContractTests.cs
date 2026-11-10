using TicketDesk.Starter;
using Xunit;

namespace TicketDesk.Tests;

public class BookingFeeContractTests
{
    // Every policy is held to the SAME contract (IBookingFee.Calculate):
    // any positive base amount is valid, the fee is nonnegative, and nothing throws.
    // Expected fees are fixed numbers from the task, not computed by the code under test.
    private static readonly Dictionary<string, (IBookingFee Fee, decimal Expected)> Policies = new()
    {
        ["Standard"] = (new StandardBookingFee(), 20m),
        ["Member"] = (new MemberBookingFee(), 0m),
        ["Priority"] = (new PriorityBookingFee(), 50m),
    };

    private static readonly decimal[] BaseAmounts = [1m, 50m, 100m, 200m];

    public static IEnumerable<object[]> Cases() =>
        from policy in Policies.Keys
        from amount in BaseAmounts
        select new object[] { policy, amount };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Fee_honours_the_positive_input_contract(string policy, decimal baseAmount)
    {
        var (fee, expected) = Policies[policy];

        var result = fee.Calculate(baseAmount);

        Assert.True(result >= 0, $"{policy} returned a negative fee for base {baseAmount}.");
        Assert.Equal(expected, result);
    }
}