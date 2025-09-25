# Eli Dang Holo Hud - Open Source 2.1
This project will be my open source repository for the Eli Dang holo hud mod for Space Engineers. I will primarily maintain it on Steam Workshop but the source code lives here. 

It has been branched from Polygon Cherub, the original author. 

It is being released as GPL 3.0, which I believe was the intention and spirit of Polygon Cherub's original comment:

"This is my first ever mod for SE and I was learning C# as I went. If you decide to try this mod out, please give me feed back / reports of trouble so that I may better learn.
I hold no desire to police the use of this mod's code. Feel free to modify and re-upload if you make significant changes. Otherwise, share your feedback with me so I can improve it for everyone!"

# Features
- Holographic HUD with radar and various ship stats including speed, energy, hydrogen, integrity holograms for local grid and target, planet orbit lines, velocity vector lines, visor effects, and more. 
Enabled on any control seat with either the tag [ELI_HUD] (default) or by setting main cockpit (optional config setting). All settings are configurable at the global level, and most also per-cockpit (or terminal block) via CustomData.

- Some actions like toggling display of unpowered grids, voxels, holograms, or entire HUD can be toggled via toolbar actions if you assign the cockpit to your toolbar. More actions to be added in the future. 

- Holographic radar projection can be displayed above any terminal block (like a holo table, LCD, control panel etc.) using [ELI_HOLO] tag in the block custom name, then use CustomData to tweak position, scale, rotation, and what it detects.

- Holographic local grid projection can be displayed above any terminal block using [ELI_LOCAL] tag in the block custom name, then use CustomData to tweak position, scale, rotation, and color.

- Holographic projections for the local grid and the selected target grid showing hull integrity and shield status (technically jump drives right now, to be overhauled for modern shield mods).
These projections use "block clustering" which defaults to a range of 1x1x1 to 2x2x2 for grids below a certain block count (and increases to 1x1x1 to 3x3x3 after) to reduce the squares drawn on screen for performance. 
Damage is reflected immediately by color changes to the cluster, and block removal/addition triggers a complete recluster after a wait period of no further changes to reduce stutter. It does this over multiple ticks to further reduce stutter.
There are complex settings to tweak this behaviour in the global config file, documentation on this below.

- SigInt lite logic for radar detections, uses powered antenna on your grid to determine radar range and whether you are in Passive or Active mode based on if any antenna is broadcasting. 
Largest broadcast distance of any antenna on the grid becomes your max Passive detection range. Largest broadcast distance of broadcasting antenna becomes your Active detection range.
This means increasing/decreasing antenna broadcast distance changes your radar range and thus the "zoom" of the radar projection. Use "Range Brackets" to determine the zoom level.

- Radar zooms in upon selecting a target so it fits better on screen. Uses configurable "Range Brackets" to determine zoom level. Ie if a target is 245m away and you have 200m brackets it will zoom to a 400m max range. 
If you had 50m brackets it would zoom to 250m max range. This makes it easier to see your position in relation to the target on the radar screen. 

- Radar uses custom blip icons for various ship sizes. Different icon sets are used for small grids and large grids. Then based on block count the icon changes to represent the size of the ship. The block counts for each tier are configurable.
Blip icons are from an X4 Mod with permission, link in the credits section below. 

- Grid flares (white glints) behind distant grids in space, configurable. 

- Keybinds to rotate the holograms (local and target) around all three axes, reset the view, and for target only you can cycle between orbit cam, perspective, or static view you can rotate manually. Configurable.

- Mouse button or keybind to target the entity in the center of your view. Configurable. 

- Huge optimizations for Radar and Holograms versus the original mod. 

- Further improved holograms versus my prior releases as well for both performance and fidelity by using event handlers after initialization, block clustering, and occlusion culling. 

- Everything configurable! You can turn off planet orbits, velocity lines, local hologram, target hologram, radar, visor effects, cockpit dust, grid flares etc. You can tweak the clustering logic, and the performance vs fidelity balance.
You can tweak how quickly the holograms recluster after block add/removal, and how many clusters it processes per tick to spread the load out. All explained further below.

