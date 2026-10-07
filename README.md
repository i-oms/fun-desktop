# IdleWalker

A tiny Windows desktop companion. When your mouse cursor has not moved for two
seconds, a small pixel-art character peeks over the bottom edge of the screen,
hops out, and strolls along the bottom of your monitors - all of them,
including external ones. Move the mouse and it gets a fright and dives back
down.

It is plain C# with no dependencies beyond what ships with Windows: no
runtime to install, no NuGet packages, no IDE needed.

```
fun/
├── build.cmd                 builds IdleWalker.exe with the compiler built into Windows
├── IdleWalker.exe            the built program (ignored by git, see .gitignore)
├── README.md                 this file
├── .gitignore
└── src/
    ├── Program.cs            entry point: single instance, DPI, message loop
    ├── Settings.cs           the tunable constants
    ├── Domain/               the character - no Windows code in here
    │   ├── Art.cs            pixel art parts and palette
    │   ├── Pose.cs           which parts to show in one frame
    │   ├── Puff.cs           a speck of dust
    │   ├── IWorld.cs         what the walker needs to know about the terrain
    │   └── Walker.cs         state machine, physics, idle behaviour
    ├── Rendering/            pixels
    │   ├── Surface.cs        software canvas, outline pass
    │   └── FrameRenderer.cs  lays out the canvas and draws a frame
    ├── Platform/             Win32
    │   ├── Native.cs         P/Invoke declarations
    │   ├── MonitorWorld.cs   IWorld backed by the real monitors
    │   ├── SpriteWindow.cs   layered click-through window
    │   ├── FramePump.cs      vsync-paced tick thread
    │   └── StartupRegistration.cs   the "Start with Windows" registry entry
    └── App/                  wiring
        ├── WalkerApp.cs      the per-frame loop
        ├── CursorWatcher.cs  idle detection
        └── TrayMenu.cs       notification-area icon and menu
```

---

## Contents

