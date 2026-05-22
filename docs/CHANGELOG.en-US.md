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
- Added Vex folder publish profiles for `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, and `osx-arm64`.
- Added the root `publish_vex_all.bat` script to run all Vex publish profiles.
- Added trimming roots to preserve Vex, Avalonia, Prism, ReactiveUI, CodeWF, and SVG rendering assemblies for trimmed publishes.
- Added unsaved document indicators in the window title and status bar, with the current file encoding shown in the status bar.
- Added recent files, clearing recent files, quick open, and close current document actions to the File menu.
- Added a find/replace bar with find next, replace next, replace all, and `Ctrl+F`, `Ctrl+H`, `F3`, and `Esc` shortcuts.
- Added HTML export for the current Markdown document.
- Added clear formatting support for common Markdown markers in the current selection or line.
- Added a floating word-count panel with word, character, line, encoding, and saved-state details.
- Added an about panel with Vex, its Chinese name, author, CodeWF, and official website details.
- Added print preview by generating temporary HTML and opening it with the system browser.
- Added a delete confirmation overlay that warns about permanent deletion and shows the target file path.
- Added menu shortcut hints and window-level shortcuts for new, open, save, save as, print, close, full screen, and zoom actions.
- Added startup file opening when Vex receives a file path from the command line or the operating system Open With flow.
- Added startup folder opening, loading the sidebar document list and opening the first Markdown file.

### Changed

- Switched the solution file to `.slnx`.
- Moved CodeWF dependencies to NuGet package references.
- Switched UI theming to Semi.Avalonia and Ursa.Semi while keeping the open Avalonia.Themes.Fluent package for AvaloniaEdit.
- Removed CommunityToolkit.Mvvm, moved the shell ViewModel to ReactiveUI, and bound menu actions directly to public methods.
- Registered CodeWF.EventBus through DryIoc and handled editor actions/navigation with `[EventHandler]` methods.
- Added Windows AOT/Win7 and Linux/macOS self-contained single-file publish settings.
- Extracted shared publish profile settings into `FolderProfile.Common.props`, unified outputs under root `publish\<RuntimeIdentifier>\`, and enabled trimming for non-Windows self-contained single-file publishes.
- Removed the NU1904 warning from old `System.Drawing.Common` resolution through central transitive pinning.
- Pinned the transitive `Avalonia.Markup.Xaml.Loader` dependency to Avalonia 12.0.3 to avoid publishing an older runtime loader.
- Improved the properties action to show document state, encoding, size, and path details.
- Restored the startup status bar text to Ready and added empty states for the files and outline sidebar tabs.
- Updated the view menu so outline/files reveal the sidebar, while source mode temporarily hides the sidebar and preview and restores them when toggled off.
- Tested `Vex.slnx` build, dependency vulnerability scanning, desktop smoke startup, and the `win-x64` Release Native AOT plus `linux-x64` self-contained single-file publish paths.
- Tested `publish_vex_all.bat` and confirmed all five Vex publish profiles complete successfully.
- Tested `Vex.slnx` build for the find/replace changes and captured a desktop screenshot for the base window layout.
- Captured a desktop screenshot to verify the default sidebar empty state, three-pane layout, and Ready status text.
- Checked Markdig's BSD-2-Clause license, built `Vex.slnx`, and ran NuGet vulnerability scanning.
- Built `Vex.slnx` to verify the clear-formatting editor action.
- Built `Vex.slnx` to verify the view-mode toggle behavior.
- Built `Vex.slnx` to verify the word-count panel.
- Built `Vex.slnx` to verify the about panel.
- Built `Vex.slnx` to verify the print-preview path.
- Built `Vex.slnx` to verify the delete confirmation overlay.
- Built `Vex.slnx` to verify the shortcut bindings.
- Built `Vex.slnx` and screenshot-verified startup opening with a temporary Markdown file path argument.
- Screenshot-verified startup opening with a temporary Markdown folder path argument.
