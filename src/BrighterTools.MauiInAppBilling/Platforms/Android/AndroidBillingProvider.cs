using Android.App;
using Android.BillingClient.Api;
using System.Security.Cryptography;
using System.Text;
namespace BrighterTools.MauiInAppBilling;
internal sealed class AndroidBillingProvider : Java.Lang.Object, IStoreBillingProvider, IPurchasesUpdatedListener
{
    private readonly BillingClient client;
    private readonly BillingOptions options;
    private readonly SemaphoreSlim connection = new(1);
    private TaskCompletionSource<StorePurchaseResult>? purchase;
    public string StoreName => "GooglePlay";
    public event EventHandler? PurchasesChanged;
    public string ProductId(ProductRegistration p) => p.GoogleProductId;
    public AndroidBillingProvider(BillingOptions options)
    {
        this.options = options;
        client = BillingClient.NewBuilder(Android.App.Application.Context).SetListener(this)
            .EnablePendingPurchases(PendingPurchasesParams.NewBuilder().EnableOneTimeProducts().Build())
            .EnableAutoServiceReconnection().Build();
    }
    private sealed class ConnectionListener(TaskCompletionSource<BillingResult> source) : Java.Lang.Object, IBillingClientStateListener
    {
        public void OnBillingSetupFinished(BillingResult result) => source.TrySetResult(result);
        public void OnBillingServiceDisconnected() => source.TrySetException(new IOException("Google Play disconnected."));
    }
    private static void RequireOk(BillingResult result) { if (result.ResponseCode != BillingResponseCode.Ok) throw new IOException(result.DebugMessage); }
    private async Task Connect(CancellationToken token)
    {
        await connection.WaitAsync(token);
        try
        {
            if (client.IsReady) return;
            var source = new TaskCompletionSource<BillingResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.StartConnection(new ConnectionListener(source));
            RequireOk(await source.Task.WaitAsync(TimeSpan.FromSeconds(30), token));
        }
        finally { connection.Release(); }
    }
    private sealed class DetailsListener(TaskCompletionSource<IList<ProductDetails>> source) : Java.Lang.Object, IProductDetailsResponseListener
    {
        public void OnProductDetailsResponse(BillingResult result, QueryProductDetailsResult details)
        {
            if (result.ResponseCode == BillingResponseCode.Ok) source.TrySetResult(details.ProductDetailsList ?? []);
            else source.TrySetException(new IOException(result.DebugMessage));
        }
    }
    private async Task<IList<ProductDetails>> Details(IEnumerable<string> ids, CancellationToken token)
    {
        await Connect(token);
        var items = ids.Where(p => !string.IsNullOrWhiteSpace(p)).Select(id => QueryProductDetailsParams.Product.NewBuilder()
            .SetProductId(id).SetProductType(BillingClient.ProductType.Inapp).Build()).ToList();
        if (items.Count == 0) return [];
        var source = new TaskCompletionSource<IList<ProductDetails>>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.QueryProductDetails(QueryProductDetailsParams.NewBuilder().SetProductList(items).Build(), new DetailsListener(source));
        return await source.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
    }
    public async Task<IReadOnlyList<BillingProduct>> GetProductsAsync(IReadOnlyList<ProductRegistration> products, CancellationToken token)
    {
        var details = await Details(products.Select(p => p.GoogleProductId), token);
        return details.Select(d => new BillingProduct(products.Single(p => p.GoogleProductId == d.ProductId).Key, d.ProductId,
            d.Title, d.GetOneTimePurchaseOfferDetails()?.FormattedPrice ?? "")).ToArray();
    }
    private sealed class PurchasesListener(TaskCompletionSource<IList<Purchase>> source) : Java.Lang.Object, IPurchasesResponseListener
    {
        public void OnQueryPurchasesResponse(BillingResult result, IList<Purchase> purchases)
        {
            if (result.ResponseCode == BillingResponseCode.Ok) source.TrySetResult(purchases);
            else source.TrySetException(new IOException(result.DebugMessage));
        }
    }
    private bool Verify(Purchase p)
    {
        if (string.IsNullOrWhiteSpace(options.AndroidPublicKey)) return false;
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(options.AndroidPublicKey), out _);
            return rsa.VerifyData(Encoding.UTF8.GetBytes(p.OriginalJson), Convert.FromBase64String(p.Signature), HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException) { return false; }
    }
    public async Task<EntitlementQuery> RefreshAsync(CancellationToken token)
    {
        try
        {
            await Connect(token);
            var source = new TaskCompletionSource<IList<Purchase>>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.QueryPurchases(QueryPurchasesParams.NewBuilder().SetProductType(BillingClient.ProductType.Inapp).Build(), new PurchasesListener(source));
            var items = await source.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
            return new(true, items.Where(p => p.PurchaseState == PurchaseState.Purchased).SelectMany(p =>
                p.Products.Select(id => new StorePurchase(id, p.PurchaseToken, Verify(p), !p.IsAcknowledged, p.OriginalJson, p.Signature))).ToArray());
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return new(false, [], ex.Message); }
    }
    public Task<EntitlementQuery> RestoreAsync(CancellationToken token) => RefreshAsync(token);
    public async Task<StorePurchaseResult> PurchaseAsync(string productId, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(options.AndroidPublicKey)) return new(PurchaseStatus.Unavailable, Message: "Configure the Play Console public licensing key first.");
        var detail = (await Details([productId], token)).SingleOrDefault();
        if (detail == null) return new(PurchaseStatus.Unavailable, Message: "This upgrade is not available in Google Play.");
        var source = new TaskCompletionSource<StorePurchaseResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        purchase = source;
        try
        {
            var product = BillingFlowParams.ProductDetailsParams.NewBuilder().SetProductDetails(detail);
            var offer = detail.GetOneTimePurchaseOfferDetails()?.OfferToken;
            if (!string.IsNullOrWhiteSpace(offer)) product.SetOfferToken(offer);
            var result = await MainThread.InvokeOnMainThreadAsync(() => client.LaunchBillingFlow(
                Microsoft.Maui.ApplicationModel.Platform.CurrentActivity ?? throw new InvalidOperationException("No active purchase activity."),
                BillingFlowParams.NewBuilder().SetProductDetailsParamsList([product.Build()]).Build()));
            if (result.ResponseCode == BillingResponseCode.ItemAlreadyOwned) return new(PurchaseStatus.AlreadyOwned);
            if (result.ResponseCode != BillingResponseCode.Ok) return new(PurchaseStatus.Failed, Message: result.DebugMessage);
            return await source.Task.WaitAsync(token);
        }
        finally { purchase = null; }
    }
    public void OnPurchasesUpdated(BillingResult result, IList<Purchase>? purchases)
    {
        var status = result.ResponseCode switch {
            BillingResponseCode.UserCancelled => PurchaseStatus.Cancelled,
            BillingResponseCode.ItemAlreadyOwned => PurchaseStatus.AlreadyOwned,
            BillingResponseCode.Ok when purchases?.Any(p => p.PurchaseState == PurchaseState.Pending) == true => PurchaseStatus.Pending,
            BillingResponseCode.Ok when purchases?.Any(p => p.PurchaseState == PurchaseState.Purchased && Verify(p)) == true => PurchaseStatus.Purchased,
            BillingResponseCode.Ok => PurchaseStatus.Unverified,
            _ => PurchaseStatus.Failed
        };
        purchase?.TrySetResult(new(status, Message: result.ResponseCode == BillingResponseCode.Ok ? null : result.DebugMessage));
        PurchasesChanged?.Invoke(this, EventArgs.Empty);
    }
    private sealed class AcknowledgeListener(TaskCompletionSource<BillingResult> source) : Java.Lang.Object, IAcknowledgePurchaseResponseListener
    { public void OnAcknowledgePurchaseResponse(BillingResult result) => source.TrySetResult(result); }
    public async Task CompleteAsync(StorePurchase p, CancellationToken token)
    {
        await Connect(token);
        var source = new TaskCompletionSource<BillingResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.AcknowledgePurchase(AcknowledgePurchaseParams.NewBuilder().SetPurchaseToken(p.TransactionId).Build(), new AcknowledgeListener(source));
        RequireOk(await source.Task.WaitAsync(TimeSpan.FromSeconds(30), token));
    }
    void IDisposable.Dispose() { client.EndConnection(); client.Dispose(); base.Dispose(); }
}
