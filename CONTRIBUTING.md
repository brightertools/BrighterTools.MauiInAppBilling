# Contributing

Use GitHub issues for bugs and proposals, and focused pull requests against main. Use the SDK pinned in global.json and MAUI 10.0.101; run eng/bootstrap.ps1 with PowerShell 7 to prepare platform workloads. Run portable tests and compile every affected platform. Native changes require the relevant device/Store tests in docs/TESTING.md; record what was actually exercised.

Keep store-specific handles and APIs inside adapters. Do not turn cancellation, unavailable service, pending purchase or unverified evidence into ownership. Persist verified delivery before acknowledging or finishing, and preserve user access during transient errors. Do not log purchase tokens, signed transactions or account details.

Small focused pull requests should describe behavior, compatibility and validation. Changes to dependencies must include reviewed lock-file updates. No credentials, signing certificates, paid fonts or production purchase evidence belong in fixtures.

By contributing, you agree to license your contribution under the MIT license.

See [usage.md](usage.md), [Store setup](docs/STORE-SETUP.md) and [release process](docs/RELEASING.md). Treat contributors respectfully; review ideas and code, not the person.