- Chat commands to edit global settings in-game! Type "/elidang help" in chat to see available commands. Type "/elidang <setting_name> help" for help with a specific setting. Type "/elidang <setting_name> <value>" to change a setting.


# Readme / Configuration
Can choose between a tag based HUD activation with [ELI_HUD] in the CustomName being eligible, or in the XML settings you can turn the MainCockpit requirement back on where it only works for control seats that are set to MainCockpit. 
Per-ship settings get stored to CustomData of the control seat. 

Can also run holographic radar from any terminal block (like a holo table, LCD, control panel, even a sound block lol) using [ELI_HOLO] custom name.
Can also run holographic local grid display from any terminal block using [ELI_LOCAL].
CustomData of these blocks has options for tweaking how and where the radar or hologram draw. You can re-set this to default by clearing the EliDang section entirely. 

Global settings are stored in the World Save folder, eg \YourSaveFolderLocation\WorldName\Storage\3540066597_EliDangHUD\EDHH_settings.xml
Each setting has a description that explains what it does.
This file will only generate after saving your game with the mod enabled. I recommend you add the mod and load the game, save it and exit, then make any changes to the mod config. 

# Notes on Hologram Block Clustering
## Clustering Block Counts and Min/Max Cluster Range
Holograms use a block clustering system to reduce the number of squares drawn on screen for performance. This works by looking at blockCountClusterStep from the settings, this determines the max number of squares to draw on screen above which it will 
use the next size of block clusters to reduce the number of squares down below blockCountClusterStep again.
It checks the block count of the grid and determines a cluster size based on that, which is cubic. It also uses a clusterSplitThreshold to determine if a cluster should be split into smaller clusters based on how many blocks are in it.
And this defaults to a range of 1 to 2 for grids below the blockCountClusterStep, and 1 to 3 for grids in the next step up, 2 to 4 for the next step and so on. If you tweak the additional min/max settings this range can vary.
Because this is cubic the default the range is 0-10000 = cluster size 1, 10000-80000 = cluster size 2, 80000 - 270000 = cluster size 3. Because 80000 / (2x2x2) = 10000. 270000 / (3x3x3) = 10000.

The system retains fidelity in sparsely populated areas of a grid by breaking down clusters that are below the clusterSplitThreshold. 
At the default of 0.33 = 33%, for a grid above 10000 blocks equaling a range of 1x1x1 - 3x3x3 this means it will first cluster all blocks in the grid into the max size (3x3x3) for the grid. As it does so it checks if each 3x3x3 cluster's
blockCount / 27 (3x3x3) is below 0.33, if so it discards the 3x3x3 cluster and recluters them as 2x2x2. 
It then checks if these 2x2x2 cluster(s) blockCount / 8 (2x2x2) is below 0.33, if so it discards the 2x2x2 cluster(s) and reclusters them as 1x1x1 (each block)

## Clustering updates on Damage or Block Add/Removal
As a grid is damaged (but no blocks are destroyed) it updates the currentIntegrity / maxIntegrity of the cluster(s) the blocks belong to and updates their color.

If a grid has a block added it simply triggers a recluster after a timer, explained below.

As a grid is damaged and blocks get removed due to complete destruction it checks the cluster they belong to and does two things: 
1) Immediately updates the currentIntegrity / maxIntegrity of that cluster so the color updates immediately. This is very low performance impact but it won't recluster.
2) Triggers a countdown to a full recluster of the entire grid after a period of no further changes (default ticksUntilClusterRebuildAfterChange = 100 ticks ). Once that time passes it will initialize a full recluster of the entire grid, but the work is
spread out over multiple game ticks (default clusterRebuildClustersPerTick = 200 clusters per tick). You can tweak these settings in the global config file. 
Lowering the ticksUntilClusterRebuildAfterChange will make the recluster start sooner after changes, but may increase stutter if you are adding/removing blocks quickly.
Increasing the clusterRebuildClustersPerTick will make the recluster finish sooner (and less likely to get started over by a new change), but may increase stutter as more work is done per tick.
I have not done much testing on this, there might be a lot of room to increase this above 200 even for potato machines, there might not. You'll have to experiment. 
If it is causing stutter you can lower settings, if you want the changes in blocks to recluster the grid sooner you can increase the clusters per tick. 
NOTE: As far as I know the update loop is at 60hz so 60 ticks per second. This means at defaults it would do 12000 clusters in a second which is higher than our max, and the cluster ranges mean most grids have a combo of 1x1x1 and 2x2x2 clusters.
So in theory a complete cluster rebuild will trigger just over a second after a change, and will complete in under a second after that so long as we aren't actively lagging due to other things going on in the game. As sim speed drops below 1.0 this
will start to take longer.


