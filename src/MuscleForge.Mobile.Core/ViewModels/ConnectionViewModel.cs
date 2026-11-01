using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MuscleForge.Mobile.Core.Networking;
using MuscleForge.Mobile.Core.Settings;

namespace MuscleForge.Mobile.Core.ViewModels;

public sealed class ConnectionViewModel : ObservableObject
{
    private readonly IApiStatusService api;
    private readonly IConnectionSettings settings;
    private string apiBaseUrl;
    private string status = "Enter your API address to check the connection.";
    private bool isBusy;

    public ConnectionViewModel(IApiStatusService api, IConnectionSettings settings)
    {
        this.api = api;
        this.settings = settings;
        apiBaseUrl = settings.ApiBaseUrl;
        CheckConnectionCommand = new AsyncRelayCommand(CheckConnectionAsync);
    }

    public string ApiBaseUrl
    {
        get => apiBaseUrl;
        set => SetProperty(ref apiBaseUrl, value);
    }

    public string Status
    {
        get => status;
        private set => SetProperty(ref status, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    public IAsyncRelayCommand CheckConnectionCommand { get; }

    private async Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        if (!ApiEndpoint.TryParse(ApiBaseUrl, out var endpoint))
        {
            Status = "Enter an HTTPS API address without credentials, a query, or a fragment.";
            return;
        }

        IsBusy = true;
        Status = "Checking the API…";
        try
        {
            settings.ApiBaseUrl = endpoint!.AbsoluteUri;
            var response = await api.GetStatusAsync(endpoint, cancellationToken);
            Status = response.Service == "MuscleForge" && response.ApiVersion == "v1"
                ? "Connected to MuscleForge API v1."
                : "This server is not a compatible MuscleForge API.";
        }
        catch (OperationCanceledException)
        {
            Status = cancellationToken.IsCancellationRequested
                ? "Connection check cancelled."
                : "The API took too long to respond. Try again.";
        }
        catch (HttpRequestException)
        {
            Status = "Could not reach the API. Check the address, network, and trusted HTTPS certificate.";
        }
        catch (System.Text.Json.JsonException)
        {
            Status = "The server returned an unexpected response.";
        }
        catch (Exception)
        {
            Status = "Could not save settings or check the connection. Try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
