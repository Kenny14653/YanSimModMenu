# Yan Sim Mod Menu

An in-game cheat and tools menu for **Yandere Simulator** (Windows, 64-bit). It works without the
game's debug menu. Open it with **F10**.

> Fan-made mod. Not affiliated with YandereDev. Use it at your own risk, and back up your saves first.

---

## 1. Download and install

You need two things: **BepInEx 5** (the mod loader) and **YanSimModMenu.dll** (this mod).

### Step 1: Install BepInEx 5 (one time only)

1. Go to the BepInEx releases page: <https://github.com/BepInEx/BepInEx/releases>
2. Download the newest **5.4.x** release file named **`BepInEx_win_x64_5.4.xx.zip`**.
   - Use **BepInEx 5**, not BepInEx 6, and get the **x64** file.
3. Open your Yandere Simulator folder. This is the folder that contains **`YandereSimulator.exe`**.
4. Extract the zip **into that folder**. Afterwards the folder should contain:
   ```
   YandereSimulator.exe
   YandereSimulator_Data\
   BepInEx\
   winhttp.dll
   doorstop_config.ini
   ```
5. **Start the game once**, wait until the title screen appears, then close it.
   BepInEx creates its folders (`BepInEx\plugins`, `BepInEx\config`, and so on) on this first run.

### Step 2: Install the mod

1. Copy **`YanSimModMenu.dll`** into **`BepInEx\plugins\`**.
2. Start the game and load into the school.
3. Press **F10** to open the menu.

You can confirm it loaded by opening `BepInEx\LogOutput.log` and looking for
`Yan Sim Mod Menu ... loaded`.

---

## 2. How to use it

### Default keys
These are for playing with the menu **closed**. You can rebind every key on the **Extras** tab.

| Key | What it does |
|---|---|
| **F10** | Open / close the menu |
| **G** | Pick a bomb: smoke, stink or amnesia (shown in the top-right corner) |
| **T** | Throw the selected bomb |
| **B** | Fill the bucket you hold, or the nearest one, with water, blood or gasoline |
| **Left Ctrl** / **RB** | While carrying a bucket and moving: bucket trip, or giggle |
| **V** | Teleport a blood-cleaning kit to you (mop, bucket, bleach, body bag box) |
| **N** | Teleport the drinking-fountain electrocution kit to you (wrench, screwdriver, power strip) |
| **J** | Tripwire kit: get string and masking tape, and bring a knife, the water cooler and a bucket |
| **U** | Teleport the dismemberment kit to you (circular saw and tarp) |
| **H** | Teleport the cover-up kit to you (raincoat, occult robe, gloves) |
| **`** (hold) | Fast-forward anything while held, cutscenes included |
| **Z** | Start / stop pouring the bucket you're holding. It never runs dry and leaves a trail of puddles (can be switched off on the Extras tab) |
| **+** / **-** on the Student Info screen | Raise or lower that student's reputation by 10 (hold Shift for 1) |
| **P** | Add info points |
| **K** | Switch rival elimination shortcut (off / matchmaking / rejection / expel), then **Enter** to confirm or **Backspace** to cancel |
| **=** (+) / numpad **+** | Speed up time like the debug menu: 2x, 3x, 4x, 5x, 10x, 15x, 20x, 25x |
| **-** / numpad **-** | Time back to normal |
| **Left Alt + 1–9** | Teleport: 1 School front · 2 Classroom 2-1 · 3 Computer lab · 4 Rooftop · 5 Shower room · 6 Martial Arts · 7 Occult · 8 Gardening · 9 Light Music. Each of the 9 keys can be rebound on the Extras tab; Left Alt is still held with them |

### The tabs
- **Player:** lock sanity and health, move speed, keep all stats maxed, money, keep reputation at 100.
- **Items:** tick any inventory item to get it, infinite items, pinned items that come back every day,
  bombs, Alphabet Challenge tools, infinite car battery, unlimited body bag box.
- **Weapons:** bring weapons and murder tools to you or duplicate them, blood-clean kit, buckets.
- **Students:** befriend everyone, tasks, Student Info, likes and dislikes (all of these can run
  automatically), own every gift-shop gift, instant matchmaking (the rival confesses to her suitor at
  the end of the day, in 202X and 1980s mode), more students watching the rival each week in 1980s Custom
  Mode, a student list with bring-to-me / go-to-them, and a club filter.
- **Debug:** the game's debug commands without the debug menu (teleports, day, uniform, week,
  progress, world and camera options).
- **Scenes:** every scene in the game, with a search box, to load straight away. Shortcuts for school, home, title and calendar.
- **Extras:** keybinds, time controls, Speed Up Time (no Senpai photo needed, up to 25x),
  all club perks without joining (Delinquents excluded), and easter eggs.

### Settings
- Your settings save automatically when you close the menu. They are stored in
  `BepInEx\config\local.yansim.modmenu.cfg`.
