# Store setup

Do not use production accounts for automated purchase tests. Product identifiers and public licensing keys are configuration, not secret signing credentials.

## Windows

Create a permanent durable add-on for the existing Partner Center application. Map its **in-app offer token** (product ID), not its Store ID, to ProductRegistration.WindowsOfferToken. The provider resolves the product's Store ID before RequestPurchaseAsync and attaches the active WinUI HWND using InitializeWithWindow.

Use the real Store-associated MSIX identity and account. Unpackaged binaries cannot establish genuine Store entitlements. Install a Store-delivered build/test flight and validate upgrade from the released app. An arbitrary development package identity does not inherit a production add-on. The sample's generated manifest identity must be replaced with your own test application's association.

## Apple: iOS and Mac Catalyst

Create a non-consumable product in App Store Connect, complete agreements and price/localization metadata, and use the exact bundle/product IDs and signing/provisioning. Configure sandbox testers/StoreKit testing, explicit Restore and App Store review information. The bridge uses Product.purchase, verified Transaction.currentEntitlements, Transaction.updates, unfinished transaction recovery and finish after durable delivery. AppStore.sync runs only for explicit restore.

On a Mac, install Xcode compatible with the .NET Apple workload, select it with xcode-select, and build the sample for the target RID. The bridge is statically linked and signed as part of the application. Mac sandbox builds need network.client, as in the sample. Verify Keychain/SecureStorage provisioning for the actual signed bundle. Test Intel and Apple Silicon, sandbox/release signing and account changes.

## Google Play

Create a one-time permanent product with a purchasable offer. Set GoogleProductId and AndroidPublicKey (the Base64 public licensing key from Play Console). Use a Play-distributed internal-test build with the correct application ID/signing and license testers. The Billing Client binding supplies the billing permission through manifest merging; confirm it in the final APK/AAB. A successful PURCHASED transaction must be acknowledged within Google's deadline; the coordinator persists then acknowledges and retries unfinished delivery. No consume API is called.

## Sample real-store build properties

Set DemoStoreBuild=true plus BillingWindowsOfferToken, BillingAppleProductId, BillingGoogleProductId and BillingGooglePublicKey as applicable. Override ApplicationId and supply platform signing/association in a private consuming project or local command line. The sample's default build is simulation.

References: [Microsoft Store purchases](https://learn.microsoft.com/en-us/windows/uwp/monetize/in-app-purchases-and-trials), [StoreKit](https://developer.apple.com/storekit/), [Google Play integration](https://developer.android.com/google/play/billing/integrate), [Play security](https://developer.android.com/google/play/billing/security).
