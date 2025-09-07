# Eli Dang Holo Hud - Open Source 2.0
This project will be my open source repository for the Eli Dang holo hud mod for Space Engineers. I will primarily maintain it on Steam Workshop but the source code lives here. 

It has been branched from Polygon Cherub, the original author. 

It is being released as GPL 3.0, which I believe was the intention and spirit of Polygon Cherub's original comment:

"This is my first ever mod for SE and I was learning C# as I went. If you decide to try this mod out, please give me feed back / reports of trouble so that I may better learn.
I hold no desire to police the use of this mod's code. Feel free to modify and re-upload if you make significant changes. Otherwise, share your feedback with me so I can improve it for everyone!"

# Readme / Configuration
Can choose between a tag based HUD activation with [ELI_HUD] in the CustomName being eligible, or in the XML settings you can turn the MainCockpit requirement back on where it only works for control seats that are set to MainCockpit. 
Per-ship settings get stored to CustomData of the control seat. 

Can also run holographic radar from any terminal block (like a holo table, LCD, control panel, even a sound block lol) using [ELI_HOLO] custom name.
Can also run holographic local grid display from any terminal block using [ELI_LOCAL].
CustomData of these blocks has options for tweaking how and where the radar or hologram draw. You can re-set this to default by clearing the EliDang section entirely. 

Global settings are stored in the World Save folder, eg SaveGameName\Storage\########_EliDangHud\EDHH_settings.xml
Each setting has a description that explains what it does.
This file will only generate after saving your game with the mod enabled. I recommend you add the mod and load the game, save it and exit, then make any changes to the mod config. 


You can rebind keys, the enum of available MyKeys are below (Note that mouse clicks are treated special, and have their own settings documented in EDHH_settings.xml).

