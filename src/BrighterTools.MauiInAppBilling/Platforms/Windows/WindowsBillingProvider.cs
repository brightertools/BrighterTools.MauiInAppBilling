using Windows.Services.Store;
namespace BrighterTools.MauiInAppBilling;
internal sealed class WindowsBillingProvider(Func<Window?> window) : IStoreBillingProvider
{
    private StoreContext? context;
    public string StoreName => "MicrosoftStore";
    public event EventHandler? PurchasesChanged;
    public string ProductId(ProductRegistration p) => p.WindowsOfferToken;
    private StoreContext Context()
    {
        if (!MainThread.IsMainThread) throw new InvalidOperationException("Store UI must run on the UI thread.");
        if (window()?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window native) throw new InvalidOperationException("The purchase window is not ready.");
        if (context == null) { context = StoreContext.GetDefault(); context.OfflineLicensesChanged += LicensesChanged; }
        WinRT.Interop.InitializeWithWindow.Initialize(context, WinRT.Interop.WindowNative.GetWindowHandle(native));
        return context;
    }
    private void LicensesChanged(StoreContext sender, object args) => PurchasesChanged?.Invoke(this, EventArgs.Empty);
    public async Task<IReadOnlyList<BillingProduct>> GetProductsAsync(IReadOnlyList<ProductRegistration> products, CancellationToken token)
    {
        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var query = await Context().GetAssociatedStoreProductsAsync(["Durable"]);
            token.ThrowIfCancellationRequested();
            if (query.ExtendedError != null) throw query.ExtendedError;
            return (IReadOnlyList<BillingProduct>)products.Select(p =>
            {
                var item = query.Products.Values.FirstOrDefault(x => x.InAppOfferToken == p.WindowsOfferToken);
                return item == null ? null : new BillingProduct(p.Key, item.StoreId, item.Title, item.Price.FormattedPrice);
            }).Where(p => p != null).Cast<BillingProduct>().ToArray();
        });
    }
    public async Task<EntitlementQuery> RefreshAsync(CancellationToken token) => await MainThread.InvokeOnMainThreadAsync(async () =>
    {
        try
        {
            var licence = await Context().GetAppLicenseAsync(); token.ThrowIfCancellationRequested();
            if (licence == null || !licence.IsActive) return new EntitlementQuery(false, [], "Microsoft Store app licensing is unavailable.");
            return new EntitlementQuery(true, licence.AddOnLicenses.Values.Where(p => p.IsActive)
                .Select(p => new StorePurchase(p.InAppOfferToken, p.SkuStoreId, true, false)).ToArray());
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return new EntitlementQuery(false, [], ex.Message); }
    });
    public Task<EntitlementQuery> RestoreAsync(CancellationToken token) => RefreshAsync(token);
    public async Task<StorePurchaseResult> PurchaseAsync(string productId, CancellationToken token) => await MainThread.InvokeOnMainThreadAsync(async () =>
    {
        var store = Context();
        var query = await store.GetAssociatedStoreProductsAsync(["Durable"]);
        if (query.ExtendedError != null) return new StorePurchaseResult(PurchaseStatus.Unavailable, Message: query.ExtendedError.Message);
        var product = query.Products.Values.FirstOrDefault(p => p.InAppOfferToken == productId);
        if (product == null) return new StorePurchaseResult(PurchaseStatus.Unavailable, Message: "This upgrade is not configured in Microsoft Store.");
        token.ThrowIfCancellationRequested();
        var result = await store.RequestPurchaseAsync(product.StoreId);
        return new StorePurchaseResult(result.Status switch
        {
            StorePurchaseStatus.Succeeded => PurchaseStatus.Purchased,
            StorePurchaseStatus.AlreadyPurchased => PurchaseStatus.AlreadyOwned,
            StorePurchaseStatus.NotPurchased => PurchaseStatus.Cancelled,
            _ => PurchaseStatus.Failed
        }, Message: result.ExtendedError?.Message);
    });
    public Task CompleteAsync(StorePurchase p, CancellationToken t) => Task.CompletedTask;
    public void Dispose() { if (context != null) context.OfflineLicensesChanged -= LicensesChanged; }
}