# Example Settings EDHH_settings.xml
```xml
<?xml version="1.0" encoding="utf-16"?>
<ModSettings xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <useMainCockpitInsteadOfTag>false</useMainCockpitInsteadOfTag>
  <useMainCockpitInsteadOfTag_DESCRIPTION>If true the mod is enabled by setting seat as Main Cockpit instead of using a tag [ELI_HUD], for users who prefer the original method. 
 Note that with this setting enabled you can only have one HUD seat per grid. If you leave this disabled you can use the [ELI_HUD] tag on as many seats as you want.</useMainCockpitInsteadOfTag_DESCRIPTION>
  <maxRadarRangeGlobal>-1</maxRadarRangeGlobal>
  <maxRadarRange_DESCRIPTION>Max global radar range in meters, setting -1 will use the draw distance. Otherwise you can set a global maximum limit for the radar range. 
 I think you could technically exceed the games 50km limit in radar scale, but there is a hard limit at the 50km antenna broadcast range for detecting.</maxRadarRange_DESCRIPTION>
  <rangeBracketDistance>200</rangeBracketDistance>
  <rangeBracketDistance_DESCRIPTION>Represents the distance in meters used for range bracketing in radar targeting calculations. 
This value is used to determine the "bracket" a target fits into for zooming the radar. Or when changing broadcast distance of antennas (radar range). 
Adjusting this value can affect the precision of radar zoom levels.</rangeBracketDistance_DESCRIPTION>
  <maxPings>500</maxPings>
  <maxPings_DESCRIPTION>Maximum number of entities/voxels that can be displayed as radar pings on the radar at once.</maxPings_DESCRIPTION>
  <useSigIntLite>true</useSigIntLite>
  <useSigIntLite_DESCRIPTION>When true uses SigInt lite logic regarding Active and Passive radar, using Antenna blocks on your and other grids. When enabled: A powered and broadcasting antenna 
 on your grid will be considered active mode, powered but not broadcasting is considered passive mode. 
 Active can pick up all entities (voxels and grids) within the broadcast range. This is your radar waves painting everything within that radius and pinging them. 
 Passive can only pick up actively broadcasting grids within your broadcast radius, and their broadcast radius. This is your radar picking up signals that are pinging off you. 
 When disabled uses the simple logic of largest radius from all powered antennae on the grid as your radar range.</useSigIntLite_DESCRIPTION>
  <holoTableRenderDistance>20</holoTableRenderDistance>
  <holoTableRenderDistance_DESCRIPTION>How far away can a holo table be before it no longer renders the radar.</holoTableRenderDistance_DESCRIPTION>
  <fadeThreshold>0.01</fadeThreshold>
  <fadeThreshold_DESCRIPTION>Percentage threshold at which radar pings start to fade out at the edge of the radar range Eg. 0.01 = 0.01*100 = 1%. 
 Also used for notifying the player of new pings. 
 When pings cross the threshold distance they will be announced audibly and visually on the radar.</fadeThreshold_DESCRIPTION>
  <lineThickness>1.5</lineThickness>
  <lineThickness_DESCRIPTION>The thickness of the lines used for rendering circles and other shapes in the HUD.</lineThickness_DESCRIPTION>
  <lineDetail>90</lineDetail>
  <lineDetail_DESCRIPTION>The number of segments used to render circles and other circular shapes in the HUD. 
 Lowering this number can make it more blocky. Eg. setting this to 6 would make it a hexagon instead of a circle.</lineDetail_DESCRIPTION>
  <lineColorDefault>
    <X>1</X>
    <Y>0.5</Y>
    <Z>0</Z>
    <W>1</W>
  </lineColorDefault>
  <lineColorDefault_DESCRIPTION>Default color of the HUD lines, used for rendering circles and other shapes.</lineColorDefault_DESCRIPTION>
  <starPos>
    <X>0</X>
    <Y>0</Y>
    <Z>0</Z>
  </starPos>
  <starPos_DESCRIPTION>Position of the star. No idea if we need to change this for "RealStars" mod or not.</starPos_DESCRIPTION>
  <starFollowSky>true</starFollowSky>
  <starFollowSky_DESCRIPTION>Does the star position follow the skybox?</starFollowSky_DESCRIPTION>
  <enableGridFlares>true</enableGridFlares>
  <enableGridFlares_DESCRIPTION>Enable or disable the white "flares" that glint behind grids in the distance. 
This is what makes a distant ship "glint" in the void of space. Some users like this, as you can see where ships are better. 
Others prefer a more realistic environment with this off so you can hide in the void.</enableGridFlares_DESCRIPTION>
  <enableCockpitDust>true</enableCockpitDust>
  <enableCockpitDust_DESCRIPTION>Enable cockpit dust effects.</enableCockpitDust_DESCRIPTION>
  <enableVisor>true</enableVisor>
  <enableVisor_DESCRIPTION>Show visor effects or not.</enableVisor_DESCRIPTION>
  <enableVelocityLines>true</enableVelocityLines>
  <enableVelocityLines_DESCRIPTION>Show the velocity lines or not.</enableVelocityLines_DESCRIPTION>
  <enablePlanetOrbits>true</enablePlanetOrbits>
  <enablePlanetOrbits_DESCRIPTION>Show the planet orbit lines or not.</enablePlanetOrbits_DESCRIPTION>
  <enableHologramsGlobal>true</enableHologramsGlobal>
  <enableHolograms_DESCRIPTION>Whether any kind of Hologram can be shown or not. 
 Each cockpit can override this setting, and turn on/off holograms for the local grid or the target grid separately in the block's custom data.</enableHolograms_DESCRIPTION>
  <useHollowReticle>true</useHollowReticle>
  <useHollowReticle_DESCRIPTION>Should the targeting reticle be hollow or have a "dot" in the middle that can sometimes block the view of the target especially if it's a small grid.</useHollowReticle_DESCRIPTION>
  <minTargetBlocksCount>5</minTargetBlocksCount>
  <minTargetBlocksCount_DESCRIPTION>The minimum number of blocks required for a grid to be selected as a target. Helps reduce pieces of debris being selected</minTargetBlocksCount_DESCRIPTION>
  <useMouseTargetSelect>true</useMouseTargetSelect>
  <useMouseTargetSelect_DESCRIPTION>Whether to use a mouse button, or a keybind, for selecting new targets in Eli Dang hud</useMouseTargetSelect_DESCRIPTION>
  <selectTargetMouseButton>1</selectTargetMouseButton>
  <selectTargetMouseButton_DESCRIPTION>Mouse button to use: 0 = Left, 1 = Right, 2 = Middle. (Default is Right)</selectTargetMouseButton_DESCRIPTION>
  <selectTargetKey>84</selectTargetKey>
  <selectTargetKey_DESCRIPTION>Key to select target, if mouse button is disabled (Default is T)</selectTargetKey_DESCRIPTION>
  <rotateLeftKey>100</rotateLeftKey>
  <rotateLeftKey_DESCRIPTION>Key to rotate the static hologram view in the +X axis (Ctrl modifier changes local hologram, no Ctrl changes target, Default is NumPad4)</rotateLeftKey_DESCRIPTION>
  <rotateRightKey>102</rotateRightKey>
  <rotateRightKey_DESCRIPTION>Key to rotate the static hologram view in the -X axis (Ctrl modifier changes local hologram, no Ctrl changes target, Default is NumPad6)</rotateRightKey_DESCRIPTION>
  <rotateUpKey>104</rotateUpKey>
  <rotateUpKey_DESCRIPTION>Key to rotate the static hologram view in the +Y axis (Ctrl modifier changes local hologram, no Ctrl changes target, Default is NumPad8)</rotateUpKey_DESCRIPTION>
  <rotateDownKey>98</rotateDownKey>
  <rotateDownKey_DESCRIPTION>Key to rotate the static hologram view in the -Y axis (Ctrl modifier changes local hologram, no Ctrl changes target, Default is NumPad2)</rotateDownKey_DESCRIPTION>
  <rotatePosZKey>103</rotatePosZKey>
  <rotatePosZKey_DESCRIPTION>Key to rotate the static hologram view in the +Z axis (Ctrl modifier changes local hologram, no Ctrl changes target, Default is NumPad7)</rotatePosZKey_DESCRIPTION>
  <rotateNegZKey>105</rotateNegZKey>
  <rotateNegZKey_DESCRIPTION>Key to rotate the static hologram view in the -Z axis (Ctrl modifier changes local hologram, no Ctrl changes target, Default is NumPad9)</rotateNegZKey_DESCRIPTION>
  <resetKey>101</resetKey>
  <resetKey_DESCRIPTION>Key to reset hologram view (Ctrl modifier cycles local hologram, no Ctrl cycles target, Default is NumPad5)</resetKey_DESCRIPTION>
  <orbitViewKey>97</orbitViewKey>
  <orbitViewKey_DESCRIPTION>Key to set hologram view to Orbit cam (Only for target, Default is NumPad1)</orbitViewKey_DESCRIPTION>
  <perspectiveViewKey>99</perspectiveViewKey>
  <perspectiveViewKey_DESCRIPTION>Key to set hologram view to Perspective cam (Only for target, Default is NumPad3)</perspectiveViewKey_DESCRIPTION>
  <largeGridSizeOneMaxBlocks>1500</largeGridSizeOneMaxBlocks>
  <largeGridSizeTwoMaxBlocks>2500</largeGridSizeTwoMaxBlocks>
  <largeGridSizeThreeMaxBlocks>5000</largeGridSizeThreeMaxBlocks>
  <largeGridSizeFourMaxBlocks>7500</largeGridSizeFourMaxBlocks>
  <largeGridSizeFiveMaxBlocks>15000</largeGridSizeFiveMaxBlocks>
  <largeGridSizeTiers_DESCRIPTION>Set the various size in blocks for the tiers that determine blip icons used. There are six (6) large grid icons.</largeGridSizeTiers_DESCRIPTION>
  <smallGridSizeOneMaxBlocks>600</smallGridSizeOneMaxBlocks>
  <smallGridSizeTwoMaxBlocks>1200</smallGridSizeTwoMaxBlocks>
  <smallGridSizeThreeMaxBlocks>1800</smallGridSizeThreeMaxBlocks>
  <smallGridSizeTiers_DESCRIPTION>Set the various size in blocks for the tiers that determine blip icons used. There are four (4) small grid icons.</smallGridSizeTiers_DESCRIPTION>
  <renderHoloRadarsInSeat>false</renderHoloRadarsInSeat>
  <renderHoloRadarsInSeat_DESCRIPTION>Whether holo table radars should still render if you are in a cockpit that has active hud.</renderHoloRadarsInSeat_DESCRIPTION>
  <renderHoloHologramInSeat>false</renderHoloHologramInSeat>
  <renderHoloHologramsInSeat_DESCRIPTION>Whether holo table holograms should still render if you are in a cockpit that has active hud.</renderHoloHologramsInSeat_DESCRIPTION>
  <blockCountClusterStep>10000</blockCountClusterStep>
  <blockCountClusterStep_DESCRIPTION>Used for setting max and min cluster sizes when building the grid hologram (There is a complex relationship with this and clusterSplitRatio for fidelity). 
 Basically we don't want to draw more than this number of squares on screen for performance so we step up the size of clusters when over this number. 
 A grid with this number of blocks or lower will be set to cluster size 1 (ie 1x1x1). A grid over this number of blocks will be set to size 2 (2x2x2). 
 Because this is cubic after this point it isn't about block count, but about how many clusters would be drawn on screen which is blockCount / (#x#x#) where # = clusterSize. 
 Max cluster size is set to clusterSize +1, Min is set to clusterSize -1 (but with a min of 1). Which allows for more fidelity where blocks are not concentrated but still clusters them larger where possible to reduce draw calls. 
 This provides the biggest performance increase of all the changes I've made. Lowering this value reduces fidelity as it will increase clusterSize sooner. 
 Eg. At blockClusterStep = 10000: a grid of 9999 blocks will range between 1x1x1 and 2x2x2 clusters and a grid of 10001 - 80000 blocks will range between 1x1x1 and 3x3x3 clusters. 
 A grid of 80001 blocks will range between 2x2x2 and 4x4x4 (because the next step is at blockCount / (2x2x2) and we do clusterSize-1 to clusterSize+1). 
 This clustering logic is the largest performance increase we can make, the number of squares drawn is the largest bottleneck. 
 We can adjust this to cluster sooner/later and adjust the splitThreshold to allow more/less fidelity around sparse blocks.</blockCountClusterStep_DESCRIPTION>
  <blockClusterAddlMax>1</blockClusterAddlMax>
  <blockCLusterAddlMax_DESCRIPTION>The value to add to get the max cluster range. eg. for a grid identified as clusterSize = 2, the max would be 3 if this value is 1, meaning it would start at 3x3x3 clusters and size down where sparse.</blockCLusterAddlMax_DESCRIPTION>
  <blockClusterAddlMin>1</blockClusterAddlMin>
  <blockCLusterAddlMin_DESCRIPTION>The value to subtract to get the min cluster range. eg. for a grid identified as clusterSize = 3, the min would be 2 if this value is 1, meaning it wouldn't go smaller than 2x2x2 clusters for sparse regions.</blockCLusterAddlMin_DESCRIPTION>
  <clusterSplitThreshold>0.33</clusterSplitThreshold>
  <clusterSplitThreshold_DESCRIPTION>The fill ratio threshold at which a larger block cluster will be split into smaller clusters. Eg at 0.33 (33%) a larger cluster will only break down to smaller clusters if less than 33% full. 
 So for a 3x3x3 cluster at 8/27 blocks it will break to smaller 2x2x2 and 1x1x1 clusters. At 9/27 blocks it will show as one 3x3x3 cluster. Combined with the blockCountClusterStep this allows drawing larger grids at larger cluster sizes with 
 less fidelity but more performance by reducing the number of block squares drawn on screen. By tweaking the splitThreshold we can add fidelity for sparse clusters, while allowing larger clusters for the rest of the grid.</clusterSplitThreshold_DESCRIPTION>
  <clusterRebuildClustersPerTick>200</clusterRebuildClustersPerTick>
  <clusterRebuildClustersPerTick_DESCRIPTION>The number of block clusters built per game tick on a rebuild of the hologram clusters triggered due to blocks being removed or added. This spreads load over time to reduce stutter. 
 Lowering this value will increase the speed at which add/removal of blocks reprocesses the hologram but increases load. </clusterRebuildClustersPerTick_DESCRIPTION>
  <ticksUntilClusterRebuildAfterChange>100</ticksUntilClusterRebuildAfterChange>
  <ticksUntilClusterRebuildAfterChange_DESCRIPTION>The number of game ticks that must pass after add or remove of a block before re-processing the hologram. Prevents rebuilds until after a period of inactivity to reduce stutter. 
 Note: On taking damage clusters will update integrity/maxIntegrity immediately and thus update hologram color. 
 Loss of blocks in a cluster will also update integrity (or remove if no blocks left), but the hologram won't reprocess fully until after this many ticks have passed. 
Then it will start a rebuild and process clusterRebuildClustersPerTick clusters each tick. 
 If another add/removal occurs that process stops, and only restarts after this number of ticks have passed. 
 Lowering this values will increase the speed at which the hologram reprocess starts. </ticksUntilClusterRebuildAfterChange_DESCRIPTION>
</ModSettings>
```

