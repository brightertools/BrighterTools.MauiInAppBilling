namespace BillingDemo;
public partial class App : Application
{
    private readonly MainPage page;
    public App(MainPage page) { InitializeComponent(); this.page=page; }
    protected override Window CreateWindow(IActivationState? state) => new(page) {Title="Billing demo",Width=480,Height=650};
}