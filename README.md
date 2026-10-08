# IdleWalker

A tiny Windows desktop companion. When your mouse cursor has not moved for a
while (two seconds by default; anything from one second to five minutes), a
small shaded, 3D-looking Minion peeks over the bottom edge of the screen,
hops out, and strolls along the bottom of your monitors - all of them,
including external ones. Move the mouse and it gets a fright and dives back
down.

You can have up to five of them out at once. Each has its own face,
temperament and habits: one whistles and nods off, one shadow-boxes and
sticks its tongue out at you, one dances, one twirls, one flexes. The
sociable ones stop to wave or dance with each other, the aggressive ones
charge at anyone nearby and shove them off their feet - and whoever is
watching gasps, tuts or jeers, as is their nature.

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
    ├── Domain/               the characters - no Windows code in here
    │   ├── Expression.cs     a face as numbers, and the repertoire of moods
    │   ├── Look.cs           eyes, eye colour and hair of one Minion
    │   ├── Personality.cs    the cast: temperament, face, speed, habits
    │   ├── Pose.cs           which parts to show in one frame
    │   ├── Puff.cs           a speck of dust
    │   ├── IWorld.cs         what a walker needs to know about the terrain
    │   ├── Walker.cs         one character: state machine, physics, face
    │   ├── WalkerAntics.cs   the same class, continued: the idle antics and reactions
    │   ├── WalkerBrain.cs    the same class, continued: wants, errands, chats, pranks, tag
    │   └── Troupe.cs         the group: entrances, greetings, shoving, tag, onlookers, contagion
    ├── Rendering/            pixels
    │   ├── MinionPainter.cs  draws a Minion from solids: distance fields, normals, lighting
    │   ├── Surface.cs        software canvas, soft halo, shadow, dirty-region tracking
    │   └── FrameRenderer.cs  lays out the canvas, draws a frame, skips unchanged ones
    ├── Platform/             Win32
    │   ├── Native.cs         P/Invoke declarations
    │   ├── MonitorWorld.cs   IWorld backed by the real monitors (cached per frame)
    │   ├── SpriteWindow.cs   layered click-through window
    │   ├── FramePump.cs      vsync-paced tick thread
    │   ├── StartupRegistration.cs   the "Start with Windows" registry entry
    │   └── UserSettings.cs   remembers the walker count
    └── App/                  wiring
        ├── WalkerApp.cs      the per-frame loop, one window per walker
        ├── CursorWatcher.cs  idle detection
        ├── TrayMenu.cs       notification-area icon and menu
        └── AboutToast.cs     the About card