- To reset everything, close the game and delete that file.
- **Pause the game while this menu is open** is a checkbox at the top of the menu.

---

## 3. When Yandere Simulator updates

Updates often replace the game folder. Go through this list after every update:

1. **Check that BepInEx is still there.** You should still see `BepInEx\`, `winhttp.dll` and
   `doorstop_config.ini` next to `YandereSimulator.exe`.
   - If they are gone, repeat **Step 1** and **Step 2** above.
   - Your settings live in `BepInEx\config\`, so back that folder up before updating if you want
     to keep them.
2. **Start the game and press F10.**
   - **The menu opens and works:** you're done.
   - **The menu does not open, or some features stop working:** open `BepInEx\LogOutput.log`
     and look for red **Error** or yellow **Warning** lines that mention *Yan Sim Mod Menu*.
     The update probably renamed or removed something the mod uses.
3. **If it is broken,** remove `YanSimModMenu.dll` from `BepInEx\plugins\` so the game runs
   normally, and wait for an updated version of the mod. When you report the problem, please
   include your `LogOutput.log`.

These are the lines to look for in `LogOutput.log` on a working install:
```
Yan Sim Mod Menu 0.20 loaded.
Club perk patches applied: 20/20
Speed-up patch applied.
Bucket spill patch applied.
Club changing booth patch applied.
Bucket trip reputation patch applied.
```
If the number in "Club perk patches applied" is lower than 20, or a line says "failed", a game update
changed that part of the game. The rest of the menu still works.

---

## 4. Uninstall

- **Remove the mod only:** delete `BepInEx\plugins\YanSimModMenu.dll`, and optionally
  `BepInEx\config\local.yansim.modmenu.cfg`.
- **Remove BepInEx as well:** delete `BepInEx\`, `winhttp.dll`, `doorstop_config.ini` and
  `.doorstop_version` if it exists.

---

## 5. Troubleshooting

| Problem | Fix |
|---|---|
| Nothing happens on F10, and there is no `LogOutput.log` | BepInEx isn't running. Check that `winhttp.dll` sits next to `YandereSimulator.exe` and that you used the **x64** BepInEx 5 zip. Some antivirus programs quarantine `winhttp.dll`; restore it or add an exception. |
| The log exists but the mod isn't listed | The DLL isn't in `BepInEx\plugins\`, or it's inside an extra subfolder that has the wrong DLL in it. |
| The mouse cursor is invisible | The menu draws its own cursor while it is open. If it is missing, open and close the menu once. |
| A number key teleports me when I drop an item | Keep **"Only while holding Left Alt"** turned on (Debug tab). |
| Something stopped working after a game update | See section 3. |

---

## Changelog

**0.20**
- New **Scenes** tab: every scene in the game is listed and loads with one click. There's a search box and shortcuts for school, home, the title screen and the calendar. Save first: jumping into a scene the game normally reaches another way can start it with odd data.
- Debug tab: set the clock to any time (hour and minute sliders), or jump to before class, class, lunch, cleaning or after school.

**0.19.1** — With "all club perks" on, every club's changing booth works for you and gives you that club's outfit, not just your own club's (Delinquents, bullies and the student council excluded).

**0.19**
- Instant rejection: lowering the rival's reputation (gossip, rumors and so on) now fills the bar too, not only sabotaging her events with Senpai. The Students tab shows the bar (x/5) and has a "Fill it now" button. She's still rejected at Friday's confession, which is when the game checks.
- Speed up actions (Weapons tab): attack kills, dismembering, washing weapons, showering, and corpse disposal (incinerator, dumpster, wood chipper, burying), all with one speed slider (1–10x).
- Hold **`** to fast-forward anything, cutscenes included. Voiced lines keep their normal speed, so a scene that waits for its voice line still waits.
- **H** teleports the cover-up kit: raincoat, occult robe and gloves.

**0.18**
- Instant body bag wrapping (Weapons tab): the wrap finishes in a fraction of a second.
- Speed up attack kills (Weapons tab), 1–10x: the game runs faster only while the kill plays, then goes back to normal.
- Bring extra from home (Items tab): every school day, also bring any of the 3 bring-item weapons and any of the other bring-item items (poisons, sake, cigarettes, condoms, lockpick, narcotics, explosive device), on top of the game's own pick. Weapons go straight to your hands while you have a free slot; the rest are put next to you.
- Each of the 9 teleport locations has its own rebindable key (Extras tab), still pressed together with Left Alt.
- Always have the Void Goddess at school ("Summon An Ancient Evil"), without the debug menu (Students tab). The game marks runs that use her as invalid, the same as the debug menu.
- The tripwire kit no longer fills the water cooler: it brings a bucket instead, so you choose the liquid.
- **U** teleports the dismemberment kit (circular saw and tarp).

**0.17** — Faster loading screens (Extras tab, on by default). Unity is allowed to spend more of each frame on loading, and the loading screen starts loading the school right away instead of counting 10 frames first. How much faster depends on your PC; most of the wait is the game building the school and its students.

