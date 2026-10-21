using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundboardLite.Core.Models;

namespace SoundboardLite.App.ViewModels;

public sealed partial class BoardItemViewModel : ObservableObject
{
    private readonly Func<BoardItemViewModel, Task> _delete;
    private readonly Func<BoardItemViewModel, string, Task> _rename;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editingName = string.Empty;

    public BoardItemViewModel(Board board, Func<BoardItemViewModel, Task> delete, Func<BoardItemViewModel, string, Task> rename)
    {
        Id = board.Id;
        _name = board.Name;
        _delete = delete;
        _rename = rename;
    }

    public Guid Id { get; }

    public ObservableCollection<ClipItemViewModel> Clips { get; } = [];

    [RelayCommand]
    private void BeginRename()
    {
        EditingName = Name;
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelRename() => IsEditing = false;

    [RelayCommand]
    private async Task CommitRenameAsync()
    {
        if (!IsEditing) return;
        IsEditing = false;

        var trimmed = EditingName.Trim();
        if (trimmed.Length == 0 || trimmed == Name) return;

        await _rename(this, trimmed);
    }

    [RelayCommand]
    private Task DeleteAsync() => _delete(this);
}