# Keybindings
You can rebind keys in EDHH_settings.xml inside the save folder (eg \YourSaveFolderLocation\WorldName\Storage\3540066597_EliDangHUD), the enum of available MyKeys are below (Note that mouse clicks are treated special, and have their own settings documented in EDHH_settings.xml).

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
## 2025-09-24 
DONE: Add keybinds for next/previous/nearest target.

## 2025-09-22
DONE: Make the Clustering logic access of the allBlocksDict safer if a key no longer exists due to a block being removed. 

DONE: Fix the Velocity lines checkbox and remove the deprecated Orbit Speed Threshold slider. 

## 2025-09-21 
DONE: Add chat commands to edit/update settings that otherwise must be written to the xml in world storage.

## 2025-09-20
DONE: Dynamic block clustering that uses a range. It will still pick a cluster size based on grid block count, but then can go up and down from there. Sparse regions can be broken down to smaller
clusters, and denser regions will be represented by one larger square. Eg. at cluster size 2 we get 1x1x1, 2x2x2, or 3x3x3 clusters. Based on various settings this can be tweaked. 
Goal is to preserve fidelity for sparse regions, but cluster dense regions for performance. 

DONE: Added occlusion culling for the interior of holograms, so those clusters aren't drawn if they are completely enclosed by other clusters.

