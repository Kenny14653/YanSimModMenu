using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

// Yandere Simulator mod menu v0.2 (BepInEx 5, Mono build).
// Toggle key: F10 by default. Does NOT use or enable the game's debug menu and
// never touches GameGlobals.Debug.
//
// Game members used here were checked against Assembly-CSharp.dll metadata.
// Behavior has not been tested in-game.

namespace YanSimModMenu
{
    [BepInPlugin("local.yansim.modmenu", "Yan Sim Mod Menu", "0.20")]
    public class ModMenuPlugin : BaseUnityPlugin
    {
        ConfigEntry<KeyCode> toggleKey;
        ConfigEntry<bool> pauseWhileOpen;
        ConfigEntry<KeyCode> throwKey;
        ConfigEntry<KeyCode> bucketKey;
        ConfigEntry<KeyCode> giggleKey;
        ConfigEntry<bool> cfgMatchCrouch, cfgGiggleEnabled, cfgPinItems;
        ConfigEntry<string> cfgKept;
        ConfigEntry<KeyCode> cleanupKey;
        ConfigEntry<bool> cfgAllClub, cfgNoPhoto, cfgFastUp;
        ConfigEntry<float> cfgUpScale;

        // v0.6 state (static so the Harmony patches can read it)
        public static bool allClubPerks, noPhotoSpeed = true, fastSpeedUp = true;
        public static float speedUpScale = 25f;
        static int shotsRestore = -1;
        ClubManagerScript clubOwner;
        float syncAt = -1f;
        Harmony harmony;

        // v0.7: always-on boxes, bomb picker, number-key teleports, infinite battery / body bags
        ConfigEntry<KeyCode> bombCycleKey, infoKey, teleModKey;
        // Debug-menu style time keys: + steps 1,2,3,4,5,10,15,20,25 ; - back to normal
        ConfigEntry<KeyCode> timeUpKey, timeResetKey;
        ConfigEntry<KeyCode> elimKey;
        ConfigEntry<KeyCode> elecKey, tripKey, spillKey, dismemberKey;
        readonly ConfigEntry<KeyCode>[] teleSlotKeys = new ConfigEntry<KeyCode>[9];
        ConfigEntry<bool> cfgInstantWrap, cfgFastAttack, cfgVoidGoddess, cfgExtraBring;
        ConfigEntry<float> cfgAttackSpeed;
        ConfigEntry<string> cfgExtraBringIds, cfgBringNames;
        bool instantWrap, fastAttack, voidGoddess, extraBring, attackBoosting;
        ConfigEntry<bool> cfgFastDismember, cfgFastWash, cfgFastShower, cfgFastDispose;
        ConfigEntry<KeyCode> fastForwardKey, coverKey;
        bool fastDismember, fastWash, fastShower, fastDispose;
        StudentManagerScript rejectScene; float rivalRepBaseline = float.NaN;
        float attackSpeed = 3f, attackRestoreScale = 1f;
        readonly HashSet<int> extraBringIds = new HashSet<int>();
        string[] bringNames;
        int bringNamesScene = -1;
        StudentManagerScript bringScene, goddessScene;
        ConfigEntry<bool> cfgSpillKeyOn, cfgAlwaysYakuza, cfgRivalSecrets;
        bool spillKeyOn = true, alwaysYakuza, rivalSecrets, pouring;
        ConfigEntry<bool> cfgFastLoading;
        bool fastLoading = true, fastLoadingApplied;
        SceneLoader loaderSeen;
        int loaderSceneChecked = -1;
        float pourTimer;
        float yakuzaTimer;
        StudentInfoScript infoCache; StudentManagerScript infoScene;
        int elimPending = -1; // method waiting for "Are you sure?" (-1 = none)
        static readonly string[] ElimNames = { "Off (no shortcut)", "Instant matchmaking", "Instant rejection", "Instant expel" };
        public static float hotkeyScale = 1f;
        ConfigEntry<bool> cfgMaxStats, cfgMaxRep, cfgAlphaAlways, cfgBombHud, cfgAutoBefriend, cfgAutoTasks,
                          cfgAutoInfo, cfgAutoLikes, cfgTeleKeys, cfgTeleAlt, cfgInfBattery, cfgInfBags;
        ConfigEntry<int> cfgBombSel, cfgInfoAmount;
        bool maxStats, maxRep, alphaAlways, bombHud = true, autoBefriend, autoTasks, autoInfo, autoLikes,
             teleKeys = true, teleAlt, infBattery, infBags;
        int bombSel, infoAmount = 20;
        static readonly string[] BombNames = { "Smoke", "Stink", "Amnesia" };
        StudentManagerScript autoScene, clubScene, alphaScene, pickScene;
        float autoTimer, toastUntil;
        string toast = "";
        AlphabetScript alphaCache;
        readonly List<PickUpScript> batteries = new List<PickUpScript>(), bagBoxes = new List<PickUpScript>();
        readonly Dictionary<PickUpScript, float> brokenAt = new Dictionary<PickUpScript, float>();
        readonly Dictionary<PickUpScript, string> batteryTag = new Dictionary<PickUpScript, string>();
        GUIStyle hudStyle;

        // v0.8: gift shop + matchmaking
        ConfigEntry<bool> cfgAutoGifts, cfgInstantMatch;
        bool autoGifts, instantMatch;
        StudentManagerScript matchScene, watchScene;
        ConfigEntry<bool> cfgWeeklyWatchers;
        bool weeklyWatchers;

        // v0.9: instant corpse disposal + content checklist
        ConfigEntry<bool> cfgInstantDisposal;
        public static bool instantDisposal;
        ConfigEntry<bool> cfgWalkAsRun;
        bool walkAsRun, walkOverridden;
        StudentManagerScript disposalScene;

        // v0.11: Nemesis in Yandere vision, locker-note subject helper
        ConfigEntry<bool> cfgNemesisVision, cfgNoteHelper;
        bool nemesisVision, noteHelper = true;
        NemesisScript nemesisCache;
        StudentManagerScript nemesisScene;
        bool nemesisSearched, nemesisLit;
        float nemesisTimer;
        readonly Dictionary<OutlineScript, Color> nemesisOrig = new Dictionary<OutlineScript, Color>();
        static readonly Color NemesisColor = new Color(6f, 0f, 10f, 1f); // bright purple (the target is red)
        static readonly FieldInfo OutlineHighlighter =
            typeof(OutlineScript).GetField("h", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        // v0.12: easy Befriend/Betray minigames, instant rejection
        ConfigEntry<bool> cfgEasyMinigames, cfgInstantReject;
        public static bool easyMinigames;
        bool instantReject;
        ConfigEntry<bool> cfgInstantExpel;
        bool instantExpel;
        bool undoMatchPending;
        ConfigEntry<bool> cfgInstantBreak;
        public static bool instantBreak;
        CounselorScript counselorCache;
        StudentManagerScript counselorScene;
        static readonly Dictionary<StalkerScript, float> stalkerOrigDist = new Dictionary<StalkerScript, float>();
        static readonly Dictionary<EvilPhotographerScript, float> asylumOrigDist = new Dictionary<EvilPhotographerScript, float>();
        NoteWindowScript noteWin;
        StudentManagerScript noteScene;
        bool noteSearched, noteActive;
        List<int> noteGood = new List<int>();
        string noteExtra = "";
        readonly Dictionary<UILabel, Color> noteTinted = new Dictionary<UILabel, Color>();
        readonly List<IncineratorScript> incinerators = new List<IncineratorScript>();
        readonly List<WoodChipperScript> chippers = new List<WoodChipperScript>();
        static readonly string[] ChecklistKeys =
        {
            "Attack", "Befriend", "Betray", "Bully", "Burn", "Crush", "Drown", "Electrocute", "Expel", "Fan",
            "Frame", "Kidnap", "Matchmake", "MurderSuicide", "Poison", "Pool", "Push", "Reject", "Suicide",
            "DrivenToMurder", "HeadHunter", "PantyQueen", "RichGirl", "WeaponMaster", "HeadsHunted", "Alphabet",
            "Amai", "Dance", "Genocide", "Headmaster", "Journalist", "LifeNote", "Maid", "Mission", "Miyuki",
            "Basement", "Panther", "Selene", "SNAP", "Yakuza", "Yanvania", "Custom", "Delinquent", "Dream",
            "Explode", "Happy", "Maiden", "Robot", "S+"
        };
        // Number keys 1-9: debug teleport spot index, x offset, name
        static readonly int[] TeleSpot = { 1, 2, 3, 4, 11, 5, 7, 8, 6 };
        static readonly string[] TeleName = { "School front", "Classroom 2-1", "Computer lab", "Rooftop", "Shower room",
                                              "Martial Arts Club", "Occult Club", "Gardening Club", "Light Music Club" };

        // Saved settings (written to the BepInEx config when the menu closes)
        ConfigEntry<bool> cfgLockSanity, cfgLockHealth, cfgFreezeClock, cfgGameSpeedOn,
                          cfgInfiniteItems, cfgShowHelper, cfgKeepBombs, cfgCycleLiquid;
        ConfigEntry<int> cfgLockedHealth, cfgBucketLiquid;
        ConfigEntry<float> cfgSpeedMult, cfgTimeMult, cfgGameSpeed, cfgSmoke, cfgStink, cfgAmnesia;

        bool open;
        int lastToggleFrame = -1;
        bool guiErrorLogged;
        Rect window = new Rect(30, 30, 520, 680);
        Vector2 scroll, listScroll;
        int tab;
        readonly string[] tabs = { "Player", "Items", "Weapons", "Students", "Debug", "Scenes", "Extras" };
        string[] sceneNames;
        string sceneFilter = "";
        int setHour = 13, setMinute = 0;
        float refreshTimer;
        string status = "";

        // Cached scene objects
        YandereScript yan;
        StudentManagerScript manager;
        ReputationScript rep;
        ClockScript clock;
        WeaponManagerScript weaponManager;
        RPG_Camera cam;

        // UI-mode bookkeeping (block game camera/movement while menu is open)
        bool camWasEnabled, canMoveWas;

        // Player
        bool lockSanity, lockHealth;
        int lockedHealth;
        float speedMult = 1f;
        bool speedActive;
        YandereScript speedOwner;
        float baseWalk, baseRun, baseCrouchWalk, baseCrouchRun;

        // Clock
        bool freezeClock, clockFrozenByUs;
        float timeMult = 1f;
        bool timeActive;
        ClockScript timeOwner;
        float baseTimeSpeed;

        // Game speed
        bool gameSpeedOn;
        float gameSpeed = 1f;
        float savedTimeScale = 1f;

        // Bombs (counts include the one currently in your inventory)
        int smokeCount, stinkCount, amnesiaCount;
        bool smokeManaged, stinkManaged, amnesiaManaged;
        float smokeSlider, stinkSlider, amnesiaSlider;

        // Inventory editor
        FieldInfo[] invFields;
        string invFilter = "";
        static readonly HashSet<string> IntItems = new HashSet<string>
        {
            "LockPicks", "EmeticPoisons", "SedativePoisons", "HeadachePoisons", "LethalPoisons", "Flyers"
        };

        // Infinite items
        bool infiniteItems;
        FieldInfo[] trackFields;
        object[] invSnapshot;
        InventoryScript snapOwner;
        bool invDirty;

        // Crouch/crawl speed matching, bucket giggle, pinned items
        bool matchCrouch = true;
        float baseCrawl;
        bool giggleEnabled = true;
        bool giggleRunning;
        ConfigEntry<int> cfgBucketAction;
        int bucketAction; // 0 = trip (June 2016), 1 = giggle
        bool tripRunning;
        bool rbMissing;
        bool pinItems = true;
        readonly Dictionary<string, int> keptItems = new Dictionary<string, int>();
        InventoryScript keptOwner;

        // Compliment helper, club filter
        SocialScript socialUI;
        int clubFilter = -1;
        ClubType[] clubValues;
        static readonly string[] ComplimentNames =
        {
            "self-reliant", "smart", "heroic", "cautious", "cunning", "friendly", "loving", "strong",
            "professional", "informed", "a victim", "sinister", "observant", "determined",
            "protective", "intimidating", "talkative", "sexy"
        };

        // Bombs
        bool keepBombsStocked;
        InventoryScript bombOwner;

        // Buckets
        int bucketLiquid;
        bool cycleLiquid;
        static readonly string[] LiquidNames = { "Water", "Blood", "Gasoline" };

        // Topic helper and keybind capture
        bool showHelper = true;
        TopicInterfaceScript topicUI;
        int rebinding = -1;

        // Students
        string filter = "";
        StudentScript selected;
        GUIStyle selStyle;

        // Software mouse cursor (the game hides the real one)
        Texture2D cursorTex;
        const int CursorW = 12, CursorH = 17, CursorScale = 2;
        static readonly string[] CursorPixels =
        {
            "X...........",
            "XX..........",
            "XWX.........",
            "XWWX........",
            "XWWWX.......",
            "XWWWWX......",
            "XWWWWWX.....",
            "XWWWWWWX....",
            "XWWWWWWWX...",
            "XWWWWWWWWX..",
            "XWWWWWXXXXX.",
            "XWWXWWX.....",
            "XWX.XWWX....",
            "XX..XWWX....",
            "X....XWWX...",
            ".....XWWX...",
            "......XX...."
        };

        void BuildCursorTexture()
        {
            cursorTex = new Texture2D(CursorW, CursorH, TextureFormat.RGBA32, false);
            cursorTex.filterMode = FilterMode.Point;
            for (int row = 0; row < CursorH; row++)
            {
                for (int x = 0; x < CursorW; x++)
                {
                    char ch = CursorPixels[row][x];
                    Color c = ch == 'X' ? Color.black : (ch == 'W' ? Color.white : new Color(0f, 0f, 0f, 0f));
                    cursorTex.SetPixel(x, CursorH - 1 - row, c);
                }
            }
            cursorTex.Apply();
        }

        // Easter eggs (YandereScript methods, invoked by reflection)
        static readonly string[] EggNames =
        {
            "Bancho", "Sukeban", "Slend", "Falcon", "Punch", "BadTime", "CyborgNinja", "Samus",
            "Witch", "Tornado", "Mandere", "Ghoul", "Berserk", "Sith", "Snake", "Gazer", "KLK",
            "Luna", "Lacuna", "Miyuki", "Cirno", "Agent", "Jojo", "Maid", "Horror", "Blacklight",
            "Vaporwave", "GarbageMode", "TallMode", "HollowMode", "Nier", "Long", "Six", "ElfenLied",
            "BlackHoleChan", "Hate", "Punish", "KON", "AttackOnTitan", "GaloSengen", "WearRaincoat",
            "AzurLane", "PpStarrs", "CazTrap", "Medibang", "WearChinaDress", "BecomeRyoba", "Cloak"
        };
        readonly Dictionary<string, MethodInfo> eggMethods = new Dictionary<string, MethodInfo>();
        bool eggsBuilt;

        void Awake()
        {
            Config.SaveOnConfigSet = false; // we save when the menu closes, not on every slider tick

            toggleKey = Config.Bind("Keys", "ToggleKey", KeyCode.F10, "Key that opens/closes the mod menu.");
            throwKey = Config.Bind("Keys", "ThrowBombKey", KeyCode.T, "Key that throws the bombs you are holding (smoke/stink/amnesia) in normal gameplay.");
            bucketKey = Config.Bind("Keys", "FillBucketKey", KeyCode.B, "Key that fills the bucket you hold (or the nearest one) with the chosen liquid.");
            giggleKey = Config.Bind("Keys", "GiggleKey", KeyCode.LeftControl, "Key that makes Yandere-chan giggle while she is carrying a bucket and moving (controller: RB).");
            cleanupKey = Config.Bind("Keys", "CleanupKitKey", KeyCode.V, "Key that teleports a blood-clean kit (mop, bucket, bleach, body bag box) to you.");
            cfgAllClub = Config.Bind("Saved", "AllClubPerks", false, "Get every club's perks (except Delinquent) without joining.");
            cfgNoPhoto = Config.Bind("Saved", "SpeedUpNoPhoto", true, "Speed Up Time works without a photo of Senpai.");
            cfgFastUp = Config.Bind("Saved", "SpeedUpFast", true, "Speed Up Time uses the fast debug-menu style speed.");
            cfgUpScale = Config.Bind("Saved", "SpeedUpScale", 25f, "Time scale used by Speed Up Time (2-25).");
            bombCycleKey = Config.Bind("Keys", "BombCycleKey", KeyCode.G, "Key that cycles which bomb the throw key uses (smoke, stink, amnesia).");
            infoKey = Config.Bind("Keys", "InfoPointsKey", KeyCode.P, "Key that adds info points.");
            timeUpKey = Config.Bind("Keys", "TimeFasterKey", KeyCode.Equals, "Speed up time like the debug menu (+1 up to 5x, then +5 up to 25x). Numpad + also works.");
            timeResetKey = Config.Bind("Keys", "TimeNormalKey", KeyCode.Minus, "Set time back to normal speed. Numpad - also works.");
            elimKey = Config.Bind("Keys", "EliminationMethodKey", KeyCode.K, "Cycle the rival elimination shortcut (off / matchmaking / rejection / expel); Enter confirms, Backspace cancels.");
            elecKey = Config.Bind("Keys", "ElectrocutionKitKey", KeyCode.N, "Teleport the drinking-fountain electrocution kit (wrench, screwdriver, power strip) to you.");
            tripKey = Config.Bind("Keys", "TripwireKitKey", KeyCode.J, "Give the tripwire items (string, masking tape) and bring a knife and a filled water cooler.");
            fastForwardKey = Config.Bind("Keys", "FastForwardHoldKey", KeyCode.BackQuote, "Hold to fast-forward anything (cutscenes too) at the action speed.");
            coverKey = Config.Bind("Keys", "CoverUpKitKey", KeyCode.H, "Teleport the things that hide blood to you (raincoat, occult robe, gloves).");
            dismemberKey = Config.Bind("Keys", "DismembermentKitKey", KeyCode.U, "Teleport the dismemberment kit (circular saw and tarp) to you.");
            for (int i = 0; i < 9; i++)
                teleSlotKeys[i] = Config.Bind("Keys", "TeleportKey" + (i + 1), KeyCode.Alpha1 + i, "Teleport to " + TeleName[i] + " (held together with the teleport modifier key).");
            spillKey = Config.Bind("Keys", "SpillBucketKey", KeyCode.Z, "Start / stop pouring the bucket you're holding (it never runs dry).");
            cfgMaxStats = Config.Bind("Saved", "KeepStatsMaxed", false, "Keep every stat at 5.");
            cfgMaxRep = Config.Bind("Saved", "KeepReputationMaxed", false, "Keep reputation at 100.");
            cfgAlphaAlways = Config.Bind("Saved", "AlphabetToolsAlways", false, "Always turn on the Alphabet Challenge tools.");
            cfgBombSel = Config.Bind("Saved", "SelectedBomb", 0, "0 = smoke, 1 = stink, 2 = amnesia.");
            cfgBombHud = Config.Bind("Saved", "ShowBombHud", true, "Show the selected bomb in the corner.");
            cfgAutoBefriend = Config.Bind("Saved", "AutoBefriend", false, "Befriend everyone automatically.");
            cfgAutoTasks = Config.Bind("Saved", "AutoCompleteTasks", false, "Complete everyone's tasks automatically.");
            cfgAutoInfo = Config.Bind("Saved", "AutoStudentInfo", false, "Fill the Student Info screen automatically.");
            cfgAutoLikes = Config.Bind("Saved", "AutoLearnLikes", false, "Learn everyone's likes and dislikes automatically.");
            cfgTeleKeys = Config.Bind("Saved", "NumberKeyTeleports", true, "Number keys 1-9 teleport you (menu closed).");
            cfgTeleAlt = Config.Bind("Saved", "NumberKeyTeleportsNeedModifier", true, "Hold the teleport modifier key with the number key to teleport (stops clashes with the game's own number keys, e.g. dropping items).");
            teleModKey = Config.Bind("Keys", "TeleportModifierKey", KeyCode.LeftAlt, "Hold this with a number key 1-9 to teleport.");
            cfgInfBattery = Config.Bind("Saved", "InfiniteCarBattery", false, "The car battery can electrocute again after use.");
            cfgInfBags = Config.Bind("Saved", "InfiniteBodyBags", false, "The body bag box never runs out.");
            cfgInfoAmount = Config.Bind("Saved", "InfoPointsPerPress", 20, "Info points added per key press.");
            cfgAutoGifts = Config.Bind("Saved", "AutoGiftShop", false, "Own every gift-shop gift (Senpai and matchmaking gifts) every time a scene loads.");
            cfgInstantWrap = Config.Bind("Saved", "InstantBodyBagWrap", false, "Wrapping a corpse in a body bag finishes almost instantly.");
            cfgFastAttack = Config.Bind("Saved", "FastAttackKills", false, "Speed up attack kills (the whole game runs faster only while the kill plays).");
            cfgAttackSpeed = Config.Bind("Saved", "AttackKillSpeed", 3f, "How fast attack kills play, 1-10x.");
            cfgFastDismember = Config.Bind("Saved", "FastDismember", false, "Speed up dismembering.");
            cfgFastWash = Config.Bind("Saved", "FastWeaponWashing", false, "Speed up washing blood off weapons (sinks, drinking fountain).");
            cfgFastShower = Config.Bind("Saved", "FastShower", false, "Speed up showering.");
            cfgFastDispose = Config.Bind("Saved", "FastCorpseDisposal", false, "Speed up getting rid of corpses: incinerator, dumpster, wood chipper, burying.");
            cfgVoidGoddess = Config.Bind("Saved", "AlwaysVoidGoddess", false, "Always have the Void Goddess at school ('Summon An Ancient Evil'), without the debug menu.");
            cfgExtraBring = Config.Bind("Saved", "BringExtraFromHome", false, "Bring the extra items and weapons ticked in the Items tab every school day.");
            cfgExtraBringIds = Config.Bind("Saved", "ExtraBringIds", "", "Bring-from-home IDs to bring as extras (comma separated).");
            cfgBringNames = Config.Bind("Saved", "BringItemNames", "", "Names of the bring-from-home items, read from the game's own menu.");
            cfgFastLoading = Config.Bind("Saved", "FastLoading", true, "Faster loading screens: Unity may spend more of each frame loading, and the loading screen starts right away.");
            cfgSpillKeyOn = Config.Bind("Saved", "SpillKeyOn", true, "The pour key starts / stops an endless pour. Off: no key pours, not even the game's own Z.");
            cfgAlwaysYakuza = Config.Bind("Saved", "AlwaysUnlockYakuza", false, "Keep the yakuza unlocked with every service (1980s).");
            cfgRivalSecrets = Config.Bind("Saved", "AlwaysKnowRivalSecrets", false, "Always know the rival's embarrassing secret (gossip) and dark secret (locker note).");
            cfgWeeklyWatchers = Config.Bind("Saved", "WeeklyRivalWatchers", true, "1980s Custom Mode: more students watch the rival each week (week 1: none, week 10: 6 extra).");
            cfgInstantMatch = Config.Bind("Saved", "InstantMatchmaking", false, "Max the rival/suitor affection so the rival confesses to her suitor at the end of the first day (202X and 1980s).");
            pauseWhileOpen = Config.Bind("General", "PauseWhileOpen", true, "Pause the game (timeScale 0) while the menu is open.");

            cfgLockSanity = Config.Bind("Saved", "LockSanity", false, "Keep sanity at 100%.");
            cfgLockHealth = Config.Bind("Saved", "LockHealth", false, "Keep health locked.");
            cfgLockedHealth = Config.Bind("Saved", "LockedHealthValue", 0, "Health value to lock to (0 = use current when first enabled).");
            cfgSpeedMult = Config.Bind("Saved", "SpeedMultiplier", 1f, "Player move speed multiplier.");
            cfgFreezeClock = Config.Bind("Saved", "FreezeClock", false, "Freeze the in-game clock.");
            cfgTimeMult = Config.Bind("Saved", "ClockSpeed", 1f, "In-game clock speed multiplier.");
            cfgGameSpeedOn = Config.Bind("Saved", "GameSpeedOn", false, "Apply the game speed multiplier.");
            cfgGameSpeed = Config.Bind("Saved", "GameSpeed", 1f, "Game speed multiplier.");
            cfgInfiniteItems = Config.Bind("Saved", "InfiniteItems", false, "Do not lose inventory items when using them.");
            cfgShowHelper = Config.Bind("Saved", "ShowTopicHelper", true, "Show which topic to pick while talking to a student.");
            cfgKeepBombs = Config.Bind("Saved", "KeepBombsStocked", false, "Re-give the chosen bomb counts every time the school loads.");
            cfgSmoke = Config.Bind("Saved", "SmokeBombs", 0f, "Smoke bomb count.");
            cfgStink = Config.Bind("Saved", "StinkBombs", 0f, "Stink bomb count.");
            cfgAmnesia = Config.Bind("Saved", "AmnesiaBombs", 0f, "Amnesia bomb count.");
            cfgBucketLiquid = Config.Bind("Saved", "BucketLiquid", 0, "0 = water, 1 = blood, 2 = gasoline.");
            cfgCycleLiquid = Config.Bind("Saved", "CycleBucketLiquid", false, "Cycle the liquid each time the fill key is pressed.");
            cfgMatchCrouch = Config.Bind("Saved", "MatchCrouchSpeed", true, "Crouch and crawl speeds follow your walk/run speed when the multiplier is changed.");
            cfgBucketAction = Config.Bind("Saved", "BucketKeyAction", 0, "What the bucket key does: 0 = trip and fling the bucket, 1 = giggle.");
            cfgGiggleEnabled = Config.Bind("Saved", "GiggleEnabled", true, "Enable the bucket giggle (press the giggle key while carrying a bucket).");
            cfgPinItems = Config.Bind("Saved", "PinItems", true, "Items you check in the menu are re-given automatically each time the school loads.");
            cfgKept = Config.Bind("Saved", "PinnedItems", "", "Pinned inventory items (Name=count;Name=count).");

            lockSanity = cfgLockSanity.Value;
            lockHealth = cfgLockHealth.Value;
            lockedHealth = cfgLockedHealth.Value;
            speedMult = cfgSpeedMult.Value;
            freezeClock = cfgFreezeClock.Value;
            timeMult = cfgTimeMult.Value;
            gameSpeedOn = cfgGameSpeedOn.Value;
            gameSpeed = cfgGameSpeed.Value;
            infiniteItems = cfgInfiniteItems.Value;
            showHelper = cfgShowHelper.Value;
            keepBombsStocked = cfgKeepBombs.Value;
            smokeSlider = cfgSmoke.Value;
            stinkSlider = cfgStink.Value;
            amnesiaSlider = cfgAmnesia.Value;
            bucketLiquid = Mathf.Clamp(cfgBucketLiquid.Value, 0, 2);
            cycleLiquid = cfgCycleLiquid.Value;
            matchCrouch = cfgMatchCrouch.Value;
            giggleEnabled = cfgGiggleEnabled.Value;
            bucketAction = Mathf.Clamp(cfgBucketAction.Value, 0, 1);
            pinItems = cfgPinItems.Value;
            allClubPerks = cfgAllClub.Value;
            noPhotoSpeed = cfgNoPhoto.Value;
            fastSpeedUp = cfgFastUp.Value;
            speedUpScale = Mathf.Clamp(cfgUpScale.Value, 2f, 25f);
            maxStats = cfgMaxStats.Value; maxRep = cfgMaxRep.Value; alphaAlways = cfgAlphaAlways.Value;
            bombSel = Mathf.Clamp(cfgBombSel.Value, 0, 2); bombHud = cfgBombHud.Value;
            autoBefriend = cfgAutoBefriend.Value; autoTasks = cfgAutoTasks.Value; autoInfo = cfgAutoInfo.Value; autoLikes = cfgAutoLikes.Value;
            teleKeys = cfgTeleKeys.Value; teleAlt = cfgTeleAlt.Value; infBattery = cfgInfBattery.Value; infBags = cfgInfBags.Value;
            infoAmount = Mathf.Clamp(cfgInfoAmount.Value, 1, 999);
            autoGifts = cfgAutoGifts.Value; instantMatch = cfgInstantMatch.Value; weeklyWatchers = cfgWeeklyWatchers.Value;
            fastLoading = cfgFastLoading.Value;
            fastDismember = cfgFastDismember.Value; fastWash = cfgFastWash.Value; fastShower = cfgFastShower.Value; fastDispose = cfgFastDispose.Value;
            instantWrap = cfgInstantWrap.Value; fastAttack = cfgFastAttack.Value; attackSpeed = Mathf.Clamp(cfgAttackSpeed.Value, 1f, 10f);
            voidGoddess = cfgVoidGoddess.Value; extraBring = cfgExtraBring.Value;
            extraBringIds.Clear();
            foreach (string part in (cfgExtraBringIds.Value ?? "").Split(','))
            {
                int bid;
                if (int.TryParse(part.Trim(), out bid) && bid >= 1 && bid <= 12) extraBringIds.Add(bid);
            }
            if (!string.IsNullOrEmpty(cfgBringNames.Value)) bringNames = cfgBringNames.Value.Split('|');
            spillKeyOn = cfgSpillKeyOn.Value; alwaysYakuza = cfgAlwaysYakuza.Value; rivalSecrets = cfgRivalSecrets.Value;
            cfgInstantDisposal = Config.Bind("Saved", "InstantCorpseDisposal", false, "The incinerator and kiln finish instantly and can be used again right away.");
            instantDisposal = cfgInstantDisposal.Value;
            cfgNemesisVision = Config.Bind("Saved", "NemesisInYandereVision", false, "Mission Mode: outline Nemesis in purple while Yandere vision is on.");
            cfgNoteHelper = Config.Bind("Saved", "LockerNoteHelper", true, "Highlight which locker-note subjects will work for the student.");
            nemesisVision = cfgNemesisVision.Value;
            noteHelper = cfgNoteHelper.Value;
            cfgEasyMinigames = Config.Bind("Saved", "EasyBefriendBetray", false, "Befriend/Betray minigames: the stalker doesn't notice you up close, struggles fill on their own and can't be lost, pickpocketing has no time limit.");
            cfgInstantReject = Config.Bind("Saved", "InstantRejection", false, "One sabotaged rival interaction fills the whole rejection bar (5/5).");
            easyMinigames = cfgEasyMinigames.Value;
            instantReject = cfgInstantReject.Value;
            cfgInstantExpel = Config.Bind("Saved", "InstantExpel", false, "One successful report to the counselor fills the rival's expel bar (5/5).");
            instantExpel = cfgInstantExpel.Value;
            if (instantMatch && (instantReject || instantExpel)) { instantMatch = false; undoMatchPending = true; }
            cfgInstantBreak = Config.Bind("Saved", "InstantMindBreak", false, "Any torture in the basement drops the prisoner's sanity straight to 0 (mind-broken).");
            instantBreak = cfgInstantBreak.Value;
            cfgWalkAsRun = Config.Bind("Saved", "WalkAndDragAtRunSpeed", false, "Walking and dragging bodies use your running speed.");
            walkAsRun = cfgWalkAsRun.Value;
            SceneManager.sceneLoaded += OnSceneLoaded;
            foreach (string part in cfgKept.Value.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = part.Split('=');
                int c;
                if (kv.Length == 2 && int.TryParse(kv[1], out c)) keptItems[kv[0]] = c;
            }

            try { InitHarmony(); }
            catch (Exception e) { Logger.LogWarning("Harmony init failed (club perks / speed-up disabled): " + e); }

            Logger.LogInfo("Yan Sim Mod Menu 0.14.1 loaded. Press " + toggleKey.Value + " in game.");
        }

        void SaveSettings()
        {
            try
            {
                cfgLockSanity.Value = lockSanity;
                cfgLockHealth.Value = lockHealth;
                cfgLockedHealth.Value = lockedHealth;
                cfgSpeedMult.Value = speedMult;
                cfgFreezeClock.Value = freezeClock;
                cfgTimeMult.Value = timeMult;
                cfgGameSpeedOn.Value = gameSpeedOn;
                cfgGameSpeed.Value = gameSpeed;
                cfgInfiniteItems.Value = infiniteItems;
                cfgShowHelper.Value = showHelper;
                cfgKeepBombs.Value = keepBombsStocked;
                cfgSmoke.Value = smokeSlider;
                cfgStink.Value = stinkSlider;
                cfgAmnesia.Value = amnesiaSlider;
                cfgBucketLiquid.Value = bucketLiquid;
                cfgCycleLiquid.Value = cycleLiquid;
                cfgMatchCrouch.Value = matchCrouch;
                cfgGiggleEnabled.Value = giggleEnabled;
                cfgBucketAction.Value = bucketAction;
                cfgPinItems.Value = pinItems;
                cfgAllClub.Value = allClubPerks;
                cfgNoPhoto.Value = noPhotoSpeed;
                cfgFastUp.Value = fastSpeedUp;
                cfgUpScale.Value = speedUpScale;
                cfgMaxStats.Value = maxStats; cfgMaxRep.Value = maxRep; cfgAlphaAlways.Value = alphaAlways;
                cfgBombSel.Value = bombSel; cfgBombHud.Value = bombHud;
                cfgAutoBefriend.Value = autoBefriend; cfgAutoTasks.Value = autoTasks; cfgAutoInfo.Value = autoInfo; cfgAutoLikes.Value = autoLikes;
                cfgTeleKeys.Value = teleKeys; cfgTeleAlt.Value = teleAlt; cfgInfBattery.Value = infBattery; cfgInfBags.Value = infBags;
                cfgInfoAmount.Value = infoAmount;
                cfgAutoGifts.Value = autoGifts; cfgInstantMatch.Value = instantMatch; cfgWeeklyWatchers.Value = weeklyWatchers;
                cfgSpillKeyOn.Value = spillKeyOn; cfgFastLoading.Value = fastLoading;
                cfgFastDismember.Value = fastDismember; cfgFastWash.Value = fastWash; cfgFastShower.Value = fastShower; cfgFastDispose.Value = fastDispose;
                cfgInstantWrap.Value = instantWrap; cfgFastAttack.Value = fastAttack; cfgAttackSpeed.Value = attackSpeed;
                cfgVoidGoddess.Value = voidGoddess; cfgExtraBring.Value = extraBring;
                cfgExtraBringIds.Value = string.Join(",", extraBringIds.OrderBy(x => x).Select(x => x.ToString()).ToArray()); cfgAlwaysYakuza.Value = alwaysYakuza; cfgRivalSecrets.Value = rivalSecrets;
                cfgInstantDisposal.Value = instantDisposal;
                cfgNemesisVision.Value = nemesisVision;
                cfgNoteHelper.Value = noteHelper;
                cfgEasyMinigames.Value = easyMinigames;
                cfgInstantReject.Value = instantReject;
                cfgInstantExpel.Value = instantExpel;
                cfgInstantBreak.Value = instantBreak;
                cfgWalkAsRun.Value = walkAsRun;
                System.Text.StringBuilder kept = new System.Text.StringBuilder();
                foreach (KeyValuePair<string, int> kv in keptItems)
                {
                    if (kept.Length > 0) kept.Append(';');
                    kept.Append(kv.Key).Append('=').Append(kv.Value);
                }
                cfgKept.Value = kept.ToString();
                Config.Save();
            }
            catch (Exception e) { Logger.LogWarning("Could not save settings: " + e.Message); }
        }

        void OnApplicationQuit() { SaveSettings(); }

        // ------------------------------------------------------------------
        //  Frame loop
        // ------------------------------------------------------------------

        void Update()
        {
            bool pressed = false;
            try { pressed = Input.GetKeyDown(toggleKey.Value); }
            catch (Exception e) { Logger.LogWarning("Input error: " + e.Message); }
            if (pressed && lastToggleFrame != Time.frameCount) { lastToggleFrame = Time.frameCount; Toggle(); }

            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f) { refreshTimer = 1f; Refresh(); }

            Safe("Fast loading", ApplyFastLoading);
            Safe("Time keys", HandleTimeKeys);
            if (!open && hotkeyScale > 1.001f) { if (Time.timeScale > 0f) Time.timeScale = hotkeyScale; } // 0 = a game pause screen, leave it
            else if (!open && gameSpeedOn && !(yan != null && yan.SenpaiGazing && fastSpeedUp)) Time.timeScale = gameSpeed;

            // Each part runs on its own, so an error in one (for example in a scene it doesn't
            // expect) can't stop the ones after it, like the instant elimination shortcuts.
            Safe("Effects", ApplyEffects);
            Safe("Bombs", ApplyBombs);
            Safe("Bomb key", HandleBombKey);
            Safe("Bucket key", HandleBucketKey);
            Safe("Kept items", ApplyKeptItems);
            Safe("Bucket giggle", HandleBucketGiggle);
            Safe("v0.6 features", HandleV06);
            Safe("Eliminations and automatic options", HandleV07);
            Safe("Student Info reputation keys", HandleInfoRepKeys);
            Safe("Pouring", UpdatePouring);
            Safe("Instant body bag", ApplyInstantWrap);
            Safe("Bring-item names", CaptureBringNames);
            Safe("Yakuza unlock", KeepYakuzaUnlocked);
        }

