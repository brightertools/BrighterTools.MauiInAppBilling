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

Toolchain: .NET SDK 10.0.303, MAUI Controls 10.0.101, installed Android 36.1.43 and Apple 26.5.10284 workloads. The CI bootstrap requests workload set 10.0.303.1; see the later public-repository validation below for CI results.

## Outstanding gates

- Complete native bridge builds for all supported Apple RIDs beyond the unsigned Mac Catalyst arm64 CI build, then run with trimming/AOT and signed StoreKit sandbox purchases.
- Real Windows Store package/product association, purchase/restore/refund/account/offline tests.
- Real Play-distributed test purchase, pending delivery, signature verification and acknowledgement on Android devices.
- Real Apple devices/Store test accounts and full app signing/distribution.
- Clean CI run, NuGet ownership/trusted publishing and owner-approved release workflow.

The public source repository is https://github.com/brightertools/BrighterTools.MauiInAppBilling. NuGet packages remain unpublished. These library checks do not certify real Store purchases.

## Public repository validation — 23 September 2026

- Portable billing tests: 12 passed in Release, none failed or skipped.
- Local Windows and Android sample Release builds: succeeded with zero warnings/errors. Android includes trimming and AOT for the default runtime set.
- Local iOS and Mac Catalyst managed Compile targets: succeeded with zero warnings/errors.
- Both preview NuGet packages and symbol packages were rebuilt. Automated verification passed for MIT/repository metadata, all four platform assemblies, portable assembly, native Swift/build sources and documentation.
- Public source, contribution templates, usage guidance and artifact-only CI are available on main. No NuGet packages were published.
- GitHub Windows CI passed tests, Windows/Android builds and package verification on the initial public commit.
- The Mac Catalyst arm64 Release sample built successfully with zero warnings/errors on the macOS runner, including the Swift native bridge and trimming. This is an unsigned build check, not a device or Store purchase test.
- CI revealed and corrected a global target-framework override that broke portable project restore, then an empty-array incompatibility with macOS Bash in the iOS bridge build script. See [GitHub Actions](https://github.com/brightertools/BrighterTools.MauiInAppBilling/actions) for the latest full run.

Local checks used the installed workload versions listed above. GitHub installs the pinned workload set 10.0.303.1; package target-platform SDK suffixes can differ from the older local Apple workload. Use the pinned bootstrap for release packages. Real purchases, signed distribution, other Apple architectures and hardware interaction remain release gates.
