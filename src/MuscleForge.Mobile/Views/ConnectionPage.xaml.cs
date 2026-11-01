using MuscleForge.Mobile.Core.ViewModels;

namespace MuscleForge.Mobile.Views;

public partial class ConnectionPage : ContentPage
{
    public ConnectionPage(ConnectionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