public enum MyKeys : byte
{
    None = 0,
    LeftButton = 1,
    RightButton = 2,
    Cancel = 3,
    MiddleButton = 4,
    ExtraButton1 = 5,
    ExtraButton2 = 6,
    Back = 8,
    Tab = 9,
    Clear = 12,
    Enter = 13,
    Shift = 16,
    Control = 17,
    Alt = 18,
    Pause = 19,
    CapsLock = 20,
    Kana = 21,
    Hangeul = 21,
    Hangul = 21,
    Junja = 23,
    Final = 24,
    Hanja = 25,
    Kanji = 25,
    Ctrl_Y = 25,
    Ctrl_Z = 26,
    Escape = 27,
    Convert = 28,
    NonConvert = 29,
    Accept = 30,
    ModeChange = 31,
    Space = 32,
    PageUp = 33,
    PageDown = 34,
    End = 35,
    Home = 36,
    Left = 37,
    Up = 38,
    Right = 39,
    Down = 40,
    Select = 41,
    Print = 42,
    Execute = 43,
    Snapshot = 44,
    Insert = 45,
    Delete = 46,
    Help = 47,
    D0 = 48,
    D1 = 49,
    D2 = 50,
    D3 = 51,
    D4 = 52,
    D5 = 53,
    D6 = 54,
    D7 = 55,
    D8 = 56,
    D9 = 57,
    A = 65,
    B = 66,
    C = 67,
    D = 68,
    E = 69,
    F = 70,
    G = 71,
    H = 72,
    I = 73,
    J = 74,
    K = 75,
    L = 76,
    M = 77,
    N = 78,
    O = 79,
    P = 80,
    Q = 81,
    R = 82,
    S = 83,
    T = 84,
    U = 85,
    V = 86,
    W = 87,
    X = 88,
    Y = 89,
    Z = 90,
    LeftWindows = 91,
    RightWindows = 92,
    Apps = 93,
    Sleep = 95,
    NumPad0 = 96,
    NumPad1 = 97,
    NumPad2 = 98,
    NumPad3 = 99,
    NumPad4 = 100,
    NumPad5 = 101,
    NumPad6 = 102,
    NumPad7 = 103,
    NumPad8 = 104,
    NumPad9 = 105,
    Multiply = 106,
    Add = 107,
    Separator = 108,
    Subtract = 109,
    Decimal = 110,
    Divide = 111,
    F1 = 112,
    F2 = 113,
    F3 = 114,
    F4 = 115,
    F5 = 116,
    F6 = 117,
    F7 = 118,
    F8 = 119,
    F9 = 120,
    F10 = 121,
    F11 = 122,
    F12 = 123,
    F13 = 124,
    F14 = 125,
    F15 = 126,
    F16 = 127,
    F17 = 128,
    F18 = 129,
    F19 = 130,
    F20 = 131,
    F21 = 132,
    F22 = 133,
    F23 = 134,
    F24 = 135,
    NumLock = 144,
    ScrollLock = 145,
    NEC_Equal = 146,
    Fujitsu_Jisho = 146,
    Fujitsu_Masshou = 147,
    Fujitsu_Touroku = 148,
    Fujitsu_Loya = 149,
    Fujitsu_Roya = 150,
    LeftShift = 160,
    RightShift = 161,
    LeftControl = 162,
    RightControl = 163,
    LeftAlt = 164,
    RightAlt = 165,
    BrowserBack = 166,
    BrowserForward = 167,
    BrowserRefresh = 168,
    BrowserStop = 169,
    BrowserSearch = 170,
    BrowserFavorites = 171,
    BrowserHome = 172,
    VolumeMute = 173,
    VolumeDown = 174,
    VolumeUp = 175,
    MediaNextTrack = 176,
    MediaPrevTrack = 177,
    MediaStop = 178,
    MediaPlayPause = 179,
    LaunchMail = 180,
    LaunchMediaSelect = 181,
    LaunchApplication1 = 182,
    LaunchApplication2 = 183,
    OemSemicolon = 186,
    OemPlus = 187,
    OemComma = 188,
    OemMinus = 189,
    OemPeriod = 190,
    OemQuestion = 191,
    OemTilde = 192,
    ChatPadGreen = 202,
    ChatPadOrange = 203,
    OemOpenBrackets = 219,
    OemPipe = 220,
    OemCloseBrackets = 221,
    OemQuotes = 222,
    Oem8 = 223,
    OEMAX = 225,
    OemBackslash = 226,
    ICOHelp = 227,
    ICO00 = 228,
    ProcessKey = 229,
    ICOClear = 230,
    Packet = 231,
    OEMReset = 233,
    OEMJump = 234,
    OEMPA1 = 235,
    OEMPA2 = 236,
    OEMPA3 = 237,
    OEMWSCtrl = 238,
    OEMCUSel = 239,
    OEMATTN = 240,
    OEMFinish = 241,
    OEMCopy = 242,
    OEMAuto = 243,
    OEMENLW = 244,
    OEMBackTab = 245,
    ATTN = 246,
    CRSel = 247,
    EXSel = 248,
    EREOF = 249,
    Play = 250,
    Zoom = 251,
    Noname = 252,
    PA1 = 253,
    OEMClear = 254
}

# Changelog
## 2025-09-07
DONE: Make Main Cockpit or Tag be an option. Make custom cockpit sliders/checkboxes etc. load based on only this setting, no longer requires a broadcasting antenna. (Was a leftover of original). 

DONE: Make SigInt lite logic be toggleable. You can have some of the old logic instead. It still checks all powered antennas on the grid and uses the max broadcast range as the radar range for detecting and scaling the radar.
But doesn't do all the active vs passive radar stuff. 

DONE: More minor optimizations: only checks if entity has power if you can already detect it based on range and the setting to only show powered grids is true. 

DONE: Top priority: Optimize the holograms! Will use a Dictionary of blocks on the grid, then have event handlers for add or remove of block, or damage of block instead of
scanning all blocks each tick. So we initialize it using getBlocks once, then only update it when something changes. When it comes to drawing their positions we will
have the grid relative positions, so for local grid this is easy. For target grid we will need to offset based on the current grid positions at Draw() time.
This is a complete overhaul to initialize once, then use event handlers for updates, and optimized draw by clustering blocks when grids are of a certain block count. Drawing each square gets very heavy.
This is configurable, defaults to 10,000 so a grid of 10,000 blocks will switch to 2x2x2 clusters of blocks being drawn as a single square instead of one per block. 


## 2025-08-01
DONE: Fix grid flare setting.

DONE: Fix visor setting.

DONE: Round space credits when at large values - should show K, M, B.

DONE: Make max radar range be the max range of the antenna of the current ship. - Covered by active/passive.

