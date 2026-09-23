# Third-party notices

This project is an independently implemented billing abstraction. The Windows HWND initialization approach was informed by Brighter Tools' SocialTextEditor fixes and Microsoft's StoreContext documentation. James Montemagno's [InAppBillingPlugin](https://github.com/jamesmontemagno/InAppBillingPlugin) was reviewed for migration context; this package does not depend on or vendor its implementation.

Direct runtime dependencies include Microsoft.Maui.Controls and Microsoft's Xamarin.Android.Google.BillingClient / AndroidX bindings. Their upstream licenses and Google's Billing Client SDK terms continue to apply to those dependencies. StoreKit, Foundation and Microsoft Store APIs remain subject to their platform terms. Refer to the resolved NuGet package metadata and license files for the pinned versions.

The sample platform scaffolding originated from Microsoft's .NET MAUI templates. The billing SVG icon and library/bridge code are provided under this repository's MIT license. No Colour Picker Font Awesome Pro fonts or other paid application assets are included.