```

---

## Contents

1. [Requirements](#requirements)
2. [Building](#building)
3. [Running](#running)
4. [What it does, step by step](#what-it-does-step-by-step)
5. [Tuning](#tuning)
6. [Editing the character](#editing-the-character)
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
arrow). Leave the mouse alone for two seconds (adjustable, see below) and a
Minion appears.

**Tray icon menu** (right-click):

| Item | Effect |
|---|---|
| Walkers: *n* → 1…5 | How many Minions come out (all five by default). Raising it brings the new ones out straight away (one at a time); lowering it sends the extras away. Remembered between runs. |
| Appear after: *t* → 1…5 seconds, 1…5 minutes | How long the mouse must be still before they come out. Takes effect at once: shorten it and they appear sooner, lengthen it while they are out and they stay (they only ever leave when the mouse moves). Remembered between runs. |
| Start with Windows | Ticked: the program starts when you sign in. On by default; untick it to switch it off. See below. |
| Pause | Keeps the characters hidden until unticked. If they are on screen, they leave immediately. |
| About | Shows a small card above the walkers' lane - a waving Minion and the creator's name - for two seconds, then it fades out. It takes no focus; click it to dismiss it sooner. |
| Exit | Quits the program. |

Only one instance can run at a time; starting a second one quietly exits.

### The cast

Minions join in this order as you raise the count. Kevin is the first one
out. Each has a resting face of its own, its own gait (how much it bobs
and leans as it walks), its own list of habits (the things it does when it
stops walking for a moment, each weighted so some are rarer than others)
and its own temperament for dealing with the others.

| # | Name | Temperament | Look | Resting face | Habits, most to least often | With the others |
|---|---|---|---|---|---|---|
| 1 | Kevin | Calm | two eyes, a few sprouts of hair | neutral; sleepy on a long rest | Tires easily and is the most curious about the cursor. Looks around, rests, whistles a tune (notes float up), scratches his head over something until an idea strikes ("!"), nods off standing up (yawn, stretch, Zzz, then jerks awake), yawns, gets hiccups, waves shyly, sneezes. | Nods at Minions he meets, goes over for a chat now and then, nods along when others dance, shakes his head at fights, is grumpy for a while when pranked. |
| 2 | Stuart | Aggressive | one eye, hair combed flat, heavy brow | grumpy; bored on a rest, hands on hips | 20% faster, leans into his walk. Looks around, shadow-boxes (and sweats), sticks his tongue out and beckons you to come and have a go, throws a two-footed tantrum, laughs at you. | Charges at anyone within reach and shoves them; creeps up on anyone standing quietly and shouts "boo!"; squares up to Mel for a shouting match; jeers and points when someone else is shoved; furious when pranked himself. |
| 3 | Bob | Friendly | bald, one green eye and one brown, big smile | happy | A little slower, bouncy, and clumsy: trips flat on his face every couple of minutes. Waves a lot, dances a jig, points at your cursor and giggles, jumps for joy, laughs, gets hiccups, sneezes. | The most sociable: walks over to friends for a chat, goes to comfort anyone who has been shoved, starts games of tag, joins in any dance, catches yawns, gasps at shoves, laughs off a prank. |
| 4 | Dave | Friendly | two eyes, hair parted in the middle, open-mouthed smile | cheerful | The most energetic show-off. Waves, twirls right round with his arms out (and walks off dizzy), points at your cursor, jumps for joy, dances. | Chats, tag, dances and gasps like Bob. |
| 5 | Mel | Aggressive | two eyes, crew cut, heavy brows | stern; bored on a rest, hands on hips | 15% faster, leans into the walk. Flexes both arms and winks at them, stomps, shadow-boxes, occasionally taunts. | Charges, pranks, argues and jeers like Stuart. |

**A mind of their own.** Each walker carries three simple drives that rise
and fall: *energy* (spent by running, dancing, boxing and the like, won
back by walking calmly, resting and above all dozing), the wish for
*company* (grows while alone, satisfied by a chat or a greeting) and
*curiosity* about the cursor (grows until it has been over for a look).
When an idle moment comes, these decide what it does: a tired one rests,
yawns or dozes; a lively one capers; one that wants company sets off on an
errand to find some; one that cannot stand not knowing walks over to stand
under your cursor and looks up at it - Kevin scratches his head over it,
Bob and Dave point at it and giggle, Stuart and Mel stick their tongues out
at it. Random hops only happen with energy to spare, and rarely.

**How they get on**

- When two sociable walkers are about to pass each other they both stop and
  face one another: a friendly one waves, a calm one nods, and two friendly
  ones sometimes (40% of the time) dance a jig together. Then they carry on.
  A pair won't greet again for a dozen seconds.
- **Chats.** A walker wanting company walks up to its nearest neighbour and
  the two face each other and talk, taking turns: the talker's mouth moves
  and one hand gestures, the listener nods along, and friendly ones finish
  with a laugh. If the other is busy (dozing, mid-twirl) the visitor shrugs
  and walks off.
- **Comfort.** A friendly walker who sees someone still sad after being
  shoved walks over, puts a hand out to its shoulder, and both cheer up.
- **Tag.** Two friendly walkers with energy to burn play tag: one runs at
  the other laughing with an arm out, the other scampers off giggling; a tag
  ends with "you're it!" (laughing and pointing) and a chuckle. The chaser
  gives up, panting, after four seconds.
- **Shouting matches.** Two aggressive walkers that meet sometimes square
  up instead of barging past: they take turns to shout, arms flailing,
  stamping a foot, the other standing with hands on hips, and finish by
  turning their backs on each other in a huff.
- **Pranks.** An aggressive walker that notices someone standing quietly
  (resting, dozing, whistling, scratching) creeps up on them, leaning in
  with fists up and a mischievous grin, and shouts: a "!" over its head and
  the victim jumps into the air with a "!" of its own. A friendly victim
  laughs it off, a calm one is grumpy for a while, an aggressive one is
  furious and may well charge the prankster. If the target wanders off
  before the prankster gets there, it stamps its feet in frustration.
- **Shoves.** An aggressive walker with energy to spare that spots someone
  within about 380 px ahead (or 120 px behind) breaks into a run. On
  contact the victim is knocked into the air with a "!", lands in a puff of
  dust and runs away scared, a drop of sweat flying, and stays sad for a
  few seconds; the bully stands proud for a moment, then leaves people
  alone for twenty to forty seconds.
- **Onlookers.** Anyone else on the same ground within about 620 px who is
  walking, resting or looking around stops and turns to watch a shove: a
  friendly one gasps with its hands up by its face and then looks worried,
  a calm one puts its hands on its hips and shakes its head, an aggressive
  one laughs and points.
- **Catching.** A yawn is catching: whoever sees one start is likely to yawn
  too a moment later (the aggressive ones less so). So is a dance: friendly
  walkers nearby almost always join in, Kevin sometimes, the bullies never.
- Two aggressive walkers that charge each other collide and both go flying.
- Aggressive walkers ignore greetings; friendly ones wave at them anyway.
- Interactions only happen between walkers on the same ground level (so not
  across a step between monitors) and never while someone is in the air.

The cast is defined in `src\Domain\Personality.cs`; the meeting rules are in
`src\Domain\Troupe.cs`.

### Start automatically with Windows

**Start with Windows** is switched on the first time the program runs
(`Settings.StartWithWindowsByDefault`); the tray menu shows it ticked, and
unticking it switches it off for good. Either way it is a value named
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
   bottom of the monitor the cursor is on. The top of the head eases up over
   the edge until the goggles are showing, the head turns to check both ways,
   then it ducks down a little...
3. **Entrance.** ...and springs out of the ground with a burst of dust, lands
   with a squash, pauses for half a second and starts walking.
4. **Walking.** It walks along the top of the taskbar (or the screen bottom if
   there is no taskbar there) at about 90 px/s, easing into and out of motion.
   The six-frame walk cycle is driven by distance travelled, so the feet never
   slide. It walks the whole width of the desktop and turns around at the
   outer edges - head first, then body.
5. **Idle moments.** Every 5-11 seconds it decides what to do next (see
   [The cast](#the-cast)): set off on an errand - over to a friend for a
   chat, over to the cursor for a look, creeping up on someone to shout boo
   - or one of its habits on the spot: look around, wave or point towards
   your cursor (turning to face it first if needed), stand and breathe,
   dance, twirl, doze, whistle, box, taunt, and so on. Now and then, with
   energy to spare, it does a little hop. It blinks every few seconds
   regardless - quick, frequent blinks when frightened, slow heavy ones when
   drowsy, sometimes two in a row - and while standing about its eyes dart a
   little way off and back every second or two.
6. **Other monitors.** When the next monitor's bottom edge is higher it jumps
   up onto it, timing the take-off so the top of the arc is over the edge.
   When it is lower, it walks off the edge and falls, landing with a bigger
   squash and more dust. See [Multiple monitors](#multiple-monitors).
7. **Exit.** As soon as the cursor moves (or Pause is ticked) a "!" appears
   over its head, it does a small startled hop and drops through the ground
   line in about 0.4 seconds. The idle timer then starts again.

With more than one walker, they come out one at a time, 0.7-1.4 s apart, each
at its own random spot (kept at least 200 px from the others when possible),
and they all leave together when the mouse moves.

The character never takes focus, never appears in the taskbar or Alt-Tab,
and mouse clicks pass straight through it to whatever is underneath.

## Tuning

All the knobs are constants in `src\Settings.cs`. Change them, exit the
running copy, rebuild.

| Constant | Default | Meaning |
|---|---|---|
| `IdleSeconds` | `2` | How long the cursor must be still before it appears, until a different time is picked from the tray menu. |
| `IdleChoices` | 1…5 s, 1…5 min | The times the **Appear after** menu offers. A remembered value that is not in this list falls back to `IdleSeconds`. |
| `MoveTolerance` | `2` | Cursor movement (pixels) below this does not count as moving. |
| `BasePixelSize` | `3` | Size in screen pixels of one art pixel at 100% display scaling. `4` makes it a third bigger. |
| `MaxWalkers` | `5` | Upper limit of the Walkers menu. The cast in `Personality.Roster` must have at least this many entries. |
| `DefaultWalkers` | `5` | How many come out until a number is picked from the menu. |
| `StartWithWindowsByDefault` | `true` | Whether the first run registers the program to start at sign-in. |
| `WalkSpeed` | `90` | Walking speed, px/s at 100% scaling. |
| `Acceleration` | `420` | How quickly it reaches walking speed. |
| `Braking` | `520` | How quickly it stops. |
| `Gravity` | `1100` | px/s². Lower = floatier jumps. |
| `HopSpeed` | `320` | Take-off speed of the random hop (about 47 px high). |
| `StartleSpeed` | `210` | Height of the frightened hop when you move the mouse. |
| `ExitGravity` | `2200` | How fast it drops away afterwards. |

Speeds are automatically multiplied by the monitor's display scaling, so the
motion looks the same on a 100% and a 200% screen.

Per-character values (resting faces, speed factor, walk bob and lean, hop
and idle intervals, stamina, sociability, curiosity, clumsiness and the
weighted list of habits) are in `src\Domain\Personality.cs`; the timing of
each antic is in `src\Domain\WalkerAntics.cs` (`StartAntic` sets how long
it lasts); how fast the drives rise and fall, and the odds of each errand,
are in `src\Domain\WalkerBrain.cs` (`StepMood` and `Decide`).
Timings that live in the state machine (`src\Domain\Walker.cs`): blink every
`Between(1.8, 4.5)` s in `Step`; peek/look/duck durations in the
`PeekRise`/`PeekLook`/`PeekDuck` cases; greet, gloat and flee durations at
the top of the class. Meeting distances and cooldowns are at the top of
`src\Domain\Troupe.cs`.

## Editing the character

There is no sprite sheet. `src\Rendering\MinionPainter.cs` builds the
Minion out of solids every frame: a capsule for the body, cylinders for the
arms and legs, a sphere behind glass for each eye, a torus for each goggle
rim, ellipsoids for the shoes. For every pixel it works out the signed
distance to each solid (which gives a smooth, anti-aliased edge) and the
surface normal at that point (which gives the lighting), then composites the
solids back to front. The light sits above and to the left of the viewer,
so the body is brighter on that side, the goggles catch a highlight and the
rims gleam. A soft shadow is painted on the ground under the feet and thins
out when the Minion jumps.

The figure is 18 units tall and is drawn at `scale` pixels per unit (3 at
100% display scaling), exactly the height the old pixel art had. Model
coordinates run across from the centre (`u`, positive towards where it
faces) and up from the soles (`v`); the geometry constants at the top of the
painter put the goggles at `v = 12.55`, the mouth at `9.0`, the hips at
`4.3` and so on.

**Faces.** An `Expression` (`src\Domain\Expression.cs`) is a set of
numbers: how far the lids are down, a squint, a wink, where the brows sit and
how they tilt, pupil size and offset, cross-eyes, and the mouth's curve,
opening, width, sideways shift, teeth, tremble and tongue. Named presets
cover the repertoire:

| Expression | Used when |
|---|---|
| `Neutral`, `Happy`, `Cheerful`, `Grumpy`, `Stern` | the resting faces of Kevin, Bob, Dave, Stuart and Mel |
| `Sleepy`, `Bored` | a long rest (calm; aggressive) |
| `Curious` | peeking out of the ground, looking around, pointing, waking up |
| `Grin`, `Winking`, `Laugh`, `Giggle` | waving, dancing, greeting, pointing, laughing |
| `Focused`, `Excited` | winding up for a jump or a twirl; a big jump, a spin, jumping for joy |
| `Thinking`, `Surprised` | scratching the head; the idea that follows |
| `Whistling` | whistling |
| `Yawn`, `Asleep`, `Shocked` | yawning; dozing; being jerked awake, seeing a shove |
| `Inhale`, `Sneeze`, `Dizzy` | a sneeze, start to finish (also after a twirl) |
| `Determined`, `Smug`, `Proud` | charging or boxing; standing over a victim; flexing, a twirl's ta-da |
| `Raspberry`, `Mischievous` | taunting; the look that lingers afterwards, creeping up on someone |
| `Furious`, `Grumpy` | a stomping tantrum, a shouting match; the other side of the argument, a huff |
| `Panting` | walking off after a bout of boxing |
| `Dizzy`, `Scared`, `Sad` | being shoved into the air; running away; slowing down after |
| `Disapproving` | a calm Minion watching a shove |
| `Surprised` | the mouse moved |
| `Sad` | sent away because the count was lowered |

The walker eases its face towards the current target expression every frame
(`Expression.Towards`): moods melt into one another over about a tenth of a
second rather than snapping, a fright arrives faster and drowsiness more
slowly, and a blink is laid over whatever the face is doing. Some moods
linger for a second or so after the antic that caused them (dizzy after a
twirl, panting after boxing, mischievous after a taunt). The body turns the same way: it swings round to face the way it is
walking, turns to face you when it stops to rest or wave at the cursor, and
towards the other Minion when greeting, charging or gloating
(`Walker.YawTarget`). The pupils follow a gaze point: your cursor when it is near, another
Minion when greeting or charging, ahead when walking, back over the shoulder
when the head is turned.

**Looks.** A `Look` (`src\Domain\Look.cs`) is what a Minion is born with:
one eye or two, the iris colour of each, and a hair style (`None`, `Sprouts`,
`Combed`, `Parted`, `Crew`). Hair is a handful of tapering strands rooted on
the dome.

**Poses.** A `Pose` (`src\Domain\Pose.cs`) carries the expression, the gaze,
the yaw (how far the body is turned about its axis: 0 faces you, 58 is the
three-quarter view it walks in, negative is a glance back over the
shoulder), whether it is walking and where in the stride it is (legs and
arms swing continuously from that), how far the knees are tucked
up, the angle of each upper arm and forearm (and whether the near hand has
its index finger out), a forward lean, an upper-body bob and
squash-and-stretch factors, and the things that float about the figure: the
"!", a drop of sweat sliding down beside the head, two "z"s drifting up
from a sleeper, a music note rising from the mouth. The shoulders
sit on the surface of the body a little forward of its widest point, so a
hanging arm shows beside the body when the Minion faces you and swings round
in front of it as it turns away. A hanging arm swings fore and aft of the
body as it walks (which in the three-quarter view is partly towards you);
an explicitly posed arm - waving, flailing, hands on hips - is read in the
picture plane, so it looks the same from any angle.
`Walker.CurrentPose()` decides the pose for each frame;
`FrameRenderer.DrawPose()` paints it.

## How it works (technical)

### Architecture

The code is split into four layers; dependencies point downwards only.

```
┌──────────────────────────────────────────────────────────────────┐
│ App          WalkerApp · CursorWatcher · TrayMenu                │  the per-frame loop
├──────────────────────────────────────────────────────────────────┤
│ Platform     SpriteWindow · MonitorWorld · FramePump             │  Win32: windows, monitors,
│              StartupRegistration · UserSettings · Native         │  registry, vsync
├──────────────────────────────────────────────────────────────────┤
│ Rendering    FrameRenderer · MinionPainter · Surface             │  pixels, no Win32
├──────────────────────────────────────────────────────────────────┤
│ Domain       Troupe · Walker · Personality · Pose · Expression   │  the characters; pure C#
│              Look · Puff · IWorld                                │
└──────────────────────────────────────────────────────────────────┘
                              Settings (constants, used by all)