DONE: Fix H2 Display % remaining or time based on current draw like power does, gives me INVALID_NUMBER currently? - Added support for modded tanks by checking if block has capacity and has Hydrogen anywhere in details/subtype etc. 

DONE: Make it keyword tag based [ELI_HUD] instead of main cockpit...

DONE: Make it so if antenna missing or broadcasting turned off only the Holograms and Radar cease to function, but the rest of the hud works as expected. - Covered by active/passive.

DONE: Overhaul entire antenna logic so we get active+passive radar and hud still functions regardless of antenna state for things like H2/Energy/Altitude etc. Uses simple SigInt logic for now.
1) Antenna off/damaged = no radar.
2) Antenna on/functional but NOT broadcasting = passive mode, receptive to grids with broadcasting antennas only, up to local grid antenna's configured broadcast range or global radar maximum (which could be draw distance if left at -1).
and only if broadcasting grid antenna range would reach local grid. Ie. if they can see you actively, you can see them passively. 
  TODO: Advanced SigInt logic where active signals from any ship can bounce around and be picked up in passive mode? So one active ship can feed passive fleet?
3) Antenna on/functional AND broadcasting = active mode, local grid can pick up grids or entities/voxels within the broadcast range. By virtue of being in active mode, you can also passively pickup signals.
ie. there is Passive, or Active+Passive, or Off. Broadcast on/off controls Passive vs Active mode, power controls On/Off.
Text on radar display shows RDR[OFF], RDR[ACT], or RDR[PAS] based on mode. Then receptive distance in KM. Then [SCL] for the radar distance scale in KM.
4) [SCL] (variable called radarScaleRange) controls how zoomed in or out the radar screen is, basically how many KM it displays and can be different than your detection range.
It uses a configurable bracket distance users can change where it will only increase every X amount with a configurable percentage based buffer to prefer zooming out. 
eg. at my default settings of 2000 meters bracket with 0.01 buffer if my range is 2000m + 20m buffer it would fall into the 4000m bracket because 2020 > 2000. If I had the bracket set to 1000 meters it would fall in the 3000m bracket. So on.
This means raising or lowering the antenna broadcast range, even in broadcast = off mode, changes the zoom of the radar in addition to how far you can pickup targets.
It also will shrink the radar display to zoom in on a selected target indicated by (T) at the end of the scale distance. It does the same range bracketing, but based on the target's distance from the local grid instead of the radar range.
This makes it easier to see your position in relation to the target on the radar screen.
  TODO: Make broadcast range only affect active signal sent out, and make scale be something else configurable that gets stored in the cockpit's block data that can have up/down toggles added to toolbar. No idea how to do this
        so using the broadcast range for now.
  WIP - I'm looking into adding toolbar actions for things like this or enabling/disabling hud now. 
This allows for fun things like having a passive antenna up to 50km to pick up grids with active radar on up to that distance (if their active signals are pinging you anyway). 
And have a separate active radar set to 2km to pick up anything near you. This way your leaked signals don't give you away as far, but passive grids (or asteroids!) can't totally sneak up on you.

DONE: Move random number generation to LoadData so it occurs once, and populates a large list of random floats between 0 and 1. These are then used by GetRandomFloat, GetRandomDouble, or GetRandomBoolean. 
These array lookups are far more efficient than generating random numbers. And in the original mod we were generating 33 random numbers per tick. 

DONE: Rework where calculations are done. UpdateBeforeSimulation() should be for calculations/changes that might affect physics (thruster input for eg), UpdateAfterSimulation() should be for calculations taht depend on updated
physics states (like calculating the position of a grid, which could have changed after a physics impact), and Draw() should be reserved for the actual rendering. 
There's a lot of complex math going on in Draw() that doesn't need to be there.

DONE: (PARTIAL) Make target condition hologram better, there's still some bugs. But we can choose between viewing various sides using hotkeys, or an Orbit cam view, or a perspective based view. But the perspective based view has minor bugs.

DONE: Make entities with active radar stand out visually. 

DONE: Look into color/scale sliders re-setting or not changing per ship as you leave one and enter another. Might already be fixed? I believe this would have been fixed
by my changes to where CheckCustomData gets called anyway. 

