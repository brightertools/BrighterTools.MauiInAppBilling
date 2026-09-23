# BrighterTools.MauiInAppBilling

[![Build and test](https://github.com/brightertools/BrighterTools.MauiInAppBilling/actions/workflows/ci.yml/badge.svg)](https://github.com/brightertools/BrighterTools.MauiInAppBilling/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Permanent in-app upgrades for .NET 10 / MAUI 10: Microsoft Store, App Store on iOS and Mac Catalyst, and Google Play.

**Preview implementation.** Windows and Android managed adapters compile; the shared transaction coordinator has automated tests. Apple uses an owned Swift StoreKit 2 bridge, built from source on a Mac. Real Store purchases, native Apple linkage and device acceptance must pass before a production NuGet release. NuGet packages have not been published.

## Scope

- One-time permanent products (Windows durable add-ons / Apple non-consumables / Google Play one-time products).
- Store prices and product mappings, purchase, refresh, restore and entitlement-change events.
- Distinct cancelled, pending, unavailable, unverified and failed outcomes.
- Verified entitlement cache in MAUI SecureStorage, serialized operations, durable delivery before finish/acknowledge, retry on refresh/startup and every minute while unfinished delivery remains.
- Optional application-provided verification service.
- No subscriptions, consumables, cross-store accounts, receipt server, pricing UI or MAUI Shell dependency.

Package layout: `BrighterTools.MauiInAppBilling.Abstractions` contains portable contracts/coordinator/testing tools; `BrighterTools.MauiInAppBilling` provides the four platform adapters and DI registration. The latter selects platform dependencies by target framework; separate Desktop/Mobile packages are unnecessary for v1.

## Platform status

| Platform | Adapter | Validation |
|---|---|---|
| Windows | Microsoft Store durable add-ons | Managed adapter and sample build; integrated into Colour Picker |
| Android | Google Play Billing | Managed adapter and sample build; real Play testing outstanding |
| iOS / Mac Catalyst | StoreKit 2 through an owned Swift bridge | Managed compilation; native Mac build and Store acceptance outstanding |

See [validation evidence](docs/VALIDATION.md) for exact checks. This preview supports permanent upgrades only; subscriptions and consumables are not implemented.

## Install locally

Build packages using [eng/pack.ps1](eng/pack.ps1), then add its `artifacts/packages` folder as a NuGet source. Version starts at `0.1.0-preview.1`. Publication and trusted-publisher policy setup remain owner actions.

```csharp
using BrighterTools.MauiInAppBilling;

builder.Services.AddMauiInAppBilling(options =>
{
    options.ApplicationId = AppInfo.PackageName;
    options.IsStoreBuild = true; // Only in a correctly associated Store distribution.
    options.Products.Add(new("pro", "WINDOWS_OFFER_TOKEN",
        "APPLE_PRODUCT_ID", "GOOGLE_PRODUCT_ID"));
    options.AndroidPublicKey = "PLAY_CONSOLE_BASE64_PUBLIC_LICENSING_KEY";
}, () => Application.Current?.Windows.FirstOrDefault());
```

Register once. Resolve `IBillingService` after the native window/activity exists and call `InitializeAsync()`. Subscribe to `EntitlementsChanged` and marshal UI updates to the main thread. Check `Entitlements.Owns("pro")` to gate features. Load localized product prices with `GetProductsAsync()`; never hard-code a purchase price. On a user gesture, call `PurchaseAsync("pro")`. Offer an explicit Restore button calling `RestorePurchasesAsync()`. Refresh on resume. Dispose the service when the application exits.

A cancelled/pending callback alone never grants access. `IsCurrent == false` means the current query was unavailable or unverified; previously verified cached access remains. A confirmed empty Store result removes access. Keep user data when that happens.

SecureStorage protects cache at rest; it is not an anti-tamper guarantee against a modified client. Android verifies the signed purchase JSON with the app's Play licensing public key. StoreKit verifies signed transactions on-device. Applications needing stronger server-side validation can register `IPurchaseVerifier` before registration. See [security](docs/SECURITY-MODEL.md).

## Apple native bridge

NuGet automatically imports `buildTransitive/BrighterTools.MauiInAppBilling.targets`. It compiles `native/AppleBilling/Billing.swift` into a static library in the consuming app's intermediate directory and links StoreKit/Foundation and Swift system libraries. Xcode and the matching .NET Apple workload are required. No binary downloading occurs during this native build.

When using a **ProjectReference**, also import the source target in the consuming app:
```xml
<Import Project="PATH/BrighterTools.MauiInAppBilling/buildTransitive/BrighterTools.MauiInAppBilling.targets" />
```

Supported bridge RIDs: ios-arm64, iossimulator-arm64, iossimulator-x64, maccatalyst-arm64 and maccatalyst-x64. Build each Mac architecture, then use the normal .NET universal application publish. Windows managed-only Apple compilation does not exercise the Swift compiler or linker.

## Build and sample

Run eng/bootstrap.ps1 with PowerShell 7 to install the workload set pinned in [eng/toolchain.json](eng/toolchain.json) and restore dependencies. Portable tests require only the SDK. Platform restores may update the sample lock file for host-specific runtime identifiers; review those changes before committing.

SDK 10.0.303; MAUI Controls 10.0.101; Google BillingClient binding 9.1.0.2. Windows 10 1809+, Android API 23+, iOS 15+, Mac Catalyst 15+. A host application can require a higher OS version.

```powershell
dotnet test tests/Billing.Tests/Billing.Tests.csproj
dotnet build samples/BillingDemo/BillingDemo.csproj -f net10.0-windows10.0.19041.0 -p:RuntimeIdentifierOverride=win-x64
dotnet build src/BrighterTools.MauiInAppBilling/BrighterTools.MauiInAppBilling.csproj -f net10.0-android
pwsh eng/pack.ps1
```

The sample defaults to **SIMULATION**, uses memory-only state and cannot charge money. Its controls exercise purchase, pending, refund and Store-unavailable outcomes. `DemoStoreBuild=true` removes simulation and enables the native provider; supply product properties and signed store association described in [Store setup](docs/STORE-SETUP.md).

Read [usage and integration](usage.md), [testing](docs/TESTING.md), [contributing](CONTRIBUTING.md), [security reporting](SECURITY.md) and [release process](docs/RELEASING.md). MIT licensed; see [third-party notices](THIRD_PARTY_NOTICES.md).
