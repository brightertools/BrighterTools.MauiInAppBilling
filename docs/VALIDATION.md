# Validation — 17 September 2026

This is implementation/build evidence, not production Store acceptance.

| Check | Result |
|---|---|
| Portable billing tests | 12 passed in Release, none failed/skipped |
| Windows adapter / sample | Built with zero warnings/errors |
| Android adapter / sample | Full APK build succeeded with zero warnings/errors |
| Local NuGet package consumers | Windows and Android sample builds succeeded using the packed package |
| iOS and Mac Catalyst | Library managed assemblies built/packed; sample managed Compile targets succeeded on Windows |
| Package contents | Both 0.1.0-preview.1 packages and symbol packages created locally; main package has four platform assemblies, native Swift source/build script, buildTransitive target and MIT documentation |
| Android manifest | Final merged manifest contains com.android.vending.BILLING and Billing service queries; minimum API 23 |
| Colour Picker integration | Windows build, portable tests and isolated fake-provider Pro UI checks passed; see the app's docs/VALIDATION.md |

Toolchain: .NET SDK 10.0.303, MAUI Controls 10.0.101, installed Android 36.1.43 and Apple 26.5.10284 workloads. The CI bootstrap requests workload set 10.0.303.1; that exact clean CI environment has not run here.

## Outstanding gates

- Compile and link the Swift bridge on a Mac for all supported Apple RIDs, then run with trimming/AOT and signed StoreKit sandbox purchases.
- Real Windows Store package/product association, purchase/restore/refund/account/offline tests.
- Real Play-distributed test purchase, pending delivery, signature verification and acknowledgement on Android devices.
- Real Apple devices/Store test accounts and full app signing/distribution.
- Clean CI run, NuGet ownership/trusted publishing and owner-approved release workflow.

The public source repository is https://github.com/brightertools/BrighterTools.MauiInAppBilling. NuGet packages remain unpublished. These library checks do not certify real Store purchases.
