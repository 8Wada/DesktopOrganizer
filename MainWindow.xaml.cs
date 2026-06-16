using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DesktopOrganizer.Models;
using DesktopOrganizer.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace DesktopOrganizer;

public sealed partial class MainWindow : Window
{
    private readonly OrganizerEngine _engine = new();
    public ObservableCollection<FilePreviewItem> PreviewItems { get; } = [];

    public MainWindow()
    {
        InitializeComponent();

        // Título y barra personalizada
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Tamaño inicial
        AppWindow.Resize(new Windows.Graphics.SizeInt32(820, 660));
        AppWindow.SetIcon(null); // ícono predeterminado

        // Estado inicial
        PathText.Text = _engine.DesktopPath;
        IgnoredFoldersBox.Text = string.Join(", ", _engine.IgnoredFolders);
        BaseFolderBox.Text = _engine.BaseFolderName;
        StatusText.Text = "Listo para escanear";
    }

    // ── Cambiar carpeta ──────────────────────────────────────────────────────

    private async void ChangeFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.Desktop };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;

        _engine.DesktopPath = folder.Path;
        PathText.Text = folder.Path;
        ClearResults();
    }

    // ── Escanear ─────────────────────────────────────────────────────────────

    private void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        StatusBar.IsOpen = false;
        ScanButton.IsEnabled = false;

        try
        {
            var items = _engine.Scan();

            PreviewItems.Clear();
            foreach (var item in items)
                PreviewItems.Add(item);

            int toMove = items.Count(i => i.WillMove);
            int staying = items.Count(i => !i.WillMove);

            StatsText.Text = toMove > 0
                ? $"{toMove} para mover · {staying} accesos directos sin cambios"
                : $"Sin archivos para organizar · {staying} accesos directos";

            EmptyState.Visibility = Visibility.Collapsed;
            SettingsPanel.Visibility = Visibility.Collapsed;
            SettingsToggle.IsChecked = false;
            ResultsPanel.Visibility = Visibility.Visible;
            StatsBar.Visibility = Visibility.Visible;

            OrganizeButton.IsEnabled = toMove > 0;
            StatusText.Text = $"{items.Count} elementos encontrados";
        }
        catch (Exception ex)
        {
            ShowMessage(InfoBarSeverity.Error, "Error al escanear", ex.Message);
        }
        finally
        {
            ScanButton.IsEnabled = true;
        }
    }

    // ── Selección ────────────────────────────────────────────────────────────

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in PreviewItems.Where(i => i.WillMove))
            item.IsSelected = true;
    }

    private void DeselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in PreviewItems)
            item.IsSelected = false;
    }

    // ── Panel de ajustes ─────────────────────────────────────────────────────

    private void SettingsToggle_Checked(object sender, RoutedEventArgs e)
    {
        ResultsPanel.Visibility = Visibility.Collapsed;
        SettingsPanel.Visibility = Visibility.Visible;
    }

    private void SettingsToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = Visibility.Collapsed;
        if (PreviewItems.Count > 0)
            ResultsPanel.Visibility = Visibility.Visible;
        else
            EmptyState.Visibility = Visibility.Visible;
    }

    private void IgnoredFoldersBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _engine.IgnoredFolders = new System.Collections.Generic.HashSet<string>(
            IgnoredFoldersBox.Text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
    }

    private void BaseFolderBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(BaseFolderBox.Text))
            _engine.BaseFolderName = BaseFolderBox.Text.Trim();
    }

    // ── Organizar ────────────────────────────────────────────────────────────

    private async void OrganizeButton_Click(object sender, RoutedEventArgs e)
    {
        var toMove = PreviewItems.Where(i => i.WillMove && i.IsSelected).ToList();
        if (toMove.Count == 0)
        {
            ShowMessage(InfoBarSeverity.Warning, "Sin elementos",
                "No hay archivos seleccionados para mover.");
            return;
        }

        // Deshabilitar controles
        SetControlsEnabled(false);
        ProgressSection.Visibility = Visibility.Visible;
        OrganizeProgress.Value = 0;
        StatusBar.IsOpen = false;

        int moved = 0, errors = 0;
        System.Collections.Generic.List<string> errorLog = [];

        var progress = new Progress<(int current, int total, string name)>(p =>
        {
            OrganizeProgress.Value = (double)p.current / p.total * 100;
            ProgressLabel.Text = $"Moviendo {p.current} de {p.total}:  {p.name}";
        });

        await Task.Run(() =>
        {
            var p = (IProgress<(int, int, string)>)progress;
            for (int i = 0; i < toMove.Count; i++)
            {
                var item = toMove[i];
                p.Report((i + 1, toMove.Count, item.Name));
                try
                {
                    System.IO.Directory.CreateDirectory(
                        System.IO.Path.GetDirectoryName(item.DestinationFullPath)!);

                    if (item.IsFolder)
                        System.IO.Directory.Move(item.FullPath, item.DestinationFullPath);
                    else
                        System.IO.File.Move(item.FullPath, item.DestinationFullPath, overwrite: false);

                    moved++;
                }
                catch (Exception ex)
                {
                    errors++;
                    errorLog.Add($"• {item.Name}: {ex.Message}");
                }
            }
        });

        // Resultado
        OrganizeProgress.Value = 100;
        ProgressLabel.Text = "Completado";

        if (errors == 0)
        {
            ShowMessage(InfoBarSeverity.Success, "¡Listo!",
                $"Se movieron {moved} elemento{(moved != 1 ? "s" : "")} correctamente.");
        }
        else
        {
            string detail = string.Join("\n", errorLog.Take(5));
            ShowMessage(InfoBarSeverity.Error, $"{moved} movidos, {errors} con error", detail);
        }

        StatusText.Text = $"Última ejecución: {moved} movidos, {errors} errores";

        SetControlsEnabled(true);

        // Re-escanear después de un momento
        await Task.Delay(800);
        ProgressSection.Visibility = Visibility.Collapsed;
        if (moved > 0) ScanButton_Click(sender, e);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void ClearResults()
    {
        PreviewItems.Clear();
        ResultsPanel.Visibility = Visibility.Collapsed;
        StatsBar.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Visible;
        OrganizeButton.IsEnabled = false;
        StatusBar.IsOpen = false;
        StatusText.Text = "Listo para escanear";
    }

    private void SetControlsEnabled(bool enabled)
    {
        OrganizeButton.IsEnabled = enabled && PreviewItems.Any(i => i.WillMove && i.IsSelected);
        ScanButton.IsEnabled = enabled;
        ChangeFolderButton.IsEnabled = enabled;
    }

    private void ShowMessage(InfoBarSeverity severity, string title, string message)
    {
        StatusBar.Severity = severity;
        StatusBar.Title = title;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }
}