1. [Requirements](#requirements)
2. [Building](#building)
3. [Running](#running)
4. [What it does, step by step](#what-it-does-step-by-step)
5. [Tuning](#tuning)
6. [Editing the character art](#editing-the-character-art)
7. [How it works (technical)](#how-it-works-technical)
8. [Multiple monitors](#multiple-monitors)
9. [Performance](#performance)
10. [Troubleshooting](#troubleshooting)
11. [Known limitations](#known-limitations)

---

## Requirements

| | |
|---|---|
| OS | Windows 10 version 1703 or later, or Windows 11 (tested on Windows 11 24H2) |
| Runtime | .NET Framework 4.x - already part of Windows, nothing to install |
| Compiler | `csc.exe` from .NET Framework 4 - already part of Windows at `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` |
| Display | Desktop Window Manager (DWM) composition - always on in Windows 8 and later |

No Visual Studio, no .NET SDK and no admin rights are required.

## Building

Double-click `build.cmd`, or run it from a terminal:

```bat
build.cmd
```

It runs this single command:

```bat
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize+ ^
    /out:IdleWalker.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /recurse:src\*.cs
```

| Flag | Why |
|---|---|
| `/target:winexe` | A windowed app - no console window opens behind it. |
| `/optimize+` | Release-style code generation. |
| `/r:System.Windows.Forms.dll` | `NativeWindow`, `Screen`, `NotifyIcon`, the message loop. |
| `/r:System.Drawing.dll` | `Bitmap`/`Icon` for the tray icon, `Rectangle`, `Point`. |
| `/recurse:src\*.cs` | Compiles every source file under `src\`, so new files need no build changes. |

The result is one ~32 KB `IdleWalker.exe` next to `build.cmd`. Build takes a
couple of seconds.

### Building another way

The sources are ordinary C#, so any .NET toolchain works too. With the .NET
SDK installed, drop this `IdleWalker.csproj` next to `src\` and run
`dotnet build -c Release`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net48</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <LangVersion>5</LangVersion>
  </PropertyGroup>
</Project>
```

(Only the Windows-compiler route is covered by `build.cmd`; the csproj above
is a convenience and is not checked in.)

**Notes**

- The compiler built into Windows understands **C# 5** only. The source
  deliberately avoids newer syntax (string interpolation, `out var`, `?.`,
  expression-bodied members, `nameof`, ...). If you add code, stay within C# 5
  or build with a newer compiler instead (e.g. `dotnet build` with a
  `net48` WinForms project, or Visual Studio).
- If IdleWalker is running, the build fails with *"could not write to output
  file"* because the `.exe` is in use. Exit it from the tray icon first.
- On a 32-bit Windows install use `Framework` instead of `Framework64` in
  `build.cmd`.

## Running

Start `IdleWalker.exe`. Nothing visible happens except a small icon in the
notification area (near the clock; it may be hidden under the `^` overflow
arrow). Leave the mouse alone for two seconds and the character appears.

**Tray icon menu** (right-click):

| Item | Effect |
|---|---|
| Start with Windows | Ticked: the program starts when you sign in. See below. |
| Pause | Keeps the character hidden until unticked. If it is on screen, it leaves immediately. |
| Exit | Quits the program. |

Only one instance can run at a time; starting a second one quietly exits.

### Start automatically with Windows

Tick **Start with Windows** in the tray menu. This writes a value named
`IdleWalker` under

```
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
```

containing the full path of the running `IdleWalker.exe`. It affects only
your user account and needs no administrator rights. Untick it to remove
the value again.

Each time the program starts with the option on, it rewrites the path, so
moving or renaming the `.exe` doesn't break auto-start - just run it once
from its new location. If the registry write fails for some reason (a
locked-down work PC, for instance), the tick stays off and a balloon tip
says why.

You can see or remove the entry yourself in Task Manager → *Startup apps*,
or with:

```powershell
Get-ItemProperty HKCU:\Software\Microsoft\Windows\CurrentVersion\Run | Select-Object IdleWalker
Remove-ItemProperty HKCU:\Software\Microsoft\Windows\CurrentVersion\Run -Name IdleWalker
```

### Stopping it

Right-click the tray icon and choose **Exit**, or from a terminal:

```powershell
Stop-Process -Name IdleWalker
```

## What it does, step by step

```
                 cursor still for 2 s
   HIDDEN ──────────────────────────────▶ PEEK RISE ──▶ PEEK LOOK ──▶ PEEK DUCK
     ▲                                                                     │
     │ fully underground                                                   │ springs out
     │                                                                     ▼
    EXIT ◀──── mouse moved / paused ────  WALK ◀─── lands ─── AIR ◀────────┘
   ("!" + hop,                           │  ▲                 ▲
    dives down)                          │  │                 │ hop, climb, or walked off a ledge
                                         ▼  │                 │
                       ┌──────────┬─────────┴─────┬───────────┴─┐
                       │  TURN    │  LOOK AROUND  │  WAVE  │ REST │  CROUCH
                       └──────────┴───────────────┴────────┴──────┘
```

1. **Watching.** While hidden, the program polls the cursor position ten times
   a second. A move of more than 2 pixels (to ignore sensor jitter) resets the
   idle timer.
2. **Peeking.** After 2 seconds of stillness it picks a random spot along the
   bottom of the monitor the cursor is on. The top of the hat eases up over
   the edge until the eyes are showing, the head turns to check both ways,
   then it ducks down a little...
3. **Entrance.** ...and springs out of the ground with a burst of dust, lands
   with a squash, pauses for half a second and starts walking.
4. **Walking.** It walks along the top of the taskbar (or the screen bottom if
   there is no taskbar there) at about 90 px/s, easing into and out of motion.
   The six-frame walk cycle is driven by distance travelled, so the feet never
   slide. It walks the whole width of the desktop and turns around at the
   outer edges - head first, then body.
5. **Idle moments.** Every 5-11 seconds it stops to do one of: look around,
   wave towards your cursor (turning to face it first if needed), or stand
   and breathe for a moment. Every 4-9 seconds it does a little hop. It
   blinks every few seconds regardless.
6. **Other monitors.** When the next monitor's bottom edge is higher it jumps
   up onto it, timing the take-off so the top of the arc is over the edge.
   When it is lower, it walks off the edge and falls, landing with a bigger
   squash and more dust. See [Multiple monitors](#multiple-monitors).
7. **Exit.** As soon as the cursor moves (or Pause is ticked) a "!" appears
   over its head, it does a small startled hop and drops through the ground
   line in about 0.4 seconds. The idle timer then starts again.

The character never takes focus, never appears in the taskbar or Alt-Tab,
and mouse clicks pass straight through it to whatever is underneath.

## Tuning

All the knobs are constants in `src\Settings.cs`. Change them, exit the
running copy, rebuild.

| Constant | Default | Meaning |
|---|---|---|
| `IdleSeconds` | `2.0` | How long the cursor must be still before it appears. |
| `MoveTolerance` | `2` | Cursor movement (pixels) below this does not count as moving. |
| `BasePixelSize` | `3` | Size in screen pixels of one art pixel at 100% display scaling. `4` makes it a third bigger. |
| `WalkSpeed` | `90` | Walking speed, px/s at 100% scaling. |
| `Acceleration` | `420` | How quickly it reaches walking speed. |
| `Braking` | `520` | How quickly it stops. |
| `Gravity` | `1100` | px/s². Lower = floatier jumps. |
| `HopSpeed` | `320` | Take-off speed of the random hop (about 47 px high). |
| `StartleSpeed` | `210` | Height of the frightened hop when you move the mouse. |
| `ExitGravity` | `2200` | How fast it drops away afterwards. |

Speeds are automatically multiplied by the monitor's display scaling, so the
motion looks the same on a 100% and a 200% screen.

Timings that live in the state machine (`src\Domain\Walker.cs`) rather than
as constants, should you want them: idle stop every `Between(5, 11)` s and
hop every `Between(4, 9)` s in `StepWalk`; blink every `Between(1.8, 4.5)` s
in `Step`; peek/look/duck durations in the `PeekRise`/`PeekLook`/`PeekDuck`
cases of `Step`.

## Editing the character art

The art lives in `src\Domain\Art.cs` as strings, one character per pixel,
16 columns wide. The soles of the boots are the bottom of row 17; parts
declare which row their first line sits on (`new Part(10, ...)` starts on
row 10). The figure is drawn facing right and mirrored in code when it faces
left.

| Letter | Colour | Used for |
|---|---|---|
| `.` | transparent | |
| `Y` | `#FFD23F` | pom-pom |
| `T` / `t` | `#1FA39A` / `#157A74` | beanie / brim |
| `S` | `#F6C9A0` | skin |
| `P` | `#EFA08A` | cheek |
| `K` | `#2B2233` | eyes, mouth |
| `O` | `#F2762E` | scarf |
| `N` / `n` | `#34508F` / `#263A6B` | jacket / sleeve |
| `G` / `g` | `#6B6F82` / `#4E5163` | near leg / far leg |
| `B` / `b` | `#6B4226` / `#4F301B` | near boot / far boot |
| `W` | `#FFFFFF` | the "!" |

Add a letter in `Art.ColorOf` to introduce a colour. The dark outline is not
drawn by hand - it is generated around whatever is opaque, so new art gets it
automatically.

The parts are:

- `HeadNormal`, `HeadBlink`, `HeadSurprised` - rows 0-9 (hat + face).
- `ArmMid`, `ArmForward`, `ArmBack`, `TorsoPlain` - rows 10-13 (scarf +
  jacket with the near arm in different positions).
- `WaveA`, `WaveB` - a raised arm drawn over `TorsoPlain` and the head.
- `LegsStand`, `LegsJump`, `WalkLegs[0..5]` - rows 13-17. Row 13 is a hip
  row normally hidden under the jacket; it fills the gap when the upper body
  bobs upward. `G/B` is the leg nearest the viewer, `g/b` the far one.
- `TailDown`, `TailA`, `TailB`, `TailUp` - the loose end of the scarf.
- `Bubble` - the "!" (rows -7 to -3, i.e. above the head).

A `Pose` (`src\Domain\Pose.cs`) picks one of each and adds an upper-body
bob (`BodyDy`), horizontal and vertical stretch factors, whether the head is
turned and whether the bubble shows. `Walker.CurrentPose()` decides the pose
for each frame; `FrameRenderer.DrawPose()` turns it into pixels.

## How it works (technical)

### Architecture

The code is split into four layers; dependencies point downwards only.

```
┌──────────────────────────────────────────────────────────────────┐
│ App          WalkerApp · CursorWatcher · TrayMenu                │  the per-frame loop
├──────────────────────────────────────────────────────────────────┤
│ Platform     SpriteWindow · MonitorWorld · FramePump             │  Win32: windows, monitors,
│              StartupRegistration · Native                        │  registry, vsync
├──────────────────────────────────────────────────────────────────┤
│ Rendering    FrameRenderer · Surface                             │  pixels, no Win32
├──────────────────────────────────────────────────────────────────┤
│ Domain       Walker · Pose · Art · Puff · IWorld                 │  the character; pure C#
└──────────────────────────────────────────────────────────────────┘
                              Settings (constants, used by all)
```

- **Domain** is the character itself: the art, the state machine, the
  physics. It depends on nothing Windows-specific. The terrain it walks on
  is abstracted behind `IWorld` (ground height at an x position, monitor
  edges, the cell size to draw at), so the whole simulation could be driven
  from a unit test with a fake world.
- **Rendering** turns a `Walker` into an array of premultiplied ARGB pixels.
  It knows about the canvas layout and the outline pass but not about
  windows.
- **Platform** is the only place that touches Win32: the layered window, the
  real monitors (`MonitorWorld` implements `IWorld` on top of
  `Screen.AllScreens` and `GetDpiForMonitor`), the vsync pump, the Run key.
- **App** wires it together. `WalkerApp.OnTick` is the whole frame:

  ```
  poll cursor → spawn / startle → walker.Step(dt) → renderer.Render(walker) → window.Present(...)
  ```

Everything is `internal` to the one assembly; there is no public API.

### Frame flow

```
FramePump (thread)   DwmFlush() ─▶ post tick ─┐
                                              ▼  UI thread
WalkerApp.OnTick     CursorWatcher.Poll ─▶ Walker.Spawn / Startle / Step
                                              │
FrameRenderer        Walker state ─▶ Pose ─▶ Surface (dust, parts, outline)
                                              │
SpriteWindow         Surface.Pixels ─▶ DIB section ─▶ UpdateLayeredWindow
```

### The window

`SpriteWindow` is a `NativeWindow` (no `Form` - nothing WinForms-y to fight
with). It is created with:

| Style | Effect |
|---|---|
| `WS_POPUP` | No frame, no title bar. |
| `WS_EX_LAYERED` | Per-pixel alpha via `UpdateLayeredWindow`. |
| `WS_EX_TRANSPARENT` | Mouse input passes through to whatever is behind. |
| `WS_EX_TOOLWINDOW` | Not in the taskbar or Alt-Tab. |
| `WS_EX_NOACTIVATE` | Never steals focus. |
| `WS_EX_TOPMOST` | Above normal windows; re-asserted once a second because the taskbar is topmost too. |

Each frame the `Surface` pixels are copied into a 32-bit top-down DIB section
selected into a memory DC, and `UpdateLayeredWindow` is called with
`ULW_ALPHA` / `AC_SRC_ALPHA`, which sets the window's position, size and
contents atomically. The DIB is recreated only when the canvas size changes
(i.e. when the character moves to a monitor with different scaling).

### The canvas

The window is a fixed canvas of 40 x 48 art cells around the character
(120 x 144 px at 100% scaling). Its **bottom edge sits on the ground line**
(top of the taskbar). Anything the character does below that line - peeking
up, diving away - is simply cut off by the canvas edge, which is how it
appears to come out of and go into the taskbar without ever drawing over it.
During a high jump the canvas follows the character upward so it never
leaves the window.

### Rendering

`Surface` is a plain `int[]` of premultiplied ARGB pixels (the format
`UpdateLayeredWindow` wants; `0xAARRGGBB` in memory is `B,G,R,A` bytes, which
is what a 32-bpp `BI_RGB` DIB holds).

1. Dust puffs are filled directly into the canvas as translucent squares.
2. Character parts are drawn into a separate layer as solid rectangles - one
   per art pixel - at `cell size x stretch`. Because every rectangle edge is
   computed with the same rounding formula, stretched cells never leave gaps.
3. **Outline pass:** a transparent layer pixel becomes outline if an opaque
   pixel lies within one cell of it along its row or its column. This is done
   in four linear sweeps (left, right, down, up) so it costs O(pixels), and it
   reproduces the look of a hand-drawn 4-neighbour pixel outline while
   working for stretched, bobbing and overlapping parts.
4. The layer is laid over the canvas (character pixels are fully opaque, so
   this is a copy).

No GDI+ is involved in the per-frame path; it is all integer array writes.

### Frame pacing

A background thread (`PumpLoop`) calls `DwmFlush()`, which returns when the
desktop compositor has presented a frame - i.e. once per monitor refresh.
After each flush it posts a tick to the UI thread through the WinForms
`SynchronizationContext`; a pending-flag ensures ticks never pile up if the
UI thread is slow. While the character is hidden the thread sleeps 100 ms
between cursor polls instead.

All motion is integrated with the measured frame time `dt` (clamped to
50 ms), so a 60 Hz and a 144 Hz monitor give the same speeds.

### Idle detection

`GetCursorPos` is polled; no global hooks are installed, so nothing is
injected into other processes and no special permissions are needed. The
position is compared with the last *anchor* position, and the anchor moves
only when the cursor has travelled more than `MoveTolerance` pixels, so a
jittery sensor cannot keep resetting the timer. `GetCursorPos` fails on the
secure desktop (UAC prompt, lock screen); that is treated as "not moving".

### Physics

The character has a centre `cx`, feet position `feetY`, velocities `vx`/`vy`
and a facing. Ground is `WorkingArea.Bottom` of the monitor under `cx` (the
top of the taskbar, or the screen bottom when the taskbar is elsewhere or
auto-hidden). Walking: `vx` eases towards `+/- WalkSpeed`. Jumping: `vy`
integrates `Gravity`; landing happens when falling and `feetY >= ground`,
with squash proportional to impact speed. Stretch in the air is proportional
to `|vy|`.

`MoveX` refuses to move the centre into a spot where the ground is higher
than the feet (the side of a taller monitor acts as a wall). In the air that
means the character slides up the wall until it clears the top; on the
ground it is what triggers a climb.

### DPI

`SetProcessDpiAwarenessContext(PER_MONITOR_AWARE_V2)` is called before any
window exists, so all coordinates are physical pixels on every monitor and
Windows never scales the window bitmap. The cell size is
`round(BasePixelSize * dpi / 96)` for the monitor under the character,
obtained with `GetDpiForMonitor`, so the figure is the same physical size on
a 100% and a 150% screen. Speeds scale the same way.

### Single instance

A named mutex (`IdleWalker.SingleInstance`) makes a second copy exit at
once.

### Start with Windows

`StartupRegistration` reads and writes the per-user Run key
(`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `IdleWalker`).
It uses `Microsoft.Win32.Registry`, which lives in mscorlib, so no extra
assembly reference is needed. The stored command is the quoted
`Application.ExecutablePath`; it is refreshed on every launch while the
option is on.

### Adding things

- A new idle antic: add a `Mode`, start it from `PickIdle` via `Brake(...)`,
  step it in `Walker.Step`, and give it a pose in `CurrentPose`.
- New art: add a `Part` in `Art.cs` (16 columns wide, say which row it
  starts on) and reference it from a pose.
- A new menu item: `TrayMenu` owns the menu; expose state as a property or
  an event like `Paused` / `ExitRequested`.
- Anything needing a new Win32 call goes in `Native.cs` and is used only
  from the Platform layer.

## Multiple monitors

The walkable world is the union of all monitors' horizontal spans; the
character turns around only at the far left of the leftmost and far right of
the rightmost monitor.

For any x position the ground is the bottom of the working area of the
monitor containing x. Where monitors overlap vertically (stacked), the lowest
one is used. The ground is therefore a step function, and the character treats
it like platform-game terrain:

- **Step up ahead** (`WallAhead`): it scans monitor edges within 400 px in
  its direction of travel. For a step of height *h* it needs a take-off
  speed `v = sqrt(2 g (h + 6 cells))`; it starts the jump when the edge is
  `WalkSpeed * v / g` away so that the apex is over the edge. If it arrives
  early, the wall rule slides it up the edge; if it falls short it lands back
  down and tries again.
- **Step down ahead:** nothing special - once the centre passes the edge the
  ground is lower than the feet, so `StayOnGround` switches it into the air
  with `vy = 0` and it falls.
- **Different scaling per monitor:** the cell size is re-evaluated every
  frame from the monitor under the character; velocities are rescaled at the
  same moment so motion stays continuous.

Monitors are read through `Screen.AllScreens`, which WinForms refreshes
automatically when the display configuration changes, so plugging a monitor
in or out while it runs is fine.

## Performance

- Hidden: ten `GetCursorPos` calls a second; effectively zero CPU.
- On screen: roughly 5% of one core at 60 Hz on a modern laptop, almost all
  of it the per-frame `UpdateLayeredWindow`. Memory ~30 MB (mostly the .NET
  runtime).

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| Nothing appears | Is the tray icon there? If not, the program is not running. Check **Pause** is unticked. The character spawns on the monitor the *cursor* is on. |
| Appears, but immediately dives away | Something is moving the cursor (a jittery mouse, a wobbling desk, some remote-control software). Raise `MoveTolerance`. |
| Hidden behind a full-screen game | Exclusive full-screen apps bypass the compositor; nothing can draw over them. Borderless-windowed games are fine. |
| Blurry or wrong size on one monitor | Check Windows' per-monitor scaling; the cell size follows it. If a monitor reports an odd DPI, `ScaleFor` is where to adjust. |
| `build.cmd` says the output file is in use | Exit IdleWalker from the tray first. |
| `build.cmd` cannot find `csc.exe` | On 32-bit Windows edit the path to use `Framework` instead of `Framework64`. |
| It walks over something I want to click | It is click-through, so just click; the character does not intercept input. |

## Known limitations

- **Vertically stacked monitors.** Only the lower monitor at each x position
  is walked on; a monitor directly above another is never visited.
- **Exclusive full-screen applications** cover it (see above).
- **Taskbar on the left/right/top.** The character walks along the very
  bottom of the screen instead; this is by design (the ground is the bottom
  of the working area).
- **Keyboard activity does not count** as "not idle" - only the cursor does,
  as requested. Switching to `GetLastInputInfo` would include the keyboard.
- The art is 16 x 18 cells; details such as individual fingers are not going
  to happen.
