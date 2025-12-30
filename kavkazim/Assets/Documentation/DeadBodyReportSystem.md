# Report System (Dead Body + Emergency Meeting)

## Overview
Single **L key** handles both:
- **Dead Body Report** - When near a body
- **Emergency Meeting** - When near the red button (if no body nearby)

**Restriction:** Each player can only report **once per game** (body OR emergency).

---

## Files

| File | Purpose |
|------|---------|
| [ReportService.cs](file:///c:/Users/germa/Desktop/Kavkazim/kavkazim/Assets/Scripts/Netcode/Reporting/ReportService.cs) | Finds targets, tracks who reported |
| [DeadBody.cs](file:///c:/Users/germa/Desktop/Kavkazim/kavkazim/Assets/Scripts/Netcode/Reporting/DeadBody.cs) | Body entity with report RPC |
| [EmergencyButton.cs](file:///c:/Users/germa/Desktop/Kavkazim/kavkazim/Assets/Scripts/Netcode/Reporting/EmergencyButton.cs) | Emergency button with cooldown |
| [DeadBodySpawner.cs](file:///c:/Users/germa/Desktop/Kavkazim/kavkazim/Assets/Scripts/Netcode/Reporting/DeadBodySpawner.cs) | Spawns bodies on kill |
| [ReportingSetup.cs](file:///c:/Users/germa/Desktop/Kavkazim/kavkazim/Assets/Scripts/Netcode/Reporting/ReportingSetup.cs) | Scene initializer |
| [ReportUIController.cs](file:///c:/Users/germa/Desktop/Kavkazim/kavkazim/Assets/Scripts/UI/ReportUIController.cs) | Report icon (orange when can report) |

---

## One Report Per Game

Server tracks which players have reported:
- `ReportService.HasPlayerReported(clientId)` - Check if used
- `ReportService.MarkPlayerAsReported(clientId)` - Mark as used
- `ReportService.ResetReportTracking()` - Call at new game start

**Important:** Call `ResetReportTracking()` when starting a new game/round!

---

## Log Messages

| Scenario | Console Log |
|----------|-------------|
| Dead Body | `REPORT (Dead Body), Found Body by "<name>"` |
| Emergency | `REPORT (Emergency Meeting) BY "<name>"` |
| Already Reported | `Report rejected - player already used their report this game.` |

---

## Setup

### Emergency Button
1. Select hexagon object in scene
2. Add **NetworkObject** component
3. Add **EmergencyButton** component
4. Set Interaction Range (default: 2.0)
