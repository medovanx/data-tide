# Changelog

All notable changes to Data Tide are listed here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

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

[1.0.3]: https://github.com/medovanx/data-tide/releases/tag/v1.0.3
[1.0.2]: https://github.com/medovanx/data-tide/releases/tag/v1.0.2
[1.0.1]: https://github.com/medovanx/data-tide/releases/tag/v1.0.1
[1.0.0]: https://github.com/medovanx/data-tide/releases/tag/v1.0.0