DONE: Wrap orbit lines in a customdata setting, and also toggle off completely at global level.

DONE: Fix Target integrity arc direction. Also fix integrity calculations on IntegrityChange and on BlockRemoved. 

DONE: Make unpowered grids have a darker icon on radar

DONE: Make target selection ignore the current target, so it more reliably switches between more than one grid very near to the center of your vision.

DONE: Make ModSettings use custom serializable types instead of VRage types like Vector3D, Vector4, and MyKeys to hopefully help with XMLSerialization errors for some users when saving the game. 

DONE: Make small grid radar sprites slightly smaller.

DONE: Make holo tables only work if they are powered and functional

DONE: Fix bug with power producers/batteries being mixed up, causing issues with the HUD disappearing when a battery powered grid was below a certain power usage.

DONE: Update radar ping logarithmic scaling to be more linear (more accurately reflecting true distance from the player, but still spreads them out when very near to keep radar readable)

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
TODO: Take a pass at the CustomCockpitLogic.cs and make sure all sliders/settings work, and add more as necessary.

TODO: Consider a "recently damaged clusters" dictionary that can be used to animate squares being hit. eg. on damage add to dict, then within 0.5s to 1.0 seconds lerp to a bigger size then back, then remove from dict when complete or re-set if damaged again. 

