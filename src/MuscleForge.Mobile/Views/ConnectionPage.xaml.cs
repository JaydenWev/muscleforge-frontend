using MuscleForge.Mobile.Core.ViewModels;
using MuscleForge.Mobile.Services;

namespace MuscleForge.Mobile.Views;

public partial class ConnectionPage : ContentPage
{
    public ConnectionPage(ConnectionViewModel viewModel, AppAppearance appearance)
    {
        InitializeComponent();
        BindingContext = viewModel;
        AppearanceOptions.Initialize(appearance);
        VersionLabel.Text = "Made for the work you put in.  ·  v" + AppInfo.Current.VersionString;
    }
}
