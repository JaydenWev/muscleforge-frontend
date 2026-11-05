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
    private ConnectionState state;

    public ConnectionViewModel(IApiStatusService api, IConnectionSettings settings)
    {
        this.api = api;
        this.settings = settings;
        apiBaseUrl = settings.ApiBaseUrl;
        state = string.IsNullOrWhiteSpace(apiBaseUrl) ? ConnectionState.NotConfigured : ConnectionState.Ready;
        if (state == ConnectionState.Ready)
            status = "Your server address is saved. Check the connection when you're ready.";
        CheckConnectionCommand = new AsyncRelayCommand(CheckConnectionAsync);
    }

    public string ApiBaseUrl
    {
        get => apiBaseUrl;
        set
        {
            if (!SetProperty(ref apiBaseUrl, value)) return;
            State = string.IsNullOrWhiteSpace(value) ? ConnectionState.NotConfigured : ConnectionState.Ready;
            Status = "Save your server address to check the connection.";
            OnPropertyChanged(nameof(ServerHost));
        }
    }

    public string Status
    {
        get => status;
        private set => SetProperty(ref status, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value)) OnPropertyChanged(nameof(ActionText));
        }
    }

    public ConnectionState State
    {
        get => state;
        private set
        {
            if (!SetProperty(ref state, value)) return;
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(ConnectionLabel));
            OnPropertyChanged(nameof(ConnectionTitle));
            OnPropertyChanged(nameof(ActionText));
        }
    }

    public bool IsConnected => State == ConnectionState.Connected;
    public string ActionText => IsBusy ? "Checking connection…" : IsConnected ? "Check again" : "Save & connect";
    public string ServerHost => Uri.TryCreate(ApiBaseUrl?.Trim(), UriKind.Absolute, out var endpoint)
        && endpoint.Scheme == Uri.UriSchemeHttps ? endpoint.Host : "No server added yet";
    public string ConnectionLabel => State switch
    {
        ConnectionState.Connected => "Connected",
        ConnectionState.Checking => "Checking",
        ConnectionState.Ready => "Not checked",
        ConnectionState.NotConfigured => "Not connected",
        _ => "Needs attention"
    };
    public string ConnectionTitle => State switch
    {
        ConnectionState.Connected => "All set. You're connected.",
        ConnectionState.Checking => "Making the connection…",
        ConnectionState.Ready => "Ready when you are.",
        ConnectionState.NotConfigured => "Let's get you connected.",
        ConnectionState.InvalidAddress => "Give that address another look.",
        ConnectionState.Incompatible => "A different server answered.",
        _ => "Let's try that again."
    };

    public IAsyncRelayCommand CheckConnectionCommand { get; }

    private async Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        if (!ApiEndpoint.TryParse(ApiBaseUrl, out var endpoint))
        {
            State = ConnectionState.InvalidAddress;
            Status = "Enter an HTTPS API address without credentials, a query, or a fragment.";
            return;
        }

        IsBusy = true;
        State = ConnectionState.Checking;
        Status = "Checking the API…";
        try
        {
            settings.ApiBaseUrl = endpoint!.AbsoluteUri;
            var response = await api.GetStatusAsync(endpoint, cancellationToken);
            var compatible = response.Service == "MuscleForge" && response.ApiVersion == "v1";
            State = compatible ? ConnectionState.Connected : ConnectionState.Incompatible;
            Status = compatible
                ? "Connected to MuscleForge API v1."
                : "This server is not a compatible MuscleForge API.";
        }
        catch (OperationCanceledException)
        {
            State = ConnectionState.Unavailable;
            Status = cancellationToken.IsCancellationRequested
                ? "Connection check cancelled."
                : "The API took too long to respond. Try again.";
        }
        catch (HttpRequestException)
        {
            State = ConnectionState.Unavailable;
            Status = "Could not reach the API. Check the address, network, and trusted HTTPS certificate.";
        }
        catch (System.Text.Json.JsonException)
        {
            State = ConnectionState.Error;
            Status = "The server returned an unexpected response.";
        }
        catch (Exception)
        {
            State = ConnectionState.Error;
            Status = "Could not save settings or check the connection. Try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