        readonly Dictionary<string, float> safeLogged = new Dictionary<string, float>();

        // Runs one part of the frame loop; logs its error at most once every 10 seconds.
        void Safe(string what, Action a)
        {
            try { a(); }
            catch (Exception e)
            {
                float last;
                if (!safeLogged.TryGetValue(what, out last) || Time.unscaledTime - last > 10f)
                {
                    safeLogged[what] = Time.unscaledTime;
                    Logger.LogWarning(what + " failed in scene " + SceneManager.GetActiveScene().name + ": " + e);
                }
            }
        }

        void LateUpdate()
        {
            try { ApplyAttackSpeed(); } catch (Exception e) { Logger.LogWarning("Attack speed: " + e.Message); fastAttack = false; }
            MaintainInfiniteItems();
            try { HandleNemesisVision(); } catch (Exception e) { Logger.LogWarning("Nemesis vision: " + e.Message); nemesisVision = false; }
            try { UpdateNoteHelper(); } catch (Exception e) { Logger.LogWarning("Note helper: " + e.Message); noteHelper = false; }
            if (!open) return;
            // Keep the cursor free and the game from reading the mouse for movement/camera.
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            if (yan != null) yan.CanMove = false;
        }

        void Toggle()
        {
            open = !open;
            Logger.LogInfo("Menu " + (open ? "opened" : "closed") + " in scene " + SceneManager.GetActiveScene().name + ".");
            if (open)
            {
                try { Refresh(); } catch (Exception e) { Logger.LogWarning("Refresh on open: " + e); }
                savedTimeScale = Time.timeScale;
                if (pauseWhileOpen.Value) Time.timeScale = 0f;

                try
                {
                    cam = FindObjectOfType<RPG_Camera>();
                    if (cam != null) { camWasEnabled = cam.enabled; cam.enabled = false; }
                    if (yan != null) { canMoveWas = yan.CanMove; yan.CanMove = false; }
                }
                catch (Exception e) { Logger.LogWarning("Camera/player on open: " + e.Message); }
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                Time.timeScale = gameSpeedOn ? gameSpeed : savedTimeScale;
                if (cam != null) cam.enabled = camWasEnabled;
                if (yan != null) yan.CanMove = canMoveWas;
                SaveSettings();
            }
        }

        void Refresh()
        {
            if (yan == null) yan = FindObjectOfType<YandereScript>();
            if (topicUI == null) topicUI = FindObjectOfType<TopicInterfaceScript>(true);
            if (socialUI == null) socialUI = FindObjectOfType<SocialScript>(true);
            if (manager == null) manager = FindObjectOfType<StudentManagerScript>();
            if (manager != null)
            {
                if (rep == null) rep = manager.Reputation;
                if (clock == null) clock = manager.Clock;
            }
            if (rep == null) rep = FindObjectOfType<ReputationScript>();
            if (clock == null) clock = FindObjectOfType<ClockScript>();
            if (weaponManager == null) weaponManager = yan != null ? yan.WeaponManager : null;
            if (weaponManager == null) weaponManager = FindObjectOfType<WeaponManagerScript>();
        }

        void ApplyEffects()
        {
            if (yan != null)
            {
                if (speedOwner != yan)
                {
                    speedOwner = yan;
                    baseWalk = yan.WalkSpeed;
                    baseRun = yan.RunSpeed;
                    baseCrouchWalk = yan.CrouchWalkSpeed;
                    baseCrouchRun = yan.CrouchRunSpeed;
                    baseCrawl = yan.CrawlSpeed;
                    speedActive = false;
                }

                if (Mathf.Abs(speedMult - 1f) > 0.001f)
                {
                    yan.WalkSpeed = baseWalk * speedMult;
                    yan.RunSpeed = baseRun * speedMult;
                    // With matching on, crouching and crawling go as fast as walking/running.
                    yan.CrouchWalkSpeed = (matchCrouch ? baseWalk : baseCrouchWalk) * speedMult;
                    yan.CrouchRunSpeed = (matchCrouch ? baseRun : baseCrouchRun) * speedMult;
                    yan.CrawlSpeed = (matchCrouch ? baseWalk : baseCrawl) * speedMult;
                    speedActive = true;
                }
                else if (speedActive)
                {
                    yan.WalkSpeed = baseWalk;
                    yan.RunSpeed = baseRun;
                    yan.CrouchWalkSpeed = baseCrouchWalk;
                    yan.CrouchRunSpeed = baseCrouchRun;
                    yan.CrawlSpeed = baseCrawl;
                    speedActive = false;
                }

                // Everything at running speed. The game moves you at:
                //   run:        RunSpeed + ClubSpeedBonus + PantySpeedBonus + PhysicalGrade * 0.2
                //   walk/drag:  WalkSpeed
                //   crouch:     CrouchWalkSpeed
                //   crouch-run: CrouchRunSpeed + (PhysicalGrade + SpeedBonus) * 0.25
                //   crawl:      CrawlSpeed
                // so each field is set so the final speed equals the running speed.
                if (walkAsRun)
                {
                    float run = yan.RunSpeed + yan.ClubSpeedBonus + yan.PantySpeedBonus + yan.PhysicalGrade * 0.2f;
                    yan.WalkSpeed = run;
                    yan.CrouchWalkSpeed = run;
                    yan.CrouchRunSpeed = run - (yan.PhysicalGrade + yan.SpeedBonus) * 0.25f;
                    yan.CrawlSpeed = run;
                    walkOverridden = true;
                }
                else if (walkOverridden)
                {
                    // With the speed slider in use, its block above already rewrites these every frame.
                    if (Mathf.Abs(speedMult - 1f) <= 0.001f)
                    {
                        yan.WalkSpeed = baseWalk;
                        yan.CrouchWalkSpeed = baseCrouchWalk;
                        yan.CrouchRunSpeed = baseCrouchRun;
                        yan.CrawlSpeed = baseCrawl;
                    }
                    walkOverridden = false;
                }

                // Only in a real school day: other scenes (Custom Mode editor, portraits) have a YandereScript that throws here.
                if (lockSanity && manager != null && yan.isActiveAndEnabled) { try { yan.Sanity = 100f; } catch (Exception) { } }
                if (lockHealth)
                {
                    if (lockedHealth <= 0) lockedHealth = yan.Health;
                    yan.Health = lockedHealth;
                }
            }

            if (clock != null)
            {
                if (timeOwner != clock)
                {
                    timeOwner = clock;
                    baseTimeSpeed = clock.TimeSpeed;
                    clockFrozenByUs = false;
                    timeActive = false;
                }

                if (freezeClock) { clock.StopTime = true; clockFrozenByUs = true; }
                else if (clockFrozenByUs) { clock.StopTime = false; clockFrozenByUs = false; }

                if (Mathf.Abs(timeMult - 1f) > 0.001f)
                {
                    clock.TimeSpeed = baseTimeSpeed * timeMult;
                    timeActive = true;
                }
                else if (timeActive)
                {
                    clock.TimeSpeed = baseTimeSpeed;
                    timeActive = false;
                }
            }
        }

        // Each bomb type: when the game uses the one in your inventory (flag goes false),
        // spend one from the count and hand you the next.
        void ApplyBombs()
        {
            if (yan == null || yan.Inventory == null) return;
            InventoryScript inv = yan.Inventory;

            if (bombOwner != inv)
            {
                bombOwner = inv;
                if (keepBombsStocked) { ApplyBomb(0); ApplyBomb(1); ApplyBomb(2); }
            }

            if (smokeManaged && !inv.SmokeBomb)
            {
                if (smokeCount > 0) smokeCount--;
                if (smokeCount > 0) inv.SmokeBomb = true; else smokeManaged = false;
            }
            if (stinkManaged && !inv.StinkBomb)
            {
                if (stinkCount > 0) stinkCount--;
                if (stinkCount > 0) inv.StinkBomb = true; else stinkManaged = false;
            }
            if (amnesiaManaged && !inv.AmnesiaBomb)
            {
                if (amnesiaCount > 0) amnesiaCount--;
                if (amnesiaCount > 0) inv.AmnesiaBomb = true; else amnesiaManaged = false;
            }
        }

        // ------------------------------------------------------------------
        //  GUI
        // ------------------------------------------------------------------

        void OnGUI()
        {
            // Backup way to catch the menu key, in case a scene stops Input.GetKeyDown from seeing it.
            Event ev = Event.current;
            if (rebinding < 0 && ev != null && ev.type == EventType.KeyDown && toggleKey != null &&
                ev.keyCode == toggleKey.Value && lastToggleFrame != Time.frameCount)
            {
                lastToggleFrame = Time.frameCount;
                Toggle();
                ev.Use();
            }

            try
            {
                if (showHelper) { DrawTopicHelper(); DrawComplimentHighlight(); }
                DrawHud();
                if (noteActive) DrawNoteHelper();
            }
            catch (Exception e) { if (!guiErrorLogged) { guiErrorLogged = true; Logger.LogWarning("Overlay error: " + e); } }
            if (!open) return;
            HandleRebind();
            if (selStyle == null)
            {
                selStyle = new GUIStyle(GUI.skin.button);
                selStyle.fontStyle = FontStyle.Bold;
            }
            GUI.depth = -1000;
            window = GUILayout.Window(0x59534D, window, DrawWindow,
                "Yan Sim Mod Menu  (" + toggleKey.Value + " to close)");

            // Draw our own cursor last so it sits on top of everything.
            if (Event.current.type == EventType.Repaint)
            {
                if (cursorTex == null) BuildCursorTexture();
                Vector2 mp = Event.current.mousePosition;
                GUI.DrawTexture(new Rect(mp.x, mp.y, CursorW * CursorScale, CursorH * CursorScale), cursorTex);
            }
        }

        void DrawWindow(int id)
        {
            bool np = GUILayout.Toggle(pauseWhileOpen.Value, "Pause the game while this menu is open");
            if (np != pauseWhileOpen.Value)
            {
                pauseWhileOpen.Value = np;
                Time.timeScale = np ? 0f : (gameSpeedOn ? gameSpeed : savedTimeScale);
            }

            tab = GUILayout.Toolbar(tab, tabs);
            scroll = GUILayout.BeginScrollView(scroll);

            try
            {
                switch (tab)
                {
                    case 0: DrawPlayerTab(); break;
                    case 1: DrawItemsTab(); break;
                    case 2: DrawWeaponsTab(); break;
                    case 3: DrawStudentsTab(); break;
                    case 4: DrawDebugTab(); break;
                    case 5: DrawScenesTab(); break;
                    case 6: DrawExtrasTab(); break;
                }
            }
            catch (Exception e)
            {
                if (e is ExitGUIException) throw;
                if (!guiErrorLogged) { guiErrorLogged = true; Logger.LogWarning("Tab " + tabs[tab] + " error in scene " + SceneManager.GetActiveScene().name + ": " + e); }
                GUILayout.Label("This tab can't be used in this scene (" + e.GetType().Name + "). Try another tab.");
            }

            GUILayout.Space(6);
            if (status.Length > 0) GUILayout.Label(status);

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        int IntSlider(string label, int value, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ": " + value, GUILayout.Width(190));
            float f = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.EndHorizontal();
            return Mathf.Clamp(Mathf.RoundToInt(f), min, max);
        }

        // ---------------- Player tab ----------------

        void DrawPlayerTab()
        {
            GUILayout.Label("=== PLAYER ===");
            if (yan == null)
            {
                GUILayout.Label("Player not found (open this inside the school scene).");
            }
            else
            {
                lockSanity = GUILayout.Toggle(lockSanity, "Lock sanity at 100%");

                bool newHealth = GUILayout.Toggle(lockHealth, "Lock health at current value (" + yan.Health + ")");
                if (newHealth && !lockHealth) lockedHealth = yan.Health;
                lockHealth = newHealth;

                GUILayout.Label("Move speed x" + speedMult.ToString("0.0"));
                speedMult = GUILayout.HorizontalSlider(speedMult, 0.5f, 4f);
                if (GUILayout.Button("Reset speed")) speedMult = 1f;
                matchCrouch = GUILayout.Toggle(matchCrouch, "Crouch and crawl speed match walk/run speed");
                walkAsRun = GUILayout.Toggle(walkAsRun, "Walk, drag bodies, crouch and crawl at running speed");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Giggle now")) GiggleNow();
                if (GUILayout.Button("Bucket trip now (hold a bucket)")) TripNow();
                GUILayout.EndHorizontal();

                GUILayout.Space(8);
                GUILayout.Label("=== STATS (same as the phone's Stats screen) ===");
                DrawStats();
            }

            GUILayout.Space(8);
            GUILayout.Label("=== MONEY & REPUTATION ===");
            GUILayout.Label("Money: " + PlayerGlobals.Money.ToString("0.00"));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+100")) AddMoney(100f);
            if (GUILayout.Button("+1000")) AddMoney(1000f);
            if (GUILayout.Button("Set 0")) { PlayerGlobals.Money = 0f; if (yan != null && yan.Inventory != null) yan.Inventory.Money = 0f; }
            GUILayout.EndHorizontal();

            if (rep == null)
            {
                GUILayout.Label("Reputation not found.");
            }
            else
            {
                GUILayout.Label("Reputation: " + rep.Reputation.ToString("0"));
                maxRep = GUILayout.Toggle(maxRep, "Keep reputation maxed (100, losses are cancelled)");
                if (!maxRep)
                {
                    float nv = GUILayout.HorizontalSlider(rep.Reputation, -100f, 100f);
                    if (!Mathf.Approximately(nv, rep.Reputation)) { rep.Reputation = nv; PlayerGlobals.Reputation = nv; }
                }
            }
        }

