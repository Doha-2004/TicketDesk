# design-notes.md — TicketDesk (written BEFORE any refactoring)

Baseline: Phase 0 commit. Observed with the starter code, unchanged:

- `dotnet build` succeeds.
- `--demo` prints Standard total 220.00 and Member total 200.00.
- `--contract-check` prints **6 passed, 2 failed**: `MemberBookingFee` fails for base 1 and base 50 with
  `InvalidOperationException("Member bookings require a base amount of at least 100 EGP.")`.

Line numbers below refer to the starter files.

---

## S — Single Responsibility

**Problem.** `TicketBookingService.Book` (lines 9–40) changes for six different reasons:
(1) request validation (lines 9–18), (2) choosing the fee rule (lines 22–27), (3) computing base/fee/total (lines 20, 29–30),
(4) formatting the receipt as a string (lines 31–32), (5) persistence via `new FileBookingStore()` (line 34),
(6) notification via `new EmailGateway()` (line 37). The receipt exists only as a string, so nothing can read its fields.

**Future change that exposes it.** Adding a validation rule, changing the receipt layout, or switching from a file to a database
all edit the same method, so each change risks breaking the other five concerns. A test of the receipt amounts cannot be written
without also triggering file I/O and console output.

**Planned change and proof.** Introduce a structured `BookingReceipt` (reference, customer, quantity, base, fee, total, currency),
a `BookingRequestValidator` holding the validation rules, and keep `TicketBookingService` as a coordinator that only calls
validate → fee → receipt → save → notify. Text formatting moves into the adapters (file store / sender).
Proof: tests assert receipt fields directly on the returned `BookingReceipt`, and one test per invalid field against the validator path
shows zero saves and zero notifications. I will not create a class per line of code; the coordinator keeps the sequencing only.

## O — Open/Closed

**Problem.** Fee selection is a string `switch` inside `Book` (lines 22–27) that names `StandardBookingFee` and `MemberBookingFee`
directly. The workflow must be edited every time a fee rule is added.

**Future change that exposes it.** Adding `PriorityBookingFee` (50 EGP) requires a new `case` in `Book`, so the booking workflow
is modified instead of extended.

**Planned change and proof.** Inject a registry of `IBookingFee` implementations keyed by a normalized name
(trim + case-insensitive); the workflow looks up the key and rejects unknown keys before any side effect.
Moving the same `switch` into another business class would not count, so the registry lives in composition (`Program`).
Proof: Priority is added in its own commit that creates `PriorityBookingFee.cs` and registers it in `Program`; `git diff --stat`
for that commit must not list `TicketBookingService.cs`. A test shows `priority`, 2 × 100 → base 200, fee 50, total 250.

## L — Liskov Substitution

**Problem.** `IBookingFee.Calculate` documents the contract: every positive base amount is valid, the fee is nonnegative, no I/O
(BookingFees.cs lines 5–6). `MemberBookingFee` adds a precondition the contract does not allow: for a positive base below 100
it throws `InvalidOperationException` (lines 28–29). A caller that works with `StandardBookingFee` for a 1 EGP or 50 EGP
booking breaks when handed a `MemberBookingFee`, so the two are not substitutable.

**Future change that exposes it.** Any new caller (or `PriorityBookingFee`) written against the interface contract will be fine
with Standard and then fail for Member on small bookings. It is already visible: `--contract-check` fails for Member at base 1 and 50,
and a 1 × 50 Member booking would throw from `Book` instead of returning a total of 50.

**Planned change and proof.** Remove the minimum-spend rule from `MemberBookingFee` (the nonpositive-input check stays, because
zero/negative base amounts are outside the contract). I will not catch the exception, skip small inputs, or return 0 from a catch.
Proof: the same contract checks run in one loop over Standard, Member, and later Priority for bases 1, 50, 100, 200;
each must pass with the right fee (20 / 0 / 50) and no negative result. Member 1 × 50 → total 50 and 1 × 100 → total 100.

