# TaskCopy — Architecture Research

**Date:** 2026-05-23
**Goal:** Identify the best architecture for a Windows clipboard snippet tool with single-click copy from a right-click menu near the taskbar.

---

## 1. Executive Summary

**Literally extending the Windows 11 taskbar right-click menu is not viable for a shippable product.** The Win11 taskbar is a XAML Islands surface inside `explorer.exe`; the only injection path is hooking `TaskbarResources::OnTaskListButtonContextRequested` in `Taskbar.View.dll` — fragile across Windows updates and requires Windhawk pre-installed.

**Recommended v1 architecture:** tray icon (`Shell_NotifyIcon`) + WPF popup flyout opened at the cursor on global hotkey **and** on tray right-click — same UX archetype Ditto, CopyQ, and ClipClip ship. Built in **C# / .NET 9 WPF** with `H.NotifyIcon.Wpf` + `NHotkey.Wpf` + SQLite.

**Optional v0.2+ companion:** a Windhawk mod (`taskcopy-taskbar-menu`) that hooks the actual taskbar context menu and IPCs the WPF app via named pipe. Ships as a power-user add-on, not the primary install.

---

## 2. The Windows 11 Taskbar Reality

| Fact | Implication |
|---|---|
| Taskbar still hosted by `explorer.exe` | Same process trust boundary as Win10 |
| Win11 22H2+ UI rewritten in XAML Islands, lives in `Taskbar.View.dll` / `ExplorerExtensions.dll` | No COM extension surface |
| Legacy `CTaskListWnd` code still present but mostly dormant | ExplorerPatcher exploits this by COM-swapping back to it |
| No `IExplorerCommand`-equivalent for taskbar | Microsoft only shipped the modern menu API for File Explorer |
| `IContextMenu` / `IShellExtInit` target shell items (files/folders) | Wrong surface for taskbar |
| `IDeskBand` removed | No taskbar toolbars / Quick Launch |
| Drag-drop to taskbar removed | API freeze confirmed |
| `Win+V` reserved by built-in clipboard history | Cannot reliably override without low-level keyboard hook (AV-flagged) |

**Conclusion:** every survivor in the clipboard-manager landscape (Ditto, CopyQ, ClipClip, ClipboardFusion) uses **tray + global hotkey**, not the taskbar context menu, because Microsoft made the direct path require injection.

---

## 3. Existing OSS Projects