DONE: Make target selection code use a minBlockCount setting to prevent debris being picked up instead of actual grids. Also uses GetTopMostParent so it returns main grids instead of subgrids like wheels etc.

DONE: Make target selection and hologram view changing keys be bindable in the config. 

DONE: Add setting to only show radar pings for grids with power. Setting is independent of voxels, so if show voxels is true they will still appear despite not having power. 

DONE: Add block actions for toggling the hud on/off, the holograms on/off, show voxels on/off, and powered only on/off. These can be bound from the G menu to the toolbar. 

DONE: Local Grid and Target Grid holograms can have their views cycled. Ctrl modifier applies to local grid hologram, no Ctrl key pressed will change target grid hologram.

DONE: Fix HasPowerProduction to check if batteries have stored power too. 

DONE: Moved some functions back into Draw(), while calculating UI positions in UpdateAfterSimulation seemed like a good optimization, when grids are moving quickly it lags too far behind and causes UI to draw oddly. 
I suspect this would be even worse with speed mods allowing over 300m/s. I have left updating the radar detections themselves in UpdateAfterSimulation, and this should be fine. 

DONE: Removed arbitrary DisplayName == "Stone" || DisplayName == null filter from radar entities return. In my testing this prevented asteroids of any type I spawned in from showing. 
I'd like to do further settings to allow users to more specifically choose to filter our floating items of name 'Stone' for eg, or only voxels (asteroids and deposits) of certain types or sizes even. 

# TODO
TODO: I'd like to do further settings to allow users to more specifically choose to filter our floating items of name 'Stone' for eg, or only voxels (asteroids and deposits) of certain types or sizes even. 

TODO: Figure out all this DamageAmount/AttachGrid/DetachGrid event handler stuff. It looks... incomplete? Eg GetDamageAmount if called would re-set the amount to zero. It is used to get a damage amount to add to glitch amount overload.
but the way it is configured all that happens prior to this is attaching event handlers for on function changed. So amount would always be 0?
Is this a remnant from presumably older code that used the queue of blocks to check each one for current damage, so it would do something like upon sitting in the control seat of a grid that was already damaged
store that value? 

TODO: Look into use of IMyCockpit vs IMyShipController, didn't we see reports of players wishing it worked for remote controlled ships?

TODO: (PARTIAL) Make player condition hologram better - would love to make it flip to show side taking damage most recently?

TODO: Can we color code power, weapon, antenna, control blocks in the target/player hologram? Then apply a yellow/red color gradient overtop for damage. Or even shade them? For eg. instead of MaterialSquare
we have other squares that have /// or ### or something. Who knows. There's definitely something to do here just not sure exact approach. 

TODO (PARTIAL): Make altitude readout - I have the value, but haven't decided where to draw it on screen yet.

TODO: Make broadcast range only affect active signal sent out, and make scale be something else configurable that gets stored in the cockpit's block data that can have up/down toggles added to toolbar. 
No idea how to do this so using the broadcast range for now. MIGHT NOT DO THIS AFTER ALL. I kinda like broadcast range directly controlling scale it gives visual feedback for how far out your antenna is set too.

TODO: Make radar work on a holotable

TODO: Make radar work on LCD with up or down triangles for verticality for eg. Only after holotable. 

TODO: Compatibility with frame shift drive? Likely around the jumpdrive mechanic? Will look into this.

TODO: Show modded ammo types in the gauges. Attempted but might have failed? needs testing...

TODO: WeaponCore compatibility - partiall attempted, but I really have no idea what I'm doing. I may need to load up the WeaponCore source code to figure out how to hook into it. 

TODO: Advanced SigInt logic where active signals from any ship can bounce around and be picked up in passive mode? So one active ship can feed passive fleet?

TODO: Apparently splitting/merging a ship with merge blocks causes a crash.

TODO: Remote control ship and then get out of antenna range (or have remote controlled ship explode for eg) was causing crash. Another author may have fixed that in this already.

TODO: Check shield mod integrations, how can we make that universal across shield mods?

TODO: Apparently asteroids can cause phantom pings on planets, should try and look into that for Cherub.

#Credits
Ship radar icons from Grandsome https://www.nexusmods.com/x4foundations/mods/895 used with attribution as per requirements. 

