using System.Globalization;

namespace TicketDesk.Starter;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return args.FirstOrDefault() switch
            {
                "--demo" => RunDemo(),
                "--contract-check" => RunContractChecks(),
                _ => ShowUsage()
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static int RunDemo()
    {
        var service = new TicketBookingService();
        var standard = service.Book(new TicketRequest
        {
            Reference = "T-001", CustomerName = "Amina", Email = "amina@example.test",
            Quantity = 2, UnitPrice = 100m
        }, "standard");

        var member = service.Book(new TicketRequest
        {
            Reference = "T-002", CustomerName = "Omar", Email = "omar@example.test",
            Quantity = 2, UnitPrice = 100m
        }, "member");

        Console.WriteLine($"Standard total: {standard.ToString("0.00", CultureInfo.InvariantCulture)} EGP");
        Console.WriteLine($"Member total: {member.ToString("0.00", CultureInfo.InvariantCulture)} EGP");
        Console.WriteLine("Run --contract-check to examine the common booking-fee contract.");
        return 0;
    }

    private static int RunContractChecks()
    {
        IBookingFee[] calculators = [new StandardBookingFee(), new MemberBookingFee()];
        decimal[] amounts = [1m, 50m, 100m, 200m];
        var failures = 0;

        foreach (var calculator in calculators)
        {
            foreach (var amount in amounts)
            {
                try
                {
                    var fee = calculator.Calculate(amount);
                    if (fee < 0)
                        throw new InvalidOperationException("A fee cannot be negative.");

                    Console.WriteLine($"PASS {calculator.GetType().Name}, base {amount}: fee {fee}");
                }
                catch (Exception exception)
                {
                    failures++;
                    Console.Error.WriteLine($"FAIL {calculator.GetType().Name}, valid base {amount}: {exception.Message}");
                }
            }
        }

        Console.WriteLine($"Contract checks: {amounts.Length * calculators.Length - failures} passed, {failures} failed.");
        return failures == 0 ? 0 : 1;
    }

    private static int ShowUsage()
    {
        Console.WriteLine("TicketDesk starter: use --demo or --contract-check.");
        return 0;
    }
}