## I — Interface Segregation

**Problem.** `IMessageGateway` bundles `SendEmail`, `SendSms`, and `Print` (Gateways.cs lines 3–8). `EmailGateway` can only
send email, so it implements `SendSms` and `Print` by throwing `NotSupportedException` (lines 19 and 24). The workflow
needs exactly one operation, sending a confirmation email.

**Future change that exposes it.** Any new sender or test double (for example a capturing sender for tests) is forced to
implement SMS and printing and to throw for them. Adding a method to the interface later breaks every implementer.

**Planned change and proof.** Replace `IMessageGateway` with the smallest contracts the consumers use: `IConfirmationSender`
with one send operation, and `IBookingStore` with one save operation. SMS and printing are not implemented.
Proof: the test doubles (`CapturingConfirmationSender`, `InMemoryBookingStore`) each implement one method and contain no
`NotSupportedException`.

## D — Dependency Inversion

**Problem.** `Book` constructs its own collaborators: `new FileBookingStore()` (line 34) and `new EmailGateway()` (line 37).
`FileBookingStore` has no abstraction at all and writes `ticket-bookings.log` in the working directory;
`EmailGateway` writes to the console.

**Future change that exposes it.** Tests for "store throws" or "sender throws", or for call order, cannot be written without
real file and console side effects (this is why running `--demo` created `ticket-bookings.log`). Changing storage means editing the workflow.

**Planned change and proof.** `TicketBookingService` receives `IBookingStore`, `IConfirmationSender`, and the fee registry through
its constructor. `Program` does the explicit composition (`new FileBookingStore()`, console sender, registry). No DI container.
Proof: tests build the service with in-memory and throwing doubles that write to one shared `List<string>` trace, and assert
the sequence `save, notify`; a throwing store yields zero notifications; a throwing sender leaves one saved record, one notify attempt, no retry.
No test touches the filesystem.

---

## LSP vs ISP — two different problems

- **LSP (Member fee)** is about *behavior*. The interface is fine and callers are entitled to rely on its stated contract,
  but one implementation narrows what it accepts. The caller's view of `IBookingFee` changed depending on which implementation it got.
  The fix is in the implementation (and in tests that hold every implementation to the same contract).
- **ISP (`IMessageGateway`)** is about the *size of the contract*. The interface asks for more than its consumers need,
  so implementers are forced into stubs that throw. The fix is in the interface, by splitting it into the small contracts that are actually used.

## Why the Member bug exists

The starter encoded a business-looking minimum spend of 100 EGP in the Member class. The documented contract and the
task's business rules say Member is simply a 0 EGP fee for every positive base amount, so the minimum was a hidden rule that the
interface does not allow. It stayed unnoticed because the workflow only exercised the happy path at 200 EGP.

## Planned verification summary

| Principle | Observable proof |
|---|---|
| S | Receipt fields asserted on `BookingReceipt`; each invalid field rejected with 0 saves and 0 notifications |
| O | Priority-only commit; `TicketBookingService.cs` absent from its diff; 2 × 100 Priority → 250 |
| L | One shared loop: Standard, Member, Priority × bases 1, 50, 100, 200 → 12 checks, all pass |
| I | Doubles implement a single method; no `NotSupportedException` anywhere |
| D | Service built with in-memory / throwing doubles; shared trace shows `save, notify`; failure cases as listed above |

---

## After the refactor

This section was added in Phase 8. Everything above is the original Phase 1 text, unchanged.

### What was done, per principle