```

- **Domain** is the characters themselves: their looks and faces, each walker's state
  machine and physics, and the `Troupe` that owns the group and decides when
  two walkers greet or one shoves another. It depends on nothing
  Windows-specific. The terrain is abstracted behind `IWorld` (ground height
  at an x position, monitor edges, the cell size to draw at), so the whole
  simulation runs headless against a fake world - that is how the group
  behaviour was tested.
- **Rendering** turns a `Walker` into an array of premultiplied ARGB pixels.
  It knows about the canvas layout, the painter and the halo pass but not
  about windows. One `FrameRenderer` per walker.
- **Platform** is the only place that touches Win32: the layered window, the
  real monitors (`MonitorWorld` implements `IWorld` on top of
  `Screen.AllScreens` and `GetDpiForMonitor`), the vsync pump, the registry.
- **App** wires it together. `WalkerApp.OnTick` is the whole frame:

  ```
  refresh monitors → poll cursor → troupe.Step(dt) → for each walker: render if changed → present
  ```

Everything is `internal` to the one assembly; there is no public API.

### Frame flow

```
FramePump (thread)   DwmFlush() ─▶ post tick ─┐
                                              ▼  UI thread
WalkerApp.OnTick     MonitorWorld.Refresh · CursorWatcher.Poll ─▶ Troupe.Step
                                              │
