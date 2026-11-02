# Building, releasing and updating the mod (for the mod author)

Players don't need this file. It explains how to rebuild `YanSimModMenu.dll` from source, package
a release, and port the mod to a new game version.

## What you need
- Windows, with the **.NET SDK** installed (`dotnet --version` should print a version number).
- Yandere Simulator with **BepInEx 5 x64** installed and run once, so that
  `BepInEx\core\BepInEx.dll` and `BepInEx\core\0Harmony.dll` exist.
- The two source files: `YanSimModMenu.cs` and `YanSimModMenu.csproj`.

## Build
1. Open `YanSimModMenu.csproj` in Notepad. Set `<GameDir>` to your game folder, which is the folder that
   contains `YandereSimulator.exe`:
   ```xml
   <GameDir Condition="'$(GameDir)' == ''">C:\Path\To\YandereSimulator</GameDir>
   ```
   Or leave the file alone and pass the folder when you build:
   `dotnet build -c Release -p:GameDir="C:\Path\To\YandereSimulator"`
2. Open PowerShell **in the folder that contains the .csproj** and run:
   ```
   dotnet build -c Release
   ```
3. The output is `bin\Release\net472\YanSimModMenu.dll`. Copy it to `BepInEx\plugins\`.

The `CS0618 ... is obsolete` warnings are harmless. Only **errors** stop the build.

## Make a release zip
1. Raise the version in **two places** in `YanSimModMenu.cs`:
   - the line `[BepInPlugin("local.yansim.modmenu", "Yan Sim Mod Menu", "x.y.z")]`
   - the `Logger.LogInfo("Yan Sim Mod Menu x.y.z loaded...")` line
2. Build in Release mode.
3. Make a zip with this layout, so players can extract it straight into their game folder:
   ```
   YanSimModMenu-x.y.z.zip
   ├─ BepInEx\plugins\YanSimModMenu.dll
   └─ README.md
   ```
4. Add a changelog entry to `README.md`.

**Do not include** `Assembly-CSharp.dll`, any other game files, or the BepInEx files in your zip.
Those belong to their owners. Link players to BepInEx's own release page instead.
You may include the `.cs` and `.csproj` files if you want to share the source.

## When the game updates: porting the mod
The mod is compiled against the game's `YandereSimulator_Data\Managed\Assembly-CSharp.dll`.
Because the `.csproj` points at your game folder, rebuilding picks up the new version automatically.

1. **Update the game.** Reinstall BepInEx if the update removed it.
2. **Rebuild:** `dotnet build -c Release`.
   - **The build succeeds:** copy the DLL to `plugins` and test in game (step 3).
   - **The build fails with `CS1061 'XScript' does not contain a definition for 'Y'` or
     `CS0117`:** the update renamed or removed field `Y`. Find the new name in dnSpy
     (open the new `Assembly-CSharp.dll`, search for the class) and update the `.cs` file.
     Or remove the feature that uses it.
   - **The build fails with `CS0012 / CS1069 ... is defined in an assembly that is not
     referenced`:** add that Unity module as a `<Reference>` in the `.csproj`, like the existing
     `UnityEngine.*Module` lines.
3. **Test in game** and read `BepInEx\LogOutput.log`:
   - `Club perk patches applied: N/20`: if N is lower than 20, the warning lines above it name
     the game methods that were renamed or removed. Update the method list in `InitHarmony()`.
   - `Speed-up patch failed` or `GetWet patch failed`: `YandereScript.UpdateEffects` or
     `StudentScript.GetWet` was renamed or changed.
   - Any **`NullReferenceException`** or **`MissingFieldException`** line with a method name
     tells you which feature to look at.
4. Quick checklist for features that depend on game internals:
   - **Bucket trip:** the animation `f02_bucketTrip_00`, `YandereScript.Tripping`,
     `BucketScript.Fly`/`Full`. If the animation is removed, the log lists her remaining clips.
   - **Club perks:** `ClubManagerScript.ActivateClubBenefit()`, `ClubType` values.
   - **Infinite car battery:** `PickUpScript.CarBattery`/`Broken`. Electrocution checks the
     battery's tag.
   - **Body bag box:** `PickUpScript.GarbageBagBox`/`BodyBags`.
   - **Teleports:** `DebugMenuScript.TeleportSpot[]` indexes (1, 2, 3, 4, 11, 5, 7, 8, 6).
   - **Inventory editor:** reads every public `bool` on `InventoryScript`, so new items appear
     automatically.
5. If BepInEx stops loading the game at all, for example after a large Unity engine upgrade,
   update to the newest BepInEx 5.4.x release. Then rebuild the mod against the new
   `BepInEx\core` files.
