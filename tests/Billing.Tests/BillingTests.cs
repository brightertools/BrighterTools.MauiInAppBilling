using BrighterTools.MauiInAppBilling;
using Xunit;
namespace Billing.Tests;
public sealed class BillingTests
{
    private static BillingOptions Options => new() { ApplicationId="tests", Products={new("pro","pro","pro","pro")} };
    [Theory][InlineData(PurchaseStatus.Cancelled)][InlineData(PurchaseStatus.Pending)][InlineData(PurchaseStatus.Failed)][InlineData(PurchaseStatus.Unverified)]
    public async Task Incomplete_purchase_never_grants_access(PurchaseStatus status)
    {
        var provider=new FakeBillingProvider {NextPurchase=status};
        using var service=new BillingService(provider,new MemoryBillingStateStore(),Options);
        Assert.Equal(status,(await service.PurchaseAsync("pro")).Status);
        Assert.False(service.Entitlements.Owns("pro")); Assert.Equal(0,provider.CompletionCount);
    }
    [Fact] public async Task Purchase_is_delivered_once_and_repeat_events_do_not_finish_again()
    {
        var provider=new FakeBillingProvider(); using var service=new BillingService(provider,new MemoryBillingStateStore(),Options);
        Assert.Equal(PurchaseStatus.Purchased,(await service.PurchaseAsync("pro")).Status);
        Assert.True(service.Entitlements.Owns("pro")); Assert.Equal(1,provider.CompletionCount);
        await Task.WhenAll(Enumerable.Range(0,10).Select(_=>service.RefreshEntitlementsAsync()));
        Assert.Equal(1,provider.CompletionCount);
    }
    [Fact] public async Task Offline_restart_preserves_secure_verified_cache_then_confirmed_refund_removes_access()
    {
        var state=new MemoryBillingStateStore();
        using(var first=new BillingService(new FakeBillingProvider {Owned=true},state,Options)) await first.InitializeAsync();
        var provider=new FakeBillingProvider {Available=false};
        using var next=new BillingService(provider,state,Options); await next.InitializeAsync();
        Assert.True(next.Entitlements.Owns("pro")); Assert.False(next.Entitlements.IsCurrent);
        provider.Available=true; await next.RefreshEntitlementsAsync();
        Assert.False(next.Entitlements.Owns("pro")); Assert.True(next.Entitlements.IsCurrent);
    }
    [Fact] public async Task Unverified_evidence_neither_grants_nor_erases_verified_ownership()
    {
        var provider=new FakeBillingProvider {Owned=true,Verified=false}; using var service=new BillingService(provider,new MemoryBillingStateStore(),Options);
        await service.InitializeAsync(); Assert.False(service.Entitlements.Owns("pro"));
        provider.Verified=true; await service.RefreshEntitlementsAsync(); Assert.True(service.Entitlements.Owns("pro"));
        provider.Verified=false; await service.RefreshEntitlementsAsync();
        Assert.True(service.Entitlements.Owns("pro")); Assert.False(service.Entitlements.IsCurrent);
    }
    [Fact] public async Task Finalization_failure_retries_after_restart_without_losing_purchase()
    {
        var state=new MemoryBillingStateStore(); var provider=new FakeBillingProvider {Owned=true,FailCompletion=true};
        using(var first=new BillingService(provider,state,Options))
        { await first.InitializeAsync(); Assert.True(first.Entitlements.Owns("pro")); Assert.Contains("retry",first.Entitlements.Message); }
        provider.FailCompletion=false;
        using var second=new BillingService(provider,state,Options); await second.InitializeAsync();
        Assert.True(second.Entitlements.Owns("pro")); Assert.Equal(1,provider.CompletionCount);
    }
    private sealed class BrokenState : IBillingStateStore
    {
        public Task<string?> ReadAsync(string key)=>Task.FromResult<string?>(null);
        public Task WriteAsync(string key,string value)=>throw new IOException("Disk failure");
    }
    [Fact] public async Task Failed_durable_delivery_does_not_acknowledge_or_grant()
    {
        var provider=new FakeBillingProvider {Owned=true}; using var service=new BillingService(provider,new BrokenState(),Options);
        await service.InitializeAsync(); Assert.False(service.Entitlements.Owns("pro")); Assert.Equal(0,provider.CompletionCount);
    }
    private sealed class Reject : IPurchaseVerifier
    { public Task<bool> VerifyAsync(string store,StorePurchase p,CancellationToken t)=>Task.FromResult(false); }
    [Fact] public async Task Optional_verifier_can_reject_store_verified_purchase()
    {
        var provider=new FakeBillingProvider {Owned=true}; using var service=new BillingService(provider,new MemoryBillingStateStore(),Options,new Reject());
        await service.InitializeAsync(); Assert.False(service.Entitlements.Owns("pro")); Assert.Equal(0,provider.CompletionCount);
    }
    [Fact] public async Task Standalone_cannot_purchase_or_restore_a_fake_entitlement()
    {
        using var service=new BillingService(new UnavailableBillingProvider(),new MemoryBillingStateStore(),Options);
        Assert.Equal(PurchaseStatus.Unavailable,(await service.PurchaseAsync("pro")).Status);
        Assert.False((await service.RestorePurchasesAsync()).Owns("pro"));
    }
    [Fact] public async Task Unknown_product_cannot_start_purchase()
    {
        var provider=new FakeBillingProvider(); using var service=new BillingService(provider,new MemoryBillingStateStore(),Options);
        Assert.Equal(PurchaseStatus.Unavailable,(await service.PurchaseAsync("unknown")).Status); Assert.False(provider.Owned);
    }
}
