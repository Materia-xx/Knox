using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Knox.App.Logic.Models;
using Knox.App.Logic.Mvvm;
using Knox.App.Logic.Storage;
using Knox.App.Services;
using Microsoft.Maui.Controls;

namespace Knox.App.ViewModels;

/// <summary>
/// Lists the user's registered connections and lets them add/edit/delete and pick
/// which one to browse. Each connection is the user's OWN single-tenant Entra
/// registration (Knox.App is not multi-tenant).
/// </summary>
public sealed class ConnectionsViewModel : BaseViewModel
{
    private readonly ConnectionStore _store;
    private readonly AppConfigStore _configStore;
    private readonly AppSession _session;
    private KnoxConnection? _selected;

    public ConnectionsViewModel(ConnectionStore store, AppConfigStore configStore, AppSession session)
    {
        _store = store;
        _configStore = configStore;
        _session = session;
        Title = "Connections";

        AddCommand = new AsyncRelayCommand(AddAsync);
        EditCommand = new AsyncRelayCommand<KnoxConnection>(EditAsync);
        DeleteCommand = new AsyncRelayCommand<KnoxConnection>(DeleteAsync);
    }

    public ObservableCollection<KnoxConnection> Connections { get; } = new();

    public bool HasNoConnections => Connections.Count == 0;

    public KnoxConnection? Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }

    /// <summary>Reload the list from storage (call on page appearing).</summary>
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            _session.Config = await _configStore.LoadAsync();
            var all = await _store.LoadAsync();
            Connections.Clear();
            foreach (var c in all)
            {
                Connections.Add(c);
            }

            OnPropertyChanged(nameof(HasNoConnections));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task AddAsync() =>
        Shell.Current.GoToAsync(nameof(Views.ConnectionEditPage));

    private Task EditAsync(KnoxConnection? connection)
    {
        if (connection is null)
        {
            return Task.CompletedTask;
        }

        return Shell.Current.GoToAsync($"{nameof(Views.ConnectionEditPage)}?id={connection.Id}");
    }

    private async Task DeleteAsync(KnoxConnection? connection)
    {
        if (connection is null)
        {
            return;
        }

        var confirm = _session.Config.SuppressWarnings ||
            await Shell.Current.DisplayAlertAsync(
                "Delete connection",
                $"Remove '{connection.Name}'? This only deletes the local registration, not anything in Azure.",
                "Delete", "Cancel");
        if (!confirm)
        {
            return;
        }

        await _store.DeleteAsync(connection.Id);
        Connections.Remove(connection);
        OnPropertyChanged(nameof(HasNoConnections));
    }
}
