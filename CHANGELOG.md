# Changelog

All notable changes to Data Tide are listed here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and versions follow [Semantic Versioning](https://semver.org/).

## [1.5.0] - 2026-11-17

### Added
- Widget layouts (Settings, General, Taskbar widget): upload and download, speeds and ping, speeds on one line, today and this month, or ping only. Backgrounds: Data Tide blue, the Windows accent colour, or none (the pill then only shows on hover). A live preview shows the choice before you save.

## [1.4.1] - 2026-11-17

### Changed
- Game mode matches a game by its full path, not just its exe name, so another program with the same file name in a different folder no longer turns it on. Games running as administrator are still recognised; only when Windows hides even the path does the name decide.

## [1.4.0] - 2026-11-17

### Added
- Game mode (Settings, Automatic): pick your games, and while one runs every other program together is held to a speed you set (512 KB/s down and 128 KB/s up by default). Programs with their own limit keep it, DNS lookups always pass, and it switches on and off as games start and quit. The flyout's limits line says when it is at work.
- Metered networks (Settings, Automatic): pick programs to cut off while Windows says the connection is metered, such as a phone hotspot or mobile plan.
- Ping, jitter and loss to a server you choose (1.1.1.1 by default), in the flyout's graph and the hover card, with a coloured quality dot. Turn it off or change the server in Settings, Network.
- Reports chart the five busiest programs day by day above the By program table.

## [1.3.5] - 2026-11-17

### Fixed
- Right-click menu: text is centred in its row, so icons sit level with it and the gaps above and below each separator are even (measured within half a pixel).
- The version line at the top of the right-click menu shows the Data Tide icon instead of a generic one.

## [1.3.4] - 2026-11-17

### Changed
- The right-click menu starts with the app name and version, and its icons sit level with their text.

## [1.3.3] - 2026-11-17

### Changed
- The widget's right-click menu (and the limit row menu in Settings) are drawn in the app's style: theme colours, rounded corners and hover rows, an icon per item and an accent check.
- While an update is checked for or installed, a thin progress line runs along the bottom of the widget, and the right-click menu shows how far it is in place of Check for updates. Closing Settings does not stop an update.

## [1.3.2] - 2026-11-17

### Added
- Language in the widget's right-click menu: switch between English and Arabic at once, without opening Settings.

## [1.3.1] - 2026-11-17

### Changed
- Updating shows its progress: on the Updates page a bar takes the place of Check now while it checks, downloads (with percent and size), verifies and restarts.

## [1.3.0] - 2026-11-17

### Added
- Reports have a By program table: download, upload, total and share for each program over the chosen range, with its icon, the top 12 by name and the rest summed. Programs are logged per day (programs.csv beside usage.csv) while the Programs list in the flyout is on, so the flyout's "today" per program now survives a restart too. Clear history and Keep history cover it.

### Changed
- Bigger icons in the per-program menu of the flyout.

## [1.2.0] - 2026-11-17

### Added
- Programs in the flyout: the busiest programs right now with their download and upload speed and today's data, from Windows' kernel network events (the source Task Manager uses). Turn it on from the flyout; it asks for administrator rights once per session and needs no driver.
- Click a program in the flyout to limit it (2 MB/s, 512 KB/s, 128 KB/s), block it, pause or remove its rule, or open the full options. A badge on its icon shows a rule: amber limited, red blocked, grey paused.

## [1.1.1] - 2026-11-17

### Changed
- The interface list in the flyout is drawn in the app's own style (rounded, accent marker, an Internet tag) instead of a stock menu.

## [1.1.0] - 2026-11-17

### Added
- Speed test: ping, jitter, download and upload against Cloudflare's servers, with live tiles and a progress line. Open it from the flyout's Speed button or the right-click menu.
- Download time calculator: file size and speed in, an estimate out. Detect fills in the last measured download speed, or runs a test first.

### Changed
- No more tray icon: the widget already opens the flyout (click), shows usage (hover) and has the menu (right-click). Update and limiter messages are Windows notifications with Data Tide's name and icon.
- The interface in the flyout header is a picker: click it to switch between Auto and any connected adapter; the one carrying the internet is marked.
- Speed limits use a funnel icon; the speedometer now means the speed test. Settings in the flyout is an icon-only button.

### Fixed
- The LICENSE no longer carries an audio credit copied from Salaty Reminder.

## [1.0.3] - 2026-11-16

### Fixed
- Buttons in Settings no longer turn their text near black when disabled or flash light when pressed in the dark theme.

## [1.0.2] - 2026-11-16

### Changed
- The flyout header shows the Data Tide mark and name, with the interface in a small pill beside it.

### Added
- GitHub page and Release notes buttons on the Updates page of Settings.

## [1.0.1] - 2026-11-16

### Changed
- The summary tiles in reports and exports no longer have coloured side bars; the download and upload tiles carry the widget's arrows instead.

## [1.0.0] - 2026-11-16

### Added
- Taskbar widget with live upload and download speeds, embedded next to the notification area. It takes the nearest free spot, so it never overlaps other taskbar widgets such as TrafficMonitor or Salaty Reminder, and it survives Explorer restarts.
- Interface choice: Auto follows the connection Windows uses to reach the internet, or pin any adapter.
- Traffic flyout: live speeds, a one-minute graph and today's usage per interface.
- Usage logger: bytes received and sent per interface, hour by hour, stored on this PC in `usage.csv`.
- Usage report for today, the last 7 or 30 days, this or last month, or all time, with a stacked chart, totals per interface and a day-by-day table. Interfaces are always kept apart and labelled.
- Export the report as a PNG image or a multi-page A4 PDF.
- Per-program download and upload speed limits through the Windows Packet Filter driver, applied by a small elevated helper.
- English and Arabic with a full right-to-left layout; light and dark theme following Windows.
- Auto-updates from GitHub Releases with SHA-256 verification.

### Fixed during testing
- Filter drivers bound to an adapter (WFP, QoS Packet Scheduler, firewalls) are no longer counted as extra interfaces with the same traffic, and their rows are removed from existing logs.
- Virtual adapters (VPNs, Hyper-V and WSL switches, virtual machines) are left out of the log by default, since their traffic also crosses a real adapter. A switch in Settings includes them.
- History can be kept for 1 week up to 5 years, forever, or any number of days.
- Closing a dropdown could crash the app on the next click.
- Hovering the widget shows today's and this month's usage, and the flyout shows this month's total.
- Program icons in the speed limits list and the program picker, which now lists apps with a window first under their real names.
- A switch in the flyout turns speed limits on or off without opening Settings.
- Each limit has its own unit (KB/s, MB/s or Mb/s), and the limits list uses the widget's arrows.
- Block a program completely, from the menu on its row.
- Compact Limits page: one line per program, one unit selector for the list, the driver card only while the driver is missing, and a live table of busy programs with a + to limit them.
- Speed limits now work with WinpkFilter 3.6.2.1, which refuses the bulk packet calls: the limiter falls back to per-adapter reads and sends (checked: a 256 KB/s limit held a download at about 239 KB/s).
- A small amber dot on the widget replaces the outline while limits are active.
- One-click driver install: downloads the official Windows Packet Filter installer from NT Kernel's GitHub release, checks its SHA-256 and runs it.

[1.5.0]: https://github.com/medovanx/data-tide/releases/tag/v1.5.0
[1.4.1]: https://github.com/medovanx/data-tide/releases/tag/v1.4.1
[1.4.0]: https://github.com/medovanx/data-tide/releases/tag/v1.4.0
[1.3.5]: https://github.com/medovanx/data-tide/releases/tag/v1.3.5
[1.3.4]: https://github.com/medovanx/data-tide/releases/tag/v1.3.4
[1.3.3]: https://github.com/medovanx/data-tide/releases/tag/v1.3.3
[1.3.2]: https://github.com/medovanx/data-tide/releases/tag/v1.3.2
[1.3.1]: https://github.com/medovanx/data-tide/releases/tag/v1.3.1
[1.3.0]: https://github.com/medovanx/data-tide/releases/tag/v1.3.0
[1.2.0]: https://github.com/medovanx/data-tide/releases/tag/v1.2.0
[1.1.1]: https://github.com/medovanx/data-tide/releases/tag/v1.1.1
[1.1.0]: https://github.com/medovanx/data-tide/releases/tag/v1.1.0
[1.0.3]: https://github.com/medovanx/data-tide/releases/tag/v1.0.3
[1.0.2]: https://github.com/medovanx/data-tide/releases/tag/v1.0.2
[1.0.1]: https://github.com/medovanx/data-tide/releases/tag/v1.0.1
[1.0.0]: https://github.com/medovanx/data-tide/releases/tag/v1.0.0
