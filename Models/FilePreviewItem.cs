using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;

namespace DesktopOrganizer.Models;

public class FilePreviewItem : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string DestinationFolder { get; set; } = "";
    public string DestinationFullPath { get; set; } = "";
    public bool WillMove { get; set; }
    public bool IsFolder { get; set; }
    public string Icon { get; set; } = "📄";

    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    // Propiedades computadas para x:Bind
    public string DestinationDisplay => WillMove ? DestinationFolder : "queda en raíz";
    public Visibility ArrowVisibility => WillMove ? Visibility.Visible : Visibility.Collapsed;
    public double NameOpacity => WillMove ? 1.0 : 0.50;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
