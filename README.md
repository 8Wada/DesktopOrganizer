# DesktopOrganizer

A WinUI 3 desktop app for Windows that tidies up a cluttered desktop by sorting files and folders into categorized subfolders.

## Features

- Scans a folder (defaults to your Windows Desktop) and previews what will move before touching anything
- Classifies files by extension into categories: Documentos, Excel_y_Datos, Comprimidos, Instaladores, Juegos, Codigo, Scripts, Fuentes, Audio, Video, Imagenes, Logos, and Varios for anything unmatched
- Leaves shortcuts (`.lnk`, `.url`) in place at the root
- Lets you pick which items to move, ignore specific folders, rename the destination base folder, and choose a different source folder
- Shows move progress and a summary of successes/errors

## Requirements

- Windows 10 1809 (build 17763) or later
- [.NET 8 SDK](https://dotnet.microsoft.com/download) with Windows App SDK workload

## Build & Run

```powershell
dotnet build
dotnet run
```

Or open `DesktopOrganizer.csproj` in Visual Studio 2022+ and run.

## How it works

1. Click **Escanear** to scan the source folder and preview the proposed organization.
2. Review and adjust the selection of items to move.
3. Click **Organizar** to move the selected items into `Archivos Varios/<Category>/` (or your configured base folder) inside the source folder.

Classification rules live in [Services/OrganizerEngine.cs](Services/OrganizerEngine.cs).
