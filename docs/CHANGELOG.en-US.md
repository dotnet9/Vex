# Changelog

## 0.1.0 - 2026-05-22

### Added

- Created the initial Vex Markdown editor.
- Added author, CodeWF, and official website metadata.
- Added a Typora-inspired title-bar menu, file/outline sidebar, Markdown editor, Markdown preview, and status bar.
- Added file actions for new, open, open folder, save, save as, delete, and reveal in file manager.
- Added initial commands for edit, paragraph, format, view, theme, language, and help menus.
- Added theme variant, typography theme, compact layout, and language switching entry points.
- Added `Vex.Controls` and `Vex.Controls.Themes` packages for Vex-specific controls and themes.
- Added outline navigation to jump to the matching editor heading line.
- Added help menu actions for opening bundled changelog, quick start, and acknowledgements documents.

### Changed

- Switched the solution file to `.slnx`.
- Moved CodeWF dependencies to NuGet package references.
- Switched UI theming to Semi.Avalonia and Ursa.Semi while keeping the open Avalonia.Themes.Fluent package for AvaloniaEdit.
- Removed CommunityToolkit.Mvvm, moved the shell ViewModel to ReactiveUI, and bound menu actions directly to public methods.
- Registered CodeWF.EventBus through DryIoc and handled editor actions/navigation with `[EventHandler]` methods.
- Added Windows AOT/Win7 and Linux/macOS self-contained single-file publish settings.
- Removed the NU1904 warning from old `System.Drawing.Common` resolution through central transitive pinning.
- Tested `Vex.slnx` build, dependency vulnerability scanning, desktop smoke startup, and the `win-x64` Release Native AOT plus `linux-x64` self-contained single-file publish paths.