| Rank | Project | Stack | License | Relevance |
|---|---|---|---|---|
| 1 | [Windhawk](https://github.com/ramensoftware/windhawk) + [`taskbar-classic-menu` mod](https://windhawk.net/mods/taskbar-classic-menu) | C++23 / MinGW Clang 20 | GPL-3.0 engine, MIT mods | Template for any future taskbar-menu mod |
| 2 | [ExplorerPatcher](https://github.com/valinet/ExplorerPatcher) | C/C++ | GPL-2.0 | Proves COM-swap approach; not a library |
| 3 | [CopyQ](https://github.com/hluk/CopyQ) | C++ / Qt | GPL-3.0 | Closest UX archetype — tray menu of clips with custom actions |
| 4 | [Ditto](https://github.com/sabrogden/Ditto) | C++ / MFC | GPL-3.0 | Gold-standard global-hotkey overlay (`Ctrl+\``); `QPasteClass` window |
| 5 | [H.NotifyIcon.Wpf](https://github.com/HavenDV/H.NotifyIcon) | C# / WPF/WinUI | MIT | **Critical dep** — modern tray icon w/ XAML `MenuFlyout` |
| 6 | [ClipboardZanager](https://github.com/veler/clipboardzanager) | C# / WPF | MIT | Reference for WPF clipboard hook |
| 7 | [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon) | C# / WPF | CPOL | Inactive — superseded by H.NotifyIcon |
| 8 | [TrayToolbar](https://github.com/rojarsmith/TrayToolbar) | C# | MIT | Replaces dead Quick Launch; low relevance |

**Adjacent Windhawk mods worth referencing if/when a companion ships:**
- [`classic-taskbar-context-menu`](https://windhawk.net/mods/classic-taskbar-context-menu) — strips immersive styling
- [`eradicate-immersive-menus`](https://windhawk.net/mods/eradicate-immersive-menus) — force classic `TrackPopupMenu` everywhere
- [`explorer-context-menu-classic`](https://windhawk.net/mods/explorer-context-menu-classic) — File Explorer menu rollback

---

## 4. Architecture Options (ranked)

### Winner — Tray icon + WPF flyout at cursor (hotkey + tray-click triggers)
- **Pros:** ships standalone, no injection, signed-EXE compatible, MSIX-able, Defender/SmartScreen clean, owns its UI surface, battle-tested by Ditto/CopyQ/ClipClip.
- **Cons:** not literally "right-click the taskbar" — UX compromise from original spec.
- **Mitigation:** default hotkey (`Ctrl+Alt+V`) + tray right-click opens the same flyout; v0.2 Windhawk mod can add the literal taskbar trigger for power users.

### 2nd — Windhawk companion mod (ship after v1 validates the copy flow)
- **Pros:** delivers the literal "right-click taskbar → snippets" UX.
- **Cons:** requires Windhawk; can break on Win11 feature updates; can't ship as `.exe`; must be C++ and either submitted to `ramensoftware/windhawk-mods` PR queue or self-hosted as `.wh.cpp`.
- **Pattern:** hook `TaskbarResources::OnTaskListButtonContextRequested` (Win11) + `CTaskListWnd::_HandleContextMenu` (Win10 fallback), append items via `InsertMenuItem`, IPC to WPF app via named pipe `\\.\pipe\TaskCopy`.

### 3rd — Global hotkey overlay only (no tray icon)
- **Cons:** zero discoverability; `Win+V` belongs to Microsoft. Skip.

### 4th — Hooked `Win+V` augmentation
- **Cons:** brittle, AV-flagged, anti-pattern. Skip.

### 5th — Deskband (`IDeskBand` shell extension)
- **Dead on arrival.** Win11 XAML taskbar does not load `IDeskBand` COM objects. Skip.

---

## 5. Recommended Stack

### Language / framework
**C# / .NET 9 WPF**, SDK-style, framework-dependent publish.
Matches existing repo precedent (Snapture, Devicer, Images, OrganizeContacts, Snapture) per CLAUDE.md. `.NET 9` over `.NET 10` keeps WPF + H.NotifyIcon ecosystem mature.

C++ enters only if/when the Windhawk companion mod ships.

### Packages
| Package | Purpose |
|---|---|
| `H.NotifyIcon.Wpf` 2.4.1 | Tray icon w/ XAML `MenuFlyout`, Win11 Efficiency Mode, survives Explorer restart |
| `Microsoft.Xaml.Behaviors.Wpf` | MVVM event binding |
| `CommunityToolkit.Mvvm` 8.x | `ObservableObject`, `RelayCommand`, `[ObservableProperty]` |
| `Microsoft.Data.Sqlite` 9.x | Snippet store (same pattern as Ditto) |
| `NHotkey.Wpf` 2.x | Global hotkey via `RegisterHotKey` |

### Win32 / COM surface
- `Shell_NotifyIcon` — wrapped by `H.NotifyIcon`. Auto-handles `WM_TASKBARCREATED`.
- `RegisterHotKey` — wrapped by `NHotkey.Wpf`. Default `Ctrl+Alt+V` (do **not** take `Win+V`).
- `System.Windows.Clipboard.SetText` w/ retry on `COMException` (clipboard race).
- `AddClipboardFormatListener` + `WM_CLIPBOARDUPDATE` — *optional* auto-capture mode.
- `GetForegroundWindow` saved pre-flyout + `SetForegroundWindow` + `keybd_event(VK_CONTROL, VK_V)` for auto-paste.
- `H.NotifyIcon` `ContextMenuMode="SecondWindow"` for a modern XAML popup (vs. dated native `TrackPopupMenuEx`).

### Anti-patterns to avoid
- `IContextMenu` / `IShellExtInit` shell extension — wrong surface.
- `IExplorerCommand` — File Explorer only, not taskbar.
- `SetWindowsHookEx` for menu injection — AV-flagged, brittle.

---

## 6. Windhawk Companion Mod (v0.2+ optional)

When/if shipped:
- Template: clone [`taskbar-classic-menu.wh.cpp`](https://github.com/ramensoftware/windhawk-mods/blob/main/mods/taskbar-classic-menu.wh.cpp).
- Metadata: `@include explorer.exe`.
- Hook `TaskbarResources::OnTaskListButtonContextRequested` (Taskbar.View.dll) + `CTaskListWnd::_HandleContextMenu` (taskbar.dll) via `SYMBOL_HOOK` arrays + `Wh_SetFunctionHook`.
- IPC: named pipe `\\.\pipe\TaskCopy`; mod sends `{x, y}` cursor pos, WPF app responds with snippet list (JSON), user clicks, mod calls clipboard API directly OR forwards selection back.
- Build via in-app Windhawk editor (Clang 20, C++23) OR VS2026 + Windhawk SDK header.
- Mod ID: `taskcopy-taskbar-menu`. Self-host the `.wh.cpp` on the TaskCopy repo as fallback if upstream PR is rejected.

---

## 7. Open Questions / Risks

1. **Scope:** v0.1 = tray-only; v0.2 = Windhawk companion mod. Recommended.
2. **Hotkey:** `Ctrl+Shift+V` is grabbed by Office/Edge ("paste w/o formatting"). **Default to `Ctrl+Alt+V`**, force first-run config.
3. **Auto-paste:** Ditto auto-pastes; ClipClip asks. Recommend auto-paste with settings toggle.
4. **Storage:** SQLite (Ditto/CopyQ pattern) wins for search + 10k+ items. SQLite over flat JSON.
5. **Capture mode:** snippet-only (user curates) vs. clipboard-history (auto-record). Name implies snippets — leaner v1 scope; auto-record is v0.3+ flag.
6. **Windhawk gallery risk:** IPC-bootstrap mods are uncommon in the official gallery; self-host the `.wh.cpp` if rejected.
7. **MSIX:** H.NotifyIcon + global hotkeys + SQLite at `LocalApplicationData` all packaging-safe.
8. **Signing:** H.NotifyIcon's persistent tray-GUID requires Authenticode across path changes — budget for cert.

---

## 8. Sources

- https://github.com/ramensoftware/windhawk
- https://github.com/ramensoftware/windhawk-mods
- https://windhawk.net/mods/taskbar-classic-menu
- https://windhawk.net/mods/classic-taskbar-context-menu
- https://windhawk.net/mods/eradicate-immersive-menus
- https://github.com/ramensoftware/windhawk/wiki/Creating-a-new-mod
- https://github.com/valinet/ExplorerPatcher
- https://github.com/valinet/ExplorerPatcher/wiki/ExplorerPatcher's-taskbar-implementation
- https://github.com/microsoft/WindowsAppSDK/discussions/2320
- https://github.com/microsoft/PowerToys/issues/21696
- https://blogs.windows.com/windowsdeveloper/2021/07/19/extending-the-context-menu-and-share-dialog-in-windows-11/
- https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-icontextmenu
- https://github.com/HavenDV/H.NotifyIcon
- https://www.nuget.org/packages/H.NotifyIcon.Wpf
- https://github.com/hluk/CopyQ
- https://github.com/sabrogden/Ditto
- https://github.com/veler/clipboardzanager
- https://github.com/rojarsmith/TrayToolbar
- https://albertakhmetov.com/posts/2025/creating-a-context-menu-for-tray-icons-in-c%23-and-winui/
- https://ramensoftware.com/windhawk-mods-for-the-windows-11-taskbar
- https://en.wikipedia.org/wiki/List_of_features_removed_in_Windows_11
