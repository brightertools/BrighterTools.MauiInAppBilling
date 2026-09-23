# Verification and ownership

The coordinator accepts only configured products verified by the native provider and, when supplied, IPurchaseVerifier. Windows uses active add-on licenses from StoreContext. Apple consumes verified StoreKit 2 non-consumable current entitlements. Android requires PURCHASED state and verifies the signed original JSON using the configured Play Console public licensing key before acknowledgement.

Pending purchases are not delivered. Confirmed owned products are persisted to the platform secure store before finish/acknowledge. Failed finalization is retried on subsequent refreshes and a one-minute retry while the process runs. Process termination is handled by querying unfinished/unacknowledged store transactions on next launch. The application must initialize billing at startup and refresh on resume; no background service is installed.

Transient and unverified queries retain previously verified cached ownership. A confirmed empty result revokes access. Refund/account changes therefore require a successful subsequent Store query. Cached access has no artificial offline expiry for permanent purchases. A hostile modified client can bypass client-only checks; applications needing stronger protection must supply a server verifier and their own account/authentication design.

The secure journal is namespaced by application and provider. It contains owned keys and unfinished purchase evidence. Do not copy this journal into ordinary settings, export it, or include it in diagnostics. FakeBillingProvider must be explicitly injected by a test/demo; production DI never selects it.

The library does not link ownership across stores. An Apple shared product across an app's supported Apple platforms requires that app's own App Store configuration. Windows and Google purchases have independent ownership.
