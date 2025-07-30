using ParallelTasks;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Planet;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.Game.Lights;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.Game.WorldEnvironment.Modules;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.Entities.Blocks;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Components.Interfaces;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Input;
using VRage.Library.Utils;
using VRage.ModAPI;
//using VRage.ModAPI;
using VRage.Utils;
//using VRage.Input;
using VRageMath;
using VRageRender;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

//---
//Hey, I made this mod because I wanted to learn a bit about the VRage API and make some fun tweaks to SE.
//Thank you to all the people who have shared their own mods over the years that I may follow the pathes you forged.
//Please forgive my foolish and ignorant choices in this script. I had fun!
//
//																					---CHERUB 04-18-2024
//---

/*
FenixPK - 2025-07-13:

To CHERUB: Thank you for your time spent creating this wonderful mod. And thank you for making the code freely available and stating that we can use, reupload etc.
I am interpreting this to mean this is under a GPL license, and will be releasing my modifications under GPL 3.0. 
Free software as in freedom. Freedom to use it, modify it, re-upload it, package it, even sell it etc.

Also, there is no way in heck I could have figured out the UI/artwork and 3D graphics of this (I mean maybe with enough time). I work with office software by day, and scripts by night. So standard UI's are one thing but
anything 3D is over my head. Without your legwork to learn from my changes would be very difficult. 
I spent weeks-months looking for radar mods, and weeks-months configuring WHIPs turret based radar on various LCDs using extended range sensors (raycasting I think?). 
This script has the potential to replace all of that and be far more performance friendly. Especially if I can get this running on LCDs and holotables... And then tie it into vanilla/weaponcore targetting... Ambitious probably.

2025-07-18 HOLY BEJEBUS... 
The whole target hologram thing has been melting my brain, I could get it to do all kinds of different things and ALMOST get what I wanted and no amount of Transforming or Rotating or inverse matrices could solve one problem:
The hologram of the target rotates in world space. So when I turn my ship to the right, the hologram turns to the left showing me a different side of it I couldn't possibly see from this angle... 
When I put the hologram in front of the screen as a test it became readily apparent it rotates because it shares a worldRadarPos/radarMatrix... The BLIPS on the radar screen rotate as your ship does. Of course they do. They're supposed to.
But the hologram shouldn't and sharing a matrix for these is, *probably*, the problem :/ That said I'm in way over my head and there's probably something else going on. Tackling one variable at a time until I narrow this down. 

2025-07-20 I figured out the target hologram.... eventually. But not due to any accomplishment on my part. 
It now has an Orbit cam (that was easy, in fact I think that is what this used to do before I gutted it and broke everything and had to start over). 
And a perspective Cam that handles both perpendicular edge cases and properly negates all of the local grids orientation so it gives you a view of what you'd see if you were looking at your target no matter where you are.
Ie. what side is facing you. In theory this means if it is facing you with weapons pointed right at you, you should be able to tell from the hologram lol.
also I challenged myself to use it to fly around a ship and get behind it without looking at anything but the hologram for reference (it doesn't show distance, so I was pretty far away from it, but I WAS behind it). 

----------------
--CHANGES--
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

--TODO--
TODO: Fix whatever I broke regarding the block position camera dir stuff in drawing the hologram when I switched back to original author code with my block positions. 
I believe it shades it when looking at grid for some reason? Think I fixed this by getting the dotProduct out of there.

TODO: Figure out why the block hotkey I added for turning hud/on off (which is a wip for adding other sliders too) turned the holo hud off, and wouldn't turn it back on??

TODO: Figure out all this DamageAmount/AttachGrid/DetachGrid event handler stuff. It looks... incomplete? Eg GetDamageAmount if called would re-set the amount to zero. It is used to get a damage amount to add to glitch amount overload.
but the way it is configured all that happens prior to this is attaching event handlers for on function changed. So amount would always be 0?
Is this a remnant from presumably older code that used the queue of blocks to check each one for current damage, so it would do something like upon sitting in the control seat of a grid that was already damaged
store that value? 
TODO: Look into use of IMyCockpit vs IMyShipController, didn't we see reports of players wishing it worked for remote controlled ships?
TODO: (PARTIAL) Make player condition hologram better... Could rotate it on a timer to show all sides, and then if taking damage rotate it so it shows the side being impacted? I have standardized the code so it is far easier to modify and maintain
but made no actual changes to it yet.
TODO: Can we color code power, weapon, antenna, control blocks in the target/player hologram? Then apply a yellow/red color gradient overtop for damage. 
TODO: Make altitude readout

TODO: Make broadcast range only affect active signal sent out, and make scale be something else configurable that gets stored in the cockpit's block data that can have up/down toggles added to toolbar. 
No idea how to do this so using the broadcast range for now.

TODO: Make radar work on a holotable, especially if the above is accomplished!
TODO: Make radar work on LCD with up or down triangles for verticality for eg. Only after holotable. 

TODO: Compatibility with frame shift drive? Likely around the jumpdrive mechanic? Will look into this.
TODO: Show modded ammo types in the gauges.
TODO: Ability to key-rebind the targetting instead of right click for eg.
TODO: Do above in concert with weaponcore compat, so targetting is shared somehow in a nice way?

TODO: Advanced SigInt logic where active signals from any ship can bounce around and be picked up in passive mode? So one active ship can feed passive fleet?
TODO: Apparently splitting/merging a ship with merge blocks causes a crash.
TODO: Remote control ship and then get out of antenna range (or have remote controlled ship explode for eg) was causing crash. Another author may have fixed that in this already.
TODO: Check shield mod integrations, how can we make that universal across shield mods?
TODO: Apparently asteroids can cause phantom pings on planets, should try and look into that for Cherub.


NO ACTION: Maybe enable squish value again? If users want that verticality squish we could toggle it in settings and allow specific value. Ie. 0.0 is off, 0.0-1.0 range. 
-Update 2025-07-29, this was already disabled by the original author. It would "squish" the vertical axis for a radar contact making it "easier to read but less vertically accurate."
I prefer accuracy personally and have had no issue with pings that are very far above or below not showing on the screen?

NO ACTION: Rework the glitch code regarding the radar, I removed it when trying to hunt down the performance issues when I first started and had no idea what anything in this code even did. 
-Update 2025-07-29, that glitch code I removed did a few things: for hostile entities only, if greater distance away than 90% of max radar range, add a "glitch" effect that consisted of
a red, blue, and green blip at random offsets instead of an exact blip for the entity. 
Since it only affected hostile contacts one could realize this doesn't happen to friendly/neutral and the color flipping becomes meaningless as you know it's hostile if it glitches at all. 
Then with my changes to radar range being based on antenna broadcast range, and having active vs passive modes, I would probably want to do this entirely differently. 
Perhaps glitch effects only for non passive detections (ie. if the entity isn't broadcasting, then it's location is less accurate), or some kind of EWAR jamming etc.

*/





namespace EliDangHUD
{
	// Define a class to hold planet information
	public class PlanetInfo
	{
		public VRage.ModAPI.IMyEntity Entity { get; set; }
		public double Mass { get; set; }	// We'll use radius as a stand-in for mass
		public double GravitationalRange { get; set; }	// Gravitational range of the planet
		public VRage.ModAPI.IMyEntity ParentEntity { get; set; }	// Parent entity of the planet
	}

	// Define class to contain info about velocity hash marks
	public class VelocityLine
	{
		public float velBirth { get; set; }
		public Vector3D velPosition { get; set; }
		public float velScale { get; set; }
	}

	public enum RelationshipStatus
	{
		Friendly, // Friendly is used for entities that are considered allies, such as friendly factions or the player
		Hostile, // Hostile is used for entities that are considered enemies, such as pirates or hostile factions
        Neutral, // Neutral is used for entities that are not hostile or friendly, such as asteroids or planets
        FObj, // Floating Objects
		Vox // Voxels
	}

	// Define class to hold information about radar targets
	public class RadarPing
	{
		public VRage.ModAPI.IMyEntity Entity { get; set; }
		public Stopwatch Time { get; set; }
		public float Width { get; set; }
		public RelationshipStatus Status { get; set; }
		public bool Announced { get; set; }
		public Vector4 Color { get; set; }
		public bool PlayerCanDetect { get; set; }

        public Vector3D RadarPingPosition { get; set; }
        public double RadarPingDistanceSqr { get; set;  }
        public bool RadarPingHasActiveRadar { get; set; }

        public double RadarPingMaxActiveRange { get; set; }
    }

	public class RadarAnimation
	{
		public VRage.ModAPI.IMyEntity Entity;
		public Stopwatch Time;
		public int Loops;
		public double LifeTime;
		public double SizeStart;
		public double SizeStop;
		public float FadeStart;
		public float FadeStop;
		public Vector3D OffsetStart;
		public Vector3D OffsetStop;
		public Vector4 ColorStart;
		public Vector4 ColorStop;
		public MyStringId Material;
	}

	public class DustParticle
	{
		public MyStringId Material;
		public double life = 0;
		public double lifeTime = 1;
		public Vector3D velocity = Vector3D.Zero;
		public double scale = 1;
		public Vector3D pos = Vector3D.Zero;
	}

    /// <summary>
    /// This enum stores the different views the Target hologram (or to some degree, local hologram) can use.
    /// </summary>
    public enum HologramView_Side
    {
        Rear = 0,
        Left = 1,
        Front = 2,
        Right = 3,
        Top = 4,
        Bottom = 5,
        Orbit = 6,
        Perspective = 7
    }

	/// <summary>
	/// This holds the global settings that will get loaded from and saved to the XML file in the save/world folder. Clients that are NOT also servers request these settings from the server. And upon request servers send them.
	/// Clients that are ALSO servers are single player and just load from file. 
	/// </summary>
    public class ModSettings
    {
        /// <summary>
        /// Max global radar range, setting -1 will use the draw distance. Otherwise you can set a global maximum limit for the radar range. 
		/// I think you could technically exceed the games 50km limit in radar scale, but there is a hard limit at the 50km antenna broadcast range for detecting.
        /// </summary>
        public double maxRadarRangeGlobal = -1;
        public string maxRadarRange_DESCRIPTION = "Max global radar range in meters, setting -1 will use the draw distance. Otherwise you can set a global maximum limit for the radar range. \r\n " +
            "I think you could technically exceed the games 50km limit in radar scale, but there is a hard limit at the 50km antenna broadcast range for detecting.";

        /// <summary>
        /// Represents the distance, in meters, used for range bracketing in radar targeting calculations.
        /// </summary>
        /// <remarks>This value is used to determine the "bracket" a target fits into for zooming the radar. Or when changing broadcast distance of antennas (radar range).
        /// Adjusting this value can affect the precision of radar zoom levels.</remarks>
        public int rangeBracketDistance = 500;
        public string rangeBracketDistance_DESCRIPTION = "Represents the distance in meters used for range bracketing in radar targeting calculations. \r\n" +
            "This value is used to determine the \"bracket\" a target fits into for zooming the radar. Or when changing broadcast distance of antennas (radar range). \r\n" +
            "Adjusting this value can affect the precision of radar zoom levels.";

        /// <summary>
        /// Maximum number of entities/voxels that can be displayed as radar pings on the radar at once.
        /// </summary>
        public int maxPings = 500;
        public string maxPings_DESCRIPTION = "Maximum number of entities/voxels that can be displayed as radar pings on the radar at once.";

        /// <summary>
        /// Percentage threshhold at which radar pings start to fade out at the edge of the radar range Eg. 0.01 = 0.01*100 = 1%. 
		/// Also used for notifying the player of new pings. When pings cross the threshhold distance they will be announced audibly and visually on the radar. 
        /// </summary>
        public double fadeThreshhold = 0.01;
        public string fadeThreshhold_DESCRIPTION = "Percentage threshhold at which radar pings start to fade out at the edge of the radar range Eg. 0.01 = 0.01*100 = 1%. \r\n" +
            " Also used for notifying the player of new pings. \r\n When pings cross the threshhold distance they will be announced audibly and visually on the radar.";

        /// <summary>
        /// The thickness of the lines used for rendering circles and other shapes in the HUD.
        /// </summary>
        public float lineThickness = 1.5f;
        public string lineThickness_DESCRIPTION = "The thickness of the lines used for rendering circles and other shapes in the HUD.";

        /// <summary>
        /// The number of segments used to render circles and other circular shapes in the HUD. Lowering this number can make it more blocky. Eg. setting this to 6 would make it a hexagon instead of a circle. 
        /// </summary>
        public int lineDetail = 90;
        public string lineDetail_DESCRIPTION = "The number of segments used to render circles and other circular shapes in the HUD. \r\n " +
            "Lowering this number can make it more blocky. Eg. setting this to 6 would make it a hexagon instead of a circle.";

        /// <summary>
        /// Default color of the HUD lines, used for rendering circles and other shapes.
        /// </summary>
        public Vector4 lineColor = new Vector4(1f, 0.5f, 0.0f, 1f);
        public string lineColor_DESCRIPTION = "Default color of the HUD lines, used for rendering circles and other shapes.";

        /// <summary>
        /// Position of the star. No idea if we need to change this for "RealStars" mod or not.
        /// </summary>
        public Vector3D starPos = new Vector3D(0, 0, 0);
        public string starPos_DESCRIPTION = "Position of the star. No idea if we need to change this for \"RealStars\" mod or not.";

        /// <summary>
        /// Does the star follow the skybox?
        /// </summary>
        public bool starFollowSky = true;
        public string starFollowSky_DESCRIPTION = "Does the star position follow the skybox?";

        /// <summary>
        /// Enable or disable the white "flares" that glint behind grids in the distance.
        /// </summary>
        /// <remarks>This is what makes a distant ship "glint" in the void of space. Some users like this, as you can see where ships are better.
        /// Others prefer a more realistic environment with this off so you can hide in the void.</remarks>	
        public bool enableGridFlares = true;
        public string enableGridFlares_DESCRIPTION = "Enable or disable the white \"flares\" that glint behind grids in the distance. \r\n" +
            "This is what makes a distant ship \"glint\" in the void of space. Some users like this, as you can see where ships are better. \r\n" +
            "Others prefer a more realistic environment with this off so you can hide in the void.";

        /// <summary>
        /// Enable cockpit dust effects.
        /// </summary>
        public bool enableCockpitDust = true;
        public string enableCockpitDust_DESCRIPTION = "Enable cockpit dust effects.";

        /// <summary>
        /// Show visor effects or not.
        /// </summary>
        public bool enableVisor = true;
        public string enableVisor_DESCRIPTION = "Show visor effects or not.";

        /// <summary>
        /// Whether any kind of Hologram can be shown or not. Each cockpit can override this setting, and turn on/off holograms for the local grid or the target grid separately in the block's custom data.
        /// </summary>
        public bool enableHologramsGlobal = true; 
        public string enableHolograms_DESCRIPTION = "Whether any kind of Hologram can be shown or not. \r\n " +
            "Each cockpit can override this setting, and turn on/off holograms for the local grid or the target grid separately in the block's custom data.";

        /// <summary>
        /// Should the targeting reticle be hollow or have a "dot" in the middle that can sometimes block the view of the target especially if it's a small grid.
        /// </summary>
        public bool useHollowReticle = true;
        public string useHollowReticle_DESCRIPTION = "Should the targeting reticle be hollow or have a \"dot\" in the middle that can sometimes block the view of the target especially if it's a small grid.";

		public int minTargetBlocksCount = 5;
		public string minTargetBlocksCount_DESCRIPTION = "The minimum number of blocks required for a grid to be selected as a target. Helps reduce pieces of debris being selected";
    }

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation)]
	public class CircleRenderer : MySessionComponentBase
	{

		public bool _modInitialized = false;
        private bool _entitiesInitialized = false;
        private bool _audioInitialized = false;
		private bool _customDataInitialized = false;
		private bool _localGridHasPower = false;
		private bool _controlledBlockTaggedForMod = false;
		private double _distanceToCameraSqr = 0;
		private bool _weaponCoreInitialized = false;
		private bool _weaponCoreWeaponsInitialized = false;

        // File to store the settings in, and settings object to hold them.
        private const string settingsFile = "EDHH_settings.xml";
		/// <summary>
		/// This holds the settings that the mod uses globally, not per ship/seat. "theSettings" gets saved to XML in the save world folder.
		/// </summary>
        private ModSettings theSettings = new ModSettings(); // Is instantiated with default values as a new ModSettings object, which is then overwritten by the settings file if it exists.
		// For multiplayer the client requests this from the server. For single player it loads from the saved world folder. 
		// This means I've completely overhauled the code so any reference to static constants like "someConstant" are now "theSettings.someConstant" instead. 
		// Saves having to update 4 places when adding a new setting for eg.

        // Values for sending the file and syncing between server and clients
        private const ushort MessageId = 10203;
        private const ushort RequestMessageId = 30201;

        // Materials used for rendering various elements of the HUD
        // These are configured in the file TransparentMaterials_ED.sbc
        private readonly MyStringId MaterialDust1 = 				MyStringId.GetOrCompute("ED_DUST1");
		private readonly MyStringId MaterialDust2 = 				MyStringId.GetOrCompute("ED_DUST2");
        private readonly MyStringId MaterialDust3 = MyStringId.GetOrCompute("ED_DUST3");
        private readonly MyStringId MaterialVisor = 				MyStringId.GetOrCompute ("ED_visor");
		private readonly MyStringId Material = 					MyStringId.GetOrCompute("Square");
		private readonly MyStringId MaterialLaser = 				MyStringId.GetOrCompute("WeaponLaser");
		private readonly MyStringId MaterialBorder = 			MyStringId.GetOrCompute("ED_Border");
		private readonly MyStringId MaterialCompass = 			MyStringId.GetOrCompute("ED_Compass");
		private readonly MyStringId MaterialCross = 				MyStringId.GetOrCompute("ED_Targetting");
		private readonly MyStringId MaterialCrossOutter = 		MyStringId.GetOrCompute("ED_Targetting_Outter");
		private readonly MyStringId MaterialLockOn = 			MyStringId.GetOrCompute("ED_LockOn");
        private readonly MyStringId MaterialLockOnHollow = MyStringId.GetOrCompute("ED_LockOn_Hollow");
        private readonly MyStringId MaterialToolbarBack = 		MyStringId.GetOrCompute("ED_ToolbarBack");
		private readonly MyStringId MaterialCircle = 			MyStringId.GetOrCompute("ED_Circle");
		private readonly MyStringId MaterialCircleHollow = 		MyStringId.GetOrCompute("ED_CircleHollow");
		private readonly MyStringId MaterialCircleSeeThrough = 	MyStringId.GetOrCompute("ED_CircleSeeThrough");
		private readonly MyStringId MaterialCircleSeeThroughAdd = 	MyStringId.GetOrCompute("ED_CircleSeeThroughAdd");
		private readonly MyStringId MaterialTarget = 			MyStringId.GetOrCompute("ED_TargetArrows");
		private readonly MyStringId MaterialSquare = 			MyStringId.GetOrCompute("ED_Square");
		private readonly MyStringId MaterialTriangle = 			MyStringId.GetOrCompute("ED_Triangle");
		private readonly MyStringId MaterialDiamond = 			MyStringId.GetOrCompute("ED_Diamond");
		private readonly MyStringId MaterialCube = 				MyStringId.GetOrCompute("ED_Cube");
		private readonly MyStringId MaterialShipFlare = 			MyStringId.GetOrCompute("ED_SHIPFLARE");
		private List<string> MaterialFont = 			new List<string> ();


        // Colors for use, sometimes multiplying the Vector4 color by a float to adjust brightness/glow. I believe this has to do with how HDR works
        private Vector4 LINECOLOR_Comp;
		private Vector3 LINECOLOR_Comp_RPG;
		private Vector3 lineColorRGB = new Vector3(1f, 0.5f, 0.0); // Local RGB line color, this is overrwritten by the CustomData per cockpit block.

        private Vector4 color_GridFriend = (Color.Green).ToVector4() * 2;
        private Vector4 color_GridEnemy = (Color.Red).ToVector4() * 4;
        private Vector4 color_GridEnemyAttack = (Color.Pink).ToVector4() * 4;
        private Vector4 color_GridNeutral = (Color.Cyan).ToVector4() * 2;
        private Vector4 color_FloatingObject = (Color.DarkGray).ToVector4();
        private Vector4 color_VoxelBase = (Color.DimGray).ToVector4();

        // Store list of Planets and their details
        public HashSet<VRage.ModAPI.IMyEntity> planetList = new HashSet<VRage.ModAPI.IMyEntity>();
		public List<PlanetInfo> planetListDetails;

		// Constant factor to scale the radius to determine gravitational range
		private const double GravitationalRangeScaleFactor = 10; // Adjust as needed

		// I believe sun rotation axis is used for the radar orientation, basically what is the "world" plane?
		Vector3 SunRotationAxis;

		// Grid helper has handy procedures and stores information about the local grid.
		GridHelper gHandler = new GridHelper();

        // Some global variables to use for dimming?
        public static float GLOW = 1f; // This is overwritten by the configurable brightness settings per cockpit. But is here as a default, it is not something in the mod settings but rather the BLOCK settings.
        public float GlobalDimmer = 1f;
		public float ControlDimmer = 1f;
		public float SpeedDimmer = 1f;


        /// <summary>
        /// Stopwatch timer used for modulating visual pulses on radar, possibly holograms. Shouldn't ever need be restarted as the double stored time elapsed should last thousands of years before overflow
        /// </summary>
        private readonly Stopwatch timerRadarElapsedTimeTotal = new Stopwatch();

        /// <summary>
        /// Used for calculating the elapsed time between ticks of the game (delta). This is used to determine how much time has passed since the last tick, and is used in AfterUpdateSimulation
        /// </summary>
        private readonly Stopwatch timerGameTickDelta = new Stopwatch();

		/// <summary>
		/// Holds how many seconds have passed since the last tick of the game, specifically in AfterUpdateSimulation. It stores the seconds elapsed, then re-starts the timer back to 0 each call of AfterUpdateSimulation.
		/// </summary>
		private double deltaTimeSinceLastTick = 0;

		// Flag that we set to tell us whether the player is seated in a cockpit or not. 
		public bool isSeated = false;

		// Glitch amount variables to be used with the "glitch" visual effect when we are close to or over our power limit.
		private double glitchAmount = 0;
		private double glitchAmount_overload = 0;
		private double glitchAmount_min = 0;

		/// <summary>
		/// At what percent power usage does the glitch effect start?
		/// </summary>
		private double powerLoadGlitchStart = 0.667;

		/// <summary>
		/// What is the grid's current power load, for calculating glitch effect
		/// </summary>
        private double powerLoadCurrent = 0;

        // Random floats for random number generation?
        private List<float> randomFloats = new List<float>();
		private int nextRandomFloat = 0;

		// Track the radar animations
		private List<RadarAnimation> RadarAnimations = new List<RadarAnimation>();

        // The radar variables
        private float min_blip_scale = 0.05f;

        // Variables that can be set by the CUSTOM DATA of the cockpit block to enable/disable various features of the HUD.
        public bool EnableMASTER = true;
		public bool EnableGauges = true;
		public bool EnableMoney = true;
		public bool EnableHolograms = true;
		public bool EnableHolograms_them = true;
		public bool EnableHolograms_you = true;
		public bool EnableDust = true;
		public bool EnableToolbars = true;
		public bool EnableSpeedLines = true;

		public bool HologramView_PerspectiveAttemptOne = false;
		public HologramView_Side HologramView_Current = HologramView_Side.Perspective; // 0 is back, 1 is left, 2 is right, 3 is front, 4 is top, 5 is bottom. 
		private int HologramView_Current_MaxSide = 5; // In case we add more? - Max is still 5. 6 and 7 are reserved for orbit and perspective cams currently.
		public bool HologramView_AngularWiggle = true;
        public bool HologramView_AngularWiggleTarget = true;

		// The Rotation matrices for changing the view of the target or local grid holograms. This allows Slerp'ing smoothly from view to view so it doesn't snap when we switch the side being displayed. 
		public MatrixD hologramRotationMatrixTarget_Current = MatrixD.Identity;
		public MatrixD hologramRotationMatrixTarget_Goal = MatrixD.Identity;
		public MatrixD hologramRotationMatrixLocal_Current = MatrixD.Identity;
		public MatrixD hologramRotationMatrixLocal_Goal = MatrixD.Identity;

		// If we apply angular rotation wiggle to the view or not, basically the faster you are turning the more the hologram turns in that direction. This is only relevant for "fixed" views
		// like back, front, side etc. So you get a sense for which direction the grid is rotating. 
		public MatrixD angularRotationWiggleTarget = MatrixD.Identity;
		public MatrixD angularRotationWiggleLocal = MatrixD.Identity;

		

        // The player
        private IMyPlayer player;
		private bool client; // Is this a client or server? If "dedicated server" is detected then this is true. 

		//================= WEAPON CORE ===============================================================================================
		private bool _isWeaponCore = false;
		public bool IsWeaponCoreLoaded()
		{
			bool isWeaponCorePresent = MyAPIGateway.Session.Mods.Any(mod => mod.PublishedFileId == 3154371364); // Replace with actual WeaponCore ID (as of 2025-07-17 this is accurate)
            _isWeaponCore = isWeaponCorePresent;
			return isWeaponCorePresent;
		}
        //=============================================================================================================================


        //=====================SYNC SETTINGS WITH CLIENTS===============================================================================
        //This entire section is a complete nightmare. Holy moly, I'm out of my depth.
        //I really hope this works.
        // FenixPK 2025-07-17, it appears to work. I did switch things to use the SecureMessage handlers for syncing settings as I was getting warnings the others were deprecated.
        /// <summary>
        /// Initialize the mod. This will sync settings from the server in multiplayer, or load them from the world storage in singleplayer. Or as a dedicated server it loads the settings and registers the event listeners for syncing settings with clients.
        /// </summary>
        /// <param name="sessionComponent"></param>
        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
		{
			client = !MyAPIGateway.Utilities.IsDedicated;

			if (client)
			{
				player = MyAPIGateway.Session.LocalHumanPlayer;
			}
			base.Init(sessionComponent);

			if (MyAPIGateway.Session.IsServer) // This is true even in singleplayer as we are both a client and a server.
			{
				theSettings = LoadSettings();
				//ApplySettings(theSettings); // Since settings are now directly loaded into "theSettings" and that is what is used in the code there is no need to "apply" them. 
				MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(RequestMessageId, OnSettingsRequestReceived); // If we are a server, we register the eventhandler to receive requests for settings.
            }

			MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(MessageId, OnSyncSettingsReceived);
			MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(RequestMessageId, OnSettingsRequestReceived);

			if (!MyAPIGateway.Session.IsServer) // In single player this is skipped because we are a server, makes sense. 
			{
				RequestSettingsFromServer(); // In multiplayer all clients do RequestSettingsFromServer
            }
		}

        /// <summary>
        /// Unregister event listeners and save settings when the mod is unloaded.
        /// </summary>
        public void Unload()
		{
			if (MyAPIGateway.Session.IsServer)
			{
				SaveSettings(theSettings);
				MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(RequestMessageId, OnSettingsRequestReceived);
			}

			MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(MessageId, OnSyncSettingsReceived);
		}

		/// <summary>
		/// Sends a request to the server to retrieve settings.
		/// </summary>
		/// <remarks>This method sends a message to the server using the specified message ID.  It does not return any
		/// data directly; the response is handled asynchronously  by the server and may trigger subsequent events or
		/// callbacks.</remarks>
		private void RequestSettingsFromServer()
		{
			MyAPIGateway.Multiplayer.SendMessageToServer(RequestMessageId, new byte[0]);
		}

		/// <summary>
		/// Handles a settings request received from a client or server.
		/// </summary>
		/// <remarks>This method processes incoming settings requests from clients and synchronizes settings with the sender if the
		/// current session is running on the server.</remarks>
		/// <param name="messageId">The unique identifier of the message associated with the request.</param>
		/// <param name="data">The raw data payload of the request. This may contain serialized settings or other relevant information.</param>
		/// <param name="sender">The unique identifier of the sender of the request. Typically represents the client or server initiating the
		/// request.</param>
		/// <param name="fromServer">A value indicating whether the request originated from the server. <see langword="true"/> if the request is from
		/// the server; otherwise, <see langword="false"/>.</param>
		private void OnSettingsRequestReceived(ushort messageId, byte[] data, ulong sender, bool fromServer)
		{
			if (MyAPIGateway.Session.IsServer)
			{
                SyncSettingsWithClient(sender, theSettings); // Use sender param.
				//var senderId = MyAPIGateway.Multiplayer.MyId; // Get sender ID
				//SyncSettingsWithClient(senderId, theSettings);
			}
		}

		/// <summary>
		/// Synchronizes the specified mod settings with a client by transmitting them in binary format.
		/// </summary>
		/// <remarks>The settings are first serialized to XML format and then converted to binary before transmission.
		/// This ensures compatibility with the underlying messaging system.</remarks>
		/// <param name="clientId">The unique identifier of the client to which the settings will be transmitted.</param>
		/// <param name="settingsToTransmit">The mod settings to be serialized and sent to the client.</param>
		public void SyncSettingsWithClient(ulong clientId, ModSettings settingsToTransmit)
		{
			// Serialize the settings to XML and transmit them to the client.
			string settingsInXML = SerializeSettingsToXML(settingsToTransmit);
            // We transmit binary so we must de-serialize from XML into binary first.
            byte[] binaryToTransmit = MyAPIGateway.Utilities.SerializeToBinary(settingsInXML);
			MyAPIGateway.Multiplayer.SendMessageTo(MessageId, binaryToTransmit, clientId);
		}

        /// <summary>
        /// Handles the receipt of synchronized settings from a remote source. (This would be the callback from RequestSettingsFromServer)
        /// </summary>
        /// <remarks>This method processes the received binary data by deserializing it into XML format and updating
        /// the local settings object. The updated settings are applied directly to the local instance.</remarks>
        /// <param name="messageId">The unique identifier of the message containing the settings.</param>
        /// <param name="data">The binary serialized settings data to be deserialized and applied.</param>
        /// <param name="sender">The identifier of the sender of the message.</param>
        /// <param name="fromServer">A value indicating whether the message originated from the server. <see langword="true"/> if from the server;
        /// otherwise, <see langword="false"/>.</param>
        private void OnSyncSettingsReceived(ushort messageId, byte[] data, ulong sender, bool fromServer)
		{
            // Client receives the binary serialized settings and de-serializes them to XML, then applies them. 
            string xmlData = MyAPIGateway.Utilities.SerializeFromBinary<string>(data); // We receive binary, so it must be transformed into XML first.
            ModSettings receivedSettings = DeserializeSettingsFromXML(xmlData);
			theSettings = receivedSettings; // Update the local settings object with the received settings.
            //ApplySettings(settings); // Since settings are now directly loaded into "theSettings" and that is what is used in the code there is no need to "apply" them. 
        }

		/// <summary>
		/// Serializes the specified settings object into an XML string representation.
		/// </summary>
		/// <remarks>This method uses the <see cref="MyAPIGateway.Utilities.SerializeToXML"/> utility to perform the
		/// serialization. Ensure that the <paramref name="settingsToSerialize"/> object is properly initialized before
		/// calling this method.</remarks>
		/// <param name="settingsToSerialize">The settings object to serialize. Cannot be null.</param>
		/// <returns>A string containing the XML representation of the <paramref name="settingsToSerialize"/> object.</returns>
        public string SerializeSettingsToXML(ModSettings settingsToSerialize)
		{
            // Serialize the settings to XML format
            return MyAPIGateway.Utilities.SerializeToXML(settingsToSerialize);
		}

		/// <summary>
		/// Deserializes the provided XML string into an instance of <see cref="ModSettings"/>.
		/// </summary>
		/// <remarks>This method uses the <c>SerializeFromXML</c> utility to perform the deserialization. Ensure the
		/// XML string adheres to the expected schema of <see cref="ModSettings"/>.</remarks>
		/// <param name="settingsInXML">A string containing the XML representation of the <see cref="ModSettings"/> object. Must be a valid XML format
		/// that matches the structure of <see cref="ModSettings"/>.</param>
		/// <returns>An instance of <see cref="ModSettings"/> populated with the data from the XML string. Returns <see
		/// langword="null"/> if the XML string is invalid or cannot be deserialized.</returns>
		public ModSettings DeserializeSettingsFromXML(string settingsInXML)
		{
            // Deserialize the settings from XML format to return type <ModSettings>
            return MyAPIGateway.Utilities.SerializeFromXML<ModSettings>(settingsInXML);
		}

		/// <summary>
		/// Saves the specified mod settings to persistent storage in XML format.
		/// </summary>
		/// <remarks>The settings are serialized to XML and stored in world-specific storage. This method overwrites
		/// any existing settings file with the same name.</remarks>
		/// <param name="settings">The mod settings to save. Cannot be <see langword="null"/>.</param>
		public void SaveSettings(ModSettings settings)
		{
			string data = SerializeSettingsToXML(settings);
			using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(settingsFile, typeof(ModSettings)))
			{
				writer.Write(data);
			}
		}

        /// <summary>
        /// Loads the mod settings from persistent storage from XML format.
        /// </summary>
        /// <returns>An instance of <see cref="ModSettings"/> populated with data from the XML file in world storage. Or a new one with default values if none exists. </returns>
        public ModSettings LoadSettings()
		{
			if (MyAPIGateway.Utilities.FileExistsInWorldStorage(settingsFile, typeof(ModSettings)))
			{
				using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(settingsFile, typeof(ModSettings)))
				{
					string data = reader.ReadToEnd();
					MyLog.Default.WriteLine($"FENIX_HUD: {data}");
					return DeserializeSettingsFromXML(data);
				}
			}
			return new ModSettings(); // Return default settings if no file exists
		}

		/// <summary>
		/// Calls the base.SaveData() and then saves settings from theSettings to xml in the world save. 
		/// </summary>
        public override void SaveData()
        {
            base.SaveData();

            if (theSettings != null)
            {
                SaveSettings(theSettings);
            }
        }

		/// <summary>
		/// Unsubscribe from event listeners.
		/// </summary>
        protected override void UnloadData()
        {
            UnsubscribeFromEvents();
        }
        //=============================================================================================================================

        

        /// <summary>
        /// This method draws text on the radar HUD.
        /// </summary>
        /// <param name="text">The text</param>
        /// <param name="size">The size of text</param>
        /// <param name="pos">The position to display the text</param>
        /// <param name="dir">The direction the text should be oriented</param>
        /// <param name="color">The color of the text</param>
        /// <param name="dim">Modifier for brightness of the text</param>
        /// <param name="flipUp">If true uses radarMatrix.Forward instead of radarMatrix.Up.</param>
        private void DrawText(string text, double size, Vector3D pos, Vector3D dir, Vector4 color, float dim = 1, bool flipUp = false)
		{
			text = text.ToLower ();
			Vector3D up = radarMatrix.Up;
			if (flipUp) 
			{
				up = radarMatrix.Forward;
			}
			Vector3D left = Vector3D.Cross(up, dir);
			List<string> parsedText = StringToList(text);
			for (int i = 0; i < parsedText.Count; i++) {
				Vector3D offset = -left * (size*i*1.8);
				if (parsedText [i] != " ") {
					DrawQuadRigid(pos + offset, dir, size, getFontMaterial(parsedText[i]), color * GLOW * dim, flipUp);
				}
			}
		}

        /// <summary>
        /// Draw the holographic speed and power guages on the HUD. 
        /// </summary>
        private void DrawGauges()
        {
            DrawPower();
            DrawSpeed();
        }

        /// <summary>
        /// This function draws the power gauge on the HUD. 
        /// </summary>
        private void DrawPower() 
		{

			Vector4 color = theSettings.lineColor;

			if (powerLoadCurrent > powerLoadGlitchStart) 
			{
				float powerLoad_offset = (float)powerLoadCurrent - (float)powerLoadGlitchStart;
				powerLoad_offset /= 0.333f;

				color.X = LerpF(color.X, 1, powerLoad_offset);
				color.Y = LerpF(color.Y, 0, powerLoad_offset);
				color.Z = LerpF(color.Z, 0, powerLoad_offset);
			}

			double powerLoadCurrentPercent = Math.Round(powerLoadCurrent*100);
			double size = 0.0070;
			string powerLoadCurrentPercentString = powerLoadCurrentPercent.ToString();
			if (powerLoadCurrentPercent < 100) 
			{
				powerLoadCurrentPercentString = " " + powerLoadCurrentPercentString;
			}
			if (powerLoadCurrentPercent < 10) 
			{
				powerLoadCurrentPercentString = " " + powerLoadCurrentPercentString;
			}
			powerLoadCurrentPercentString = "~" + powerLoadCurrentPercentString + "%";

			DrawText(powerLoadCurrentPercentString, size, _powerLoadCurrentPercentPosition, _powerLoadCurrentPercentDirection, color);
			DrawText(" 000 ", size, _powerLoadCurrentPercentPosition, _powerLoadCurrentPercentDirection, color, 0.333f);

			double powerSeconds = (double)gHandler.localGridPowerHoursRemaining * 3600;
			string powerSecondsS = "~" + FormatSecondsToReadableTime(powerSeconds);
			DrawText(powerSecondsS, _powerSecondsRemainingSizeModifier, _powerSecondsRemainingPosition, radarMatrix.Forward, LINECOLOR_Comp, 1);

			float powerPer = 60 * (gHandler.localGridPowerStored / gHandler.localGridPowerStoredMax);

			//----ARC----
			float arcLength = 56f*(float)powerLoadCurrent;
			float arcLengthTime = 70f*(float)powerLoadCurrent;
			DrawArc(worldRadarPos-radarMatrix.Up*0.0025, radarRadius*1.27, radarMatrix.Up, 35, 35+arcLengthTime, color, 0.007f, 0.5f);
			DrawArc(worldRadarPos-radarMatrix.Up*0.0025, radarRadius*1.27 + 0.01, radarMatrix.Up, 42, 42+powerPer, LINECOLOR_Comp, 0.002f, 0.75f);
		}

		double maxSpeed;
        /// <summary>
        /// This function draws the speed gauge on the HUD.
        /// </summary>
        private void DrawSpeed()
		{
			maxSpeed = Math.Max(maxSpeed, gHandler.localGridSpeed);

			double PL = Math.Round(gHandler.localGridSpeed);
			double size = 0.0070;
			string units = "m";
			if (PL > 1000) {
				PL = Math.Round(PL / 100);
				PL = PL / 10;
				units = "k";
			}

			string PLS = PL.ToString ();
			PLS += units;

			int dif = 5 - PLS.Length;
			for (int j = 0 ; j < dif ; j++){
				PLS = " " + PLS;
			}

			DrawText (PLS, size, _speedPosition, _speedDirection, theSettings.lineColor);
			DrawText ("0000 ", size, _speedPosition, _speedDirection, theSettings.lineColor, 0.333f);

			//----ARC----
			float arcLength = (float)(gHandler.localGridSpeed/maxSpeed);
			arcLength = Clamped(arcLength, 0, 1);
			arcLength *= 70;
			DrawArc(worldRadarPos-radarMatrix.Up*0.0025, radarRadius*1.27, radarMatrix.Up, 360-30-arcLength, 360-30, theSettings.lineColor, 0.007f, 0.5f);

			
			double powerSeconds = (double)gHandler.H2powerSeconds;
			string powerSecondsS = "@" + FormatSecondsToReadableTime(powerSeconds);
			Vector3D hydrogenSecondsRemainingPos = _hydrogenSecondsRemainingPosition + radarMatrix.Left * powerSecondsS.Length * _hydrogenSecondsRemainingSizeModifier * 1.8;
			DrawText (powerSecondsS, _hydrogenSecondsRemainingSizeModifier, hydrogenSecondsRemainingPos, radarMatrix.Forward, LINECOLOR_Comp, 1);

			float powerPer = 56f*gHandler.H2Ratio;
			DrawArc(worldRadarPos-radarMatrix.Up*0.0025, radarRadius*1.27 + 0.01, radarMatrix.Up, 360-37-powerPer, 360-37, LINECOLOR_Comp, 0.002f, 0.75f);
		}

        /// <summary>
        /// Populates the MaterialFont list with the names of the materials used for rendering text in the HUD. These come from the transparent materials file TransparentMaterials_ED.sbc.
        /// </summary>
        private void PopulateFonts(){
			int num = 51;

			for (int y = 0; y < num; y++) {
				string name = "ED_FONT_" + Convert.ToString (y);
				MaterialFont.Add(name);
			}
		}

        /// <summary>
        /// Returns the MyStringId for the specified character, used for rendering text in the HUD.
        /// </summary>
        /// <param name="S">The character to get the material for.</param>
        /// <returns>The MyStringId of the material corresponding to the character.</returns>
        /// </summary>
        /// <param name="S"></param>
        /// <returns></returns>
        private MyStringId getFontMaterial(string S){
			MyStringId mat;
			switch (S)
			{
				case "0":
					mat = MyStringId.GetOrCompute(MaterialFont[0]);
					break;
				case "1":
					mat = MyStringId.GetOrCompute(MaterialFont[1]);
					break;
				case "2":
					mat = MyStringId.GetOrCompute(MaterialFont[2]);
					break;
				case "3":
					mat = MyStringId.GetOrCompute(MaterialFont[3]);
					break;
				case "4":
					mat = MyStringId.GetOrCompute(MaterialFont[4]);
					break;
				case "5":
					mat = MyStringId.GetOrCompute(MaterialFont[5]);
					break;
				case "6":
					mat = MyStringId.GetOrCompute(MaterialFont[6]);
					break;
				case "7":
					mat = MyStringId.GetOrCompute(MaterialFont[7]);
					break;
				case "8":
					mat = MyStringId.GetOrCompute(MaterialFont[8]);
					break;
				case "9":
					mat = MyStringId.GetOrCompute(MaterialFont[9]);
					break;
				case "a":
					mat = MyStringId.GetOrCompute(MaterialFont[10]);
					break;
				case "b":
					mat = MyStringId.GetOrCompute(MaterialFont[11]);
					break;
				case "c":
					mat = MyStringId.GetOrCompute(MaterialFont[12]);
					break;
				case "d":
					mat = MyStringId.GetOrCompute(MaterialFont[13]);
					break;
				case "e":
					mat = MyStringId.GetOrCompute(MaterialFont[14]);
					break;
				case "f":
					mat = MyStringId.GetOrCompute(MaterialFont[15]);
					break;
				case "g":
					mat = MyStringId.GetOrCompute(MaterialFont[16]);
					break;
				case "h":
					mat = MyStringId.GetOrCompute(MaterialFont[17]);
					break;
				case "i":
					mat = MyStringId.GetOrCompute(MaterialFont[18]);
					break;
				case "j":
					mat = MyStringId.GetOrCompute(MaterialFont[19]);
					break;
				case "k":
					mat = MyStringId.GetOrCompute(MaterialFont[20]);
					break;
				case "l":
					mat = MyStringId.GetOrCompute(MaterialFont[21]);
					break;
				case "m":
					mat = MyStringId.GetOrCompute(MaterialFont[22]);
					break;
				case "n":
					mat = MyStringId.GetOrCompute(MaterialFont[23]);
					break;
				case "o":
					mat = MyStringId.GetOrCompute(MaterialFont[24]);
					break;
				case "p":
					mat = MyStringId.GetOrCompute(MaterialFont[25]);
					break;
				case "q":
					mat = MyStringId.GetOrCompute(MaterialFont[26]);
					break;
				case "r":
					mat = MyStringId.GetOrCompute(MaterialFont[27]);
					break;
				case "s":
					mat = MyStringId.GetOrCompute(MaterialFont[28]);
					break;
				case "t":
					mat = MyStringId.GetOrCompute(MaterialFont[29]);
					break;
				case "u":
					mat = MyStringId.GetOrCompute(MaterialFont[30]);
					break;
				case "v":
					mat = MyStringId.GetOrCompute(MaterialFont[31]);
					break;
				case "w":
					mat = MyStringId.GetOrCompute(MaterialFont[32]);
					break;
				case "x":
					mat = MyStringId.GetOrCompute(MaterialFont[33]);
					break;
				case "y":
					mat = MyStringId.GetOrCompute(MaterialFont[34]);
					break;
				case "z":
					mat = MyStringId.GetOrCompute(MaterialFont[35]);
					break;
				case ".":
					mat = MyStringId.GetOrCompute(MaterialFont[36]);
					break;
				case "!":
					mat = MyStringId.GetOrCompute(MaterialFont[37]);
					break;
				case "?":
					mat = MyStringId.GetOrCompute(MaterialFont[38]);
					break;
				case "-":
					mat = MyStringId.GetOrCompute(MaterialFont[39]);
					break;
				case "/":
					mat = MyStringId.GetOrCompute(MaterialFont[40]);
					break;
				case "%":
					mat = MyStringId.GetOrCompute(MaterialFont[41]);
					break;
				case "~":
					mat = MyStringId.GetOrCompute(MaterialFont[42]);
					break;
				case "@":
					mat = MyStringId.GetOrCompute(MaterialFont[43]);
					break;
				case "$":
					mat = MyStringId.GetOrCompute(MaterialFont[44]);
					break;
				case ":":
					mat = MyStringId.GetOrCompute(MaterialFont[45]);
					break;
				case "|":
					mat = MyStringId.GetOrCompute(MaterialFont[46]);
					break;
				case "[":
					mat = MyStringId.GetOrCompute(MaterialFont[47]);
					break;
				case "]":
					mat = MyStringId.GetOrCompute(MaterialFont[48]);
					break;
				case "(":
					mat = MyStringId.GetOrCompute(MaterialFont[49]);
					break;
				case ")":
					mat = MyStringId.GetOrCompute(MaterialFont[50]);
					break;
				default:
					// Invalid component
					mat = MyStringId.GetOrCompute(MaterialFont[39]);
					break;
			}
			return mat;
		}

		private static List<string> StringToList(string input)
		{
			// Convert each character to a string and add to a list
			List<string> letters = input.Select(c => c.ToString()).ToList();
			return letters;
		}

		private List<DustParticle> dustList = new List<DustParticle>();
		private void UpdateDust()
		{
			int dustAmount = 32;

			if (dustList.Count < dustAmount) {
				for (int i = 0; i < dustAmount - dustList.Count; i++) {
					//Generate Dust
					dustList.Add(FormatDust());
				}
			}
				
			if (dustList.Count > 0) {
				//update Dust
				for(var i = 0; i < dustList.Count; i++) {
					dustList[i].life += deltaTimeSinceLastTick;

					if (dustList[i].life >= dustList[i].lifeTime *0.985) {
						dustList[i] = FormatDust();
					}
				}
			}
		}

		private DustParticle FormatDust()
		{
			DustParticle dust = new DustParticle();
			dust.life = 0;
			dust.Material = MaterialDust1;
			double whichMat = Math.Round(GetRandomDouble() * 3);
			if (whichMat > 1) {
				dust.Material = MaterialDust2;
			} else if (whichMat > 2) {
				dust.Material = MaterialDust3;
			}
			dust.lifeTime = GetRandomDouble() * 8 + 2;
			dust.velocity = new Vector3D ((GetRandomDouble()-0.5)*2, (GetRandomDouble()-0.5)*2, (GetRandomDouble()-0.5)*2);
			dust.velocity = Vector3D.Normalize(dust.velocity) * (GetRandomDouble() * 0.1);
			dust.scale = GetRandomDouble() * 0.1 + 0.025;
			dust.pos = new Vector3D ((GetRandomDouble()-0.5)*2, (GetRandomDouble()-0.5)*2, (GetRandomDouble()-0.5)*2);
			dust.pos *= 0.25;

			return dust;
		}

		private void DrawDust()
		{
			if (dustList.Count > 0)
			{
				//update Dust
				for (var i = 0; i < dustList.Count; i++)
				{
					float alpha = (float)(dustList[i].life / dustList[i].lifeTime);
					alpha = (alpha - 0.5f) * 2;
					alpha = Math.Abs(alpha);
					alpha = 1 - alpha;
					alpha = (float)(Math.Pow((double)alpha, 0.5)) * 0.5f;

					alpha = Clamped(alpha, 0.001f, 1);

					Vector3D pos = dustList[i].pos + (dustList[i].life * dustList[i].velocity) * 0.125;
					pos = Vector3D.Transform(pos, radarMatrix);
					Vector4 color = new Vector4(1, 1, 1, 1) * alpha * 0.5f;

					Vector3D dir = MyAPIGateway.Session.Camera.WorldMatrix.Up;
					Vector3D lef = MyAPIGateway.Session.Camera.WorldMatrix.Left;

					double scale = dustList[i].scale;// * (double)alpha;
													 //scale *= scale;

					//DrawQuad (pos, MyAPIGateway.Session.Camera.WorldMatrix.Backward, dustList[i].scale, dustList[i].Material, color);
					MyTransparentGeometry.AddBillboardOriented(dustList[i].Material, color, pos, lef, dir, (float)scale, BlendTypeEnum.AdditiveTop);
				}
			}
		}

		private void DeleteDust()
		{
			dustList.Clear();
		}

		//RANDOM=============================================================================
		/// <summary>
		/// Populates a list of random float values for use in getting random floats, doubles (by casting) or bools. We do this now so we can reference randoms in Draw thread etc. without getting new ones all the time which is computationally expensive.
		/// You could think of this like generating a new seed at the start of the mod/game which we use for randomness throughout the session. 
		/// </summary>
		private void PopulateRandoms()
		{
			randomFloats.Clear();
			int totalRands = 337; // Change from 33 to 337 which is a prime number. Since we are not computing this each call we want more values than 33 so as not to have noticeable patterns.
			// With 337 there are only two common factors, 1 and 337 so it only syncs with other game mechanics (FPS for eg) every 337 frames. 
			for(int i = 0 ; i < totalRands ; i++)
			{
				randomFloats.Add(MyRandom.Instance.NextFloat());
			}
		}

		/// <summary>
		/// Gets a float from our random float list
		/// </summary>
		/// <returns></returns>
		public float GetRandomFloat()
		{
			float value = randomFloats[nextRandomFloat];
			nextRandomFloat += 1;
			if (nextRandomFloat >= randomFloats.Count) 
			{
				nextRandomFloat = 0;
			}
			return value;
		}


		/// <summary>
		/// Casts a float from our random float list to double.
		/// </summary>
		/// <returns></returns>
		public double GetRandomDouble()
		{
			float value = randomFloats[nextRandomFloat];
			nextRandomFloat += 1;
			if (nextRandomFloat >= randomFloats.Count) 
			{
				nextRandomFloat = 0;
			}
			return (double)value;
		}

		/// <summary>
		/// Returns true or false depending on rounding result of float from our float list. The list of floats is populated between 0 and 1 by using MyRandom.Instance.NextFloat() this checks the float after rounding to nearest integer.
		/// So below 0.5 becomes false, above 0.5 becomes true, and with bankers rounding 0.5 exactly is also false. So it's 51% false and 49% true. Assuming our floats are evenly distributed.  
		/// </summary>
		/// <returns></returns>
		public bool GetRandomBoolean()
		{
			float value = randomFloats[nextRandomFloat];
			nextRandomFloat += 1;
			if (nextRandomFloat >= randomFloats.Count) 
			{
				nextRandomFloat = 0;
			}
			return Convert.ToBoolean((int)Math.Round(value));
		}

        /// <summary>
        /// Starts deltaTimer if not already running. Stores the current elapsed time as deltaTime, then restarts the timer back to 0. 
        /// </summary>
        public void UpdateElapsedTimeDeltaTimer()
        {
            if (!timerGameTickDelta.IsRunning)
            {
                timerGameTickDelta.Start();
            }
            deltaTimeSinceLastTick = timerGameTickDelta.Elapsed.TotalSeconds;
            timerGameTickDelta.Restart();
        }
        //-----------------------------------------------------------------------------------


        //LOAD DATA==========================================================================
        /// <summary>
        /// Load initial mod settings, and values. This occurs once at load of mod (start of game). 
        /// </summary>
        public override void LoadData()
		{	
			base.LoadData();

			SubscribeToEvents();

			randomFloats.Add(0);

			timerGameTickDelta.Start(); // Delta timer for time passed since last tick
			timerRadarElapsedTimeTotal.Start(); // Timer for radar pulse effects and other animations. Stores elapsed time
            timerElapsedTimeSinceLastSound.Start(); // Delta timer for time passe since last sound

			PopulateFonts();
            PopulateRandoms(); // Lets do this ONCE here to save literally thousands of CPU calls per tick like it was doing in UpdateBeforeSimulation haha. 

            LINECOLOR_Comp_RPG = secondaryColor(lineColorRGB)*2 + new Vector3(0.01f,0.01f,0.01f);
			LINECOLOR_Comp = new Vector4 (LINECOLOR_Comp_RPG, 1f);

            // Handle variables set based on the settings XML file inside the world folder. If they can't be used directly or need more initialization logic first.
            if (!MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                // True single player - use full range
                if (theSettings.maxRadarRangeGlobal == -1)
				{
                    maxRadarRange = 50000; // Hard limit == broadcast distance 
                }
				else 
				{
                    maxRadarRange = Math.Min(theSettings.maxRadarRangeGlobal, 50000); // Hard limit == broadcast distance 
                }
            }
            else
            {
                // Any multiplayer scenario - respect sync distance
                if (theSettings.maxRadarRangeGlobal == -1)
				{
                    maxRadarRange = MyAPIGateway.Session.SessionSettings.SyncDistance; // Use sync distance directly.
                }
				else 
				{
                    maxRadarRange = Math.Min(theSettings.maxRadarRangeGlobal, MyAPIGateway.Session.SessionSettings.SyncDistance); // Use limit OR sync distance, whichever is lower. As entities wont sync below this distance anyway.
                }	
            }

            if (theSettings.starFollowSky) {
				//Thank you Rotate With Skybox Mod
				if (MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Session.IsServer)
				{ 
					return; 
				}
				if (!MyAPIGateway.Session.SessionSettings.EnableSunRotation)
				{ 
					return; 
				}
				MyObjectBuilder_Sector saveOB = MyAPIGateway.Session.GetSector ();

				Vector3 baseSunDir;
				Vector3.CreateFromAzimuthAndElevation (saveOB.Environment.SunAzimuth, saveOB.Environment.SunElevation, out baseSunDir);
				baseSunDir.Normalize ();

				SunRotationAxis = (!(Math.Abs (Vector3.Dot (baseSunDir, Vector3.Up)) > 0.95f)) ? Vector3.Cross (Vector3.Cross (baseSunDir, Vector3.Up), baseSunDir) : Vector3.Cross (Vector3.Cross (baseSunDir, Vector3.Left), baseSunDir);
				SunRotationAxis.Normalize ();
			}
		}
		//-----------------------------------------------------------------------------------

		public MatrixD _cameraMatrix;
		public Vector3D _cameraPosition;

        public MatrixD _headMatrix;
        public Vector3D _headPosition;

        public Vector3D _powerLoadCurrentPercentPosition;
		public Vector3D _powerLoadCurrentPercentDirection;
        public Vector3D _powerSecondsRemainingPosition;
        public double _powerSecondsRemainingSizeModifier = 0.0045;

        public Vector3D _speedPosition;
		public Vector3D _speedDirection;
		public Vector3D _hydrogenSecondsRemainingPosition;
		public double _hydrogenSecondsRemainingSizeModifier = 0.0045;

		public Vector3D _hologramPositionRight;
		public Vector3D _hologramPositionLeft;

		public bool _playerHasPassiveRadar;
		public bool _playerHasActiveRadar;
		public double _playerMaxPassiveRange;
		public double _playerMaxActiveRange;

		public Vector3D _currentTargetPositionReturned;
		public bool _playerCanDetectCurrentTarget;
		public double _distanceToCurrentTargetSqr;
        public double _distanceToCurrentTarget;
        public bool _currentTargetHasActiveRadar;
		public double _currentTargetMaxActiveRange;

		public Vector3D _radarCurrentRangeTextPosition;

		public double _fadeDistance;
        public double _fadeDistanceSqr; 
		public double _radarShownRangeSqr;

        /// <summary>
        /// Gets the camera matrix and position in the world and stores them to class variables. This should be called in Draw() for best results. 
        /// </summary>
        public void UpdateCameraPosition()
        {
            // CAMERA POSITION //
            // Get the camera's vectors
            _cameraMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            _cameraPosition = MyAPIGateway.Session.Camera.Position;
        }

        /// <summary>
        /// Updates the positions of UI elements based on the current camera and player head position. These values are stored in class variables that can be used later by the render pipeline in Draw() 
        /// </summary>
        public void UpdateUIPositions() 
		{

            //Head Location=================================================
            // Get the player's character entity
            _headPosition = Vector3D.Zero;
            IMyCharacter character = MyAPIGateway.Session.Player.Character;

            if (character != null)
            {
                // Extract the head matrix
                _headMatrix = character.GetHeadMatrix(true);

                // Get the head position from the head matrix
                _headPosition = _headMatrix.Translation;
                _headPosition -= gHandler.localGridControlledEntityPosition;
            }
			//==============================================================


			// COCKPIT RADAR SCREEN POSITION AND OFFSET //
			// Apply the radar offset to the ship's position
			radarMatrix = gHandler.localGridControlledEntityMatrix; // Set the radar matrix = the localGrid controlled entity matrix, ie. the cockpit or control seat.
            worldRadarPos = Vector3D.Transform(radarOffset, radarMatrix) + _headPosition;

            // Radar scale/range
            _radarCurrentRangeTextPosition = worldRadarPos + radarMatrix.Up * -0.1 + radarMatrix.Left * 0.3;

            // Positions
            // Power Load / Power Remaining
            _powerLoadCurrentPercentPosition = worldRadarPos + (radarMatrix.Forward * radarRadius * 0.4) + (radarMatrix.Left * radarRadius * 1.3) + (radarMatrix.Up * 0.0085);
            _powerLoadCurrentPercentDirection = Vector3D.Normalize(_powerLoadCurrentPercentPosition - worldRadarPos);
            _powerLoadCurrentPercentDirection = Vector3D.Normalize((_powerLoadCurrentPercentDirection + radarMatrix.Forward) / 2);
            _powerSecondsRemainingPosition = worldRadarPos + radarMatrix.Left * radarRadius * 0.88 + radarMatrix.Backward * radarRadius * 1.1 + radarMatrix.Down * _powerSecondsRemainingSizeModifier * 2;

			// Speed / Hydrogen Remaining
            double speedSize = 0.0070;
            _speedPosition = worldRadarPos + (radarMatrix.Forward * radarRadius * 0.4) + (radarMatrix.Right * radarRadius * 1.3) + (radarMatrix.Up * 0.0085);
            _speedDirection = Vector3D.Normalize(_speedPosition - worldRadarPos);
            _speedDirection = Vector3D.Normalize((_speedDirection + radarMatrix.Forward) / 2);
            Vector3D left = Vector3D.Cross(radarMatrix.Up, _speedDirection);
            _speedPosition = (left * speedSize * 7) + _speedPosition;
			_hydrogenSecondsRemainingPosition = worldRadarPos + radarMatrix.Right * radarRadius * 0.88 + radarMatrix.Backward * radarRadius * 1.1 + radarMatrix.Down * _hydrogenSecondsRemainingSizeModifier * 2;

            // Holograms
            _hologramPositionRight = worldRadarPos + radarMatrix.Left * -radarRadius + radarMatrix.Left * _hologramRightOffset_HardCode.X + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
            _hologramPositionLeft = worldRadarPos + radarMatrix.Left * -radarRadius * -1 + radarMatrix.Left * _hologramRightOffset_HardCode.X * -1 + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
			// Could add further user customization of the offset here.


            // COCKPIT TARGET HOLOGRAM SCREEN POSITION AND OFFSET //
            // These might be removed, or at least replaced with a logic where we use the radarPos first and then offset the holograms from there. 
            cockpitMatrix = gHandler.localGridControlledEntity.WorldMatrix;
            // Now we want to discard yaw and keep pitch and roll. The holograms should not rotate as the ship rotates, they'll be displayed in a fixed space in front of you in your cockpit. But as the ship rolls
            // around the forward axis or pitches nose up or down the position needs to stay relative to the forward of the ship (block) essentially. 
            // As I typed that I already foresee an issue with ops stations that face left/right on your ship, will that affect things if we use the cockpit forward instead of grid forward? No... maybe?
            cockpitForward = Vector3D.Normalize(new Vector3D(0, cockpitMatrix.Forward.Y, cockpitMatrix.Forward.Z)); // Remove X which is yaw.... I think? But we still need pitch right.
            cockpitUp = cockpitMatrix.Up; // This preserves roll, up will orient based on the cockpits orientation. 
            cockpitRight = Vector3D.Normalize(Vector3D.Cross(cockpitUp, cockpitForward));
            cockpitUp = Vector3D.Normalize(Vector3D.Cross(cockpitForward, cockpitRight)); // re-orthagonalize up vector.

            // Set the hologram matrix so it has roll and pitch but NOT yaw. Holograms don't need to spin around as your ship spins around, but do need to stay stable in front of you. 
            targetHologramMatrix = MatrixD.CreateWorld(Vector3D.Zero, cockpitForward, cockpitUp);
            localHologramMatrix = MatrixD.CreateWorld(Vector3D.Zero, cockpitForward, cockpitUp);

            // Offsets for the local worldspace to set where these UI controls appear, should be configurable. 
            targetHologramOffset = new Vector3D(-0.5, -0.2, -1.5);
            localHologramOffset = new Vector3D(0.5, -0.2, -1.5);

            // Set these offsets using the ORIGINAL cockpit matrix, so offset stays relative to cockpit.
            targetHologramWorldPos = Vector3D.Transform(targetHologramOffset, cockpitMatrix);
            localHologramWorldPos = Vector3D.Transform(localHologramOffset, cockpitMatrix);
            targetHologramMatrix.Translation = targetHologramWorldPos;
            localHologramMatrix.Translation = localHologramWorldPos;



        }

		/// <summary>
		/// Update the local grid's altitude if in gravity. 
		/// </summary>
		public void UpdateAltitude()
		{
            Vector3D gravity = gHandler.localGrid.Physics?.Gravity ?? Vector3D.Zero;
            bool isInGravity = gravity.LengthSquared() > 0.0001;
			if (isInGravity)
			{
				MyPlanet planet = MyGamePruningStructure.GetClosestPlanet(gHandler.localGridPosition);
				Vector3D surfacePosition = planet.GetClosestSurfacePointGlobal(gHandler.localGridPosition);
				gHandler.localGridAltitude = Vector3D.Distance(gHandler.localGridPosition, surfacePosition);
			}
			else 
			{
				gHandler.localGridAltitude = 0d;
			}

        }



        /// <summary>
        /// Updates radar detections based on the player's current grid's radar capabilities in relation to the entities. Ie. if player is in passive mode and entities nearby have active radar on. Also updates the current target selection.
        /// </summary>
        public void UpdateRadarDetections()
		{

            // Check radar active/passive status and broadcast range for player grid.
            // playerGrid is set once earlier in the Draw() method when determining if cockpit is eligible, player controlled etc, and is used to get power draw among other things. Saved for re-use here and elsewhere.
            EvaluateGridAntennaStatus(gHandler.localGrid, out _playerHasPassiveRadar, out _playerHasActiveRadar, out _playerMaxPassiveRange, out _playerMaxActiveRange);

            // Radar distance/scale should be based on our highest functional, powered antenna (regardless of mode).
            radarScaleRange = _playerMaxPassiveRange; // Already limited to global max. Uses passive only because in active mode active and passive will be equal. 

            _currentTargetPositionReturned = new Vector3D(); // Will reuse this.
            if (currentTarget != null && !currentTarget.Closed)
            {
                // In here we will check if the player can still actively or passively target the current target and release them if not, or adjust range brackets accordingly if still targetted. 
                CanGridRadarDetectEntity(_playerHasPassiveRadar, _playerHasActiveRadar, _playerMaxPassiveRange, _playerMaxActiveRange, gHandler.localGridPosition,
                    currentTarget, out _playerCanDetectCurrentTarget, out _currentTargetPositionReturned, out _distanceToCurrentTargetSqr, out _currentTargetHasActiveRadar, out _currentTargetMaxActiveRange);

                if (_playerCanDetectCurrentTarget)
                {
                    if (debug)
                    {
                        //MyLog.Default.WriteLine($"FENIX_HUD: Target selected, and can detect");
                    }
                    _distanceToCurrentTarget = Math.Sqrt(_distanceToCurrentTargetSqr);
                    radarScaleRange_Goal = GetRadarScaleBracket(_distanceToCurrentTarget);
                    squishValue_Goal = 0.0;
					double distanceToCamera = Vector3D.DistanceSquared(_cameraPosition, _currentTargetPositionReturned); // Additional check on target distance to our CAMERA. 
                    if (distanceToCamera > 50000d*50000d)
                    {
                        ReleaseTarget();
                    }
                }
                else
                {
                    if (debug)
                    {
                        //MyLog.Default.WriteLine($"FENIX_HUD: Target selected, but playerCanDetect is false - clearing target");
                    }
                    ReleaseTarget();
                    radarScaleRange_Goal = GetRadarScaleBracket(radarScaleRange);
                    squishValue_Goal = 0.75;
                }
            }
            else
            {
                if (debug)
                {
                    //MyLog.Default.WriteLine($"FENIX_HUD: No Target found");
                }
                radarScaleRange_Goal = GetRadarScaleBracket(radarScaleRange);
                squishValue_Goal = 0.75;
            }
            squishValue = LerpD(squishValue, squishValue_Goal, 0.1);

            // Handle smooth animation when first starting up or sitting in seat, along with smooth animation for switching range bracket due to targetting. 
            radarScaleRange_CurrentLogin = LerpD(radarScaleRange_CurrentLogin, radarScaleRange_GoalLogin, 0.01);
            radarScaleRange_Current = LerpD(radarScaleRange_Current, radarScaleRange_Goal, 0.1);
            radarScaleRange = radarScaleRange_Current * radarScaleRange_CurrentLogin;

            radarScale = (radarRadius / radarScaleRange);

            _fadeDistance = radarScaleRange * (1 - theSettings.fadeThreshhold); // Eg at 0.01 would be 0.99%. For a radar distance of 20,000m this means the last 19800-19999 becomes fuzzy/dims the blip.
            _fadeDistanceSqr = _fadeDistance * _fadeDistance; // Sqr for fade distance in comparisons.
            _radarShownRangeSqr = radarScaleRange * radarScaleRange; // Anything over this range, even if within sensor range, can't be drawn on screen. 

			// Go up to count OR max pings, so we don't process too many pings on the radar. 
            for (int i = 0; i < Math.Min(radarPings.Count, theSettings.maxPings); i++)
            {
                VRage.ModAPI.IMyEntity entity = radarPings[i].Entity;
                if (entity == null)
                {
                    radarPings.RemoveAt(i);
                    continue; // Clear the ping then continue
                }
                if (!ShowVoxels && radarPings[i].Status == RelationshipStatus.Vox)
                {
                    continue; // Skip voxels if not set to show them. 
                }
                if (entity.GetTopMostParent() == gHandler.localGridControlledEntity.GetTopMostParent())
                {
                    continue; // Skip drawing yourself on the radar.
                }

                bool playerCanDetect = false;
                Vector3D entityPos;
                double entityDistanceSqr;
                bool entityHasActiveRadar = false;
                double entityMaxActiveRange = 0;

                CanGridRadarDetectEntity(_playerHasPassiveRadar, _playerHasActiveRadar, _playerMaxPassiveRange, _playerMaxActiveRange, gHandler.localGridPosition,
                    entity, out playerCanDetect, out entityPos, out entityDistanceSqr, out entityHasActiveRadar, out entityMaxActiveRange);

				radarPings[i].PlayerCanDetect = playerCanDetect;
				radarPings[i].RadarPingPosition = entityPos;
                radarPings[i].RadarPingDistanceSqr = entityDistanceSqr;
                radarPings[i].RadarPingHasActiveRadar = entityHasActiveRadar;
				radarPings[i].RadarPingMaxActiveRange = entityMaxActiveRange;

                if (!playerCanDetect || entityDistanceSqr > _radarShownRangeSqr) // If can't detect it, or the radar scale prevents showing it move on.
                {
                    continue;
                }

                // Check if relationship status has changed and update ping (eg. if a ship flipped from neutral to hostile or hostile to friendly via capture). 
                RadarPing currentPing = radarPings[i];
                UpdateExistingRadarPingStatus(ref currentPing);
                radarPings[i] = currentPing;

                if (entityDistanceSqr < _radarShownRangeSqr * (1 - theSettings.fadeThreshhold)) // Once we pass the fade threshold an auidible and visual cue should be applied. 
                {
                    if (!radarPings[i].Announced)
                    {
                        radarPings[i].Announced = true;
                        if (radarPings[i].Status == RelationshipStatus.Hostile && entityDistanceSqr > 250000) //500^2 = 250,000
                        {
                            PlayCustomSound(SP_ENEMY, worldRadarPos);
                            NewAlertAnim(entity);
                        }
                        else if (radarPings[i].Status == RelationshipStatus.Friendly && entityDistanceSqr > 250000)
                        {
                            PlayCustomSound(SP_NEUTRAL, worldRadarPos);
                            NewBlipAnim(entity);
                        }
                        else if (radarPings[i].Status == RelationshipStatus.Neutral && entityDistanceSqr > 250000)
                        {
                            // No Sound
                        }
                    }
                }
                else
                {
                    if (radarPings[i].Announced)
                    {
                        radarPings[i].Announced = false;
                    }
                }
            }
        }

		public void UpdateSpeedSettings() 
		{
            ControlDimmer += 0.05f;
            SpeedDimmer = Clamped((float)gHandler.localGridSpeed * 0.01f + 0.05f, 0f, 1f);

            SpeedDimmer = (float)(Math.Pow(SpeedDimmer, 2));

            SpeedDimmer = MathHelper.Clamp(Remap((float)gHandler.localGridSpeed, (SpeedThreshold * 0.75f), SpeedThreshold, 0f, 1f), 0f, 1f);
            ControlDimmer = Clamped(ControlDimmer, 0f, 1f);
            GlobalDimmer = ControlDimmer * SpeedDimmer;

            if (GlobalDimmer > 0.01f)
            {
                //Get Sun direction
                if (theSettings.starFollowSky)
                {
                    MyOrientation sunOrientation = getSunOrientation();

                    Quaternion sunRotation = sunOrientation.ToQuaternion();
                    Vector3D sunForwardDirection = Vector3D.Transform(Vector3D.Forward, sunRotation);

                    theSettings.starPos = sunForwardDirection * 100000000;
                }
            }
        }

		public void DrawSpeedLinesAndPlanetOrbits()
		{
            if (ShowVelocityLines && (float)gHandler.localGridSpeed > 10)
            {
                //DrawSpeedGaugeLines(gHandler.localGridControlledEntity, gHandler.localGridVelocity); // Original author disabled this function
                UpdateAndDrawVerticalSegments(gHandler.localGridControlledEntity, gHandler.localGridVelocity);
            }

            if (GlobalDimmer > 0.01f)
            {
                //Draw orbit lines
                foreach (var i in planetListDetails)
                {
                    // Cast the entity to IMyPlanet
                    MyPlanet planet = (MyPlanet)i.Entity;
                    Vector3D parentPos = (i.ParentEntity != null) ? i.ParentEntity.GetPosition() : theSettings.starPos;

                    DrawPlanetOutline(planet);
                    DrawPlanetOrbit(planet, parentPos);
                }
            }
        }


        //UPDATE=============================================================================
        //------------- B E F O R E -----------------
        /// <summary>
        /// The UpdateBeforeSimulation override is part of the VRage modding pipeline. The BeforeSimulation runs before Draw(). Basically anything that needs to be computed befor physics simulation. Like player inputs, application of thrust etc. I believe this runs 60 times per tick.
        /// The Draw() call runs multiple times per tick to draw graphics on screen (at your FPS) and therefore should be segregated to only do with rendering. 
        /// </summary>
        public override void UpdateBeforeSimulation()
		{
			// If the mod isn't even enabled do nothing.
			if(!EnableMASTER)
			{
				return;
			}
        }

        //------------ A F T E R ------------------
		/// <summary>
		/// UpdateAfterSimulation should be used to handle anything that requires the results of physics simulation. Playing sounds for eg. or updating a variable that tracks damage (UI updates that depnd on the result of physics simulation).
		/// </summary>
        public override void UpdateAfterSimulation()
		{

            

            if (!EnableMASTER)
            {
                return;
            }

            // Delta timer is used to track the number of seconds between game ticks by storing the elapsed seconds and then re-starting the timer back to 0 each call of AfterUpdateSimulation. 
            if (timerGameTickDelta != null)
            {
				UpdateElapsedTimeDeltaTimer();
            }
			if (timerRadarElapsedTimeTotal != null && !timerRadarElapsedTimeTotal.IsRunning) 
			{
				timerRadarElapsedTimeTotal.Start(); // We only need to start this once, putting this here in case it stops for some reason?
			}

            // Only execute things in here once for the mod. Everything out here should be executed potentially more than once such as the grid handler updating per ship. 
            if (!_modInitialized)
			{
                if (!_entitiesInitialized)
                {
                    // Initialize the planet manager
                    planetList = GetPlanets(); // We get planets once at launch, then by listening to OnEntityAdd event we can handle new ones. 
                    planetListDetails = GatherPlanetInfo(planetList); 

                    // Initialize Entity List for Radar
                    FindEntities(); // We get entities once at launch, then by listening to OnEntityAdd event we can handle new ones. 
                    _entitiesInitialized = true;
                }
                if (!_audioInitialized)
                {
                    InitializeAudio(); // Initialize audio sounds and an emitter once. 
                    _audioInitialized = true;
                }
                IsWeaponCoreLoaded(); // Check if weapon core is loaded and set a boolean true if so (WIP)
				if (_isWeaponCore) 
				{
					InitWeaponCore();
				}
                _modInitialized = true;
            }

            if (gHandler != null)
            {
                gHandler.UpdateLocalGrid(); // Always update current grid each tick

                if (gHandler.damageHandlerCurrentGrid == null)
                {
                    // Only if not already initialized call this method, as it adds event handlers on each BLOCK of the grid for if the Functional status changes (ie. is damaged to non-functional).
                    // Not quite sure how this plays into the total damage variable for the grid and how it affects glitch just yet...
                    gHandler.InitializeLocalGridDamageHandler();
                }
            }

            // Each tick check if the player is controlling a grid
            bool IsPlayerControlling = (gHandler != null && gHandler.IsPlayerControlling && gHandler.localGridControlledEntity != null && gHandler.localGrid != null);

			// If player is controlling we perform an OnSitDown event, or if we aren't seated we perform an OnStandUp event.
            if (IsPlayerControlling)
            {
                if (MyAPIGateway.Gui.IsCursorVisible || !_customDataInitialized)
                {
                    // Always check if not initialized, and if cursor is visible (ie. we are possibly editing the custom data). 
                    // This should ensure differences in ship setup apply (so long as we re-set this bool to false when we get up from the seat). 
                    CheckCustomData();
                }
                // localGrid will be null if we aren't in a cockpit block and that block doesn't have a grid initialized, localGridEntity will be null if we aren't controlling a seat of some kind. 

                if (!gHandler.localGridControlledEntityCustomName.Contains("[ELI_HUD]"))
                {
                    Echo("Include [ELI_HUD] tag in cockpit block name to enable Scanner.");
					_controlledBlockTaggedForMod = false;
                    return;
                }
				_controlledBlockTaggedForMod = true;

                if (!HasPowerProduction(gHandler.localGrid))
                {
					// If the grid has no power we don't draw any UI for it as radar / holograms could not be active.
					_localGridHasPower = false;
                    return;
                }
				_localGridHasPower = true;

                // Calculate positions and offsets and matrices etc.

                _powerLoadCurrentPercentPosition = worldRadarPos + (radarMatrix.Forward * radarRadius * 0.4) + (radarMatrix.Left * radarRadius * 1.3) + (radarMatrix.Up * 0.0085);


                powerLoadCurrent = gHandler.GetGridPowerUsagePercentage(gHandler.localGrid);

                glitchAmount_min = MathHelper.Clamp(gHandler.GetGridPowerUsagePercentage(gHandler.localGrid), 0.85, 1.0) - 0.85;
                glitchAmount_overload = MathHelper.Lerp(glitchAmount_overload, 0, deltaTimeSinceLastTick * 2);
                glitchAmount = MathHelper.Clamp(glitchAmount_overload, glitchAmount_min, 1);
                if (glitchAmount_overload < 0.01)
                {
                    glitchAmount_overload = 0;
                }

                // Perform sitdown event once until we get up. 
                if (!isSeated)
                {
                    //Seated Event!
                    OnSitDown();
                }


                CheckPlayerInput();

                UpdateUIPositions(); // Update the positions of the UI elements based on the current grid and camera position.
				
				UpdateRadarDetections(); // Update some radar scaling/range values based on current target etc. Then loop through each entity and check if player can detect it, update relationships, store values like active radar range etc.
				
				UpdateSpeedSettings();

                if (EnableDust)
                {
                    UpdateDust();
                }

				HG_Update();

                if (EnableMoney)
                {
                    UpdateCredits();
                }
                if (EnableToolbars)
                {
                    UpdateToolbars();
                }
            }
            else
            {
				// If no longer controlling a grid, we should perform the OnStandUp event if we haven't already.
                if (isSeated)
                {
                    //Standing Event!
                    OnStandUp();
                }
            }
            _distanceToCameraSqr = Vector3D.DistanceSquared(_cameraPosition, worldRadarPos);

        }

        

        //------------ D R A W -----------------
		/// <summary>
		/// Draw runs many times per tick (your FPS). But rendering can be skipped and it runs at different rates. We should not be updating game states here. No updating variables, no complex math calculations etc. This is for rendering only. 
		/// Heavy calculations in here will affect your framerate. 
		/// </summary>
        public override void Draw()
		{
			base.Draw();

            if (!EnableMASTER)
            {
				// If not enabled don't render anything
                return;
            }

			IMyPlayer player = MyAPIGateway.Session?.LocalHumanPlayer;
			if (player == null)
			{
				// Exit if we are not a palyer. This is only null for dedicated servers. Everything past this point should run if mod is enabled on the current block and you are not a dedicated server.
				return;
			}

            // Draw grid flares (glint) behind grids (if enabled). 
            DrawGridFlare();

            // Update visor effects (if enabled)
            UpdateVisor();

            // gHandler must be initialized, the player must be controlling something, that something (entity) must be initialized, and that entity must have a localGrid initialized that it belongs to. 
			// localGridControlledntity will only be non-null if the player has control of a block (cockpit, seat etc.)
            bool IsPlayerControlling = (gHandler != null && gHandler.IsPlayerControlling && gHandler.localGridControlledEntity != null && gHandler.localGrid != null);

			// The player must be in control of a valid grid, and it must have power. 
			if (IsPlayerControlling && _localGridHasPower && _controlledBlockTaggedForMod)
			{
				// If the player is controlling a valid block, with a valid grid, and it has power, and was tagged for using the mod then we continue. 

				//----Additional Checks----//
                // Check if the controlled entity is a turret or remote control
                Sandbox.ModAPI.IMyLargeTurretBase turret = gHandler.localGridControlledEntity as Sandbox.ModAPI.IMyLargeTurretBase;
                if (turret != null)
                {
                    return; // Do we actually want this? Or should turrets have radar? Perhaps turret based radar can be a secondary feature that rotates radar plane based on where turret is looking?
                }
                Sandbox.ModAPI.IMyRemoteControl remoteControl = gHandler.localGridControlledEntity as Sandbox.ModAPI.IMyRemoteControl;
                if (remoteControl != null && remoteControl.Pilot != null)
                {
                    return; // Again do we actually want this or should remotely controlled grids have radar?
                }
				//----End Additional Checks----//

				DrawSpeedLinesAndPlanetOrbits();

                UpdateCameraPosition();
                // Check for camera distance, so we do not draw close up hud elements if position would prevent their viewing anyway.
                if (_distanceToCameraSqr > 4) // 2^2 = 4
                {
                    return;
                }
                DrawRadar();

                //if dust ----------------------------------------------------------------------------------
                if (EnableDust)
                {
					DrawDust();
                }
                //------------------------------------------------------------------------------------------

                if (EnableGauges)
                {
                    DrawGauges();
                }

                if (GlobalDimmer > 0.99f)
                {
                    TB_activationTime = 0;
                    HG_activationTime = 0;
                    HG_activationTimeTarget = 0;
                    return;
                }

                if (EnableHolograms)
                {
					// 2025-07-25 FenixPK has separated the block position calculations to UpdateAfterSimulation, this simply loops through the blocks and draws them now.
					HG_Draw();
                    //HG_Update_OLD();
                }

                if (EnableMoney)
                {
					DrawCredits();
                }

                if (EnableToolbars)
                {
					DrawToolbars();
                }

            }
			else 
			{
                ControlDimmer -= 0.05f;
                return;
			}
		}
        //-----------------------------------------------------------------------------------

		/// <summary>
		/// Checks each IMyPowerProducer block in the grid to see if it is working, enabled, and has output greater than 0.01. If at least one satisfies returns true. 
		/// </summary>
		/// <param name="grid"></param>
		/// <returns></returns>
        bool HasPowerProduction(VRage.Game.ModAPI.IMyCubeGrid grid)
        {
            List<Sandbox.ModAPI.IMyPowerProducer> producers = new List<Sandbox.ModAPI.IMyPowerProducer>();
            MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid)?.GetBlocksOfType(producers);

            foreach (var producer in producers)
            {
                if (producer.IsWorking && producer.Enabled && producer.CurrentOutput > 0.01f)
                {
                    return true;
                }
            }
            return false;
        }

        //ACTIVATION=========================================================================
        private double visorLife = 0;
		private bool visorDown = false;

		private void UpdateVisor()
		{
			if (theSettings.enableVisor) 
			{
                if (MyAPIGateway.Session.CameraController.IsInFirstPersonView)
                {

                    MatrixD cameraMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
                    Vector3D camUp = cameraMatrix.Up;
                    Vector3D camLeft = cameraMatrix.Left;
                    Vector3D camForward = cameraMatrix.Forward;
                    Vector3D camPos = MyAPIGateway.Session.Camera.Position;
                    camPos += camForward * MyAPIGateway.Session.Camera.NearPlaneDistance * 1.001;


                    float fov = (float)GetCameraFOV();
                    float scale = 50 / fov;

                    visorDown = IsPlayerHelmetOn();

                    if (visorDown)
                    {
                        visorLife -= deltaTimeSinceLastTick * 1.5;
                    }
                    else
                    {
                        visorLife += deltaTimeSinceLastTick * 1.5;
                    }

                    visorLife = ClampedD(visorLife, 0, 1);

                    Vector3D visorVert = (LerpD(-0.75, 0.75, visorLife)) * camUp;

                    camPos += visorVert;
                    if (visorLife > 0 && visorLife < 1)
                    {
                        MyTransparentGeometry.AddBillboardOriented(MaterialVisor, new Vector4(1, 1, 1, 1), camPos, camLeft, camUp, scale);
                    }
                }
            }
		}

		private bool IsPlayerHelmetOn()
		{
			var playa = MyAPIGateway.Session?.LocalHumanPlayer;
			if (playa == null)
			{
				return false;
			}

			var controlledEntity = playa.Controller?.ControlledEntity;
			if (controlledEntity == null)
			{
				return false;
			}

			// Check if the controlled entity is a character
			var character = controlledEntity.Entity as IMyCharacter;
			if (character != null)
			{
				return character.EnabledHelmet;
			}

			// Check if the controlled entity is a cockpit
			var cockpit = controlledEntity.Entity as Sandbox.ModAPI.IMyCockpit;
			if (cockpit != null && cockpit.Pilot != null)
			{
				return cockpit.Pilot.EnabledHelmet;
			}

			// Check if the controlled entity is a turret or remote control
			var turret = controlledEntity.Entity as Sandbox.ModAPI.IMyLargeTurretBase;
			if (turret != null)
			{
				return false;
			}

			var remoteControl = controlledEntity.Entity as Sandbox.ModAPI.IMyRemoteControl;
			if (remoteControl != null && remoteControl.Pilot != null)
			{
				return remoteControl.Pilot.EnabledHelmet;
			}

			// Default to false if no valid controlled entity is found
			return false;
		}

		private void OnSitDown()
		{
			isSeated = true;
			//MyAPIGateway.Utilities.ShowMessage("NOTICE", "Entering Cockpit.");

			//Trigger animations and sounds for console.
			radarScaleRange_CurrentLogin = 0.001;
				
			//Check for CustomData variables in cockpit and register.
			CheckCustomData();
			glitchAmount_overload = 0.25;

			PlayCustomSound(SP_BOOTUP, worldRadarPos);
			//PlayCustomSound (SP_ZOOMINIT, worldRadarPos);

			HG_InitializeLocalGrid();
			InitializeWeaponCoreWeapons();
		}

		private void OnStandUp()
		{
			isSeated = false;
			//MyAPIGateway.Utilities.ShowMessage("NOTICE", "Leaving Cockpit.");
			//Trigger animations and sounds for console.
			HG_initialized = false;
			HG_initializedTarget = false;
			localGridBlocks = new List<BlockTracker>();
			targetGridBlocks = new List<BlockTracker>();
			localGridDrives = new List<BlockTracker>();
			targetGridDrives = new List<BlockTracker>();
			TB_activationTime = 0;
			HG_activationTime = 0;
			HG_activationTimeTarget = 0;
            _customDataInitialized = false; // Re-set custom data if we get out of the grid control seat.
			_localGridWCWeapons.Clear();
			_weaponCoreWeaponsInitialized = false;

            ReleaseTarget();
			DeleteDust();

		}

		private bool ShowVelocityLines = true;
		private bool ShowVoxels = true;
		private float SpeedThreshold = 10f;

		/// <summary>
		/// Parses the controlled block's CustomData for a section starting with "EliDang". If parsed successfuly after loading settings sets _customDataInitialized = true.
		/// </summary>
		private void CheckCustomData()
		{
			Sandbox.ModAPI.Ingame.IMyTerminalBlock block = (Sandbox.ModAPI.Ingame.IMyTerminalBlock)gHandler.localGridControlledEntity;

			// Read your specific data
			string mySection = "EliDang"; // Your specific section

			// Ensure CustomData is parsed
			string customData = block.CustomData;
			MyIni ini = new MyIni();
			MyIniParseResult result;

			// Parse the entire CustomData
			if (ini.TryParse(customData, out result))
			{
				ReadCustomData(ini);
                _customDataInitialized = true;
                return;
			}

			// Pattern to match the section including the delimiter "---"
			string pattern = $@"(\[{mySection}\].*?---\n)";
			var match = Regex.Match(customData, pattern, RegexOptions.Singleline);

			if (match.Success)
			{
				string sectionData = match.Groups[1].Value;

				if (ini.TryParse(sectionData, out result))
				{
					ReadCustomData(ini);
                    _customDataInitialized = true;
                    return;
				}
				else
				{
					// Section not found
					return;
				}
			}
			else
			{
				// Section not found
				return;
			}
		}

		/// <summary>
		/// Loads an ini object that has already been parsed setting the variables in the code to the values from the ini. 
		/// </summary>
		/// <param name="ini"></param>
		private void ReadCustomData(MyIni ini)
		{

			string mySection = "EliDang";

			// Master
			EnableMASTER = ini.Get(mySection, "ScannerEnable").ToBoolean(true);

			// Holograms
			EnableHolograms = !theSettings.enableHologramsGlobal ? false : ini.Get(mySection, "ScannerHolo").ToBoolean(true); // If disabled at global level don't even check just default to false.
			EnableHolograms_them = !theSettings.enableHologramsGlobal ? false : ini.Get(mySection, "ScannerHoloThem").ToBoolean(true); // If disabled at global level don't even check just default to false.
            EnableHolograms_you = !theSettings.enableHologramsGlobal ? false : ini.Get(mySection, "ScannerHoloYou").ToBoolean(true); // If disabled at global level don't even check just default to false.


            HologramView_AngularWiggle = ini.Get(mySection, "HologramViewFlat_AngularWiggle").ToBoolean(true);
            HologramView_AngularWiggleTarget = ini.Get(mySection, "HologramViewFlat_AngularWiggleTarget").ToBoolean(true);
			HologramView_Side theSide;
			string hologramViewFlat_CurrentSideString = ini.Get(mySection, "HologramViewFlat_CurrentSide").ToString();
			if (Enum.TryParse<HologramView_Side>(hologramViewFlat_CurrentSideString, out theSide))
			{
				HologramView_Current = theSide;
            }


            // Toolbar
            EnableToolbars = ini.Get(mySection, "ScannerTools").ToBoolean(true);

			// Gauges
			EnableGauges = ini.Get(mySection, "ScannerGauges").ToBoolean(true);

			// Money
			EnableMoney = ini.Get(mySection, "ScannerDolla").ToBoolean(true);

			// Offset
			double radarOffsetX = ini.Get(mySection, "ScannerX").ToDouble(0.0);
			double radarOffsetY = ini.Get(mySection, "ScannerY").ToDouble(-.2);
			double radarOffsetZ = ini.Get(mySection, "ScannerZ").ToDouble(-0.575);
			radarOffset = new Vector3D(radarOffsetX, radarOffsetY, radarOffsetZ);

            // Scale
            float radarScaleData = ini.Get(mySection, "ScannerS").ToSingle(1);
			radarRadius = 0.125f * radarScaleData;

			// Brightness
			float radarBrightness = ini.Get(mySection, "ScannerB").ToSingle(1);
			GLOW = radarBrightness;

			// Color
			string colorString = ini.Get(mySection, "ScannerColor").ToString(Convert.ToString(lineColorRGB));
			Color radarColor = ParseColor(colorString);
			theSettings.lineColor = (radarColor).ToVector4() * GLOW;

			// Velocity Toggle
			ShowVelocityLines = ini.Get(mySection, "ScannerLines").ToBoolean(true);

			// Orbit Line Speed Threshold
			SpeedThreshold = ini.Get(mySection, "ScannerOrbits").ToSingle(500);

			lineColorRGB = new Vector3(theSettings.lineColor.X, theSettings.lineColor.Y, theSettings.lineColor.Z);
			LINECOLOR_Comp_RPG = secondaryColor(lineColorRGB) * 2 + new Vector3(0.01f, 0.01f, 0.01f);
			LINECOLOR_Comp = new Vector4(LINECOLOR_Comp_RPG, 1f);

			// Voxel Toggle
			ShowVoxels = ini.Get(mySection, "ScannerShowVoxels").ToBoolean(true);
		}
		//-----------------------------------------------------------------------------------



		//GET SET CUSTOMDATA=================================================================
		public static void SetParameter(Sandbox.ModAPI.Ingame.IMyTerminalBlock block, string key, string value)
		{
			var cockpit = block as Sandbox.ModAPI.Ingame.IMyCockpit;
			if (cockpit != null)
			{
				string customData = cockpit.CustomData;

				// Normalize new lines to ensure consistent handling
				customData = customData.Replace("\r\n", "\n").Replace("\r", "\n");
				string[] lines = customData.Split(new[] {'\n'}, StringSplitOptions.None);

				bool found = false;
				// The key in the data should include the divider for clarity
				string fullKey = key + ": ";  // Ensure there's a space after colon for readability

				for (int i = 0; i < lines.Length; i++)
				{
					// Check if the current line starts with the full key including the divider
					if (lines[i].StartsWith(fullKey))
					{
						// Key found, update the value
						lines[i] = fullKey + value;
						found = true;
						break;
					}
				}

				// If the key wasn't found, append it
				if (!found)
				{
					Array.Resize(ref lines, lines.Length + 1);
					lines[lines.Length - 1] = fullKey + value;
				}

				// Join all lines back to a single string with proper new lines
				cockpit.CustomData = string.Join("\n", lines);
			}
		}

		public static string GetParameter(Sandbox.ModAPI.Ingame.IMyTerminalBlock block, string key)
		{
			string customData = block.CustomData;
			string pattern = $"\n{key}: ";
			int startIndex = customData.IndexOf(pattern);

			if (startIndex != -1)
			{
				startIndex += pattern.Length;
				int endIndex = customData.IndexOf("\n", startIndex);
				endIndex = endIndex == -1 ? customData.Length : endIndex;
				return customData.Substring(startIndex, endIndex - startIndex);
			}

			return null; // Key not found
		}

		private Color ParseColor(string colorString)
		{
			if (string.IsNullOrEmpty(colorString))
			{
                return lineColorRGB * GLOW;  // Default color if no data
			}


            string[] parts = colorString.Split(',');
			if (parts.Length == 3)
			{
				byte r, g, b;
				if (byte.TryParse(parts[0], out r) &&
					byte.TryParse(parts[1], out g) &&
					byte.TryParse(parts[2], out b))
				{
					return new Color(r, g, b);
				}
			}
			return lineColorRGB*GLOW;  // Default color on parse failure
		}
		//-----------------------------------------------------------------------------------



		//Stolen from the Rotate With Skybox Mod mod=========================================
		public MyOrientation getSunOrientation()
		{
			Vector3 dirToSun = MyVisualScriptLogicProvider.GetSunDirection();
			MatrixD matrix = MatrixD.CreateWorld(Vector3D.Zero, dirToSun, SunRotationAxis);

			Vector3 angles = MyMath.QuaternionToEuler(Quaternion.CreateFromForwardUp(matrix.Forward, matrix.Up));

			float pitch = angles.X;
			float yaw = angles.Y;
			float roll = angles.Z;

			MyOrientation orientation = new MyOrientation(yaw, pitch, roll);

			return orientation;
		}
		//-----------------------------------------------------------------------------------



		//PLANET FINDER======================================================================
		private HashSet<VRage.ModAPI.IMyEntity> GetPlanets()
		{
			HashSet<VRage.ModAPI.IMyEntity> planets = new HashSet<VRage.ModAPI.IMyEntity>();

			// Get all entities in the scene
			HashSet<VRage.ModAPI.IMyEntity> entities = new HashSet<VRage.ModAPI.IMyEntity>();
            
			// Pre-filter only MyPlanet entities using Lambda
            MyAPIGateway.Entities.GetEntities(entities, planetEntity => 
			{
				if (planetEntity == null) return false;
				if (planetEntity.MarkedForClose || planetEntity.Closed) return false;
				return planetEntity is MyPlanet;
			});
			
			// Iterate through the entities and add planets
			foreach (var entity in entities)
			{
				planets.Add(entity);
			}
			return planets;
		}

		void SubscribeToEvents()
		{
			// Subscribe to the OnEntityAdd event
			MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
			MyAPIGateway.Entities.OnEntityRemove += OnEntityRemove;
		}

		void UnsubscribeFromEvents()
		{
			// Unsubscribe from the OnEntityAdd event
			MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
			MyAPIGateway.Entities.OnEntityRemove -= OnEntityRemove;
		}

		private void OnEntityAdd(VRage.ModAPI.IMyEntity entity)
		{
			if (entity is MyPlanet)
			{
				//MyAPIGateway.Utilities.ShowMessage ("Planet Found", entity.EntityId.ToString());
				planetList.Add(entity);
				//MyAPIGateway.Utilities.ShowMessage("Planet Count",  planetList.Count.ToString());
				planetListDetails = GatherPlanetInfo(planetList);

				return;
			}

			if (entity == null || entity.MarkedForClose || entity.Closed || entity is MyPlanet || entity.DisplayName == null || entity.DisplayName == "Stone")
			{
				// Only add to entities OR pings if valid.
				return;
			}
			else 
			{
                bool found = false;
                if (entity is VRage.ModAPI.IMyEntity)
                {
                    foreach (var i in radarPings)
                    {
                        if (i.Entity == entity)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        RadarPing newPing = newRadarPing(entity);
                        radarPings.Add(newPing);
                    }
                }
                if (!radarEntities.Contains(entity))
                {
                    if (entity is VRage.Game.ModAPI.IMyCubeGrid)
                    {
                        radarEntities.Add(entity);
                    }
                }
            }
			
		}

		private void OnEntityRemove(VRage.ModAPI.IMyEntity entity)
		{
			if (entity is MyPlanet)
			{
				if (planetList.Contains(entity))
				{
					planetList.Remove(entity);
					planetListDetails = GatherPlanetInfo(planetList);
				}
			}

			if (entity is VRage.ModAPI.IMyEntity) 
			{
				foreach (var i in radarPings) 
				{
					if (i.Entity == entity) 
					{
						i.Time.Stop();
						radarPings.Remove (i);
						break;
					}
				}
			}

			if (radarEntities.Contains(entity)) 
			{
				radarEntities.Remove (entity);
			}
		}
		//-----------------------------------------------------------------------------------



		//DRAW LINES=========================================================================
		/// <summary>
		/// If grid flares are enabled draws a flare on screen so they show up better in the void. 
		/// </summary>
		void DrawGridFlare()
		{
			if (theSettings.enableGridFlares) 
			{
                MatrixD cameraMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
                Vector3 viewUp = cameraMatrix.Up;
                Vector3 viewLeft = cameraMatrix.Left;
                foreach (VRage.ModAPI.IMyEntity entity in radarEntities)
                {
                    if (entity is VRage.Game.ModAPI.IMyCubeGrid)
                    {
                        double dis2Cam = Vector3D.Distance(MyAPIGateway.Session.Camera.Position, entity.GetPosition());
                        if (dis2Cam >= 100)
                        {
                            double radius = entity.WorldVolume.Radius;
                            radius = RemapD(radius, 10, 1000, 0.5, 10);

                            float scale = (((GetRandomFloat() - 0.5f) * 0.01f) + 0.1f) * (float)dis2Cam;
                            double scaleF = RemapD(dis2Cam, 10000, 20000, 1, 0);
                            scaleF = ClampedD(scaleF, 0, 1);

                            Vector4 color = new Vector4(1, 1, 1, 1);
                            color.W += ((GetRandomFloat() - 1f) * 0.1f);

                            double fadder = RemapD(dis2Cam, 100, 2000, 0, 1);
                            fadder = ClampedD(fadder, 0, 1);

                            MyTransparentGeometry.AddBillboardOriented(MaterialShipFlare, color * 1f * (float)fadder, entity.GetPosition(), viewLeft, viewUp, scale * (float)scaleF * (float)radius, MyBillboard.BlendTypeEnum.AdditiveBottom);
                        }
                    }
                }
            }
		}


		void DrawLineBillboard(MyStringId material, Vector4 color, Vector3D origin, Vector3 directionNormalized, float length, float thickness, BlendTypeEnum blendType = 0, int customViewProjection = -1, float intensity = 1, List<VRageRender.MyBillboard> persistentBillboards = null)
		{
			if (GetRandomBoolean()) 
			{
				if (glitchAmount > 0.001) 
				{
					float glitchValue = (float)glitchAmount;

					Vector3D offsetRan = new Vector3D ((GetRandomDouble() - 0.5) * 2, (GetRandomDouble() - 0.5) * 2, (GetRandomDouble() - 0.5) * 2);
					double dis2Cam = Vector3D.Distance(MyAPIGateway.Session.Camera.Position, origin);

					origin += offsetRan * dis2Cam * glitchValue * 0.025;
					color *= GetRandomFloat();
				}
			}

			MyTransparentGeometry.AddLineBillboard(material, color, origin, directionNormalized, length, thickness, blendType, customViewProjection, intensity, persistentBillboards);
		}

		void DrawWorldUpAxis(Vector3D position)
		{
			BlendTypeEnum blendType = BlendTypeEnum.Standard;
			float lineThickness = 0.001f;
			Vector3D cameraPosition = MyAPIGateway.Session.Camera.Position;
			lineThickness = Convert.ToSingle(Vector3D.Distance(cameraPosition, position)) * lineThickness;

			float lineLength = 0.25f;
			lineLength = Convert.ToSingle(Vector3D.Distance(cameraPosition, position)) * lineLength;

			// Define the colors for each axis
			Color xColor = Color.Red;
			Color yColor = Color.Green;
			Color zColor = Color.Blue;

			// Draw the X-axis (red) RIGHT
			DrawLineBillboard(Material, xColor, position, Vector3D.Right, lineLength, lineThickness, blendType);

			// Draw the Y-axis (green) UP
			DrawLineBillboard(Material, yColor, position, Vector3D.Up, lineLength, lineThickness, blendType);

			// Draw the Z-axis (blue) FORWARD
			DrawLineBillboard(Material, zColor, position, Vector3D.Forward, lineLength, lineThickness, blendType);
		}

		// Method to draw a circle in 3D space
		void DrawCircle(Vector3D center, double radius, Vector3 planeDirection, Vector4 colorOverride, bool dotted = false, float dimmerOverride = 0f, float thicknessOverride = 0f)
		{
			// Define orbit parameters
			Vector3D planetPosition = center;   // Position of the center of the circle
			double orbitRadius = radius;        // Radius of the circle
			int segments = theSettings.lineDetail;                 // Number of segments to approximate the circle
			bool dotFlipper = true;

			//Lets adjust the tesselation based on radius instead of an arbitrary value.
			int segmentsInterval = (int)Math.Round(Remap((float)orbitRadius,1000, 10000000, 1, 8));
			segments = segments * (int)Clamped((float)segmentsInterval, 1, 16);

			float lineLength = 1.01f;           // Length of each line segment
			float lineThickness = theSettings.lineThickness;        // Thickness of the lines

			//Lets adjust the line thickness based on screen resolution so that it doesn't get to pixelated.
			lineThickness = ((float)GetScreenHeight()/1080f)*lineThickness;

			BlendTypeEnum blendType = BlendTypeEnum.Standard;  // Blend type for rendering
			Vector4 lineColor = colorOverride*GLOW;  // Color of the lines

			Vector3D cameraPosition = MyAPIGateway.Session.Camera.Position;  // Position of the camera

			// Calculate points on the orbit
			Vector3D[] orbitPoints = new Vector3D[segments];  // Array to hold points on the orbit
			double angleIncrement = 2 * Math.PI / segments;   // Angle increment for each segment

			// Normalize the plane direction vector
			planeDirection.Normalize();

			// Calculate the rotation matrix based on the plane direction
			MatrixD rotationMatrix = MatrixD.CreateFromDir(planeDirection);

			// Generate points on the orbit
			for (int i = 0; i < segments; i++)
			{
				double angle = angleIncrement * i;
				double x = orbitRadius * Math.Cos(angle);
				double y = orbitRadius * Math.Sin(angle);

				// Apply rotation transformation to the point
				Vector3D point = new Vector3D(x, y, 0);
				point = Vector3D.Transform(point, rotationMatrix);

				// Translate the point to the planet's position
				point += planetPosition;

				orbitPoints[i] = point;  // Store the transformed point
			}

			int count = orbitPoints.Length;

			// Draw lines between adjacent points on the orbit to form the circle
			for (int i = 0; i < segments; i++)
			{
				Vector3D point1 = orbitPoints[i];
				Vector3D point2 = orbitPoints[(i + 1) % count];  // Wrap around to the beginning
				Vector3 direction = (point2 - point1);  // Direction vector of the line segment

				Vector3D normalizedPoint1 = Vector3D.Normalize(point1-center);
				double dotPoint1 = Vector3D.Dot(normalizedPoint1, MyAPIGateway.Session.Camera.WorldMatrix.Backward);
				dotPoint1 = RemapD(dotPoint1, -1, 1, 0.25, 1);
				dotPoint1 = ClampedD(dotPoint1, 0.1, 1);

				// Calculate camera distance from whole segment.
				float distanceToSegment = DistanceToLineSegment(cameraPosition, point1, point2);

				// Calculate the segment thickness based on distance from camera.
				float segmentThickness = Math.Max(Remap(distanceToSegment, 1000f, 1000000f, 0f, 1000f) * lineThickness, 0f);

				// Calculate the segment brightness based on distance from camera.
				float dimmer = Clamped(Remap(distanceToSegment, -10000f, 10000000f, 1f, 0f), 0f, 1f)*1f;
				dimmer *= GlobalDimmer;

				if (thicknessOverride != 0) {
					segmentThickness = thicknessOverride;
				}

				if (dimmerOverride != 0) {
					dimmer = dimmerOverride;
				}

				if (dotFlipper || !dotted) {
					dotFlipper = false;
					// Add a line billboard representing the line segment
					if (dimmer > 0.01f && segmentThickness > 0) {
						DrawLineBillboard(Material, lineColor * dimmer * (float)dotPoint1, point1, direction, lineLength, segmentThickness, blendType);
					}
				} else {
					dotFlipper = true;
				}
			}
		}

		// Outline a planet with a dotted line
		void DrawPlanetOutline(VRage.ModAPI.IMyEntity entity)
		{
			// Cast the entity to a MyPlanet object
			MyPlanet planet = (MyPlanet)entity;

			// Determine the planet's effective radius (maximum radius or atmosphere radius, whichever is greater)
			double planetRadius = planet.MaximumRadius;
			double planetAtmo = planet.AtmosphereRadius;
			planetRadius = Math.Max(planetAtmo, planetRadius);

			// Get the position of the planet
			Vector3D planetPosition = entity.GetPosition();

			// Determine the direction to aim the outline towards (camera position)
			Vector3D aimDirection = AimAtCam(planetPosition);

			// Draw a circle representing the outline of the planet
			DrawCircle(planetPosition, (float)planetRadius, aimDirection, theSettings.lineColor, true);
		}

		// Fake an orbit for a planet
		void DrawPlanetOrbit(VRage.ModAPI.IMyEntity entity, Vector3D parentPosition)
		{
			// Cast the entity to a MyPlanet object
			MyPlanet planet = (MyPlanet)entity;

			// Determine the planet's effective radius (maximum radius or atmosphere radius, whichever is greater)
			double planetRadius = planet.MaximumRadius;
			double planetAtmo = planet.AtmosphereRadius;
			planetRadius = Math.Max(planetAtmo, planetRadius);

			// Get the position of the planet
			Vector3D planetPosition = entity.GetPosition();

			// Calculate the distance between the planet and its parent
			double orbitRadius = Vector3D.Distance(planetPosition, parentPosition); 

			// Calculate the direction from the planet to its parent and normalize it
			Vector3D orbitDirection = Vector3D.Normalize(parentPosition - planetPosition); 

			// Create a rotation matrix to align a reference direction with the orbit direction
			MatrixD rotationMatrix = MatrixD.CreateFromDir(orbitDirection);

			// Transform a reference direction (e.g., down) to align with the orbit direction
			orbitDirection = Vector3D.Transform(Vector3D.Down, rotationMatrix);

			// Draw a circle representing the planet's orbit around its parent
			DrawCircle(parentPosition, (float)orbitRadius, orbitDirection, theSettings.lineColor);
		}

		void DrawVelocityLines()
		{
			// Get the camera position and velocity
			Vector3D cameraPosition = MyAPIGateway.Session.Camera.Position;
			Vector3D cameraVelocity = MyAPIGateway.Session.Camera.WorldMatrix.Forward;

			// Define parameters for the line segments
			float segmentLength = 10.0f; // Length of each line segment
			int numSegments = 10; // Number of line segments to draw
			float segmentSpacing = 1.0f; // Spacing between line segments

			// Calculate the starting position for the line segments
			Vector3D startPosition = cameraPosition - cameraVelocity * segmentLength * numSegments * 0.5f;

			// Draw the line segments
			for (int i = 0; i < numSegments; i++)
			{
				// Calculate the position of the current line segment
				Vector3D segmentPosition = startPosition + cameraVelocity * (i * segmentLength + i * segmentSpacing);

				// Calculate the endpoints of the line segment
				Vector3D startPoint = segmentPosition;
				Vector3D endPoint = segmentPosition + cameraVelocity * segmentLength;

				// Draw the line segment
				DrawLineSegment(startPoint, endPoint);
			}
		}

		void DrawLineSegment(Vector3D start, Vector3D end)
		{
			BlendTypeEnum blendType = BlendTypeEnum.Standard;
			Vector3D cameraPosition = MyAPIGateway.Session.Camera.Position;  // Position of the camera

			float segmentLength = 10f;

			float lineThickness = theSettings.lineThickness;        // Thickness of the lines

			//Lets adjust the line thickness based on screen resolution so that it doesn't get to pixelated.
			lineThickness = ((float)GetScreenHeight()/1080f)*lineThickness;

			// Calculate camera distance from whole segment.
			float distanceToSegment = DistanceToLineSegment(cameraPosition, start, end);

			// Calculate the segment thickness based on distance from camera.
			float segmentThickness = Math.Max(Remap(distanceToSegment, 0f, 1000000f, 0f, 1000f) * lineThickness, 0f);

			// Calculate the segment brightness based on distance from camera.
			float dimmer = Clamped(Remap(distanceToSegment, 0, 10000000f, 1f, 0f), 0f, 1f)*7f;

			if (dimmer > 0.01f) 
			{
				DrawLineBillboard(Material, theSettings.lineColor * GLOW * dimmer, start, Vector3D.Normalize(end - start), segmentLength, segmentThickness, blendType);
			}
		}

		private void DrawSpeedGaugeLines(VRage.ModAPI.IMyEntity gridEntity, Vector3D velocity)
		{
			return;
			// Disabling this function for now.

			float lineLength = 100f;           	// Length of each line segment
			float lineThickness = 0.01f;        // Thickness of the lines

			var cockpit = gridEntity as Sandbox.ModAPI.IMyCockpit;
			if (cockpit == null || cockpit.CubeGrid == null || cockpit.CubeGrid.Physics == null) 
			{
				return;
			}

			VRage.Game.ModAPI.IMyCubeGrid grid = cockpit.CubeGrid;
			if (grid == null) 
			{
				return;
			}

			Vector3D gridCenter = grid.WorldVolume.Center;
			Vector3D direction = Vector3D.Normalize(velocity);
			double gridWidth = grid.WorldVolume.Radius;
			//MyVisualScriptLogicProvider.ShowNotification(gridWidth.ToString(), 2, "White");

			double speed = velocity.Length();
			lineLength = Math.Max((float)speed*2f, (float)gridWidth*6f);

			// Calculate a perpendicular vector using the cross product
			Vector3D upVector = Vector3D.Up; // Global up vector, adjust if necessary
			Vector3D perpendicular = Vector3D.Cross(direction, upVector);
			perpendicular.Normalize(); // Normalize to make it a unit vector

			// Offset positions to flank the grid
			Vector3D leftPosition = gridCenter + perpendicular * gridWidth * 0.5;
			Vector3D rightPosition = gridCenter - perpendicular * gridWidth * 0.5;

			// Adjust start positions so the lines are centered on the grid
			Vector3D leftLineStart = leftPosition - direction * (lineLength / 2);
			Vector3D rightLineStart = rightPosition - direction * (lineLength / 2);

			// Draw lines starting from these new positions along the velocity direction
			DrawLineBillboard(Material, Color.White, leftLineStart, direction, lineLength, lineThickness);
			DrawLineBillboard(Material, Color.White, rightLineStart, direction, lineLength, lineThickness);

		}

		private float scrollOffset = 0f; 		// This will keep track of the scrolling position
		private List<float> scrollOffsets = new List<float>();
		private bool scrollInit = false;

		private void UpdateAndDrawVerticalSegments(VRage.ModAPI.IMyEntity gridEntity, Vector3D velocity)
		{
			float segmentSpeedFactor = 0.01f;	// Factor to adjust responsiveness of segment spacing to speed changes
			float totalLineLength = 100f;		// Total length along which to place line segments
			float segmentLength = 1f;			// Length of each line segment
			float lineThickness = 0.05f;		// Thickness of the lines
			int totalNumLines = 10;				// Nmber of line segments active at a time.

			var cockpit = gridEntity as Sandbox.ModAPI.IMyCockpit;
			if (cockpit == null || cockpit.CubeGrid == null || cockpit.CubeGrid.Physics == null) 
			{
				return;
			}

			VRage.Game.ModAPI.IMyCubeGrid grid = cockpit.CubeGrid;
			if (grid == null) 
			{
				return;
			}

			if (!scrollInit) 
			{
				for (int i = 0; i < totalNumLines; i++) 
				{
					scrollOffsets.Add(totalLineLength - (totalLineLength / (float)totalNumLines) * (float)i);
				}
				scrollInit = true;
			}

			double gridWidth = grid.WorldVolume.Radius * 1.25;
			segmentLength = (float)gridWidth / 3f;
			Vector3D gridCenter = grid.WorldVolume.Center;
			Vector3D direction = Vector3D.Normalize(velocity);
			Vector3D perpendicular = Vector3D.Cross(direction, Vector3D.Up);
			perpendicular.Normalize();

			Vector3D worldUp = Vector3D.Up;
			Vector3D right = Vector3D.Cross(direction, worldUp);  // Get a right vector orthogonal to the direction
			right.Normalize();
			Vector3D segmentUp = Vector3D.Cross(right, direction);

			// Dynamic spacing inversely based on speed
			double speed = velocity.Length();
			totalLineLength = Math.Max((float)speed*2f, (float)gridWidth*6f);
			float spacing = Math.Max(1, totalLineLength*0.5f-(float)speed * segmentSpeedFactor); // Smaller spacing as speed decreases

			// Calculate base positions for segments
			Vector3D zeroBase;
			Vector3D leftBase = gridCenter + right * gridWidth * 0.5;
			Vector3D rightBase = gridCenter - right * gridWidth * 0.5;

			// Update scroll offset to move against the direction of travel
			scrollOffset -= (float)speed * segmentSpeedFactor; // Adjust factor as needed
			if (scrollOffset < 0) scrollOffset = (totalLineLength / (float)totalNumLines);  // Wrap around positively

			List<Vector3D> verticalSegments = new List<Vector3D>();

			// Calculate new positions for vertical segments
			for (int i = 0; i < totalNumLines; i++) 
			{

				scrollOffsets[i] = (totalLineLength - (totalLineLength / (float)totalNumLines) * (float)i);
				scrollOffsets[i] += scrollOffset;
				Vector3D segmentPosition = direction * scrollOffsets[i];
				verticalSegments.Add(segmentPosition);
			}

			// Draw vertical segments
			lineThickness = theSettings.lineThickness;        // Thickness of the lines

			//Lets adjust the line thickness based on screen resolution so that it doesn't get to pixelated.
			lineThickness = ((float)GetScreenHeight()/1080f)*lineThickness;

			// Calculate camera distance from whole segment.
			Vector3D cameraPosition = MyAPIGateway.Session.Camera.Position;

			foreach (var segmentPosition in verticalSegments)
			{
				// Calculate the segment brightness based on distance from camera.
				float distanceToSegment = (float)segmentPosition.Length();
				float segmentThickness = Remap(distanceToSegment, 0f, totalLineLength, 0f, 1f);
				segmentThickness = Math.Abs((segmentThickness - 0.5f) * 2f);
				float dimmer = Clamped(1-segmentThickness, 0f, 1f)*5f;
				dimmer *= SpeedDimmer*0.5f;

				lineThickness = theSettings.lineThickness;        // Thickness of the lines
				//Lets adjust the line thickness based on screen resolution so that it doesn't get to pixelated.
				lineThickness = ((float)GetScreenHeight()/1080f)*lineThickness;
				// Calculate camera distance from whole segment.
				distanceToSegment = (float)Vector3D.Distance(cameraPosition, leftBase + segmentPosition - direction * totalLineLength / 2);
				// Calculate the segment thickness based on distance from camera.
				segmentThickness = Math.Max(Remap(distanceToSegment, 0f, 1000000f, 0f, 1000f) * lineThickness, 0f);

				if (dimmer > 0.01f) 
				{
					zeroBase = (leftBase + segmentPosition - direction * totalLineLength / 2);
					zeroBase = (segmentLength * 0.5 * -segmentUp) + zeroBase;
					DrawLineBillboard(Material, theSettings.lineColor*GLOW * dimmer, zeroBase, segmentUp, segmentLength, segmentThickness);

					zeroBase = (rightBase + segmentPosition - direction * totalLineLength / 2);
					zeroBase = (segmentLength * 0.5 * -segmentUp) + zeroBase;
					DrawLineBillboard(Material, theSettings.lineColor*GLOW * dimmer, zeroBase, segmentUp, segmentLength, segmentThickness);
				}
			}
		}

		public void DrawQuad(Vector3D position, Vector3D normal, double radius, MyStringId materialId, Vector4 color, bool mode = false)
		{
			// Ensure the normal is normalized
			normal.Normalize();

			// Calculate perpendicular vectors to form the quad
			Vector3D up = Vector3D.CalculatePerpendicularVector(normal);
			Vector3D left = Vector3D.Cross(up, normal);
			up.Normalize();
			left.Normalize();

			if (GetRandomBoolean()) 
			{
				if (glitchAmount > 0.001) 
				{
					float glitchValue = (float)glitchAmount;

					Vector3D offsetRan = new Vector3D((GetRandomDouble () - 0.5) * 2, (GetRandomDouble () - 0.5) * 2, (GetRandomDouble () - 0.5) * 2);
					double dis2Cam = Vector3D.Distance(MyAPIGateway.Session.Camera.Position, position);

					position += offsetRan * dis2Cam * glitchValue * 0.025;
					color *= GetRandomFloat();
				}
			}

			// Calculate the four corners of the quad
			Vector3D topLeft = position + left * radius + up * radius;
			Vector3D topRight = position - left * radius + up * radius;
			Vector3D bottomLeft = position + left * radius - up * radius;
			Vector3D bottomRight = position - left * radius - up * radius;

			// Use MyTransparentGeometry to draw the quad
			MyQuadD quad = new MyQuadD
			{
				Point0 = topLeft,
				Point1 = topRight,
				Point2 = bottomRight,
				Point3 = bottomLeft
			};
			if (!mode) 
			{
				MyTransparentGeometry.AddQuad(materialId, ref quad, color, ref position);
			} 
			else 
			{
				MyTransparentGeometry.AddQuad(materialId, ref quad, color, ref position, -1, MyBillboard.BlendTypeEnum.AdditiveTop);
			}
		}

		public void DrawQuadRigid(Vector3D position, Vector3D normal, double radius, MyStringId materialId, Vector4 color, bool upward = false)
		{

			Vector3D radarPos = Vector3D.Transform(radarOffset, radarMatrix);
			Vector3D radarUp = radarMatrix.Up;
			Vector3D radarRight = radarMatrix.Right;
			Vector3D radarForward = radarMatrix.Forward;

			// Ensure the normal is normalized
			normal.Normalize();

			// Calculate perpendicular vectors to form the quad
			Vector3D up = radarUp; //Vector3D.CalculatePerpendicularVector(normal);

			if (upward) 
			{
				up = radarForward;
			}
			Vector3D left = Vector3D.Cross(up, normal);
			up.Normalize();
			left.Normalize();

			if (GetRandomBoolean()) 
			{
				if (glitchAmount > 0.001) 
				{
					float glitchValue = (float)glitchAmount;

					Vector3D offsetRan = new Vector3D ((GetRandomDouble() - 0.5) * 2, (GetRandomDouble() - 0.5) * 2, (GetRandomDouble() - 0.5) * 2);
					double dis2Cam = Vector3D.Distance(MyAPIGateway.Session.Camera.Position, position);

					position += offsetRan * dis2Cam * glitchValue * 0.025;
					color *= GetRandomFloat();
				}
			}

			// Calculate the four corners of the quad
			Vector3D topLeft = position + left * radius + up * radius;
			Vector3D topRight = position - left * radius + up * radius;
			Vector3D bottomLeft = position + left * radius - up * radius;
			Vector3D bottomRight = position - left * radius - up * radius;

			// Use MyTransparentGeometry to draw the quad
			MyQuadD quad = new MyQuadD
			{
				Point0 = topLeft,
				Point1 = topRight,
				Point2 = bottomRight,
				Point3 = bottomLeft
			};

			MyTransparentGeometry.AddQuad(materialId, ref quad, color, ref position);
		}


		//-----------------------------------------------------------------------------------



		//PLANET DATA MANAGER================================================================
		// Method to calculate the gravitational range of a planet
		private double CalculateGravitationalRange(double radius)
		{
			return radius * GravitationalRangeScaleFactor;
		}

		public List<PlanetInfo> GatherPlanetInfo(HashSet<VRage.ModAPI.IMyEntity> pList)
		{
			List<PlanetInfo> dList = new List<PlanetInfo>();

			dList = OrderPlanetsBySize(pList);
			dList = DetermineParentChildRelationships(dList);

			return dList;
		}

		// Method to order planets by size (radius) in descending order
		public List<PlanetInfo> OrderPlanetsBySize(HashSet<VRage.ModAPI.IMyEntity> pList)
		{
			// Create a list to hold planet information
			List<PlanetInfo> planetInfoList = new List<PlanetInfo>();

			// Iterate through each planet in the planet list
			foreach (var entity in pList)
			{
				// Cast the entity to a MyPlanet object
				MyPlanet planet = (MyPlanet)entity;

				// Determine the effective radius (use atmosphere radius if available)
				double planetRadius = Math.Max(planet.AtmosphereRadius, planet.MaximumRadius);

				// Calculate the gravitational range of the planet
				double gravitationalRange = CalculateGravitationalRange(planetRadius);

				// Add planet information to the list
				planetInfoList.Add(new PlanetInfo { Entity = entity, Mass = planetRadius, GravitationalRange = gravitationalRange });
			}

			// Order the planet information list by mass (radius) in descending order (largest to smallest)
			planetInfoList = planetInfoList.OrderByDescending(p => p.Mass).ToList();

			return planetInfoList;
		}

		public List<PlanetInfo> DetermineParentChildRelationships(List<PlanetInfo> orderedPlanets)
		{
			for (int i = 0; i < orderedPlanets.Count - 1; i++)
			{
				PlanetInfo currentPlanet = orderedPlanets[i];

				for (int j = i + 1; j < orderedPlanets.Count; j++)
				{
					PlanetInfo nextPlanet = orderedPlanets[j];

					// Calculate distance between the centers of the planets
					double distance = Vector3D.Distance(currentPlanet.Entity.GetPosition(), nextPlanet.Entity.GetPosition());

					// Calculate the sum of gravitational ranges
					double sumOfRanges = currentPlanet.GravitationalRange + nextPlanet.GravitationalRange;

					// Check if the distance is within the sum of the ranges
					if (distance < sumOfRanges)
					{
						// Determine parent and child based on size
						PlanetInfo parent = (currentPlanet.Mass > nextPlanet.Mass) ? currentPlanet : nextPlanet;
						PlanetInfo child = (currentPlanet.Mass > nextPlanet.Mass) ? nextPlanet : currentPlanet;

						// Update the parent-child relationship
						child.ParentEntity = parent.Entity;
					}
				}
			}

			return orderedPlanets;
		}
		//-----------------------------------------------------------------------------------



		//MATH HELPERS=======================================================================
		float Clamped(float value, float min, float max)
		{
			if (value < min)
				return min;
			else if (value > max)
				return max;
			else
				return value;
		}

		double ClampedD(double value, double min, double max)
		{
			if (value < min)
				return min;
			else if (value > max)
				return max;
			else
				return value;
		}

		float Remap(float value, float fromLow, float fromHigh, float toLow, float toHigh)
		{
			return toLow + (value - fromLow) * (toHigh - toLow) / (fromHigh - fromLow);
		}

		double RemapD(double value, double fromLow, double fromHigh, double toLow, double toHigh)
		{
			return toLow + (value - fromLow) * (toHigh - toLow) / (fromHigh - fromLow);
		}
			
		Vector3D AimAtCam(Vector3D point)
		{
			// Used for billboard rendering.

			// Get the position of the player camera
			Vector3D camPosition = MyAPIGateway.Session.Camera.Position;

			// Calculate the direction vector from the point toward the camera
			Vector3D towardCamDirection = Vector3D.Normalize(camPosition - point);

			return towardCamDirection;
		}

		Vector3D Swizzle(Vector3D original, string swizzlePattern)
		{
			double x = 0, y = 0, z = 0;

			for (int i = 0; i < swizzlePattern.Length; i++)
			{
				switch (swizzlePattern[i])
				{
				case 'x':
					x = original.X;
					break;
				case 'y':
					y = original.Y;
					break;
				case 'z':
					z = original.Z;
					break;
				default:
					// Invalid component, do nothing
					break;
				}
			}

			return new Vector3D(x, y, z);
		}

		Vector3D AlignAxis(Vector3D newDirection)
		{
			// Calculate the rotation matrix to align the upward axis with the new direction
			MatrixD rotationMatrix = MatrixD.CreateFromDir (newDirection, Vector3D.Down);
			newDirection = Vector3D.Transform(newDirection, rotationMatrix);

			return newDirection;
		}

		int GetScreenWidth()
		{
			return MyAPIGateway.Session.Config.ScreenWidth ?? 1920;
		}

		int GetScreenHeight()
		{
			return MyAPIGateway.Session.Config.ScreenHeight ?? 1080;
		}

		float DistanceToLineSegment(Vector3D cameraPosition, Vector3D segmentStart, Vector3D segmentEnd)
		{
			// Calculate the vector from the segment start to the camera position
			Vector3D segmentStartToCamera = cameraPosition - segmentStart;

			// Calculate the vector along the line segment
			Vector3D segmentVector = segmentEnd - segmentStart;

			// Calculate the length of the segment
			double segmentLengthSquared = segmentVector.LengthSquared();

			// Calculate the projection of the camera position onto the line defined by the segment
			double t = Vector3D.Dot(segmentStartToCamera, segmentVector) / segmentLengthSquared;

			// Clamp t to ensure it's within [0, 1] to stay within the segment
			t = Math.Max(0, Math.Min(1, t));

			// Calculate the closest point on the line segment to the camera position
			Vector3D closestPoint = segmentStart + t * segmentVector;

			// Calculate the distance between the camera position and the closest point
			float distance = (float)Vector3D.Distance(cameraPosition, closestPoint);

			return distance;
		}

		double LerpD(double a, double b, double t)
		{
			return a + (b - a) * t;
		}

		float LerpF(float a, float b, float t)
		{
			return a + (b - a) * t;
		}
        //-----------------------------------------------------------------------------------



       


        public double maxRadarRange = 20000; // Set by config to draw distance or value specified in config. This is a fallback value. 

		// Radar scale range. Ie. how zoomed in or out is the radar screen. 
		public double radarScaleRange = 5000;
		public double radarScaleRange_Goal = 5000;
		public double radarScaleRange_Current = 5000;

		public double radarScaleRange_GoalLogin = 1;
		public double radarScaleRange_CurrentLogin = 0.01;

		public static float radarRadius = 0.125f; 
		public static double radarScale = 0.000025;
		public static float targetHologramRadius = 0.125f;
		
		private bool targetingFlipper = false;

		

        /// <summary>
        /// The radarMatrix built from the cockpit block WorldMatrix, includes rotation so blips rotate relative to the way you are facing.
        /// </summary>
        private MatrixD radarMatrix;

		/// <summary>
		/// The position for the radar screen in world space, used as an offset to draw UI elements to this location. 
		/// </summary>
        private Vector3D worldRadarPos;

		/// <summary>
		/// The offset used to change where on screen the radar is located.
		/// </summary>
        public Vector3D radarOffset = new Vector3D(0, -0.20, -0.575);

        /// <summary>
        /// The cockpitMatrix is built from a new MatrixD.Identity that has no rotation - then we apply the cockpit block .Translation to it. So it is translation only. 
        /// </summary>
        private MatrixD cockpitMatrix;

		/// <summary>
		/// The cockpit's forward vector. 
		/// </summary>
		private Vector3D cockpitForward;

		/// <summary>
		///  The cockpit's up vector.
		/// </summary>
		private Vector3D cockpitUp;

		/// <summary>
		/// The cockpit's right vector.
		/// </summary>
		private Vector3D cockpitRight;

		/// <summary>
		/// The target hologram world position. 
		/// </summary>
        public Vector3D targetHologramWorldPos;

        /// <summary>
        /// The offset used to change where on screen the target hologram is located. 
        /// </summary>
        public Vector3D targetHologramOffset;

		/// <summary>
		/// The matrix for the target hologram, this has only translation - no rotation.
		/// </summary>
		public MatrixD targetHologramMatrix;

        /// <summary>
        /// The local grid hologram world position.
        /// </summary>
        public Vector3D localHologramWorldPos;
        
		/// <summary>
		/// The offset used to change where on the screen the local grid hologram is located.
		/// </summary>
        public Vector3D localHologramOffset;

		/// <summary>
		/// The matrix for the local grid hologram, this has only translation - no rotation.
		/// </summary>
		public MatrixD localHologramMatrix;


        

		
		private double targettingLerp = 1.5;
		private double targettingLerp_goal = 1.5;
		private double alignLerp = 1.0;
		private double alignLerp_goal = 1.0;

		private double squishValue = 0.75;
		private double squishValue_Goal = 0.75;

		private double rangeBracketBuffer = 500; // Meters to add to a distance as a buffer for purposes of determining bracket.
		private int debugFrameCount = 0;
        private bool debug = false;



        //===================================================================================
        //Radar==============================================================================
        //===================================================================================

        public int GetRadarScaleBracket(double rangeToBracket) 
		{
            // Add buffer to encourage zooming out slightly beyond the target
            double bufferedDistance = rangeToBracket + rangeBracketBuffer;
            int bracketedRange = ((int)(bufferedDistance / theSettings.rangeBracketDistance) + 1) * theSettings.rangeBracketDistance;

			// Clamp to the user-defined max radar range
			return Math.Min(bracketedRange, (int)maxRadarRange);
        }

        MatrixD GetHologramMatrixWithoutYaw(Sandbox.ModAPI.IMyTerminalBlock cockpit)
        {
            var cockpitMatrix = cockpit.WorldMatrix;
            Vector3D forward = cockpitMatrix.Forward;

            // Remove yaw: zero X component
            forward = Vector3D.Normalize(new Vector3D(0, forward.Y, forward.Z));

            // Preserve roll and pitch
            Vector3D up = cockpitMatrix.Up;
            Vector3D right = Vector3D.Normalize(Vector3D.Cross(up, forward));
            up = Vector3D.Normalize(Vector3D.Cross(forward, right));

            return MatrixD.CreateWorld(Vector3D.Zero, forward, up);
        }



        // DrawRadar modified 2025-07-13 by FenixPK to use Vector3D.DistanceSquared for comparisons.
        // Using .Distance() directly does a square root to get the exact distance since the values are stored squared already.
        // If we need to display a distance, then by all means do the square root.
        // But for things like checking if an entity is within a certain distance etc. we can just use the squares and save CPU cycles. 
        /// <summary>
		/// DrawRadar draws main UI elements of the radar, including the radar circle, holograms, and entities within radar range.
		/// It also handles the targetting reticle and alignment of the radar screen based on the player's position and orientation. FenixPK has modified this so it
		/// only does rendering, calculating positions and what can/can't be detected is moved to the UpdateAfterSimulation function. 
		/// </summary>
		private void DrawRadar()
		{
			// We shouldn't be in here if we haven't already checked if gHandler is null, camera too far away etc. So we can skip more checks here. 

			// Debug log writes every 360 frames to reduce log spam
			if (debugFrameCount >= 360)
			{
				debug = true;
			}
			else 
			{
				debug = false;
				debugFrameCount++;
			}

            // Okay FenixPK is pretty sure the "problems" I've been having with vectors, matrices, and rotations and perspective etc. are all coming from the fact that the holograms share a position vector and matrix with the radar which needs to rotate.
            // So we will split this into three positions, three offsets. And it solves a request where users wanted to move all of this stuff around independently too. If I can finish it in time haha.

            // Fetch the player's cockpit entity and it's world position (ie. the block the player is controlling). 
            Vector3D playerGridPos = gHandler.localGridControlledEntityPosition;

            // CAMERA POSITION //
            // Get the camera's vectors
            MatrixD cameraMatrix = _cameraMatrix;
            Vector3 viewUp = cameraMatrix.Up;
            Vector3 viewLeft = cameraMatrix.Left;
            Vector3 viewForward = cameraMatrix.Forward;
            Vector3 viewBackward = cameraMatrix.Backward;
            Vector3D viewBackwardD = cameraMatrix.Backward;
			Vector3D cameraPos = _cameraPosition;

            Vector3D radarPos = worldRadarPos;

            // Draw the radar circle
            Vector3D radarUp = radarMatrix.Up;
            Vector3D radarDown = radarMatrix.Down;
            Vector3D radarLeft = radarMatrix.Left;
            Vector3D radarForward = radarMatrix.Forward;
            Vector3D radarBackward = radarMatrix.Backward;

            // Can always show our own hologram
            if (EnableHolograms && EnableHolograms_you)
            {
                double lockInTime_Right = HG_activationTime;
                lockInTime_Right = ClampedD(lockInTime_Right, 0, 1);
                lockInTime_Right = Math.Pow(lockInTime_Right, 0.1);

                DrawQuad(_hologramPositionRight + (radarUp * _hologramRightOffset_HardCode.Y * 0.5), viewBackward, 0.15, MaterialCircleSeeThroughAdd, theSettings.lineColor * 0.125f, true);

                DrawCircle(_hologramPositionRight, 0.04 * lockInTime_Right, radarUp, theSettings.lineColor, false, 0.5f, 0.00075f);
                DrawCircle(_hologramPositionRight - (radarUp * 0.015), 0.045, radarUp, theSettings.lineColor, false, 0.25f, 0.00125f);

                DrawQuad(_hologramPositionRight + (radarUp * _hologramRightOffset_HardCode.Y), viewBackward, 0.1, MaterialCircleSeeThrough, new Vector4(0, 0, 0, 0.75f)); //Dim Backing
            }

         
			if (currentTarget != null && !currentTarget.Closed)
			{
				if (_playerCanDetectCurrentTarget)
				{
                    if (debug)
                    {
                        //MyLog.Default.WriteLine($"FENIX_HUD: Target selected, and can detect");
                    }

					// Handle target hologram here
					if (EnableHolograms && EnableHolograms_them)
					{

						double lockInTime_Left = HG_activationTimeTarget;
						lockInTime_Left = ClampedD(lockInTime_Left, 0, 1);
						lockInTime_Left = Math.Pow(lockInTime_Left, 0.1);

						DrawCircle(_hologramPositionLeft, 0.04 * lockInTime_Left, radarUp, theSettings.lineColor, false, 0.5f, 0.00075f);
						DrawCircle(_hologramPositionLeft - (radarUp * 0.015), 0.045, radarUp, theSettings.lineColor, false, 0.25f, 0.00125f);

						// We will have cleared target lock earlier in loop if no longer able to target due to distance or lack of antenna etc.
						if (isTargetLocked)
						{
							DrawQuad(_hologramPositionLeft + (radarUp * _hologramRightOffset_HardCode.Y * 0.5), viewBackward, 0.15, MaterialCircleSeeThroughAdd, LINECOLOR_Comp * 0.125f, true);
							DrawQuad(_hologramPositionLeft + (radarUp * _hologramRightOffset_HardCode.Y), viewBackward, 0.1, MaterialCircleSeeThrough, new Vector4(0, 0, 0, 0.75f)); //Dim Backing
						}
						else
						{
							DrawQuad(_hologramPositionLeft + (radarUp * _hologramRightOffset_HardCode.Y * 0.5), viewBackward, 0.15, MaterialCircleSeeThroughAdd, theSettings.lineColor * 0.125f, true);
						}
					}
				}
			}

			// Draw the current range bracket on screen. 
			// This does a lot. It shows the player if they are in active or passive mode or off.
			// Shows the range of active or passive scanning.
			// Shows the radar scale range which will be bracketed. Ie. your active radar might be set to 4123 meters, your passive radar set to 5512 meters.
			// Your bracket would use your maxPassive and set it in clean increments as the user defined. I use 2000m with 500m buffer. So 5512+buffer would put it into the 8000 range bracket.
			// This means the radar would be zoomed out to show 8k worth of space, despite only detecting objects passively up to 5512 meters and actively up to 4123 meters.
			// Because active mode takes priority I show it's range instead of passive. When active mode off passive will show. Passive's range if different than active can be inferred a bit from scale.
			// Additionally if the radar scale is overriden by having a Target selected a T is show, as scale might be lower than expected. When you select a target nearby it "zooms" the radar scale in. 
            Vector3D textDir = -radarMatrix.Backward; // text faces the player/camera
			double activeRangeDisp = Math.Round(_playerMaxActiveRange/1000, 1);
			double passiveRangeDisp = Math.Round(_playerMaxPassiveRange/1000, 1);
			double radarScaleDisp = Math.Round(radarScaleRange / 1000, 1);
            string rangeText = $"RDR{(_playerHasActiveRadar ? $"[ACT]:{activeRangeDisp}" : _playerHasPassiveRadar ? $"[PAS]:{passiveRangeDisp}" : "[OFF]:0")}KM [SCL]:{radarScaleDisp}KM {(currentTarget != null ? "(T)" : "")}";
            DrawText(rangeText, 0.0045, _radarCurrentRangeTextPosition, textDir, theSettings.lineColor);


			Vector4 color_Current = color_VoxelBase; // Default to voxelbase color
			float scale_Current = 1.0f;

			// Radar Pulse Timer for the pulse animation
            double radarPulseTime = this.timerRadarElapsedTimeTotal.Elapsed.TotalSeconds / 3;
			radarPulseTime = 1 - (radarPulseTime - Math.Truncate (radarPulseTime))*2;

			// Radar attack timer to flip the blips and make them pulsate. Goal is to tie this to being targetted by those grids rather than by distance to them.
			double attackTimer = this.timerRadarElapsedTimeTotal.Elapsed.TotalSeconds*4;
			attackTimer = (attackTimer - Math.Truncate (attackTimer));
			if (attackTimer > 0.5) 
			{
				targetingFlipper = true;
			} 
			else 
			{
				targetingFlipper = false;
			}
			// Draw Rings
            DrawQuad(radarPos, radarUp, (double)radarRadius * 0.03f, MaterialCircle, theSettings.lineColor * 5f); //Center
			// Draw perspective lines
            float radarFov = Clamped(GetCameraFOV ()/2, 0, 90)/90;
			DrawLineBillboard (Material, theSettings.lineColor*GLOW*0.4f, radarPos, Vector3D.Lerp(radarMatrix.Forward,radarMatrix.Left,radarFov), radarRadius*1.45f, 0.0005f); //Perspective Lines
			DrawLineBillboard (Material, theSettings.lineColor*GLOW*0.4f, radarPos, Vector3D.Lerp(radarMatrix.Forward,radarMatrix.Right,radarFov), radarRadius*1.45f, 0.0005f);

			// Animate radar pulse
			for (int i = 1; i < 11; i++) 
			{
				int radarPulseSteps = (int)Math.Truncate (10 * radarPulseTime);
				float radarPulse = 1;
				if (radarPulseSteps == i) 
				{
					radarPulse = 2;
				}
				DrawCircle(radarPos-(radarUp*0.003), (radarRadius*0.95)-(radarRadius*0.95)*(Math.Pow((float)i/10, 4)), radarUp, theSettings.lineColor, false, 0.25f*radarPulse, 0.00035f);
			}
            
			// Draw border
            DrawQuadRigid(radarPos, radarUp, (double)radarRadius*1.18, MaterialBorder, theSettings.lineColor, true); //Border

			// Draw compass
			if(EnableGauges)
			{
				DrawQuadRigid(radarPos-(radarUp*0.005), -radarUp, (double)radarRadius*1.5, MaterialCompass, theSettings.lineColor, true); //Compass
			}
			DrawQuad(radarPos-(radarUp*0.010), radarUp, (double)radarRadius*2.25, MaterialCircleSeeThrough, new Vector4(0, 0, 0, 0.75f)); //Dim Backing
			DrawQuad(radarPos + (radarUp*0.025), viewBackward, 0.25, MaterialCircleSeeThroughAdd, theSettings.lineColor*0.075f, true);

            UpdateRadarAnimations(playerGridPos);

            // Okay I had to do some research on this. The way this works is Math.Sin() returns a value between -1 and 1, so we add 1 to it to get a value between 0 and 2, then divide by 2 to get a value between 0 and 1.
			// Basically shifting the wave up so we are into positive value only territory, and then scaling it to a 0-1 range where 0 is completely invisible and 1 is fully visible. Neat.
            float haloPulse = (float)((Math.Sin(timerRadarElapsedTimeTotal.Elapsed.TotalSeconds * (4*Math.PI)) + 1.0) * 0.5); // 0 -> 1 -> 0 smoothly. Should give 2 second pulses. Could do (2*Math.PI) for 1 second pulses

            // Go up to count OR max pings, so we don't process too many pings on the radar. 
            for (int i = 0; i < Math.Min(radarPings.Count, theSettings.maxPings); i++)
            {
                VRage.ModAPI.IMyEntity entity = radarPings[i].Entity;
                if (entity == null) 
				{
					radarPings.RemoveAt(i);
					continue; // Clear the ping then continue
				}
				if (!ShowVoxels && radarPings[i].Status == RelationshipStatus.Vox) 
				{
                    continue; // Skip voxels if not set to show them. 
				}
				if (entity.GetTopMostParent() == gHandler.localGridControlledEntity.GetTopMostParent()) 
				{
					continue; // Skip drawing yourself on the radar.
				}

				if (!radarPings[i].PlayerCanDetect || radarPings[i].RadarPingDistanceSqr > _radarShownRangeSqr) // If can't detect it, or the radar scale prevents showing it move on.
				{
					continue;
				}

				// Handle fade for edge
				float fadeDimmer = 1f;
                // Have to invert old logic, original author made this extend radar range past configured limit. 
				// I am having the limit as a hard limit be it global config or antenna broadcast range, so we instead make a small range before that "fuzzy" instead. 
				// If it was outside max range it wouldn't have been detected by the player actively or passively, so we skipped it already. Meaning all we have to do is check if it is in the fade region and fade it or draw it regularly. 
                if (radarPings[i].RadarPingDistanceSqr >= _fadeDistanceSqr) 	
				{
                    fadeDimmer = 1 - Clamped(1 - (float)((_fadeDistanceSqr - radarPings[i].RadarPingDistanceSqr) / (_fadeDistanceSqr - _radarShownRangeSqr)), 0, 1);
                }
                Vector3D scaledPos = ApplyLogarithmicScaling(radarPings[i].RadarPingPosition, playerGridPos); // Apply radar scaling

                // Position on the radar
                Vector3D radarEntityPos = radarPos + scaledPos;

                // If there are any problems that would make displaying the entity fail or do something ridiculous that tanks FPS just skip to the next one.
                if (double.IsNaN(radarEntityPos.X) || double.IsInfinity(radarEntityPos.X))
                {
                    continue;
                }
                if (!radarEntityPos.IsValid())
                {
                    continue;
                }

                Vector3D v = radarEntityPos - radarPos;
                Vector3D uNorm = Vector3D.Normalize(radarUp);
                float vertDistance = (float)Vector3D.Dot(v, uNorm);

                float upDownDimmer = 1f;
                if (vertDistance < 0)
                {
                    upDownDimmer = 0.8f;
                }

                float lineLength = (float)Vector3D.Distance(radarEntityPos, radarPos);
                Vector3D lineDir = radarDown;

				// If invalid, skip now.
                if (!lineDir.IsValid())
                {
                    continue;
                }

                double gridWidth = radarPings[i].Width;
                scale_Current = Math.Max(min_blip_scale, (float)gridWidth); // If gridWidth is something ridiculous like 0.0f then fall back on min scale. 
                if (scale_Current <= 0.000001 || double.IsNaN(scale_Current)) // Compare to EPS rather than 0. If invalid skip now.
                {
                    continue;
                }

				// Do color of blip based on relationship to player
                color_Current = radarPings[i].Color;
                if (debug)
                {
                    //MyLog.Default.WriteLine($"FENIX_HUD: EntityID {radarPings[i].Entity.EntityId}, status = {currentPing.Status.ToString()} color = {color_Current}");
                }

                // Detect being targeted (under attack) and modify color based on flipper so the blip flashes
                if (radarPings[i].Status == RelationshipStatus.Hostile && IsEntityTargetingPlayer(radarPings[i].Entity)) 
				{
                    if (!targetingFlipper)
                    {
                        color_Current = color_GridEnemyAttack;
                    }
                }

				// Pulse timers for animation
                Vector3D pulsePos = radarEntityPos + (lineDir * vertDistance);
                double pulseDistance = Vector3D.Distance(pulsePos, radarPos);
                float pulseTimer = (float)(ClampedD(radarPulseTime, 0, 1) + 0.5 + Math.Min(pulseDistance, radarRadius) / radarRadius);
                if (pulseTimer > 1)
                {
                    pulseTimer = pulseTimer - 1;//(float)Math.Truncate (pulseTimer);
                    if (pulseTimer > 1)
                    {
                        pulseTimer = pulseTimer - 1;
                    }
                }
                pulseTimer = Math.Max(pulseTimer * 2, 1);

				// Set drawMaterial based on type of entity/relationship.
                MyStringId drawMat = MaterialCircle;
                switch (radarPings[i].Status)
                {
                    case RelationshipStatus.Friendly:
                        drawMat = MaterialSquare;
                        break;
                    case RelationshipStatus.Hostile:
                        drawMat = MaterialTriangle;
                        break;
                    case RelationshipStatus.Neutral:
                        drawMat = MaterialSquare;
                        break;
                    case RelationshipStatus.Vox:
                        drawMat = MaterialCircle;
                        color_Current = theSettings.lineColor;
                        break;
                    case RelationshipStatus.FObj:
                        drawMat = MaterialDiamond;
                        break;
                    default:
                        drawMat = MaterialCircle;
                        break;
                }

                // Draw each entity as a billboard on the radar
                float blipSize = 0.005f * fadeDimmer * scale_Current;
                Vector3D blipPos = radarEntityPos + (lineDir * vertDistance);
                // Draw the blip with vertical elevation +/- relative to radar plane, with line connecting it to the radar plane.
                DrawLineBillboard(MaterialSquare, color_Current * 0.25f * fadeDimmer * pulseTimer, radarEntityPos, lineDir, vertDistance, 0.001f * fadeDimmer);
                DrawQuad(blipPos, radarUp, blipSize, MaterialCircle, color_Current * upDownDimmer * 0.25f * pulseTimer * 0.5f);
				// Add blip "shadow" on radar plane
                MyTransparentGeometry.AddBillboardOriented(drawMat, color_Current * upDownDimmer * pulseTimer, radarEntityPos, viewLeft, viewUp, 0.0025f * fadeDimmer * scale_Current);

                // DONE UNTESTED: need to check that they have it AND it can reach us, if we aren't being painted by it don't show it. For situations where our active radar is set far enough to pickup grids
				// that also have active radar and thus the bool would be true but it's range is low enough they can't reach us (see us). As in only show grids that can see us. 
                if (radarPings[i].RadarPingHasActiveRadar && radarPings[i].RadarPingDistanceSqr <= (radarPings[i].RadarPingMaxActiveRange*radarPings[i].RadarPingMaxActiveRange)) 
                {
                    float pulseScale = 1.0f + (haloPulse * 0.20f); // Scale the halo by 15% at max pulse
                    float pulseAlpha = 1.0f * (1.0f - haloPulse); // Alpha goes from 0.25 to 0 at max pulse, so it fades out as pulse increases.
                    float ringSize =  (1.10f * blipSize) * pulseScale; // Scale the ring size based on the pulse
                    Vector4 haloColor = color_Current * 0.25f * (float)haloPulse; // subtle, fading
                    // Use the same position and orientation as the blip
                    DrawCircle(radarEntityPos, ringSize, radarUp, Color.WhiteSmoke, false, 1.0f * pulseAlpha, 0.00035f); //0.5f * haloPulse

                    //float haloSize = (blipSize * 1.05f) * (1.0f + 0.25f * haloPulse); // slightly larger than blip
                    //DrawQuad(radarEntityPos, radarUp, haloSize, MaterialCircleHollow, haloColor, true); // Soft pulsing halo/glow around the blip...
                    //DrawQuad(radarEntityPos, radarUp, ringSize, MaterialCircleHollow, haloColor * pulseAlpha, true); // Soft expanding/pusling ring around the blip...
                }

                if (currentTarget != null && entity == currentTarget)
                {
                    float outlineSize = blipSize * 1.15f; // Slightly larger than the blip
					Vector4 color = (Color.Yellow).ToVector4() * 2;

                    MyTransparentGeometry.AddBillboardOriented(
                        MaterialTarget,     
                        color,
                        radarEntityPos,
                        viewLeft,           // Align with radar plane
                        viewUp,
                        outlineSize        
                    );
                }
            }

            // Targeting CrossHair
            double crossSize = 0.30;

			Vector3D crossOffset = radarMatrix.Left*radarRadius*1.65 + radarMatrix.Up*radarRadius*crossSize + radarMatrix.Forward*radarRadius*0.35;
			Vector4 crossColor = theSettings.lineColor;

			if (currentTarget != null) 
			{
                targettingLerp_goal = 1;
                targettingLerp = LerpD(targettingLerp, targettingLerp_goal, deltaTimeSinceLastTick * 10);

                Vector3D targetDir = Vector3D.Normalize(currentTarget.WorldVolume.Center - cameraPos) * 0.5;
                double dis2Cam = Vector3D.Distance(cameraPos, cameraPos + targetDir);
                DrawQuadRigid(cameraPos + targetDir, cameraMatrix.Forward, dis2Cam * 0.125 * targettingLerp, theSettings.useHollowReticle ? MaterialLockOnHollow : MaterialLockOn, theSettings.lineColor * (float)targettingLerp, false);
                DrawText(currentTarget.DisplayName, dis2Cam * 0.01, cameraPos + targetDir + (cameraMatrix.Up * dis2Cam * 0.02) + (cameraMatrix.Right * dis2Cam * 0.10 * targettingLerp), cameraMatrix.Forward, theSettings.lineColor);

                string disUnits = "m";
                double distanceToTargetFromLocalGrid = _distanceToCurrentTarget;
                if (_distanceToCurrentTarget > 1500)
                {
                    disUnits = "km";
                    distanceToTargetFromLocalGrid = distanceToTargetFromLocalGrid / 1000;
                }
                DrawText(Convert.ToString(Math.Round(distanceToTargetFromLocalGrid)) + " " + disUnits, dis2Cam * 0.01, cameraPos + targetDir + (cameraMatrix.Up * dis2Cam * -0.02) + (cameraMatrix.Right * dis2Cam * 0.11 * targettingLerp), cameraMatrix.Forward, theSettings.lineColor);

                double dotProduct = Vector3D.Dot(radarMatrix.Forward, targetDir);

                Vector3D targetPipPos = radarPos;
                Vector3D targetPipDir = Vector3D.Normalize(currentTarget.WorldVolume.Center - playerGridPos) * ((double)radarRadius * crossSize * 0.55);

                // Calculate the component of the position along the forward vector
                Vector3D forwardComponent = Vector3D.Dot(targetPipDir, radarMatrix.Forward) * radarMatrix.Forward;

                // Subtract the forward component from the original position to remove the forward/backward contribution
                targetPipDir = targetPipDir - forwardComponent;

                targetPipPos += targetPipDir;
                targetPipPos += crossOffset;

                if (dotProduct > 0.49)
                {
                    crossColor = LINECOLOR_Comp;
                    alignLerp_goal = 0.8;

                    if (gHandler.localGridSpeed > 0.1 && _distanceToCurrentTarget > 100)
                    {
                        double arivalTime = _distanceToCurrentTarget / gHandler.localGridSpeed;
                        string unitsTime = "sec";
                        if (arivalTime > 120)
                        {
                            arivalTime /= 60;
                            unitsTime = "min";

                            if (arivalTime > 60)
                            {
                                arivalTime /= 60;
                                unitsTime = "hrs";
                            }
                        }

                        DrawText(Convert.ToString(Math.Round(arivalTime)) + " " + unitsTime, dis2Cam * 0.01, cameraPos + targetDir + (cameraMatrix.Up * dis2Cam * -0.05) + (cameraMatrix.Right * dis2Cam * 0.11 * targettingLerp), cameraMatrix.Forward, LINECOLOR_Comp);
                    }
                }
                else
                {
                    alignLerp_goal = 1;
                }
                alignLerp = LerpD(alignLerp, alignLerp_goal, deltaTimeSinceLastTick * 20);

                if (dotProduct > 0)
                {
                    DrawQuadRigid(targetPipPos, radarMatrix.Backward, crossSize * 0.125 * 0.125, MaterialCircle, LINECOLOR_Comp, false); //LockOn
                }
                else
                {
                    DrawQuadRigid(targetPipPos, radarMatrix.Backward, crossSize * 0.125 * 0.125, MaterialCircleHollow, LINECOLOR_Comp, false); //LockOn
                }
            } 
			else 
			{
				targettingLerp = 1.5;
				targettingLerp_goal = 1.5;
				alignLerp = 1;
				alignLerp_goal = 1;
			}

			DrawQuadRigid(radarPos+crossOffset, radarMatrix.Backward, crossSize*alignLerp * 0.125, MaterialCross, crossColor, false); //Target
			DrawQuadRigid(radarPos+crossOffset, radarMatrix.Backward, crossSize * 0.125, MaterialCrossOutter, theSettings.lineColor, false); //Target
			DrawQuad(radarPos+crossOffset, radarMatrix.Backward, crossSize * 0.25, MaterialCircleSeeThrough, new Vector4(0, 0, 0, 0.75f)); //Dim Backing
		}



		// List to hold all entities found
		HashSet<VRage.ModAPI.IMyEntity> radarEntities = new HashSet<VRage.ModAPI.IMyEntity>();
		List<RadarPing> radarPings = new List<RadarPing>();

        private void FindEntities()
        {
            // Updated find entities 2025-07-13 by FenixPK. I found in my testing of an empty world that 82244 entities existed all with width = 0.
			// This was causing the scale_current variable to be 0, and causing the radar entity painting loop to be a huge FPS sink. 
			// The below changes should do two things
			// 1) ensure that we only get entities that make sense. Not invisible background game objects that exist even in a brand new empty world.
			// 2) Ensure ping width is within bounds before adding it to the list of radarPings.

			// On second look I think I need to modify this again, as it looked like radarEntities originally was supposed to hold voxels, and entities. And only radarPings was filtered...
			// SO radarPings should always contain voxels, and if not showing we just skip them during Draw.
			// it should not store planets, as it never used them from here anyway, it added them to a planetList for drawing planets and orbits. 
            radarEntities.Clear(); // Always clear first
            radarPings.Clear();

            MyAPIGateway.Entities.GetEntities(radarEntities, entity =>
            {
				if (entity == null) return false;
                if (entity.MarkedForClose || entity.Closed) return false;
                if (entity is MyPlanet) return false;
               //if (!ShowVoxels && entity is IMyVoxelBase) return false;

                // Skip invalid display names or types
                if (entity.DisplayName == null || entity.DisplayName == "Stone") return false;

                return true;
            });


            foreach (var entity in radarEntities)
            {
                RadarPing ping = newRadarPing(entity);

                // Only add if width is valid
                if (ping.Width > 0.00001f && ping.Width < 4f) // safety bounds
                    radarPings.Add(ping);
            }
        }
		
		public long GetLocalPlayerId()
		{
			long fail = -1;

			// Check if the API gateway and the local human player are available
			if (MyAPIGateway.Session?.Player != null)
			{
				return MyAPIGateway.Session.Player.IdentityId;
			}
			else
			{
				// Optionally handle the case where the player is not available
				// This can happen if the code runs too early or if there's an issue with the game context
				//throw new InvalidOperationException("Local player not available.");

				return fail;
			}
		}

		public RelationshipStatus GetGridRelationship(VRage.Game.ModAPI.IMyCubeGrid grid, long playerId)
		{
			var blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
			grid.GetBlocks(blocks, b => b.FatBlock != null && b.FatBlock.OwnerId != 0);

			if (!blocks.Any())
			{
				return RelationshipStatus.Neutral; // No owned blocks found, treat as neutral
			}

			VRage.Game.ModAPI.IMySlimBlock representativeBlock = blocks.First(); // Get the first owned block
			long owner = representativeBlock.FatBlock.OwnerId;

			if (owner == playerId)
			{
				return RelationshipStatus.Friendly; // Owned by the player
			}

			IMyFaction playerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(playerId);
			IMyFaction ownerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(owner);

            const int FriendlyThreshold = 500;

			if (playerFaction != null && ownerFaction != null)
			{
				int rep = MyAPIGateway.Session.Factions.GetReputationBetweenFactions(playerFaction.FactionId, ownerFaction.FactionId);
				if (rep >= FriendlyThreshold)
					return RelationshipStatus.Friendly;
				else if (rep < 0)
					return RelationshipStatus.Hostile;
				else
					return RelationshipStatus.Neutral;
			}

            // Fallback: use in-game block hostility system
            var relation = MyIDModule.GetRelationPlayerBlock(owner, playerId, MyOwnershipShareModeEnum.Faction);

            switch (relation)
            {
                case MyRelationsBetweenPlayerAndBlock.Enemies:
                    return RelationshipStatus.Hostile;
                case MyRelationsBetweenPlayerAndBlock.Owner:
                case MyRelationsBetweenPlayerAndBlock.FactionShare:
                    return RelationshipStatus.Friendly;
                default:
                    return RelationshipStatus.Neutral;
            }

   //         if (playerFaction == null || ownerFaction == null)
			//{
			//	return RelationshipStatus.Hostile; // No faction info available
			//}

			//int reputation = MyAPIGateway.Session.Factions.GetReputationBetweenFactions(playerFaction.FactionId, ownerFaction.FactionId);

			//if (reputation > 0)
			//	return RelationshipStatus.Friendly;
			//else if (reputation < 0)
			//	return RelationshipStatus.Hostile;
			//else
			//	return RelationshipStatus.Neutral;
		}

		public float GetCameraFOV()
		{
			var camera = MyAPIGateway.Session.Camera;

			if (camera != null)
			{
				return camera.FieldOfViewAngle;
			}
			else
			{
				throw new InvalidOperationException("Camera is not available.");
			}
		}

		////==============LOG SCALING==================================================================
		//public MatrixD CreateLogarithmicScaleMatrix(Vector3D entityPosition, Vector3D referencePoint)
		//{
		//	double scaleBase = 10; //Base of the logarithm for scaling calculations

		//	// Calculate distance from the reference point
		//	double distance = Vector3D.Distance(entityPosition, referencePoint);

		//	// Avoid taking logarithm of zero or negative numbers
		//	distance = Math.Max(distance, 0.01); // A small positive value

		//	// Calculate logarithmic scale factor
		//	//double scaleFactor = Math.Max(Math.Exp(distance/radarRange)/2, radarScale); 
		//	double scaleFactor = Math.Max(Math.Exp(distance/radarRange)/2.8, radarScale); 


		//	// Create a scale matrix
		//	//MatrixD scaleMatrix = MatrixD.CreateScale(Math.Pow(1 / scaleFactor, 3));
		//	MatrixD scaleMatrix = MatrixD.CreateScale(1 / scaleFactor);

		//	// Translate entity position to origin, scale, and translate back
		//	MatrixD translationMatrixToOrigin = MatrixD.CreateTranslation(-referencePoint);
		//	//MatrixD translationMatrixBack = MatrixD.CreateTranslation(referencePoint);

		//	// Combine the matrices
		//	MatrixD transformationMatrix = translationMatrixToOrigin * scaleMatrix; //* translationMatrixBack;

		//	return transformationMatrix;
		//}

		//public Vector3D ApplyLogarithmicScaling(Vector3D entityPosition, Vector3D referencePoint)
		//{
		//	MatrixD transformationMatrix = CreateLogarithmicScaleMatrix(entityPosition, referencePoint);
		//	MatrixD scalingMatrix = MatrixD.CreateScale(radarScale, radarScale, radarScale);

		//	return Vector3D.Transform(Vector3D.Transform(entityPosition, transformationMatrix), scalingMatrix);
		//}

        public Vector3D ApplyLogarithmicScaling(Vector3D entityPos, Vector3D referencePos)
        {
			// Modified 2025-07-13 by FenixPK - I believe this accomplishes the same end result, but without the Matrices which should in theory be faster/less CPU intensive.
			// Idea being that radar blips are in logarmithic brackets for where they appear on the radar screen depending on their distance. When far away
			// and approaching at a constant velocity they will slowly move from the outer brackets, the rate at which they jump to the next "bracket" increasing exponentially as they get closer.
			// Hence the whole logarithmic part of this... It's a rather cool effect! 

            Vector3D offset = entityPos - referencePos;
            double distance = offset.Length();

            if (distance < 0.01)
                return Vector3D.Zero;

            // Logarithmic scaling
            double scaleFactor = Math.Max(Math.Exp(distance / radarScaleRange) / 2.8, radarScale);
            double inverseScale = 1.0 / scaleFactor;

            Vector3D direction = offset / distance;
            Vector3D scaledOffset = direction * distance * inverseScale * radarScale;

            return scaledOffset;
        }
		//------------------------------------------------------------------------------------------



		//===============IS GRID ATTACKING?==========================================================
		//      public bool IsGridTargetingPlayer(VRage.Game.ModAPI.IMyCubeGrid grid)
		//{
		//	var turrets = new List<Sandbox.ModAPI.IMyLargeTurretBase>();

		//	IMyPlayer player = MyAPIGateway.Session.Player;
		//	IMyCharacter character = player.Character;
		//	VRage.ModAPI.IMyEntity playerShip = character.Parent as VRage.ModAPI.IMyEntity; // If the player is piloting a ship

		//	foreach (Sandbox.ModAPI.IMyLargeTurretBase turret in turrets)
		//	{
		//		var currentTarget = turret.GetTargetedEntity();
		//			if (currentTarget.EntityId == character.EntityId || (playerShip != null && currentTarget.EntityId == playerShip.EntityId))
		//			{
		//				return true; // The turret is targeting the player or the player's ship
		//			}
		//	}
		//	return false;
		//}

		public bool IsEntityTargetingPlayer(VRage.ModAPI.IMyEntity entity) 
		{
            VRage.Game.ModAPI.IMyCubeGrid entityGrid = entity as VRage.Game.ModAPI.IMyCubeGrid; // If a cube grid we can get that here.
            // Check if even a grid first, can only passively detect grids that are broadcasting.
            if (entityGrid == null)
            {
				return false;
            }
			return IsGridTargetingPlayer(entityGrid);

        }
        public bool IsGridTargetingPlayer(VRage.Game.ModAPI.IMyCubeGrid grid)
        {
			if (grid == null || MyAPIGateway.Session?.Player == null)
			{ 
				return false; 
			}

            // Get all turret blocks on the grid
            List<Sandbox.ModAPI.IMyLargeTurretBase> turrets = new List<Sandbox.ModAPI.IMyLargeTurretBase>();
            turrets = grid.GetFatBlocks<Sandbox.ModAPI.IMyLargeTurretBase>().ToList();

			// We store playerGrid for re-use. 
            long playerShipId = gHandler.localGrid?.EntityId ?? 0;

            foreach (Sandbox.ModAPI.IMyLargeTurretBase turret in turrets)
            {
				if (!turret.IsWorking || !turret.HasTarget) 
				{
                    continue;
                }
                MyDetectedEntityInfo targetInfo = turret.GetTargetedEntity();
				if (targetInfo.IsEmpty())
				{
                    continue;
                }
                if (targetInfo.EntityId == playerShipId)
                {
                    // Turret is targeting the player grid
                    return true;
                }
            }
            return false;
        }
        //-------------------------------------------------------------------------------------------


        //===============External Mod Access=========================================================
        public bool IsModLoaded(long modId)
		{
			var mods = MyAPIGateway.Session.Mods;
			foreach (var mod in mods)
			{
				if ((long)mod.PublishedFileId == modId)
					return true;
			}
			return false;
		}
		//-------------------------------------------------------------------------------------------


		//================SOUND FILES================================================================

		//All sounds stolen from Parag Oswal.
		//https://www.youtube.com/watch?v=mFNvL8ruDbg
		//Sorry.
		// 2025-07-24 FenixPK - Well that sucks... If these are using stolen sounds from a sound pack that is copyrighted, and for sale, we should rectify this.
		// Way too expensive to buy I suspect... I could reach out to the author and ask if we can get a special license that is available for open source usage in a mod like this without paying the $500+ PER SOUND for team license.
		// Will table this for now, and return to it later after I'm done working on what I am capable of fixing. 
		// As of this writing I have reached out to the youtube video creator via Gmail they linked in comments asking for official permission. But I have also noted via screenshot that he has granted
		// non-commercial use permission via replying to youtube comments to multiple people already, so if he isn't active or doesn't reply I'd assume the precedent is set that we can use sounds from that youtube video for free so 
		// long as it is non-commercial. 


		private static string SOUND_DAMAGE = "ED_warbleStatic";
		private static string SOUND_BROKEN = "ED_flubStatic";
		private static string SOUND_ENEMY = "ED_beeps";
		private static string SOUND_NEUTRAL = "ED_simpleBeep";
		private static string SOUND_ZOOMINIT = "ED_clicks3";
		private static string SOUND_ZOOMIN = "ED_clicks1";
		private static string SOUND_ZOOMOUT = "ED_clicks2";
		private static string SOUND_BOOTUP = "ED_marchingClicks";
		private static string SOUND_MONEY = "ED_staticClicks";

//		private static string SOUND_DAMAGE = "ArcBlockCollect";
//		private static string SOUND_BROKEN = "ArcBlockCollect";
//		private static string SOUND_ENEMY = "ArcHudGPSNotification1";
//		private static string SOUND_NEUTRAL = "ArcHudBleep";
//		private static string SOUND_ZOOMINIT = "ArcHudBleep";
//		private static string SOUND_ZOOMIN = "ArcHudAntennaOn";
//		private static string SOUND_ZOOMOUT = "ArcHudAntennaOff";
//		private static string SOUND_BOOTUP = "ArcHudGPSNotification3";
//		private static string SOUND_MONEY = "ArcHudGPSNotification2";

		private static MySoundPair SP_DAMAGE;
		private static MySoundPair SP_BROKEN;
		private static MySoundPair SP_ENEMY;
		private static MySoundPair SP_NEUTRAL;
		private static MySoundPair SP_ZOOMINIT;
		private static MySoundPair SP_ZOOMIN;
		private static MySoundPair SP_ZOOMOUT;
		private static MySoundPair SP_BOOTUP;
		private static MySoundPair SP_MONEY;

		/// <summary>
		/// Timer used to ensure sounds are played too often. Will re-set to 0 when a sound plays, and should get checked before playing a new sound to ensure it hasn't been too recent. 
		/// </summary>
		private Stopwatch timerElapsedTimeSinceLastSound = new Stopwatch();

		private VRage.ModAPI.IMyEntity soundEntity;
		private MyEntity3DSoundEmitter ED_soundEmitter;

		private class queuedSound
		{
			public MySoundPair soundId;
			public Vector3D position;
		}

		private List<queuedSound> queuedSounds = new List<queuedSound>();

		public void PlayCustomSound(MySoundPair soundId, Vector3D position)
		{

			if (timerElapsedTimeSinceLastSound != null && !timerElapsedTimeSinceLastSound.IsRunning) 
			{
				timerElapsedTimeSinceLastSound.Start();
			}

			// Only play one sound per 0.05 seconds.
			if (timerElapsedTimeSinceLastSound.Elapsed.TotalSeconds > 0.05) 
			{

//				queuedSound qs = new queuedSound ();
//				qs.soundId = soundId;
//				qs.position = position;
//				queuedSounds.Add (qs);
//
				ED_soundEmitter.SetPosition(position);
				ED_soundEmitter.PlaySound(soundId);

				timerElapsedTimeSinceLastSound.Restart();
			}
		}

		public void PlayQueuedSounds()
		{
			foreach(var qs in queuedSounds)
			{

				if (ED_soundEmitter != null && qs.soundId != null && soundEntity != null)
				{
					ED_soundEmitter.SetPosition (qs.position);
					ED_soundEmitter.PlaySound (qs.soundId);
				}
			}

			queuedSounds.Clear ();
		}

		private void InitializeAudio()
		{
			soundEntity = MyAPIGateway.Session.LocalHumanPlayer?.Controller?.ControlledEntity?.Entity;

			ED_soundEmitter = 	new MyEntity3DSoundEmitter (soundEntity as VRage.Game.Entity.MyEntity, false, 0.01f);
			ED_soundEmitter.Entity = soundEntity as VRage.Game.Entity.MyEntity;

			SP_DAMAGE = 	new MySoundPair (SOUND_DAMAGE);
			SP_BROKEN = 	new MySoundPair (SOUND_BROKEN);
			SP_ENEMY = 		new MySoundPair (SOUND_ENEMY);
			SP_NEUTRAL = 	new MySoundPair (SOUND_NEUTRAL);
			SP_ZOOMINIT = 	new MySoundPair (SOUND_ZOOMINIT);
			SP_ZOOMIN = 	new MySoundPair (SOUND_ZOOMIN);
			SP_ZOOMOUT = 	new MySoundPair (SOUND_ZOOMOUT);
			SP_BOOTUP = 	new MySoundPair (SOUND_BOOTUP);
			SP_MONEY = 		new MySoundPair (SOUND_MONEY);
		}
		//-------------------------------------------------------------------------------------------

		private void UpdateNewRadarPingStatus(ref RadarPing ping) 
		{
			VRage.ModAPI.IMyEntity entity = ping.Entity;

            if (entity is VRage.Game.ModAPI.IMyCubeGrid)
            {
                VRage.Game.ModAPI.IMyCubeGrid gridEntity = entity as VRage.Game.ModAPI.IMyCubeGrid;

                if (gridEntity != null)
                {
                    double gridWidth = gridEntity.PositionComp.WorldVolume.Radius;
                    ping.Width = (float)gridWidth / 25;

                    long lpID = GetLocalPlayerId();
                    if (lpID != -1)
                    {
                        //DETECT FACTION STATUS
                        RelationshipStatus gridFaction = GetGridRelationship(gridEntity, lpID);
                        switch (gridFaction)
                        {
                            case RelationshipStatus.Friendly:
                                ping.Color = color_GridFriend * GLOW;
                                ping.Status = RelationshipStatus.Friendly;
                                break;
                            case RelationshipStatus.Hostile:
                                ping.Color = color_GridEnemy * GLOW;
                                ping.Status = RelationshipStatus.Hostile;
                                break;
                            case RelationshipStatus.Neutral:
                                ping.Color = color_GridNeutral * GLOW;
                                ping.Status = RelationshipStatus.Neutral;
                                ping.Announced = true;
                                break;
                            default:
                                ping.Color = color_GridNeutral * GLOW;
                                ping.Status = RelationshipStatus.Neutral;
                                ping.Announced = true;
                                break;
                        }
                    }
                    else
                    {
                        ping.Color = color_GridNeutral * GLOW;
                        ping.Status = RelationshipStatus.Neutral;
                        ping.Announced = true;
                    }
                    ping.Width = Clamped(ping.Width, 1f, 3f);
                }
            }
            else if (entity is IMyFloatingObject)
            {
                ping.Color = color_FloatingObject * GLOW;
                ping.Status = RelationshipStatus.FObj;
                ping.Width = 0.5f;
            }
            else if (entity is MyPlanet)
            {
                ping.Color = new Vector4(0, 0, 0, 0);
                ping.Status = RelationshipStatus.FObj;
                ping.Width = 0.00001f;
                ping.Announced = true;
            }
            else if (entity is IMyVoxelBase)
            {
                ping.Color = theSettings.lineColor * GLOW; //color_VoxelBase;
                ping.Status = RelationshipStatus.Vox;
                IMyVoxelBase voxelEntity = entity as IMyVoxelBase;
                if (voxelEntity != null)
                {
                    double voxelWidth = voxelEntity.PositionComp.WorldVolume.Radius;
                    ping.Width = (float)voxelWidth / 250;
                    ping.Width = Clamped(ping.Width, 1f, 3f);
                }
            }
        }

		private void UpdateExistingRadarPingStatus(ref RadarPing ping) 
		{
            VRage.ModAPI.IMyEntity entity = ping.Entity;

            if (entity is VRage.Game.ModAPI.IMyCubeGrid)
            {
                VRage.Game.ModAPI.IMyCubeGrid gridEntity = entity as VRage.Game.ModAPI.IMyCubeGrid;
				RelationshipStatus startingStatus = ping.Status;
                if (gridEntity != null)
                {
                    long lpID = GetLocalPlayerId();
                    if (lpID != -1)
                    {
                        //DETECT FACTION STATUS
                        RelationshipStatus gridFaction = GetGridRelationship(gridEntity, lpID);
                        switch (gridFaction)
                        {
                            case RelationshipStatus.Friendly:
                                ping.Color = color_GridFriend * GLOW;
                                ping.Status = RelationshipStatus.Friendly;
                                break;
                            case RelationshipStatus.Hostile:
                                ping.Color = color_GridEnemy * GLOW;
                                ping.Status = RelationshipStatus.Hostile;
                                break;
                            case RelationshipStatus.Neutral:
                                ping.Color = color_GridNeutral * GLOW;
                                ping.Status = RelationshipStatus.Neutral;
                                break;
                            default:
                                ping.Color = color_GridNeutral * GLOW;
                                ping.Status = RelationshipStatus.Neutral;
                                break;
                        }
                    }
                    else
                    {
                        ping.Color = color_GridNeutral * GLOW;
                        ping.Status = RelationshipStatus.Neutral;
                    }
					if (startingStatus != ping.Status) 
					{
						ping.Announced = false;
					}
                }
            }
        }

		private RadarPing newRadarPing(VRage.ModAPI.IMyEntity entity){
			RadarPing ping = new RadarPing();

			//===Icon Colors===
			//Vector4 color_GridFriend =		(Color.Green).ToVector4()*2;		//IMyCubeGrid
			//Vector4 color_GridEnemy =		(Color.Red).ToVector4()*4;			//   "
			//Vector4 color_GridNeutral =		(Color.Yellow).ToVector4()*2;		//   "
			//Vector4 color_FloatingObject =	(Color.DarkGray).ToVector4();		//IMyFloatingObject
			//Vector4 color_VoxelBase =		(Color.DimGray).ToVector4();		//IMyVoxelBase
			//-----------------

			ping.Entity = entity;
			ping.Announced = false;
			ping.Width = 0f;
			ping.Color  = color_GridNeutral;
			ping.Status = RelationshipStatus.Neutral;

			ping.Time = new Stopwatch();
			ping.Time.Start();

			UpdateNewRadarPingStatus(ref ping);

			return ping;
		}

		public void NewRadarAnimation(VRage.ModAPI.IMyEntity entity, int loops, double lifeTime, double sizeStart, double sizeStop, float fadeStart, float fadeStop, MyStringId material, Vector4 colorStart, Vector4 colorStop, Vector3D offsetStart, Vector3D offsetStop){
			RadarAnimation r = new RadarAnimation();
			r.Entity = entity;
			r.Loops = loops;
			r.LifeTime = lifeTime;
			r.SizeStart = sizeStart;
			r.SizeStop = sizeStop;
			r.FadeStart = fadeStart;
			r.FadeStop = fadeStop;
			r.Material = material;
			r.ColorStart = colorStart;
			r.ColorStop = colorStop;
			r.OffsetStart = offsetStart;
			r.OffsetStop = offsetStop;

			r.Time = new Stopwatch();
			r.Time.Start();

			RadarAnimations.Add(r);
		}
			
		public void NewAlertAnim(VRage.ModAPI.IMyEntity entity)
		{
			if (entity == null) 
			{
				return;
			}

			Vector4 color = new Vector4(Color.Red, 1);
			Vector3D zero = new Vector3D(0, 0, 0);

			NewRadarAnimation(entity, 4, 0.20, 0.02, 0.002, 5, 1, MaterialTarget, color, color, zero, zero);
		}

		public void NewBlipAnim(VRage.ModAPI.IMyEntity entity)
		{
			if (entity == null) 
			{
				return;
			}

			Vector4 color = new Vector4(Color.Yellow, 1);
			Vector3D zero = new Vector3D(0, 0, 0);

			NewRadarAnimation(entity, 1, 0.25, 0.02, 0.002, 3, 1, MaterialTarget, color, color, zero, zero);
		}

		public void UpdateRadarAnimations(Vector3D shipPos)
		{
			List<RadarAnimation> deleteList = new List<RadarAnimation>();

			MatrixD cameraMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
			Vector3 viewUp = cameraMatrix.Up;
			Vector3 viewLeft = cameraMatrix.Left;

			for (int i = 0; i < RadarAnimations.Count; i++)
			{
				if (RadarAnimations[i].Time.Elapsed.TotalSeconds > (RadarAnimations[i].LifeTime * RadarAnimations[i].Loops)) 
				{
					//If time is greater than length of animation, add to the deletion list and skip rendering.
					deleteList.Add(RadarAnimations[i]); // Why was this commented out?
				}
				else
				{
					VRage.ModAPI.IMyEntity entity = RadarAnimations[i].Entity;

					//Calculate time scaling
					double cTime = RadarAnimations[i].Time.Elapsed.TotalSeconds / RadarAnimations[i].LifeTime;
					cTime = cTime - Math.Truncate(cTime);
					cTime = MathHelperD.Clamp(cTime, 0, 1);

					//Lerp Attributes based on time scaling
					Vector4 color = Vector4.Lerp(RadarAnimations[i].ColorStart, RadarAnimations[i].ColorStop, (float)cTime);
					Vector3D offset = Vector3D.Lerp(RadarAnimations[i].OffsetStart, RadarAnimations[i].OffsetStop, (float)cTime);
					float fade = LerpF(RadarAnimations[i].FadeStart, RadarAnimations[i].FadeStop, (float)cTime);
					double size = LerpD(RadarAnimations[i].SizeStart, RadarAnimations[i].SizeStop, cTime);

					Vector3D entityPos = entity.GetPosition();
					Vector3D upSquish = Vector3D.Dot(entityPos, radarMatrix.Up) * radarMatrix.Up;
					//entityPos -= (upSquish*squishValue); //------Squishing the vertical axis on the radar to make it easier to read... but less vertically accurate.

					// Apply radar scaling
					Vector3D scaledPos = ApplyLogarithmicScaling(entityPos, shipPos); 

					// Position on the radar
					Vector3D radarEntityPos = worldRadarPos + scaledPos + offset;

					MyTransparentGeometry.AddBillboardOriented(RadarAnimations[i].Material, color*fade, radarEntityPos, viewLeft, viewUp, (float)size);
				}
			}

			//Delete all animations that were flagged in the prior step.
			foreach(var d in deleteList)
			{
				if (RadarAnimations.Contains(d)) 
				{
					d.Time.Stop();
					RadarAnimations.Remove(d);
				}
			}
			deleteList.Clear();
		}

		void DrawArc(Vector3D center, double radius, Vector3D planeDirection, float startAngle, float endAngle, Vector4 colorr, float width = 0.005f, float gap = 0)
		{
			// Convert start and end angles from degrees to radians
			double startRadians = Math.PI / 180 * startAngle;
			double endRadians = Math.PI / 180 * endAngle;

			// Obtain the up and left vectors from radar matrix which are stable relative to the ship's orientation
			Vector3D up = radarMatrix.Up;
			Vector3D forward = radarMatrix.Forward;
			Vector3D left = radarMatrix.Left;

			// Normalize the plane direction vector
			planeDirection.Normalize();

			// Create a rotation matrix that aligns with the plane defined by the plane direction and the left vector
			MatrixD rotationMatrix = MatrixD.CreateWorld(center, planeDirection, left);

			// Calculate other parameters required for drawing
			int segments = theSettings.lineDetail * Convert.ToInt32(Clamped(Remap((float)radius, 1000, 10000000, 1, 8), 1, 16));
			double angleIncrement = 2 * Math.PI / segments;

			// Prepare to store points of the orbit
			Vector3D[] orbitPoints = new Vector3D[segments];

			// Generate points for the arc
			for (int i = 0; i < segments; i++)
			{
				double angle = angleIncrement * i;

				// Only process segments within the specified angle range
				if (angle >= startRadians && angle <= endRadians)
				{
					Vector3D point = new Vector3D(radius * Math.Cos(angle), radius * Math.Sin(angle), 0);
					point = Vector3D.Transform(point, rotationMatrix); // Apply the rotation matrix
					orbitPoints[i] = point;
				}
			}

			// Draw the arc by connecting points within the angle range
			for (int i = 0; i < segments - 1; i++)
			{
				if (orbitPoints[i] != Vector3D.Zero && orbitPoints[i + 1] != Vector3D.Zero) // Check if points are set
				{

					Vector3D position = orbitPoints [i];
					Vector4 color = colorr;
					if (GetRandomBoolean()) 
					{
						if (glitchAmount > 0.001) {
							float glitchValue = (float)glitchAmount;

							Vector3D offsetRan = new Vector3D ((GetRandomDouble () - 0.5) * 2, (GetRandomDouble () - 0.5) * 2, (GetRandomDouble () - 0.5) * 2);
							double dis2Cam = Vector3D.Distance (MyAPIGateway.Session.Camera.Position, position);

							position += offsetRan * dis2Cam * glitchValue * 0.005;
							color *= GetRandomFloat();
						}
					}



					double dis = Vector3D.Distance(position, orbitPoints [(i + 1) % segments]);
					Vector3D dir = Vector3D.Normalize(orbitPoints [(i + 1) % segments] - position);
					MyTransparentGeometry.AddBillboardOriented(MaterialSquare,color, position,dir,left,(float)dis*gap,width);
				}
			}
		}

		void DrawSegment(Vector3D point1, Vector3D point2, float lineThickness, float dimmerOverride, float thicknessOverride)
		{

			Vector3D cameraPosition = MyAPIGateway.Session.Camera.Position;
			Vector3 direction = point2 - point1;
			float distanceToSegment = DistanceToLineSegment(cameraPosition, point1, point2);
			float segmentThickness = lineThickness;//Math.Max(Remap(distanceToSegment, 1000f, 1000000f, 0f, 1000f) * lineThickness, 0f);
			float dimmer = GlobalDimmer;//Clamped(Remap(distanceToSegment, -10000f, 10000000f, 1f, 0f), 0f, 1f) * GlobalDimmer;

			if (thicknessOverride != 0)
				segmentThickness = thicknessOverride;

			if (dimmerOverride != 0)
				dimmer = dimmerOverride;

			if (dimmer > 0 && segmentThickness > 0)
				DrawLineBillboard(MaterialSquare, theSettings.lineColor * dimmer, point1, direction, 0.9f, segmentThickness, BlendTypeEnum.Standard);
		}



		//==================COLOR===================================================
		public void RGBtoHSV(float r, float g, float b, out float h, out float s, out float v)
		{
			float max = Math.Max(r, Math.Max(g, b));
			float min = Math.Min(r, Math.Min(g, b));
			v = max; // value is maximum of r, g, b

			float delta = max - min;
			if (delta < 0.00001f)
			{
				s = 0;
				h = 0; // undefined, maybe nan?
				return;
			}

			if (max > 0.0f) 
			{
				s = delta / max; // saturation
			}
			else 
			{
				// r = g = b = 0		// s = 0, v is undefined
				s = 0;
				h = float.NaN; // its now undefined
				return;
			}

			if (r >= max)
				h = (g - b) / delta; // between yellow & magenta
			else if (g >= max)
				h = 2.0f + (b - r) / delta; // between cyan & yellow
			else
				h = 4.0f + (r - g) / delta; // between magenta & cyan

			h *= 60.0f; // convert to degrees
			if (h < 0.0f)
				h += 360.0f;
		}



		public float GetComplementaryHue(float hue)
		{
			return (hue + 180.0f) % 360.0f;
		}



		public void HSVtoRGB(float h, float s, float v, out float r, out float g, out float b)
		{
			if (s == 0) 
			{
				// Achromatic (grey)
				r = g = b = v;
				return;
			}

			int i = (int)(h / 60.0f) % 6;
			float f = (h / 60.0f) - i;
			float p = v * (1 - s);
			float q = v * (1 - f * s);
			float t = v * (1 - (1 - f) * s);

			switch (i) 
			{
			case 0:
				r = v;
				g = t;
				b = p;
				break;
			case 1:
				r = q;
				g = v;
				b = p;
				break;
			case 2:
				r = p;
				g = v;
				b = t;
				break;
			case 3:
				r = p;
				g = q;
				b = v;
				break;
			case 4:
				r = t;
				g = p;
				b = v;
				break;
			default:
				r = v;
				g = p;
				b = q;
				break;
			}
		}



		private Vector3 secondaryColor(Vector3 color){
			float h, s, v;
			float r = color.X, g = color.Y, b = color.Z; // Red color example
			RGBtoHSV(r, g, b, out h, out s, out v); // Convert from RGB to HSV
			float complementaryHue = GetComplementaryHue(h); // Get complementary hue
			HSVtoRGB(complementaryHue, s, v, out r, out g, out b); // Convert back to RGB

			Vector3 retColor = new Vector3 (r, g, b);
			return retColor;

		}
		//--------------------------------------------------------------------------



		//==================================SHIP HOLOGRAMS============================================================
		private VRage.ModAPI.IMyEntity currentTarget = null;
		private bool isTargetLocked = false;
		private bool isTargetAnnounced = false;

		/// <summary>
		/// Check player input, could handle various things. Currently just right-click target locking. 
		/// </summary>
		private void CheckPlayerInput()
		{
			if (MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.SECONDARY_TOOL_ACTION))
			{
				//Echo ("Press");
				if (isTargetLocked && currentTarget != null && !currentTarget.MarkedForClose)
				{
					// Toggle off if the same target is still valid
					ReleaseTarget();
				}
				else
				{
					// Attempt to lock on a new target, gets the nearest entity in camera sights.
					var newTarget = FindEntityInSight();

					if (newTarget != null) 
					{
                        // Check if player grid even has passive radar ability?
                        bool playerHasPassiveRadar;
                        bool playerHasActiveRadar;
                        double playerMaxPassiveRange;
                        double playerMaxActiveRange;
                        double relativeDistanceSqr;
						bool entityHasActiveRadar;
						double entityMaxActiveRange;
                        EvaluateGridAntennaStatus(gHandler.localGrid, out playerHasPassiveRadar, out playerHasActiveRadar, out playerMaxPassiveRange, out playerMaxActiveRange);

                        bool canPlayerDetect = false;
                        Vector3D entityPos = new Vector3D();
                        CanGridRadarDetectEntity(playerHasPassiveRadar, playerHasActiveRadar, playerMaxPassiveRange, playerMaxActiveRange, gHandler.localGridControlledEntity.GetPosition(), newTarget, out canPlayerDetect, out entityPos, out relativeDistanceSqr, out entityHasActiveRadar, out entityMaxActiveRange);

                        if (canPlayerDetect)
                        {
                            LockTarget(newTarget);
                            return;
                        }
                    }
                    
                    if (isTargetLocked)
                    {
                        ReleaseTarget();
                    }
				}
			}
			if (!HologramView_PerspectiveAttemptOne) 
			{
                if (MyAPIGateway.Input.IsNewKeyPressed(MyKeys.NumPad4))
                {
					HologramView_Current = ((int)HologramView_Current - 1 < 0 ? HologramView_Side.Bottom : HologramView_Current - 1); // Step down, or roll over to max.
                }
                if (MyAPIGateway.Input.IsNewKeyPressed(MyKeys.NumPad5))
                {
					HologramView_Current = HologramView_Side.Rear; // Reset
                }
                if (MyAPIGateway.Input.IsNewKeyPressed(MyKeys.NumPad6))
                {
                    HologramView_Current = ((int)HologramView_Current + 1 > HologramView_Current_MaxSide ? HologramView_Side.Rear : HologramView_Current + 1); // Step up, or roll over to min.
                }
                if (MyAPIGateway.Input.IsNewKeyPressed(MyKeys.NumPad1))
                {
					HologramView_Current = HologramView_Side.Orbit; 
                }
                if (MyAPIGateway.Input.IsNewKeyPressed(MyKeys.NumPad3))
                {
                    HologramView_Current = HologramView_Side.Perspective; // Perspective
                }
            }

		}

        /// <summary>
        /// This function evaluates a grids antenna blocks. Pseudo SIGINT logic where we can receive passive or output active.
        /// Logic: 
        /// <br></br>
        /// Any antenna that is powered on can passively receive active signals from grids with broadcasting antennas.
        /// <br></br>
        /// Any antenna that is powered on and broadcasting sends active signals out to that broadcast radius and can "ping" voxels or grids within that radius.
        /// <br></br>
        /// Any antenna that is broadcasting will also be able to passively receive signals. 
        /// <br></br>
        /// Any antenna that is broadcasting will have equal maxPassiveRange and maxActiveRange, both = broadcast radius
        /// <br></br>
        /// Any antenna this is not broadcasting will have maxPassiveRange = broadcast radius, and maxActiveRange = 0
        /// <br></br>
        /// Any antenna that is powered off, disabled, or otherwise non-functional will have maxPassiveRange = 0 and maxActiveRange = 0.
        /// </summary>
        /// <param name="grid">The grid to check</param>
        /// <param name="hasPassive">If any antenna is powered on and functional this is true, it can receive signals</param>
        /// <param name="hasActive">If any antenna is powered on, functional, and broadcasting this is true, it can send out signals</param>
        /// <param name="maxPassiveRange">Max broadcast radius from all powered on, functional antennas</param>
        /// <param name="maxActiveRange">Max broadcast radius from all powered on, functional, and broadcasting antennas</param>
        public void EvaluateGridAntennaStatus(VRage.Game.ModAPI.IMyCubeGrid grid, out bool hasPassive, out bool hasActive, out double maxPassiveRange, out double maxActiveRange)
        {
            hasPassive = false;
            hasActive = false;
            maxPassiveRange = 0.0;
            maxActiveRange = 0.0;

            var antennas = grid.GetFatBlocks<Sandbox.ModAPI.IMyRadioAntenna>();

            foreach (var antenna in antennas)
            {
                // Check if antenna is non-functional or tagged as [NORADAR] and continue to check next antenna.
                if (!antenna.IsWorking || !antenna.Enabled || !antenna.IsFunctional)
                {
                    continue;
                }
                if (antenna.CustomData.Contains("[NORADAR]"))
                {
                    continue;
                }
                // If we are here the antenna must be functional and powered on, even if not broadcasting. So set passive to true.
                hasPassive = true;
                double antennaRadius = antenna.Radius; // Store radius once. 
                maxPassiveRange = Math.Max(maxPassiveRange, antennaRadius);

                if (antenna.IsBroadcasting)
                {
                    hasActive = true;
                    maxActiveRange = Math.Max(maxActiveRange, antennaRadius);
                }
            }
            // Safety check if config limits radar range. 
            maxPassiveRange = Math.Min(maxPassiveRange, maxRadarRange);
            maxActiveRange = Math.Min(maxActiveRange, maxRadarRange);
        }


		/// <summary>
		/// This checks if a grid can detect an entity. Requires you know the grid's radar status and pass values in first. Will query the entity's radar status as required.
		/// If detected returns canDetect = true, the entity's Vector3D entityPosReturn, and the relativeDistanceSqrReturn which is the squared distance between gridPos and entityPosReturn. 
		/// </summary>
		/// <param name="gridHasPassiveRadar">Does the grid have passive radar?</param>
		/// <param name="gridHasActiveRadar">Does the grid have active radar?</param>
		/// <param name="gridMaxPassiveRange">What is the grid's max passive detection range?</param>
		/// <param name="gridMaxActiveRange">What is the grid's max active detection range?</param>
		/// <param name="gridPos">What is the grid's Vector3D position?</param>
		/// <param name="entity">Entity to test if grid can detect</param>
		/// <param name="canDetect">Out return true if detected</param>
		/// <param name="entityPosReturn">Out return Vector3D position of the detected entity</param>
		/// <param name="relativeDistanceSqrReturn">Out return squared relative distance between gridPos and entityReturnPos</param>
        public void CanGridRadarDetectEntity(bool gridHasPassiveRadar, bool gridHasActiveRadar, double gridMaxPassiveRange, double gridMaxActiveRange, Vector3D gridPos, VRage.ModAPI.IMyEntity entity, 
			out bool canDetect, out Vector3D entityPosReturn, out double relativeDistanceSqrReturn, out bool entityHasActiveRadarReturn, out double entityMaxActiveRangeReturn) 
		{
			canDetect = false;
			entityPosReturn = new Vector3D();
            relativeDistanceSqrReturn = 0;
			entityHasActiveRadarReturn = false;
			entityMaxActiveRangeReturn = 0;

			if (!gridHasPassiveRadar && !gridHasActiveRadar) 
			{
				// If no functional radar we can't detect anything
				return; // Can't detect it, return defaults.
            }
            VRage.Game.ModAPI.IMyCubeGrid entityGrid = entity as VRage.Game.ModAPI.IMyCubeGrid; // Is this a grid? will be Null if not.
			Vector3D entityPos = entity.GetPosition();

            // Check if even a grid first
            if (entityGrid == null && !gridHasActiveRadar)
            {
				// If grid has no active radar, and the entity to detect is not a grid, we can safely exit and return canDetect = false.
				// We make this assertation that anything that isn't a grid can't possibly have active radar.
				// In future I may have to change this if I do SigInt rework to allow other active grids signals to paint nearby entities...
				return; // Can't detect it, return defaults.
            }

			// Store calculate variables once here and re-use.
			double gridMaxActiveRangeSqr = gridMaxActiveRange * gridMaxActiveRange;
            Vector3D relativePos = entityPos - gridPos; // Position of entity relative to grid
            double relativeDistanceSqr = relativePos.LengthSquared();

            // At this point we may have passive only, or active+passive. 
            if (entityGrid == null && gridHasActiveRadar)
			{
				// Check if grid can actively detect non-grid entities
				if (IsWithinRadarRadius(relativeDistanceSqr, gridMaxActiveRangeSqr))
				{
					canDetect = true;
					entityPosReturn = entityPos;
                    relativeDistanceSqrReturn = relativeDistanceSqr;
                    return; // Detected, set values and return
				}
				else 
				{
					return; // entity is not a grid, and outside grid's active radar range so we return now and skip further checks.
				}
            } 
            else 
			{
                // If a entity is a grid, the grid can detect it passively OR actively.
                // entityGrid might be actively broadcasting a signal that reaches grid's maxPassiveRange.
                // entityGrid might be within grid's maxActiveRange
                // We should check if grid has active radar first and test that first, as we might be in passive only mode and could save some cycles
                // Then if still not detected check passive where we evaluate the entityGrid's antenna status.
                double gridMaxPassiveRangeSqr = gridMaxPassiveRange * gridMaxPassiveRange;
                bool entityHasPassiveRadar = false; // Not used
                bool entityHasActiveRadar = false; // Is entityGrid sending out signals grid can passively receive?
                double entityMaxPassiveRange = 0; // Not used
                double entityMaxActiveRange = 0; // Grid can receive entityGrid's signals if within this range.

                EvaluateGridAntennaStatus(entityGrid, out entityHasPassiveRadar, out entityHasActiveRadar, out entityMaxPassiveRange, out entityMaxActiveRange);
				entityHasActiveRadarReturn = entityHasActiveRadar; // Can return this for re-use elsewhere.
				entityMaxActiveRangeReturn = entityMaxActiveRange; // Can return this for re-use elsewhere.
                if (gridHasActiveRadar)
				{
					if (IsWithinRadarRadius(relativeDistanceSqr, gridMaxActiveRangeSqr))
					{
						canDetect = true;
						entityPosReturn = entityPos;
                        relativeDistanceSqrReturn = relativeDistanceSqr;
                        return;  // Detected, set values and return
					}
				}
				else 
				{
                    if (!entityHasActiveRadar)
                    {
                        // If entity is not actively broadcasting it can't be passively detected. 
                        return; // Can't detect it, return defaults.
                    }

					double entityMaxActiveRangeSqr = entityMaxActiveRange * entityMaxActiveRange;
					if (IsWithinRadarRadius(relativeDistanceSqr, gridMaxPassiveRangeSqr) && IsWithinRadarRadius(relativeDistanceSqr, entityMaxActiveRangeSqr))
					{
                        // entityGrid is within grid's passive detection range and grid is within entityGrid's active detection range.
                        canDetect = true;
                        entityPosReturn = entityPos;
						relativeDistanceSqrReturn = relativeDistanceSqr;
                        return;  // Detected, set values and return
                    }
                }
            }
        }

		public bool IsWithinRadarRadius(double relativeDistanceSqr, double radarDetectionRangeSqr) 
		{
			if (relativeDistanceSqr <= radarDetectionRangeSqr)
			{
				return true;
			}
			return false;
		}

		///// <summary>
		///// Returns true if player grid can passively detect entity grid passed in. By virtue of passive radar only detecting things that can broadcast it will return false for non-grids like voxels.
		///// </summary>
		///// <param name="entity">The entity to check, will first see if it is a grid and return false if not. Otherwise proceeds to check params.</param>
		///// <returns></returns>
		//public void CanPlayerDetectPassively(bool playerHasPassiveRadar, VRage.ModAPI.IMyEntity entity, out bool canDetect, out Vector3D entityPosReturn) 
		//{
  //          VRage.Game.ModAPI.IMyCubeGrid entityGrid = entity as VRage.Game.ModAPI.IMyCubeGrid; // If a cube grid we can get that here.
  //          // Check if even a grid first, can only passively detect grids that are broadcasting.
  //          if (entityGrid == null)
  //          {
		//		canDetect = false;
  //              entityPosReturn = new Vector3D();
		//		return;
  //          }

		//	if (!playerHasPassiveRadar) 
		//	{
  //              // If no passive radar we can't passively detect anything.
  //              canDetect = false;
  //              entityPosReturn = new Vector3D();
  //              return;
  //          }

		//	bool entityHasPassiveRadar = false;
  //          bool entityHasActiveRadar = false;
  //          double entityMaxActiveRange = 0;
  //          EvaluateGridAntennaStatus(entityGrid, out entityHasPassiveRadar, out entityHasActiveRadar, out entityMaxActiveRange);
		//	if (!entityHasActiveRadar) 
		//	{
  //              // If entity is not actively broadcasting it can't be passively detected. 
  //              canDetect = false;
  //              entityPosReturn = new Vector3D();
  //              return;
  //          }

  //          Vector3D playerGridPos = gHandler.localGridEntity.GetPosition();
  //          Vector3D entityPos = entityGrid.GetPosition();
  //          Vector3D relativePos = entityPos - playerGridPos; // Position relative to the grid
  //          double relativeDistance = relativePos.LengthSquared();
		//	double radarDetectionRange = Math.Min(entityMaxActiveRange, maxRadarRange);

  //          // If their active broadcast range is within player, player can passively detect grid entity.
  //          if (relativeDistance <= (radarDetectionRange * radarDetectionRange))
		//	{
  //              canDetect = true;
  //              entityPosReturn = entityPos;
  //              return;
  //          }
		//	else 
		//	{
  //              canDetect = false;
  //              entityPosReturn = entityPos;
  //              return;
  //          }
  //      }

		///// <summary>
		///// Returns true if player has active radar on and entity is within range (voxel or grid). If active radar is off, returns false. 
		///// </summary>
		///// <param name="entity">The entity to check if in range</param>
		///// <returns></returns>
		//public void CanPlayerDetectActively(bool playerHasActiveRadar, double playerMaxActiveRange, VRage.ModAPI.IMyEntity entity, out bool canDetect, out Vector3D entityPosReturn) 
		//{
  //          if (!playerHasActiveRadar)
  //          {
  //              // If no active radar we can't detect actively.
  //              canDetect = false;
  //              entityPosReturn = new Vector3D();
  //              return;
  //          }

  //          Vector3D playerGridPos = gHandler.localGridEntity.GetPosition();
  //          Vector3D entityPos = entity.GetPosition();
  //          Vector3D relativePos = entityPos - playerGridPos; // Position relative to the grid
  //          double relativeDistance = relativePos.LengthSquared();
  //          double radarDetectionRange = Math.Min(playerMaxActiveRange, maxRadarRange);

  //          if (relativeDistance <= (radarDetectionRange * radarDetectionRange))
  //          {
  //              canDetect = true;
  //              entityPosReturn = entityPos;
  //              return;
  //          }
  //          else
  //          {
  //              canDetect = false;
  //              entityPosReturn = entityPos;
  //              return;
  //          }
  //      }

		/// <summary>
		/// Clears the target
		/// </summary>
		private void ReleaseTarget()
		{
            isTargetLocked = false;
            HG_initializedTarget = false;
            HG_activationTimeTarget = 0;
            currentTarget = null;
            PlayCustomSound(SP_ZOOMIN, worldRadarPos);
        }

		/// <summary>
		/// Target locks the entity passed in
		/// </summary>
		/// <param name="newTarget"></param>
		private void LockTarget(VRage.ModAPI.IMyEntity newTarget) 
		{
            currentTarget = newTarget;
            isTargetLocked = true;
            NewBlipAnim(newTarget);
            PlayCustomSound(SP_ZOOMOUT, worldRadarPos);
			if (_isWeaponCore && _weaponCoreInitialized && _weaponCoreWeaponsInitialized) 
			{
				LockTargetWeaponCore(newTarget);
			}
        }

        private Dictionary<string, Delegate> weaponCoreApi = null;
        private Action<Sandbox.ModAPI.IMyTerminalBlock, VRage.ModAPI.IMyEntity> setTarget;
        Action<Sandbox.ModAPI.IMyTerminalBlock, VRage.ModAPI.IMyEntity, bool, bool> setTargetFocus;
        Func<Sandbox.ModAPI.IMyTerminalBlock, bool> hasCoreWeapon;
        private const long WEAPONCORE_MOD_ID = 5649865810; // WC's Mod Communication ID (check your version if this changes)

        private void InitWeaponCore()
        {
			if (weaponCoreApi != null || _weaponCoreInitialized)
			{ 
				return; // Already initialized
            }

            weaponCoreApi = new Dictionary<string, Delegate>();

            MyAPIGateway.Utilities.SendModMessage(WEAPONCORE_MOD_ID, weaponCoreApi);

            if (weaponCoreApi.Count == 0)
            {
                MyLog.Default.WriteLine("[MyMod] WeaponCore API not available.");
                return;
            }

            Delegate setTargetDelegate = null;
            if (weaponCoreApi.TryGetValue("SetTarget", out setTargetDelegate))
                setTarget = (Action<Sandbox.ModAPI.IMyTerminalBlock, VRage.ModAPI.IMyEntity>)setTargetDelegate;
            Delegate focusDelegate = null;
            if (weaponCoreApi.TryGetValue("SetTargetFocus", out focusDelegate))
                setTargetFocus = (Action<Sandbox.ModAPI.IMyTerminalBlock, VRage.ModAPI.IMyEntity, bool, bool>)focusDelegate;
            Delegate hasCoreWeaponDelegate = null;
            if (weaponCoreApi.TryGetValue("HasCoreWeapon", out hasCoreWeaponDelegate))
                hasCoreWeapon = (Func<Sandbox.ModAPI.IMyTerminalBlock, bool>)hasCoreWeaponDelegate;

            _weaponCoreInitialized = true;

            MyAPIGateway.Utilities.ShowMessage("WC", "WeaponCore API initialized");  
        }

		
        public void InitializeWeaponCoreWeapons()
        {
			if (!_weaponCoreWeaponsInitialized && _weaponCoreInitialized) 
			{
                List<VRage.Game.ModAPI.IMySlimBlock> blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
                gHandler.localGrid.GetBlocks(blocks);

                foreach (VRage.Game.ModAPI.IMySlimBlock block in blocks)
                {
					Sandbox.ModAPI.IMyTerminalBlock terminalBlock = block.FatBlock as Sandbox.ModAPI.IMyTerminalBlock;
                    if (terminalBlock != null && terminalBlock.IsFunctional)
                    {
                        if (IsWeaponCoreWeapon(terminalBlock))
                        {
                            if (!_localGridWCWeapons.Contains(terminalBlock))
                            {
                                _localGridWCWeapons.Add(terminalBlock);
                            }

                        }
                    }
                }
                _weaponCoreWeaponsInitialized = true;
            }
        }


        private void LockTargetWeaponCore(VRage.ModAPI.IMyEntity newTarget)
        {
            InitWeaponCore(); // Ensure API is ready

			if (setTarget == null || newTarget == null)
			{
                return;
            }
               

			//List<Sandbox.ModAPI.IMyTerminalBlock> weaponCoreTurrets = new List<Sandbox.ModAPI.IMyTerminalBlock>();
			//var grid = (gHandler?.localGridControlledEntity as VRage.Game.ModAPI.IMyCubeGrid);
			//if (grid == null)
			//    return;

			//MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid)
			//    .GetBlocksOfType<Sandbox.ModAPI.IMyTerminalBlock>(weaponCoreTurrets, b =>
			//        b.DefinitionDisplayNameText != null && b.DefinitionDisplayNameText.Contains("WeaponCore")); // Or better filtering

			//foreach (var turret in weaponCoreTurrets)
			//{
			//    setTarget(turret, newTarget);
			//}

			// Parameters:
			// - Weapon block (any WC weapon on the grid)
			// - Target entity
			// - Focus: true to lock it, false to clear
			// - Subsystem: false = full grid, true = subsystem targeting
			setTargetFocus(_localGridWCWeapons.First<Sandbox.ModAPI.IMyTerminalBlock>(), newTarget, true, false);
        }

        bool IsWeaponCoreWeapon(Sandbox.ModAPI.IMyTerminalBlock block)
        {
			if (hasCoreWeapon != null && hasCoreWeapon(block))
			{
				return true;
			}
			else 
			{
                return block.DefinitionDisplayNameText.Contains("Weapon") ||
                           block.BlockDefinition.SubtypeName.Contains("WC") ||
                           block.CustomName.Contains("WC");
            }	
        }


        /// <summary>
        /// Uses camera position to calculate the closest entity, uses GetTopMostParent to return main grid not subgrids like wheels etc. Also uses a min block count to help prevent debris being picked up.
        /// </summary>
        /// <returns></returns>
        private VRage.ModAPI.IMyEntity FindEntityInSight()
		{
			IMyCamera camera = MyAPIGateway.Session.Camera;
			Vector3D cameraPosition = camera.WorldMatrix.Translation;
			Vector3D cameraForward = camera.WorldMatrix.Forward;

            HashSet<VRage.ModAPI.IMyEntity> entities = new HashSet<VRage.ModAPI.IMyEntity>();
			MyAPIGateway.Entities.GetEntities(entities, e => {
                //           if (e is VRage.Game.ModAPI.IMyCubeGrid grid)
                //           {
                //return grid.WorldVolume.Radius > 1;
                //           }
                VRage.Game.ModAPI.IMyCubeGrid cubeGrid = e as VRage.Game.ModAPI.IMyCubeGrid;
                if (cubeGrid != null)
                {
                    List<VRage.Game.ModAPI.IMySlimBlock> tempBlocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
                    cubeGrid.GetBlocks(tempBlocks);
                    return tempBlocks.Count >= theSettings.minTargetBlocksCount;
                }

                return e is IMyCharacter;
            });

            

            VRage.ModAPI.IMyEntity closestEntity = null;
			double highestDotProduct = 0.0;  // This will store the highest dot product found

			foreach (VRage.ModAPI.IMyEntity entity in entities)
			{
				if (entity == null)
				{
					continue;
				}

				Vector3D entityPosition = entity.GetPosition();
				Vector3D directionToEntity = Vector3D.Normalize(entityPosition - cameraPosition);
				double distanceToEntity = Vector3D.Distance(cameraPosition, entityPosition);

				double dotProduct = Vector3D.Dot(cameraForward, directionToEntity);

				// Check both the dot product and the distance
				if (dotProduct > highestDotProduct && dotProduct >= 0.899 && distanceToEntity <= 50000)
				{
					highestDotProduct = dotProduct;
					closestEntity = entity.GetTopMostParent(); // Use GetTopmostParent to return main grid, not subgrids like wheels etc.
                }
			}

			return closestEntity;
		}


		private VRage.ModAPI.IMyEntity GetControlledGrid()
		{
			// Obtain the cockpit or ship controller that the player is directly controlling
			var cockpit = MyAPIGateway.Session.ControlledObject as Sandbox.ModAPI.IMyCockpit;
			if (cockpit != null)
			{
				// Return the top-most parent, which should be the grid itself
				return cockpit.CubeGrid;
			}

			return null; // If not controlling a cockpit, return null or handle other types similarly
		}

		private string Echo_String_Prev;
		private void Echo(string message)
		{
			bool isGuiVisible = MyAPIGateway.Gui.IsCursorVisible;
			if (isGuiVisible) 
			{
				return;
			}

			if (message != Echo_String_Prev) 
			{
				// This method should be replaced with your actual logging or display method
				MyAPIGateway.Utilities.ShowMessage ("Echo", message);
			}

			Echo_String_Prev = message;
		}


		public class BlockTracker
		{
			public VRage.Game.ModAPI.IMySlimBlock Block;
			public Vector3D Position;
			public Vector3D WorldRelativePosition;
			public Vector3D HologramDrawPosition;
			public double HealthMax;
			public double HealthCurrent;
			public double HealthLast;
			public bool IsJumpDrive;
			public double JumpDriveLastStoredPower;
			public double JumpDrive_Avg;
			public Sandbox.ModAPI.Ingame.IMyJumpDrive JumpDrive;
		}

		private List<BlockTracker> localGridBlocks = new List<BlockTracker>();
		private List<BlockTracker> targetGridBlocks = new List<BlockTracker>();

		private List<BlockTracker> localGridDrives = new List<BlockTracker>();
		private List<BlockTracker> targetGridDrives = new List<BlockTracker>();
		private List<Sandbox.ModAPI.IMyTerminalBlock> _localGridWCWeapons = new List<Sandbox.ModAPI.IMyTerminalBlock>();

		private double localGridHealthMax = 0;
		private double localGridHealthCurrent = 0;

		private double targetGridHealthMax = 0;
		private double targetGridHealthCurrent = 0;

		private int localGridBlockCounter = 0;
		private int targetBlockCounter = 0;

		private double localGridShieldsMax = 0;
		private double localGridShieldsCurrent = 0;
		private double localGridShieldsLast = 0;

		private double targetShieldsMax = 0;
		private double targetShieldsCurrent = 0;
		private double targetShieldsLast = 0;

		private double localGridTimeToReady = 0;
		private double targetTimeToReady = 0;

		private VRage.Game.ModAPI.IMyCubeGrid hologramLocalGrid;
		private VRage.Game.ModAPI.IMyCubeGrid hologramTargetGrid;
		private List<Vector3D> HG_blockPositions;
		private List<Vector3D> HG_blockPositionsTarget;
		private MatrixD HG_scalingMatrix;
		private MatrixD HG_scalingMatrixTarget;
		private bool HG_initialized = false;
		private bool HG_initializedTarget = false;
		private double HG_Scale = 0.0075;//0.0125;
		private Vector3D _hologramRightOffset_HardCode = new Vector3D(-0.2, 0.075, 0); 
        private Vector3D _hologramOffsetLeft_HardCode = new Vector3D(0.2, 0.075, 0);
        private double HG_scaleFactor = 10;
		private double HG_activationTime = 0;
		private double HG_activationTimeTarget = 0;

        /// <summary>
        /// This function draws a hologram of a grid's status, including health, shields, and block status.
        /// </summary>
        /// <param name="hgPos">Position for the hologram, could be left or right. Original function has "your" HG on the right and "their" HG on the left. </param>
        /// <param name="shieldPos">Position for the shield</param>
        /// <param name="textPosOffset">Text offset for writing hitpoints string</param>
        /// <param name="activationTime"></param>
        /// <param name="shieldLast"></param>
        /// <param name="shieldCurrent"></param>
        /// <param name="shieldMax"></param>
        /// <param name="healthCurrent"></param>
        /// <param name="healthMax"></param>
        /// <param name="deltaTime"></param>
        /// <param name="drives"></param>
        /// <param name="blocks"></param>
        /// <param name="timeToReady"></param>
        /// <param name="fontSize"></param>
        /// <param name="blockCount"></param>
        private void DrawHologramStatus(Vector3D hgPos, Vector3D shieldPos, Vector3D textPosOffset, ref double activationTime, ref double shieldLast, ref double shieldCurrent, ref double shieldMax, ref double healthCurrent, double healthMax, 
			double deltaTime, List<BlockTracker> drives, List<BlockTracker> blocks, ref double timeToReady, double fontSize, ref int blockCount)
        {
            // An alpha value that ramps up over time, used for boot-up effect
            double bootUpAlpha = activationTime;
            bootUpAlpha = ClampedD(bootUpAlpha, 0, 1);
            bootUpAlpha = Math.Pow(bootUpAlpha, 0.25);

            //Shields, 
            if (shieldMax > 0.01)
            {
                double tempShields = LerpD(shieldLast, shieldCurrent, deltaTime * 2);

               // Vector3D ShieldPos_Right = worldRadarPos + radarMatrix.Left * -radarRadius + radarMatrix.Left * HG_Offset.X + radarMatrix.Up * HG_Offset.Y + radarMatrix.Forward * HG_Offset.Z;
                drawShields(shieldPos, tempShields, shieldMax, bootUpAlpha, timeToReady);
                shieldLast = tempShields;
            }

            double hitPoints = 1;
            if (healthMax > 0)
            {
                hitPoints = healthCurrent / healthMax;
                hitPoints = Math.Pow(hitPoints, 4);
            }
            hitPoints = Math.Round(hitPoints * 100 * bootUpAlpha);

            //hgPos_Right = worldRadarPos + radarMatrix.Right * radarRadius + radarMatrix.Left * HG_Offset.X + radarMatrix.Down * 0.0075 + (radarMatrix.Forward * fontSize * 2);
            DrawArc(hgPos, 0.065, radarMatrix.Up, 0, 90 * ((float)hitPoints / 100) * (float)bootUpAlpha, theSettings.lineColor, 0.015f, 1f);

            string hitPointsString = Convert.ToString(Math.Ceiling(healthCurrent / 100 * bootUpAlpha));
            Vector3D textPos = hgPos + (radarMatrix.Left * (hitPointsString.Length * fontSize)) + (radarMatrix.Up * fontSize) + (radarMatrix.Forward * fontSize * 0.5) + textPosOffset;
            DrawText(hitPointsString, fontSize, textPos, radarMatrix.Forward, theSettings.lineColor);
        }


        private void UpdateHologramStatus(ref double activationTime, ref double shieldLast, ref double shieldCurrent, ref double shieldMax, ref double healthCurrent,
            double deltaTime, List<BlockTracker> drives, List<BlockTracker> blocks, ref double timeToReady, ref int blockCount)
        {
           

            activationTime += deltaTime * 0.667;

            HG_UpdateBlockStatus(ref blockCount, blocks, ref healthCurrent);
            double tempTimeToReady = 0;
            HG_UpdateDriveStatus(drives, ref shieldCurrent, ref shieldMax, out tempTimeToReady);
            if (tempTimeToReady > 0)
            {
                timeToReady = tempTimeToReady;
            }
        }



        // Currently handles both rendering and update, need to separate out this logic...
        public void HG_Update_OLD()
		{
			if (!Drives_deltaTimer.IsRunning) {
				Drives_deltaTimer.Start();
			}

			Drives_deltaTime = Drives_deltaTimer.Elapsed.TotalSeconds;
			Drives_deltaTimer.Restart();
			//Drives_deltaTimer.Start ();

			if (!HG_initialized)
			{
				localGridHealthMax = 0;
				localGridHealthCurrent = 0;
				localGridBlockCounter = 0;
				localGridShieldsMax = 0;
				localGridShieldsCurrent = 0;

				HG_InitializeLocalGrid();
				HG_initialized = true;
				HG_activationTime = 0;
			}

			double fontSize = 0.005;

			
			if (hologramLocalGrid != null && theSettings.enableHologramsGlobal && EnableHolograms_you) //God, I really should have made this far more generic so I don't have to manage the same code for the player and the target seperately...
				// No problem, FenixPK has your got your back jack. Standardized this 2025-07-14. 
			{

				HG_DrawHologramLocal(hologramLocalGrid, localGridBlocks);
				Vector3D hgPos_Right = worldRadarPos + radarMatrix.Right * radarRadius + radarMatrix.Left * _hologramRightOffset_HardCode.X + radarMatrix.Down * 0.0075 + (radarMatrix.Forward * fontSize * 2);
				Vector3D textPos_Offset = (radarMatrix.Left * 0.065);
				Vector3D shieldPos_Right = worldRadarPos + radarMatrix.Left * -radarRadius + radarMatrix.Left * _hologramRightOffset_HardCode.X + radarMatrix.Up * _hologramRightOffset_HardCode.Y + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
				DrawHologramStatus(hgPos_Right, shieldPos_Right, textPos_Offset, ref HG_activationTime, ref localGridShieldsLast, ref localGridShieldsCurrent,
					ref localGridShieldsMax, ref localGridHealthCurrent, localGridHealthMax, deltaTimeSinceLastTick, localGridDrives, localGridBlocks, ref localGridTimeToReady, fontSize, ref localGridBlockCounter);
			}


			if (isTargetLocked && currentTarget != null && theSettings.enableHologramsGlobal && EnableHolograms_them) {
				if (!HG_initializedTarget) {

					targetGridHealthMax = 0;
					targetGridHealthCurrent = 0;
					targetBlockCounter = 0;
					targetShieldsMax = 0;
					targetShieldsCurrent = 0;

					HG_InitializeTargetGrid(currentTarget);
					HG_initializedTarget = true;
					HG_activationTimeTarget = 0;
				}
				if (HG_initializedTarget) 
				{
                    HG_DrawHologramTarget(hologramTargetGrid, targetGridBlocks);
                    Vector3D hgPos_Left = worldRadarPos + radarMatrix.Left * radarRadius + radarMatrix.Right * _hologramRightOffset_HardCode.X + radarMatrix.Down * 0.0075 + (radarMatrix.Forward * fontSize * 2);
                    Vector3D textPos_Offset = (radarMatrix.Right * 0.065);
                    Vector3D shieldPos_Left = worldRadarPos + radarMatrix.Right * -radarRadius + radarMatrix.Right * _hologramRightOffset_HardCode.X + radarMatrix.Up * _hologramRightOffset_HardCode.Y + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
                    DrawHologramStatus(hgPos_Left, shieldPos_Left, textPos_Offset, ref HG_activationTimeTarget, ref targetShieldsLast, ref targetShieldsCurrent,
                        ref targetShieldsMax, ref targetGridHealthCurrent, targetGridHealthMax, deltaTimeSinceLastTick, targetGridDrives, targetGridBlocks, ref targetTimeToReady, fontSize, ref targetBlockCounter);
				}
			} else {
				HG_initializedTarget = false;
			}
		}

        public void HG_Update()
        {
            if (!Drives_deltaTimer.IsRunning)
            {
                Drives_deltaTimer.Start();
            }

            Drives_deltaTime = Drives_deltaTimer.Elapsed.TotalSeconds;
            Drives_deltaTimer.Restart();
            //Drives_deltaTimer.Start ();

            if (!HG_initialized)
            {
                localGridHealthMax = 0;
                localGridHealthCurrent = 0;
                localGridBlockCounter = 0;
                localGridShieldsMax = 0;
                localGridShieldsCurrent = 0;

                HG_InitializeLocalGrid();
                HG_initialized = true;
                HG_activationTime = 0;

                
            }
            if (hologramLocalGrid != null && theSettings.enableHologramsGlobal && EnableHolograms_you) //God, I really should have made this far more generic so I don't have to manage the same code for the player and the target seperately...
                                                                                                       // No problem, FenixPK has your got your back jack. Standardized this 2025-07-14. 
            {
                HG_UpdateHologramLocal(hologramLocalGrid, localGridBlocks);
                UpdateHologramStatus(ref HG_activationTime, ref localGridShieldsLast, ref localGridShieldsCurrent,
                        ref localGridShieldsMax, ref localGridHealthCurrent, deltaTimeSinceLastTick, localGridDrives, localGridBlocks, ref localGridTimeToReady,  ref localGridBlockCounter);
            }



            if (isTargetLocked && currentTarget != null && theSettings.enableHologramsGlobal && EnableHolograms_them)
            {
                if (!HG_initializedTarget)
                {
                    targetGridHealthMax = 0;
                    targetGridHealthCurrent = 0;
                    targetBlockCounter = 0;
                    targetShieldsMax = 0;
                    targetShieldsCurrent = 0;

                    HG_InitializeTargetGrid(currentTarget);
                    HG_initializedTarget = true;
                    HG_activationTimeTarget = 0;
                }
                if (HG_initializedTarget)
                {
                    HG_UpdateHologramTarget(hologramTargetGrid, targetGridBlocks);
                    UpdateHologramStatus(ref HG_activationTimeTarget, ref targetShieldsLast, ref targetShieldsCurrent,
                        ref targetShieldsMax, ref targetGridHealthCurrent, deltaTimeSinceLastTick, targetGridDrives, targetGridBlocks, ref targetTimeToReady, ref targetBlockCounter);
                }
            }
            else
            {
                HG_initializedTarget = false;
            }
        }

        /// <summary>
        /// Just the Drawing/Rendering logic for the local grid and target grid holograms.
        /// </summary>
        public void HG_Draw()
        {
            double fontSize = 0.005;
            
            if (hologramLocalGrid != null && theSettings.enableHologramsGlobal && EnableHolograms_you) //God, I really should have made this far more generic so I don't have to manage the same code for the player and the target seperately...                                                                                     // No problem, FenixPK has your got your back jack. Standardized this 2025-07-14. 
            {
                if (HG_initialized)
                {
                    HG_DrawHologramLocal(hologramLocalGrid, localGridBlocks);
                    Vector3D hgPos_Right = worldRadarPos + radarMatrix.Right * radarRadius + radarMatrix.Left * _hologramRightOffset_HardCode.X + radarMatrix.Down * 0.0075 + (radarMatrix.Forward * fontSize * 2);
                    Vector3D textPos_Offset = (radarMatrix.Left * 0.065);
                    Vector3D shieldPos_Right = worldRadarPos + radarMatrix.Left * -radarRadius + radarMatrix.Left * _hologramRightOffset_HardCode.X + radarMatrix.Up * _hologramRightOffset_HardCode.Y + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
                    DrawHologramStatus(hgPos_Right, shieldPos_Right, textPos_Offset, ref HG_activationTime, ref localGridShieldsLast, ref localGridShieldsCurrent,
                        ref localGridShieldsMax, ref localGridHealthCurrent, localGridHealthMax, deltaTimeSinceLastTick, localGridDrives, localGridBlocks, ref localGridTimeToReady, fontSize, ref localGridBlockCounter);
                }
            }
            if (isTargetLocked && currentTarget != null && theSettings.enableHologramsGlobal && EnableHolograms_them)
            {
                if (HG_initializedTarget)
                {
                    HG_DrawHologramTarget(hologramTargetGrid, targetGridBlocks);
                    //HG_DrawHologramTarget_OLD(hologramTargetGrid, targetGridBlocks);
                    Vector3D hgPos_Left = worldRadarPos + radarMatrix.Left * radarRadius + radarMatrix.Right * _hologramRightOffset_HardCode.X + radarMatrix.Down * 0.0075 + (radarMatrix.Forward * fontSize * 2);
                    Vector3D textPos_Offset = (radarMatrix.Right * 0.065);
                    Vector3D shieldPos_Left = worldRadarPos + radarMatrix.Right * -radarRadius + radarMatrix.Right * _hologramRightOffset_HardCode.X + radarMatrix.Up * _hologramRightOffset_HardCode.Y + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
                    DrawHologramStatus(hgPos_Left, shieldPos_Left, textPos_Offset, ref HG_activationTimeTarget, ref targetShieldsLast, ref targetShieldsCurrent,
                        ref targetShieldsMax, ref targetGridHealthCurrent, targetGridHealthMax, deltaTimeSinceLastTick, targetGridDrives, targetGridBlocks, ref targetTimeToReady, fontSize, ref targetBlockCounter);
                }
            }
        }

        private void drawShields(Vector3D pos, double sp_cur, double sp_max, double bootTime, double time2ready)
		{

			double sp = sp_cur/sp_max;

			double boot1 = ClampedD(RemapD(bootTime, 0.5, 0.8, 0, 1), 0, 1);
			double boot2 = ClampedD(RemapD(bootTime, 0.8, 0.925, 0, 1), 0, 1);
			double boot3 = ClampedD(RemapD(bootTime, 0.925, 1, 0, 1), 0, 1);

			double yShieldPer1 = ClampedD(RemapD(sp, 0, 0.333, 0, 1), 0, 1);
			double yShieldPer2 = ClampedD(RemapD(sp, 0.333, 0.667, 0, 1), 0, 1);
			double yShieldPer3 = ClampedD(RemapD(sp, 0.667, 1, 0, 1), 0, 1);

			Vector3D dir = Vector3D.Normalize((radarMatrix.Up * 2 + radarMatrix.Backward) / 3);

			if (sp > 0.01) 
			{
				bool dotty = false;
				if (yShieldPer1 > 0.01) 
				{
					dotty = yShieldPer1 < 0.25;
					DrawCircle(pos, 0.08 - (1 - boot3) * 0.08, dir, LINECOLOR_Comp * (float)Math.Ceiling (boot3), dotty, 1, 0.001f * (float)yShieldPer1);
				}
				if (yShieldPer2 > 0.01) 
				{
					dotty = yShieldPer2 < 0.25;
					DrawCircle(pos, 0.085 - (1-boot2)*0.085, dir, LINECOLOR_Comp * (float)Math.Ceiling(boot2), dotty, 1, 0.001f * (float)yShieldPer2);
				}
				if (yShieldPer3 > 0.01) 
				{
					dotty = yShieldPer3 < 0.25;
					DrawCircle(pos, 0.09 - (1-boot1)*0.09, dir, LINECOLOR_Comp * (float)Math.Ceiling(boot1), dotty, 1, 0.001f * (float)yShieldPer3);
				}
			}
			else
			{
				DrawCircle(pos, 0.08, dir, new Vector4(1,0,0,1), true, 0.5f, 0.001f);
			}

			double fontSize = 0.005;
			string ShieldValueS = Convert.ToString(Math.Round(sp_cur * bootTime * 1000));
			Vector4 ShieldValueC = LINECOLOR_Comp;

			if (sp < 0.01) 
			{
				ShieldValueS = "SHIELDS DOWN";
				ShieldValueC = new Vector4(1, 0, 0, 1);

				string time2readyS = FormatSecondsToReadableTime(time2ready);

				Vector3D timePos = pos + (radarMatrix.Backward * 0.065) + (radarMatrix.Down * fontSize * 8) + (radarMatrix.Left * time2readyS.Length * fontSize);
				DrawText(time2readyS, fontSize, timePos, radarMatrix.Forward, ShieldValueC, 1);
			}
			Vector3D textPos = pos + (radarMatrix.Backward * 0.065) + (radarMatrix.Down * fontSize * 4) + (radarMatrix.Left * ShieldValueS.Length * fontSize);
			DrawText (ShieldValueS, fontSize, textPos, radarMatrix.Forward, ShieldValueC, 1);

		}

		public string FormatSecondsToReadableTime(double seconds)
		{
			if (double.IsNaN(seconds) || double.IsInfinity(seconds))
			{
				return "Invalid time"; // Handle invalid input appropriately
			}

			if (seconds > 86400) 
			{
				seconds /= 86400;
				seconds = Math.Round(seconds);
				string days = $"{seconds} Days";
				return days;
			}
			seconds = ClampedD(seconds, 0.001, double.MaxValue);

			if (double.IsNaN(seconds) || double.IsInfinity(seconds))
			{
				return "Invalid time"; // Handle invalid clamped value
			}

			TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);

			// If the time span includes hours, include hours in the format
			if (timeSpan.TotalHours >= 1)
			{
				return string.Format("{0:D2}:{1:D2}:{2:D2}", 
					(int)timeSpan.TotalHours, 
					timeSpan.Minutes, 
					timeSpan.Seconds);
			}
			else
			{
				return string.Format("{0:D2}:{1:D2}", 
					timeSpan.Minutes, 
					timeSpan.Seconds);
			}
		}

        private void HG_UpdateBlockStatus (ref int blockCount, List<BlockTracker> blocksList, ref double hitPoints)
		{
			int tempCount = blockCount;
			if (tempCount >= 0 && tempCount < blocksList.Count) 
			{

				if (blocksList[tempCount].Block != null) 
				{
					VRage.Game.ModAPI.IMySlimBlock block = blocksList[tempCount].Block;
					blocksList[tempCount].HealthCurrent = block.Integrity;
					blocksList[tempCount].HealthMax = block.MaxIntegrity;

					hitPoints -= blocksList[tempCount].HealthLast - blocksList[tempCount].HealthCurrent;

					blocksList[tempCount].HealthLast = blocksList[tempCount].HealthCurrent;

					//DAMAGE EVENT if Last is more than Current
				}

                tempCount += 1;
				if (tempCount >= blocksList.Count) 
				{
                    tempCount = 0;
				}

			} else {
                tempCount = 0;
			}
			blockCount = tempCount;
		}

		private Stopwatch Drives_deltaTimer = new Stopwatch();
		private double Drives_deltaTime = 0;

		private void HG_UpdateDriveStatus(List<BlockTracker> BT, ref double hitPointsCurrent, ref double hitPointsMax, out double time2ready)
		{
			hitPointsCurrent = 0;
			hitPointsMax = 0;

			double minTime = double.MaxValue;

			foreach (BlockTracker block in BT)
			{
				if (block.JumpDrive != null)
				{
					bool isReady = block.JumpDrive.Status == MyJumpDriveStatus.Ready;
					if (block.JumpDrive.IsWorking && isReady)
					{
						hitPointsCurrent += block.JumpDrive.CurrentStoredPower;
					}
					hitPointsMax += block.JumpDrive.MaxStoredPower;

					if (hitPointsMax != hitPointsCurrent) 
					{
						double currentStoredPower = block.JumpDrive.CurrentStoredPower;
						double lastStoredPower = block.JumpDriveLastStoredPower;
						double difPower = (currentStoredPower - lastStoredPower);

						if( difPower > 0)
						{
							double powerPerSecond = (currentStoredPower-lastStoredPower) / Drives_deltaTime;

							if (powerPerSecond > 0) 
							{
								double timeRemaining = ((block.JumpDrive.MaxStoredPower - currentStoredPower) / powerPerSecond)*100;

								if (timeRemaining < minTime) 
								{
									minTime = timeRemaining;
								}

								//Echo ($"CPS: {powerPerSecond}, TR: {timeRemaining}, MIN: {minTime}");
							}

							// Update the last stored power for the next iteration
							block.JumpDriveLastStoredPower = currentStoredPower;

						}
					}
				}
			}
			time2ready = minTime != double.MaxValue ? minTime : 0;
			//Echo ($"Time till ready: {time2ready}");
		}



        private void HG_InitializeGrid(VRage.ModAPI.IMyEntity entity, ref VRage.Game.ModAPI.IMyCubeGrid hologramGrid,
            ref List<BlockTracker> blockList, ref List<BlockTracker> driveList, ref MatrixD scalingMatrixHG, ref double gridHealthCurrent, ref double gridHealthMax)
        {
            if (entity == null || !(entity is VRage.Game.ModAPI.IMyCubeGrid))
            {
                return;
            }
            hologramGrid = entity as VRage.Game.ModAPI.IMyCubeGrid;

            if (hologramGrid != null)
            {
                blockList = new List<BlockTracker>();
                driveList = new List<BlockTracker>();
                blockList = HG_GetBlockInfo(hologramGrid, ref gridHealthCurrent, ref gridHealthMax);
                // Define a scaling matrix for positioning on the dashboard
                double thicc = HG_scaleFactor / (hologramGrid.WorldVolume.Radius / hologramGrid.GridSize); // HG_scaleFactor is global.
                scalingMatrixHG = MatrixD.CreateScale(HG_Scale * thicc); //HG_Scale is global

                for (int b = 0; b < blockList.Count; b++)
                {
                    if (blockList[b].IsJumpDrive)
                    {
                        driveList.Add(blockList[b]);
                    }
                }
            }
        }

        private void HG_InitializeLocalGrid()
		{
			// Get the player's controlled grid
			var controlledEntity = GetControlledGrid();
			hologramLocalGrid = controlledEntity as VRage.Game.ModAPI.IMyCubeGrid;

			if (hologramLocalGrid != null)
			{
				HG_InitializeGrid(controlledEntity, ref hologramLocalGrid, 
					ref localGridBlocks, ref localGridDrives, ref HG_scalingMatrix, ref localGridHealthCurrent, ref localGridHealthMax);
			}
		}


		private void HG_InitializeTargetGrid(VRage.ModAPI.IMyEntity target)
		{
			if (target == null) 
			{
				return;
			}
			hologramTargetGrid = target as VRage.Game.ModAPI.IMyCubeGrid;
			if (hologramTargetGrid != null)
			{
				HG_InitializeGrid(target, ref hologramTargetGrid, 
					ref targetGridBlocks, ref targetGridDrives, ref HG_scalingMatrixTarget, ref targetGridHealthCurrent, ref targetGridHealthMax);
			}
		}

		private List<BlockTracker> HG_GetBlockInfo(VRage.Game.ModAPI.IMyCubeGrid grid, ref double gridHealthCurrent, ref double gridHealthMax)
		{
			Vector3D gridCenter = grid.WorldVolume.Center; // Center of the grid's bounding box
			Vector3D gridPos = grid.GetPosition(); //Position of the grid in the world

			MatrixD worldRotationMatrix = GetRotationMatrix(grid.WorldMatrix); // Gets a rotation matrix from the grid's world matrix.
            MatrixD inverseMatrix = MatrixD.Invert(worldRotationMatrix);

            List<BlockTracker> blockInfo = new List<BlockTracker>();

            List<VRage.Game.ModAPI.IMySlimBlock> blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
			grid.GetBlocks(blocks);

			foreach (VRage.Game.ModAPI.IMySlimBlock block in blocks)
			{
				Vector3D localPosition;
                Vector3D scaledPosition;
				Vector3D worldRelativePosition;
				block.ComputeWorldCenter(out localPosition); // Gets world position for the center of the block
				scaledPosition = localPosition; // Copy the world position to sc
                scaledPosition -= gridCenter; // set scaledPosition to be relative to the center of the grid
				worldRelativePosition = localPosition;
				//worldRelativePosition /= grid.GridSize;
                scaledPosition = Vector3D.Transform(scaledPosition, inverseMatrix); // Transform by the inverseMatrix
				scaledPosition /= grid.GridSize; // Divide vector by grid size to get scaled coordinates, I guess this is what makes a large ship still fit on screen
				// Yes my updated understanding of the above is we are normalizing the block position. So we normalize it a position relative to the grid position
				// as if the grid were at (0, 0, 0) and then scale it by dividing by gridSize. Basically a grid could be at (1234, -1234, 1234), we don't really care.
				// What we care about is there is a block on that grid taht is at (1235, -1233, 1235) in worldspace and relative to the grid's center that block is at (1, 1, 1)
				// Then we scale it down by the grid size, so if the grid size is 2, then the block is at (0.5, 0.5, 0.5) in normalized coordinates.
				// We dont care where the block is in the world, we care where it is on the grid. That makes sense to me now. 

				VRage.Game.ModAPI.IMySlimBlock Blocker = block as VRage.Game.ModAPI.IMySlimBlock;
				Vector3D Position = scaledPosition; // Store the scaled position of the block
				double Health_Max = block.MaxIntegrity;
				double Health_Cur = block.Integrity;

				// Store block related info in the BlockTracker
				BlockTracker BT = new BlockTracker();
				BT.Position = Position; // Okay my understanding of this is limited at best.
										// I believe what BT.Position actually contains is "where this block would be if the grid was at origin (0, 0, 0), facing forward, and normalized to unit size." 
				BT.WorldRelativePosition = worldRelativePosition; 
				BT.Block = Blocker;
				BT.HealthMax = Health_Max;
				BT.HealthCurrent = Health_Cur;
				BT.HealthLast = Health_Cur;
                gridHealthCurrent += Health_Cur;
                gridHealthMax += Health_Max;

                BT.IsJumpDrive = false;

				if (IsJumpDrive(block.FatBlock)) 
				{
                    Sandbox.ModAPI.Ingame.IMyJumpDrive jumpDrive = block.FatBlock as Sandbox.ModAPI.Ingame.IMyJumpDrive;
					if (jumpDrive != null) 
					{
						BT.IsJumpDrive = true;
						BT.JumpDrive = jumpDrive;
						BT.JumpDriveLastStoredPower = 0;
					}
				}

                

                blockInfo.Add(BT);
			}

			return blockInfo;
		}

		

		private bool IsJumpDrive(VRage.Game.ModAPI.IMyCubeBlock block)
		{
			if (block == null)
			{
				return false;
			}
			return block.BlockDefinition.TypeId == typeof(Sandbox.Common.ObjectBuilders.MyObjectBuilder_JumpDrive);
		}

		private List<Vector3D> HG_GetBlockPositions(VRage.Game.ModAPI.IMyCubeGrid grid)
		{
			var blockPositions = new List<Vector3D>();

			if (grid == null) 
			{
				return blockPositions;
			}

			var center = grid.WorldVolume.Center;
			var pos = grid.GetPosition();

			MatrixD inverseMatrix = GetRotationMatrix(grid.WorldMatrix);
			inverseMatrix = MatrixD.Invert(inverseMatrix);


			var blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
			grid.GetBlocks(blocks);

			foreach (var block in blocks)
			{
				Vector3D sc;
				//block.ComputeScaledCenter(out sc);
				block.ComputeWorldCenter(out sc);
				sc -= center;
				sc = Vector3D.Transform(sc, inverseMatrix);
				sc /= grid.GridSize;
				blockPositions.Add(sc);
			}

			return blockPositions;
		}

		private MatrixD GetRotationMatrix(MatrixD matrix)
		{
			// Extract the rotation components
			Vector3D right = matrix.Right;
			Vector3D up = matrix.Up;
			Vector3D forward = matrix.Forward;

			// Create a new matrix with the rotation components
			MatrixD rotationMatrix = MatrixD.Identity;
			rotationMatrix.Right = right;
			rotationMatrix.Up = up;
			rotationMatrix.Forward = forward;

			// Set translation to zero
			rotationMatrix.Translation = Vector3D.Zero;

			return rotationMatrix;
		}

        private void HG_UpdateHologramLocal(VRage.Game.ModAPI.IMyCubeGrid localGrid, List<BlockTracker> blockInfo)
        {
            // TODO this method needs rework to be like target, BUT only in the sense of the transformations/code improvements. It should be using block Tracker. And block tracker should be modified to 
            // store UNSCALED blocks only so it can work with transformations/rotations. And also update in the UpdateBeforeSimulation not in the Draw
            if (localGrid != null)
            {
                bool isEntityTarget = false;
                MatrixD angularRotationWiggle = MatrixD.Identity;
                MatrixD rotationOnlyGridMatrix = localGrid.WorldMatrix;
                rotationOnlyGridMatrix.Translation = Vector3D.Zero; // Set the translation to zero to get only the rotation component.

                // This uses the cockpit controlled by the player and gets the angular velocity of the cubeblock.
                if (HologramView_AngularWiggle)
                {
                    angularRotationWiggle = CreateNormalizedLocalGridRotationMatrix();  // Used to apply a "wiggle" effect to the hologram based on the angular velocity of the grid.
                }

                foreach (var BT in blockInfo)
                {
                    // FenixPK Woohoo! as of 2025-07-17 I have figured this out, it now shows the local grid from the back, and wiggles it left/right, up/down, or rolls it based on the angular velocity of the grid. It recenters on the rear view when you come to a rest.
                    // Fantastic. Next step is to allow the user to change the angle of the hologram on demand with keybinds, AND code something to detect damage and weight it so we can flip the hologram to show the side taking damage mwahahaha. 

                    //Tests
                    //So if I were to create a fake MatrixD I could do whatever I want to the view...
                    //MatrixD rotate180X = MatrixD.CreateRotationX(MathHelper.ToRadians(180)); // This rotates the position 180 degrees around the X axis. Was math.pi in radians?
                    //Vector3D positionTest = Vector3D.Rotate(BT.Position, rotate180X);
                    //positionTest = Vector3D.Transform(positionTest, targetGrid.WorldMatrix);
                    // YES! The above did exactly what I expected, it rotated the position of the hologram 180 degrees around the X axis. So it was upside down. 
                    // HOWEVER it still rotates based on how the player rotates likely because HG_DrawBillboard uses the camera's position to draw the hologram...

                    // Note to self, we can create MatrixD's that have various rotational transformations and then use them here on a period so we get it to rotate through top, bottom, left, right, front, back etc. 
                    // Or make it a toggle with a keybind? So many ideas. 

                    Vector3D blockPositionToTransform = BT.Position;
                    Vector3D blockPositionTransformed = Vector3D.Zero;
                    Vector3D finalBlockPositionToDraw = Vector3D.Zero;

                    if (HologramView_AngularWiggle)
                    {
                        // This is to give a "wiggle" effect to a fixed hologram if you are rotating to represent how quickly you are rotating. For eg. if viewed from the back and you pitch nose up
                        // the hologram will have the nose pitch up based on how fast you are rotating. The faster you are rotating in the upward direction, the more the nose of the hologram will pitch up.
                        Vector3D blockPositionWiggled = Vector3D.Rotate(blockPositionToTransform, angularRotationWiggle); // This applies the angular rotation based on the angular velocity of the grid.
                        blockPositionTransformed = blockPositionWiggled; // Wiggle it first for angular velocity if enabled.
                    }

                    Vector3D blockPositionRotated = Vector3D.Transform(blockPositionTransformed, rotationOnlyGridMatrix);
                    finalBlockPositionToDraw = blockPositionRotated;
					BT.HologramDrawPosition = finalBlockPositionToDraw;
                }
            }
        }

        private void HG_DrawHologramLocal(VRage.Game.ModAPI.IMyCubeGrid localGrid, List<BlockTracker> blockInfo)
		{
			// TODO this method needs rework to be like target, BUT only in the sense of the transformations/code improvements. It should be using block Tracker. And block tracker should be modified to 
			// store UNSCALED blocks only so it can work with transformations/rotations. And also update in the UpdateBeforeSimulation not in the Draw
			if (localGrid != null)
			{
				bool isEntityTarget = false;

                foreach (var BT in blockInfo)
                {
					// FenixPK Woohoo! as of 2025-07-17 I have figured this out, it now shows the local grid from the back, and wiggles it left/right, up/down, or rolls it based on the angular velocity of the grid. It recenters on the rear view when you come to a rest.
					// Fantastic. Next step is to allow the user to change the angle of the hologram on demand with keybinds, AND code something to detect damage and weight it so we can flip the hologram to show the side taking damage mwahahaha. 

					//Tests
					//So if I were to create a fake MatrixD I could do whatever I want to the view...
					//MatrixD rotate180X = MatrixD.CreateRotationX(MathHelper.ToRadians(180)); // This rotates the position 180 degrees around the X axis. Was math.pi in radians?
					//Vector3D positionTest = Vector3D.Rotate(BT.Position, rotate180X);
					//positionTest = Vector3D.Transform(positionTest, targetGrid.WorldMatrix);
					// YES! The above did exactly what I expected, it rotated the position of the hologram 180 degrees around the X axis. So it was upside down. 
					// HOWEVER it still rotates based on how the player rotates likely because HG_DrawBillboard uses the camera's position to draw the hologram...

					// Note to self, we can create MatrixD's that have various rotational transformations and then use them here on a period so we get it to rotate through top, bottom, left, right, front, back etc. 
					// Or make it a toggle with a keybind? So many ideas. 

                    double HealthPercent = ClampedD(BT.HealthCurrent / BT.HealthMax, 0, 1);
                    HG_DrawBillboardLocal(BT.HologramDrawPosition, localGrid, isEntityTarget, HealthPercent);
                }
            }
        }

        private void HG_DrawBillboardLocal(Vector3D position, VRage.Game.ModAPI.IMyCubeGrid grid, bool flippit = false, double HP = 1)
        {
            if (HP < 0.01)
            {
                return;
            }
            bool randoTime = false;
            if (GetRandomFloat() > 0.95f || glitchAmount > 0.5)
            {
                randoTime = true;
            }

            double bootUpAlpha = 1;

            if (flippit)
            {
                bootUpAlpha = HG_activationTimeTarget;
            }
            else
            {
                bootUpAlpha = HG_activationTime;
            }

            bootUpAlpha = ClampedD(bootUpAlpha, 0, 1);
            bootUpAlpha = Math.Pow(bootUpAlpha, 0.25);

            if (GetRandomDouble() > bootUpAlpha)
            {
                position *= bootUpAlpha;
            }
            if (GetRandomDouble() > bootUpAlpha)
            {
                randoTime = true;
            }

            var camera = MyAPIGateway.Session.Camera;
            Vector3D AxisLeft = camera.WorldMatrix.Left;
            Vector3D AxisUp = camera.WorldMatrix.Up;
            Vector3D AxisForward = camera.WorldMatrix.Forward;

            Vector3D billDir = Vector3D.Normalize(position);
            double dotProd = 1 - (Vector3D.Dot(position, AxisForward) + 1) / 2;
            dotProd = RemapD(dotProd, -0.5, 1, 0.25, 1);
            dotProd = ClampedD(dotProd, 0.25, 1);

            var color = theSettings.lineColor * 0.5f;
            if (flippit)
            {
                color = LINECOLOR_Comp * 0.5f;
            }
            color.W = 1;
            if (randoTime)
            {
                color *= Clamped(GetRandomFloat(), 0.25f, 1);
            }

            Vector4 cRed = new Vector4(1, 0, 0, 1);
            Vector4 cYel = new Vector4(1, 1, 0, 1);

            if (HP > 0.5)
            {
                HP -= 0.5;
                HP *= 2;
                color.X = LerpF(cYel.X, color.X, (float)HP);
                color.Y = LerpF(cYel.Y, color.Y, (float)HP);
                color.Z = LerpF(cYel.Z, color.Z, (float)HP);
                color.W = LerpF(cYel.W, color.W, (float)HP);
            }
            else
            {
                HP *= 2;
                color.X = LerpF(cRed.X, cYel.X, (float)HP);
                color.Y = LerpF(cRed.Y, cYel.Y, (float)HP);
                color.Z = LerpF(cRed.Z, cYel.Z, (float)HP);
                color.W = LerpF(cRed.W, cYel.W, (float)HP);
            }

            double thicc = HG_scaleFactor / (grid.WorldVolume.Radius / grid.GridSize);
            var size = (float)HG_Scale * 0.65f * (float)thicc;//*grid.GridSize;
            var material = MaterialSquare;

            double flipperAxis = 1;
            if (flippit)
            {
                flipperAxis = -1;
            }

            double gridThicc = grid.WorldVolume.Radius;
            Vector3D HG_Offset_tran = radarMatrix.Left * -radarRadius * flipperAxis + radarMatrix.Left * _hologramRightOffset_HardCode.X * flipperAxis + radarMatrix.Up * _hologramRightOffset_HardCode.Y + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;

            if (randoTime)
            {
                Vector3D randOffset = new Vector3D((GetRandomDouble() - 0.5) * 2, (GetRandomDouble() - 0.5) * 2, (GetRandomDouble() - 0.5) * 2);
                randOffset *= 0.333;
                position += position * randOffset;
            }

            if (flippit)
            {
                position = Vector3D.Transform(position, HG_scalingMatrixTarget);
            }
            else
            {
                position = Vector3D.Transform(position, HG_scalingMatrix);
            }

            position += worldRadarPos + HG_Offset_tran;

            double dis2Cam = Vector3.Distance(camera.Position, position);

            MyTransparentGeometry.AddBillboardOriented(
                material,
                color * (float)dotProd * (float)bootUpAlpha,
                position,
                AxisLeft, // Billboard orientation
                AxisUp, // Billboard orientation
                size,
                MyBillboard.BlendTypeEnum.AdditiveTop);

            if (GetRandomFloat() > 0.9f)
            {
                Vector3D holoCenter = radarMatrix.Left * -radarRadius * flipperAxis + radarMatrix.Left * _hologramRightOffset_HardCode.X * flipperAxis + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
                holoCenter += worldRadarPos;
                Vector3D holoDir = Vector3D.Normalize(position - holoCenter);
                double holoLength = Vector3D.Distance(holoCenter, position);

                DrawLineBillboard(MaterialSquare, color * 0.15f * (float)dotProd * (float)bootUpAlpha, holoCenter, holoDir, (float)holoLength, 0.0025f, BlendTypeEnum.AdditiveTop);
            }
        }


        private void HG_DrawHologramTarget(VRage.Game.ModAPI.IMyCubeGrid targetGrid, List<BlockTracker> blockInfo)
        {
            if (targetGrid != null)
            {
                foreach (BlockTracker block in blockInfo)
                {
                    // FenixPK Woohoo! as of 2025-07-17 I have figured this out, it now shows the local grid from the back, and wiggles it left/right, up/down, or rolls it based on the angular velocity of the grid. It recenters on the rear view when you come to a rest.
                    // Fantastic. Next step is to allow the user to change the angle of the hologram on demand with keybinds, AND code something to detect damage and weight it so we can flip the hologram to show the side taking damage mwahahaha. 

                    //Tests
                    //So if I were to create a fake MatrixD I could do whatever I want to the view...
                    //MatrixD rotate180X = MatrixD.CreateRotationX(MathHelper.ToRadians(180)); // This rotates the position 180 degrees around the X axis. Was math.pi in radians?
                    //Vector3D positionTest = Vector3D.Rotate(BT.Position, rotate180X);
                    //positionTest = Vector3D.Transform(positionTest, targetGrid.WorldMatrix);
                    // YES! The above did exactly what I expected, it rotated the position of the hologram 180 degrees around the X axis. So it was upside down. 
                    // HOWEVER it still rotates based on how the player rotates likely because HG_DrawBillboard uses the camera's position to draw the hologram...

                    // Note to self, we can create MatrixD's that have various rotational transformations and then use them here on a period so we get it to rotate through top, bottom, left, right, front, back etc. 
                    // Or make it a toggle with a keybind? So many ideas. 

                    double HealthPercent = ClampedD(block.HealthCurrent / block.HealthMax, 0, 1);
                    HG_DrawBillboardTarget(block.HologramDrawPosition, targetGrid, HealthPercent);
                }

    //            // Alright, let's roll our own and see what the F is going on here, talk about "out of your depth" holy moly here I am:
    //            double hologramScale = 0.0075;
    //            double hologramScaleFactor = 10;

    //            VRage.Game.ModAPI.IMyCubeGrid gridA = gHandler.localGrid; // Observer grid, position matters rotation does not
    //            VRage.Game.ModAPI.IMyCubeGrid gridB = targetGrid; // Observed grid, position and rotation matters.

    //            List<VRage.Game.ModAPI.IMySlimBlock> blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
    //            targetGrid.GetBlocks(blocks);
    //            double flipAxisForTarget = -1; // An inversion factor so it draws on the left holographic display

    //            // Color to use and re-use
    //            Vector4 color = LINECOLOR_Comp * 0.5f;
    //            color.W = 1;

              
    //            double thickness = hologramScaleFactor / (gridB.WorldVolume.Radius / gridB.GridSize);
    //            float hologramSize = (float)hologramScale * 0.65f * (float)thickness;
    //            foreach (BlockTracker block in blockInfo) 
				//{
    //                // Get health of block
    //                double HealthPercent = ClampedD(block.HealthCurrent / block.HealthMax, 0, 1);

    //                // Do we even draw it on screen?
    //                if (HealthPercent < 0.01)
    //                {
    //                    continue; // Do not draw destroyed blocks. 
    //                }

    //                // Draw on screen.
    //                MyTransparentGeometry.AddBillboardOriented(
    //                    MaterialSquare,
    //                    color,
    //                    block.HologramDrawPosition,
    //                    MyAPIGateway.Session.Camera.WorldMatrix.Left, // Orient billboard drawn toward camera so we can see the square. 
    //                    MyAPIGateway.Session.Camera.WorldMatrix.Up,
    //                    hologramSize,
    //                    MyBillboard.BlendTypeEnum.AdditiveTop);

    //                if (GetRandomFloat() > 0.9f)
    //                {
    //                    // Aha so this is the shimmery holographic effect under it.
    //                    Vector3D holoCenter = radarMatrix.Left * -radarRadius * flipAxisForTarget + radarMatrix.Left * _hologramRightOffset_HardCode.X * flipAxisForTarget + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
    //                    holoCenter += worldRadarPos;
    //                    Vector3D holoDir = Vector3D.Normalize(block.HologramDrawPosition - holoCenter);
    //                    double holoLength = Vector3D.Distance(holoCenter, block.HologramDrawPosition);
    //                    DrawLineBillboard(MaterialSquare, color * 0.15f, holoCenter, holoDir, (float)holoLength, 0.0025f, BlendTypeEnum.AdditiveTop);
    //                }
    //            }



			}
        }


        private void HG_DrawBillboardTarget(Vector3D position, VRage.Game.ModAPI.IMyCubeGrid grid, double HP = 1)
        {
            if (HP < 0.01)
            {
                return; // Do not draw destroyed blocks. 
            }
            bool randoTime = false;
            if (GetRandomFloat() > 0.95f || glitchAmount > 0.5)
            {
                randoTime = true; // If we are at high power draw and the glitchEffect should be on then we set randoTime to true so further effects can be altered by it.
            }

            double hologramScale = 0.0075;
            double hologramScaleFactor = 10;
            double flipAxisForTarget = -1;

            double bootUpAlpha = 1;
            bootUpAlpha = HG_activationTimeTarget; // This is the activation time for the target grid hologram. 

            bootUpAlpha = ClampedD(bootUpAlpha, 0, 1); // Clamp bootUpAlpha to be between 0 and 1. 
            bootUpAlpha = Math.Pow(bootUpAlpha, 0.25); // bootUpAlpha^(1/4)?

            if (GetRandomDouble() > bootUpAlpha)
            {
                position *= bootUpAlpha; // I assume this is what gives the hologram the booting up effect, it scales the position of blocks drawn by the bootUpAlpha.
                                         // But only randomly when the double is >, so as bootUpAlpha approaches 1, the hologram will be drawn at full position.
            }
            if (GetRandomDouble() > bootUpAlpha)
            {
                randoTime = true; // If we haven't yet booted up then we also apply the randomEffect to the hologram.
                                  // So the same glitch effect is also applied at boot up BUT it's also scaled during boot up, not during just a glitch.
            }

            double dotProd = 1 - (Vector3D.Dot(position, MyAPIGateway.Session.Camera.WorldMatrix.Forward) + 1) / 2; 
            dotProd = RemapD(dotProd, -0.5, 1, 0.25, 1);
            dotProd = ClampedD(dotProd, 0.25, 1);

            var color = LINECOLOR_Comp * 0.5f; // This is the color of the hologram for the target. I assume comp means complimentary?
            color.W = 1;
            if (randoTime)
            {
                color *= Clamped(GetRandomFloat(), 0.25f, 1); // If the effect is being applied then we randomize the color brightness between 0.25 and 1.
            }

            Vector4 cRed = new Vector4(1, 0, 0, 1); // Colors for damanged blocks.
            Vector4 cYel = new Vector4(1, 1, 0, 1); // Colors for damaged blocks. Red is more damaged than yellow.

            // Interesting, we split this into two ranges. White color that LerpFs to yellow. Then yellow color that LerpFs to red. Essentially two ranges of color, White->Yellow and Yellow->Red. Neat.
            if (HP > 0.5)
            {
                HP -= 0.5;
                HP *= 2;
                color.X = LerpF(cYel.X, color.X, (float)HP);
                color.Y = LerpF(cYel.Y, color.Y, (float)HP);
                color.Z = LerpF(cYel.Z, color.Z, (float)HP);
                color.W = LerpF(cYel.W, color.W, (float)HP);
            }
            else
            {
                HP *= 2;
                color.X = LerpF(cRed.X, cYel.X, (float)HP);
                color.Y = LerpF(cRed.Y, cYel.Y, (float)HP);
                color.Z = LerpF(cRed.Z, cYel.Z, (float)HP);
                color.W = LerpF(cRed.W, cYel.W, (float)HP);
            }

          
            if (randoTime)
            {
                // Offset the position by a random amount to give a "glitch" effect, also used for booting up. 
                Vector3D randOffset = new Vector3D((GetRandomDouble() - 0.5) * 2, (GetRandomDouble() - 0.5) * 2, (GetRandomDouble() - 0.5) * 2);
                randOffset *= 0.333;
                position += position * randOffset;
            }

            double thickness = hologramScaleFactor / (grid.WorldVolume.Radius / grid.GridSize);
            float hologramSize = (float)hologramScale * 0.65f * (float)thickness;
            MyTransparentGeometry.AddBillboardOriented(
                MaterialSquare,
                color * (float)bootUpAlpha,
                position,
                MyAPIGateway.Session.Camera.WorldMatrix.Left, // Orient billboard drawn toward camera so we can see the square. 
                MyAPIGateway.Session.Camera.WorldMatrix.Up,
                hologramSize,
                MyBillboard.BlendTypeEnum.AdditiveTop);

            if (GetRandomFloat() > 0.9f)
            {
                // Aha so this is the shimmery holographic effect under it.
                Vector3D holoCenter = radarMatrix.Left * -radarRadius * flipAxisForTarget + radarMatrix.Left * _hologramRightOffset_HardCode.X * flipAxisForTarget + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
                holoCenter += worldRadarPos;
                Vector3D holoDir = Vector3D.Normalize(position - holoCenter);
                double holoLength = Vector3D.Distance(holoCenter, position);

                DrawLineBillboard(MaterialSquare, color * 0.15f * (float)dotProd * (float)bootUpAlpha, holoCenter, holoDir, (float)holoLength, 0.0025f, BlendTypeEnum.AdditiveTop);
            }
        }


        private void HG_UpdateHologramTarget(VRage.Game.ModAPI.IMyCubeGrid targetGrid, List<BlockTracker> blockInfo)
        {
            if (targetGrid != null)
            {
                // New, updated by FenixPK 2025-07-25 after a boatload of research and fighting with this. Will likely leave the original author's code with my various attempts in as an _OLD method that is never used, just for
				// reference in the future. 

                // Alright, let's roll our own and see what the F is going on here, talk about "out of your depth" holy moly here I am:
                double hologramScale = 0.0075;
                double hologramScaleFactor = 10;

                VRage.Game.ModAPI.IMyCubeGrid gridA = gHandler.localGrid; // Observer grid, position matters rotation does not
                VRage.Game.ModAPI.IMyCubeGrid gridB = targetGrid; // Observed grid, position and rotation matters.

                List<VRage.Game.ModAPI.IMySlimBlock> blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
                targetGrid.GetBlocks(blocks);
                double flipAxisForTarget = -1; // An inversion factor so it draws on the left holographic display

                // Color to use and re-use
                Vector4 color = LINECOLOR_Comp * 0.5f;
                color.W = 1;

                MatrixD angularRotationWiggle = CreateNormalizedTargetGridRotationMatrix(gridB); // Could apply wiggle for angular velocity like localGrid view if in fixed frame mode. So you get a fixed view but see which way it is rotating.
				foreach (BlockTracker block in blockInfo) 
				{
                    // Get health of block
                    double HealthPercent = ClampedD(block.HealthCurrent / block.HealthMax, 0, 1);

                    // Do we even draw it on screen?
                    if (HealthPercent < 0.01)
                    {
                        continue; // Do not draw destroyed blocks. 
                    }

                    // Let's store some variables here for easier pathing below.
                    Vector3D HG_Offset_transformation = Vector3D.Zero; // Offset for location to draw "on screen" (technically relative to the cockpit block not camera/screen but eh)
                    Vector3D hologramConsoleLocation = Vector3D.Zero;
                    Vector3D finalBlockPositionToDraw = Vector3D.Zero; // Final block position to draw with ALL transformations done.
                    Vector3D blockPositionInWorld = block.WorldRelativePosition; // Block tracker stores world relative position, and grid relative too. 
                    Vector3D blockPositionRelativeToGridCenter = blockPositionInWorld - gridB.WorldVolume.Center; // Get the blocks positions in meters relative to the grid's center. As if center of grid world volume is center
                    Vector3D blockPositionToTransform = Vector3D.Zero;
                    Vector3D blockPositionTransformed = Vector3D.Zero;
                    Vector3D blockPositionConvertedFromMetersToBlocks = Vector3D.Zero;
                    Vector3D blockPositionInBlocksScaledForHologram = Vector3D.Zero;
                    Vector3D blockPositionInHologram = Vector3D.Zero;
                    double thickness = hologramScaleFactor / (gridB.WorldVolume.Radius / gridB.GridSize);
                    MatrixD scalingMatrix = MatrixD.CreateScale(hologramScale * thickness);
                    float hologramSize = (float)hologramScale * 0.65f * (float)thickness;

                    // Apply the offsets for where the hologram "emitter" is versus the radar center.
                    HG_Offset_transformation = radarMatrix.Left * -radarRadius * flipAxisForTarget + radarMatrix.Left * _hologramRightOffset_HardCode.X * flipAxisForTarget + radarMatrix.Up * _hologramRightOffset_HardCode.Y + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
                    hologramConsoleLocation = worldRadarPos + HG_Offset_transformation; // Set the position to draw the hologram based on where the radar center is, + the offsets. This is where in the world the hologram appears.
                                                                                        // When we draw the blocks relative to the targetGrid's center we will essentially be scaling them down and then drawing them as if the center was this emitter point. 

                    // Original author had the block position divided by gridSize, So instead of working with positions in meters we work with position in blocks and the actual size in game might be different if small grid vs big grid ship. 
                    // This allows the hologram to display a 20x20x20 small block ship the same as a 20x20x20 large block ship.
                    // HOWEVER.... This means when we do anything fun like Inverse matrix of gridA to get gridB into gridA's frame of reference we are using blocks as UoM in gridB's matrix and meters as UoM in gridA's matrix.
                    // This is like mixing Tons and Metric Board Feet in a subtotal and then wondering why your average is wrong. It doesn't work xD


                    // FLAT VIEW MODE: 
                    // This mode cancels out all rotational elements from localGrid or taretGrid to display a static view. By default this is from behind because everything is relative to gridA pointing forward and all rotation is removed.
                    // With toggles/keybinds this would allow us to take this static view and rotate it as desired. Ie. press left arrow to rotate 90 to the left
                    // Currently I am seeking a radar imager approach, which is where the perspective view above comes in, and you can only see the side facing you.
                    // But this view would be cool for magic space technology that can scan an entire grid and present a hologram of it viewed from any angle.
                    // Maybe we can even do floor plans someday by color coding empty space vs armor/structure blocks vs functional blocks xD

                    MatrixD rotationOnlyGridAMatrix = gridA.WorldMatrix;
                    rotationOnlyGridAMatrix.Translation = Vector3D.Zero;

                    MatrixD rotationOnlyGridBMatrix = gridB.WorldMatrix;
                    rotationOnlyGridBMatrix.Translation = Vector3D.Zero;

                    // Do we wiggle?
                    if (HologramView_AngularWiggleTarget)
                    {
                        blockPositionToTransform = Vector3D.Rotate(blockPositionRelativeToGridCenter, angularRotationWiggle);
                    }
                    else
                    {
                        blockPositionToTransform = blockPositionRelativeToGridCenter;
                    }

                    MatrixD rotationMatrixCancelGridAB = MatrixD.Invert(rotationOnlyGridBMatrix) * rotationOnlyGridAMatrix;
                    Vector3D blockPositionOrientationNeutral = Vector3D.Transform(blockPositionToTransform, rotationMatrixCancelGridAB);


                    blockPositionTransformed = blockPositionOrientationNeutral;

                    // Do scaling into block unit of measure from meters (so small grid and large grid are drawn the same size in the hologram) HERE, not earlier. Just before we draw this. 
                    // Or better yet move all of this to BeforeUpdateSimulation and just store them until we do the draw call but that re-write is waaaay down the road at this point. 
                    // Calculate an offset transformation so it draws on the left or right holographic display based on settings, flipAxisForTarget = -1 causes it to draw on the left
                    blockPositionConvertedFromMetersToBlocks = blockPositionTransformed / gridB.GridSize; // Convert from meters UoM to blocks UoM independent of large vs small grid ship.
                    blockPositionInBlocksScaledForHologram = Vector3D.Transform(blockPositionConvertedFromMetersToBlocks, scalingMatrix);

                    if (!HologramView_PerspectiveAttemptOne)
                    {
                        MatrixD rotationMatrixForView = MatrixD.Identity; // new matrixD.

                        // Can use MatrixD.slerp to smoothly transition between a current matrix and a target matrix for rotation...

                        // I actually think instead of just CreateRotationY/X/Z I need to do this relative to my cockpit blocks "up" direction, or it doesn't make any sense.
                        switch (HologramView_Current)
                        {
                            case HologramView_Side.Rear:
                                // Rear (default - no rotation needed)
                                rotationMatrixForView = MatrixD.Identity;
                                break;
                            case HologramView_Side.Left:
                                // Left side (90° yaw left)
                                rotationMatrixForView = MatrixD.CreateFromAxisAngle(gridA.WorldMatrix.Up, MathHelper.ToRadians(90)); // Use up so we pivot around that axis.
                                break;
                            case HologramView_Side.Front:
                                // Front (180° yaw)
                                rotationMatrixForView = MatrixD.CreateFromAxisAngle(gridA.WorldMatrix.Up, MathHelper.ToRadians(180)); // Use up so we pivot around that axis.
                                break;
                            case HologramView_Side.Right:
                                // Right side (90° yaw right / 270° left)
                                rotationMatrixForView = MatrixD.CreateFromAxisAngle(gridA.WorldMatrix.Up, MathHelper.ToRadians(-90)); // Use up so we pivot around that axis.
                                break;
                            case HologramView_Side.Top:
                                // Top view (90° pitch down)
                                rotationMatrixForView = MatrixD.CreateFromAxisAngle(gridA.WorldMatrix.Right, MathHelper.ToRadians(-90)); // Use right so we pivot around that axis.
                                break;
                            case HologramView_Side.Bottom:
                                // Bottom view (90° pitch up)
                                rotationMatrixForView = MatrixD.CreateFromAxisAngle(gridA.WorldMatrix.Right, MathHelper.ToRadians(90)); // Use right so we pivot around that axis.
                                break;
                            case HologramView_Side.Orbit:

                                // This is technically an "Orbit" cam... It will show a hologram of the target grid EXACTLY as it appears in world space relative to your orientation
                                // but not your translation/position in the world. So you can rotate your ship around and see all sides of gridB from anywhere in the world.
                                // if you are facing a direction and the target is also facing the same direction you see it's backside, even if it is behind you lol.

                                // What transformation gets us from gridA's coordinate system to gridB's?
                                MatrixD gridAToGridB = MatrixD.Invert(gridA.WorldMatrix) * gridB.WorldMatrix;

                                //// Use this as the rotation (this naturally includes both position and orientation differences)
                                rotationMatrixForView = MatrixD.CreateFromQuaternion(Quaternion.CreateFromRotationMatrix(gridAToGridB));
                                break;
                            case HologramView_Side.Perspective:
								// FenixPK 2025-07-28 there are still problems with this when gridA is rolled but this is sooo beyond my understanding at this point I give up haha. 
								// Round 3, the winner, this perpendicular on the left/right being handled made all the difference. It has the effect I was hoping for. 
								// Create proxy matrix: gridB's orientation at gridA's position
								MatrixD proxyMatrix = gridB.WorldMatrix;
								proxyMatrix.Translation = gridA.WorldMatrix.Translation;
								MatrixD relativeOrientation = gridB.WorldMatrix * MatrixD.Invert(proxyMatrix);
								// Extract just the rotation part (remove any translation)
								rotationMatrixForView = MatrixD.CreateFromQuaternion(Quaternion.CreateFromRotationMatrix(relativeOrientation));

								// Create a "should be looking at" orientation
								Vector3D directionToGridB = Vector3D.Normalize(gridB.WorldMatrix.Translation - proxyMatrix.Translation);

								// Project gridA's up vector onto the plane perpendicular to the direction vector
								Vector3D gridAUpInWorldSpace = gridA.WorldMatrix.Up;  //gridA.WorldMatrix.Up;
								Vector3D projectedGridAUp = gridAUpInWorldSpace - Vector3D.Dot(gridAUpInWorldSpace, directionToGridB) * directionToGridB;

								Vector3D gridAUp;
								// Check if the projection is valid (not too small)
								if (projectedGridAUp.LengthSquared() > 0.001)
								{
									gridAUp = Vector3D.Normalize(projectedGridAUp);
								}
								else
								{
									// Fallback when projection fails (gridA's up is parallel to direction)
									Vector3D rightCandidate = gridA.WorldMatrix.Right;
									Vector3D forwardCandidate = gridA.WorldMatrix.Forward;
									double rightDot = Math.Abs(Vector3D.Dot(directionToGridB, rightCandidate));
									double forwardDot = Math.Abs(Vector3D.Dot(directionToGridB, forwardCandidate));
									gridAUp = (rightDot < forwardDot) ? rightCandidate : forwardCandidate;
								}

								// Handle parallel case (though this should be rare now)
								double dotProduct = Math.Abs(Vector3D.Dot(directionToGridB, gridAUp));
								if (dotProduct > 0.999)
								{
									Vector3D rightCandidate = proxyMatrix.Right;
									Vector3D forwardCandidate = proxyMatrix.Forward;
									double rightDot = Math.Abs(Vector3D.Dot(directionToGridB, rightCandidate));
									double forwardDot = Math.Abs(Vector3D.Dot(directionToGridB, forwardCandidate));
									gridAUp = (rightDot < forwardDot) ? rightCandidate : forwardCandidate;
								}

								Vector3D shouldBeRight = Vector3D.Normalize(Vector3D.Cross(directionToGridB, gridAUp));
								Vector3D shouldBeUp = Vector3D.Cross(shouldBeRight, directionToGridB);
								MatrixD shouldBeLookingAt = MatrixD.CreateWorld(Vector3D.Zero, directionToGridB, shouldBeUp);

								// Get gridA's actual orientation (no translation)
								MatrixD gridAActualOrientation = proxyMatrix;
								gridAActualOrientation.Translation = Vector3D.Zero;

								// Calculate the differential rotation (flip the order)
								MatrixD differential = MatrixD.Invert(shouldBeLookingAt) * gridAActualOrientation;
								MatrixD differentialRotation = MatrixD.CreateFromQuaternion(Quaternion.CreateFromRotationMatrix(differential));

								// Apply the compensation
								rotationMatrixForView = rotationMatrixForView * differentialRotation;
								break;

                            default:
                                rotationMatrixForView = MatrixD.Identity;
                                break;
                        }

                        // Rotate around a center point
                        Vector3D rotatedRelativePosition = Vector3D.Transform(blockPositionInBlocksScaledForHologram, rotationMatrixForView);
                        blockPositionInHologram = rotatedRelativePosition + hologramConsoleLocation; // Position it in the hologram console
                    }

                    finalBlockPositionToDraw = blockPositionInHologram;
					block.HologramDrawPosition = finalBlockPositionToDraw; // Will be used by render pipeline.
                }
			}
        }

        Vector3D SnapDirectionTo45Degrees(Vector3D dir)
        {
            double x = SnapComponent(dir.X);
            double y = SnapComponent(dir.Y);
            double z = SnapComponent(dir.Z);
            Vector3D result = new Vector3D(x, y, z);
            if (result.LengthSquared() < 1e-6)
                return Vector3D.Forward; // fallback
            return Vector3D.Normalize(result);
        }

        double SnapComponent(double value)
        {
            if (value > 0.5) return 1;
            if (value < -0.5) return -1;
            if (value > 0.15) return 0.5;
            if (value < -0.15) return -0.5;
            return 0;
        }




        private void HG_DrawHologramTarget_OLD(VRage.Game.ModAPI.IMyCubeGrid targetGrid, List<BlockTracker> blockInfo)
        {

            if (targetGrid != null)
            {
                // Now that we know
                //Vector3D angularVelocity = gHandler.localGridVelocityAngular; // This uses the target grid's angular velocity.
                //MatrixD rotationMatrix = CreateAngularRotationMatrix(angularVelocity, 10); // 

                //// For each block in the blockInfo list we calculate it's position, and health. Which we then pass to HG_DrawBillboard which is responsbile for drawing a square on screen at that position.
                //// I believe HG_DrawBillboard is responsible for the rotation of everything relative to the camera too. 
                //Vector3D positionR = BT.Position;
                //// Let's comment out the rotationMatrix that is applied from angular velocity, we don't need the hologram to "wiggle" based on our angular velocity (or its for that matter)
                //positionR = Vector3D.Rotate(positionR, rotationMatrix); // Adds hologram "wiggle" based on the angular velocity used to calculate rotationMatrix.
                //Vector3D positionT = Vector3D.Transform(positionR, targetGrid.WorldMatrix); // This transforms the position of the block to be relative to the target grid's world matrix.

                // Okay lets do some math and write down my understanding.
                // positionT starts as the position of the block, but it has been normalized. So regardless of where the block is in the real world, it is now relative to the grid's center AND scaled down by dividing by the grid size.
                // so a block that was at (1, 1, 1) relative to the grid's center is now at (0.5, 0.5, 0.5) if the grid size is 2 meters. 
                // Then we transform the positionT by the targetGrid.WorldMatrix which will apply a rotation and translation to the positionT based on the target grid's world matrix.

                // Another example:Grid center at exactly (100, 100, 100) and block is at (100, 101, 100). Grid size is 10 meters. We store a normalized position (0, 1, 0)/10 = (0, 0.1, 0) in positionT.
                // ie. the block was +1 meter in Y axis from the grid's center and we scaled it down by the grid size of 10 meters. 
                // If the grid has rotated 180 degrees on the X axis since the block was stored and moved +1 meters in Z then the transformation using WorldMatrix would first rotate so we get (0, -0.1, 0) and then translate
                // so we get (0, -0.1, 1)...

                // OKAY. So this is actually kinda cool. when we take positionT and subtract the grid's world position coordinates we are effectively removing the translation component of the world matrix.
                // So we get rotation ONLY. NEAT! I've looked into other ways to do this using Quaternions, matrix multiplications etc. but this is rather elegant and simple. I just wish you added a comment about 
                // what the heck was going on so I didn't have to spend 1.5 days figuring it out haha. 
                // This also means we could take MatrixD worldMatrix = targetGrid.WorldMatrix, then set worldMatrix.Translation = Vector3D.Zero; and then use that as the targetGrid.WorldMatrix for the transformation
                // as another way to get rotational only transformation. And it eliminates a 3 component vector subtraction. So it is slightly more CPU efficient. Gimmie dem frames potato! 


                // OLD CODE for reference:
                //Vector3D positionR = BT.Position;
                //positionR = Vector3D.Rotate(positionR, rotationMatrix); // Adds hologram "wiggle" based on the angular velocity used to calculate rotationMatrix.
                //Vector3D positionT = Vector3D.Transform(positionR, targetGrid.WorldMatrix); // This transforms the position of the block to be relative to the target grid's world matrix
                //double HealthPercent = ClampedD(BT.HealthCurrent / BT.HealthMax, 0, 1);
                //HG_DrawBillboard(positionT - targetGrid.GetPosition(), targetGrid, isEntityTarget, HealthPercent);

                // FenixPK's new code 2025-07-16 (not that yours was bad, I just want to know what the heck is going on). Tested and confirmed to work as understood/expected.
                //Vector3D positionRotated = Vector3D.Transform(BT.Position , targetGridRotationalMatrix); // This transforms the position of the block to be relative to the target grid's world matrix, but without translation.

                //MatrixD targetGridRotationalMatrix = targetGrid.WorldMatrix;
                //targetGridRotationalMatrix.Translation = Vector3D.Zero; // Set the translation to zero to get only the rotation component.

                bool skip = true; // Disable existing code and roll my own to see what the hell is going on here.
                if (!skip)
                {
                    foreach (BlockTracker BT in blockInfo)
                    {

                        //Vector3D blockWorldRelativePosition = BT.WorldRelativePosition; // Get the blocks relative world position
                        //MatrixD localGridRotationalMatrix = gHandler.localGridEntity.WorldMatrix;
                        //localGridRotationalMatrix.Translation = Vector3D.Zero; // Get a rotation only matrix for the local grid.
                        //blockWorldRelativePosition = Vector3D.Transform(blockWorldRelativePosition, localGridRotationalMatrix); // Remove/reverse the local grid orientation effect on the hologram. Effectively locks it in place so it only changes based on it's own change in position in the world.
                        // At this point we get a hologram that is locked in place based on it's location in the world, as it changes rotation etc. so will the view, but it does not changed based on localGrid's position in space which is not what we want.
                        // If I comment out the Vector3d.transform then we can at least get a decent view of the ship.... but it rotates as we rotate, not ideal 

                        Vector3D blockWorldRelativePosition = BT.WorldRelativePosition; // Get the blocks relative world position
                        Vector3D localGridWorldPosition = gHandler.localGridControlledEntity.WorldMatrix.Translation;
                        Vector3D targetGridWorldPosition = targetGrid.WorldMatrix.Translation;

                        Vector3D worldSpaceOffset = targetGridWorldPosition - localGridWorldPosition;
                        //Vector3D adjustedPosition = blockWorldRelativePosition + worldSpaceOffset; // This is kinda close... maybe? It's odd.


                        double HealthPercent = ClampedD(BT.HealthCurrent / BT.HealthMax, 0, 1);
                        HG_DrawBillboardTarget(blockWorldRelativePosition, targetGrid, HealthPercent);
                    }
                }

                // Alright, let's roll our own and see what the F is going on here, talk about "out of your depth" holy moly here I am:
                double hologramScale = 0.0075;
                double hologramScaleFactor = 10;

                VRage.Game.ModAPI.IMyCubeGrid gridA = gHandler.localGrid; // Observer grid, position matters rotation does not
                VRage.Game.ModAPI.IMyCubeGrid gridB = targetGrid; // Observed grid, position and rotation matters.

                List<VRage.Game.ModAPI.IMySlimBlock> blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
                targetGrid.GetBlocks(blocks);
                double flipAxisForTarget = -1; // An inversion factor so it draws on the left holographic display

                // Color to use and re-use
                Vector4 color = LINECOLOR_Comp * 0.5f;
                color.W = 1;

                MatrixD angularRotationWiggle = CreateNormalizedTargetGridRotationMatrix(gridB); // Could apply wiggle for angular velocity like localGrid view if in fixed frame mode. So you get a fixed view but see which way it is rotating.

                foreach (VRage.Game.ModAPI.IMySlimBlock block in blocks)
                {
                    // Get health of block
                    double HealthPercent = ClampedD(block.Integrity / block.MaxIntegrity, 0, 1);

                    // Do we even draw it on screen?
                    if (HealthPercent < 0.01)
                    {
                        continue; // Do not draw destroyed blocks. 
                    }

                    // Let's store some variables here for easier pathing below.
                    Vector3D HG_Offset_transformation = Vector3D.Zero; // Offset for location to draw "on screen" (technically relative to the cockpit block not camera/screen but eh)
                    Vector3D hologramConsoleLocation = Vector3D.Zero;
                    Vector3D finalBlockPositionToDraw = Vector3D.Zero; // Final block position to draw with ALL transformations done.
                    Vector3D blockPositionInWorld;
                    block.ComputeWorldCenter(out blockPositionInWorld); // This is the world position of the block's center
                    Vector3D blockPositionRelativeToGridCenter = blockPositionInWorld - gridB.WorldVolume.Center; // Get the blocks positions in meters relative to the grid's center. As if center of grid world volume is center
                    Vector3D blockPositionToTransform = Vector3D.Zero;
                    Vector3D blockPositionTransformed = Vector3D.Zero;
                    Vector3D blockPositionConvertedFromMetersToBlocks = Vector3D.Zero;
                    Vector3D blockPositionInBlocksScaledForHologram = Vector3D.Zero;
                    Vector3D blockPositionInHologram = Vector3D.Zero;
                    double thickness = hologramScaleFactor / (gridB.WorldVolume.Radius / gridB.GridSize);
                    MatrixD scalingMatrix = MatrixD.CreateScale(hologramScale * thickness);
                    float hologramSize = (float)hologramScale * 0.65f * (float)thickness;

                    // Apply the offsets for where the hologram "emitter" is versus the radar center.
                    HG_Offset_transformation = radarMatrix.Left * -radarRadius * flipAxisForTarget + radarMatrix.Left * _hologramRightOffset_HardCode.X * flipAxisForTarget + radarMatrix.Up * _hologramRightOffset_HardCode.Y + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
                    hologramConsoleLocation = worldRadarPos + HG_Offset_transformation; // Set the position to draw the hologram based on where the radar center is, + the offsets. This is where in the world the hologram appears.
                                                                                        // When we draw the blocks relative to the targetGrid's center we will essentially be scaling them down and then drawing them as if the center was this emitter point. 

                    // Original author had the block position divided by gridSize, So instead of working with positions in meters we work with position in blocks and the actual size in game might be different if small grid vs big grid ship. 
                    // This allows the hologram to display a 20x20x20 small block ship the same as a 20x20x20 large block ship.
                    // HOWEVER.... This means when we do anything fun like Inverse matrix of gridA to get gridB into gridA's frame of reference we are using blocks as UoM in gridB's matrix and meters as UoM in gridA's matrix.
                    // This is like mixing Tons and Metric Board Feet in a subtotal and then wondering why your average is wrong. It doesn't work xD



                    if (HologramView_PerspectiveAttemptOne)
                    {
                        // NOTE 2025-07-20 none of this worked, I tried my best but I could not get it working using my own brain. I almost gave up by Claude came in clutch with some 
                        // perspective cam code in the View enum switch below. I mean I definitely had to tease it out by asking specific questions and really thinking about what we had to invert/counteract etc. 
                        // But I don't know how those Dot products actually work, I just know it works in game. 


                        // PERSPECTIVE VIEW MODE:
                        // This view mode shows us a hologram of the targetGrid as it is positioned relative to us. Ie. if we had a gimbaled camera pointing at it from our grid to it what side would we see?
                        // A fun way of thinking of this is the radar is returning signals bounced off the grid and showing us that surface.

                        // Okay round two, let's REALLY think about reference frames. I feel like I'm in an episode of PBS Space Time. 
                        // .WorldMatrix turns grid local positions into world positions. ie. worldPosition = Vector3D.Transform(localPosition, WorldMatrix); - position with world as frame of ref and (0, 0, 0) in world as center.
                        // To get a local position back you would take Vector3D.Transform(worldPosition, MatrixD.Invert(WorldMatrix)); Hot potato pass them back and forth. 
                        // When we call GetPosition(), or ComputeWorldCenter(out pos) for blocks we get the WORLD position. With world (0, 0, 0) as the frame of reference.

                        // So to get gridB (target) into gridA's (player) reference frame (where gridA's current position is (0, 0, 0) as far as gridB is concerned).
                        // This tells us where gridB is positioned and oriented relative to gridA as if gridA is the center of the universe facing forward. That all makes sense to me so far.
                        //MatrixD gridAInverse = MatrixD.Invert(gridA.WorldMatrix);
                        //MatrixD gridBRelativeToA = gridB.WorldMatrix * gridAInverse;

                        /*
                        
						GridA's world matrix:
						Right: (0, 1, 0)
						Up:    (0, 0, 1)
						Fwd:   (1, 0, 0)
						Pos:   (100, -100, 100)

						GridB's world matrix:
						Right: (1, 0, 0)
						Up:    (0, 1, 0)
						Fwd:   (0, 0, -1)
						Pos:   (-100, 100, 200)

						MatrixD gridBRelativeToGridA = gridB.WorldMatrix * MatrixD.Invert(gridA.WorldMatrix); Okay. So we get A^-1
						GridA's inverse: 
						Right: (0, 1, 0)
						Up:    (0, 0, 1)
						Fwd:   (1, 0, 0)
						Pos:   (100, -100, 100)

						See this didn't make sense because matrices can't have inverses if they aren't square so I asked and got this insight about 3D engines. It's actually stored:

						| Right.X  Up.X  Fwd.X  Pos.X |
						| Right.Y  Up.Y  Fwd.Y  Pos.Y |
						| Right.Z  Up.Z  Fwd.Z  Pos.Z |
						|   0       0      0      1   |

						So grid A is 
						0, 0, 1, 100
						1, 0, 0, -100
						0, 1, 0, 100
						0, 0, 0, 1 (this is always 0, 0, 0, 1 - handy.) 

						Inverse is 
						0, 1, 0, 100
						0, 0, 1, -100
						1, 0, 0, -100
						0, 0, 0, 1

						Grid B is :
						1, 0, 0, -100
						0, 1, 0, 100
						0, 0, -1, 200
						0, 0, 0, 1

						Multiply them and we get (I am soooo just using an online calculator for this, I remember filling out PAGES of paper in University just to show the calculation of a matrix multiplication and there's like what 16 (4x4) steps here)
						0, 1, 0, 200
						0, 0, -1, 100
						1, 0, 0, -200
						0, 0, 0, 1 

						So that last matrix is gridB relative to gridA as if gridA were the center of the universe. Can kind of visualize this like we took the world "box" and click+dragged it so it's center was right on gridA and all position values changed accordingly.

						So when I didn't care much for Matrix mathematics back in the day because calculus was more fun THIS was they day it was supposed to be preparing me for. Who knew. 

						*/
                        blockPositionToTransform = blockPositionRelativeToGridCenter;

                        MatrixD gridBRelativeToGridA = gridB.WorldMatrix * MatrixD.Invert(gridA.WorldMatrix);
                        Vector3D blockInGridAFrame = Vector3D.Transform(blockPositionToTransform, gridBRelativeToGridA);
                        //Vector3D forwardOffset = gridA.WorldMatrix.Forward * 2; // Offset so we put this hologram 2 meters (UoM?) in front of gridA. I'll adjust as needed until it appears. 
                        // We add an offset because once blocks from gridB have their world positions normalized based on the world position of gridB's center
                        // and then multiply by gridA's inverse matrix to get them relative to gridA we essentially have a hologram that would be EXACTLY at gridA's center. We can't see it there.
                        // Now ignoring the fact that gridA is actually using the localGridEntity which is the COCKPIT not the grid, so it's actually the cockpit block's center. But whatever. Idea is the same. 
                        blockPositionTransformed = blockInGridAFrame;

                        // Okay now I think my understanding of this is pretty solid as to why the hologram seems to change with your ships orientation. Because the world frame of reference is essentially now a flat plane level with gridA.
                        // So if we are both oriented the same way the hologram appears as expected, as I yaw however, now my forward changes. Lets say I yaw 90 to the right. Well now forward is that way for gridB too.
                        // If I roll now the plane is perpendicular to gridB, so it appears as if the hologram rolled. 
                        // A way this was rephrased to me is that gridA's forward is considered the "camera" forward for the hologram! Which is why rotating gridA around lets us see all sides of the gridB hologram!
                        // When gridA rotates the entire coordinate space the hologram is being drawn in also rotates. 

                        Vector3D forward = Vector3D.Normalize(gridB.WorldMatrix.Translation - gridA.WorldMatrix.Translation); // Direction vector from gridB's position to gridA's with no orientation involved.
                        Vector3D up = Vector3D.Up; // Global world "up"

                        // We will use a Cross calculation to generate a perpendicular. However we need to make sure that forward isn't exactly Up.
                        if (Vector3D.IsZero(Vector3D.Cross(forward, up), 1e-6)) // Use epsilon for floating point error
                        {
                            up = Vector3D.Right; // If forward == up, then lets use global world "right" instead.
                        }

                        Vector3D right = Vector3D.Normalize(Vector3D.Cross(up, forward));
                        up = Vector3D.Normalize(Vector3D.Cross(forward, right));

                        // Build the rotation matrix for perspective
                        MatrixD cameraBasis = MatrixD.Identity;
                        cameraBasis.Right = right;
                        cameraBasis.Up = up;
                        cameraBasis.Forward = forward;
                        cameraBasis.Translation = Vector3D.Zero; // No translation, only rotation. 

                        Vector3D blockPositionInCameraSpace = Vector3D.Transform(blockPositionToTransform, cameraBasis);
                        blockPositionTransformed = blockPositionInCameraSpace;

                    }
                    else
                    {
                        // FLAT VIEW MODE: 
                        // This mode cancels out all rotational elements from localGrid or taretGrid to display a static view. By default this is from behind because everything is relative to gridA pointing forward and all rotation is removed.
                        // With toggles/keybinds this would allow us to take this static view and rotate it as desired. Ie. press left arrow to rotate 90 to the left
                        // Currently I am seeking a radar imager approach, which is where the perspective view above comes in, and you can only see the side facing you.
                        // But this view would be cool for magic space technology that can scan an entire grid and present a hologram of it viewed from any angle.
                        // Maybe we can even do floor plans someday by color coding empty space vs armor/structure blocks vs functional blocks xD

                        MatrixD rotationOnlyGridAMatrix = gridA.WorldMatrix;
                        rotationOnlyGridAMatrix.Translation = Vector3D.Zero;

                        MatrixD rotationOnlyGridBMatrix = gridB.WorldMatrix;
                        rotationOnlyGridBMatrix.Translation = Vector3D.Zero;

                        // Do we wiggle?
                        if (HologramView_AngularWiggleTarget)
                        {
                            blockPositionToTransform = Vector3D.Rotate(blockPositionRelativeToGridCenter, angularRotationWiggle);
                        }
                        else
                        {
                            blockPositionToTransform = blockPositionRelativeToGridCenter;
                        }

                        MatrixD rotationMatrixCancelGridAB = MatrixD.Invert(rotationOnlyGridBMatrix) * rotationOnlyGridAMatrix;
                        Vector3D blockPositionOrientationNeutral = Vector3D.Transform(blockPositionToTransform, rotationMatrixCancelGridAB);


                        blockPositionTransformed = blockPositionOrientationNeutral;
                    }

                    // Do scaling into block unit of measure from meters (so small grid and large grid are drawn the same size in the hologram) HERE, not earlier. Just before we draw this. 
                    // Or better yet move all of this to BeforeUpdateSimulation and just store them until we do the draw call but that re-write is waaaay down the road at this point. 
                    // Calculate an offset transformation so it draws on the left or right holographic display based on settings, flipAxisForTarget = -1 causes it to draw on the left
                    blockPositionConvertedFromMetersToBlocks = blockPositionTransformed / gridB.GridSize; // Convert from meters UoM to blocks UoM independent of large vs small grid ship.
                    blockPositionInBlocksScaledForHologram = Vector3D.Transform(blockPositionConvertedFromMetersToBlocks, scalingMatrix);

                    if (!HologramView_PerspectiveAttemptOne)
                    {
                        MatrixD rotationMatrixForView = MatrixD.Identity; // new matrixD.

                        // Can use MatrixD.slerp to smoothly transition between a current matrix and a target matrix for rotation...

                        // I actually think instead of just CreateRotationY/X/Z I need to do this relative to my cockpit blocks "up" direction, or it doesn't make any sense.
                        switch (HologramView_Current)
                        {
                            case HologramView_Side.Rear:
                                // Rear (default - no rotation needed)
                                rotationMatrixForView = MatrixD.Identity;
                                break;
                            case HologramView_Side.Left:
                                // Left side (90° yaw left)
                                rotationMatrixForView = MatrixD.CreateFromAxisAngle(gridA.WorldMatrix.Up, MathHelper.ToRadians(90)); // Use up so we pivot around that axis.
                                break;
                            case HologramView_Side.Front:
                                // Front (180° yaw)
                                rotationMatrixForView = MatrixD.CreateFromAxisAngle(gridA.WorldMatrix.Up, MathHelper.ToRadians(180)); // Use up so we pivot around that axis.
                                break;
                            case HologramView_Side.Right:
                                // Right side (90° yaw right / 270° left)
                                rotationMatrixForView = MatrixD.CreateFromAxisAngle(gridA.WorldMatrix.Up, MathHelper.ToRadians(-90)); // Use up so we pivot around that axis.
                                break;
                            case HologramView_Side.Top:
                                // Top view (90° pitch down)
                                rotationMatrixForView = MatrixD.CreateFromAxisAngle(gridA.WorldMatrix.Right, MathHelper.ToRadians(-90)); // Use right so we pivot around that axis.
                                break;
                            case HologramView_Side.Bottom:
                                // Bottom view (90° pitch up)
                                rotationMatrixForView = MatrixD.CreateFromAxisAngle(gridA.WorldMatrix.Right, MathHelper.ToRadians(90)); // Use right so we pivot around that axis.
                                break;
                            case HologramView_Side.Orbit:

                                // This is technically an "Orbit" cam... It will show a hologram of the target grid EXACTLY as it appears in world space relative to your orientation
                                // but not your translation/position in the world. So you can rotate your ship around and see all sides of gridB from anywhere in the world.
                                // if you are facing a direction and the target is also facing the same direction you see it's backside, even if it is behind you lol.

                                // What transformation gets us from gridA's coordinate system to gridB's?
                                MatrixD gridAToGridB = MatrixD.Invert(gridA.WorldMatrix) * gridB.WorldMatrix;

                                //// Use this as the rotation (this naturally includes both position and orientation differences)
                                rotationMatrixForView = MatrixD.CreateFromQuaternion(Quaternion.CreateFromRotationMatrix(gridAToGridB));
                                break;
                            case HologramView_Side.Perspective:
                                // Round 1 works great unless you roll.
                                //// Create proxy matrix: gridB's orientation at gridA's position
                                //MatrixD proxyMatrix = gridB.WorldMatrix;
                                //proxyMatrix.Translation = gridA.WorldMatrix.Translation;

                                //MatrixD relativeOrientation = gridB.WorldMatrix * MatrixD.Invert(proxyMatrix);
                                //// Extract just the rotation part (remove any translation)
                                //rotationMatrixForView = MatrixD.CreateFromQuaternion(Quaternion.CreateFromRotationMatrix(relativeOrientation));
                                //// Create a "should be looking at gridB" orientation using gridA's up
                                //Vector3D directionToGridB = Vector3D.Normalize(gridB.WorldMatrix.Translation - proxyMatrix.Translation);
                                //Vector3D gridAUp = proxyMatrix.Up;
                                //// Handle parallel case
                                //double dotProduct = Math.Abs(Vector3D.Dot(directionToGridB, gridAUp));
                                //if (Math.Abs(dotProduct) > 0.999)
                                //{
                                //	// Choose Right or Forward, whichever is more perpendicular to the direction
                                //	Vector3D rightCandidate = proxyMatrix.Right;
                                //	Vector3D forwardCandidate = proxyMatrix.Forward;

                                //	double rightDot = Math.Abs(Vector3D.Dot(directionToGridB, rightCandidate));
                                //	double forwardDot = Math.Abs(Vector3D.Dot(directionToGridB, forwardCandidate));

                                //	gridAUp = (rightDot < forwardDot) ? rightCandidate : forwardCandidate;
                                //}
                                //Vector3D shouldBeRight = Vector3D.Normalize(Vector3D.Cross(directionToGridB, gridAUp));
                                //Vector3D shouldBeUp = Vector3D.Cross(shouldBeRight, directionToGridB);
                                //MatrixD shouldBeLookingAt = MatrixD.CreateWorld(Vector3D.Zero, directionToGridB, shouldBeUp);
                                //// Get gridA's actual orientation (no translation)
                                //MatrixD gridAActualOrientation = proxyMatrix;
                                //gridAActualOrientation.Translation = Vector3D.Zero;
                                //// Calculate the differential rotation (flip the order)
                                //MatrixD differential = MatrixD.Invert(shouldBeLookingAt) * gridAActualOrientation;
                                //MatrixD differentialRotation = MatrixD.CreateFromQuaternion(Quaternion.CreateFromRotationMatrix(differential));
                                //// Apply the compensation
                                //rotationMatrixForView = rotationMatrixForView * differentialRotation;




                                // Round 2 seems to work great even rolled unless to left or right of gridB.
                                // Create proxy matrix: gridB's orientation at gridA's position
                                //MatrixD proxyMatrix = gridB.WorldMatrix;
                                //proxyMatrix.Translation = gridA.WorldMatrix.Translation;
                                //MatrixD relativeOrientation = gridB.WorldMatrix * MatrixD.Invert(proxyMatrix);
                                //rotationMatrixForView = MatrixD.CreateFromQuaternion(Quaternion.CreateFromRotationMatrix(relativeOrientation));

                                //// Create a "should be looking at" orientation, but use gridA's roll relative to gridB
                                //Vector3D directionToGridB = Vector3D.Normalize(gridB.WorldMatrix.Translation - proxyMatrix.Translation);

                                //// Calculate how much gridA is rolled relative to gridB
                                //Vector3D gridAUpInWorldSpace = gridA.WorldMatrix.Up;
                                //Vector3D gridBUpInWorldSpace = gridB.WorldMatrix.Up;
                                //Vector3D gridBForwardInWorldSpace = gridB.WorldMatrix.Forward;

                                //// Project gridA's up vector onto the plane perpendicular to gridB's forward
                                //Vector3D projectedGridAUp = gridAUpInWorldSpace - Vector3D.Dot(gridAUpInWorldSpace, gridBForwardInWorldSpace) * gridBForwardInWorldSpace;
                                //projectedGridAUp = Vector3D.Normalize(projectedGridAUp);

                                //// Use this roll-adjusted up vector
                                //Vector3D gridAUp = projectedGridAUp;

                                //// Handle parallel case (same as before)
                                //double dotProduct = Math.Abs(Vector3D.Dot(directionToGridB, gridAUp));
                                //if (dotProduct > 0.999)
                                //{
                                //    Vector3D rightCandidate = proxyMatrix.Right;
                                //    Vector3D forwardCandidate = proxyMatrix.Forward;
                                //    double rightDot = Math.Abs(Vector3D.Dot(directionToGridB, rightCandidate));
                                //    double forwardDot = Math.Abs(Vector3D.Dot(directionToGridB, forwardCandidate));
                                //    gridAUp = (rightDot < forwardDot) ? rightCandidate : forwardCandidate;
                                //}

                                //Vector3D shouldBeRight = Vector3D.Normalize(Vector3D.Cross(directionToGridB, gridAUp));
                                //Vector3D shouldBeUp = Vector3D.Cross(shouldBeRight, directionToGridB);
                                //MatrixD shouldBeLookingAt = MatrixD.CreateWorld(Vector3D.Zero, directionToGridB, shouldBeUp);
                                //// Get gridA's actual orientation (no translation)
                                //MatrixD gridAActualOrientation = proxyMatrix;
                                //gridAActualOrientation.Translation = Vector3D.Zero;
                                //// Calculate the differential rotation (flip the order)
                                //MatrixD differential = MatrixD.Invert(shouldBeLookingAt) * gridAActualOrientation;
                                //MatrixD differentialRotation = MatrixD.CreateFromQuaternion(Quaternion.CreateFromRotationMatrix(differential));
                                //// Apply the compensation
                                //rotationMatrixForView = rotationMatrixForView * differentialRotation;


                                // Round 3, the winner, this perpendicular on the left/right being handled made all the difference. It has the effect I was hoping for. 
                                // Create proxy matrix: gridB's orientation at gridA's position
                                MatrixD proxyMatrix = gridB.WorldMatrix;
                                proxyMatrix.Translation = gridA.WorldMatrix.Translation;
                                MatrixD relativeOrientation = gridB.WorldMatrix * MatrixD.Invert(proxyMatrix);
                                // Extract just the rotation part (remove any translation)
                                rotationMatrixForView = MatrixD.CreateFromQuaternion(Quaternion.CreateFromRotationMatrix(relativeOrientation));

                                // Create a "should be looking at" orientation
                                Vector3D directionToGridB = Vector3D.Normalize(gridB.WorldMatrix.Translation - proxyMatrix.Translation);

                                // Project gridA's up vector onto the plane perpendicular to the direction vector
                                Vector3D gridAUpInWorldSpace = gridA.WorldMatrix.Up;
                                Vector3D projectedGridAUp = gridAUpInWorldSpace - Vector3D.Dot(gridAUpInWorldSpace, directionToGridB) * directionToGridB;

                                Vector3D gridAUp;
                                // Check if the projection is valid (not too small)
                                if (projectedGridAUp.LengthSquared() > 0.001)
                                {
                                    gridAUp = Vector3D.Normalize(projectedGridAUp);
                                }
                                else
                                {
                                    // Fallback when projection fails (gridA's up is parallel to direction)
                                    Vector3D rightCandidate = gridA.WorldMatrix.Right;
                                    Vector3D forwardCandidate = gridA.WorldMatrix.Forward;
                                    double rightDot = Math.Abs(Vector3D.Dot(directionToGridB, rightCandidate));
                                    double forwardDot = Math.Abs(Vector3D.Dot(directionToGridB, forwardCandidate));
                                    gridAUp = (rightDot < forwardDot) ? rightCandidate : forwardCandidate;
                                }

                                // Handle parallel case (though this should be rare now)
                                double dotProduct = Math.Abs(Vector3D.Dot(directionToGridB, gridAUp));
                                if (dotProduct > 0.999)
                                {
                                    Vector3D rightCandidate = proxyMatrix.Right;
                                    Vector3D forwardCandidate = proxyMatrix.Forward;
                                    double rightDot = Math.Abs(Vector3D.Dot(directionToGridB, rightCandidate));
                                    double forwardDot = Math.Abs(Vector3D.Dot(directionToGridB, forwardCandidate));
                                    gridAUp = (rightDot < forwardDot) ? rightCandidate : forwardCandidate;
                                }

                                Vector3D shouldBeRight = Vector3D.Normalize(Vector3D.Cross(directionToGridB, gridAUp));
                                Vector3D shouldBeUp = Vector3D.Cross(shouldBeRight, directionToGridB);
                                MatrixD shouldBeLookingAt = MatrixD.CreateWorld(Vector3D.Zero, directionToGridB, shouldBeUp);

                                // Get gridA's actual orientation (no translation)
                                MatrixD gridAActualOrientation = proxyMatrix;
                                gridAActualOrientation.Translation = Vector3D.Zero;

                                // Calculate the differential rotation (flip the order)
                                MatrixD differential = MatrixD.Invert(shouldBeLookingAt) * gridAActualOrientation;
                                MatrixD differentialRotation = MatrixD.CreateFromQuaternion(Quaternion.CreateFromRotationMatrix(differential));

                                // Apply the compensation
                                rotationMatrixForView = rotationMatrixForView * differentialRotation;


                                break;

                            default:
                                rotationMatrixForView = MatrixD.Identity;
                                break;
                        }

                        // Rotate around a center point
                        Vector3D rotatedRelativePosition = Vector3D.Transform(blockPositionInBlocksScaledForHologram, rotationMatrixForView);
                        blockPositionInHologram = rotatedRelativePosition + hologramConsoleLocation; // Position it in the hologram console
                    }

                    finalBlockPositionToDraw = blockPositionInHologram;

                    // Draw on screen.
                    MyTransparentGeometry.AddBillboardOriented(
                        MaterialSquare,
                        color,
                        finalBlockPositionToDraw,
                        MyAPIGateway.Session.Camera.WorldMatrix.Left, // Orient billboard drawn toward camera so we can see the square. 
                        MyAPIGateway.Session.Camera.WorldMatrix.Up,
                        hologramSize,
                        MyBillboard.BlendTypeEnum.AdditiveTop);

                    if (GetRandomFloat() > 0.9f)
                    {
                        // Aha so this is the shimmery holographic effect under it.
                        Vector3D holoCenter = radarMatrix.Left * -radarRadius * flipAxisForTarget + radarMatrix.Left * _hologramRightOffset_HardCode.X * flipAxisForTarget + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
                        holoCenter += worldRadarPos;
                        Vector3D holoDir = Vector3D.Normalize(finalBlockPositionToDraw - holoCenter);
                        double holoLength = Vector3D.Distance(holoCenter, finalBlockPositionToDraw);
                        DrawLineBillboard(MaterialSquare, color * 0.15f, holoCenter, holoDir, (float)holoLength, 0.0025f, BlendTypeEnum.AdditiveTop);
                    }
                }
            }
        }




        //private void HG_DrawHologramTarget(VRage.Game.ModAPI.IMyCubeGrid targetGrid, List<BlockTracker> blockInfo)
        //{


        //    if (targetGrid != null)
        //    {
        //        // Now that we know
        //        //Vector3D angularVelocity = gHandler.localGridVelocityAngular; // This uses the target grid's angular velocity.
        //        //MatrixD rotationMatrix = CreateAngularRotationMatrix(angularVelocity, 10); // 

        //        //// For each block in the blockInfo list we calculate it's position, and health. Which we then pass to HG_DrawBillboard which is responsbile for drawing a square on screen at that position.
        //        //// I believe HG_DrawBillboard is responsible for the rotation of everything relative to the camera too. 
        //        //Vector3D positionR = BT.Position;
        //        //// Let's comment out the rotationMatrix that is applied from angular velocity, we don't need the hologram to "wiggle" based on our angular velocity (or its for that matter)
        //        //positionR = Vector3D.Rotate(positionR, rotationMatrix); // Adds hologram "wiggle" based on the angular velocity used to calculate rotationMatrix.
        //        //Vector3D positionT = Vector3D.Transform(positionR, targetGrid.WorldMatrix); // This transforms the position of the block to be relative to the target grid's world matrix.

        //        // Okay lets do some math and write down my understanding.
        //        // positionT starts as the position of the block, but it has been normalized. So regardless of where the block is in the real world, it is now relative to the grid's center AND scaled down by dividing by the grid size.
        //        // so a block that was at (1, 1, 1) relative to the grid's center is now at (0.5, 0.5, 0.5) if the grid size is 2 meters. 
        //        // Then we transform the positionT by the targetGrid.WorldMatrix which will apply a rotation and translation to the positionT based on the target grid's world matrix.

        //        // Another example:Grid center at exactly (100, 100, 100) and block is at (100, 101, 100). Grid size is 10 meters. We store a normalized position (0, 1, 0)/10 = (0, 0.1, 0) in positionT.
        //        // ie. the block was +1 meter in Y axis from the grid's center and we scaled it down by the grid size of 10 meters. 
        //        // If the grid has rotated 180 degrees on the X axis since the block was stored and moved +1 meters in Z then the transformation using WorldMatrix would first rotate so we get (0, -0.1, 0) and then translate
        //        // so we get (0, -0.1, 1)...

        //        // OKAY. So this is actually kinda cool. when we take positionT and subtract the grid's world position coordinates we are effectively removing the translation component of the world matrix.
        //        // So we get rotation ONLY. NEAT! I've looked into other ways to do this using Quaternions, matrix multiplications etc. but this is rather elegant and simple. I just wish you added a comment about 
        //        // what the heck was going on so I didn't have to spend 1.5 days figuring it out haha. 
        //        // This also means we could take MatrixD worldMatrix = targetGrid.WorldMatrix, then set worldMatrix.Translation = Vector3D.Zero; and then use that as the targetGrid.WorldMatrix for the transformation
        //        // as another way to get rotational only transformation. And it eliminates a 3 component vector subtraction. So it is slightly more CPU efficient. Gimmie dem frames potato! 


        //        // OLD CODE for reference:
        //        //Vector3D positionR = BT.Position;
        //        //positionR = Vector3D.Rotate(positionR, rotationMatrix); // Adds hologram "wiggle" based on the angular velocity used to calculate rotationMatrix.
        //        //Vector3D positionT = Vector3D.Transform(positionR, targetGrid.WorldMatrix); // This transforms the position of the block to be relative to the target grid's world matrix
        //        //double HealthPercent = ClampedD(BT.HealthCurrent / BT.HealthMax, 0, 1);
        //        //HG_DrawBillboard(positionT - targetGrid.GetPosition(), targetGrid, isEntityTarget, HealthPercent);

        //        // FenixPK's new code 2025-07-16 (not that yours was bad, I just want to know what the heck is going on). Tested and confirmed to work as understood/expected.
        //        //Vector3D positionRotated = Vector3D.Transform(BT.Position , targetGridRotationalMatrix); // This transforms the position of the block to be relative to the target grid's world matrix, but without translation.

        //        //MatrixD targetGridRotationalMatrix = targetGrid.WorldMatrix;
        //        //targetGridRotationalMatrix.Translation = Vector3D.Zero; // Set the translation to zero to get only the rotation component.

        //        bool skip = true; // Disable existing code and roll my own to see what the hell is going on here.
        //        if (!skip)
        //        {
        //            foreach (BlockTracker BT in blockInfo)
        //            {

        //                //Vector3D blockWorldRelativePosition = BT.WorldRelativePosition; // Get the blocks relative world position
        //                //MatrixD localGridRotationalMatrix = gHandler.localGridEntity.WorldMatrix;
        //                //localGridRotationalMatrix.Translation = Vector3D.Zero; // Get a rotation only matrix for the local grid.
        //                //blockWorldRelativePosition = Vector3D.Transform(blockWorldRelativePosition, localGridRotationalMatrix); // Remove/reverse the local grid orientation effect on the hologram. Effectively locks it in place so it only changes based on it's own change in position in the world.
        //                // At this point we get a hologram that is locked in place based on it's location in the world, as it changes rotation etc. so will the view, but it does not changed based on localGrid's position in space which is not what we want.
        //                // If I comment out the Vector3d.transform then we can at least get a decent view of the ship.... but it rotates as we rotate, not ideal 

        //                Vector3D blockWorldRelativePosition = BT.WorldRelativePosition; // Get the blocks relative world position
        //                Vector3D localGridWorldPosition = gHandler.localGridEntity.WorldMatrix.Translation;
        //                Vector3D targetGridWorldPosition = targetGrid.WorldMatrix.Translation;

        //                Vector3D worldSpaceOffset = targetGridWorldPosition - localGridWorldPosition;
        //                //Vector3D adjustedPosition = blockWorldRelativePosition + worldSpaceOffset; // This is kinda close... maybe? It's odd.


        //                double HealthPercent = ClampedD(BT.HealthCurrent / BT.HealthMax, 0, 1);
        //                HG_DrawBillboardTarget(blockWorldRelativePosition, targetGrid, HealthPercent);
        //            }
        //        }

        //        // Alright, let's roll our own and see what the F is going on here, talk about "out of your depth" holy moly here I am:
        //        double hologramScale = 0.0075;
        //        double hologramScaleFactor = 10;

        //        VRage.Game.ModAPI.IMyCubeGrid gridA = playerGrid; // Observer grid, position matters rotation does not
        //        VRage.Game.ModAPI.IMyCubeGrid gridB = targetGrid; // Observed grid, position and rotation matters.

        //        List<VRage.Game.ModAPI.IMySlimBlock> blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
        //        targetGrid.GetBlocks(blocks);
        //        double flipAxisForTarget = -1; // An inversion factor so it draws on the left holographic display



        //        MatrixD angularRotationWiggle = CreateNormalizedLocalGridRotationMatrix();
        //        foreach (VRage.Game.ModAPI.IMySlimBlock block in blocks)
        //        {
        //            // Let's get three types of positions, world, grid relative, and grid center relative.
        //            Vector3D blockPositionInWorld;
        //            block.ComputeWorldCenter(out blockPositionInWorld); // This is the world position of the block's center
        //            Vector3D blockPositionRelativeToGridCenter = blockPositionInWorld - gridB.WorldVolume.Center; ;
        //            Vector3D blockScaledPositionRelativeToGridCenter = blockPositionRelativeToGridCenter / gridB.GridSize;

        //            MatrixD rotationOnlyGridAMatrix = gridA.WorldMatrix;
        //            rotationOnlyGridAMatrix.Translation = Vector3D.Zero; // Set the translation to zero to get only the rotation component.

        //            MatrixD rotationOnlyGridBMatrix = gridB.WorldMatrix;
        //            rotationOnlyGridBMatrix.Translation = Vector3D.Zero;

        //            MatrixD combinedRotationMatrix = MatrixD.Invert(rotationOnlyGridBMatrix) * rotationOnlyGridAMatrix;

        //            Vector3D blockPositionRotated = Vector3D.Transform(blockScaledPositionRelativeToGridCenter, combinedRotationMatrix);



        //            // Toggle between flat view and relative perspective view
        //            bool showRelativePerspective = false; // Your toggle variable

        //            Vector3D blockPositionRotated;

        //            if (showRelativePerspective)
        //            {
        //                // RELATIVE PERSPECTIVE MODE: Show gridB as it appears from gridA's viewpoint

        //                // Calculate the direction from gridA to gridB
        //                Vector3D directionToGridB = Vector3D.Normalize(gridB.WorldVolume.Center - gridA.WorldVolume.Center);

        //                // Create a "look at" matrix from gridA's perspective looking toward gridB
        //                // This creates a view matrix that represents looking from gridA toward gridB
        //                MatrixD lookAtMatrix = MatrixD.CreateLookAt(
        //                    Vector3D.Zero,           // We're working in relative space, so start at origin
        //                    directionToGridB,        // Look toward gridB
        //                    gridA.WorldMatrix.Up     // Use gridA's up vector for orientation
        //                );

        //                // Apply gridB's rotation relative to this viewing angle
        //                MatrixD rotationOnlyGridBMatrix = gridB.WorldMatrix;
        //                rotationOnlyGridBMatrix.Translation = Vector3D.Zero;

        //                // Combine the perspective view with gridB's actual rotation
        //                MatrixD relativePerspectiveMatrix = rotationOnlyGridBMatrix * MatrixD.Invert(lookAtMatrix);

        //                // Apply the relative perspective transformation
        //                blockPositionRotated = Vector3D.Transform(blockScaledPositionRelativeToGridCenter, relativePerspectiveMatrix);
        //            }
        //            else
        //            {
        //                // FLAT VIEW MODE: Your current working code
        //                MatrixD rotationOnlyGridAMatrix = gridA.WorldMatrix;
        //                rotationOnlyGridAMatrix.Translation = Vector3D.Zero;

        //                MatrixD rotationOnlyGridBMatrix = gridB.WorldMatrix;
        //                rotationOnlyGridBMatrix.Translation = Vector3D.Zero;

        //                MatrixD combinedRotationMatrix = MatrixD.Invert(rotationOnlyGridBMatrix) * rotationOnlyGridAMatrix;
        //                blockPositionRotated = Vector3D.Transform(blockScaledPositionRelativeToGridCenter, combinedRotationMatrix);
        //            }

        //            // Continue with your existing scaling and positioning code
        //            MatrixD scalingMatrix = MatrixD.CreateScale(hologramScale * thickness);
        //            Vector3D blockPositionToDraw = Vector3D.Transform(blockPositionRotated, scalingMatrix);
        //            blockPositionToDraw = blockPositionToDraw + (worldRadarPos + HG_Offset_transformation);









        //            // Get health of block
        //            double HealthPercent = ClampedD(block.Integrity / block.MaxIntegrity, 0, 1);

        //            // Now to draw block on screen
        //            if (HealthPercent < 0.01)
        //            {
        //                continue; // Do not draw destroyed blocks. 
        //            }

        //            // Color to use
        //            Vector4 color = LINECOLOR_Comp * 0.5f;
        //            color.W = 1;

        //            // Calculate an offset transformation so it draws on the left or right holographic display based on settings, flipAxisForTarget = -1 causes it to draw on the left
        //            Vector3D HG_Offset_transformation = radarMatrix.Left * -radarRadius * flipAxisForTarget + radarMatrix.Left * HG_Offset.X * flipAxisForTarget + radarMatrix.Up * HG_Offset.Y + radarMatrix.Forward * HG_Offset.Z;
        //            double thickness = hologramScaleFactor / (gridB.WorldVolume.Radius / gridB.GridSize);
        //            MatrixD scalingMatrix = MatrixD.CreateScale(hologramScale * thickness);
        //            float hologramSize = (float)hologramScale * 0.65f * (float)thickness;

        //            Vector3D blockPositionToTransform = blockPositionRotated;
        //            Vector3D blockPositionToDraw = blockPositionToTransform;
        //            blockPositionToDraw = Vector3D.Transform(blockPositionToTransform, scalingMatrix);

        //            blockPositionToDraw = blockPositionToDraw + (worldRadarPos + HG_Offset_transformation);

        //            // Draw on screen.
        //            MyTransparentGeometry.AddBillboardOriented(
        //                MaterialSquare,
        //                color,
        //                blockPositionToDraw,
        //                MyAPIGateway.Session.Camera.WorldMatrix.Left, // Orient billboard drawn toward camera so we can see the square. 
        //                MyAPIGateway.Session.Camera.WorldMatrix.Up,
        //                hologramSize,
        //                MyBillboard.BlendTypeEnum.AdditiveTop);

        //            if (GetRandomFloat() > 0.9f)
        //            {
        //                // Aha so this is the shimmery holographic effect under it.
        //                Vector3D holoCenter = radarMatrix.Left * -radarRadius * flipAxisForTarget + radarMatrix.Left * HG_Offset.X * flipAxisForTarget + radarMatrix.Forward * HG_Offset.Z;
        //                holoCenter += worldRadarPos;
        //                Vector3D holoDir = Vector3D.Normalize(blockPositionToDraw - holoCenter);
        //                double holoLength = Vector3D.Distance(holoCenter, blockPositionToDraw);
        //                DrawLineBillboard(MaterialSquare, color * 0.15f, holoCenter, holoDir, (float)holoLength, 0.0025f, BlendTypeEnum.AdditiveTop);
        //            }
        //        }
        //    }
        //}

















        // Okay motherfucker, I'm asking Claude some basic questions about why it does what it does currently. I've made the above so far by my own understanding... I just can't get the result I want.
        // I've tried so many things...

        /*
		 The result:
		If gridB rotates in the world, the holographic display will show the same static structural layout. For example, if the target ship's nose was pointing "up" in the hologram when the ship was facing north, the nose will still point "up" in the hologram even when the actual ship rotates to face east, west, or any other direction.
		To make the hologram rotate with the target grid, the code would need to transform blockPositionRelativeToGridCenter through gridB's rotation matrix before applying the scaling and positioning transforms.

		The visual result:
		If you're looking at a hologram of a ship and then turn your observer ship 90 degrees to the right, the holographic ship model will appear in the same world-space orientation. 
		From your new viewing angle, it might appear to have "rotated left" relative to your view, but that's just because you changed your perspective - the hologram itself maintains its absolute world orientation.
		To make the hologram rotate with the observer's view (like a typical radar display), the code would need to transform the block positions through the inverse of the observer's rotation matrix.
		*/


        // Shimmer
        //if (GetRandomFloat() > 0.9f)
        //                  {
        //                      // Aha so this is the shimmery holographic effect under it.
        //                      Vector3D holoCenter = radarMatrix.Left * -radarRadius * flipAxisForTarget + radarMatrix.Left * HG_Offset.X * flipAxisForTarget + radarMatrix.Forward * HG_Offset.Z;
        //      holoCenter += worldRadarPos;
        //                      Vector3D holoDir = Vector3D.Normalize(position - holoCenter);
        //      double holoLength = Vector3D.Distance(holoCenter, position);

        //      DrawLineBillboard(MaterialSquare, color* 0.15f * (float) dotProd * (float) bootUpAlpha, holoCenter, holoDir, (float) holoLength, 0.0025f, BlendTypeEnum.AdditiveTop);
        //                  }

        // Shimmer



        //private void HG_DrawHologramTarget(VRage.Game.ModAPI.IMyCubeGrid targetGrid, List<BlockTracker> blockInfo)
        //{
        //    if (targetGrid != null)
        //    {
        //        // Now that we know
        //        //Vector3D angularVelocity = gHandler.localGridVelocityAngular; // This uses the target grid's angular velocity.
        //        //MatrixD rotationMatrix = CreateAngularRotationMatrix(angularVelocity, 10); // 

        //        //// For each block in the blockInfo list we calculate it's position, and health. Which we then pass to HG_DrawBillboard which is responsbile for drawing a square on screen at that position.
        //        //// I believe HG_DrawBillboard is responsible for the rotation of everything relative to the camera too. 
        //        //Vector3D positionR = BT.Position;
        //        //// Let's comment out the rotationMatrix that is applied from angular velocity, we don't need the hologram to "wiggle" based on our angular velocity (or its for that matter)
        //        //positionR = Vector3D.Rotate(positionR, rotationMatrix); // Adds hologram "wiggle" based on the angular velocity used to calculate rotationMatrix.
        //        //Vector3D positionT = Vector3D.Transform(positionR, targetGrid.WorldMatrix); // This transforms the position of the block to be relative to the target grid's world matrix.

        //        // Okay lets do some math and write down my understanding.
        //        // positionT starts as the position of the block, but it has been normalized. So regardless of where the block is in the real world, it is now relative to the grid's center AND scaled down by dividing by the grid size.
        //        // so a block that was at (1, 1, 1) relative to the grid's center is now at (0.5, 0.5, 0.5) if the grid size is 2 meters. 
        //        // Then we transform the positionT by the targetGrid.WorldMatrix which will apply a rotation and translation to the positionT based on the target grid's world matrix.

        //        // Another example:Grid center at exactly (100, 100, 100) and block is at (100, 101, 100). Grid size is 10 meters. We store a normalized position (0, 1, 0)/10 = (0, 0.1, 0) in positionT.
        //        // ie. the block was +1 meter in Y axis from the grid's center and we scaled it down by the grid size of 10 meters. 
        //        // If the grid has rotated 180 degrees on the X axis since the block was stored and moved +1 meters in Z then the transformation using WorldMatrix would first rotate so we get (0, -0.1, 0) and then translate
        //        // so we get (0, -0.1, 1)...

        //        // OKAY. So this is actually kinda cool. when we take positionT and subtract the grid's world position coordinates we are effectively removing the translation component of the world matrix.
        //        // So we get rotation ONLY. NEAT! I've looked into other ways to do this using Quaternions, matrix multiplications etc. but this is rather elegant and simple. I just wish you added a comment about 
        //        // what the heck was going on so I didn't have to spend 1.5 days figuring it out haha. 
        //        // This also means we could take MatrixD worldMatrix = targetGrid.WorldMatrix, then set worldMatrix.Translation = Vector3D.Zero; and then use that as the targetGrid.WorldMatrix for the transformation
        //        // as another way to get rotational only transformation. And it eliminates a 3 component vector subtraction. So it is slightly more CPU efficient. Gimmie dem frames potato! 


        //        // OLD CODE for reference:
        //        //Vector3D positionR = BT.Position;
        //        //positionR = Vector3D.Rotate(positionR, rotationMatrix); // Adds hologram "wiggle" based on the angular velocity used to calculate rotationMatrix.
        //        //Vector3D positionT = Vector3D.Transform(positionR, targetGrid.WorldMatrix); // This transforms the position of the block to be relative to the target grid's world matrix
        //        //double HealthPercent = ClampedD(BT.HealthCurrent / BT.HealthMax, 0, 1);
        //        //HG_DrawBillboard(positionT - targetGrid.GetPosition(), targetGrid, isEntityTarget, HealthPercent);

        //        // FenixPK's new code 2025-07-16 (not that yours was bad, I just want to know what the heck is going on). Tested and confirmed to work as understood/expected.
        //        //Vector3D positionRotated = Vector3D.Transform(BT.Position , targetGridRotationalMatrix); // This transforms the position of the block to be relative to the target grid's world matrix, but without translation.

        //        //MatrixD targetGridRotationalMatrix = targetGrid.WorldMatrix;
        //        //targetGridRotationalMatrix.Translation = Vector3D.Zero; // Set the translation to zero to get only the rotation component.

        //        bool skip = true; // Disable existing code and roll my own to see what the hell is going on here.
        //        if (!skip)
        //        {
        //            foreach (BlockTracker BT in blockInfo)
        //            {

        //                //Vector3D blockWorldRelativePosition = BT.WorldRelativePosition; // Get the blocks relative world position
        //                //MatrixD localGridRotationalMatrix = gHandler.localGridEntity.WorldMatrix;
        //                //localGridRotationalMatrix.Translation = Vector3D.Zero; // Get a rotation only matrix for the local grid.
        //                //blockWorldRelativePosition = Vector3D.Transform(blockWorldRelativePosition, localGridRotationalMatrix); // Remove/reverse the local grid orientation effect on the hologram. Effectively locks it in place so it only changes based on it's own change in position in the world.
        //                // At this point we get a hologram that is locked in place based on it's location in the world, as it changes rotation etc. so will the view, but it does not changed based on localGrid's position in space which is not what we want.
        //                // If I comment out the Vector3d.transform then we can at least get a decent view of the ship.... but it rotates as we rotate, not ideal 

        //                Vector3D blockWorldRelativePosition = BT.WorldRelativePosition; // Get the blocks relative world position
        //                Vector3D localGridWorldPosition = gHandler.localGridEntity.WorldMatrix.Translation;
        //                Vector3D targetGridWorldPosition = targetGrid.WorldMatrix.Translation;

        //                Vector3D worldSpaceOffset = targetGridWorldPosition - localGridWorldPosition;
        //                //Vector3D adjustedPosition = blockWorldRelativePosition + worldSpaceOffset; // This is kinda close... maybe? It's odd.


        //                double HealthPercent = ClampedD(BT.HealthCurrent / BT.HealthMax, 0, 1);
        //                HG_DrawBillboardTarget(blockWorldRelativePosition, targetGrid, HealthPercent);
        //            }
        //        }


        //        // Alright, let's roll our own and see what the F is going on here, talk about "out of your depth" holy moly here I am:
        //        double hologramScale = 0.0075; // HG_Scale?
        //        double hologramScaleFactor = 10; // HG_ScaleFactor?

        //        VRage.Game.ModAPI.IMyCubeGrid gridA = playerGrid; // Observer grid, position matters rotation does not
        //        VRage.Game.ModAPI.IMyCubeGrid gridB = targetGrid; // Observed grid, position and rotation matters.

        //        Vector3D observerPosition = gridA.GetPosition();
        //        Vector3D gridPosition = gridB.GetPosition();
        //        Vector3D gridCenter = gridB.WorldVolume.Center;
        //        MatrixD gridWorldRotationMatrix = GetRotationMatrix(gridB.WorldMatrix);
        //        MatrixD gridWorldRotationMatrixInverse = MatrixD.Invert(gridWorldRotationMatrix);

        //        // Calculate our distance for scaling by distance
        //        double fixedDistance = 100.0;
        //        double actualDistance = Vector3D.Distance(gridPosition, observerPosition);
        //        double scale = fixedDistance / actualDistance;

        //        // Calculate our gridSize for scaling by size of grid

        //        List<VRage.Game.ModAPI.IMySlimBlock> blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
        //        targetGrid.GetBlocks(blocks);
        //        double flipAxisForTarget = -1; // An inversion factor so it draws on the left holographic display
        //        foreach (VRage.Game.ModAPI.IMySlimBlock block in blocks)
        //        {
        //            // Let's get three types of positions, world, grid relative, and grid center relative.
        //            Vector3D blockPositionRelativeToGrid = block.Position; // This is the grid relative position
        //            Vector3D blockPositionInWorld;
        //            block.ComputeWorldCenter(out blockPositionInWorld); // This is the world position of the block's center
        //            Vector3D blockPositionRelativeToGridCenter = blockPositionInWorld - gridCenter;

        //            // Let's create three scaled positions by gridSize.
        //            Vector3D blockScaledPositionInWorld = blockPositionInWorld / gridB.GridSize;
        //            Vector3D blockScaledPositionRelativeToGrid = blockPositionRelativeToGrid / gridB.GridSize;
        //            Vector3D blockScaledPositionRelativeToGridCenter = blockPositionRelativeToGridCenter / gridB.GridSize;

        //            // Get health of block
        //            double HealthPercent = ClampedD(block.Integrity / block.MaxIntegrity, 0, 1);

        //            // Now to draw block on screen
        //            if (HealthPercent < 0.01)
        //            {
        //                continue; // Do not draw destroyed blocks. 
        //            }

        //            // Color to use
        //            Vector4 color = LINECOLOR_Comp * 0.5f;
        //            color.W = 1;

        //            // Calculate an offset transformation so it draws on the left or right holographic display based on settings, flipAxisForTarget = -1 causes it to draw on the left
        //            Vector3D HG_Offset_transformation = radarMatrix.Left * -radarRadius * flipAxisForTarget + radarMatrix.Left * HG_Offset.X * flipAxisForTarget + radarMatrix.Up * HG_Offset.Y + radarMatrix.Forward * HG_Offset.Z;
        //            double thickness = hologramScaleFactor / (gridB.WorldVolume.Radius / gridB.GridSize);
        //            MatrixD scalingMatrix = MatrixD.CreateScale(hologramScale * thickness);
        //            float hologramSize = (float)hologramScale * 0.65f * (float)thickness;

        //            Vector3D blockPositionToTransform = blockScaledPositionRelativeToGridCenter;
        //            switch (HologramBlockReferenceToggle)
        //            {
        //                case 0:
        //                    blockPositionToTransform = blockScaledPositionRelativeToGridCenter;
        //                    break;
        //                case 1:
        //                    blockPositionToTransform = blockScaledPositionRelativeToGrid;
        //                    break;
        //                case 2:
        //                    blockPositionToTransform = blockScaledPositionInWorld;
        //                    break;
        //                case 3:
        //                    blockPositionToTransform = blockPositionRelativeToGridCenter;
        //                    break;
        //                case 4:
        //                    blockPositionToTransform = blockPositionRelativeToGrid;
        //                    break;
        //                case 5:
        //                    blockPositionToTransform = blockPositionInWorld;
        //                    break;
        //                default:
        //                    blockPositionToTransform = blockScaledPositionRelativeToGridCenter;
        //                    break;
        //            }
        //            // Can use blockScaledPositionInWorld, blockScaledPositionRelativeToGrid, or blockScaledPositionRelativeToGridCenter. Which gives me what I want?
        //            Vector3D blockPositionToDraw = blockPositionToTransform;
        //            switch (HologramBlockScalingToggle)
        //            {
        //                case 0:
        //                    blockPositionToDraw = Vector3D.Transform(blockPositionToTransform, scalingMatrix);
        //                    break;
        //                case 1:
        //                    blockPositionToDraw = blockPositionToTransform;
        //                    break;
        //                default:
        //                    blockPositionToDraw = Vector3D.Transform(blockPositionToTransform, scalingMatrix);
        //                    break;
        //            }






        //            MatrixD worldMatrixA = gridA.WorldMatrix;
        //            MatrixD worldMatrixB = gridB.WorldMatrix;

        //            // Remove translation — we only care about orientation
        //            worldMatrixA.Translation = Vector3D.Zero;
        //            worldMatrixB.Translation = Vector3D.Zero;

        //            // Invert B's rotation to get world → B local space
        //            MatrixD worldToB = MatrixD.Invert(worldMatrixB);

        //            // Multiply: A's world rotation in B's local space
        //            MatrixD relativeRotation = worldMatrixA * worldToB;
        //            switch (HologramCoordSpaceToggle)
        //            {
        //                case 0:
        //                    blockPositionToDraw = blockPositionToDraw;
        //                    break;
        //                case 1:
        //                    Vector3D gridAPos = gridA.GetPosition();
        //                    MatrixD gridBWorldMatrix = gridB.WorldMatrix;
        //                    MatrixD gridBWorldMatrixInv = MatrixD.Invert(gridBWorldMatrix);
        //                    Vector3D gridAPositionInBLocalSpace = Vector3D.Transform(gridAPos, gridBWorldMatrixInv);

        //                    blockPositionToDraw = gridAPositionInBLocalSpace;
        //                    break;
        //                case 2:
        //                    Vector3D directionWorld = Vector3D.Normalize(gridA.GetPosition() - gridB.GetPosition());
        //                    blockPositionToDraw = directionWorld;
        //                    break;
        //                case 3:
        //                    blockPositionToDraw = Vector3D.Rotate(blockPositionToDraw, relativeRotation);
        //                    break;
        //                case 4:
        //                    blockPositionToDraw = Vector3D.TransformNormal(blockPositionToDraw, relativeRotation);
        //                    break;
        //                case 5:
        //                    MatrixD rotationOnlyGridAMatrix = gridA.WorldMatrix;
        //                    rotationOnlyGridAMatrix.Translation = Vector3D.Zero;
        //                    blockPositionToDraw = Vector3D.Transform(blockPositionToDraw, rotationOnlyGridAMatrix);
        //                    break;
        //                default:
        //                    blockPositionToDraw = blockPositionToDraw;
        //                    break;
        //            }



        //            blockPositionToDraw = blockPositionToDraw + (worldRadarPos + HG_Offset_transformation); // worldRadarPos is global, user can shift around where on screen it draws. 

        //            // Draw on screen.
        //            MyTransparentGeometry.AddBillboardOriented(
        //                MaterialSquare,
        //                color,
        //                blockPositionToDraw,
        //                MyAPIGateway.Session.Camera.WorldMatrix.Left, // Orient billboard drawn toward camera so we can see the square. 
        //                MyAPIGateway.Session.Camera.WorldMatrix.Up,
        //                hologramSize,
        //                MyBillboard.BlendTypeEnum.AdditiveTop);
        //        }
        //    }
        //}




        //Vector3D blockWorldRelativePosition = BT.WorldRelativePosition; // Get the blocks relative world position
        //Vector3D localGridWorldPosition = gHandler.localGridEntity.WorldMatrix.Translation;
        //Vector3D targetGridWorldPosition = targetGrid.WorldMatrix.Translation;

        //MatrixD localGridRotationalMatrix = gHandler.localGridEntity.WorldMatrix;
        //localGridRotationalMatrix.Translation = Vector3D.Zero; // Get a rotation only matrix for the local grid.
        //            //MatrixD inverseLocalRotation = MatrixD.Invert(localGridRotationalMatrix);
        //            Vector3D orientationNeutralPosition = Vector3D.Transform(blockWorldRelativePosition, localGridRotationalMatrix);

        //// Calculate the direction from local to target
        //Vector3D directionToTarget = Vector3D.Normalize(targetGridWorldPosition - localGridWorldPosition);

        //// Create a rotation matrix that orients towards the target.
        //MatrixD orientationMatrix = MatrixD.CreateFromDir(directionToTarget);

        //Vector3D rotatedPosition = Vector3D.Transform(orientationNeutralPosition, orientationMatrix);

        //double HealthPercent = ClampedD(BT.HealthCurrent / BT.HealthMax, 0, 1);
        //HG_DrawBillboardTarget(rotatedPosition, targetGrid, HealthPercent);
        //// The below was close but not quite




        private List<Vector3D> HG_ScalePositions(List<Vector3D> positions, MatrixD scalingMatrix)
		{
			var scaledPositions = new List<Vector3D>();

			foreach (var position in positions)
			{
				var scaledPosition = Vector3D.Transform(position, scalingMatrix);
				//scaledPosition = Vector3D.Transform (scaledPosition, radarMatrix);
				scaledPositions.Add(scaledPosition);
			}

			return scaledPositions;
		}




        private void HG_DrawBillboardTarget_OLD(Vector3D position, VRage.Game.ModAPI.IMyCubeGrid grid, double HP = 1)
        {
            if (HP < 0.01)
            {
                return; // Do not draw destroyed blocks. 
            }
            bool randoTime = false;
            if (GetRandomFloat() > 0.95f || glitchAmount > 0.5)
            {
                randoTime = true; // If we are at high power draw and the glitchEffect should be on then we set randoTime to true so further effects can be altered by it.
            }

            double bootUpAlpha = 1;
            bootUpAlpha = HG_activationTimeTarget; // This is the activation time for the target grid hologram. 

            bootUpAlpha = ClampedD(bootUpAlpha, 0, 1); // Clamp bootUpAlpha to be between 0 and 1. 
            bootUpAlpha = Math.Pow(bootUpAlpha, 0.25); // bootUpAlpha^(1/4)?

            if (GetRandomDouble() > bootUpAlpha)
            {
                position *= bootUpAlpha; // I assume this is what gives the hologram the booting up effect, it scales the position of blocks drawn by the bootUpAlpha.
                                         // But only randomly when the double is >, so as bootUpAlpha approaches 1, the hologram will be drawn at full position.
            }
            if (GetRandomDouble() > bootUpAlpha)
            {
                randoTime = true; // If we haven't yet booted up then we also apply the randomEffect to the hologram.
                                  // So the same glitch effect is also applied at boot up BUT it's also scaled during boot up, not during just a glitch.
            }

            // Here's where FenixPK thinks the problem lies, drawing this from the camera perspective instead of from the grid's perspective.
            // I want the target hologram to represent where it is and how it is facing relative to the local grid. Not the camera.
            var camera = MyAPIGateway.Session.Camera;
            Vector3D AxisLeft = camera.WorldMatrix.Left;
            Vector3D AxisUp = camera.WorldMatrix.Up;
            Vector3D AxisForward = camera.WorldMatrix.Forward;

            // Okay in theory, I should be able to use the localGrid's worldMatrix instead to get the perspective I would see if I looked at it from the localGrid. 
            // Ie. use the grid as the frame of reference not the camera.


            Vector3D billDir = Vector3D.Normalize(position);
            double dotProd = 1 - (Vector3D.Dot(position, AxisForward) + 1) / 2; // With the change above this dotProduct should now calculate how much the target block is facing toward or away from the local grid's forward direction. That might not be exactly it.. but for now.
            dotProd = RemapD(dotProd, -0.5, 1, 0.25, 1);
            dotProd = ClampedD(dotProd, 0.25, 1);

            var color = LINECOLOR_Comp * 0.5f; // This is the color of the hologram for the target. I assume comp means competition, as the alternative is the line color from the settings for local grid drawing?
            color.W = 1;
            if (randoTime)
            {
                color *= Clamped(GetRandomFloat(), 0.25f, 1); // If the effect is being applied then we randomize the color brightness between 0.25 and 1.
            }

            Vector4 cRed = new Vector4(1, 0, 0, 1); // Colors for damanged blocks.
            Vector4 cYel = new Vector4(1, 1, 0, 1); // Colors for damaged blocks. Red is more damaged than yellow.

            // Interesting, we split this into two ranges. White color that LerpFs to yellow. Then yellow color that LerpFs to red. Essentially two ranges of color, White->Yellow and Yellow->Red. Neat.
            if (HP > 0.5)
            {
                HP -= 0.5;
                HP *= 2;
                color.X = LerpF(cYel.X, color.X, (float)HP);
                color.Y = LerpF(cYel.Y, color.Y, (float)HP);
                color.Z = LerpF(cYel.Z, color.Z, (float)HP);
                color.W = LerpF(cYel.W, color.W, (float)HP);
            }
            else
            {
                HP *= 2;
                color.X = LerpF(cRed.X, cYel.X, (float)HP);
                color.Y = LerpF(cRed.Y, cYel.Y, (float)HP);
                color.Z = LerpF(cRed.Z, cYel.Z, (float)HP);
                color.W = LerpF(cRed.W, cYel.W, (float)HP);
            }

            double thicc = HG_scaleFactor / (grid.WorldVolume.Radius / grid.GridSize); // I can only assume thicc is thickness, it appears relative to the grids bounding box radius in meters over its size in meters. 
            var size = (float)HG_Scale * 0.65f * (float)thicc;//*grid.GridSize;
            var material = MaterialSquare; // A square to represent a block, makes sense.

            double flipAxisForTarget = -1; // A lot of this logic relied on flippit to render to left vs right holo display...
                                           // But your grid must be viewed from the camera, and target grid must be viewed from the perspective of your grid vs target grid.
                                           // We could repurpose this so a player could choose which hologram appears on which side if they prefer target on the right for eg. 

            //double gridThicc = grid.WorldVolume.Radius; // Grid thickness is not used.
            Vector3D HG_Offset_tran = radarMatrix.Left * -radarRadius * flipAxisForTarget + radarMatrix.Left * _hologramRightOffset_HardCode.X * flipAxisForTarget + radarMatrix.Up * _hologramRightOffset_HardCode.Y + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;

            if (randoTime)
            {
                // Offset the position by a random amount to give a "glitch" effect, also used for booting up. 
                Vector3D randOffset = new Vector3D((GetRandomDouble() - 0.5) * 2, (GetRandomDouble() - 0.5) * 2, (GetRandomDouble() - 0.5) * 2);
                randOffset *= 0.333;
                position += position * randOffset;
            }

            // Not sure what this does exactly. We transform the position by the scaling matrix, I assume it scales it to fit on the radar but it depends how HG_scalingMatrixTarget is generated. 
            position = Vector3D.Transform(position, HG_scalingMatrixTarget);
            position += worldRadarPos + HG_Offset_tran; // We add the world radar position and then the offset to the position (defined in the block Custom Data where you shift the thing around). 
                                                        // In theory we can add offsets to each component instead of just one global offset. That'd be cool and I think players requested it. 


            //double dis2Cam = Vector3.Distance(camera.Position, position); // This isn't used

            MyTransparentGeometry.AddBillboardOriented(
                material,
                color * (float)dotProd * (float)bootUpAlpha,
                position,
                AxisLeft, // Billboard orientation
                AxisUp, // Billboard orientation
                size,
                MyBillboard.BlendTypeEnum.AdditiveTop);

            if (GetRandomFloat() > 0.9f)
            {
                // Aha so this is the shimmery holographic effect under it.
                Vector3D holoCenter = radarMatrix.Left * -radarRadius * flipAxisForTarget + radarMatrix.Left * _hologramRightOffset_HardCode.X * flipAxisForTarget + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
                holoCenter += worldRadarPos;
                Vector3D holoDir = Vector3D.Normalize(position - holoCenter);
                double holoLength = Vector3D.Distance(holoCenter, position);

                DrawLineBillboard(MaterialSquare, color * 0.15f * (float)dotProd * (float)bootUpAlpha, holoCenter, holoDir, (float)holoLength, 0.0025f, BlendTypeEnum.AdditiveTop);
            }
        }



        //private void HG_DrawBillboardTarget(Vector3D position, VRage.Game.ModAPI.IMyCubeGrid grid, double HP = 1)
        //{
        //    if (HP < 0.01)
        //    {
        //        return; // Do not draw destroyed blocks. 
        //    }
        //    bool randoTime = false;
        //    if (GetRandomFloat() > 0.95f || glitchAmount > 0.5)
        //    {
        //        randoTime = true; // If we are at high power draw and the glitchEffect should be on then we set randoTime to true so further effects can be altered by it.
        //    }

        //    double bootUpAlpha = 1;
        //    bootUpAlpha = HG_activationTimeTarget; // This is the activation time for the target grid hologram. 

        //    bootUpAlpha = ClampedD(bootUpAlpha, 0, 1); // Clamp bootUpAlpha to be between 0 and 1. 
        //    bootUpAlpha = Math.Pow(bootUpAlpha, 0.25); // bootUpAlpha^(1/4)?

        //    if (GetRandomDouble() > bootUpAlpha)
        //    {
        //        position *= bootUpAlpha; // I assume this is what gives the hologram the booting up effect, it scales the position of blocks drawn by the bootUpAlpha.
        //                                 // But only randomly when the double is >, so as bootUpAlpha approaches 1, the hologram will be drawn at full position.
        //    }
        //    if (GetRandomDouble() > bootUpAlpha)
        //    {
        //        randoTime = true; // If we haven't yet booted up then we also apply the randomEffect to the hologram.
        //                          // So the same glitch effect is also applied at boot up BUT it's also scaled during boot up, not during just a glitch.
        //    }

        //    // Here's where FenixPK thinks the problem lies, drawing this from the camera perspective instead of from the grid's perspective.
        //    // I want the target hologram to represent where it is and how it is facing relative to the local grid. Not the camera.
        //    var camera = MyAPIGateway.Session.Camera;
        //    Vector3D AxisLeft = camera.WorldMatrix.Left;
        //    Vector3D AxisUp = camera.WorldMatrix.Up;
        //    Vector3D AxisForward = camera.WorldMatrix.Forward;

        //    // Okay in theory, I should be able to use the localGrid's worldMatrix instead to get the perspective I would see if I looked at it from the localGrid. 
        //    // Ie. use the grid as the frame of reference not the camera.

        //    MatrixD localGridMatrix = gHandler.localGridEntity.WorldMatrix;
        //    AxisLeft = localGridMatrix.Left;
        //    AxisUp = localGridMatrix.Up;
        //    AxisForward = localGridMatrix.Forward;

        //    // Yeah on second thought the above gets us the same result in most cases as the cockpit is facing the same way as the camera..... Huhhhhhhh...
        //    Vector3D observerPosition = gHandler.localGridEntity.GetPosition();
        //    Vector3D toObserver = Vector3D.Normalize(observerPosition - position); // This is the direction vector calculated between the position (which is a block we are drawing) and the observer (which is the local grid). 
        //    Vector3D gridForward = grid.WorldMatrix.Forward; // This may not work as we want the block not the grid... but honestly since these are just squares on a hologram it doesn't matter much xD, if we start drawing complex shapes then we will need to change this.

        //    double dotProd = Vector3D.Dot(toObserver, gridForward); // This calculates the dot product between the direction to the observer and the block's forward direction.
        //    dotProd = 1 - (dotProd + 1) / 2; //  This remaps the dot product to a range of 0 to 1, where 0 is facing away and 1 is facing toward the observer? 


        //    Vector3D billDir = Vector3D.Normalize(position);
        //    //double dotProd = 1 - (Vector3D.Dot(position, AxisForward) + 1)/2; // With the change above this dotProduct should now calculate how much the target block is facing toward or away from the local grid's forward direction. That might not be exactly it.. but for now.
        //    dotProd = RemapD(dotProd, -0.5, 1, 0.25, 1);
        //    dotProd = ClampedD(dotProd, 0.25, 1);

        //    var color = LINECOLOR_Comp * 0.5f; // This is the color of the hologram for the target. I assume comp means competition, as the alternative is the line color from the settings for local grid drawing?
        //    color.W = 1;
        //    if (randoTime)
        //    {
        //        color *= Clamped(GetRandomFloat(), 0.25f, 1); // If the effect is being applied then we randomize the color brightness between 0.25 and 1.
        //    }

        //    Vector4 cRed = new Vector4(1, 0, 0, 1); // Colors for damanged blocks.
        //    Vector4 cYel = new Vector4(1, 1, 0, 1); // Colors for damaged blocks. Red is more damaged than yellow.

        //    // Interesting, we split this into two ranges. White color that LerpFs to yellow. Then yellow color that LerpFs to red. Essentially two ranges of color, White->Yellow and Yellow->Red. Neat.
        //    if (HP > 0.5)
        //    {
        //        HP -= 0.5;
        //        HP *= 2;
        //        color.X = LerpF(cYel.X, color.X, (float)HP);
        //        color.Y = LerpF(cYel.Y, color.Y, (float)HP);
        //        color.Z = LerpF(cYel.Z, color.Z, (float)HP);
        //        color.W = LerpF(cYel.W, color.W, (float)HP);
        //    }
        //    else
        //    {
        //        HP *= 2;
        //        color.X = LerpF(cRed.X, cYel.X, (float)HP);
        //        color.Y = LerpF(cRed.Y, cYel.Y, (float)HP);
        //        color.Z = LerpF(cRed.Z, cYel.Z, (float)HP);
        //        color.W = LerpF(cRed.W, cYel.W, (float)HP);
        //    }

        //    double thicc = HG_scaleFactor / (grid.WorldVolume.Radius / grid.GridSize); // I can only assume thicc is thickness, it appears relative to the grids bounding box radius in meters over its size in meters. 
        //    var size = (float)HG_Scale * 0.65f * (float)thicc;//*grid.GridSize;
        //    var material = MaterialSquare; // A square to represent a block, makes sense.

        //    double flipAxisForTarget = -1; // A lot of this logic relied on flippit to render to left vs right holo display...
        //                                   // But your grid must be viewed from the camera, and target grid must be viewed from the perspective of your grid vs target grid.
        //                                   // We could repurpose this so a player could choose which hologram appears on which side if they prefer target on the right for eg. 

        //    //double gridThicc = grid.WorldVolume.Radius; // Grid thickness is not used.
        //    Vector3D HG_Offset_tran = radarMatrix.Left * -radarRadius * flipAxisForTarget + radarMatrix.Left * HG_Offset.X * flipAxisForTarget + radarMatrix.Up * HG_Offset.Y + radarMatrix.Forward * HG_Offset.Z;

        //    if (randoTime)
        //    {
        //        // Offset the position by a random amount to give a "glitch" effect, also used for booting up. 
        //        Vector3D randOffset = new Vector3D((GetRandomDouble() - 0.5) * 2, (GetRandomDouble() - 0.5) * 2, (GetRandomDouble() - 0.5) * 2);
        //        randOffset *= 0.333;
        //        position += position * randOffset;
        //    }

        //    // Not sure what this does exactly. We transform the position by the scaling matrix, I assume it scales it to fit on the radar but it depends how HG_scalingMatrixTarget is generated. 
        //    position = Vector3D.Transform(position, HG_scalingMatrixTarget);
        //    position += worldRadarPos + HG_Offset_tran; // We add the world radar position and then the offset to the position (defined in the block Custom Data where you shift the thing around). 
        //                                                // In theory we can add offsets to each component instead of just one global offset. That'd be cool and I think players requested it. 


        //    //double dis2Cam = Vector3.Distance(camera.Position, position); // This isn't used

        //    MyTransparentGeometry.AddBillboardOriented(
        //        material,
        //        color * (float)dotProd * (float)bootUpAlpha,
        //        position,
        //        AxisLeft, // Billboard orientation
        //        AxisUp, // Billboard orientation
        //        size,
        //        MyBillboard.BlendTypeEnum.AdditiveTop);

        //    if (GetRandomFloat() > 0.9f)
        //    {
        //        // Aha so this is the shimmery holographic effect under it.
        //        Vector3D holoCenter = radarMatrix.Left * -radarRadius * flipAxisForTarget + radarMatrix.Left * HG_Offset.X * flipAxisForTarget + radarMatrix.Forward * HG_Offset.Z;
        //        holoCenter += worldRadarPos;
        //        Vector3D holoDir = Vector3D.Normalize(position - holoCenter);
        //        double holoLength = Vector3D.Distance(holoCenter, position);

        //        DrawLineBillboard(MaterialSquare, color * 0.15f * (float)dotProd * (float)bootUpAlpha, holoCenter, holoDir, (float)holoLength, 0.0025f, BlendTypeEnum.AdditiveTop);
        //    }
        //}


        private MatrixD CreateNormalizedLocalGridRotationMatrix()
		{
            Sandbox.ModAPI.IMyCockpit cockpit = gHandler.localGridControlledEntity as Sandbox.ModAPI.IMyCockpit;
			
            if (cockpit == null)
            {
                return MatrixD.Zero; // Return Zero matrix if no cockpit is found
            }
            VRage.Game.ModAPI.IMyCubeGrid grid = cockpit.CubeGrid;
            if (grid == null)
            {
                return MatrixD.Zero; // Return Zero matrix if no grid is found
            }

            //Vector3D angularVelocity = gHandler.localGridVelocityAngular; // Gets the angular velocity of the entity the player is currently controlling
            // gHandler.localGridVelocityAngular always gets the controlled BLOCK's angular velocity, not the grid.

			var gridEntity = grid as VRage.ModAPI.IMyEntity;
			Vector3D angularVelocity = gridEntity?.Physics?.AngularVelocity ?? Vector3D.Zero;
            MatrixD cockpitMatrix = cockpit.WorldMatrix; // Get cockpit orientation
            MatrixD gridMatrix = cockpit.CubeGrid.WorldMatrix; // Get grid orientation

            // Transform the angular velocity from world space to grid space. This makes it relative to the grid instead of the world plane so it doesn't matter which way
			// the grid is currently facing.
            MatrixD worldToGrid = MatrixD.Invert(grid.WorldMatrix);
            Vector3D gridAngularVelocity = Vector3D.TransformNormal(angularVelocity, worldToGrid);

            // Configuration values - adjust these to tune the effect
            const double maxAngularVelocity = 6; // Maximum angular velocity in your units, as in X m/s angular velocity = maxTiltAngle.
			// For eg. if we have 6 m/s max and a maxTilt of 89 radians then at 6 m/s (or higher) the hologram will be tiled 89 degrees in the direction of the angular velocity.

            const double maxTiltAngle = 89.0 * Math.PI / 180.0; // 89 degrees in radians
            const double sensitivityMultiplier = 1.0; // Adjust this to make the effect more or less pronounced

            // Calculate rotation angles based on angular velocity
            // Scale each component to the desired range and clamp to prevent flipping
            double rotationX = ClampAndScale(gridAngularVelocity.X, maxAngularVelocity, maxTiltAngle) * sensitivityMultiplier;
            double rotationY = ClampAndScale(gridAngularVelocity.Y, maxAngularVelocity, maxTiltAngle) * sensitivityMultiplier;
            double rotationZ = ClampAndScale(gridAngularVelocity.Z, maxAngularVelocity, maxTiltAngle) * sensitivityMultiplier;

            // Create a rotation matrix purely from the angular velocity components
            // This creates a rotation around an arbitrary axis defined by the velocity vector
            Vector3D rotationAxis = new Vector3D(rotationX, rotationY, rotationZ);
            double rotationAngle = rotationAxis.Length();

            if (rotationAngle > 0.001) // Avoid division by zero
            {
                // Normalize the axis
                rotationAxis = rotationAxis / rotationAngle;

                // Create rotation matrix around the arbitrary axis
                MatrixD localRotationMatrix = MatrixD.CreateFromAxisAngle(rotationAxis, rotationAngle);
                return localRotationMatrix;
            }
            else
            {
                // No rotation needed
                return MatrixD.Identity;
            }
        }

        private MatrixD CreateNormalizedTargetGridRotationMatrix(VRage.ModAPI.IMyEntity entity)
        {
            if (entity == null)
            {
                return MatrixD.Zero; // Return Zero matrix if no cockpit is found
            }
            VRage.Game.ModAPI.IMyCubeGrid grid = entity as VRage.Game.ModAPI.IMyCubeGrid;
            if (grid == null)
            {
                return MatrixD.Zero; // Return Zero matrix if no grid is found
            }

            //Vector3D angularVelocity = gHandler.localGridVelocityAngular; // Gets the angular velocity of the entity the player is currently controlling
            // gHandler.localGridVelocityAngular always gets the controlled BLOCK's angular velocity, not the grid.

            var gridEntity = grid as VRage.ModAPI.IMyEntity;
            Vector3D angularVelocity = gridEntity?.Physics?.AngularVelocity ?? Vector3D.Zero;
            MatrixD gridMatrix = grid.WorldMatrix; // Get grid

            // Transform the angular velocity from world space to grid space. This makes it relative to the grid instead of the world plane so it doesn't matter which way
            // the grid is currently facing.
            MatrixD worldToGrid = MatrixD.Invert(grid.WorldMatrix);
            Vector3D gridAngularVelocity = Vector3D.TransformNormal(angularVelocity, worldToGrid);

            // Configuration values - adjust these to tune the effect
            const double maxAngularVelocity = 6; // Maximum angular velocity in your units, as in X m/s angular velocity = maxTiltAngle.
                                                 // For eg. if we have 6 m/s max and a maxTilt of 89 radians then at 6 m/s (or higher) the hologram will be tiled 89 degrees in the direction of the angular velocity.

            const double maxTiltAngle = 89.0 * Math.PI / 180.0; // 89 degrees in radians
            const double sensitivityMultiplier = 1.0; // Adjust this to make the effect more or less pronounced

            // Calculate rotation angles based on angular velocity
            // Scale each component to the desired range and clamp to prevent flipping
            double rotationX = ClampAndScale(gridAngularVelocity.X, maxAngularVelocity, maxTiltAngle) * sensitivityMultiplier;
            double rotationY = ClampAndScale(gridAngularVelocity.Y, maxAngularVelocity, maxTiltAngle) * sensitivityMultiplier;
            double rotationZ = ClampAndScale(gridAngularVelocity.Z, maxAngularVelocity, maxTiltAngle) * sensitivityMultiplier;

            // Create a rotation matrix purely from the angular velocity components
            // This creates a rotation around an arbitrary axis defined by the velocity vector
            Vector3D rotationAxis = new Vector3D(rotationX, rotationY, rotationZ);
            double rotationAngle = rotationAxis.Length();

            if (rotationAngle > 0.001) // Avoid division by zero
            {
                // Normalize the axis
                rotationAxis = rotationAxis / rotationAngle;

                // Create rotation matrix around the arbitrary axis
                MatrixD targetRotationMatrix = MatrixD.CreateFromAxisAngle(rotationAxis, rotationAngle);
                return targetRotationMatrix;
            }
            else
            {
                // No rotation needed
                return MatrixD.Identity;
            }
        }

        // Helper function to scale and clamp angular velocity to rotation angle
        private double ClampAndScale(double angularVelocity, double maxVelocity, double maxAngle)
        {
            // Normalize the angular velocity to (-1, 1) range
            double normalizedVelocity = angularVelocity / maxVelocity;

            // Clamp to prevent values outside (-1, 1)
            normalizedVelocity = Math.Max(-1.0, Math.Min(1.0, normalizedVelocity));

            // Scale to the desired angle range
            return normalizedVelocity * maxAngle;
        }




        /// <summary>
        /// This function creates a rotation matrix based on the angular velocity of the grid. Optional amplification factor can be used to increase the effect of the rotation. Use without amplification factor to get a rotation matrix that represents the
        /// actual rotation of a grid in the last deltaTime period. Apply an amplification factor to make the rotation more pronounced, which is useful for visual effects like wobbling holograms based on angular velocity where the position re-sets afterword and isn't permanently
        /// transformed by this function. I think anyway... -FenixPK 2025-07-16
        /// </summary>
        /// <param name="angularVelocity"></param>
        /// <param name="amplificationFactor"></param>
        /// <returns></returns>
        private MatrixD CreateAngularRotationMatrix(Vector3D angularVelocity, float amplificationFactor = 10f)
		{
            // This appears to be genearating a rotation matrix based on the rotation that occured during the last deltaTime period.
            // Baiscally if a grid is rotating at 0.5 radians per second around the Y-axis and detlaTime is 0.1 seconds then 
            // rotationAngle.Y = 0.5 * 0.1 = 0.05 radians
            // Therefore the matrix product would represent a 0.05 radian rotation around the Y-axis
            // We have Vector3D angularVelocity which is the angular velocity of the grid in radians per second around each axis.
            // So we can generate a rotation matrix that represents the a radian amount rotation around each axis based on the angular velocity and the deltaTime.
            // What fun. 
            // Additionally now that I know what it does and re-enabled it for the local grid I've observed that as I pivot up-down it rotates the hologram left/right, and vice versa left-right rotates the hologram up/down. 
            // Roll seems fine. I wonder if this is a universal issue or just due to ship design? Ie a grid has xyz facings and you could put the cockpit facing forward on the x or y or z axis and that becomes "Forward" for the grid.
            // so at any time you can have grids where forward is x, forward is y, or forward is z. 
            // Further this is space engineers so you could have a cube that has multiple control seats, each facing x, -x, y, -y, z, -z so what really is the "front" of the grid?
            // Assuming we use this function at all going forward we could make assumptions about the ships grid facing by which axis the currently controlled cockpit is facing.
            // For ships with multiple seats and each player could have an EliDang's hud active we would want to use other indicators like if there are fixed weapons facing a certain direction. 
            // Or hell we could add a tag called [ELI_FRONT] to a block and use the first block with that tag's forward face as the "front" of the grid (in case they tag more than one we don't want an exception).
			// Eg. a remote control, control, even ai blocks have "front" faces. Any block that shows in console could be used but some have more obvious fronts than others. 
			// Or we could use custom data on the control block itself. So we can tell it waht side we want to be treated as the front of the grid without needing a block tag... I like that more. 
			// aye karumba. 

            // NOTE ON amplificationFactor
            // Amplifies the angular velocity to affect a rotation matrix in a way that pronounces the rotation effect based on how fast we are rotating.
            // If rotational angular velocity is low, then the rotation matrix will be small and the hologram will wobble less.
            // If rotational angular velocity is high, then the rotation matrix will be large and the hologram will wobble more.
            // If used to directly rotate something it would cause it to rotate more than it actually did. So I wouldn't use this to take a fixed view of a grid
            // and say "lets rotate it based on how much it rotated in the last deltaTime period" because it would amplify and you'd end up with the visual depiction
            // rotating much faster than the actual grid...
            // But since the way this function is used is to rotate a hologram based on your angularVelocity while you have angularVelocity and then it re-centers after it has the effect of 
            // making it wobble more or less based on how fast you are rotating.
            // Mind melting. I dislike magic numbers. 

            // Create the rotation angle vector (angular velocity * deltaTime)
            Vector3D rotationAngle = angularVelocity * deltaTimeSinceLastTick * amplificationFactor; // the * 10 here is likely a scaling factor? I'm honestly not sure? FenixPK replaced a magic number of "10" with a variable so this makes sense. 
			// Removing * 10 made no visible difference to me, however setting it to 100 made it obvious what it did.
			// As of current original author code where target hologram takes LOCAL GRID angular velicity and passes it to this function and transforms positionR by applying this
			// matrix. If I pass in 100, it is obvious the target hologram block positions get rotated/wobbled a bit based on my current angular velocity before resetting back to original view...
			// That original view is our positions relative to where we are facing IN THE WORLD not relative to each other.
			// Eg if we are both facing forward (but I'm BEHIND THEM) and I rotate my ship to the left I am now seeing their right side as if I were looking at their right side. But I'm not.. they aren't even in front of me anymore.
			// None of this makes sense to me other than as a way to view the target grid from different angles by rotating my ship on the spot... But that's only combined with the existing HG_DrawBillboard.
			// This function alone just seems to "wobble" it a bit more in either axis to depict angular velocity. Ie. if moving quickly it wobbles more off the "regular" position, if moving slowly it wobbles less.
			// That kinda makes sense for the local grid if we are fixing the view of it, it gives us feedback. But for the target... I don't think it makes sense.
			// And the fact it shows the side of the target based on our WORLD facing, instead of relative to the grid makes no sense either.
			// If I am behind a grid, and I rotate in any direction (without changing my x,y,z coordinate just rotation) I should still be seeing the back of the grid the whole time. That's the side facing my grid.
			// I suspect it is using the camera direction, somewhere, somehow. This is melting my brain. 2025-07-16 FenixPK.

			// Create the rotation matrices for each axis
			MatrixD rotationX = MatrixD.CreateRotationX(-rotationAngle.X);
			MatrixD rotationY = MatrixD.CreateRotationY(-rotationAngle.Y);
			MatrixD rotationZ = MatrixD.CreateRotationZ(-rotationAngle.Z);

			// Combine the rotations. Order of multiplication matters.
			// Here we assume the rotation order is ZYX.
			MatrixD rotationMatrix = rotationZ * rotationY * rotationX;

			return rotationMatrix;
		}

		private MatrixD ApplyRotation(MatrixD originalMatrix, MatrixD rotationMatrix)
		{
			return originalMatrix * rotationMatrix;
		}
		//------------------------------------------------------------------------------------------------------------














		//==================MONEY=====================================================================================
		private double creditBalance = 0;
		private double creditBalance_fake = 0;
		private double creditBalance_dif = 0;
		private bool credit_counting = false;

        public string FormatCredits(double value)
        {
            // Billions, Millions, Thousands, or Exact. 
            var culture = System.Globalization.CultureInfo.InvariantCulture; // Invariant should use decimal, but no commas. 
            if (value >= 1000000000)
				return (value / 1000000000.0).ToString("0.##", culture) + " B";
			else if (value >= 1000000)
				return (value / 1000000.0).ToString("0.##", culture) + " M";
			else if (value >= 100000)
				return (value / 1000.0).ToString("0.##", culture) + " K";
			else
				return value.ToString("0", culture); 
        }

        public void UpdateCredits()
        {
            // Example usage: Get credits for the local player
            if (MyAPIGateway.Session?.Player != null)
            {
                IMyPlayer localPlayer = MyAPIGateway.Session.Player;
                string balance = Convert.ToString(GetPlayerCredits(localPlayer));
                double balance_double = Convert2Credits(balance);
                //Echo($"You have ${balance_double} space credits.");

                creditBalance = balance_double;
                creditBalance_fake = LerpD(creditBalance_fake, creditBalance, deltaTimeSinceLastTick * 2);

                UpdateCreditBalance(creditBalance_fake, creditBalance);
            }
        }

        public void DrawCredits()
		{
			// Example usage: Get credits for the local player
			if (MyAPIGateway.Session?.Player != null)
			{
                DrawCreditBalance(creditBalance_fake, creditBalance);
			}
		}


		private string GetPlayerCredits(IMyPlayer player)
		{
			if (player == null)
				return "0";

			// Get the player's balance as a formatted string
			string balance = player.GetBalanceShortString();

			// Remove the last three characters if the string is longer than three characters
			if (balance.Length > 3)
			{
				balance = balance.Substring(0, balance.Length - 3);
			}

			// Replace all commas with periods
			balance = balance.Replace(",", "");

			return balance;
		}

		private double Convert2Credits(string cr)
		{
			double cr_d = Convert.ToDouble(cr);

			return cr_d;
		}

        private void UpdateCreditBalance(double cr, double new_cr)
        {
            cr = Math.Round(cr);
            new_cr = Math.Round(new_cr);
            double cr_dif = new_cr - cr;

            if (cr_dif != 0)
            {
                if (!credit_counting)
                {
                    PlayCustomSound(SP_MONEY, worldRadarPos);
                    credit_counting = true;
                }


                if (cr_dif < 0)
                {
                    if (cr_dif > creditBalance_dif)
                    {
                        cr_dif = creditBalance_dif;
                    }
                }
                else if (cr_dif > 0)
                {
                    if (cr_dif < creditBalance_dif)
                    {
                        cr_dif = creditBalance_dif;
                    }
                }
                creditBalance_dif = cr_dif;
            }
            else
            {
                creditBalance_dif = 0;
                credit_counting = false;
            }
        }

        private void DrawCreditBalance(double cr, double new_cr)
		{
			cr = Math.Round(cr);
			new_cr = Math.Round(new_cr);
			double cr_dif = new_cr - cr;

			Vector4 color = theSettings.lineColor;
			double fontSize = 0.005;

			if (cr != new_cr) 
			{
				color *= 2;
				fontSize = 0.006;
			}

			//string crs = "$"+Convert.ToString (cr);
			string crs = $"${FormatCredits(cr)}";
			Vector3D pos = worldRadarPos + (radarMatrix.Right*radarRadius*1.2) + (radarMatrix.Backward*radarRadius*0.9);
			Vector3D dir = Vector3D.Normalize((radarMatrix.Forward*4 + radarMatrix.Right)/5);
			DrawText(crs, fontSize, pos, dir, color);

			if (cr_dif != 0) 
			{
				if (cr_dif < 0) 
				{
					if (cr_dif > creditBalance_dif) 
					{
						cr_dif = creditBalance_dif;
					}
				} else if (cr_dif > 0) 
				{
					if (cr_dif < creditBalance_dif) 
					{
						cr_dif = creditBalance_dif;
					}
				}

				string cr_difs = Convert.ToString(cr_dif);
				double lengthDif = crs.Length - cr_difs.Length;
				if (lengthDif > 0) 
				{
					for (int i = 1; i <= lengthDif; i++) 
					{
						cr_difs = " " + cr_difs;
					}
				}
				pos += radarMatrix.Up * fontSize * 2;
				DrawText(cr_difs, fontSize, pos, dir, LINECOLOR_Comp);
			} 
		}
		//------------------------------------------------------------------------------------------------------------












		//== HUD TOOL BARS ==========================================================================================
		private double TB_activationTime = 0;
		private List<AmmoInfo> ammoInfos;

		private void UpdateToolbars()
		{
            if (gHandler.localGridControlledEntity == null)
            {
                return;
            }

            var cockpit = gHandler.localGridControlledEntity as Sandbox.ModAPI.IMyCockpit;
            var grid = cockpit.CubeGrid;
            if (grid != null)
            {
            }
            else
            {
                return;
            }
            ammoInfos = GetAmmoInfos(grid);

            TB_activationTime += deltaTimeSinceLastTick * 0.1;
            TB_activationTime = ClampedD(TB_activationTime, 0, 1);
            TB_activationTime = Math.Pow(TB_activationTime, 0.9);
        }



		private void DrawToolbars()
		{
			if (gHandler.localGridControlledEntity == null) {
				return;
			}

			var cockpit = gHandler.localGridControlledEntity as Sandbox.ModAPI.IMyCockpit;
			var grid = cockpit.CubeGrid;
			if (grid != null) 
			{
			} else {
				return;
			}

			Vector3D pos = getToolbarPos();
			//two arcs

			DrawCircle(pos, 0.01, radarMatrix.Forward, theSettings.lineColor, false, 0.25f, 0.001f); // Center Dot
			DrawToolbarBack(pos);			// Left
			DrawToolbarBack(pos, true);	// Right
		}



		public void DrawToolbarBack(Vector3D position, bool flippit = false)
		{
			Vector3D radarUp = radarMatrix.Up;
			Vector3D radarDown = radarMatrix.Down;

			Vector3D normal = radarMatrix.Forward;

			Vector4 color = theSettings.lineColor;


			double TB_bootUp = LerpD(12, 4, TB_activationTime);


			double scale = 1.75;
			double height = 0.1024 * scale;
			double width = 0.0256 * scale;

			if (flippit) 
			{
				position += (radarMatrix.Right * radarRadius * 2) + (radarMatrix.Right * width * TB_bootUp);
			} 
			else 
			{
				position += (radarMatrix.Left * radarRadius * 2) + (radarMatrix.Left * width * TB_bootUp);
			}

			// Ensure the normal is normalized
			normal.Normalize();

			// Calculate perpendicular vectors to form the quad
			Vector3D up = radarUp; //Vector3D.CalculatePerpendicularVector(normal);

			Vector3D left = Vector3D.Cross(up, normal);
			up.Normalize();
			left.Normalize();

			if (GetRandomBoolean()) 
			{
				if (glitchAmount > 0.001) 
				{
					float glitchValue = (float)glitchAmount;

					Vector3D offsetRan = new Vector3D ((GetRandomDouble() - 0.5) * 2, (GetRandomDouble() - 0.5) * 2, (GetRandomDouble() - 0.5) * 2);
					double dis2Cam = Vector3D.Distance(MyAPIGateway.Session.Camera.Position, position);

					position += offsetRan * dis2Cam * glitchValue * 0.025;
					color *= GetRandomFloat();
				}
			}

			// Calculate the four corners of the quad
			Vector3D topLeft = position + left * width + up * height;
			Vector3D topRight = position - left * width + up * height;
			Vector3D bottomLeft = position + left * width - up * height;
			Vector3D bottomRight = position - left * width - up * height;

			if (flippit) 
			{
				Vector3D tempCorner = topLeft;
				topLeft = topRight;
				topRight = tempCorner;

				tempCorner = bottomLeft;
				bottomLeft = bottomRight;
				bottomRight = tempCorner;
			}

			// Use MyTransparentGeometry to draw the quad
			MyQuadD quad = new MyQuadD
			{
				Point0 = topLeft,
				Point1 = topRight,
				Point2 = bottomRight,
				Point3 = bottomLeft
			};

			MyTransparentGeometry.AddQuad(MaterialToolbarBack, ref quad, color, ref position);

			//DrawActionSlot (0, "Weapon", "Ammo: 999", position, height, flippit);

			for (int am = 0; am < ammoInfos.Count && am < 7; am++) 
			{
				DrawActionSlot(am, ammoInfos [am].AmmoType, Convert.ToString(ammoInfos [am].AmmoCount), position, height, flippit);
			}

			//-------------------COCKPIT----------------------------------
			string cockPitName;
			float cockPitCur;
			float cockPitMax;
			GetCockpitInfo(out cockPitName, out cockPitCur, out cockPitMax);

			DrawActionSlot(7, cockPitName +" Integrity", Convert.ToString(cockPitCur) + " / " + Convert.ToString(cockPitMax), position, height, flippit);
			//--------------------COCKPIT----------------------------------



		}

		private void DrawAction(string name, Vector3D pos, bool flippit = false)
		{
			double fontSize = 0.0075;

			if (!flippit) 
			{
				pos += (radarMatrix.Left * fontSize * 1.8 * name.Length) - (radarMatrix.Left * fontSize * 1.8);
			}
			//DrawCircle (pos, 0.005, radarMatrix.Forward, LINECOLOR_Comp, false, 1f, 0.001f);
			DrawText(name, fontSize, pos, radarMatrix.Forward, theSettings.lineColor, 1f);
		}

		private Vector3D getToolbarPos()
		{
			Vector3D pos = Vector3D.Zero;

			Vector3D cameraPos = MyAPIGateway.Session.Camera.Position;

			double elevation = GetHeadElevation(worldRadarPos, radarMatrix);

			pos = worldRadarPos + (radarRadius * 1.5 * radarMatrix.Forward) + (elevation * radarMatrix.Up) +  (radarMatrix.Up * 0.05);

			return pos;
		}

		private double GetHeadElevation(Vector3D referencePosition, MatrixD referenceMatrix)
		{
			// Get the player's character entity
			IMyCharacter character = MyAPIGateway.Session?.Player?.Character;

			if (character == null) 
			{
				return 0.0;
			}

			// Extract the head matrix
			MatrixD headMatrix = character.GetHeadMatrix(true);

			// Get the head position from the head matrix
			Vector3D headPosition = headMatrix.Translation;

			// Flatten the head position along the reference matrix's forward axis
			Vector3D referenceForward = referenceMatrix.Forward;
			Vector3D flattenedHeadPosition = headPosition - Vector3D.Dot(headPosition - referencePosition, referenceForward) * referenceForward;

			// Calculate the elevation (distance between the flattened head position and the reference position)
			double elevation = Vector3D.Distance(flattenedHeadPosition, referencePosition);

			return elevation;
		}

		private void DrawActionSlot(int slot, string name, string value, Vector3D position, double height, bool flippit = false)
		{
			double fontSize = 0.005;

			slot = (int)MathHelper.Clamp((double)slot, 0, 7);

			if(slot <= 3 && flippit)
			{
				return;
			}

			if(slot >= 4 && !flippit)
			{
				return;
			}

			if (flippit) 
			{
				slot -= 4;
			}

			int actionCount = 4;
			int actionStart = 0;

			slot = (int)MathHelper.Clamp((double)slot, 0, 7);

			if (flippit) 
			{
				actionStart = 4;
			}

			Vector3D actionDirection = radarMatrix.Left;
			if (flippit) 
			{
				actionDirection = radarMatrix.Right;
			}

			Vector3D aPos = position + (radarMatrix.Up * (height / 2)) - (radarMatrix.Up * ((height*2) / actionCount) * slot) + (radarMatrix.Up * ((height*2) / 8)) - (radarMatrix.Up * ((height*2) / 12));


			double bootOffset = -((height*2) / actionCount) * (actionCount-slot);
			double TB_bootUp2 = LerpD(bootOffset, 0, TB_activationTime);
			aPos += radarMatrix.Up * TB_bootUp2;

			if (slot == 0) 
			{
				aPos += (actionDirection * ((height * 2) / 24));
			}else if(slot == 1)
			{
				aPos += (actionDirection * ((height * 2) / 24))*2;
			}else if(slot == 2)
			{
				aPos += (actionDirection * ((height * 2) / 24))*1.75;
			}

			string theAction = value;
			DrawAction(theAction, aPos, flippit);

			if (!flippit) 
			{
				aPos += (radarMatrix.Left * fontSize * 1.8 * name.Length) - (radarMatrix.Left * fontSize * 1.8);
			}
			DrawText(Convert.ToString(name), 0.005, aPos + (radarMatrix.Up * 0.005 * 3), radarMatrix.Forward, theSettings.lineColor, 0.75f);
		}

		private List<string> GetHotbarItems(Sandbox.ModAPI.IMyCockpit cockpit)
		{
			List<string> itemNames = new List<string>();

			// Check if the cockpit is a terminal block
			Sandbox.ModAPI.Ingame.IMyTerminalBlock terminalBlock = cockpit as Sandbox.ModAPI.Ingame.IMyTerminalBlock;
			if (terminalBlock != null)
			{
				// Get the actions available in the terminal block
				List<ITerminalAction> actions = new List<ITerminalAction>();
				terminalBlock.GetActions(actions);

				// Add the names of the actions to the item names list
				foreach (var action in actions)
				{
					itemNames.Add(action.Name.ToString());
				}
			}

			return itemNames;
		}

		private void GetCockpitInfo(out string name, out float current, out float max)
		{
			VRage.Game.ModAPI.Interfaces.IMyControllableEntity controlledEntity = MyAPIGateway.Session.Player.Controller.ControlledEntity;

			name = "invalid";
			current = 0;
			max = 1;

			if (controlledEntity == null) 
			{
				return;
			}

			string blockName = "null";
			float currentIntegrity = 0;
			float maxIntegrity = 0;

			// Check if the controlled entity is a block
			VRage.Game.ModAPI.IMyCubeBlock controlledBlock = controlledEntity as VRage.Game.ModAPI.IMyCubeBlock;
			if (controlledBlock != null)
			{
				 blockName = controlledBlock.DisplayNameText;
				VRage.Game.ModAPI.IMySlimBlock slimBlock = controlledBlock.SlimBlock;

				if (slimBlock != null)
				{
					currentIntegrity = (float)Math.Round(slimBlock.Integrity/100);
					maxIntegrity = (float)Math.Round(slimBlock.MaxIntegrity/100);
				}
			}

			name = blockName;
			current = currentIntegrity;
			max = maxIntegrity;
		}

		public class AmmoInfo
		{
			public string AmmoType { get; set; }
			public int AmmoCount { get; set; }

			public AmmoInfo(string ammoType, int ammoCount)
			{
				AmmoType = ammoType;
				AmmoCount = ammoCount;
			}
		}

		/// <summary>
		/// Hopefully supports modded ammo now - loops through weapon or turret blocks for ammo definitions. 
		/// </summary>
		/// <param name="grid"></param>
		/// <returns></returns>
		private List<AmmoInfo> GetAmmoInfos(VRage.Game.ModAPI.IMyCubeGrid grid)
		{
			Dictionary<string, int> ammoCounts = new Dictionary<string, int>();
			HashSet<string> ammoTypes = new HashSet<string>();

			List<VRage.Game.ModAPI.IMySlimBlock> slimBlocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
			grid.GetBlocks(slimBlocks, block => block.FatBlock is Sandbox.ModAPI.IMyUserControllableGun || block.FatBlock is Sandbox.ModAPI.IMyLargeTurretBase);

			foreach (var slimBlock in slimBlocks)
			{

                VRage.Game.ModAPI.IMyCubeBlock fat = slimBlock.FatBlock;

                // Try to get weapon component definition from the block's ID
                MyCubeBlockDefinition blockDef = MyDefinitionManager.Static.GetCubeBlockDefinition(fat.BlockDefinition);

				if (blockDef?.Components == null)
				{
					continue;
				}

                foreach (MyCubeBlockDefinition.Component component in blockDef.Components)
                {
                    // Find the weapon component
                    if (component.Definition.Id.TypeId == typeof(MyObjectBuilder_WeaponBlockDefinition))
                    {
                        MyDefinitionId weaponDefId = new MyDefinitionId(typeof(MyObjectBuilder_WeaponDefinition), component.Definition.Id.SubtypeName);
                        MyWeaponDefinition weaponDef = MyDefinitionManager.Static.GetWeaponDefinition(weaponDefId);

						if (weaponDef == null)
						{
							continue;
						}

                        foreach (MyDefinitionId ammoMagId in weaponDef.AmmoMagazinesId)
                        {
                            MyAmmoMagazineDefinition ammoDef = MyDefinitionManager.Static.GetAmmoMagazineDefinition(ammoMagId);
							if (ammoDef == null)
							{
								continue;
							}

                            string ammoType = ammoDef.Id.SubtypeName;

                            if (!ammoTypes.Contains(ammoType))
                            {
                                ammoTypes.Add(ammoType);
                                int count = GetAmmoCount(grid, ammoType);
                                ammoCounts[ammoType] = count;
                            }
                        }
                    }
                }

    //            Sandbox.ModAPI.IMyUserControllableGun weapon = slimBlock.FatBlock as Sandbox.ModAPI.IMyUserControllableGun;
				//if (weapon != null)
				//{
				//	var def = (MyWeaponBlockDefinition)slimBlock.BlockDefinition;
				//	var weaponDef = MyDefinitionManager.Static.GetWeaponDefinition(def.WeaponDefinitionId);

				//	if (weaponDef != null)
				//	{
				//		MyDefinitionId ammoTypeId = weaponDef.AmmoMagazinesId[0];

				//		MyAmmoMagazineDefinition ammoData = MyDefinitionManager.Static.GetAmmoMagazineDefinition(ammoTypeId);
				//		MyAmmoMagazineDefinition ammoDefinition = ammoData;

				//		if (ammoDefinition != null) 
				//		{
				//			string ammoType = ammoDefinition.Id.SubtypeName;
				//			if (!ammoTypes.Contains(ammoType)) 
				//			{
				//				ammoTypes.Add(ammoType);
				//				int count = GetAmmoCount(grid, ammoType);
				//				ammoCounts[ammoType] = count;
				//			}
				//		}
						
				//	}
				//}
			}

			List<AmmoInfo> ammoInfos = new List<AmmoInfo>();
			foreach (var entry in ammoCounts)
			{
				ammoInfos.Add(new AmmoInfo(entry.Key, entry.Value));
			}

			return ammoInfos;
		}

		private int GetAmmoCount(VRage.Game.ModAPI.IMyCubeGrid grid, string ammoType)
		{
			int count = 0;
			List<VRage.Game.ModAPI.IMySlimBlock> slimBlocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
			grid.GetBlocks(slimBlocks, block => block.FatBlock is IMyInventoryOwner);

			foreach (var slimBlock in slimBlocks)
			{
				IMyInventoryOwner inventoryOwner = slimBlock.FatBlock as IMyInventoryOwner;
				if (inventoryOwner != null)
				{

					for (int i = 0; i < inventoryOwner.InventoryCount; i++)
					{
						var inventory = inventoryOwner.GetInventory(i);
						List<MyInventoryItem> items = new List<MyInventoryItem>();
						inventory.GetItems(items);

						foreach (var item in items)
						{
							if (item.Type.SubtypeId == ammoType)
							{
								count += (int)item.Amount;
							}
						}
					}
				}
			}
				
			return count;
		}


		//------------------------------------------------------------------------------------------------------------
	}
}