TODO: Make holograms show more data, weapons, effective range, DPS?

TODO: Make radar sprites show more data for selected target. Eg. when you pick a target the radar screen has an "effective range" bubble for the target. 

TODO: Waypoint system with GPS. Would show on screen and on radar. Could we do something global that syncs between players? So a CIC could set waypoints or even pin radar pings?

TODO: Maybe a lightweight player hud that matches this, we could keep default toolbar but hide other elements. 

TODO: I'd like to do further settings to allow users to more specifically choose to filter our floating items of name 'Stone' for eg, or only voxels (asteroids and deposits) of certain types or sizes even. 

TODO: Consider making this work in Third Person view, as well as for Remote Controlled ships?

TODO (PARTIAL): Make altitude readout - I have the value, but haven't decided where to draw it on screen yet.

TODO: Make radar work on LCD with up or down triangles for verticality for eg. Possibly? 

TODO: Compatibility with frame shift drive? Likely around the jumpdrive mechanic? Will look into this.

TODO: Show modded ammo types in the gauges. Attempted but might have failed? needs testing...

TODO: WeaponCore compatibility - partially attempted, but I really have no idea what I'm doing. I may need to load up the WeaponCore source code to figure out how to hook into it. 

TODO: Advanced SigInt logic where active signals from any ship can bounce around and be picked up in passive mode? So one active ship can feed passive fleet?
This might tie into a much longer term goal of CIC and fleet management content, if I ever do it. 

TODO: Check shield mod integrations, how can we make that universal across shield mods?

# Credits
Polygon cherub for original mod https://steamcommunity.com/sharedfiles/filedetails/?id=3252520404
Ship radar icons from Grandsome https://www.nexusmods.com/x4foundations/mods/895 used with attribution as per requirements. 