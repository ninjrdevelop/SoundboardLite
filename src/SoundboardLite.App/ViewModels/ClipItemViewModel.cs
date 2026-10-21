using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundboardLite.Core.Models;

namespace SoundboardLite.App.ViewModels;

public sealed partial class ClipItemViewModel : ObservableObject
{
    private readonly Func<Guid, Task> _trigger;
    private readonly Action<Guid> _stop;
    private readonly Action<ClipItemViewModel> _edit;

    [ObservableProperty]
    private string _name;

    public ClipItemViewModel(Clip clip, Func<Guid, Task> trigger, Action<Guid> stop, Action<ClipItemViewModel> edit)
    {
        Clip = clip;
        Id = clip.Id;
        _name = clip.Name;
        _trigger = trigger;
        _stop = stop;
        _edit = edit;
    }

    public Guid Id { get; }
    public Clip Clip { get; }

    [RelayCommand]
    private Task PlayAsync() => _trigger(Id);

    [RelayCommand]
    private void Stop() => _stop(Id);

    [RelayCommand]
    private void Edit() => _edit(this);
}