**0.16.2** — Fixed the keys (K, N, J, Z, P, teleports) not working at home: one check threw an error there every frame.

**0.16.1** — **Z** is now an endless pour you switch on and off. While it's on, the bucket you hold leaves a puddle of whatever is in it (water, blood, gasoline or brown paint) every half second or every few steps, and never runs dry. Press Z again to stop. It also stops when you put the bucket down. The game's own Z still does nothing, and the Extras-tab checkbox switches the key off completely.

**0.16**
- **N** teleports the drinking-fountain electrocution kit to you. Wrench the fountain to make a puddle, turn the light off, use the screwdriver on the outlet, then turn the light on when she's in the puddle.
- **Z** spills the full bucket you're holding, without needing the game's debug keys. A checkbox on the Extras tab switches it off, so nothing spills it by key, not even the game's own Z.
- **+** / **-** on the Student Info screen raise or lower that student's reputation by 10, or by 1 while holding Shift. Time speed keys don't react while that screen is open.
- **J** tripwire kit: gives you string and masking tape, brings a knife and a water cooler, and fills the cooler if it's empty (with blood on the difficulty that needs mud).
- Students tab: always unlock the yakuza with every service (1980s).
- Students tab: always know the rival's embarrassing secret (for gossip) and her dark secret (the photographer secret, for a locker note).

**0.15.1** — Each part of the menu now runs on its own. Before, one error repeating every frame (for example in a scene that part didn't expect) silently switched off everything after it, including instant rejection, expel and matchmaking and the K key. Errors are now logged with the part's name, once every 10 seconds.

**0.15** — 1980s Custom Mode: more students watch the rival each week (Students tab, on by default). Week 1 adds none and week 10 adds 6. They give up their free time (morning, lunch, after school) to follow whoever this week's rival is, the same way story mode guards its week-10 rival. Students the Custom Mode Editor already made watchers are left as they are.

**0.14.1** — "Lock sanity" only runs during a school day (it threw an error every frame on the Custom Mode and portrait screens).

**0.14** — K cycles the rival elimination shortcut (off / instant matchmaking / instant rejection / instant expel) with an "Are you sure you want this method?" confirmation.

**0.13.3** — Instant matchmaking, rejection and expel are now one-at-a-time. Switching away from matchmaking resets the rival/suitor affection it maxed and cancels that day's confession, so rejection or expel can happen instead.

**0.13.2** — Menu opens more reliably outside school: a second way of catching the menu key, and errors in one tab or overlay no longer stop the window from showing. The log now says when and in which scene the menu opens.

**0.13.1** — Instant mind-broken slave: one torture session in the basement takes the prisoner's sanity to 0%.

**0.13** — Instant expel: one successful report to the counselor fills the rival's expel bar (5/5).

**0.12.1** — Easy Befriend/Betray now covers the asylum: the photographer, squatters and guards never notice you.

**0.12** — Instant rejection (one sabotage fills the rejection bar to 5/5). Easy Befriend/Betray minigames: the stalker doesn't notice you up close, struggles fill on their own and can't be lost, pickpocketing has no time limit.

**0.11** — Mission Mode: show Nemesis in Yandere vision (purple outline + distance in the corner). Locker notes: the subjects that will work for that student turn green, with a summary box at the top.

**0.10.2** — The run-speed box now also covers crouching, crouch-running and crawling.

**0.10.1** — Walk/drag-at-run-speed now uses the game's exact running formula (run speed + club bonus + panty bonus + Physical x 0.2).

**0.10** — Debug-menu style time keys: + speeds time up step by step (up to 25x), - returns to normal. Yandere-vision Speed Up Time keeps its own speed slider.

**0.9.1** — Washing machine joins instant disposal (and takes any clothing). Incinerator / kiln timing moved into game-side patches. New Player-tab box: walk and drag bodies at running speed.

**0.9** — Complete the whole Content Checklist with one button (Extras tab). No time limit on corpse disposal: the incinerator and kiln finish instantly (Weapons tab).

**0.8** — Own everything in the gift shop automatically. Instant matchmaking checkbox (202X and 1980s).

**0.7.1** — Number-key teleports need Left Alt held, so dropping items no longer teleports you.

**0.7** — Always-on boxes for maxed stats, maxed reputation and Alphabet tools. Automatic befriend,
tasks, Student Info and likes. Bomb picker (G) with a corner display. Number-key teleports.
P adds info points. Infinite car battery and body bag box. Better performance.

**0.6.x** — The June 2016 bucket trip, rebuilt. Blood-clean kit (V). Checked items work without
picking them up first. All club perks without joining. Speed Up Time without a photo, up to 25x.

**0.5 and earlier** — Inventory editor, pinned and infinite items, bombs in normal mode, stats,
befriend, tasks, Student Info, likes and dislikes with the compliment helper, weapon teleport and
duplicate, debug commands, rebindable keys, saved settings, crouch speed matching.
