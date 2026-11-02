# Server-delivered subscriptions

The subscription API is additive. `IBillingService`, its product registration, permanent entitlement cache, Windows durable products and Apple/Google one-time operations are unchanged.

`ISubscriptionBillingService` supplies store products/prices, purchase, refresh, restore and store-management navigation. `SubscriptionPurchaseRequest` carries server-issued account correlation and optional Google replacement context. The application supplies `ISubscriptionDelivery`; there is deliberately no client-only verification fallback. The server must verify the store evidence, bind its original organisation/account and durably deliver the entitlement before returning `Delivered=true`.

Register `AddMauiSubscriptionBilling(isStoreBuild: ...)` and a server-backed `ISubscriptionDelivery`, or construct a provider with `SubscriptionRegistrationExtensions.CreateProvider(...)`. Load the configured `SubscriptionRegistration` list before purchase or restore. Prices and billing periods must come from the returned `SubscriptionProduct`, not a hard-coded currency/amount. Windows subscription purchasing is unavailable; Windows permanent purchases remain supported.

The coordinator does not cache subscription ownership. Pending, failed or missing device purchases cannot grant/revoke server entitlements. Renewals, grace periods, account hold, expiry, refunds and revocations are authoritative server lifecycle states, supplied by verified provider notifications and reconciliation. Subscribe to `PurchasesChanged`, refresh on screen/resume and retry unfinished delivery after network failures. Persist purchase preparation on the server before launching the store. Restore must resolve the original transaction binding, never bind to the organisation currently selected on the device.

Apple auto-renewable transactions carry the server-issued `appAccountToken` and signed JWS. StoreKit `finish()` occurs only after successful server delivery. Google sends its purchase token for Developer API verification; the application server must acknowledge after durable delivery. Android `CompleteAsync` intentionally does not race a second device acknowledgement.

## Replacement and store configuration

- Apple: use one subscription group for alternative seat tiers. Reuse the original account correlation when changing a bound subscription. StoreKit determines supported upgrade/downgrade timing.
- Google: configure one subscription product with the alternative recurring base plans when the product requires one subscription per store account. Google does not expose a stable private Play-account identifier to the app; multiple independent products cannot enforce this rule by inferring the Google account.
- The Android adapter verifies the original purchase token is still returned by the store before launching a replacement. It never silently falls back to a new purchase.
- Billing 9 same-product changes support full-price immediate charging or without-proration(next-renewal pricing). Lower seat limits must remain deferred on the application server until the existing paid period ends, even when the store immediately changes the base-plan identifier. Across distinct products the adapter uses prorated-price upgrade/deferred downgrade modes. Runtime replacement context must originate from the verified server binding.
- Introductory offers and prepaid plans are not selected by this adapter; it chooses the configured recurring base plan with no offer ID.

References: [Google replacement rules](https://developer.android.com/google/play/billing/subscriptions), [Google verification](https://developer.android.com/google/play/billing/security), [Apple current entitlements](https://developer.apple.com/documentation/storekit/transaction/currententitlements).

## Verification recorded 2026-11-01

- Portable suite: 22 passed (permanent purchases plus subscription delivery rejection, pending, restore binding, finish retry, product/correlation validation and supported replacement modes).
- Android, Windows and iOS managed adapters compile with SDK 10.0.401, zero warnings/errors; repository SDK pin 10.0.303 was unavailable here and remains unchanged. SDK-induced ILLink lockfile changes were restored to the existing pin; no dependency upgrade is required by this additive API.
- The unchanged DesktopColourPicker consumer compiled successfully for Windows against this exact local billing project, with zero warnings/errors. Its 73 portable core regression tests passed. Commands were run from ClrImpactApp with SDK 10.0.401: `dotnet build C:/Development/DesktopColourPicker/src/DesktopColourPicker/DesktopColourPicker.csproj -f net10.0-windows10.0.19041.0 --no-restore -p:StoreRelease=false -p:WindowsPackageType=None -p:AppxPackageSigningEnabled=false` and `dotnet test C:/Development/DesktopColourPicker/tests/ColourPicker.Core.Tests/ColourPicker.Core.Tests.csproj --no-restore`. Consumer source, references and locks were unchanged; the app was not launched. These checks do not establish real Store purchase/restore/refund behaviour.
- Native Swift compilation/linking, iOS/Mac device tests, real Google Play test-track purchases, actual server notifications and acknowledgement/finish recovery remain release gates. Managed compilation or mocked tests are not evidence of store acceptance.
- No packages published and no store purchases initiated.
