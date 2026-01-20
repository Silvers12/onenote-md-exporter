# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

OneNote Md Exporter is a Windows console application that exports OneNote notebooks to Markdown or Joplin formats. It uses COM Interop with OneNote/Word APIs and Pandoc for conversion.

**Requirements:** Windows 10+, OneNote 2013+, Word 2013+

## Build Commands

```bash
# Build (must use MSBuild, not dotnet build - COM references not supported)
msbuild src/OneNoteMdExporter.sln

# Or use Visual Studio / VS Code task (Ctrl+Shift+B)

# Release build (self-contained executable)
dotnet publish -c Release src/OneNoteMdExporter/OneNoteMdExporter.csproj
```

**Important:** `dotnet build` does not support COMReference. Use MSBuild or Visual Studio.

## Testing

```bash
# Run integration tests (requires OneNote installed)
dotnet test src/OneNoteMdExporter.IntTests/OneNoteMdExporter.IntTests.csproj
```

Tests are NUnit 4.1 integration tests that require OneNote to be installed and running.

## Architecture

```
Program.cs (CLI Entry - CommandLineParser)
    ↓
OneNoteApp (COM Interop)
    ↓
ExportServiceFactory
    ├→ MdExportService (Markdown export)
    └→ JoplinExportService (Joplin format)
        ↓
      ExportServiceBase (shared export logic)
        ├→ ConverterService (Pandoc integration)
        └→ AppSettings (static config from appSettings.json)
```

### Key Components

- **Models/**: Domain objects - `Node` base class with `Notebook→Section→Page` hierarchy
- **Services/Export/**: Export implementations - extend `ExportServiceBase` for new formats
- **Infrastructure/**: `AppSettings` (static config), `ExportServiceFactory`, `Localizer` (i18n: EN/FR/ES/ZH)
- **Helpers/**: Extension methods for paths, strings, OneNote objects
- **pandoc/**: Embedded Pandoc binary for DocX→Markdown conversion

### Configuration

Settings in `src/OneNoteMdExporter/appSettings.json` control export behavior:
- Page hierarchy handling (folder tree vs filename prefix)
- Resource folder location (root vs page-relative)
- OneNote link conversion (keep/markdown/wikilink/remove)
- Markdown format (Pandoc flavors like `gfm`)
- HTML styling preservation

Access via static `AppSettings.*` properties.

## Adding Features

### New Export Setting
1. Add property in `Infrastructure/AppSettings.cs`
2. Add default in `appSettings.json`
3. Use via `AppSettings.PropertyName` in export services

### New Export Format
1. Create class extending `ExportServiceBase`
2. Implement abstract methods (see `MdExportService.cs`, `JoplinExportService.cs`)
3. Register in `ExportServiceFactory.cs`

### New Translation
Copy `Resources/trad.en.json` to `trad.<lang>.json` and translate.

## CLI Usage

```bash
OneNoteMdExporter.exe --notebook "Name" --format 1  # 1=Markdown, 2=Joplin
OneNoteMdExporter.exe --help
```