| Principle | Planned | Done |
|---|---|---|
| S | `BookingReceipt`, `BookingRequestValidator`, coordinator-only service, formatting in adapters | As planned (commit `a2695a6`). Text formatting lives in `Gateways.cs` (a small internal `ReceiptText` helper shared by both adapters). The validator is a plain static class, not an interface: nothing needs to substitute it. |
| O | Injected registry keyed by normalized name; Priority in its own commit | Registry injected in `0160aa0`. The service copies the registry into a trim + case-insensitive dictionary, so callers need not care how it was built. Null, blank and unknown modes throw `ArgumentException` before any side effect. Priority added in `b3c03af`. |
| L | Remove the minimum spend from `MemberBookingFee`, keep the `<= 0` rejection | Done in `c48bd39` (3 lines deleted). No catch, no special case, Member stays in every check. |
| I | `IBookingStore` (Save) and `IConfirmationSender` (Send); delete `IMessageGateway` | As planned. `EmailGateway` became `ConsoleConfirmationSender`. SMS and print are not implemented. |
| D | Constructor injection; explicit composition in `Program` | As planned. `Program.CreateService()` is the only place that names concrete collaborators and fee rules. |

### Verification results

| Principle | Planned proof | Actual result |
|---|---|---|
| S | Receipt fields asserted; each invalid field rejected with 0 saves and 0 notifications | 12 invalid-input cases plus 4 unknown-fee cases, all with an empty trace; full receipt asserted in the success test |
| O | Priority-only commit without `TicketBookingService.cs` | `b3c03af` changed 4 files: `PriorityBookingFee.cs`, `Program.cs` and two test files. `TicketBookingService.cs`, the validator and the store/sender interfaces were not touched. |
| L | One loop over Standard, Member, Priority x 1, 50, 100, 200 | Before the fix (`3ff3ad6`): 6 passed, 2 failed (Member 1 and 50). After (`c48bd39`): 8 of 8. With Priority (`b3c03af`): 12 of 12, also in `--contract-check`. |
| I | Doubles implement one method; no `NotSupportedException` | Holds. |
| D | Trace `save, notify`; failing store -> no notify; failing sender -> one saved record, one attempt, no retry | Covered by two dedicated tests plus the success test. The whole suite is 37 tests and uses no file system or console. |

Deviation from the plan: the Priority commit also touched two test files and `Program.cs` (to add the contract
rows and the `priority` registration), which the plan allowed; the workflow files were not touched.

### Where is each abstraction useful, and what gets harder without it?

- **`IBookingFee` + injected registry.** Priority was added with one new class and one registration line.
  Without them every new fee rule means editing the booking workflow (the old `switch`), and there is no
  shared contract to run the same checks against every rule.
- **`IBookingStore`.** The save-failure case ("failure reaches the caller, nobody is notified") can be tested
  with a throwing double. Without it the workflow would create a real file store, tests would write
  `ticket-bookings.log`, and switching to a database would edit the workflow.
- **`IConfirmationSender`.** Tests capture what would be sent and simulate a failing sender. Without it the
  workflow prints to the console directly, notification failure cannot be reproduced, and a new channel
  means editing the workflow.
- **`BookingReceipt`.** Tests compare fields. Without it the receipt is a string and tests have to parse text.
- **`BookingRequestValidator`.** Validation rules change independently of pricing and I/O, and can be tested
  with no collaborators. It is deliberately not an interface: there is one implementation and nothing to
  substitute, so an interface would add a file without adding value.

### LSP and ISP are different problems

- **LSP (`MemberBookingFee`) is about behavior.** `IBookingFee` documents that every positive base amount is
  valid. `MemberBookingFee` added a precondition (a minimum of 100 EGP) and threw
  `InvalidOperationException` for small amounts. A caller that works with `StandardBookingFee` for a 1 EGP or
  50 EGP booking breaks when handed a `MemberBookingFee`, so the caller's view of the contract changed with
  the implementation. The interface was fine; the fix is in the implementation, and tests hold every
  implementation to the same contract.
- **ISP (`IMessageGateway`) is about the size of the contract.** The interface demanded email, SMS and
  printing although the only consumer needed to send one confirmation email, so implementers were forced to
  write stubs that throw `NotSupportedException`. Nothing misbehaved when used correctly; the contract simply
  asked for too much. The fix is in the interface: split it into the small contracts that consumers use.

LSP breaks the expectations of callers; ISP breaks the design of the contract itself.