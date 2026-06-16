using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesktopOrganizer.Models;

namespace DesktopOrganizer.Services;

public record OrganizationRule(string[] Extensions, string Destination, string Icon);

public class OrganizerEngine
{
    public string DesktopPath { get; set; }
    public string BaseFolderName { get; set; } = "Archivos Varios";
    public HashSet<string> IgnoredFolders { get; set; }

    private static readonly List<OrganizationRule> Rules =
    [
        new([ ".pdf", ".docx", ".doc", ".odt", ".pptx", ".ppt", ".rtf" ],
            "Documentos", "📄"),
        new([ ".xlsx", ".xls", ".xlsm", ".csv", ".ods" ],
            "Excel_y_Datos", "📊"),
        new([ ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2" ],
            "Comprimidos", "📦"),
        new([ ".exe", ".msi", ".msix", ".appx", ".appinstaller" ],
            "Instaladores", "⚙️"),
        new([ ".nsp", ".xci", ".nsz", ".xcz" ],
            "Juegos", "🎮"),
        new([ ".json", ".xml", ".yaml", ".yml", ".toml", ".cfg", ".env", ".cer", ".winmd" ],
            "Codigo", "💻"),
        new([ ".bat", ".cmd", ".ps1", ".sh" ],
            "Scripts", "📜"),
        new([ ".ttf", ".otf", ".woff", ".woff2" ],
            "Fuentes", "🔤"),
        new([ ".mp3", ".wav", ".flac", ".m3u", ".aac", ".ogg" ],
            "Audio", "🎵"),
        new([ ".mp4", ".mkv", ".avi", ".mov", ".wmv" ],
            "Video", "🎬"),
        new([ ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg", ".ico" ],
            "Imagenes", "🖼️"),
    ];

    private static readonly HashSet<string> RootExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".lnk", ".url" };

    public OrganizerEngine()
    {
        DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        IgnoredFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SICEUC", "colsoft", "UCOL", "Innovardix",
            "Archivos Varios", ".claude", "DesktopOrganizer"
        };
    }

    public List<FilePreviewItem> Scan()
    {
        var items = new List<FilePreviewItem>();
        var baseFolder = Path.Combine(DesktopPath, BaseFolderName);

        foreach (var file in Directory.GetFiles(DesktopPath))
        {
            var name = Path.GetFileName(file);
            var ext = Path.GetExtension(file).ToLowerInvariant();

            var attrs = File.GetAttributes(file);
            if (attrs.HasFlag(FileAttributes.System)) continue;

            if (RootExtensions.Contains(ext))
            {
                items.Add(new FilePreviewItem
                {
                    Name = name,
                    FullPath = file,
                    WillMove = false,
                    Icon = ext == ".lnk" ? "🔗" : "🌐",
                    IsSelected = false,
                });
                continue;
            }

            var (dest, icon) = Classify(name, ext);
            var destPath = Path.Combine(baseFolder, dest, name);

            items.Add(new FilePreviewItem
            {
                Name = name,
                FullPath = file,
                WillMove = true,
                Icon = icon,
                DestinationFolder = dest,
                DestinationFullPath = destPath,
            });
        }

        foreach (var dir in Directory.GetDirectories(DesktopPath))
        {
            var name = Path.GetFileName(dir)!;
            if (IgnoredFolders.Contains(name)) continue;
            if (name.StartsWith('.')) continue;

            items.Add(new FilePreviewItem
            {
                Name = name,
                FullPath = dir,
                WillMove = true,
                IsFolder = true,
                Icon = "📂",
                DestinationFolder = "Codigo",
                DestinationFullPath = Path.Combine(baseFolder, "Codigo", name),
            });
        }

        return items
            .OrderBy(i => !i.WillMove)
            .ThenBy(i => i.DestinationFolder)
            .ThenBy(i => i.Name)
            .ToList();
    }

    private static (string destination, string icon) Classify(string fileName, string ext)
    {
        // Logos special case
        if (fileName.Contains("logo", StringComparison.OrdinalIgnoreCase)
            && (ext is ".png" or ".jpg" or ".jpeg" or ".svg" or ".webp"))
            return ("Logos", "🏷️");

        var rule = Rules.FirstOrDefault(r =>
            r.Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase));

        return rule is not null
            ? (rule.Destination, rule.Icon)
            : ("Varios", "📁");
    }

    public void Execute(
        IEnumerable<FilePreviewItem> items,
        Action<int, int, string> onProgress,
        out int moved, out int errors, out List<string> errorLog)
    {
        var list = items.Where(i => i.WillMove && i.IsSelected).ToList();
        moved = 0;
        errors = 0;
        errorLog = [];

        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            onProgress(i + 1, list.Count, item.Name);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(item.DestinationFullPath)!);

                if (item.IsFolder)
                    Directory.Move(item.FullPath, item.DestinationFullPath);
                else
                    File.Move(item.FullPath, item.DestinationFullPath, overwrite: false);

                moved++;
            }
            catch (Exception ex)
            {
                errors++;
                errorLog.Add($"{item.Name}: {ex.Message}");
            }
        }
    }
}
