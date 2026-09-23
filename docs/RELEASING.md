# Local packaging and release

Run eng/pack.ps1 on Windows with all four MAUI workloads installed. It runs tests, packs both packages into artifacts/packages, and verifies MIT/repository metadata, all expected platform assemblies, native bridge sources and symbol packages. Re-run eng/verify-packages.ps1 to check existing packages. The main package includes the Swift source and automatic buildTransitive target, so an Apple consumer compiles its native bridge with its own Xcode toolchain.

Before publishing:
1. Complete docs/TESTING.md on real Store-distributed test builds and Apple/mobile devices.
2. Build a clean package-consuming app for all four platforms, including a full signed/trimmed Apple link.
3. Review packages for credentials, paid fonts, local paths and unintended files.
4. The source repository is https://github.com/brightertools/BrighterTools.MauiInAppBilling. Establish ownership of both NuGet package IDs. Configure NuGet trusted publishing for the exact repository, release workflow and protected environment. Never put a NuGet API key in source.
5. Add an owner-approved publishing workflow only after that policy exists. This repository currently builds artifacts only and does not push packages.
6. Update Version, CHANGELOG and validation evidence, tag the reviewed commit, and publish both matching package versions.

The initial preview is 0.1.0-preview.1. Do not describe it as production-ready while native Store acceptance is outstanding.