Troupe               spawn next / startle all ─▶ Walker.Step ×n ─▶ Interact (greet, charge, shove)
                                              │
FrameRenderer ×n     Walker state ─▶ Pose ─▶ Surface (shadow, dust, painted Minion, halo)   [skipped if unchanged]
                                              │
SpriteWindow ×n      Surface.Pixels (content rect only) ─▶ DIB section ─▶ UpdateLayeredWindow
```

Each walker has its own small window rather than one desktop-wide window:
five 120 x 144 px windows are far cheaper to update than one 3840 px wide
one, and a walker that hasn't visibly changed costs nothing at all.

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

1. The shadow (a soft ellipse) and the dust puffs are drawn directly into
   the canvas.
2. `MinionPainter` shades the character into a separate layer, one pixel at
   a time. Each pixel is mapped into model units (undoing squash, stretch
   and facing), every solid contributes a coverage from its signed distance
   (`0.5 - d / pixel`, clamped, which is a one-pixel anti-aliased edge) and a
   colour from its lit normal, and the solids are composited back to front:
   far leg, near leg, the parts of the arms behind the body, body with the
   overalls and goggle strap painted on, eyes and rims, brows, mouth, hair,
   the parts of the arms in front, the "!". The arms are the one place where
   "behind" is decided per pixel rather than per solid: each joint carries
   a depth, every arm pixel compares its depth with the body's surface at
   that point, and the two blend over a band a couple of pixels deep. That
   is what lets a hand slide behind the body edge and come out again as the
   Minion turns, instead of the whole arm flipping over in one frame.
3. **Halo pass:** a transparent layer pixel next to a character pixel (along
   its row or column) gets a translucent dark halo, found with four linear
   sweeps so it costs O(pixels). It keeps the figure legible on a pale or
   busy desktop without looking like an inked outline.
4. The layer is laid over the canvas with a premultiplied "over", so
   anti-aliased edges blend with the shadow and dust behind them.

No GDI+ is involved in the per-frame path; it is all array writes. Painting
one Minion costs about 1 ms at 100% scaling (2 ms at 150%), and only
happens for walkers whose picture changed.

**Doing less work.** The canvas is 40 x 48 units but the character is only
about 12 x 21, so `Surface` tracks the rectangle that has actually been drawn
on. Clearing, the halo sweeps, the compositing and the upload to the DIB
all touch only that rectangle, and `UpdateLayeredWindow` is given just that
sub-rectangle (the window shrinks to fit the content each frame). On top of
that `FrameRenderer` compares the new pose, position, scale and facing with
the previous frame and skips everything - including the `UpdateLayeredWindow`
call - when nothing has changed, which is most frames while a walker is
standing still. Slow motions such as breathing are stepped in eighths of a
unit for the same reason.

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

### Remembered settings

`UserSettings` stores the walker count as a DWORD `WalkerCount`, the rest
time as a DWORD `IdleSeconds` and, once "Start with Windows" has been set
up (by the first-run default or by a click), a DWORD `StartupDecided` of 1,
all under `HKCU\Software\IdleWalker`. The last one is what stops the
default re-ticking the box after the user has unticked it. Reading or
writing failures are swallowed - the worst case is the count resets to 5
and the rest time to two seconds next time.

### Adding things

- A new character: add an entry to `Personality.Roster` (name, temperament,
  look, resting faces, speed, gait, hop/idle ranges, weighted habits) and,
  if you want more than five, raise `Settings.MaxWalkers`.
- A new temperament: add it to the `Temperament` enum and teach
  `Troupe.TryGreet` / `ConsiderCharging` how it behaves towards others, and
  `Walker.React` how it responds to a shove.
- A new idle antic: add it to the `Antic` enum, give it a duration in
  `StartAntic`, its face in `AnticFace`, its turn in `AnticYaw` and its body
  in `AnticPose` (all in `src\Domain\WalkerAntics.cs`; one-off effects such
  as dust go in `StepAntic`), then list it in the habits of whoever should
  do it.
- A new errand (something that needs walking somewhere first): add it to
  the `Goal` enum, choose it in `Decide`, say where it leads in `GoalX` and
  what happens on arrival in `Arrive` (all in `src\Domain\WalkerBrain.cs`).
- A new thing two walkers do face to face: a `ChatStyle`, with its face,
  pose and ending in `ChatFace`, `ChatPose` and `EndChat`.
- A new expression: add a preset to `Expression` and pick it in
  `Walker.UpdateFace` for the situation it belongs to. A new hair style is a
  case in `MinionPainter.HairFor`. A new accessory is a solid in the painter:
  a distance function, a normal, an albedo, and a place in the back-to-front
  order.
- A new menu item: `TrayMenu` owns the menu; expose state as a property or
  an event like `Paused` / `Count` / `ExitRequested`.
- Anything needing a new Win32 call goes in `Native.cs` and is used only
  from the Platform layer.
- Testing group behaviour: the Domain layer runs headless. A `Troupe` with a
  fake `IWorld` (flat ground, one monitor) stepped at 1/60 s in a console
  harness is enough to count greetings, charges and shoves.

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

Measured on a Windows 11 laptop with two 60 Hz monitors (12 logical cores):

| State | CPU | Notes |
|---|---|---|
| Hidden (cursor moving, normal use) | ~0% | Ten `GetCursorPos` calls a second, nothing else. |
| One walker out | < 5% of one core | 4.7% was measured before the dirty-rectangle optimisation; it is lower now. |
| Five walkers out | 6-7% of one core | ≈ 0.5% of the machine (measured: 6.3% and 7.2% over two 8-second runs). |

Memory is about 35 MB working set, almost all of it the .NET runtime.

Note that the walkers are only ever on screen while the mouse has been idle
for two seconds - i.e. while you are *not* using the computer - and they are
gone within half a second of the cursor moving. While you work, the program
is doing nothing but a cheap poll ten times a second.

What keeps it cheap:

- The simulation is trivial: stepping all five walkers plus their
  interactions and face logic takes a few tens of microseconds per frame.
- Slow motions (breathing, swaying, a sleeper's nodding, floating letters)
  are stepped in coarse increments, so a walker that is only idling is
  repainted a few times a second rather than sixty.
- Monitor layout and DPI are read once per frame (`MonitorWorld.Refresh`),
  not once per query.
- Rendering and `UpdateLayeredWindow` happen only for walkers whose picture
  changed, and only for the rectangle that has content (see
  [Rendering](#rendering)).
- Each walker has its own small window; nothing is ever drawn desktop-wide.
- Frames are paced by the compositor (`DwmFlush`), so there is never more
  than one frame per screen refresh and no busy-waiting.
- No timers fire and no hooks are installed while hidden.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| Nothing appears | Is the tray icon there? If not, the program is not running. Check **Pause** is unticked. The characters spawn on the monitor the *cursor* is on. |
| Fewer walkers appear than expected | Check **Walkers** in the tray menu; the count is remembered between runs and defaults to 5. |
| Walkers overlap each other | They can walk through one another; only meetings (greeting, shoving) are physical. This is by design. |
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
- The figure is only about 36 x 54 pixels at 100% scaling; details such as
  individual fingers are not going to happen.
