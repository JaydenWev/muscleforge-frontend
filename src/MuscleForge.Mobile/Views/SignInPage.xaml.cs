using MuscleForge.Mobile.Core.ViewModels;

namespace MuscleForge.Mobile.Views;

public partial class SignInPage : ContentPage
{
    private readonly IServiceProvider services;
    public SignInPage(SignInViewModel viewModel, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = viewModel;
        this.services = services;
    }
    private async void OnServerSettings(object? sender, EventArgs e) =>
        await Navigation.PushAsync(services.GetRequiredService<ConnectionPage>());
}
