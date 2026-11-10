# TicketDesk: SOLID refactoring

Event-ticket booking console app (C#, .NET 8), refactored to demonstrate the five SOLID principles through
observable behavior. The original problems are documented **before** the refactor in
[`design-notes.md`](design-notes.md) (written in the Phase 1 commit); the "After the refactor" section at the
end of that file records what actually changed.

## Requirements

- .NET 8 SDK (or newer; both projects target `net8.0`)
- Git

## Commands

Run everything from the repository root (the folder that contains `TicketDesk.sln`).

```text
dotnet build TicketDesk.sln
dotnet test TicketDesk.sln
dotnet run --project TicketDesk.Starter -- --demo
dotnet run --project TicketDesk.Starter -- --contract-check
```

- `--demo` books one Standard and one Member ticket (2 x 100 EGP): totals **220.00** and **200.00**.
  It writes `ticket-bookings.log` in the working directory; the file is git-ignored.
- `--contract-check` runs the shared `IBookingFee` contract (bases 1, 50, 100, 200) over Standard, Member and
  Priority: **12 passed, 0 failed**.
- `dotnet test` runs 37 xUnit tests with in-memory doubles; they never touch the file system or the console,
  and the process exits with a non-zero code if any check fails.

## Business rules

`BaseAmount = Quantity x UnitPrice`, `Total = BaseAmount + Fee`, amounts in EGP (`decimal`).

| Fee rule | Fee for every positive base amount |
| --- | ---: |
| Standard | 20 |
| Member | 0 |
| Priority | 50 |

## Layout

```text
TicketDesk.Starter/
  TicketRequest.cs             input DTO
  BookingRequestValidator.cs   request validation rules only
  BookingReceipt.cs            structured receipt (record)
  BookingFees.cs               IBookingFee, StandardBookingFee, MemberBookingFee
  PriorityBookingFee.cs        extension added in its own commit
  IBookingStore.cs             one operation: Save
  IConfirmationSender.cs       one operation: Send
  Gateways.cs                  adapters: FileBookingStore, ConsoleConfirmationSender (text formatting lives here)
  TicketBookingService.cs      coordinator: validate -> fee -> receipt -> save -> notify
  Program.cs                   explicit composition (no DI container), --demo, --contract-check
tests/TicketDesk.Tests/
  BookingFeeContractTests.cs   same contract checks for every fee policy
  TicketBookingServiceTests.cs workflow tests against the real service
  TestDoubles.cs               in-memory / capturing / throwing doubles sharing one trace list
```

## Principle -> code -> evidence

| Principle | Where it is in the code | Evidence |
| --- | --- | --- |
| **S** Single Responsibility | `TicketBookingService` only sequences validate -> fee -> receipt -> save -> notify. Rules are in `BookingRequestValidator`, data in `BookingReceipt`, file/console details and text formatting in the adapters in `Gateways.cs`. | `Successful_booking_saves_once_then_notifies_once_with_a_full_receipt` asserts receipt fields without any I/O. `Invalid_request_is_rejected_with_no_side_effects` covers 12 invalid inputs, each with zero saves and zero notifications. |
| **O** Open/Closed | Fee rules are an injected registry (`IReadOnlyDictionary<string, IBookingFee>`) built in `Program.CreateService`. The service contains no fee-specific code. | Commit `b3c03af` adds Priority by creating `PriorityBookingFee.cs` and registering it; `git show --stat b3c03af` does not list `TicketBookingService.cs`. Test `priority` 2 x 100 -> 200 / 50 / 250. |
| **L** Liskov Substitution | `MemberBookingFee` no longer adds a minimum spend (commit `c48bd39`); it accepts every positive base amount like the other fees. | Commit `3ff3ad6` ("before"): 2 of 8 contract checks fail (Member at 1 and 50). After `c48bd39` all pass. `BookingFeeContractTests` runs one loop over Standard, Member and Priority x 1, 50, 100, 200 (12 checks). |
| **I** Interface Segregation | `IMessageGateway` (email + SMS + print) was removed. `IBookingStore` has only `Save`; `IConfirmationSender` has only `Send`. | Every double in `TestDoubles.cs` implements one method; no `NotSupportedException` exists in the code base (`Select-String` finds none). |
| **D** Dependency Inversion | `TicketBookingService` receives `IBookingStore`, `IConfirmationSender` and the fee registry through its constructor and never uses `new` on a concrete collaborator, `File` or `Console`. `Program` composes everything explicitly. | `When_saving_fails_...` and `When_notifying_fails_...` use throwing doubles; the shared trace proves `save` then `notify`, no notify after a failed save, and no retry or second save after a failed notify. |

## Commit history (one commit per phase)

```text
Phase 0  baseline starter (unchanged source)
Phase 1  design notes written before refactoring
Phase 2  test harness and failing Member contract checks
Phase 3  fix MemberBookingFee LSP violation
Phase 4  separate responsibilities, small interfaces, constructor injection
Phase 5  inject fee registry (OCP)
Phase 6  workflow tests with in-memory doubles
Phase 7  add PriorityBookingFee as extension
Phase 8  documentation and review preparation
```

## Review walkthrough

1. **Standard and low-value Member.**
   `dotnet run --project TicketDesk.Starter -- --demo`, then
   `dotnet test TicketDesk.sln --filter "DisplayName~Book_returns_the_expected_amounts" --logger "console;verbosity=normal"`
   (includes Member 1 x 50 -> 50 and Member 1 x 100 -> 100).
2. **Substitute collaborators.** Open `TicketBookingServiceTests.Successful_booking_...`: the service is built
   with `InMemoryBookingStore` and `CapturingConfirmationSender`, and the trace is `save, notify`.
3. **Failures.**
   `dotnet test TicketDesk.sln --filter "DisplayName~When_" --logger "console;verbosity=normal"`
   (save failure -> caller sees it, zero notifications; notify failure -> caller sees it, one saved record, no retry).
4. **Priority without touching the workflow.**
   `git show --stat b3c03af` and `dotnet test TicketDesk.sln --filter "DisplayName~priority" --logger "console;verbosity=normal"`.
5. **LSP vs ISP.** See the last section of `design-notes.md`.