using Android.BillingClient.Api;
namespace BrighterTools.MauiInAppBilling;

// Subscription purchases are delivered by the application server, never by the device cache.
internal sealed class AndroidSubscriptionProvider : Java.Lang.Object, IStoreSubscriptionProvider, IPurchasesUpdatedListener
{
    private readonly BillingClient client;
    private readonly SemaphoreSlim connection = new(1);
    private TaskCompletionSource<PurchaseResult>? purchase;
    public string Provider => "Google";
    public event EventHandler? PurchasesChanged;
    public AndroidSubscriptionProvider()
    {
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
        var items = ids.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().Select(id => QueryProductDetailsParams.Product.NewBuilder()
            .SetProductId(id).SetProductType(BillingClient.ProductType.Subs).Build()).ToList();
        if (items.Count == 0) return [];
        var source = new TaskCompletionSource<IList<ProductDetails>>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.QueryProductDetails(QueryProductDetailsParams.NewBuilder().SetProductList(items).Build(), new DetailsListener(source));
        return await source.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
    }
    public async Task<IReadOnlyList<SubscriptionProduct>> GetProductsAsync(IReadOnlyList<SubscriptionRegistration> products, CancellationToken token)
    {
        var details = await Details(products.Select(p => p.GoogleProductId), token);
        var result = new List<SubscriptionProduct>();
        foreach (var registration in products)
        {
            var detail = details.SingleOrDefault(d => d.ProductId == registration.GoogleProductId);
            // No introductory offers in this release: select the configured recurring base plan.
            var offer = detail?.GetSubscriptionOfferDetails()?.SingleOrDefault(o => o.BasePlanId == registration.GoogleBasePlanId && string.IsNullOrEmpty(o.OfferId));
            var phase = offer?.PricingPhases?.PricingPhaseList?.LastOrDefault();
            if (detail != null && offer != null && phase != null)
                result.Add(new(registration.Key, detail.ProductId, detail.Title, phase.FormattedPrice, phase.BillingPeriod, offer.BasePlanId, offer.OfferToken));
        }
        return result;
    }
    private sealed class PurchasesListener(TaskCompletionSource<IList<Purchase>> source) : Java.Lang.Object, IPurchasesResponseListener
    {
        public void OnQueryPurchasesResponse(BillingResult result, IList<Purchase> purchases)
        {
            if (result.ResponseCode == BillingResponseCode.Ok) source.TrySetResult(purchases);
            else source.TrySetException(new IOException(result.DebugMessage));
        }
    }
    public async Task<SubscriptionQuery> QueryAsync(bool restore, CancellationToken token)
    {
        try
        {
            await Connect(token);
            var source = new TaskCompletionSource<IList<Purchase>>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.QueryPurchases(QueryPurchasesParams.NewBuilder().SetProductType(BillingClient.ProductType.Subs).Build(), new PurchasesListener(source));
            var items = await source.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
            return new(true, items.Where(p => p.PurchaseState == PurchaseState.Purchased).SelectMany(p =>
                p.Products.Select(id => new SubscriptionPurchase(Provider, id, p.PurchaseToken, p.PurchaseToken, !p.IsAcknowledged, p.Signature))).ToArray());
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return new(false, [], ex.Message); }
    }
    public async Task<PurchaseResult> PurchaseAsync(SubscriptionPurchaseRequest request, CancellationToken token)
    {
        var detail = (await Details([request.ProductId], token)).SingleOrDefault();
        var offer = detail?.GetSubscriptionOfferDetails()?.SingleOrDefault(o => o.BasePlanId == request.BasePlanId && o.OfferToken == request.OfferToken && string.IsNullOrEmpty(o.OfferId));
        if (detail == null || offer == null) return new(PurchaseStatus.Unavailable, "This subscription is not available in Google Play. Refresh the store options.");
        var source = new TaskCompletionSource<PurchaseResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        purchase = source;
        try
        {
            var product = BillingFlowParams.ProductDetailsParams.NewBuilder().SetProductDetails(detail).SetOfferToken(offer.OfferToken);
            var flow = BillingFlowParams.NewBuilder().SetObfuscatedAccountId(request.AccountId).SetObfuscatedProfileId(request.AccountToken.ToString("D"));
            if (!string.IsNullOrEmpty(request.ReplacesPurchaseToken))
            {
                var current = await QueryAsync(false, token);
                var previous = current.Purchases.Where(p => p.TransactionId == request.ReplacesPurchaseToken).ToArray();
                if (!current.Confirmed || previous.Length != 1)
                    return new(PurchaseStatus.Unavailable, "The original store subscription could not be confirmed. Restore or check subscription status before changing plan.");
                product.SetSubscriptionProductReplacementParams(BillingFlowParams.ProductDetailsParams.SubscriptionProductReplacementParams.NewBuilder()
                    .SetOldProductId(previous[0].ProductId)
                    .SetReplacementMode(SubscriptionReplacementPolicy.GoogleMode(previous[0].ProductId == request.ProductId, request.DeferredReplacement)).Build());
                flow.SetSubscriptionUpdateParams(BillingFlowParams.SubscriptionUpdateParams.NewBuilder().SetOldPurchaseToken(request.ReplacesPurchaseToken).Build());
            }
            flow.SetProductDetailsParamsList([product.Build()]);
            var result = await MainThread.InvokeOnMainThreadAsync(() => client.LaunchBillingFlow(
                Microsoft.Maui.ApplicationModel.Platform.CurrentActivity ?? throw new InvalidOperationException("No active purchase activity."), flow.Build()));
            if (result.ResponseCode == BillingResponseCode.ItemAlreadyOwned) return new(PurchaseStatus.AlreadyOwned);
            if (result.ResponseCode != BillingResponseCode.Ok) return new(PurchaseStatus.Failed, result.DebugMessage);
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
            BillingResponseCode.Ok when purchases?.Any(p => p.PurchaseState == PurchaseState.Purchased) == true => PurchaseStatus.Purchased,
            BillingResponseCode.Ok => PurchaseStatus.Unverified,
            _ => PurchaseStatus.Failed
        };
        purchase?.TrySetResult(new(status, result.ResponseCode == BillingResponseCode.Ok ? null : result.DebugMessage));
        PurchasesChanged?.Invoke(this, EventArgs.Empty);
    }
    public Task CompleteAsync(SubscriptionPurchase purchase, CancellationToken token)
    {
        // The backend acknowledges Google purchases after durable delivery. Do not race a second device acknowledgement.
        return Task.CompletedTask;
    }
    public async Task OpenManagementAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        await Launcher.Default.OpenAsync("https://play.google.com/store/account/subscriptions?package=" + Uri.EscapeDataString(Android.App.Application.Context.PackageName ?? ""));
    }
    void IDisposable.Dispose() { client.EndConnection(); client.Dispose(); base.Dispose(); }
}