        void AddMoney(float amount)
        {
            PlayerGlobals.Money += amount;
            if (yan != null && yan.Inventory != null) yan.Inventory.Money += amount;
        }

        void DrawStats()
        {
            ClassScript c = yan.Class;
            if (c == null) { GUILayout.Label("Stats not available."); return; }
            int v;
            maxStats = GUILayout.Toggle(maxStats, "Keep all stats maxed (always 5, no matter what)");
            if (maxStats)
            {
                GUILayout.Label("Biology, Chemistry, Language, Physical, Psychology, Seduction, Numbness, Enlightenment: 5");
                v = IntSlider("Friends", yan.Friends, 0, 100);
                if (v != yan.Friends) { yan.Friends = v; PlayerGlobals.Friends = v; }
                v = IntSlider("Alerts", yan.Alerts, 0, 99);
                if (v != yan.Alerts) { yan.Alerts = v; PlayerGlobals.Alerts = v; }
                return;
            }

            v = IntSlider("Biology", c.BiologyGrade, 0, 5);
            if (v != c.BiologyGrade) { c.BiologyGrade = v; ClassGlobals.BiologyGrade = v; }
            v = IntSlider("Chemistry", c.ChemistryGrade, 0, 5);
            if (v != c.ChemistryGrade) { c.ChemistryGrade = v; ClassGlobals.ChemistryGrade = v; }
            v = IntSlider("Language", c.LanguageGrade, 0, 5);
            if (v != c.LanguageGrade) { c.LanguageGrade = v; ClassGlobals.LanguageGrade = v; }
            v = IntSlider("Physical", c.PhysicalGrade, 0, 5);
            if (v != c.PhysicalGrade) { c.PhysicalGrade = v; ClassGlobals.PhysicalGrade = v; yan.PhysicalGrade = v; }
            v = IntSlider("Psychology", c.PsychologyGrade, 0, 5);
            if (v != c.PsychologyGrade) { c.PsychologyGrade = v; ClassGlobals.PsychologyGrade = v; }

            v = IntSlider("Seduction", c.Seduction, 0, 5);
            if (v != c.Seduction) { c.Seduction = v; PlayerGlobals.Seduction = v; }
            v = IntSlider("Numbness", c.Numbness, 0, 5);
            if (v != c.Numbness) { c.Numbness = v; PlayerGlobals.Numbness = v; }
            v = IntSlider("Enlightenment", c.Enlightenment, 0, 5);
            if (v != c.Enlightenment) { c.Enlightenment = v; PlayerGlobals.Enlightenment = v; }

            v = IntSlider("Friends", yan.Friends, 0, 100);
            if (v != yan.Friends) { yan.Friends = v; PlayerGlobals.Friends = v; }
            v = IntSlider("Alerts", yan.Alerts, 0, 99);
            if (v != yan.Alerts) { yan.Alerts = v; PlayerGlobals.Alerts = v; }

            GUILayout.Label("Some skill effects (e.g. speed, numbness) fully apply after reloading the day.");
        }

        // ---------------- Items tab ----------------

        void BuildInventoryFields()
        {
            List<FieldInfo> list = new List<FieldInfo>();
            foreach (FieldInfo fi in typeof(InventoryScript).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                string n = fi.Name;
                if (n.StartsWith("ArrivedWith") || n == "ExplosiveDeviceSet" || n == "BroughtCarrotsToSchool") continue;
                if (n == "SmokeBomb" || n == "StinkBomb" || n == "AmnesiaBomb") continue; // handled below
                if (fi.FieldType == typeof(bool) || (fi.FieldType == typeof(int) && IntItems.Contains(n)))
                    list.Add(fi);
            }
            invFields = list.ToArray();

            List<FieldInfo> all = new List<FieldInfo>(list);
            foreach (string bn in new string[] { "SmokeBomb", "StinkBomb", "AmnesiaBomb" })
            {
                FieldInfo bf = typeof(InventoryScript).GetField(bn, BindingFlags.Public | BindingFlags.Instance);
                if (bf != null) all.Add(bf);
            }
            trackFields = all.ToArray();
        }

        static string Pretty(string name)
        {
            return Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ");
        }

        void DrawItemsTab()
        {
            GUILayout.Label("=== INVENTORY ===");
            if (yan == null || yan.Inventory == null)
            {
                GUILayout.Label("Inventory not found (open this inside the school scene).");
                return;
            }
            InventoryScript inv = yan.Inventory;
            if (invFields == null) BuildInventoryFields();

            infiniteItems = GUILayout.Toggle(infiniteItems, "Don't lose items when I use them (infinite items)");
            pinItems = GUILayout.Toggle(pinItems, "Keep the items I check (re-given every time the school loads)");
            if (GUILayout.Button("Forget pinned items (" + keptItems.Count + ")")) { keptItems.Clear(); status = "Pinned items cleared."; }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Give everything"))
            {
                foreach (FieldInfo fi in invFields)
                {
                    if (fi.FieldType == typeof(bool)) SetInv(fi, inv, true);
                    else SetInv(fi, inv, 5);
                }
                status = "Gave every item (counts set to 5).";
            }
            if (GUILayout.Button("Remove everything"))
            {
                foreach (FieldInfo fi in invFields)
                {
                    if (fi.FieldType == typeof(bool)) SetInv(fi, inv, false);
                    else SetInv(fi, inv, 0);
                }
                status = "Cleared the inventory.";
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter:", GUILayout.Width(45));
            invFilter = GUILayout.TextField(invFilter);
            GUILayout.EndHorizontal();

            foreach (FieldInfo fi in invFields)
            {
                string label = Pretty(fi.Name);
                if (invFilter.Length > 0 && label.IndexOf(invFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                if (fi.FieldType == typeof(bool))
                {
                    bool cur = (bool)fi.GetValue(inv);
                    bool nv = GUILayout.Toggle(cur, label);
                    if (nv != cur) SetInv(fi, inv, nv);
                }
                else
                {
                    int cur = (int)fi.GetValue(inv);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(label + ": " + cur, GUILayout.Width(220));
                    if (GUILayout.Button("-", GUILayout.Width(40))) SetInv(fi, inv, Mathf.Max(0, cur - 1));
                    if (GUILayout.Button("+", GUILayout.Width(40))) SetInv(fi, inv, cur + 1);
                    if (GUILayout.Button("+5", GUILayout.Width(40))) SetInv(fi, inv, cur + 5);
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("=== BOMBS & ALPHABET TOOLS ===");
            bool na = GUILayout.Toggle(alphaAlways, "Always enable the Alphabet Challenge tools (any game mode)");
            if (na != alphaAlways) { alphaAlways = na; alphaScene = null; }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Throw key uses:", GUILayout.Width(110));
            bombSel = GUILayout.Toolbar(bombSel, BombNames);
            GUILayout.EndHorizontal();
            bombHud = GUILayout.Toggle(bombHud, "Show the selected bomb in the top-right corner");

            DrawBombRow("Smoke bombs", ref smokeSlider, ref smokeCount, ref smokeManaged, 0);
            DrawBombRow("Stink bombs", ref stinkSlider, ref stinkCount, ref stinkManaged, 1);
            DrawBombRow("Amnesia bombs", ref amnesiaSlider, ref amnesiaCount, ref amnesiaManaged, 2);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Throw smoke")) ThrowBombs(true, false, false, false);
            if (GUILayout.Button("Throw stink")) ThrowBombs(false, true, false, false);
            if (GUILayout.Button("Throw amnesia")) ThrowBombs(false, false, true, false);
            GUILayout.EndHorizontal();
            keepBombsStocked = GUILayout.Toggle(keepBombsStocked, "Restock these bomb counts every time the school loads");
            GUILayout.Label("In normal play, press " + bombCycleKey.Value + " to pick smoke / stink / amnesia and " + throwKey.Value + " to throw it. A bomb is re-given after each throw until its count runs out.");

            GUILayout.Space(10);
            GUILayout.Label("=== INFINITE-USE OBJECTS ===");
            infBattery = GUILayout.Toggle(infBattery, "Infinite car battery (works again a few seconds after each electrocution)");
            infBags = GUILayout.Toggle(infBags, "Unlimited body bag box (never runs out of bags)");

            GUILayout.Space(10);
            GUILayout.Label("=== BRING EXTRA FROM HOME ===");
            extraBring = GUILayout.Toggle(extraBring, "Every school day, also bring everything ticked below");
            GUILayout.Label("This is on top of what you pick in the game's own bring-item menu at home. Names come from that menu once you've opened it.");
            GUILayout.Label("Weapons:");
            GUILayout.BeginHorizontal();
            for (int id = 1; id <= 3; id++) BringToggle(id);
            GUILayout.EndHorizontal();
            GUILayout.Label("Items:");
            for (int id = 4; id <= 12; id++)
            {
                if ((id - 4) % 3 == 0) GUILayout.BeginHorizontal();
                BringToggle(id);
                if ((id - 4) % 3 == 2 || id == 12) GUILayout.EndHorizontal();
            }
        }

        void BringToggle(int id)
        {
            bool on = extraBringIds.Contains(id);
            bool now = GUILayout.Toggle(on, BringName(id), GUILayout.Width(190));
            if (now && !on) extraBringIds.Add(id);
            if (!now && on) extraBringIds.Remove(id);
        }

        void DrawBombRow(string label, ref float slider, ref int count, ref bool managed, int kind)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ": " + Mathf.RoundToInt(slider) + (managed ? "  (left: " + count + ")" : ""), GUILayout.Width(250));
            slider = GUILayout.HorizontalSlider(slider, 0f, 99f);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Apply " + label.ToLower()))
            {
                ApplyBomb(kind);
                status = label + " set to " + Mathf.RoundToInt(kind == 0 ? smokeSlider : (kind == 1 ? stinkSlider : amnesiaSlider)) + ".";
            }
        }

        void ApplyBomb(int kind)
        {
            InventoryScript inv = yan != null ? yan.Inventory : null;
            if (inv == null) { status = "Inventory not found."; return; }
            float sl = kind == 0 ? smokeSlider : (kind == 1 ? stinkSlider : amnesiaSlider);
            int n = Mathf.RoundToInt(sl);
            bool has = n > 0;
            if (kind == 0) { smokeCount = n; smokeManaged = has; inv.SmokeBomb = has; }
            else if (kind == 1) { stinkCount = n; stinkManaged = has; inv.StinkBomb = has; }
            else { amnesiaCount = n; amnesiaManaged = has; inv.AmnesiaBomb = has; }
            invDirty = true;
        }

        // ---------------- Infinite items ----------------

        void SetInv(FieldInfo fi, InventoryScript inv, object v)
        {
            fi.SetValue(inv, v);
            invDirty = true;
            if (v is bool && (bool)v && (fi.Name == "MaskingTape" || fi.Name == "TapePlayer" || fi.Name == "AnswerSheet"))
                syncAt = Time.unscaledTime + 0.2f;
            if (v is bool)
            {
                if ((bool)v) keptItems[fi.Name] = 1; else keptItems.Remove(fi.Name);
            }
            else if (v is int)
            {
                int c = (int)v;
                if (c > 0) keptItems[fi.Name] = c; else keptItems.Remove(fi.Name);
            }
        }

        // Items you checked in the menu are re-given each time a new school scene loads.
        void ApplyKeptItems()
        {
            if (!pinItems || yan == null || yan.Inventory == null) return;
            InventoryScript inv = yan.Inventory;
            if (keptOwner == inv) return;
            keptOwner = inv;
            if (keptItems.Count == 0) return;
            foreach (KeyValuePair<string, int> kv in keptItems)
            {
                FieldInfo f = typeof(InventoryScript).GetField(kv.Key, BindingFlags.Public | BindingFlags.Instance);
                if (f == null) continue;
                if (f.FieldType == typeof(bool)) f.SetValue(inv, true);
                else if (f.FieldType == typeof(int))
                {
                    int cur = (int)f.GetValue(inv);
                    if (cur < kv.Value) f.SetValue(inv, kv.Value);
                }
            }
            invDirty = true;
            syncAt = Time.unscaledTime + 2f;
        }

        // Each frame: if an item flag went from true to false (or a count dropped) since the
        // last frame and it wasn't us who changed it, put it back.
        void MaintainInfiniteItems()
        {
            if (!infiniteItems || yan == null || yan.Inventory == null) { invSnapshot = null; return; }
            InventoryScript inv = yan.Inventory;
            if (trackFields == null) BuildInventoryFields();
            if (trackFields == null) return;

            bool fresh = invSnapshot == null || snapOwner != inv || invSnapshot.Length != trackFields.Length || invDirty;
            if (!fresh)
            {
                for (int i = 0; i < trackFields.Length; i++)
                {
                    FieldInfo f = trackFields[i];
                    object now = f.GetValue(inv);
                    if (f.FieldType == typeof(bool))
                    {
                        if ((bool)invSnapshot[i] && !(bool)now) f.SetValue(inv, true);
                    }
                    else if ((int)now < (int)invSnapshot[i])
                    {
                        f.SetValue(inv, invSnapshot[i]);
                    }
                }
            }

            if (invSnapshot == null || invSnapshot.Length != trackFields.Length) invSnapshot = new object[trackFields.Length];
            for (int i = 0; i < trackFields.Length; i++) invSnapshot[i] = trackFields[i].GetValue(inv);
            snapOwner = inv;
            invDirty = false;
        }

        // ---------------- Buckets ----------------

        void HandleBucketKey()
        {
            if (open || yan == null || !yan.CanMove) return;
            bool pressed = false;
            try { pressed = Input.GetKeyDown(bucketKey.Value); }
            catch (Exception) { }
            if (pressed) FillBucketNow();
        }

        BucketScript FindTargetBucket()
        {
            if (yan == null) return null;
            if (yan.PickUp != null && yan.PickUp.Bucket != null) return yan.PickUp.Bucket;
            if (manager == null || manager.AllBuckets == null) return null;
            BucketScript best = null;
            float bestDist = 25f; // 5 m, squared
            foreach (BucketScript b in manager.AllBuckets)
            {
                if (b == null) continue;
                float d = (b.transform.position - yan.transform.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = b; }
            }
            return best;
        }

        void FillOne(BucketScript b, int liquid)
        {
            b.Fill();
            b.Spilling = false;
            b.DyedBrown = false;
            b.Bleached = false;
            b.Gasoline = (liquid == 2);
            b.Bloodiness = (liquid == 1) ? 100f : 0f;
            b.UpdateAppearance = true;
        }

        void FillBucketNow()
        {
            BucketScript b = FindTargetBucket();
            if (b == null) { status = "No bucket held or within 5 m."; return; }
            int liquid = bucketLiquid;
            FillOne(b, liquid);
            status = "Filled the bucket with " + LiquidNames[liquid].ToLower() + ".";
            if (cycleLiquid) bucketLiquid = (bucketLiquid + 1) % 3;
        }

        void FillAllBuckets()
        {
            if (manager == null || manager.AllBuckets == null) { status = "No buckets found."; return; }
            int n = 0;
            foreach (BucketScript b in manager.AllBuckets)
            {
                if (b == null) continue;
                FillOne(b, bucketLiquid);
                n++;
            }
            status = "Filled " + n + " buckets with " + LiquidNames[bucketLiquid].ToLower() + ".";
        }

        void DrawBucketSection()
        {
            GUILayout.Label("--- Buckets ---");
            bucketLiquid = GUILayout.Toolbar(bucketLiquid, LiquidNames);
            cycleLiquid = GUILayout.Toggle(cycleLiquid, "Cycle water > blood > gasoline each time I press " + bucketKey.Value);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fill held/nearest bucket")) FillBucketNow();
            if (GUILayout.Button("Fill ALL buckets")) FillAllBuckets();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Bring all buckets to me"))
            {
                if (manager != null && manager.AllBuckets != null)
                {
                    int n = 0;
                    foreach (BucketScript b in manager.AllBuckets)
                    {
                        if (b == null) continue;
                        BringObject(b.gameObject, FrontOfPlayer(1.5f, 0.5f) + yan.transform.right * (0.5f * n));
                        n++;
                    }
                    status = "Moved " + n + " buckets to you.";
                }
                else status = "No buckets found.";
            }
            GUILayout.Label("In game, press " + bucketKey.Value + " to fill the bucket you hold (or the nearest within 5 m) with the chosen liquid.");
        }

        // ---------------- Traps ----------------

        void AllowTrapsOnAllDoors()
        {
            FieldInfo f = typeof(DoorScript).GetField("NoTrap", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null) { status = "Door trap setting not found in this build."; return; }
            int n = 0;
            foreach (DoorScript d in FindObjectsOfType<DoorScript>())
            {
                if (d == null) continue;
                if ((bool)f.GetValue(d)) { f.SetValue(d, false); n++; }
            }
            status = "Allowed bucket traps on " + n + " more doors.";
        }

        // ---------------- Likes / dislikes ----------------

        void LearnAllOpinions()
        {
            if (manager == null || manager.Students == null) { status = "Student manager not found."; return; }
            int n = 0;
            for (int id = 1; id < manager.Students.Length; id++)
            {
                if (manager.Students[id] == null) continue;
                for (int t = 1; t <= 25; t++) manager.SetTopicLearnedByStudent(t, id, true);
                n++;
            }
            for (int t = 1; t <= 25; t++) ConversationGlobals.SetTopicDiscovered(t, true);
            status = "Learned the likes and dislikes of " + n + " students.";
        }

        // Overlay shown while the topic picker is open: which topics this student loves/hates.
        void DrawTopicHelper()
        {
            try
            {
                if (topicUI == null || !topicUI.gameObject.activeInHierarchy || topicUI.JSON == null) return;
                TopicJson[] all = topicUI.JSON.Topics;
                int sid = topicUI.StudentID;
                if (all == null || sid < 0 || sid >= all.Length || all[sid] == null || all[sid].Topics == null) return;
                int[] ops = all[sid].Topics;
                string[] names = topicUI.TopicNames;
                if (names == null) return;

                System.Text.StringBuilder likes = new System.Text.StringBuilder();
                System.Text.StringBuilder hates = new System.Text.StringBuilder();
                for (int t = 1; t <= 25; t++)
                {
                    int oi = ops.Length >= 26 ? t : t - 1;
                    int ni = names.Length >= 26 ? t : t - 1;
                    if (oi < 0 || oi >= ops.Length || ni < 0 || ni >= names.Length) continue;
                    if (ops[oi] == 2) { if (likes.Length > 0) likes.Append(", "); likes.Append(names[ni]); }
                    else if (ops[oi] == 1) { if (hates.Length > 0) hates.Append(", "); hates.Append(names[ni]); }
                }

                string who = topicUI.Student != null ? topicUI.Student.Name + " (" + topicUI.Student.Persona + ")" : "Student " + sid;
                string pick = "";
                int cur = topicUI.TopicSelected;
                int ci = ops.Length >= 26 ? cur : cur - 1;
                int cn = names.Length >= 26 ? cur : cur - 1;
                if (ci >= 0 && ci < ops.Length && cn >= 0 && cn < names.Length)
                {
                    string tn = names[cn];
                    if (ops[ci] == 2) pick = "Highlighted: " + tn + "  =>  they LOVE it. Pick the \"loves\" option.";
                    else if (ops[ci] == 1) pick = "Highlighted: " + tn + "  =>  they HATE it. Pick the \"hates\" option.";
                    else pick = "Highlighted: " + tn + "  =>  they feel neutral. Pick another topic.";
                }

                GUI.depth = -900;
                Rect r = new Rect(Screen.width - 400, 90, 390, 190);
                GUI.Box(r, "Topic helper: " + who);
                GUILayout.BeginArea(new Rect(r.x + 8, r.y + 24, r.width - 16, r.height - 28));
                GUILayout.Label("LOVES (say you love it): " + (likes.Length > 0 ? likes.ToString() : "none"));
                GUILayout.Label("HATES (say you hate it): " + (hates.Length > 0 ? hates.ToString() : "none"));
                if (pick.Length > 0) GUILayout.Label(pick);
                GUILayout.EndArea();
            }
            catch (Exception) { }
        }


        // ---------------- Club label ----------------

        static string ClubLabel(ClubType c)
        {
            if (c == ClubType.None) return "No club";
            return Regex.Replace(c.ToString(), "(?<=[a-z])(?=[A-Z])", " ");
        }

        // ---------------- Compliment highlight ----------------
        // The compliment grid is laid out in the same order as the persona list, so the
        // compliment whose number equals the student's persona number is the matching one.

        Rect? ScreenRectOf(Component c)
        {
            Camera best = null;
            float bestDepth = float.MinValue;
            int bit = 1 << c.gameObject.layer;
            foreach (Camera cm in Camera.allCameras)
            {
                if ((cm.cullingMask & bit) != 0 && cm.depth >= bestDepth) { bestDepth = cm.depth; best = cm; }
            }
            if (best == null) return null;

            PropertyInfo wc = c.GetType().GetProperty("worldCorners");
            if (wc != null)
            {
                Vector3[] corners = wc.GetValue(c, null) as Vector3[];
                if (corners != null && corners.Length >= 3)
                {
                    Vector3 a = best.WorldToScreenPoint(corners[0]);
                    Vector3 b = best.WorldToScreenPoint(corners[2]);
                    float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x);
                    float y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);
                    return new Rect(x0, Screen.height - y1, x1 - x0, y1 - y0);
                }
            }
            Vector3 sp = best.WorldToScreenPoint(c.transform.position);
            float k = Screen.height / 1080f;
            float w = 330f * k, h = 90f * k;
            return new Rect(sp.x - w / 2f, Screen.height - sp.y - h / 2f, w, h);
        }

        void DrawComplimentHighlight()
        {
            try
            {
                if (socialUI == null || socialUI.ComplimentSet == null || !socialUI.ComplimentSet.activeInHierarchy || socialUI.Student == null) return;
                int p = (int)socialUI.Student.Persona;
                if (p < 1 || p > 18) return;

                Array btns = typeof(SocialScript).GetField("ComplimentButton").GetValue(socialUI) as Array;
                if (btns == null) return;
                int idx = btns.Length >= 19 ? p : p - 1;
                if (idx < 0 || idx >= btns.Length) return;
                Component target = btns.GetValue(idx) as Component;
                if (target == null) return;
                Rect? rr = ScreenRectOf(target);
                if (!rr.HasValue) return;
                Rect r = rr.Value;

                GUI.depth = -950;
                float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f);
                Color old = GUI.color;
                GUI.color = new Color(0.1f, 1f, 0.25f, pulse);
                Texture2D white = Texture2D.whiteTexture;
                float t = 5f;
                GUI.DrawTexture(new Rect(r.x - t, r.y - t, r.width + 2 * t, t), white);
                GUI.DrawTexture(new Rect(r.x - t, r.yMax, r.width + 2 * t, t), white);
                GUI.DrawTexture(new Rect(r.x - t, r.y, t, r.height), white);
                GUI.DrawTexture(new Rect(r.xMax, r.y, t, r.height), white);
                GUI.color = old;

                string persona = Regex.Replace(socialUI.Student.Persona.ToString(), "(?<=[a-z])(?=[A-Z])", " ");
                GUI.Box(new Rect(r.xMax - 270, r.y - 26, 270, 22), "Best for " + persona + ": You're " + ComplimentNames[p - 1] + "!");
            }
            catch (Exception) { }
        }


        // ---------------- Bucket trip (June 2016 mechanic, rebuilt) ----------------
        // Same steps as the old BucketScript/YandereScript code: drop the bucket, play
        // f02_bucketTrip_00, set Yandere.Tripping (students splashed now say "Be more careful!"
        // and you lose reputation, which the game still handles), slide forward for the first
        // half second, thud at 0.33s, then stand up. Mash E / A to get up faster.

        const string TripClip = "f02_bucketTrip_00";

        string FindTripClip(Animation a)
        {
            if (a == null) return null;
            if (a[TripClip] != null) return TripClip;
            string fallback = null;
            foreach (AnimationState st in a)
            {
                string n = st.name.ToLower();
                if (n.Contains("trip") && !n.Contains("wire") && !n.Contains("strip")) return st.name;
                if (fallback == null && (n.Contains("stumble") || n.Contains("shoved") || n.Contains("fall"))) fallback = st.name;
            }
            return fallback;
        }

        bool ReadA()
        {
            try { return Input.GetButtonDown(InputNames.Xbox_A); } catch (Exception) { return false; }
        }

        void TripNow()
        {
            if (yan == null) { status = "Player not found."; return; }
            if (tripRunning || giggleRunning) return;
            if (yan.PickUp == null || yan.PickUp.Bucket == null) { status = "Hold a bucket first."; return; }
            StartCoroutine(TripRoutine());
        }

