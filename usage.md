# Usage

This library supplies billing and entitlement state. Your application supplies the upgrade UI, product configuration, feature gating and Store association.

## Register and initialize

See the registration example in [README](README.md). Set `IsStoreBuild` from your distribution configuration, not from a user preference. For local development use `FakeBillingProvider` and `MemoryBillingStateStore` as shown in [BillingDemo](samples/BillingDemo/MauiProgram.cs); simulation cannot charge money.

Resolve `IBillingService` after the platform window/activity exists:

```csharp
await billing.InitializeAsync();
var products = await billing.GetProductsAsync();
var pro = products.FirstOrDefault(product => product.Key == "pro");
var ownsPro = billing.Entitlements.Owns("pro");
```

Display the returned `FormattedPrice`; a missing product or unavailable Store is not a free upgrade.

## Purchase and restore

Call purchases from a deliberate user action and prevent repeated button presses while the operation is running:

```csharp
var result = await billing.PurchaseAsync("pro");
if (result.Status is PurchaseStatus.Purchased or PurchaseStatus.AlreadyOwned)
{
    // Use verified entitlement state to decide which features are enabled.
    var ownsPro = billing.Entitlements.Owns("pro");
}
```

Handle `Pending`, `Cancelled`, `Unavailable`, `Unverified` and `Failed` as distinct outcomes. Do not unlock a feature just because the payment UI closed. Keep pending purchases eligible for later delivery.

Offer a Restore action:

```csharp
var restored = await billing.RestorePurchasesAsync();
```

Refresh with `RefreshEntitlementsAsync()` when the host resumes. Listen to `EntitlementsChanged`, marshal UI changes onto the MAUI main thread, and unsubscribe when the consuming view is disposed. Dispose the singleton service at application shutdown, not when navigating between pages.

## Cached access and verification

`Entitlements.Owns(key)` includes previously verified cached ownership during temporary Store failures. `IsCurrent` indicates whether the latest query was confirmed. A confirmed result without an entitlement removes that entitlement; retain the user's saved data.

The coordinator journals verified delivery before finishing or acknowledging a transaction. If durable storage fails, delivery must retry. Do not replace the production state store with the memory-only sample implementation.

An application can register `IPurchaseVerifier` before `AddMauiInAppBilling` to add its own verification backend. The library does not provide a receipt server or cross-store accounts. Read [the security model](docs/SECURITY-MODEL.md) before choosing your trust boundary.

## Platform setup

Use [Store setup](docs/STORE-SETUP.md) for identifiers, signing and distribution. Windows offer tokens are distinct from Store IDs. Apple ProjectReference consumers must also import the native build target described in the README; NuGet consumers receive it automatically.

Use [the acceptance checklist](docs/TESTING.md) for real Store tests. A build or simulated purchase alone does not validate a payment integration.
