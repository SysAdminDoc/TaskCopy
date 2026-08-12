using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TaskCopy.ViewModels;

/// <summary>
/// Backup/store encryption toggles and recent-clip cleanup. Password capture
/// remains in App; this partial owns the binding state and safe rollback hooks.
/// </summary>
public partial class SettingsViewModel
{
    [ObservableProperty]
    private bool _backupEncrypted;

    /// <summary>F49: App owns password capture; the VM exposes the toggle.</summary>
    public event EventHandler<bool>? ToggleBackupEncryptionRequested;

    [ObservableProperty]
    private bool _storeEncrypted;

    /// <summary>F30: App owns password capture and in-place store conversion.</summary>
    public event EventHandler<bool>? ToggleStoreEncryptionRequested;

    /// <summary>True while LoadFromStore is populating from disk.</summary>
    private bool _suppressEncryptionToggleEvent;
    private bool _suppressStoreEncryptionToggleEvent;

    partial void OnBackupEncryptedChanged(bool value)
    {
        if (_suppressEncryptionToggleEvent) return;
        ToggleBackupEncryptionRequested?.Invoke(this, value);
    }

    partial void OnStoreEncryptedChanged(bool value)
    {
        if (_suppressStoreEncryptionToggleEvent) return;
        ToggleStoreEncryptionRequested?.Invoke(this, value);
    }

    /// <summary>App calls this when backup password capture is cancelled.</summary>
    public void RevertBackupEncryptedBinding(bool actualValue)
    {
        if (BackupEncrypted == actualValue) return;
        _suppressEncryptionToggleEvent = true;
        try { BackupEncrypted = actualValue; }
        finally { _suppressEncryptionToggleEvent = false; }
    }

    public void RevertStoreEncryptedBinding(bool actualValue)
    {
        if (StoreEncrypted == actualValue) return;
        _suppressStoreEncryptionToggleEvent = true;
        try { StoreEncrypted = actualValue; }
        finally { _suppressStoreEncryptionToggleEvent = false; }
    }

    [RelayCommand]
    private void ClearRecentClips()
    {
        try
        {
            _db.ClearRecentClips();
            StatusMessage = "Recent clipboard items cleared.";
        }
        catch (Exception ex)
        {
            Services.CrashLog.Write("ClearRecentClips", ex);
            StatusMessage = $"Clear failed: {ex.Message}";
        }
    }
}