        IEnumerator TripRoutine()
        {
            tripRunning = true;
            BucketScript b = yan.PickUp.Bucket;
            Animation a = yan.CharacterAnimation;
            string clip = FindTripClip(a);
            if (clip == null)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                if (a != null) foreach (AnimationState st0 in a) sb.Append(st0.name).Append(", ");
                Logger.LogWarning("Bucket trip: no trip animation on Yandere-chan. Clips: " + sb);
                status = "Trip animation not found (see LogOutput.log). Using giggle instead.";
                tripRunning = false;
                GiggleNow();
                yield break;
            }
            if (clip != TripClip) Logger.LogInfo("Bucket trip: f02_bucketTrip_00 missing, using " + clip);

            bool wasFull = b != null && b.Full;
            Logger.LogInfo("Bucket trip: clip " + clip + ", bucket full = " + wasFull);
            yan.EmptyHands();
            if (b != null && wasFull)
            {
                // COMMAND 1: Full = false; Fly = true. BucketScript.Update then throws the
                // bucket forward (AddRelativeForce), spins it and spawns the spill.
                b.Full = false;
                b.Fly = true;
            }

            AnimationState st = a[clip];
            st.wrapMode = WrapMode.ClampForever;
            st.speed = 1f;
            st.time = 0f;
            a.CrossFade(clip);
            yan.Tripping = true;
            yan.CanMove = false;

            bool thud = false;
            float elapsed = 0f;
            while (yan != null && st != null)
            {
                yield return null;
                elapsed += Time.deltaTime;
                if (st.time >= st.length || elapsed > 12f) break;
                if (!a.IsPlaying(clip)) a.CrossFade(clip);
                if (st.time < 0.5f)
                {
                    if (yan.MyController != null) yan.MyController.Move(yan.transform.forward * Time.deltaTime * 2f);
                    if (!thud && st.time > 0.33333f)
                    {
                        thud = true;
                        if (yan.Thud != null) AudioSource.PlayClipAtPoint(yan.Thud, yan.transform.position);
                    }
                }
                else
                {
                    bool mash = false;
                    try { mash = Input.GetKeyDown(KeyCode.E); } catch (Exception) { }
                    if (mash || ReadA()) st.speed += 0.5f;
                }
            }
            if (st != null) st.speed = 1f;
            if (yan != null) { yan.Tripping = false; yan.CanMove = true; }
            tripRunning = false;
        }

        // ---------------- Giggle ----------------
        // The game's own giggle: pick one of three giggle animations with its matching
        // sound, play the little giggle lines, and hold her still until it finishes.
        // (No "giggle disc" is spawned, so students are not alerted by it.)

        bool ReadRB()
        {
            if (rbMissing) return false;
            try { return Input.GetButtonDown("RB"); }
            catch (Exception) { rbMissing = true; return false; }
        }

        void HandleBucketGiggle()
        {
            if (!giggleEnabled || giggleRunning || tripRunning || open || yan == null) return;

            bool pressed = false;
            try { pressed = Input.GetKeyDown(giggleKey.Value); } catch (Exception) { }
            if (!pressed) pressed = ReadRB();
            if (!pressed) return;

            // Same test as the original: the thing in her hands is a bucket.
            if (yan.PickUp == null || yan.PickUp.Bucket == null) return;
            if (!yan.CanMove) { status = "Bucket key: she can't move right now."; return; }
            CharacterController cc = yan.MyController;
            bool moving = yan.v != 0f || yan.h != 0f || (cc != null && cc.velocity.sqrMagnitude > 0.25f);
            if (!moving) { status = "Bucket key: walk or run while pressing it."; return; }
            if (bucketAction == 0) TripNow(); else GiggleNow();
        }

        void GiggleNow()
        {
            if (yan == null) { status = "Player not found."; return; }
            if (giggleRunning || yan.Laughing) return;
            if (yan.NoLaugh) { status = "Giggling isn't allowed right now."; return; }
            StartCoroutine(GiggleRoutine());
        }

        IEnumerator GiggleRoutine()
        {
            giggleRunning = true;

            int id = UnityEngine.Random.Range(0, 3);
            string anim = "f02_giggle_0" + (id + 1);
            AudioClip clip = id == 0 ? yan.Laugh0 : (id == 1 ? yan.Laugh1 : yan.Laugh2);

            yan.LaughAnim = anim;
            try
            {
                FieldInfo lc = typeof(YandereScript).GetField("LaughClip", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (lc != null) lc.SetValue(yan, clip);
            }
            catch (Exception) { }

            yan.LaughIntensity = 1f;
            if (yan.MyAudio != null && clip != null)
            {
                yan.MyAudio.clip = clip;
                yan.MyAudio.time = 0f;
                yan.MyAudio.volume = 1f;
                yan.MyAudio.Play();
            }
            if (yan.GiggleLines != null) yan.GiggleLines.Play();
            if (yan.Teeth != null) yan.Teeth.SetActive(false);
            yan.LaughTimer = 0.5f;
            yan.Laughing = true;
            yan.CanMove = false;
            if (yan.CharacterAnimation != null) yan.CharacterAnimation.CrossFade(anim);

            float t = 0f;
            while (yan != null && t < 1.8f)
            {
                t += Time.deltaTime;
                yield return null;
            }

            if (yan != null)
            {
                if (yan.Laughing) yan.StopLaughing();
                yan.CanMove = true;
            }
            giggleRunning = false;
        }

        // ---------------- Keybinds ----------------

        void KeyRow(string label, ConfigEntry<KeyCode> entry, int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(210));
            string txt = rebinding == id ? "Press a key...  (Esc cancels)" : entry.Value.ToString();
            if (GUILayout.Button(txt)) rebinding = id;
            GUILayout.EndHorizontal();
        }

        void DrawKeybinds()
        {
            GUILayout.Label("=== KEYBINDS ===");
            KeyRow("Open / close this menu", toggleKey, 0);
            KeyRow("Throw bombs", throwKey, 1);
            KeyRow("Fill bucket", bucketKey, 2);
            KeyRow("Bucket trip / giggle (carrying a bucket)", giggleKey, 3);
            KeyRow("Teleport blood-clean kit", cleanupKey, 4);
            KeyRow("Pick bomb (smoke/stink/amnesia)", bombCycleKey, 5);
            KeyRow("Add info points", infoKey, 6);
            KeyRow("Hold for number-key teleports", teleModKey, 7);
            KeyRow("Speed up time (+)", timeUpKey, 8);
            KeyRow("Time back to normal (-)", timeResetKey, 9);
            KeyRow("Switch elimination method", elimKey, 10);
            KeyRow("Teleport electrocution kit", elecKey, 11);
            KeyRow("Tripwire kit", tripKey, 12);
            KeyRow("Pour bucket on / off", spillKey, 13);
            KeyRow("Teleport dismemberment kit", dismemberKey, 14);
            KeyRow("Teleport cover-up kit", coverKey, 24);
            KeyRow("Hold to fast-forward", fastForwardKey, 25);
            GUILayout.Label("Teleport keys (pressed together with " + teleModKey.Value + "):");
            for (int i = 0; i < 9; i++) KeyRow("   " + TeleName[i], teleSlotKeys[i], 15 + i);
            spillKeyOn = GUILayout.Toggle(spillKeyOn, "Pour key works (off: no key pours, not even the game's own Z)");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Info points per press: " + infoAmount, GUILayout.Width(210));
            infoAmount = Mathf.RoundToInt(GUILayout.HorizontalSlider(infoAmount, 1f, 100f));
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Reset keybinds to defaults"))
            {
                toggleKey.Value = KeyCode.F10;
                throwKey.Value = KeyCode.T;
                bucketKey.Value = KeyCode.B;
                giggleKey.Value = KeyCode.LeftControl;
                cleanupKey.Value = KeyCode.V;
                bombCycleKey.Value = KeyCode.G;
                infoKey.Value = KeyCode.P;
                teleModKey.Value = KeyCode.LeftAlt;
                timeUpKey.Value = KeyCode.Equals;
                timeResetKey.Value = KeyCode.Minus;
                elimKey.Value = KeyCode.K;
                elecKey.Value = KeyCode.N;
                tripKey.Value = KeyCode.J;
                spillKey.Value = KeyCode.Z;
                dismemberKey.Value = KeyCode.U;
                coverKey.Value = KeyCode.H;
                fastForwardKey.Value = KeyCode.BackQuote;
                for (int i = 0; i < 9; i++) teleSlotKeys[i].Value = KeyCode.Alpha1 + i;
                rebinding = -1;
                Config.Save();
                status = "Keybinds reset.";
            }
            showHelper = GUILayout.Toggle(showHelper, "Show the topic helper and compliment highlight while talking to students");
            giggleEnabled = GUILayout.Toggle(giggleEnabled, "Enable the bucket key (carry a bucket, move, press it or RB)");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Bucket key does:", GUILayout.Width(120));
            bucketAction = GUILayout.Toolbar(bucketAction, new string[] { "Trip + fling bucket", "Giggle" });
            GUILayout.EndHorizontal();
            GUILayout.Label("Your settings (locks, speeds, bombs, buckets, infinite items) are saved when you close the menu.");
        }

        void HandleRebind()
        {
            if (rebinding < 0) return;
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
            {
                if (e.keyCode != KeyCode.Escape)
                {
                    List<ConfigEntry<KeyCode>> all = new List<ConfigEntry<KeyCode>> { toggleKey, throwKey, bucketKey, giggleKey, cleanupKey, bombCycleKey, infoKey, teleModKey, timeUpKey, timeResetKey, elimKey, elecKey, tripKey, spillKey, dismemberKey };
                    all.AddRange(teleSlotKeys);
                    all.Add(coverKey);
                    all.Add(fastForwardKey);
                    ConfigEntry<KeyCode> target = all[Mathf.Clamp(rebinding, 0, all.Count - 1)];
                    target.Value = e.keyCode;
                    Config.Save();
                    status = "Key set to " + e.keyCode + ".";
                }
                rebinding = -1;
                e.Use();
            }
        }

        // ---------------- Weapons tab ----------------

        Vector3 FrontOfPlayer(float dist, float up)
        {
            return yan.transform.position + yan.transform.forward * dist + Vector3.up * up;
        }

        void BringObject(GameObject go, Vector3 pos)
        {
            if (go == null) return;
            if (!go.activeSelf) go.SetActive(true);
            go.transform.position = pos;
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        }


        // ------------------------------------------------------------------
        //  v0.6: pickup side effects, club perks, speed-up, cleanup kit
        // ------------------------------------------------------------------

        static List<T> FindAllScene<T>() where T : Component
        {
            List<T> list = new List<T>();
            foreach (T t in Resources.FindObjectsOfTypeAll<T>())
                if (t != null && t.gameObject.scene.IsValid()) list.Add(t);
            return list;
        }

        void HandleV06()
        {
            if (yan == null) return;
            if (syncAt > 0f && Time.unscaledTime >= syncAt) { syncAt = -1f; SyncPickupEffects(); }

            if (allClubPerks && manager != null && clubScene != manager && Time.timeSinceLevelLoad > 3f) { clubScene = manager; ApplyAllClubBenefits(); }

            if (!open)
            {
                bool k = false;
                try { k = Input.GetKeyDown(cleanupKey.Value); } catch (Exception) { }
                if (k) CleanupKit();
            }
        }

        // Items you check that normally do something when picked up (masking tape, tape player,
        // answer sheet) get the same scene changes here so you don't need to grab them first.
        void SyncPickupEffects()
        {
            if (yan == null || yan.Inventory == null) return;
            InventoryScript inv = yan.Inventory;
            try
            {
                if (inv.MaskingTape)
                    foreach (MaskingTapeScript t in FindAllScene<MaskingTapeScript>())
                    {
                        if (t.Box != null)
                        {
                            t.Box.enabled = true;
                            if (t.Box.Prompt != null) t.Box.Prompt.enabled = true;
                        }
                        t.gameObject.SetActive(false);
                    }
                if (inv.TapePlayer)
                    foreach (TapePlayerScript t in FindAllScene<TapePlayerScript>())
                    {
                        if (t.TaskTapePlayer == null || t.TaskTapePlayer == t.gameObject) continue;
                        if (t.Prompt != null) { t.Prompt.Hide(); t.Prompt.enabled = false; }
                        t.TaskTapePlayer.SetActive(true);
                        t.gameObject.SetActive(false);
                    }
                if (inv.AnswerSheet)
                    foreach (AnswerSheetScript t in FindAllScene<AnswerSheetScript>())
                    {
                        if (t.DoorGap == null) continue;
                        if (t.DoorGap.Prompt != null) t.DoorGap.Prompt.enabled = true;
                        if (t.Prompt != null) { t.Prompt.Hide(); t.Prompt.enabled = false; }
                        t.gameObject.SetActive(false);
                    }
            }
            catch (Exception e) { Logger.LogWarning("SyncPickupEffects: " + e.Message); }
        }

        // ---------------- Club perks ----------------

