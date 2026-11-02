using BrighterTools.MauiInAppBilling;
using Xunit;

public sealed class SubscriptionTests
{
    [Theory]
    [InlineData(true, false, 4)]
    [InlineData(true, true, 3)]
    [InlineData(false, false, 2)]
    [InlineData(false, true, 5)]
    public void Google_replacement_uses_supported_mode_for_product_relationship(bool sameProduct, bool deferred, int expected)
        => Assert.Equal(expected, SubscriptionReplacementPolicy.GoogleMode(sameProduct, deferred));
    private static readonly SubscriptionRegistration Registration = new("team", "team.apple", "team.google", "monthly");
    private static readonly SubscriptionPurchase Purchase = new("Apple", "team.apple", "transaction", "signed-proof", true);
    [Fact]
    public async Task Server_rejection_never_finishes_or_grants_a_purchase()
    {
        var store = new Store { Purchases = [Purchase] };
        using var service = new SubscriptionBillingService(store, new Delivery(false));
        await service.GetProductsAsync([Registration]);
        Assert.Equal(PurchaseStatus.Unverified, (await service.PurchaseAsync(new("team.apple", Guid.NewGuid(), "account"))).Status);
        Assert.Equal(0, store.Completions);
    }
    [Fact]
    public async Task Server_delivery_precedes_finish_and_failed_finish_is_recoverable()
    {
        var store = new Store { Purchases = [Purchase], FailCompletion = true };
        var delivery = new Delivery(true);
        using var service = new SubscriptionBillingService(store, delivery);
        await service.GetProductsAsync([Registration]);
        await Assert.ThrowsAsync<IOException>(() => service.RefreshAsync());
        Assert.Equal(1, delivery.Calls);
        store.FailCompletion = false;
        Assert.Equal(PurchaseStatus.Purchased, (await service.RefreshAsync()).Status);
        Assert.Equal(2, delivery.Calls);
        Assert.Equal(1, store.Completions);
    }
    [Fact]
    public async Task Pending_store_payment_is_not_delivered()
    {
        var store = new Store { Result = PurchaseStatus.Pending, Purchases = [Purchase] };
        var delivery = new Delivery(true);
        using var service = new SubscriptionBillingService(store, delivery);
        await service.GetProductsAsync([Registration]);
        Assert.Equal(PurchaseStatus.Pending, (await service.PurchaseAsync(new("team.apple", Guid.NewGuid(), "account"))).Status);
        Assert.Equal(0, delivery.Calls);
        Assert.Equal(0, store.Completions);
    }
    [Fact]
    public async Task Restore_preserves_proof_and_original_identity_for_server_binding()
    {
        var store = new Store { Purchases = [Purchase with { OriginalTransactionId = "original", AccountToken = "original-account" }] };
        var delivery = new Delivery(true);
        using var service = new SubscriptionBillingService(store, delivery);
        await service.GetProductsAsync([Registration]);
        await service.RefreshAsync(true);
        Assert.True(store.Restored);
        Assert.Equal("original-account", delivery.Last?.AccountToken);
        Assert.Equal("signed-proof", delivery.Last?.Proof);
    }
    [Fact]
    public async Task Unknown_products_and_missing_server_correlation_cannot_purchase()
    {
        var store = new Store();
        using var service = new SubscriptionBillingService(store, new Delivery(true));
        await service.GetProductsAsync([Registration]);
        Assert.Equal(PurchaseStatus.Unavailable, (await service.PurchaseAsync(new("unknown", Guid.NewGuid(), "account"))).Status);
        Assert.Equal(PurchaseStatus.Unavailable, (await service.PurchaseAsync(new("team.apple", Guid.Empty, "account"))).Status);
        Assert.Equal(0, store.PurchaseCalls);
    }
    [Fact]
    public async Task Empty_or_unconfirmed_store_state_does_not_revoke_server_entitlement()
    {
        var store = new Store();
        var delivery = new Delivery(true);
        using var service = new SubscriptionBillingService(store, delivery);
        await service.GetProductsAsync([Registration]);
        Assert.Equal(PurchaseStatus.Unavailable, (await service.RefreshAsync()).Status);
        Assert.Equal(0, delivery.Calls);
        Assert.Equal(0, store.Completions);
    }
    private sealed class Delivery(bool accepted) : ISubscriptionDelivery
    {
        public int Calls;
        public SubscriptionPurchase? Last;
        public Task<SubscriptionDeliveryResult> VerifyAndDeliverAsync(SubscriptionPurchase purchase, CancellationToken token)
        { Calls++; Last = purchase; return Task.FromResult(new SubscriptionDeliveryResult(accepted)); }
    }
    private sealed class Store : IStoreSubscriptionProvider
    {
        public string Provider => "Apple";
        public event EventHandler? PurchasesChanged { add { } remove { } }
        public IReadOnlyList<SubscriptionPurchase> Purchases = [];
        public PurchaseStatus Result = PurchaseStatus.Purchased;
        public int Completions;
        public int PurchaseCalls;
        public bool FailCompletion;
        public bool Restored;
        public Task<IReadOnlyList<SubscriptionProduct>> GetProductsAsync(IReadOnlyList<SubscriptionRegistration> products, CancellationToken token) => Task.FromResult<IReadOnlyList<SubscriptionProduct>>([]);
        public Task<PurchaseResult> PurchaseAsync(SubscriptionPurchaseRequest request, CancellationToken token) { PurchaseCalls++; return Task.FromResult(new PurchaseResult(Result)); }
        public Task<SubscriptionQuery> QueryAsync(bool restore, CancellationToken token) { Restored = restore; return Task.FromResult(new SubscriptionQuery(true, Purchases)); }
        public Task CompleteAsync(SubscriptionPurchase purchase, CancellationToken token) { if (FailCompletion) throw new IOException(); Completions++; return Task.CompletedTask; }
        public Task OpenManagementAsync(CancellationToken token) => Task.CompletedTask;
        public void Dispose() { }
    }
}
