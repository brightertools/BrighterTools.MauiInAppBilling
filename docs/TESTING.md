# Validation

## Automated

Run dotnet test tests/Billing.Tests/Billing.Tests.csproj. Coverage includes cancellation/pending/failure, verified delivery, repeated refresh, offline restart, confirmed loss, unverified evidence, durable-write failure, optional verification rejection, finalization retry and standalone unavailable behavior.

Build the library and sample on Windows and Android. Compile managed iOS/Mac Catalyst targets on Windows using -t:Compile only as an early C# check. On a Mac, build and run the full sample for maccatalyst-arm64/x64 and iOS simulator/device; verify the bt_billing_request symbol links and callbacks survive trimming/AOT.

## Store/device matrix (required before production release)

For each platform: localized price, successful purchase, cancel, already owned, pending approval then completion, unavailable Store, account change, explicit restore after reinstall, refund/revocation, app terminated between delivery and finish, repeated native events, acknowledgement retry, resume and cold start.

Check unverified purchase evidence never grants access. Verify cached access survives offline launch but disappears after confirmed refund. Verify completing a transaction is idempotent and never finishes before secure persistence. Exercise unsigned/misconfigured apps: purchase must remain unavailable and no fake entitlement can be selected automatically.

Windows: foreground owner HWND and real Store package association. Android: signed Play test builds, release trimming, pending product approval and acknowledgement state. Apple: StoreKit native compilation, signed sandbox purchases, restore, updates while inactive, AOT callbacks and real Mac/iOS hardware.

## Current evidence

See docs/VALIDATION.md for this implementation's actual results. A CI build or a fake-provider test is not evidence of a real successful payment.