        static readonly int[] PerkClubs = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 15 }; // everything except Delinquent/Bully/Council

        void ApplyAllClubBenefits()
        {
            ClubManagerScript cm = FindObjectOfType<ClubManagerScript>();
            if (cm == null || cm == clubOwner) return;
            clubOwner = cm;
            ClubType orig = yan.Club;
            foreach (int c in PerkClubs)
            {
                try { yan.Club = (ClubType)c; cm.ActivateClubBenefit(); }
                catch (Exception e) { Logger.LogWarning("Club benefit " + c + ": " + e.Message); }
            }
            yan.Club = orig;
            status = "All club object perks enabled.";
        }

        public static bool HasClubPerk(YandereScript y, int c)
        {
            if (y == null) return false;
            if ((int)y.Club == c) return true;
            return allClubPerks && Array.IndexOf(PerkClubs, c) >= 0;
        }

        static bool LdcInt(CodeInstruction ci, out int v)
        {
            v = 0;
            OpCode o = ci.opcode;
            if (o == OpCodes.Ldc_I4_M1) v = -1;
            else if (o == OpCodes.Ldc_I4_0) v = 0;
            else if (o == OpCodes.Ldc_I4_1) v = 1;
            else if (o == OpCodes.Ldc_I4_2) v = 2;
            else if (o == OpCodes.Ldc_I4_3) v = 3;
            else if (o == OpCodes.Ldc_I4_4) v = 4;
            else if (o == OpCodes.Ldc_I4_5) v = 5;
            else if (o == OpCodes.Ldc_I4_6) v = 6;
            else if (o == OpCodes.Ldc_I4_7) v = 7;
            else if (o == OpCodes.Ldc_I4_8) v = 8;
            else if (o == OpCodes.Ldc_I4_S) v = Convert.ToInt32(ci.operand);
            else if (o == OpCodes.Ldc_I4) v = Convert.ToInt32(ci.operand);
            else return false;
            return true;
        }

        // Rewrites "yan.Club == X" / "!= X" checks to call HasClubPerk(yan, X).
        public static IEnumerable<CodeInstruction> ClubTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> src = new List<CodeInstruction>(instructions);
            List<CodeInstruction> res = new List<CodeInstruction>(src.Count);
            FieldInfo fClub = typeof(YandereScript).GetField("Club");
            MethodInfo has = typeof(ModMenuPlugin).GetMethod("HasClubPerk");
            for (int i = 0; i < src.Count; i++)
            {
                CodeInstruction ins = src[i];
                int c;
                if (ins.opcode == OpCodes.Ldfld && (ins.operand as FieldInfo) == fClub && i + 2 < src.Count && LdcInt(src[i + 1], out c))
                {
                    OpCode br = src[i + 2].opcode;
                    bool bne = br == OpCodes.Bne_Un || br == OpCodes.Bne_Un_S;
                    bool beq = br == OpCodes.Beq || br == OpCodes.Beq_S;
                    bool ceq = br == OpCodes.Ceq;
                    if (bne || beq || ceq)
                    {
                        CodeInstruction ldc = src[i + 1];
                        ldc.labels.AddRange(ins.labels);
                        ldc.blocks.AddRange(ins.blocks);
                        res.Add(ldc);
                        res.Add(new CodeInstruction(OpCodes.Call, has));
                        if (bne) res.Add(new CodeInstruction(OpCodes.Brfalse, src[i + 2].operand));
                        else if (beq) res.Add(new CodeInstruction(OpCodes.Brtrue, src[i + 2].operand));
                        i += 2;
                        continue;
                    }
                }
                res.Add(ins);
            }
            return res;
        }

        // ---------------- Speed-up time ----------------

        public static void SpeedPrefix(YandereScript __instance)
        {
            shotsRestore = -1;
            if (!noPhotoSpeed || __instance == null || __instance.Inventory == null) return;
            if (__instance.Inventory.SenpaiShots == 0) { __instance.Inventory.SenpaiShots = 1; shotsRestore = 0; }
        }

        public static void SpeedPostfix(YandereScript __instance)
        {
            if (__instance == null) return;
            if (shotsRestore == 0 && __instance.Inventory != null) __instance.Inventory.SenpaiShots = 0;
            shotsRestore = -1;
            if (fastSpeedUp && hotkeyScale <= 1.001f && __instance.SenpaiGazing && Time.timeScale > 0.01f) Time.timeScale = speedUpScale;
        }

        // ---------------- v0.12: easy Befriend/Betray minigames ----------------

        // Stalker: once you're within MinimumDistance he starts warning you and then chases.
        // Setting it to 0 stops that; walking into his boundary areas still alarms him.
        public static void StalkerPrefix(StalkerScript __instance)
        {
            if (__instance == null) return;
            float orig;
            if (!stalkerOrigDist.TryGetValue(__instance, out orig)) { orig = __instance.MinimumDistance; stalkerOrigDist[__instance] = orig; }
            __instance.MinimumDistance = easyMinigames ? 0f : orig;
        }

        // Asylum (and any other EvilPhotographerScript NPC: the photographer, the squatters, guards).
        // They only build up detection, and only catch you by bumping into you, while you're inside
        // their MinimumDistance. Setting it to 0 makes them never notice you.
        public static void AsylumPrefix(EvilPhotographerScript __instance)
        {
            if (__instance == null) return;
            float orig;
            if (!asylumOrigDist.TryGetValue(__instance, out orig)) { orig = __instance.MinimumDistance; asylumOrigDist[__instance] = orig; }
            __instance.MinimumDistance = easyMinigames ? 0f : orig;
        }

        // Struggle bar: Victory runs from -100 (you lose) to 100 (you win). Push it up a little each
        // frame and make you unable to lose. It goes past 100 so the game's own clamp lands on exactly 100.
        public static void StrugglePostfix(StruggleBarScript __instance)
        {
            if (!easyMinigames || __instance == null || !__instance.Struggling) return;
            __instance.Invincible = true;
            __instance.Victory = Mathf.Min(__instance.Victory + Time.deltaTime * 35f, 130f);
        }

        // Pickpocketing: you normally get 1 second per button. Keep the timer from running out
        // while a button is showing.
        public static void PickpocketPrefix(PickpocketMinigameScript __instance)
        {
            if (!easyMinigames || __instance == null || !__instance.Show) return;
            if (__instance.ButtonID > 0 && __instance.Timer > 0.5f) __instance.Timer = 0.5f;
        }

        // Basement: the torture screen loads the prisoner's sanity into HomePrisonerScript.Sanity, and a
        // torture choice saves a lower value with StudentGlobals.SetStudentSanity. Once the saved value
        // drops below what was loaded (a torture just happened), set it to 0 = mind-broken.
        public static void PrisonerPostfix(HomePrisonerScript __instance)
        {
            if (!instantBreak || __instance == null || __instance.PrisonerManager == null) return;
            int id = __instance.PrisonerManager.StudentID;
            if (id <= 0) return;
            int now = StudentGlobals.GetStudentSanity(id);
            if (now > 0 && now < __instance.Sanity) StudentGlobals.SetStudentSanity(id, 0);
        }

        // COMMAND 2 (StudentScript.GetWet): splashing someone by tripping costs reputation.
        public static void GetWetPostfix(StudentScript __instance)
        {
            try
            {
                YandereScript y = __instance.Yandere;
                if (y == null || !y.Tripping || y.Mask != null) return;
                __instance.RepLoss = 10f;
                __instance.RepDeduction = 0f;
                __instance.CalculateReputationPenalty();
                if (__instance.RepDeduction >= 0f) __instance.RepLoss -= __instance.RepDeduction;
                if (__instance.Reputation != null) __instance.Reputation.PendingRep -= __instance.RepLoss * __instance.Paranoia;
                __instance.PendingRep -= __instance.RepLoss * __instance.Paranoia;
            }
            catch (Exception) { }
        }

        // ---------------- Instant disposal (Harmony postfixes: run right after each object's Update) ----------------

        public static void IncineratorPostfix(IncineratorScript __instance)
        {
            if (!instantDisposal || __instance == null) return;
            // Bodies, weapons and evidence are destroyed the moment it is lit; the 60-minute
            // Timer is only the burn/cooldown. Cut it so Finish() runs on the next frame.
            if (__instance.Timer > 0.01f) __instance.Timer = 0.0001f;
        }

        public static void WoodChipperPostfix(WoodChipperScript __instance)
        {
            if (!instantDisposal || __instance == null) return;
            if (__instance.BurnTimer > 0f) __instance.BurnTimer = 0f; // kiln cooldown
        }

        public static void WasherPostfix(WashingMachineScript __instance)
        {
            if (!instantDisposal || __instance == null) return;
            try
            {
                if (__instance.Washing)
                {
                    if (__instance.WashTimer > 0.01f) __instance.WashTimer = 0.0001f; // Finish() next frame
                    return;
                }
                // The game only shows "put in" for bloody / red-paint clothing. Allow any clothing.
                PromptScript p = __instance.Prompt;
                if (p != null && p.Yandere != null && p.Yandere.PickUp != null && p.Yandere.PickUp.Clothing &&
                    p.HideButton != null && p.HideButton.Length > 3)
                    p.HideButton[3] = false;
            }
            catch (Exception) { }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void InitHarmony()
        {
            harmony = new Harmony("local.yansim.modmenu");
            try
            {
                harmony.Patch(AccessTools.Method(typeof(BucketScript), "Update"),
                    prefix: new HarmonyMethod(typeof(ModMenuPlugin).GetMethod("BucketPrefix")),
                    finalizer: new HarmonyMethod(typeof(ModMenuPlugin).GetMethod("BucketFinalizer")));
                Logger.LogInfo("Bucket spill patch applied.");
            }
            catch (Exception e) { Logger.LogWarning("Bucket spill patch failed: " + e.Message); }
            try
            {
                harmony.Patch(AccessTools.Method(typeof(ChangingBoothScript), "Update"),
                    prefix: new HarmonyMethod(typeof(ModMenuPlugin).GetMethod("BoothPrefix")),
                    finalizer: new HarmonyMethod(typeof(ModMenuPlugin).GetMethod("BoothFinalizer")));
                Logger.LogInfo("Club changing booth patch applied.");
            }
            catch (Exception e) { Logger.LogWarning("Club changing booth patch failed: " + e.Message); }
            Type[] types = { typeof(FootprintScript), typeof(FootprintSpawnerScript), typeof(SecurityCameraScript), typeof(ShutterScript), typeof(ShutterScript),
                typeof(StruggleBarScript), typeof(StudentScript), typeof(StudentScript), typeof(StudentScript), typeof(StudentScript), typeof(StudentScript),
                typeof(StudentScript), typeof(StudentScript), typeof(TaskManagerScript), typeof(WeaponScript), typeof(WeaponScript), typeof(YandereScript),
                typeof(YandereScript), typeof(YandereScript), typeof(ModernRivalSabotageScript) };
            string[] names = { "Start", "Update", "OnTriggerStay", "ResumeGameplay", "Update",
                "Update", "AttackReaction", "DetermineTeacherSubtitle", "DetermineWhatWasWitnessed", "GetSleuthTarget", "UpdatePerception",
                "UpdateRoutine", "UpdateVision", "CheckTaskRequirement", "LateUpdate", "SuspicionCheck", "UpdateMovement",
                "UpdateNumbness", "set_Bloodiness", "Update" };
            HarmonyMethod tr = new HarmonyMethod(typeof(ModMenuPlugin).GetMethod("ClubTranspiler"));
            int ok = 0;
            for (int i = 0; i < types.Length; i++)
            {
                try
                {
                    MethodInfo m = AccessTools.Method(types[i], names[i]);
                    if (m == null) { Logger.LogWarning("Club patch: " + types[i].Name + "." + names[i] + " not found"); continue; }
                    harmony.Patch(m, null, null, tr);
                    ok++;
                }
                catch (Exception e) { Logger.LogWarning("Club patch " + types[i].Name + "." + names[i] + " failed: " + e.Message); }
            }
            Logger.LogInfo("Club perk patches applied: " + ok + "/" + types.Length);
            try
            {
                MethodInfo ue = AccessTools.Method(typeof(YandereScript), "UpdateEffects");
                harmony.Patch(ue, new HarmonyMethod(typeof(ModMenuPlugin).GetMethod("SpeedPrefix")), new HarmonyMethod(typeof(ModMenuPlugin).GetMethod("SpeedPostfix")));
                Logger.LogInfo("Speed-up patch applied.");
            }
            catch (Exception e) { Logger.LogWarning("Speed-up patch failed: " + e.Message); }
            try
            {
                harmony.Patch(AccessTools.Method(typeof(StudentScript), "GetWet"), null, new HarmonyMethod(typeof(ModMenuPlugin).GetMethod("GetWetPostfix")));
                Logger.LogInfo("Bucket trip reputation patch applied.");
            }
            catch (Exception e) { Logger.LogWarning("GetWet patch failed: " + e.Message); }
            PatchPostfix(typeof(IncineratorScript), "Update", "IncineratorPostfix");
            PatchPostfix(typeof(WoodChipperScript), "Update", "WoodChipperPostfix");
            PatchPostfix(typeof(WashingMachineScript), "Update", "WasherPostfix");
            PatchPostfix(typeof(StruggleBarScript), "Update", "StrugglePostfix");
            PatchPrefix(typeof(StalkerScript), "Update", "StalkerPrefix");
            PatchPrefix(typeof(PickpocketMinigameScript), "Update", "PickpocketPrefix");
            PatchPrefix(typeof(EvilPhotographerScript), "Update", "AsylumPrefix");
            PatchPostfix(typeof(HomePrisonerScript), "Update", "PrisonerPostfix");
        }

        void PatchPrefix(Type type, string method, string prefix)
        {
            try
            {
                harmony.Patch(AccessTools.Method(type, method), new HarmonyMethod(typeof(ModMenuPlugin).GetMethod(prefix)));
                Logger.LogInfo("Patched " + type.Name + "." + method + " (prefix).");
            }
            catch (Exception e) { Logger.LogWarning(type.Name + "." + method + " prefix failed: " + e.Message); }
        }

        void PatchPostfix(Type type, string method, string postfix)
        {
            try
            {
                harmony.Patch(AccessTools.Method(type, method), null, new HarmonyMethod(typeof(ModMenuPlugin).GetMethod(postfix)));
                Logger.LogInfo("Patched " + type.Name + "." + method + ".");
            }
            catch (Exception e) { Logger.LogWarning(type.Name + "." + method + " patch failed: " + e.Message); }
        }

        // ---------------- v0.18 ----------------

        // Wrapping a corpse plays Yandere's ConcealAnim; the wrap finishes when that animation
        // reaches its end (YandereScript.UpdateMovement -> StopWrappingCorpse). Playing it at 20x
        // finishes the wrap in a fraction of a second, through the game's own ending.
        void ApplyInstantWrap()
        {
            if (!instantWrap || yan == null || !yan.WrappingCorpse || yan.CharacterAnimation == null || string.IsNullOrEmpty(yan.ConcealAnim)) return;
            AnimationState st = yan.CharacterAnimation[yan.ConcealAnim];
            if (st != null && st.speed < 20f) st.speed = 20f;
        }

        // Attack kills run through AttackManagerScript, with its own timers, the victim's animation
        // and sometimes a slow-motion moment. Speeding up the whole game while the kill plays keeps
        // all of that in step; time goes back to what it was the moment the kill ends.
        // Runs in LateUpdate so it wins over the game's own slow motion.
        void ApplyAttackSpeed()
        {
            bool attacking = false;
            if (!open && yan != null)
            {
                if (fastAttack) attacking = yan.Attacking || (yan.AttackManager != null && yan.AttackManager.IsAttacking());
                if (fastDismember && yan.Dismembering) attacking = true;
                if (fastWash && yan.CleaningWeapon) attacking = true;
                if (fastShower && yan.Bathing) attacking = true;
                if (fastDispose && (yan.Dumping || yan.DumpsterGrabbing || yan.Chipping || yan.Burying || yan.Digging)) attacking = true;
            }
            // Held fast-forward works anywhere, including cutscenes in other scenes.
            if (!open && rebinding < 0 && fastForwardKey != null && Input.GetKey(fastForwardKey.Value)) attacking = true;
            if (attacking)
            {
                if (!attackBoosting) { attackBoosting = true; attackRestoreScale = Time.timeScale > 0f ? Time.timeScale : 1f; }
                if (Time.timeScale > 0f) Time.timeScale = Mathf.Max(1f, attackSpeed);
            }
            else if (attackBoosting)
            {
                attackBoosting = false;
                if (Time.timeScale > 0f) Time.timeScale = attackRestoreScale;
            }
        }

        // The Void Goddess is normally only switched on by the debug enabler. Her prompt reads
        // "Summon An Ancient Evil"; once summoned she can Pass Judgement, Follow or be dismissed.
        // The game marks a run that uses her as invalid, the same as with the debug menu.
        void KeepVoidGoddess()
        {
            goddessScene = manager;
            if (MissionModeGlobals.MissionMode) return;
            DebugEnablerScript de = FindObjectOfType<DebugEnablerScript>(true);
            if (de == null || de.VoidGoddess == null) { Logger.LogInfo("Void Goddess not found in this scene."); return; }
            if (!de.VoidGoddess.activeSelf) de.VoidGoddess.SetActive(true);
            Logger.LogInfo("Void Goddess enabled.");
        }

        // Saw (circular saw, weapon 7, or any chainsaw) and a tarp to lay the body on.
        void DismembermentKit()
        {
            if (yan == null) { status = "Player not found."; return; }
            List<string> got = new List<string>(), miss = new List<string>();
            DebugMenuScript dm = FindObjectOfType<DebugMenuScript>(true);
            WeaponScript saw = null;
            if (dm != null && dm.CircularSaw != null) saw = dm.CircularSaw.GetComponent<WeaponScript>();
            if (saw == null) saw = FindWeapon(w => w.WeaponID == 7 || w.Chainsaw);
            if (saw != null) { BringWeaponTo(saw, FrontOfPlayer(1.4f, 0.4f) - yan.transform.right * 0.4f); got.Add(string.IsNullOrEmpty(saw.Name) ? "saw" : saw.Name); }
            else miss.Add("saw");

            PickUpScript tarp = null;
            foreach (PickUpScript pu in FindAllScene<PickUpScript>())
            {
                if (pu == null || !pu.Tarp || pu == yan.PickUp) continue;
                if (tarp == null || (!tarp.gameObject.activeInHierarchy && pu.gameObject.activeInHierarchy)) tarp = pu;
            }
            if (tarp != null) { BringPickUp(tarp, FrontOfPlayer(1.6f, 0.5f) + yan.transform.right * 0.4f); got.Add("tarp"); }
            else miss.Add("tarp");
            status = got.Count > 0 ? "Dismemberment kit: " + string.Join(", ", got.ToArray()) + (miss.Count > 0 ? "  (not found: " + string.Join(", ", miss.ToArray()) + ")" : "")
                                   : "Dismemberment kit not found in this scene.";
        }

        // Things that keep blood off your uniform: the raincoat (RaincoatScript), and the GloveScript
        // pickups, which are the gloves, the occult robe and the raincoat-style coverings.
        void CoverUpKit()
        {
            if (yan == null) { status = "Player not found."; return; }
            List<string> got = new List<string>();
            int n = 0;
            GloveScript robe = null, coat = null, gloves = null;
            foreach (GloveScript g in FindAllScene<GloveScript>())
            {
                if (g == null || g.PickUp == null || g.PickUp == yan.PickUp) continue;
                if (g.OccultRobe) { if (robe == null || (!robe.gameObject.activeInHierarchy && g.gameObject.activeInHierarchy)) robe = g; }
                else if (g.Raincoat) { if (coat == null || (!coat.gameObject.activeInHierarchy && g.gameObject.activeInHierarchy)) coat = g; }
                else if (gloves == null || (!gloves.gameObject.activeInHierarchy && g.gameObject.activeInHierarchy)) gloves = g;
            }
            foreach (KeyValuePair<string, GloveScript> kv in new[] { new KeyValuePair<string, GloveScript>("occult robe", robe), new KeyValuePair<string, GloveScript>("raincoat", coat), new KeyValuePair<string, GloveScript>("gloves", gloves) })
            {
                if (kv.Value == null) continue;
                BringPickUp(kv.Value.PickUp, FrontOfPlayer(1.4f, 0.4f) + yan.transform.right * (0.5f * n++ - 0.5f));
                got.Add(kv.Value.gameObject.activeInHierarchy ? kv.Key : kv.Key + " (not active in this mode)");
            }
            if (coat == null && !yan.WearingRaincoat)
            {
                RaincoatScript rc = FindAllScene<RaincoatScript>().FirstOrDefault(x => x != null);
                if (rc != null) { BringObject(rc.gameObject, FrontOfPlayer(1.4f, 0.4f) + yan.transform.right * (0.5f * n++ - 0.5f)); got.Add("raincoat"); }
            }
            status = got.Count > 0 ? "Cover-up kit: " + string.Join(", ", got.ToArray()) + ". Put one on before you get bloody." : "No raincoat, robe or gloves found in this scene.";
        }

        // ---- bring extra from home ----
        // The game's bring-item menu allows one ID (PlayerGlobals.BringingItem):
        //   1-3  weapons (WeaponManager.BroughtWeapons[id], handed to you at the start of the day)
        //   4 rat poison, 5 sake, 6 cigarettes, 7 condoms, 8 lockpick, 9 sedative, 10 narcotics,
        //   11 lethal poison, 12 explosive device  (InventoryScript.Start)
        // Extras are applied the same way, once per school day, on top of the game's own pick.
        static readonly string[] DefaultBringNames = { "", "Brought weapon 1", "Brought weapon 2", "Brought weapon 3", "Rat poison", "Sake",
            "Cigarettes", "Condoms", "Lockpick", "Sedative", "Narcotics", "Lethal poison", "Explosive device" };

        string BringName(int id)
        {
            if (bringNames != null && id < bringNames.Length && !string.IsNullOrEmpty(bringNames[id])) return bringNames[id];
            return id < DefaultBringNames.Length ? DefaultBringNames[id] : "Item " + id;
        }

        // Read the real names from the game's bring-item menu (at home), once per scene.
        void CaptureBringNames()
        {
            if (manager != null) return;
            int scene = SceneManager.GetActiveScene().handle;
            if (scene == bringNamesScene) return;
            bringNamesScene = scene;
            BringItemScript bi = FindObjectOfType<BringItemScript>(true);
            if (bi == null || bi.Labels == null) return;
            string[] names = new string[13];
            for (int i = 1; i < 13 && i < bi.Labels.Length; i++)
            {
                if (bi.Labels[i] == null) continue;
                string t = System.Text.RegularExpressions.Regex.Replace(bi.Labels[i].text ?? "", @"\[[^\]]*\]", "").Trim();
                if (t.Length > 0 && t.IndexOf('?') < 0) names[i] = t.Replace("|", "/");
            }
            if (bringNames != null)
                for (int i = 1; i < 13; i++)
                    if (string.IsNullOrEmpty(names[i]) && i < bringNames.Length) names[i] = bringNames[i];
            bringNames = names;
            cfgBringNames.Value = string.Join("|", names.Select(n => n ?? "").ToArray());
            Logger.LogInfo("Bring-item names read from the home menu.");
        }

        void ApplyExtraBring()
        {
            if (yan == null || yan.Inventory == null) return;
            bringScene = manager;
            if (MissionModeGlobals.MissionMode || extraBringIds.Count == 0) return;
            int chosen = PlayerGlobals.BringingItem;   // the game already gave you this one
            InventoryScript inv = yan.Inventory;
            List<string> got = new List<string>();
            foreach (int id in extraBringIds.Where(x => x >= 4).OrderBy(x => x))
            {
                if (id == chosen) continue;
                switch (id)
                {
                    case 4: inv.ArrivedWithRatPoison = true; inv.EmeticPoisons++; break;
                    case 5: inv.ArrivedWithSake = true; inv.Sake = true; break;
                    case 6: inv.ArrivedWithCigs = true; inv.Cigs = true; break;
                    case 7: inv.ArrivedWithCondoms = true; inv.Condoms = true; break;
                    case 8: inv.LockPicks = Math.Max(inv.LockPicks, 0) + 1; break;
                    case 9: inv.ArrivedWithSedative = true; inv.SedativePoisons++; break;
                    case 10: inv.ArrivedWithNarcotics = true; inv.Narcotics = true; break;
                    case 11: inv.ArrivedWithPoison = true; inv.LethalPoisons++; break;
                    case 12: if (inv.ExplosiveDeviceSet != null) inv.ExplosiveDeviceSet.SetActive(true); break;
                    default: continue;
                }
                got.Add(BringName(id));
            }

            WeaponManagerScript wm = FindObjectOfType<WeaponManagerScript>();
            if (wm != null && wm.BroughtWeapons != null && yan.Weapon != null)
            {
                int free = 0;
                for (int s = 1; s <= 2 && s < yan.Weapon.Length; s++) if (yan.Weapon[s] == null) free++;
                int placed = 0;
                foreach (int id in extraBringIds.Where(x => x >= 1 && x <= 3).OrderBy(x => x))
                {
                    if (id == chosen || id >= wm.BroughtWeapons.Length || wm.BroughtWeapons[id] == null) continue;
                    WeaponScript w = wm.BroughtWeapons[id];
                    if (Array.IndexOf(yan.Weapon, w) >= 0) continue;
                    w.gameObject.SetActive(true);
                    if (free > 0 && w.Prompt != null && w.Prompt.Circle != null && w.Prompt.Circle.Length > 3)
                    {
                        // Same as the game does for the weapon you picked at home: picked up straight away.
                        w.Prompt.Circle[3].fillAmount = 0f;
                        w.UnequipImmediately = true;
                        free--;
                    }
                    else BringObject(w.gameObject, FrontOfPlayer(1.2f, 0.4f) + yan.transform.right * (0.4f * placed++));
                    got.Add(string.IsNullOrEmpty(w.Name) ? BringName(id) : w.Name);
                }
            }
            if (got.Count > 0)
            {
                Logger.LogInfo("Brought extra from home: " + string.Join(", ", got.ToArray()));
                Toast("Also brought from home: " + string.Join(", ", got.ToArray()));
            }
        }

        // ---------------- Faster loading ----------------
        // Two things the game leaves on the table:
        // 1. Unity's background loading priority. At Normal it spends about 10 ms of each frame
        //    finishing loaded assets on the main thread; at High it may use up to 50 ms, so a scene
        //    that loads with LoadSceneAsync (the school, from the loading screen) finishes sooner.
        // 2. SceneLoader (the loading screen with the controls) counts 10 frames before it even
        //    starts loading the school. That count is skipped.
        // The rest of the wait is the game itself building the school and its 100 students.
        void ApplyFastLoading()
        {
            if (fastLoading != fastLoadingApplied || (fastLoading && Application.backgroundLoadingPriority != ThreadPriority.High))
            {
                Application.backgroundLoadingPriority = fastLoading ? ThreadPriority.High : ThreadPriority.Normal;
                if (fastLoading != fastLoadingApplied) Logger.LogInfo("Faster loading " + (fastLoading ? "on" : "off") + ".");
                fastLoadingApplied = fastLoading;
            }
            if (!fastLoading) return;
            if (loaderSeen == null)
            {
                // Look once per scene; the loading screen is its own scene.
                int scene = SceneManager.GetActiveScene().handle;
                if (scene == loaderSceneChecked) return;
                loaderSceneChecked = scene;
                loaderSeen = FindObjectOfType<SceneLoader>();
                if (loaderSeen == null) return;
            }
            if (!loaderSeen.Debugging && loaderSeen.Timer < 10f) loaderSeen.Timer = 10f;   // its Update starts loading at exactly 10
        }

        // ---------------- v0.16 ----------------

        // The game spills a full bucket on Z only when debug keys are on (Yandere.NoDebug false).
        // The mod's spill key replaces it: during the bucket's own Update NoDebug is forced on, so
        // the game's Z does nothing and only the spill key (if switched on) spills.
        public static void BucketPrefix(BucketScript __instance, out bool __state)
        {
            __state = false;
            if (__instance != null && __instance.Yandere != null && !__instance.Yandere.NoDebug)
            {
                __instance.Yandere.NoDebug = true;
                __state = true;
            }
        }

        // Club changing booths only offer "change" when Yandere.Club matches the booth, and
        // ChangeClubwear picks the outfit from Yandere.Club. With all club perks on, the player
        // counts as that booth's club for the length of the booth's own Update, so any club's
        // booth works and hands out that club's outfit. Her real club is put back right after.
        public static void BoothPrefix(ChangingBoothScript __instance, out int __state)
        {
            __state = -1;
            if (!allClubPerks || __instance == null || __instance.Yandere == null) return;
            ClubType booth = __instance.ClubID;
            if (booth == ClubType.None || booth == ClubType.Delinquent || booth == ClubType.Bully || booth == ClubType.Council) return;
            if (__instance.Yandere.Club == booth) return;
            __state = (int)__instance.Yandere.Club;
            __instance.Yandere.Club = booth;
        }

        public static void BoothFinalizer(ChangingBoothScript __instance, int __state)
        {
            if (__state >= 0 && __instance != null && __instance.Yandere != null) __instance.Yandere.Club = (ClubType)__state;
        }

        public static void BucketFinalizer(BucketScript __instance, bool __state)
        {
            if (__state && __instance != null && __instance.Yandere != null) __instance.Yandere.NoDebug = false;
        }

        // Endless pour: the key switches it on and off. While on, the bucket leaves a puddle of
        // whatever it holds (water, blood, gasoline, brown paint) every half second or every 0.75 m
        // you walk, using the game's own Spill(), and it never runs dry. It stops by itself when you
        // put the bucket down or it's empty to begin with.
        void TogglePouring()
        {
            if (pouring) { pouring = false; Toast("Stopped pouring."); return; }
            if (!spillKeyOn) return;
            BucketScript b = HeldBucket();
            if (b == null) { Toast("Hold a bucket to pour it."); return; }
            if (!b.Full) { Toast("The bucket is empty. Fill it first (B)."); return; }
            pouring = true;
            pourTimer = 1f;   // first puddle right away
            Toast("Pouring " + BucketLiquid(b) + "... press " + spillKey.Value + " to stop.");
        }

        void UpdatePouring()
        {
            if (!pouring) return;
            BucketScript b = HeldBucket();
            if (b == null || !b.Full || !spillKeyOn) { pouring = false; Toast("Stopped pouring."); return; }
            pourTimer += Time.deltaTime;
            if (pourTimer < 0.5f && Vector3.Distance(yan.transform.position, b.LastSpillPosition) <= 0.75f) return;
            if (!b.CheckForGround()) return;
            pourTimer = 0f;
            b.Spill();
        }

        BucketScript HeldBucket()
        {
            if (yan == null || yan.PickUp == null) return null;
            return yan.PickUp.Bucket;
        }

        static string BucketLiquid(BucketScript b)
        {
            if (b.DyedBrown) return "brown paint";
            if (b.Gasoline) return "gasoline";
            if (b.Bloodiness > 0f) return "blood";
            return "water";
        }

        // The game's own kit from the debug menu; if that isn't in the scene, the wrench (fountain,
        // weapon 24) and screwdriver (power outlet, weapon 6) are found and brought instead.
        void ElectrocutionKit()
        {
            if (yan == null) { status = "Player not found."; return; }
            DebugMenuScript dm = FindObjectOfType<DebugMenuScript>(true);
            if (dm != null && dm.ElectrocutionKit != null && dm.ElectrocutionKit.Length > 0)
            {
                for (int i = 0; i < dm.ElectrocutionKit.Length; i++)
                {
                    if (dm.ElectrocutionKit[i] == null) continue;
                    if (dm.ElectrocutionKit[i].transform.parent != null && !dm.ElectrocutionKit[i].transform.parent.gameObject.activeInHierarchy)
                        dm.ElectrocutionKit[i].transform.SetParent(null, true);
                    dm.ElectrocutionKit[i].transform.position = FrontOfPlayer(1.5f, 0.3f) + yan.transform.right * (0.4f * i);
                }
                if (dm.ElectrocutionKit.Length > 3 && dm.ElectrocutionKit[3] != null) dm.ElectrocutionKit[3].SetActive(true);
                status = "Electrocution kit: wrench the fountain, turn the light off, screwdriver the outlet, then turn the light on when she's in the puddle.";
                return;
            }
            List<string> got = new List<string>();
            WeaponScript wrench = FindWeapon(w => w.WeaponID == 24), driver = FindWeapon(w => w.WeaponID == 6);
            if (wrench != null) { BringWeaponTo(wrench, FrontOfPlayer(1.5f, 0.4f) - yan.transform.right * 0.3f); got.Add(wrench.Name); }
            if (driver != null) { BringWeaponTo(driver, FrontOfPlayer(1.5f, 0.4f) + yan.transform.right * 0.3f); got.Add(driver.Name); }
            status = got.Count > 0 ? "Electrocution kit: " + string.Join(", ", got.ToArray()) + ". Wrench the fountain, turn the light off, screwdriver the outlet."
                                   : "Electrocution kit not found in this scene.";
        }

        // A weapon in the scene that isn't already in your hands or inventory.
        WeaponScript FindWeapon(Func<WeaponScript, bool> match)
        {
            WeaponScript best = null;
            foreach (WeaponScript w in FindAllScene<WeaponScript>())
            {
                if (w == null || !match(w)) continue;
                if (yan.Weapon != null && Array.IndexOf(yan.Weapon, w) >= 0) return w;   // you already have one
                if (best == null || (!best.gameObject.activeInHierarchy && w.gameObject.activeInHierarchy)) best = w;
            }
            return best;
        }

        void BringWeaponTo(WeaponScript w, Vector3 pos)
        {
            if (yan.Weapon != null && Array.IndexOf(yan.Weapon, w) >= 0) return;
            if (w.transform.parent != null && !w.transform.parent.gameObject.activeInHierarchy) w.transform.SetParent(null, true);
            BringObject(w.gameObject, pos);
        }

        // Water cooler tripwire: the cooler needs liquid in it, and you need string, masking tape
        // and a knife (any weapon of type Knife) to set the trap. The cooler isn't filled for you:
        // a bucket comes along so you pick the liquid.
        void TripwireKit()
        {
            if (yan == null || yan.Inventory == null) { status = "Player not found."; return; }
            List<string> got = new List<string>(), miss = new List<string>();
            yan.Inventory.String = true;
            yan.Inventory.MaskingTape = true;
            got.Add("string"); got.Add("masking tape");

            WeaponScript knife = FindWeapon(w => (int)w.Type == 1);
            if (knife != null) { BringWeaponTo(knife, FrontOfPlayer(1.4f, 0.4f) - yan.transform.right * 0.4f); got.Add(knife.Name); }
            else miss.Add("knife");

            WaterCoolerScript cooler = null;
            foreach (WaterCoolerScript wc in FindAllScene<WaterCoolerScript>())
            {
                if (wc == null || wc.TrapSet) continue;
                if (cooler == null || (!cooler.gameObject.activeInHierarchy && wc.gameObject.activeInHierarchy)) cooler = wc;
            }
            if (cooler != null)
            {
                if (cooler.PickUp != null && cooler.PickUp != yan.PickUp) BringPickUp(cooler.PickUp, FrontOfPlayer(1.6f, 0.5f) + yan.transform.right * 0.4f);
                else if (cooler.PickUp == null) BringObject(cooler.gameObject, FrontOfPlayer(1.6f, 0.5f) + yan.transform.right * 0.4f);
                got.Add("water cooler");
            }
            else miss.Add("water cooler");

            // A bucket so you choose the liquid yourself: fill it (B, or a tap), then pour it in the cooler.
            BucketScript bucket = null;
            foreach (BucketScript b in FindAllScene<BucketScript>())
            {
                if (b == null || b.PickUp == null || b.PickUp == yan.PickUp) continue;
                if (bucket == null || (bucket.Full && !b.Full) || (!bucket.gameObject.activeInHierarchy && b.gameObject.activeInHierarchy)) bucket = b;
            }
            if (bucket != null) { BringPickUp(bucket.PickUp, FrontOfPlayer(1.4f, 0.4f) + yan.transform.right * 0.9f); got.Add("bucket"); }
            else miss.Add("bucket");
            status = "Tripwire kit: " + string.Join(", ", got.ToArray()) + (miss.Count > 0 ? "  (not found: " + string.Join(", ", miss.ToArray()) + ")" : "") +
                     ". Fill the bucket with what you want and pour it into the cooler, then carry the knife and set the cooler down.";
        }

        // The Student Info screen (pause menu): + / - change the shown student's reputation by 10,
        // or by 1 while Shift is held. Saved for the game at the end of the day like normal.
        StudentInfoScript StudentInfoShowing()
        {
            if (manager == null) return null;
            if (infoScene != manager) { infoScene = manager; infoCache = FindObjectOfType<StudentInfoScript>(true); }
            StudentInfoScript si = infoCache;
            return si != null && si.gameObject.activeInHierarchy && si.CurrentStudent > 0 ? si : null;
        }

        void HandleInfoRepKeys()
        {
            if (open) return;
            bool up = KeyDown(KeyCode.Equals) || KeyDown(KeyCode.KeypadPlus) || KeyDown(KeyCode.Plus);
            bool down = KeyDown(KeyCode.Minus) || KeyDown(KeyCode.KeypadMinus);
            if (!up && !down) return;
            StudentInfoScript si = StudentInfoShowing();
            if (si == null || manager.StudentReps == null || si.CurrentStudent >= manager.StudentReps.Length) return;
            int id = si.CurrentStudent;
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float step = (shift ? 1f : 10f) * (up ? 1f : -1f);
            float v = Mathf.Clamp(Mathf.Round(manager.StudentReps[id] + step), -100f, 100f);
            manager.StudentReps[id] = v;
            try { StudentGlobals.SetStudentReputation(id, (int)v); } catch (Exception) { }
            si.UpdateInfo(id);
            Toast("Reputation: " + (v > 0 ? "+" : "") + v.ToString("0"));
        }

        // Yakuza: phase 0 = not met yet, 100 = every service introduced (what Custom Mode uses).
        // Not touched while the yakuza menu itself is open, so its cutscenes aren't cut short.
        void KeepYakuzaUnlocked()
        {
            if (!alwaysYakuza) return;
            yakuzaTimer -= Time.unscaledDeltaTime;
            if (yakuzaTimer > 0f) return;
            yakuzaTimer = 2f;
            if (GameGlobals.YakuzaPhase >= 100) return;
            if (FindObjectOfType<YakuzaMenuScript>() != null) return;
            GameGlobals.IntroducedAbduction = true;
            GameGlobals.IntroducedRansom = true;
            GameGlobals.YakuzaPhase = 100;
            Logger.LogInfo("Yakuza unlocked (phase 100).");
        }

        // Embarrassing secret: what reading her diary or buying the service gives (gossip topic).
        // Dark secret: the photographer secret from the diary (locker-note subject). Both are kept
        // for today and saved for the rest of the week.
        void KnowRivalSecrets()
        {
            if (!manager.EmbarassingSecret) manager.EmbarassingSecret = true;
            EndOfDayScript eod = manager.Police != null ? manager.Police.EndOfDay : null;
            if (eod != null && !eod.LearnedAboutPhotographer) eod.LearnedAboutPhotographer = true;
            if (!SchemeGlobals.EmbarassingSecret) SchemeGlobals.EmbarassingSecret = true;
            if (!EventGlobals.LearnedAboutPhotographer) EventGlobals.LearnedAboutPhotographer = true;
        }

        // ---------------- Blood-clean kit ----------------

        // Moves a pickup by its root PickUpScript object (moving a child like the mop head
        // gets snapped back), turning parents on if the item was stored somewhere hidden.
        void BringPickUp(PickUpScript pu, Vector3 pos)
        {
            if (pu == null) return;
            GameObject go = pu.gameObject;
            if (go.transform.parent != null && !go.transform.parent.gameObject.activeInHierarchy) go.transform.SetParent(null, true);
            BringObject(go, pos);
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = false; rb.useGravity = true; }
        }

        void CleanupKit()
        {
            if (yan == null) { status = "Player not found."; return; }
            List<string> got = new List<string>(), miss = new List<string>();
            int slot = 0;
            Vector3 right = yan.transform.right;
            try
            {
                List<PickUpScript> pickups = FindAllScene<PickUpScript>();
                PickUpScript mopPu = null, bleachPu = null, boxPu = null;
                foreach (PickUpScript pu in pickups)
                {
                    if (pu == yan.PickUp) continue;
                    bool isMop = pu.ThisIsAMop || pu.Mop != null;
                    if (isMop && (mopPu == null || (!mopPu.gameObject.activeInHierarchy && pu.gameObject.activeInHierarchy))) mopPu = pu;
                    if (pu.Bleach && (bleachPu == null || (!bleachPu.gameObject.activeInHierarchy && pu.gameObject.activeInHierarchy))) bleachPu = pu;
                    if (pu.GarbageBagBox && (boxPu == null || (!boxPu.gameObject.activeInHierarchy && pu.gameObject.activeInHierarchy))) boxPu = pu;
                }

                if (mopPu != null) { BringPickUp(mopPu, FrontOfPlayer(1.4f, 0.8f) + right * (0.4f * (slot++ - 1.5f))); got.Add("mop"); }
                else if (yan.Mop != null && yan.Mop.PickUp != null && yan.Mop.PickUp != yan.PickUp) { BringPickUp(yan.Mop.PickUp, FrontOfPlayer(1.4f, 0.8f) + right * (0.4f * (slot++ - 1.5f))); got.Add("mop"); }
                else miss.Add("mop");

                BucketScript bucket = null;
                if (manager != null && manager.AllBuckets != null)
                    foreach (BucketScript b in manager.AllBuckets)
                        if (b != null && (yan.PickUp == null || yan.PickUp.Bucket != b)) { bucket = b; break; }
                if (bucket != null)
                {
                    BringObject(bucket.gameObject, FrontOfPlayer(1.4f, 0.5f) + right * (0.4f * (slot++ - 1.5f)));
                    try { FillOne(bucket, 0); } catch (Exception) { }
                    got.Add("bucket");
                }
                else miss.Add("bucket");

                if (bleachPu != null) { BringPickUp(bleachPu, FrontOfPlayer(1.4f, 0.5f) + right * (0.4f * (slot++ - 1.5f))); got.Add("bleach"); } else miss.Add("bleach");
                if (boxPu != null) { BringPickUp(boxPu, FrontOfPlayer(1.4f, 0.5f) + right * (0.4f * (slot++ - 1.5f))); got.Add("body bag box"); } else miss.Add("body bag box");
            }
            catch (Exception e) { Logger.LogWarning("CleanupKit: " + e); }
            status = "Cleanup kit: " + string.Join(", ", got.ToArray()) + (miss.Count > 0 ? "  (not found: " + string.Join(", ", miss.ToArray()) + ")" : "");
        }


        // ------------------------------------------------------------------
        //  v0.7: always-on boxes, bomb picker + HUD, hotkeys, infinite objects
        // ------------------------------------------------------------------

        void Toast(string msg) { toast = msg; toastUntil = Time.unscaledTime + 2.5f; }

        AlphabetScript GetAlphabet()
        {
            if (alphaCache == null) alphaCache = FindObjectOfType<AlphabetScript>(true);
            return alphaCache;
        }

        void HandleV07()
        {
            if (yan == null) return;

            // Every frame (cheap): locks
            Safe("Max reputation", () =>
            {
                if (maxRep && rep != null)
                {
                    if (rep.Reputation < 100f) { rep.Reputation = 100f; PlayerGlobals.Reputation = 100f; }
                    if (rep.PendingRep < 0f) rep.PendingRep = 0f;
                }
                if (infBags && yan.PickUp != null && yan.PickUp.GarbageBagBox && yan.PickUp.BodyBags < 50) yan.PickUp.BodyBags = 100;
            });
            Safe("Instant elimination", ApplyInstantEliminations);
            HandleV07Rest();
        }

        void ApplyInstantEliminations()
        {
            // Rejection: each sabotaged rival/Senpai interaction adds 1 to SabotageProgress; 5 on Friday = rejected.
            // Damaging her reputation (gossip, rumors, photos...) counts too: the confession also fails
            // at -100 reputation.
            if (instantReject && manager != null)
            {
                float rivalRep = RivalRep();
                if (rejectScene != manager) { rejectScene = manager; rivalRepBaseline = rivalRep; }
                bool sabotaged = manager.SabotageProgress > manager.InitialSabotageProgress;
                bool repDropped = !float.IsNaN(rivalRep) && !float.IsNaN(rivalRepBaseline) && rivalRep < rivalRepBaseline - 0.5f;
                if ((sabotaged || repDropped) && manager.SabotageProgress < 5) FillRejection(sabotaged ? "sabotage" : "her reputation dropped");
                if (!float.IsNaN(rivalRep) && (float.IsNaN(rivalRepBaseline) || rivalRep > rivalRepBaseline)) rivalRepBaseline = rivalRep;
            }
            // Expel: each report the counselor accepts adds 1 to RivalExpelProgress (saved at the end of
            // the day). At 5 she is expelled a few seconds after the lecture ends.
            if (instantExpel && manager != null)
            {
                if (counselorScene != manager) { counselorScene = manager; counselorCache = FindObjectOfType<CounselorScript>(); }
                CounselorScript c = counselorCache;
                if (c != null && c.RivalExpelProgress > StudentGlobals.ExpelProgress && c.RivalExpelProgress < 5)
                {
                    c.RivalExpelProgress = 5;
                    Toast("Expel bar filled: 5/5");
                    Logger.LogInfo("Instant expel: rival expel progress set to 5/5.");
                }
            }
        }

        float RivalRep()
        {
            if (manager == null || manager.StudentReps == null) return float.NaN;
            int id = manager.RivalID;
            return id > 0 && id < manager.StudentReps.Length ? manager.StudentReps[id] : float.NaN;
        }

        void FillRejection(string why)
        {
            if (manager == null) return;
            manager.SabotageProgress = 5;
            Toast("Rejection bar filled: 5/5. She'll be rejected at Friday's confession.");
            Logger.LogInfo("Instant rejection (" + why + "): sabotage progress set to 5/5.");
        }

        void HandleV07Rest()
        {

            // Once a second: stats, alphabet tools, battery/body bag boxes, auto unlocks
            autoTimer -= Time.unscaledDeltaTime;
            if (autoTimer <= 0f)
            {
                autoTimer = 1f;
                if (maxStats) Safe("Max stats", ApplyMaxStats);
                if (instantMatch && manager != null && matchScene != manager && Time.timeSinceLevelLoad > 2f) { matchScene = manager; ForceMatchmaking(); }
                if (undoMatchPending && manager != null && Time.timeSinceLevelLoad > 2f) UndoMatchmaking();
                if (weeklyWatchers && manager != null && watchScene != manager && Time.timeSinceLevelLoad > 3f) Safe("Rival watchers", AddWeeklyWatchers);
                if (alphaAlways && manager != null && Time.timeSinceLevelLoad > 2f) Safe("Alphabet tools", KeepAlphabetTools);
                if (rivalSecrets && manager != null && Time.timeSinceLevelLoad > 2f) Safe("Rival secrets", KnowRivalSecrets);
                if (voidGoddess && manager != null && goddessScene != manager && Time.timeSinceLevelLoad > 2f) Safe("Void Goddess", KeepVoidGoddess);
                if (extraBring && manager != null && bringScene != manager && Time.timeSinceLevelLoad > 3f) Safe("Bring extra items", ApplyExtraBring);
                if ((infBattery || infBags) && manager != null && Time.timeSinceLevelLoad > 2f) Safe("Infinite objects", KeepInfiniteObjects);
                if ((autoBefriend || autoTasks || autoInfo || autoLikes) && manager != null && autoScene != manager && Time.timeSinceLevelLoad > 3f)
                {
                    autoScene = manager;
                    string keep = status;
                    try
                    {
                        if (autoBefriend) BefriendEveryone();
                        if (autoTasks) SetAllTasks(3, false);
                        if (autoInfo) FillStudentInfo();
                        if (autoLikes) LearnAllOpinions();
                    }
                    catch (Exception e) { Logger.LogWarning("Auto unlocks: " + e.Message); }
                    status = keep;
                }
            }

            if (open || !yan.CanMove) return;

            // Hotkeys (menu closed)
            if (KeyDown(bombCycleKey.Value))
            {
                bombSel = (bombSel + 1) % 3;
                Toast("Bomb: " + BombNames[bombSel]);
            }
            if (KeyDown(infoKey.Value)) { AddInfoPoints(infoAmount); Toast(status); }
            Safe("Elimination key", HandleElimKeys);
            if (KeyDown(elecKey.Value)) Safe("Electrocution kit", () => { ElectrocutionKit(); Toast(status); });
            if (KeyDown(tripKey.Value)) Safe("Tripwire kit", () => { TripwireKit(); Toast(status); });
            if (KeyDown(dismemberKey.Value)) Safe("Dismemberment kit", () => { DismembermentKit(); Toast(status); });
            if (KeyDown(coverKey.Value)) Safe("Cover-up kit", () => { CoverUpKit(); Toast(status); });
            if (KeyDown(spillKey.Value)) Safe("Pour bucket", TogglePouring);
            bool modHeld = false;
            try { modHeld = teleModKey.Value != KeyCode.None && Input.GetKey(teleModKey.Value); } catch (Exception) { }
            // Without the modifier, never teleport while your hands are busy: the game uses
            // the number keys for dropping / switching what you hold.
            bool armed = false;
            try { armed = yan.Armed; } catch (Exception) { }   // throws at home, where there are no weapon slots
            bool handsBusy = yan.PickUp != null || yan.Carrying || yan.Dragging || armed;
            if (teleKeys && (teleAlt ? modHeld : !handsBusy))
            {
                for (int i = 0; i < 9; i++)
                {
                    KeyCode k = teleSlotKeys[i] != null ? teleSlotKeys[i].Value : KeyCode.Alpha1 + i;
                    // The keypad digit still works for slots left on their default number key.
                    if (KeyDown(k) || (k == KeyCode.Alpha1 + i && KeyDown(KeyCode.Keypad1 + i)))
                    {
                        TeleportTo(TeleSpot[i], i == 1 ? new Vector3(0.75f, 0f, 0f) : Vector3.zero);
                        Toast(status == "Teleported." ? "Teleported: " + TeleName[i] : status);
                        break;
                    }
                }
            }
        }

        static bool KeyDown(KeyCode k)
        {
            try { return k != KeyCode.None && Input.GetKeyDown(k); } catch (Exception) { return false; }
        }

        void ApplyMaxStats()
        {
            ClassScript c = yan.Class;
            if (c == null) return;
            if (c.BiologyGrade != 5) { c.BiologyGrade = 5; ClassGlobals.BiologyGrade = 5; }
            if (c.ChemistryGrade != 5) { c.ChemistryGrade = 5; ClassGlobals.ChemistryGrade = 5; }
            if (c.LanguageGrade != 5) { c.LanguageGrade = 5; ClassGlobals.LanguageGrade = 5; }
            if (c.PhysicalGrade != 5 || yan.PhysicalGrade != 5) { c.PhysicalGrade = 5; ClassGlobals.PhysicalGrade = 5; yan.PhysicalGrade = 5; }
            if (c.PsychologyGrade != 5) { c.PsychologyGrade = 5; ClassGlobals.PsychologyGrade = 5; }
            if (c.Seduction != 5) { c.Seduction = 5; PlayerGlobals.Seduction = 5; }
            if (c.Numbness != 5) { c.Numbness = 5; PlayerGlobals.Numbness = 5; }
            if (c.Enlightenment != 5) { c.Enlightenment = 5; PlayerGlobals.Enlightenment = 5; }
        }

        void KeepAlphabetTools()
        {
            if (alphaScene != manager) { alphaScene = manager; alphaCache = null; }
            AlphabetScript al = GetAlphabet();
            if (al != null && al.AlphabetTools != null && !al.AlphabetTools.activeSelf) al.AlphabetTools.SetActive(true);
        }

        void KeepInfiniteObjects()
        {
            if (pickScene != manager)
            {
                pickScene = manager;
                batteries.Clear(); bagBoxes.Clear(); brokenAt.Clear(); batteryTag.Clear();
                foreach (PickUpScript pu in FindAllScene<PickUpScript>())
                {
                    if (pu.CarBattery) batteries.Add(pu);
                    if (pu.GarbageBagBox) bagBoxes.Add(pu);
                }
            }
            if (infBags)
                foreach (PickUpScript b in bagBoxes)
                    if (b != null && b.BodyBags < 50) b.BodyBags = 100;
            if (infBattery)
            {
                foreach (PickUpScript b in batteries)
                {
                    if (b == null) continue;
                    if (!b.Broken)
                    {
                        if (!batteryTag.ContainsKey(b) && b.gameObject.tag != "Untagged") batteryTag[b] = b.gameObject.tag;
                        continue;
                    }
                    float t;
                    if (!brokenAt.TryGetValue(b, out t)) { brokenAt[b] = Time.time; continue; }
                    if (Time.time - t < 3f) continue; // let the electrocution finish first
                    brokenAt.Remove(b);
                    b.Broken = false;
                    if (b.Smoke != null) b.Smoke.Stop();
                    string tag;
                    if (!batteryTag.TryGetValue(b, out tag)) tag = "E";
                    try { b.gameObject.tag = tag; } catch (Exception e) { Logger.LogWarning("Battery tag: " + e.Message); }
                }
            }
        }


        // ---------------- v0.8: gift shop + matchmaking ----------------

        // Runs right after every scene load, before the scene's own Start() code,
        // so the gift shop and the LoveManager read our values.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                if (autoGifts) GrantGifts();
                if (instantMatch) DatingGlobals.Affection = 100f;
            }
            catch (Exception e) { Logger.LogWarning("Scene-load extras: " + e.Message); }
            matchScene = null;
        }

        // Gift shop items: IDs 1-5 are Senpai gifts, 6-9 are matchmaking gifts (rival/suitor).
        void GrantGifts()
        {
            for (int id = 1; id <= 9; id++) CollectibleGlobals.SetGiftPurchased(id, true);
            if (CollectibleGlobals.SenpaiGifts < 5) CollectibleGlobals.SenpaiGifts = 5;
            if (CollectibleGlobals.MatchmakingGifts < 4) CollectibleGlobals.MatchmakingGifts = 4;
        }

        // 0 = none, 1 = instant matchmaking, 2 = instant rejection, 3 = instant expel. They're exclusive:
        // matchmaking ends the rival at the end of the day, before Friday's rejection could happen.
        // 1980s Custom Mode keeps one routine per student for all ten weeks, so the routines alone
        // can't get harder week by week. This adds watchers at the start of each school day the way
        // story mode's week 10 does (StudentManager.BecomeGuard): their free-time blocks become
        // "Guard", which sends them to whoever the current rival is. Week 1: none ... week 10: 6.
        static readonly HashSet<string> WatchFreeTime = new HashSet<string> { "Hangout", "Patrol", "LunchSpot" };

        void AddWeeklyWatchers()
        {
            StudentManagerScript m = manager;
            if (m == null || !m.CustomMode || !m.Eighties || m.Students == null) { watchScene = m; return; }
            int rid = m.RivalID;
            if (rid <= 0 || rid >= m.Students.Length || m.Students[rid] == null) return;   // try again next second
            watchScene = m;
            int week = Mathf.Clamp(m.Week, 1, 10);
            int want = Mathf.RoundToInt((week - 1) * 2f / 3f);
            if (want <= 0) return;

            List<StudentScript> cands = new List<StudentScript>();
            for (int i = 2; i < Mathf.Min(m.Students.Length, 90); i++)
            {
                if (i >= 11 && i <= 20) continue;
                StudentScript s = m.Students[i];
                if (s == null || s.Rival || s.Teacher || s.Grudge || s.Slave || s.ScheduleBlocks == null) continue;
                if (s.Club == ClubType.Council || s.Club == ClubType.Bully || s.Club == ClubType.Delinquent) continue;
                bool guarding = false, free = false;
                foreach (ScheduleBlock b in s.ScheduleBlocks)
                {
                    if (b == null) continue;
                    if (b.destination == "Guard") guarding = true;
                    if (b.time >= 7.5f && b.time <= 18f && WatchFreeTime.Contains(b.destination)) free = true;
                }
                if (!guarding && free) cands.Add(s);   // students the editor already made watchers are skipped
            }
            // Heroes, sleuths, strict and protective students first; the same picks all week.
            cands.Sort((a, b) =>
            {
                int pa = IsWatcherPersona(a.Persona) ? 0 : 1, pb = IsWatcherPersona(b.Persona) ? 0 : 1;
                if (pa != pb) return pa.CompareTo(pb);
                return ((a.StudentID * 37 + week * 11) % 101).CompareTo((b.StudentID * 37 + week * 11) % 101);
            });

            int added = 0;
            foreach (StudentScript s in cands)
            {
                if (added >= want) break;
                try
                {
                    foreach (ScheduleBlock b in s.ScheduleBlocks)
                        if (b != null && b.time >= 7.5f && b.time <= 18f && WatchFreeTime.Contains(b.destination))
                        { b.destination = "Guard"; b.action = "Guard"; }
                    s.GetDestinations();
                    added++;
                }
                catch (Exception e) { Logger.LogWarning("Rival watcher " + s.StudentID + " failed: " + e.Message); }
            }
            Logger.LogInfo("Week " + week + ": " + added + " extra students are watching the rival.");
            if (added > 0) Toast(added + " extra student" + (added == 1 ? " is" : "s are") + " watching the rival this week");
        }

        static bool IsWatcherPersona(PersonaType p)
        {
            return p == PersonaType.Heroic || p == PersonaType.Sleuth || p == PersonaType.Strict || p == PersonaType.Protective;
        }

        int CurrentElimination() { return instantMatch ? 1 : (instantReject ? 2 : (instantExpel ? 3 : 0)); }

        // The elimination key steps through the methods; the choice only applies after Enter.
        void HandleElimKeys()
        {
            if (KeyDown(elimKey.Value))
            {
                int from = elimPending < 0 ? CurrentElimination() : elimPending;
                elimPending = (from + 1) % ElimNames.Length;
            }
            if (elimPending < 0) return;
            if (KeyDown(KeyCode.Return) || KeyDown(KeyCode.KeypadEnter))
            {
                int pick = elimPending;
                elimPending = -1;
                SetElimination(pick);
                Toast("Elimination method: " + ElimNames[pick]);
            }
            else if (KeyDown(KeyCode.Backspace))
            {
                elimPending = -1;
                Toast("Kept: " + ElimNames[CurrentElimination()]);
            }
        }

        void SetElimination(int which)
        {
            bool wasMatch = instantMatch;
            instantMatch = which == 1;
            instantReject = which == 2;
            instantExpel = which == 3;
            matchScene = null;
            if (instantMatch) { ForceMatchmaking(); status = "Instant matchmaking on: watch the end of the day."; }
            else if (wasMatch) UndoMatchmaking();
            if (which == 2) status = "Instant rejection on (instant matchmaking and expel are off).";
            if (which == 3) status = "Instant expel on (instant matchmaking and rejection are off).";
            SaveSettings();
        }

        // Instant matchmaking saved full rival/suitor affection, and the game itself matchmakes the
        // rival whenever affection is full. Take it back down and cancel today's confession.
        void UndoMatchmaking()
        {
            try
            {
                if (DatingGlobals.Affection >= 100f) DatingGlobals.Affection = 0f;
                if (manager != null && manager.LoveManager != null) manager.LoveManager.ConfessToSuitor = false;
                Logger.LogInfo("Instant matchmaking undone: affection reset, today's confession to the suitor cancelled.");
            }
            catch (Exception e) { Logger.LogWarning("Undo matchmaking: " + e.Message); }
            undoMatchPending = false;
        }

        // Same as the debug menu's matchmaking keys: full affection, and tell the LoveManager
        // that the rival confesses to her suitor at the end of today (any weekday, either mode).
        void ForceMatchmaking()
        {
            try
            {
                DatingGlobals.Affection = 100f;
                if (manager != null && manager.LoveManager != null)
                {
                    manager.LoveManager.ConfessToSuitor = true;
                    Logger.LogInfo("Instant matchmaking: rival will confess to her suitor at the end of the day.");
                }
            }
            catch (Exception e) { Logger.LogWarning("Instant matchmaking: " + e.Message); }
        }


        // ---------------- v0.9: content checklist + instant disposal ----------------

        // The title screen's checklist reads one PlayerPrefs key per entry (1 = done). This is
        // the same list the game's own "obtain all challenges" function writes.
        void CompleteChecklist()
        {
            try
            {
                foreach (string k in ChecklistKeys) PlayerPrefs.SetInt(k, 1);
                PlayerPrefs.Save();
                // If the checklist screen is loaded right now, make it re-read the keys.
                TitleDemoChecklistScript cl = FindObjectOfType<TitleDemoChecklistScript>(true);
                if (cl != null)
                {
                    MethodInfo st = typeof(TitleDemoChecklistScript).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (st != null) st.Invoke(cl, null);
                }
                status = "Content Checklist completed (" + ChecklistKeys.Length + " entries).";
            }
            catch (Exception e)
            {
                Exception inner = e.InnerException ?? e;
                status = "Checklist: " + inner.Message;
                Logger.LogWarning("CompleteChecklist: " + inner);
            }
        }

        // Incinerator: once lit it burns for an hour (Timer 60) before the bodies are gone and it
        // can be reused. Kiln: after each corpse it cools down (BurnTimer 60). Both get cut to 0.
        void KeepDisposalReady()
        {
            if (disposalScene != manager)
            {
                disposalScene = manager;
                incinerators.Clear(); chippers.Clear();
                incinerators.AddRange(FindAllScene<IncineratorScript>());
                chippers.AddRange(FindAllScene<WoodChipperScript>());
            }
            for (int i = 0; i < incinerators.Count; i++)
            {
                IncineratorScript inc = incinerators[i];
                if (inc != null && inc.Timer > 0.01f) inc.Timer = 0.0001f; // its Update calls Finish() next frame
            }
            for (int i = 0; i < chippers.Count; i++)
            {
                WoodChipperScript wc = chippers[i];
                if (wc != null && wc.BurnTimer > 0f) wc.BurnTimer = 0f;
            }
        }


        // ---------------- Debug-menu style time keys ----------------

        void HandleTimeKeys()
        {
            if (open || StudentInfoShowing() != null) return;   // + and - change reputation there
            bool up = KeyDown(timeUpKey.Value) || KeyDown(KeyCode.KeypadPlus);
            bool reset = KeyDown(timeResetKey.Value) || KeyDown(KeyCode.KeypadMinus);
            if (up)
            {
                float cur = Mathf.Max(1f, hotkeyScale);
                hotkeyScale = cur < 5f ? cur + 1f : Mathf.Min(25f, cur + 5f);
                Toast("Time: " + hotkeyScale.ToString("0") + "x");
            }
            if (reset && hotkeyScale > 1.001f)
            {
                hotkeyScale = 1f;
                Time.timeScale = gameSpeedOn ? gameSpeed : 1f;
                Toast("Time: normal");
            }
        }


        // ---------------- v0.11: Nemesis in Yandere vision ----------------
        // Mission Mode marks the target by giving its outlines a bright red colour, which shows in
        // Yandere vision. Same treatment for the student NemesisScript drives, in purple, only while
        // Yandere vision is on; the original colours come back when it's turned off.

        void HandleNemesisVision()
        {
            bool want = nemesisVision && yan != null && manager != null && yan.YandereVision;
            if (!want) { if (nemesisLit) UnlightNemesis(); return; }

            if (nemesisScene != manager)
            {
                nemesisScene = manager; nemesisCache = null; nemesisSearched = false;
                nemesisOrig.Clear(); nemesisLit = false;
            }
            nemesisTimer -= Time.unscaledDeltaTime;
            if (nemesisTimer > 0f) return;
            nemesisTimer = 0.5f;

            if (nemesisCache == null)
            {
                if (nemesisSearched && Time.timeSinceLevelLoad > 10f) return; // not a Nemesis mission
                nemesisCache = FindObjectOfType<NemesisScript>();
                nemesisSearched = true;
                if (nemesisCache == null) return;
            }
            StudentScript s = nemesisCache.Student;
            if (s == null || s.Outlines == null) return;
            foreach (OutlineScript o in s.Outlines)
            {
                if (o == null) continue;
                if (!nemesisOrig.ContainsKey(o)) nemesisOrig[o] = o.color;
                o.color = NemesisColor;
                o.enabled = true;
                SetHighlight(o, NemesisColor);
            }
            nemesisLit = true;
        }

        void UnlightNemesis()
        {
            foreach (KeyValuePair<OutlineScript, Color> kv in nemesisOrig)
            {
                if (kv.Key == null) continue;
                kv.Key.color = kv.Value;
                SetHighlight(kv.Key, kv.Value);
            }
            nemesisOrig.Clear();
            nemesisLit = false;
        }

        // OutlineScript.h is a Highlighter from another game assembly, so it's reached by reflection.
        static void SetHighlight(OutlineScript o, Color c)
        {
            try
            {
                object h = OutlineHighlighter != null ? OutlineHighlighter.GetValue(o) : null;
                if (h == null) return;
                Behaviour b = h as Behaviour;
                if (b != null) b.enabled = true;
                MethodInfo m = h.GetType().GetMethod("ConstantOnImmediate", new Type[] { typeof(Color) });
                if (m != null) m.Invoke(h, new object[] { c });
            }
            catch (Exception) { }
        }

        // ---------------- v0.11: locker note helper ----------------
        // Rules copied from NoteWindowScript.Update (which decides NoteLocker.Success).
        // Subjects: 1 Making Friends, 2 Low Grades, 3 Fighting Evil, 4 Suspicious Activity,
        // 5 Your Friends, 6 Social Media, 7 Bullying, 8 The Supernatural, 9 Compensated Dating,
        // 10 Domestic Abuse.
        // Personas: 1 Loner, 2 Teacher's Pet, 3 Heroic, 4 Coward, 5 Evil, 6 Social Butterfly,
        // 8 Dangerous, 10 Phone Addict, 12 Spiteful, 13 Sleuth, 15 Protective, 16 Violent, 17 Landline User.
        // Clubs: 3 Occult, 12 Student Council, 13 Bully.

        List<int> GoodNoteSubjects(StudentScript st, out string extra)
        {
            List<int> ok = new List<int>();
            extra = "";
            int id = st.StudentID, p = (int)st.Persona, club = (int)st.Club;
            if (!manager.Eighties)
            {
                if (id == 30) ok.Add(9);
                if (id == 5 && manager.MissionMode) for (int i = 1; i <= 10; i++) ok.Add(i);
                if (id == 2 || id == 3 || club == 3) ok.Add(8);
            }
            if (st.Bullied) ok.Add(7);

            bool rival = id == manager.RivalID || (manager.MissionMode && id > 10 && id < 21);
            if (rival)
            {
                ok.Add(10);
                extra = "Rival: a 5 PM meeting on Friday always fails.";
                return Distinct(ok);
            }

            if (p == 1) ok.Add(1);
            if (p == 2) ok.Add(2);
            if (p == 3 || p == 15) ok.Add(3);
            if (p == 4) { ok.Add(1); ok.Add(4); return Distinct(ok); }
            if (p == 6) ok.Add(5);
            if (p == 10) ok.Add(6);
            if (club == 13) { ok.Add(5); ok.Add(9); return Distinct(ok); }
            if (p == 13) { ok.Add(1); ok.Add(3); ok.Add(4); ok.Add(5); return Distinct(ok); }
            if (p == 12 || p == 5) { ok.Add(7); return Distinct(ok); }
            if (p == 16) { ok.Add(7); return Distinct(ok); }
            if (p == 17 || club == 12 || p == 8) { ok.Add(7); ok.Add(3); ok.Add(4); }
            return Distinct(ok);
        }

        static List<int> Distinct(List<int> l)
        {
            List<int> o = new List<int>();
            foreach (int i in l) if (!o.Contains(i)) o.Add(i);
            o.Sort();
            return o;
        }

        static readonly string[] NoteSubjectNames =
        {
            "", "Making Friends", "Low Grades", "Fighting Evil", "Suspicious Activity", "Your Friends",
            "Social Media", "Bullying", "The Supernatural", "Compensated Dating", "Domestic Abuse"
        };

        string NoteSubjectName(int i)
        {
            if (noteWin != null && noteWin.Subjects != null && i < noteWin.Subjects.Length && !string.IsNullOrEmpty(noteWin.Subjects[i]))
                return noteWin.Subjects[i];
            return i >= 1 && i < NoteSubjectNames.Length ? NoteSubjectNames[i] : "Subject " + i;
        }

        void UpdateNoteHelper()
        {
            bool active = false;
            if (noteHelper && manager != null)
            {
                if (noteScene != manager) { noteScene = manager; noteWin = null; noteSearched = false; noteTinted.Clear(); }
                if (noteWin == null && !noteSearched) { noteWin = FindObjectOfType<NoteWindowScript>(true); noteSearched = true; }
                if (noteWin != null && noteWin.Show && noteWin.Selecting && noteWin.Slot == 1 && !noteWin.LifeNote &&
                    noteWin.NoteLocker != null && noteWin.NoteLocker.Student != null)
                {
                    active = true;
                    noteGood = GoodNoteSubjects(noteWin.NoteLocker.Student, out noteExtra);
                }
            }

            if (active && noteWin.SubLabels != null)
            {
                for (int i = 1; i < noteWin.SubLabels.Length; i++)
                {
                    UILabel l = noteWin.SubLabels[i];
                    if (l == null) continue;
                    Color cur = l.color;
                    if (noteGood.Contains(i))
                    {
                        if (!noteTinted.ContainsKey(l)) noteTinted[l] = cur;
                        l.color = new Color(0.35f, 1f, 0.35f, cur.a);
                    }
                    else if (noteTinted.ContainsKey(l))
                    {
                        Color o = noteTinted[l];
                        l.color = new Color(o.r, o.g, o.b, cur.a);
                        noteTinted.Remove(l);
                    }
                }
            }
            else if (noteTinted.Count > 0)
            {
                foreach (KeyValuePair<UILabel, Color> kv in noteTinted)
                    if (kv.Key != null) kv.Key.color = new Color(kv.Value.r, kv.Value.g, kv.Value.b, kv.Key.color.a);
                noteTinted.Clear();
            }
            noteActive = active;
        }

        void DrawNoteHelper()
        {
            if (noteWin == null || noteWin.NoteLocker == null || noteWin.NoteLocker.Student == null) return;
            StudentScript st = noteWin.NoteLocker.Student;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("Note for ").Append(st.Name).Append("  (").Append(Pretty(st.Persona.ToString())).Append(")\n");
            if (noteGood.Count == 0) sb.Append("No subject will work for this student.");
            else
            {
                sb.Append("Will work (green): ");
                for (int i = 0; i < noteGood.Count; i++) { if (i > 0) sb.Append(", "); sb.Append(NoteSubjectName(noteGood[i])); }
            }
            if (noteExtra.Length > 0) sb.Append("\n").Append(noteExtra);
            GUIStyle style = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            style.normal.textColor = noteGood.Count > 0 ? new Color(0.55f, 1f, 0.55f) : new Color(1f, 0.6f, 0.6f);
            float w = Mathf.Min(620f, Screen.width - 40f);
            GUI.Box(new Rect((Screen.width - w) / 2f, 20f, w, noteExtra.Length > 0 ? 78f : 56f), sb.ToString(), style);
        }

        void DrawHud()
        {
            if (yan == null || manager == null) return;
            if (hudStyle == null)
            {
                hudStyle = new GUIStyle(GUI.skin.box);
                hudStyle.alignment = TextAnchor.MiddleLeft;
                hudStyle.fontSize = 14;
                hudStyle.normal.textColor = Color.white;
                hudStyle.padding = new RectOffset(8, 8, 4, 4);
            }
            int y = 10;
            if (bombHud)
            {
                InventoryScript inv = yan.Inventory;
                int count = 0;
                if (inv != null)
                {
                    if (bombSel == 0) count = smokeManaged ? smokeCount : (inv.SmokeBomb ? 1 : 0);
                    else if (bombSel == 1) count = stinkManaged ? stinkCount : (inv.StinkBomb ? 1 : 0);
                    else count = amnesiaManaged ? amnesiaCount : (inv.AmnesiaBomb ? 1 : 0);
                }
                string txt = "Bomb: " + BombNames[bombSel] + "  x" + count + "   [" + bombCycleKey.Value + " switch / " + throwKey.Value + " throw]";
                GUI.Box(new Rect(Screen.width - 330, y, 320, 26), txt, hudStyle);
                y += 30;
            }
            if (nemesisLit && nemesisCache != null && nemesisCache.Student != null && yan != null)
            {
                float dist = Vector3.Distance(yan.transform.position, nemesisCache.Student.transform.position);
                GUI.Box(new Rect(Screen.width - 330, y, 320, 26), "Nemesis: " + dist.ToString("0") + " m away (purple outline)", hudStyle);
                y += 30;
            }
            if (elimPending >= 0)
            {
                GUIStyle ask = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                ask.normal.textColor = Color.white;
                float aw = Mathf.Min(560f, Screen.width - 40f);
                string msg = "Rival elimination: " + ElimNames[elimPending] +
                             "\n(now: " + ElimNames[CurrentElimination()] + ")" +
                             "\n\nAre you sure you want this method?" +
                             "\n[Enter] Yes     [Backspace] No     [" + elimKey.Value + "] Next option";
                GUI.Box(new Rect((Screen.width - aw) / 2f, Screen.height * 0.3f, aw, 130f), msg, ask);
            }
            if (hotkeyScale > 1.001f)
            {
                GUI.Box(new Rect(Screen.width - 330, y, 320, 26), "Time: " + hotkeyScale.ToString("0") + "x   [" + timeResetKey.Value + " = normal]", hudStyle);
                y += 30;
            }
            if (Time.unscaledTime < toastUntil && toast.Length > 0)
                GUI.Box(new Rect(Screen.width - 330, y, 320, 26), toast, hudStyle);
        }

        void DrawWeaponsTab()
        {
            GUILayout.Label("=== WEAPONS ===");
            if (yan == null) { GUILayout.Label("Player not found."); return; }

            if (weaponManager == null || weaponManager.Weapons == null)
            {
                GUILayout.Label("Weapon list not found.");
            }
            else
            {
                if (GUILayout.Button("Bring ALL weapons to me"))
                {
                    int n = 0;
                    foreach (WeaponScript w in weaponManager.Weapons)
                    {
                        if (w == null) continue;
                        BringObject(w.gameObject, FrontOfPlayer(1.5f, 0.5f + 0.1f * n));
                        n++;
                    }
                    status = "Moved " + n + " weapons to you.";
                }

                listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Height(220));
                foreach (WeaponScript w in weaponManager.Weapons)
                {
                    if (w == null) continue;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("[" + w.WeaponID + "] " + w.Name + " (" + w.Type + ")", GUILayout.Width(240));
                    if (GUILayout.Button("Bring")) BringWeapon(w);
                    if (GUILayout.Button("Duplicate")) DuplicateWeapon(w);
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
                GUILayout.Label("Duplicates are best-effort copies and are not tracked by the game's weapon list.");
            }

            GUILayout.Space(10);
            GUILayout.Label("=== MURDER METHODS ===");

            if (GUILayout.Button("Bring electrocution kit (wrench, screwdriver, power strip, snacks)  [" + elecKey.Value + "]")) ElectrocutionKit();
            if (GUILayout.Button("Tripwire kit: string + masking tape, bring a knife, the water cooler and a bucket  [" + tripKey.Value + "]")) TripwireKit();
            if (GUILayout.Button("Dismemberment kit: circular saw and tarp  [" + dismemberKey.Value + "]")) DismembermentKit();
            instantWrap = GUILayout.Toggle(instantWrap, "Instant body bag wrapping");
            if (GUILayout.Button("Cover-up kit: raincoat, occult robe and gloves  [" + coverKey.Value + "]")) CoverUpKit();

            GUILayout.Space(6);
            GUILayout.Label("=== SPEED UP ACTIONS ===");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Speed: " + attackSpeed.ToString("0.#") + "x", GUILayout.Width(90));
            attackSpeed = Mathf.Round(GUILayout.HorizontalSlider(attackSpeed, 1f, 10f) * 2f) / 2f;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            fastAttack = GUILayout.Toggle(fastAttack, "Attack kills", GUILayout.Width(150));
            fastDismember = GUILayout.Toggle(fastDismember, "Dismembering", GUILayout.Width(150));
            fastWash = GUILayout.Toggle(fastWash, "Washing weapons", GUILayout.Width(150));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            fastShower = GUILayout.Toggle(fastShower, "Showering", GUILayout.Width(150));
            fastDispose = GUILayout.Toggle(fastDispose, "Corpse disposal (incinerator, dumpster, wood chipper, burying)");
            GUILayout.EndHorizontal();
            GUILayout.Label("The whole game runs at that speed only while the action plays, then goes back to normal. Hold " + fastForwardKey.Value +
                            " to fast-forward anything else, like cutscenes (voiced lines keep their normal speed, so some scenes still wait for the voice).");

            if (GUILayout.Button("Teleport blood-clean kit to me (mop, bucket, bleach, body bag box)  [" + cleanupKey.Value + "]")) CleanupKit();
            instantDisposal = GUILayout.Toggle(instantDisposal, "No time limit on disposal: incinerator, kiln and washing machine finish instantly");
            if (instantDisposal) GUILayout.Label("The washing machine also takes any clothing you hold, not only bloody or painted clothes.");
            DrawBucketSection();

            if (GUILayout.Button("Put gasoline in the watering can"))
            {
                if (manager != null)
                {
                    manager.GasInWateringCan = true;
                    status = "Watering can now holds gasoline.";
                }
            }

            GUILayout.Space(6);
            GUILayout.Label("--- Traps ---");
            if (GUILayout.Button("Allow bucket traps on every door")) AllowTrapsOnAllDoors();
            if (GUILayout.Button("Give string (tripwire trap)") && yan != null && yan.Inventory != null)
            {
                yan.Inventory.String = true;
                invDirty = true;
                status = "String added.";
            }

            if (GUILayout.Button("Bring all poison bottles to me"))
            {
                PoisonBottleScript[] bottles = FindObjectsOfType<PoisonBottleScript>();
                int n = 0;
                foreach (PoisonBottleScript p in bottles)
                {
                    if (p == null) continue;
                    BringObject(p.gameObject, FrontOfPlayer(1.2f, 0.5f) + yan.transform.right * (0.3f * n));
                    n++;
                }
                status = "Moved " + n + " poison bottles to you.";
            }
        }

        void BringWeapon(WeaponScript w)
        {
            BringObject(w.gameObject, FrontOfPlayer(1.5f, 0.5f));
            status = "Moved " + w.Name + " to you.";
        }

        void DuplicateWeapon(WeaponScript w)
        {
            GameObject clone = Instantiate(w.gameObject, FrontOfPlayer(1.5f, 0.6f), w.transform.rotation);
            clone.name = w.gameObject.name + " (copy)";
            clone.SetActive(true);
            status = "Duplicated " + w.Name + ".";
        }

        // ---------------- Students tab ----------------

        void DrawStudentsTab()
        {
            GUILayout.Label("=== UNLOCKS ===");
            if (manager == null)
            {
                GUILayout.Label("Student manager not found (not in the school scene).");
                return;
            }

            if (GUILayout.Button("Befriend everyone")) BefriendEveryone();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Discover all tasks")) SetAllTasks(1, true);
            if (GUILayout.Button("Complete all tasks")) SetAllTasks(3, false);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Fill Student Info screen")) FillStudentInfo();
            if (GUILayout.Button("Learn everyone's likes and dislikes")) LearnAllOpinions();
            GUILayout.Label("Always do these automatically (every school load):");
            bool b1 = GUILayout.Toggle(autoBefriend, "Befriend everyone");
            bool b2 = GUILayout.Toggle(autoTasks, "Complete everyone's tasks");
            bool b3 = GUILayout.Toggle(autoInfo, "Fill the Student Info screen");
            bool b4 = GUILayout.Toggle(autoLikes, "Learn everyone's likes and dislikes");
            if (b1 != autoBefriend || b2 != autoTasks || b3 != autoInfo || b4 != autoLikes)
            {
                bool turnedOn = (b1 && !autoBefriend) || (b2 && !autoTasks) || (b3 && !autoInfo) || (b4 && !autoLikes);
                autoBefriend = b1; autoTasks = b2; autoInfo = b3; autoLikes = b4;
                if (turnedOn) autoScene = null; // run now
            }

            GUILayout.Space(6);
            GUILayout.Label("=== GIFT SHOP & MATCHMAKING ===");
            bool g = GUILayout.Toggle(autoGifts, "Own everything in the gift shop (re-given every time a scene loads)");
            if (g != autoGifts) { autoGifts = g; if (g) { GrantGifts(); status = "All gift-shop gifts owned."; } }
            bool m = GUILayout.Toggle(instantMatch, "Instant matchmaking: rival confesses to her suitor at the end of the day (202X + 1980s)");
            if (m != instantMatch) SetElimination(m ? 1 : 0);
            if (instantMatch) GUILayout.Label("Affection is maxed. The rival is matchmade when the day ends, even on Monday. (Turns off instant rejection and instant expel.)");
            GUILayout.Space(6);
            GUILayout.Label("=== 1980s CUSTOM MODE ===");
            alwaysYakuza = GUILayout.Toggle(alwaysYakuza, "Always unlock the yakuza, with every service (1980s)");
            voidGoddess = GUILayout.Toggle(voidGoddess, "Always have the Void Goddess at school (\"Summon An Ancient Evil\")");
            rivalSecrets = GUILayout.Toggle(rivalSecrets, "Always know the rival's embarrassing secret (gossip) and dark secret (locker note)");
            weeklyWatchers = GUILayout.Toggle(weeklyWatchers, "More students watch the rival each week (week 1: none, week 10: 6 extra)");
            showHelper = GUILayout.Toggle(showHelper, "Show the topic helper while talking to students");
            noteHelper = GUILayout.Toggle(noteHelper, "Show which subjects work when leaving a note in a locker");
            GUILayout.Label("Close and reopen the phone screens after using these.");

            GUILayout.Space(8);
            GUILayout.Label("=== STUDENTS ===");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter:", GUILayout.Width(45));
            filter = GUILayout.TextField(filter);
            GUILayout.EndHorizontal();

            if (clubValues == null) clubValues = (ClubType[])Enum.GetValues(typeof(ClubType));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Club:", GUILayout.Width(45));
            if (GUILayout.Button("<", GUILayout.Width(30))) clubFilter = clubFilter <= -1 ? clubValues.Length - 1 : clubFilter - 1;
            GUILayout.Label(clubFilter < 0 ? "All clubs" : ClubLabel(clubValues[clubFilter]), GUILayout.Width(150));
            if (GUILayout.Button(">", GUILayout.Width(30))) clubFilter = clubFilter >= clubValues.Length - 1 ? -1 : clubFilter + 1;
            GUILayout.EndHorizontal();

            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Height(180));
            if (manager.Students == null)
            {
                GUILayout.Label("No student list.");
            }
            else
            {
                for (int i = 0; i < manager.Students.Length; i++)
                {
                    StudentScript s = manager.Students[i];
                    if (s == null) continue;
                    if (clubFilter >= 0 && s.Club != clubValues[clubFilter]) continue;
                    string label = "[" + s.StudentID + "] " + s.Name + "  -  " + ClubLabel(s.Club);
                    if (filter.Length > 0 &&
                        label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (GUILayout.Button(label, selected == s ? selStyle : GUI.skin.button)) selected = s;
                }
            }
            GUILayout.EndScrollView();

            bool hasSel = selected != null;
            GUILayout.Label(hasSel
                ? "Selected: [" + selected.StudentID + "] " + selected.Name + "  -  " + ClubLabel(selected.Club) +
                  "  -  " + selected.Persona + "  HP " + selected.Health.ToString("0")
                : "Selected: none");

            GUI.enabled = hasSel && yan != null;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Bring to me")) BringStudent();
            if (GUILayout.Button("Go to them")) GoToStudent();
            GUILayout.EndHorizontal();
            GUI.enabled = true;
        }

        void BefriendEveryone()
        {
            if (manager == null || manager.Students == null) return;
            int max = manager.Students.Length;
            int last = manager.StudentsTotal > 1 ? Mathf.Min(manager.StudentsTotal, max - 1) : max - 1;
            int count = 0;
            for (int i = 2; i <= last; i++)
            {
                StudentScript s = manager.Students[i];
                if (s == null) continue;
                StudentGlobals.SetStudentFriendship(i, 100);
                PlayerGlobals.SetStudentFriend(i, true);
                StudentGlobals.SetStudentPhotographed(i, true);
                if (manager.StudentPhotographed != null && i < manager.StudentPhotographed.Length)
                    manager.StudentPhotographed[i] = true;
                s.Friend = true;
                count++;
            }
            PlayerGlobals.Friends = count;
            if (yan != null) yan.Friends = count;
            status = "Befriended " + count + " students.";
        }

        void SetAllTasks(int value, bool onlyIfUnstarted)
        {
            TaskManagerScript tm = manager != null ? manager.TaskManager : null;
            if (tm == null) tm = FindObjectOfType<TaskManagerScript>();
            if (tm == null || tm.TaskStatus == null) { status = "Task manager not found."; return; }

            int n = 0;
            for (int i = 1; i < tm.TaskStatus.Length; i++)
            {
                if (onlyIfUnstarted && tm.TaskStatus[i] != 0) continue;
                tm.TaskStatus[i] = value;
                TaskGlobals.SetTaskStatus(i, value);
                n++;
            }
            status = (value == 3 ? "Completed " : "Discovered ") + n + " task entries.";
        }

        void FillStudentInfo()
        {
            int n = 0;
            if (manager.StudentPhotographed != null)
            {
                for (int i = 1; i < manager.StudentPhotographed.Length; i++)
                {
                    manager.StudentPhotographed[i] = true;
                    StudentGlobals.SetStudentPhotographed(i, true);
                    n++;
                }
            }
            for (int t = 1; t <= 25; t++) ConversationGlobals.SetTopicDiscovered(t, true);

            try
            {
                StudentInfoMenuScript menu = FindObjectOfType<StudentInfoMenuScript>(true);
                if (menu != null)
                {
                    menu.GrabbedPortraits = false;
                    if (menu.PortraitLoaded != null)
                        for (int i = 0; i < menu.PortraitLoaded.Length; i++) menu.PortraitLoaded[i] = false;
                }
            }
            catch (Exception e) { Logger.LogWarning("Portrait refresh failed: " + e.Message); }

            status = "Unlocked info for " + n + " students and all topics.";
        }

        void BringStudent()
        {
            if (selected == null || yan == null) return;
            if (!selected.gameObject.activeInHierarchy) { status = "That student isn't on campus right now."; return; }
            MoveStudent(selected, yan.transform.position + yan.transform.forward * 1.5f);
            status = "Moved " + selected.Name + " next to you.";
        }

        // Students are driven by a CharacterController and an A* path follower, both of which
        // can undo a plain position change. Turn the controller off, teleport the path
        // follower, then sync physics.
        void MoveStudent(StudentScript s, Vector3 pos)
        {
            CharacterController cc = s.MyController;
            bool wasOn = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;

            s.transform.position = pos;

            try
            {
                FieldInfo pf = typeof(StudentScript).GetField("Pathfinding", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object ai = pf != null ? pf.GetValue(s) : null;
                if (ai != null)
                {
                    MethodInfo tp = ai.GetType().GetMethod("Teleport", new Type[] { typeof(Vector3), typeof(bool) });
                    if (tp != null) tp.Invoke(ai, new object[] { pos, true });
                }
            }
            catch (Exception e) { Logger.LogWarning("Path teleport failed: " + e.Message); }

            if (cc != null) cc.enabled = wasOn;
            Physics.SyncTransforms();
        }

        void GoToStudent()
        {
            if (selected == null || yan == null) return;
            CharacterController cc = yan.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            yan.transform.position = selected.transform.position - selected.transform.forward * 1.5f;
            if (cc != null) cc.enabled = true;
            status = "Teleported to student.";
        }


        // ---------------- Bombs: throwing in normal mode ----------------
        // The challenge's own throw code only runs in Alphabet Challenge mode, so this
        // does the same thing (spawn the bomb prefab at your feet, use up the flag).

        void HandleBombKey()
        {
            if (open || yan == null || !yan.CanMove) return;
            bool pressed = false;
            try { pressed = Input.GetKeyDown(throwKey.Value); }
            catch (Exception) { }
            if (pressed) { ThrowBombs(bombSel == 0, bombSel == 1, bombSel == 2, true); Toast(status); }
        }

        void ThrowBombs(bool smoke, bool stink, bool amnesia, bool fromKey)
        {
            if (yan == null || yan.Inventory == null) { status = "Player not found."; return; }
            AlphabetScript al = GetAlphabet();
            if (al == null) { status = "Bomb prefabs not found in this scene."; return; }
            if (fromKey && al.isActiveAndEnabled) return; // the challenge's own script already handles the key

            InventoryScript inv = yan.Inventory;
            int thrown = 0;
            if (smoke && inv.SmokeBomb && al.SmokeBomb != null)
            {
                Instantiate(al.SmokeBomb, yan.transform.position, Quaternion.identity);
                inv.SmokeBomb = false; thrown++;
            }
            if (stink && inv.StinkBomb && al.StinkBomb != null)
            {
                Instantiate(al.StinkBomb, yan.transform.position, Quaternion.identity);
                inv.StinkBomb = false; thrown++;
            }
            if (amnesia && inv.AmnesiaBomb && al.AmnesiaBomb != null)
            {
                Instantiate(al.AmnesiaBomb, yan.transform.position, Quaternion.identity);
                inv.AmnesiaBomb = false; thrown++;
            }
            status = thrown > 0 ? "Threw " + thrown + " bomb(s)." : "You are not holding that bomb.";
        }

        // ---------------- Debug tab (the game's debug commands, no debug menu needed) ----------------

        DebugMenuScript dmCache;
        float atmosphere = 0.5f;
        bool confirmDestructive;

        DebugMenuScript GetDM()
        {
            if (dmCache == null) dmCache = FindObjectOfType<DebugMenuScript>(true);
            return dmCache;
        }

        void ReloadDay()
        {
            if (open) Toggle();
            Time.timeScale = 1f;
            SceneManager.LoadScene("LoadingScene");
        }

        void TeleportTo(int idx, Vector3 offset)
        {
            DebugMenuScript dm = GetDM();
            if (yan == null || dm == null || dm.TeleportSpot == null ||
                idx >= dm.TeleportSpot.Length || dm.TeleportSpot[idx] == null)
            {
                status = "Teleport spot not available in this scene.";
                return;
            }
            if (idx == 1) yan.DebugTimer = 1f;
            yan.transform.position = dm.TeleportSpot[idx].position + offset;
            if (yan.Followers > 0 && yan.Follower != null) yan.Follower.transform.position = yan.transform.position;
            Physics.SyncTransforms();
            status = "Teleported.";
        }

        static string ClockText(float minutes)
        {
            int m = Mathf.FloorToInt(minutes);
            int h = (m / 60) % 24, mm = m % 60;
            return (h % 12 == 0 ? 12 : h % 12) + ":" + mm.ToString("00") + (h < 12 ? " AM" : " PM");
        }

        // Every scene in the game's build, by name. Loading one directly skips whatever normally
        // leads into it, so the game may have nothing set up for it (wrong day, empty save data).
        void DrawScenesTab()
        {
            if (sceneNames == null) DrawScenesTabCache();
            GUILayout.Label("=== SCENES === (" + sceneNames.Length + " in this build, current: " + SceneManager.GetActiveScene().name + ")");
            GUILayout.Label("Loading a scene jumps straight there. Save first: scenes the game normally reaches another way may start with odd data or get stuck.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("School (loading screen)")) LoadSceneByName("LoadingScene");
            if (GUILayout.Button("Home")) LoadSceneByName("HomeScene");
            if (GUILayout.Button("Title")) LoadSceneByName("NewTitleScene");
            if (GUILayout.Button("Calendar")) LoadSceneByName("CalendarScene");
            if (GUILayout.Button("Reload this scene")) LoadSceneByName(SceneManager.GetActiveScene().name);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(55));
            sceneFilter = GUILayout.TextField(sceneFilter ?? "", GUILayout.Width(250));
            if (GUILayout.Button("Clear", GUILayout.Width(60))) sceneFilter = "";
            GUILayout.EndHorizontal();
            int col = 0;
            GUILayout.BeginHorizontal();
            for (int i = 0; i < sceneNames.Length; i++)
            {
                string nm = sceneNames[i];
                if (string.IsNullOrEmpty(nm)) continue;
                if (sceneFilter.Length > 0 && nm.IndexOf(sceneFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (GUILayout.Button(i + "  " + nm, GUILayout.Width(230))) LoadSceneByIndex(i, nm);
                if (++col % 3 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            }
            GUILayout.EndHorizontal();
        }

        void LoadSceneByName(string name)
        {
            if (sceneNames == null) DrawScenesTabCache();
            int idx = sceneNames == null ? -1 : Array.FindIndex(sceneNames, x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) { status = "Scene \"" + name + "\" isn't in this build."; return; }
            LoadSceneByIndex(idx, sceneNames[idx]);
        }

        void DrawScenesTabCache()
        {
            int n = SceneManager.sceneCountInBuildSettings;
            sceneNames = new string[n];
            for (int i = 0; i < n; i++) sceneNames[i] = System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
        }

        void LoadSceneByIndex(int index, string name)
        {
            if (open) Toggle();
            Time.timeScale = 1f;
            hotkeyScale = 1f;
            Logger.LogInfo("Loading scene " + index + " (" + name + ") from the menu.");
            SceneManager.LoadScene(index);
        }

        void SetClock(float minutes)
        {
            if (clock == null) { status = "Clock not found."; return; }
            clock.PresentTime = minutes;
            clock.HourTime = minutes / 60f;
        }

        void DrawDebugTab()
        {
            GUILayout.Label("=== TELEPORTS ===");
            if (yan == null) { GUILayout.Label("Player not found (open this inside the school scene)."); }
            else
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("1  School front")) TeleportTo(1, Vector3.zero);
                if (GUILayout.Button("2  Classroom 2-1")) TeleportTo(2, new Vector3(0.75f, 0f, 0f));
                if (GUILayout.Button("3  Computer lab")) TeleportTo(3, Vector3.zero);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("4  Rooftop")) TeleportTo(4, Vector3.zero);
                if (GUILayout.Button("5  Shower room")) TeleportTo(11, Vector3.zero);
                if (GUILayout.Button("6  Martial Arts Club")) TeleportTo(5, Vector3.zero);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("7  Occult Club")) TeleportTo(7, Vector3.zero);
                if (GUILayout.Button("8  Gardening Club")) TeleportTo(8, Vector3.zero);
                if (GUILayout.Button("9  Light Music + weapons, 7:05"))
                {
                    DebugMenuScript dm = GetDM();
                    if (dm != null && dm.Turtle != null) dm.Turtle.SpawnWeapons();
                    TeleportTo(6, Vector3.zero);
                    SetClock(425f);
                }
                GUILayout.EndHorizontal();
            }

            teleKeys = GUILayout.Toggle(teleKeys, "Number keys 1-9 teleport me (menu closed)");
            teleAlt = GUILayout.Toggle(teleAlt, "Only while holding " + teleModKey.Value + " (recommended: number keys alone also drop / switch items)");
            if (!teleAlt) GUILayout.Label("Without the modifier, number keys only teleport when your hands are empty.");

            GUILayout.Space(8);
            GUILayout.Label("=== DAY, UNIFORM, WEEK (these reload the day) ===");
            GUILayout.BeginHorizontal();
            string[] days = { "Mon", "Tue", "Wed", "Thu", "Fri" };
            for (int d = 1; d <= 5; d++)
                if (GUILayout.Button(days[d - 1])) { DateGlobals.Weekday = (DayOfWeek)d; ReloadDay(); }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            for (int u = 1; u <= 6; u++)
                if (GUILayout.Button("Uniform " + u))
                {
                    StudentGlobals.FemaleUniform = u;
                    StudentGlobals.MaleUniform = u;
                    ReloadDay();
                }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Advance week"))
            {
                int w = DateGlobals.Week + 1;
                if (!GameGlobals.Eighties && w > 2) w = 1;
                DateGlobals.Week = w;
                ReloadDay();
            }
            if (GUILayout.Button("Restart day")) ReloadDay();
            GUILayout.EndHorizontal();

            GUILayout.Label("School atmosphere: " + atmosphere.ToString("0.00") + " (0 = bad, 1 = good)");
            atmosphere = GUILayout.HorizontalSlider(atmosphere, 0f, 1f);
            if (GUILayout.Button("Apply atmosphere and reload day"))
            {
                SchoolGlobals.SchoolAtmosphere = atmosphere;
                SchoolGlobals.PreviousSchoolAtmosphere = atmosphere;
                ReloadDay();
            }

            GUILayout.Space(8);
            GUILayout.Label("=== TIME ===");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("End day (5:59 PM)")) { Time.timeScale = 1f; SetClock(1079f); }
            if (GUILayout.Button("+10 minutes") && clock != null) clock.PresentTime += 10f;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Set the clock to:", GUILayout.Width(110));
            setHour = Mathf.RoundToInt(GUILayout.HorizontalSlider(setHour, 6f, 18f, GUILayout.Width(150)));
            setMinute = Mathf.RoundToInt(GUILayout.HorizontalSlider(setMinute, 0f, 59f, GUILayout.Width(150)));
            GUILayout.Label(ClockText(setHour * 60 + setMinute), GUILayout.Width(70));
            if (GUILayout.Button("Set", GUILayout.Width(60))) { Time.timeScale = 1f; SetClock(setHour * 60 + setMinute); status = "Clock set to " + ClockText(setHour * 60 + setMinute) + "."; }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (KeyValuePair<string, int> t in new[] {
                new KeyValuePair<string, int>("Before class 7:05", 425), new KeyValuePair<string, int>("Class 8:30", 510),
                new KeyValuePair<string, int>("Lunch 1:00", 780), new KeyValuePair<string, int>("Class 1:30", 810),
                new KeyValuePair<string, int>("Cleaning 3:30", 930), new KeyValuePair<string, int>("After school 4:00", 960) })
                if (GUILayout.Button(t.Key)) { Time.timeScale = 1f; SetClock(t.Value); status = "Clock set to " + ClockText(t.Value) + "."; }
            GUILayout.EndHorizontal();
            GUILayout.Label(clock != null ? "Now: " + ClockText(clock.PresentTime) + ". Going forward is safest; going back in time doesn't undo what already happened today." : "The clock only exists at school.");
            GUILayout.Label("Freeze and speed controls are on the Extras tab.");

            GUILayout.Space(8);
            GUILayout.Label("=== PROGRESS ===");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Max Seduction + Strength")) MaxSeductionStrength();
            if (GUILayout.Button("Add " + infoAmount + " info points  [" + infoKey.Value + "]")) AddInfoPoints(infoAmount);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reputation high")) SetReputation(100f);
            if (GUILayout.Button("Reputation neutral")) SetReputation(0f);
            if (GUILayout.Button("Reputation low")) SetReputation(-100f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Befriend rival + suitor, learn topics")) BefriendRivalAndSuitor();
            if (GUILayout.Button("Get rival's phone"))
            {
                if (yan != null && yan.Inventory != null) { yan.Inventory.RivalPhone = true; status = "Rival phone added."; }
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Unlock all tapes (headmaster + basement)")) UnlockTapes();
            GUILayout.Label("Befriend everyone, tasks, and student info are on the Students tab.");

            GUILayout.Space(8);
            GUILayout.Label("=== WORLD & CAMERA ===");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Toggle invisibility"))
            {
                if (yan != null)
                {
                    yan.Invisible = !yan.Invisible;
                    if (yan.Invisible) yan.Cloak(); else yan.Decloak();
                }
            }
            if (GUILayout.Button("Over-the-shoulder camera"))
            {
                ZoomScript z = FindObjectOfType<ZoomScript>();
                if (z != null) { z.OverShoulder = !z.OverShoulder; status = "Camera mode toggled (fully zoom in to see it)."; }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Green screen area"))
            {
                DebugMenuScript dm = GetDM();
                if (dm != null && dm.GreenScreen != null) { dm.GreenScreen.SetActive(true); status = "Green screen enabled."; }
            }
            if (GUILayout.Button("Sabotage drinking fountains")) SabotageFountains();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Toggle blood censor"))
            {
                GameGlobals.CensorBlood = !GameGlobals.CensorBlood;
                if (weaponManager != null) weaponManager.ChangeBloodTexture();
                if (yan != null) yan.Bloodiness = 0f;
                status = "Blood censor " + (GameGlobals.CensorBlood ? "on." : "off.");
            }
            if (GUILayout.Button("Toggle blurred kill animations"))
            {
                GameGlobals.BlurKillingAnims = !GameGlobals.BlurKillingAnims;
                if (yan != null && yan.AttackManager != null) yan.AttackManager.Blur = GameGlobals.BlurKillingAnims;
                status = "Kill-animation blur " + (GameGlobals.BlurKillingAnims ? "on." : "off.");
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Send delinquents to the incinerator area")) SendDelinquents();

            GUILayout.Space(8);
            GUILayout.Label("=== DESTRUCTIVE (tick the box first) ===");
            confirmDestructive = GUILayout.Toggle(confirmDestructive, "I understand these can't be undone");
            GUI.enabled = confirmDestructive;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Kill all students except Senpai")) KillAllStudents();
            if (GUILayout.Button("Reset ALL saved progress"))
            {
                bool eighties = GameGlobals.Eighties;
                Globals.DeleteAll();
                GameGlobals.Eighties = eighties;
                confirmDestructive = false;
                ReloadDay();
            }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            GUILayout.Label("Not included: story-event shortcuts (rival rooftop, couple, kidnap, dark-secret) and the free-fly camera.");
        }

        void MaxSeductionStrength()
        {
            ClassGlobals.PhysicalGrade = 5;
            PlayerGlobals.Seduction = 5;
            if (yan != null)
            {
                yan.PhysicalGrade = 5;
                if (yan.Class != null) { yan.Class.Seduction = 5; yan.Class.PhysicalGrade = 5; }
            }
            status = "Seduction and strength maxed.";
        }

        void AddInfoPoints(int amount)
        {
            if (yan == null || yan.Inventory == null) { status = "Inventory not found."; return; }
            int v = yan.Inventory.PantyShots + amount;
            yan.Inventory.PantyShots = v;
            PlayerGlobals.PantyShots = v;
            status = "Info points: " + v;
        }

        void SetReputation(float value)
        {
            PlayerGlobals.Reputation = value;
            if (rep != null) rep.Reputation = value;
            status = "Reputation set to " + value.ToString("0") + ".";
        }

        void BefriendRivalAndSuitor()
        {
            if (manager == null) { status = "Student manager not found."; return; }
            int[] ids = { manager.SuitorID, manager.RivalID };
            foreach (int id in ids)
            {
                PlayerGlobals.SetStudentFriend(id, true);
                if (manager.Students != null && id > 0 && id < manager.Students.Length && manager.Students[id] != null)
                    manager.Students[id].Friend = true;
            }
            for (int t = 1; t <= 25; t++)
            {
                ConversationGlobals.SetTopicDiscovered(t, true);
                manager.SetTopicLearnedByStudent(t, manager.RivalID, true);
            }
            status = "Befriended the rival and suitor and learned the rival's topics.";
        }

        void UnlockTapes()
        {
            for (int i = 1; i <= 10; i++)
            {
                CollectibleGlobals.SetHeadmasterTapeCollected(i, true);
                CollectibleGlobals.SetBasementTapeCollected(i, true);
            }
            status = "Tapes unlocked (reopen the tape menu).";
        }

        void SabotageFountains()
        {
            if (manager == null || manager.DrinkingFountains == null) { status = "Drinking fountains not found."; return; }
            int n = 0;
            for (int i = 1; i < 8 && i < manager.DrinkingFountains.Length; i++)
            {
                DrinkingFountainScript f = manager.DrinkingFountains[i];
                if (f == null) continue;
                if (f.PowerSwitch != null && f.PowerSwitch.PowerOutlet != null && f.PowerSwitch.PowerOutlet.SabotagedOutlet != null)
                    f.PowerSwitch.PowerOutlet.SabotagedOutlet.SetActive(true);
                if (f.Puddle != null) f.Puddle.SetActive(true);
                n++;
            }
            status = "Sabotaged " + n + " drinking fountains.";
        }

        void SendDelinquents()
        {
            if (manager == null || manager.Students == null) { status = "Student list not found."; return; }
            int n = 0;
            for (int i = 0; i < 5; i++)
            {
                int id = 76 + i;
                if (id >= manager.Students.Length) break;
                StudentScript s = manager.Students[id];
                if (s == null || s.Destinations == null || s.Destinations.Length < 3 || s.Destinations[2] == null) continue;
                s.Phase = 2;
                s.CurrentDestination = s.Destinations[2];
                s.transform.position = s.Destinations[2].position;
                n++;
            }
            status = "Moved " + n + " delinquents.";
        }

        void KillAllStudents()
        {
            if (manager == null || manager.Students == null) { status = "Student list not found."; return; }
            if (yan != null && yan.Police != null) yan.Police.Invalid = true;
            int n = 0;
            for (int i = 2; i < manager.Students.Length && i <= 100; i++)
            {
                StudentScript s = manager.Students[i];
                if (s == null) continue;
                s.SpawnAlarmDisc();
                s.BecomeRagdoll();
                s.DeathType = DeathType.EasterEgg;
                n++;
            }
            confirmDestructive = false;
            status = "Killed " + n + " students.";
        }

        // ---------------- Extras tab ----------------

        void BuildEggs()
        {
            eggsBuilt = true;
            foreach (string n in EggNames)
            {
                MethodInfo mi = typeof(YandereScript).GetMethod(n,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                if (mi != null) eggMethods[n] = mi;
            }
        }

        void DrawExtrasTab()
        {
            DrawKeybinds();
            GUILayout.Space(10);
            fastLoading = GUILayout.Toggle(fastLoading, "Faster loading screens");
            GUILayout.Space(10);
            GUILayout.Label("=== TIME ===");
            if (clock == null)
            {
                GUILayout.Label("Clock not found.");
            }
            else
            {
                GUILayout.Label("In-game time: " + Mathf.FloorToInt(clock.Hour) + ":" +
                                Mathf.FloorToInt(clock.Minute).ToString("00"));
                freezeClock = GUILayout.Toggle(freezeClock, "Freeze clock");
                GUILayout.Label("Clock speed x" + timeMult.ToString("0.0"));
                timeMult = GUILayout.HorizontalSlider(timeMult, 0.1f, 5f);
                if (GUILayout.Button("Reset clock speed")) timeMult = 1f;
            }

            GUILayout.Label("--- Time keys (like the debug menu) ---");
            GUILayout.Label("Press " + timeUpKey.Value + " (or numpad +) to speed up: 2x, 3x, 4x, 5x, then 10x, 15x, 20x, 25x. " +
                            "Press " + timeResetKey.Value + " (or numpad -) for normal speed. Now: " +
                            (hotkeyScale > 1.001f ? hotkeyScale.ToString("0") + "x" : "normal"));
            GUILayout.Label("--- Speed Up Time (Yandere vision, A / gaze key) ---");
            noPhotoSpeed = GUILayout.Toggle(noPhotoSpeed, "No photo of Senpai needed");
            fastSpeedUp = GUILayout.Toggle(fastSpeedUp, "Fast speed-up (debug-menu style) instead of the normal 2x");
            GUILayout.Label("Speed-up time scale x" + speedUpScale.ToString("0"));
            speedUpScale = Mathf.Round(GUILayout.HorizontalSlider(speedUpScale, 2f, 25f));

            GUILayout.Space(6);
            GUILayout.Label("=== CLUB PERKS ===");
            bool newAc = GUILayout.Toggle(allClubPerks, "All club perks without joining (not Delinquent)");
            if (newAc != allClubPerks) { allClubPerks = newAc; clubOwner = null; clubScene = null; }
            GUILayout.Label("Passive perks need the Harmony patch; object perks (fridge, masks, shower...) are enabled when the school loads.");
            GUILayout.Space(6);
            GUILayout.Label("Game speed x" + gameSpeed.ToString("0.0") + (gameSpeedOn ? " (active)" : " (off)"));
            gameSpeed = GUILayout.HorizontalSlider(gameSpeed, 0.1f, 3f);
            bool newGs = GUILayout.Toggle(gameSpeedOn, "Apply game speed");
            if (newGs != gameSpeedOn)
            {
                gameSpeedOn = newGs;
                if (!gameSpeedOn) savedTimeScale = 1f;
            }

            GUILayout.Space(10);
            GUILayout.Label("=== RIVAL ELIMINATION ===");
            GUILayout.Label("Only one of instant matchmaking / rejection / expel can be on at a time. In game, press " + elimKey.Value + " to switch between them (Enter = yes, Backspace = no).");
            bool r = GUILayout.Toggle(instantReject, "Instant rejection: sabotaging one rival interaction fills the whole rejection bar (5/5)");
            if (r != instantReject) SetElimination(r ? 2 : 0);
            GUILayout.Label("Sabotaging one of her events with Senpai, or lowering her reputation at all, fills the bar. She's rejected when she tries to confess on Friday.");
            if (manager != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Rejection bar right now: " + manager.SabotageProgress + "/5", GUILayout.Width(220));
                if (GUILayout.Button("Fill it now", GUILayout.Width(110))) FillRejection("button");
                GUILayout.EndHorizontal();
            }
            bool x = GUILayout.Toggle(instantExpel, "Instant expel: one successful report to the counselor fills the whole expel bar (5/5)");
            if (x != instantExpel) SetElimination(x ? 3 : 0);
            GUILayout.Label("The rival is expelled right after that lecture.");
            instantBreak = GUILayout.Toggle(instantBreak, "Instant mind-broken slave: one torture session in the basement takes sanity straight to 0%");
            easyMinigames = GUILayout.Toggle(easyMinigames, "Easy Befriend/Betray minigames");
            GUILayout.Label("The stalker and the asylum's photographer, squatters and guards don't notice you, struggle bars fill on their own and can't be lost, and pickpocketing has no time limit (wrong buttons still fail).");

            GUILayout.Space(10);
            GUILayout.Label("=== MISSION MODE ===");
            nemesisVision = GUILayout.Toggle(nemesisVision, "Show Nemesis in Yandere vision (purple outline, like the red target outline)");
            GUILayout.Label("Works while Nemesis is active in a mission. The top-right box also shows how far away she is.");

            GUILayout.Space(10);
            GUILayout.Label("=== CONTENT CHECKLIST ===");
            if (GUILayout.Button("Complete the whole Content Checklist (" + ChecklistKeys.Length + " entries)")) CompleteChecklist();
            GUILayout.Label("Works from any screen. Reopen the checklist on the title screen to see it.");

            GUILayout.Space(10);
            GUILayout.Label("=== EASTER EGGS (no debug menu needed) ===");
            GUILayout.Label("The game may flag a run as invalid for the Genocide challenge when easter eggs are used. Close this menu to see the effect.");
            if (yan == null) { GUILayout.Label("Player not found."); return; }
            if (!eggsBuilt) BuildEggs();

            int col = 0;
            GUILayout.BeginHorizontal();
            foreach (string n in EggNames)
            {
                if (!eggMethods.ContainsKey(n)) continue;
                if (GUILayout.Button(n, GUILayout.Width(115))) InvokeEgg(n);
                col++;
                if (col % 4 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            }
            GUILayout.EndHorizontal();
        }

        void InvokeEgg(string name)
        {
            MethodInfo mi;
            if (yan == null || !eggMethods.TryGetValue(name, out mi)) return;
            try
            {
                mi.Invoke(yan, null);
                status = "Activated " + name + ".";
            }
            catch (Exception e)
            {
                Exception inner = e.InnerException ?? e;
                status = name + " failed: " + inner.Message;
                Logger.LogWarning(name + " failed: " + inner);
            }
        }
    }
}
