using BrighterTools.MauiInAppBilling;
namespace BillingDemo;
public sealed class MainPage : ContentPage
{
    private readonly IBillingService billing;
    private readonly Label status=new();
    private readonly Label ownership=new();
    private readonly VerticalStackLayout controls=new(){Padding=20,Spacing=12};
    private bool busy;
    public MainPage(IBillingService billing,IServiceProvider services)
    {
        this.billing=billing;
        controls.Add(new Label {Text="Permanent upgrade demo",FontSize=24});
#if DEMO_STORE
        controls.Add(new Label {Text="LIVE STORE MODE — purchases can charge your store account. Use store test accounts."});
#else
        controls.Add(new Label {Text="SIMULATION — no charges, accounts or real entitlements."});
#endif
        controls.Add(ownership);
        Button Action(string text,Func<Task> work)
        {
            var button=new Button {Text=text};
            button.Clicked+=async(_,_)=>{if(busy)return;busy=true;controls.IsEnabled=false;try{await work();}catch(Exception ex){status.Text=ex.Message;}finally{busy=false;controls.IsEnabled=true;Update();}};
            controls.Add(button);return button;
        }
        Action("Load store price",async()=>status.Text=string.Join("\n",(await billing.GetProductsAsync()).Select(p=>p.Title+" · "+p.FormattedPrice)));
        Action("Purchase Pro",async()=>{var r=await billing.PurchaseAsync("pro");status.Text=r.Status+": "+r.Message;});
        Action("Refresh",async()=>status.Text=(await billing.RefreshEntitlementsAsync()).Message);
        Action("Restore purchases",async()=>status.Text=(await billing.RestorePurchasesAsync()).Message);
#if !DEMO_STORE
        var fake=services.GetRequiredService<FakeBillingProvider>();
        Action("Simulate pending purchase",()=>{fake.NextPurchase=PurchaseStatus.Pending;return Task.CompletedTask;});
        Action("Complete pending / grant",async()=>{fake.Owned=true;fake.NextPurchase=PurchaseStatus.Purchased;await billing.RefreshEntitlementsAsync();});
        Action("Simulate refund",async()=>{fake.Owned=false;await billing.RefreshEntitlementsAsync();});
        Action("Toggle store availability",async()=>{fake.Available=!fake.Available;await billing.RefreshEntitlementsAsync();});
        Action("Toggle finalization failure",()=>{fake.FailCompletion=!fake.FailCompletion;return Task.CompletedTask;});
#endif
        controls.Add(status); Content=new ScrollView {Content=controls};
        billing.EntitlementsChanged+=(_,_)=>MainThread.BeginInvokeOnMainThread(Update);
        Loaded+=async(_,_)=>{await billing.InitializeAsync();Update();};
    }
    private void Update()=>ownership.Text="Pro: "+(billing.Entitlements.Owns("pro")?"owned":"not owned")+" · "+(billing.Entitlements.IsCurrent?"current store result":"cached / unavailable");
}