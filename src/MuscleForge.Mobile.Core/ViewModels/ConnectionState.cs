namespace MuscleForge.Mobile.Core.ViewModels;

public enum ConnectionState
{
    NotConfigured,
    Ready,
    Checking,
    Connected,
    InvalidAddress,
    Unavailable,
    Incompatible,
    Error
}
