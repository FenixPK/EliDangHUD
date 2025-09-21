using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Components;
using VRage.Input;
using VRage.Library.Utils;
using VRage.ModAPI;
using VRage.ObjectBuilders;

//using VRage.ModAPI;
using VRage.Utils;
//using VRage.Input;
using VRageMath;
using VRageRender;
using VRageRender.Import;
using static Sandbox.Game.Entities.MyCubeGrid;
using static VRageMath.Base6Directions;
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


2025-08-22
I suggest new approach:
UpdateAfterSimulation initializes mod, and consistently checks if a nearest grid to player is not null, if so it initalizes that portion of the mod.
If player is inside a grid's bounding box and it is the "nearest grid" then we set this to the local grid and process it's blocks ONCE adding event handlers to manage them from there. OnBlockAdded or OnBlockRemoved.
These event handlers can check if a block is a terminal like a holo table and add event handlers for checking if its custom name changes. If so re-evaluate if it has the tag [ELI_HOLO] or [ELI_LOCAL] and then
add them to the dictionary if not present or remove them if present and no longer tagged. Also triggering flags for re-processing clusters/slices as necessary so we only process them on init or on change.
During this processing we can check if there are any relevant holo tables and add them to the dictionaries tracking these relying on event handlers to remove them or add more from here.
Now when a player is controlling a grid the only thing that changes is the hud being drawn. We can still rely on nearest grid for everything else. One unified list of blocks, one hologram representation of the ship (that might be
drawn differently in HUD and on multiple holo tables)
Of course there are things like power and H2 time that don't need to be updated unless a player is controlling the grid. So that can be refactored and skipped.
I might want to start with completely new methods and not even rely on "gHandler" or anything like that anymore. The original author did a good job, but trying to add my changes to their existing structure might not be the best idea
versus rewriting the approach from the ground up.

-Init mod
-If nearestGrid then init blocks, tagged holo tables etc, add event handlers for managing.
-If player controlling grid then update power/h2/altitude etc.
-Draw HUD if player controlling.
-Draw holo tables if in range of any tagged holo tables. NOTE applying rotations at this point is more taxing than necessary and I COULD technically store a list of blocks/slices with rotations already applied per holo table. So when a change happens (or on init)
and it re-processes the slices it does one reprocess of the slices then per holo table it loops through the slices rotating them and then storing in the holo table dictionary. So the main dict of slices isn't ever used for drawing directly. 

TODO and CHANGES moved to README.md

Wise words from baby:
hjmiiiiiiiiooooooooooooooooooooooo88888888888888888888888888888888...;////////////////////////////////////////////////

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

		public bool RadarPingHasPower { get; set; }

        public int BlockCount { get; set; }

        public MyCubeSize GridCubeSize { get; set; }

        public MyStringId Material { get; set; } // Material used for rendering the ping


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
    /// This enum stores the different types of views there are for the holograms
    /// </summary>
    public enum HologramViewType
    {
        Static = 0,
        Orbit = 1,
        Perspective = 2
    }

    /// <summary>
    /// GridBlock class to hold the IMySlimBlock, draw position, and other info for use with hologram and grid tracking. 
    /// </summary>
    public class GridBlock
    {
        public VRage.Game.ModAPI.IMySlimBlock Block;
        public Vector3D DrawPosition;
        public float LastCurrentIntegrity;
        public BlockClusterType ClusterType = BlockClusterType.Structure;
        public int ClusterSize = 1;
    }

    /// <summary>
    /// Enum for different block cluster types for hologram rendering or grid tracking.
    /// </summary>
    public enum BlockClusterType
    {
        Structure = 0,
        PDC = 1,
        Railgun = 2,
        MediumBallistic = 3,
        LargeBallistic = 4,
        EnergyWeapon = 5,
        Torpedo = 6,
        Missile = 7,
        Reactor = 8,
        Battery = 9,
        Antenna = 10,
        IonThruster = 11,
        HydrogenThruster = 12,
        AtmosphericThruster = 13,
        HydrogenTank = 14,
        OxygenTank = 15,
        JumpDrive = 16,
        Shield = 17,
        SolarPanel = 18,
        PowerProducer = 19
    }

    /// <summary>
    /// BlockCluster is a cube of blocks of a certain size, used for hologram rendering. Size of square drawn is = clusterSize, and the cube will be up to clusterSize^3 blocks. 
    /// Different tolerances for when to break clusters down to smaller ones means the density of the cluster is not concrete so it can be 1 block, or a full cube of blocks. 
    /// </summary>
    public class BlockCluster
    {
        public float Integrity = 0f;
        public float MaxIntegrity = 0f;
        public Vector3D DrawPosition = Vector3D.Zero;
        public int ClusterSize = 1;
        public BlockClusterType ClusterType = BlockClusterType.Structure;
        public Dictionary<Vector3I, GridBlock> Blocks = new Dictionary<Vector3I, GridBlock>();
    }

    /// <summary>
    /// Not using this, but keeping the class because a function that used to use it has a "contribution" from my baby and that legacy is absolutely adorable. C:
    /// </summary>
    public class ClusterBox
    {
        public Vector3I Min;
        public Vector3I Max;
        public List<VRage.Game.ModAPI.IMySlimBlock> Blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
        public int IntegrityBucket; // which bucket this cluster belongs to
    }

    [Serializable]
    public class SerializableVector3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        // Parameterless ctor for XmlSerializer
        public SerializableVector3D() { }

        public SerializableVector3D(Vector3D v)
        {
            X = v.X;
            Y = v.Y;
            Z = v.Z;
        }

        public Vector3D ToVector3D() => new Vector3D(X, Y, Z);
    }

    [Serializable]
    public class SerializableVector4
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float W { get; set; }

        public SerializableVector4() { }

        public SerializableVector4(Vector4 v)
        {
            X = v.X;
            Y = v.Y;
            Z = v.Z;
            W = v.W;
        }

        public Vector4 ToVector4() => new Vector4(X, Y, Z, W);
    }


    /// <summary>
    /// This holds the global settings that will get loaded from and saved to the XML file in the save/world folder. Clients that are NOT also servers request these settings from the server. And upon request servers send them.
    /// Clients that are ALSO servers are single player and just load from file. 
    /// </summary>
    public class ModSettings
    {

		/// <summary>
		/// If true the mod is enabled by setting seat as Main Cockpit instead of using an [ELI_HUD] tag. 
		/// </summary>
		public bool useMainCockpitInsteadOfTag = false;
		public string useMainCockpitInsteadOfTag_DESCRIPTION = "If true the mod is enabled by setting seat as Main Cockpit instead of using a tag [ELI_HUD], for users who prefer the original method. \r\n " +
			"Note that with this setting enabled you can only have one HUD seat per grid. If you leave this disabled you can use the [ELI_HUD] tag on as many seats as you want.";

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
        public int rangeBracketDistance = 200;
        public string rangeBracketDistance_DESCRIPTION = "Represents the distance in meters used for range bracketing in radar targeting calculations. \r\n" +
            "This value is used to determine the \"bracket\" a target fits into for zooming the radar. Or when changing broadcast distance of antennas (radar range). \r\n" +
            "Adjusting this value can affect the precision of radar zoom levels.";

        /// <summary>
        /// Maximum number of entities/voxels that can be displayed as radar pings on the radar at once.
        /// </summary>
        public int maxPings = 500;
        public string maxPings_DESCRIPTION = "Maximum number of entities/voxels that can be displayed as radar pings on the radar at once.";

		/// <summary>
		/// When true uses SigInt lite logic with Active and Passive radar.
		/// </summary>
		public bool useSigIntLite = true;
		public string useSigIntLite_DESCRIPTION = "When true uses SigInt lite logic regarding Active and Passive radar, using Antenna blocks on your and other grids. When enabled: A powered and broadcasting antenna \r\n " +
			"on your grid will be considered active mode, powered but not broadcasting is considered passive mode. \r\n " +
			"Active can pick up all entities (voxels and grids) within the broadcast range. This is your radar waves painting everything within that radius and pinging them. \r\n " +
			"Passive can only pick up actively broadcasting grids within your broadcast radius, and their broadcast radius. This is your radar picking up signals that are pinging off you. \r\n " +
			"When disabled uses the simple logic of largest radius from all powered antennae on the grid as your radar range.";

        /// <summary>
        /// How far away can a holo table be before it no longer renders the radar.
        /// </summary>
        public double holoTableRenderDistance = 20;
        public string holoTableRenderDistance_DESCRIPTION = "How far away can a holo table be before it no longer renders the radar.";

        /// <summary>
        /// Percentage threshold at which radar pings start to fade out at the edge of the radar range Eg. 0.01 = 0.01*100 = 1%. 
		/// Also used for notifying the player of new pings. When pings cross the threshold distance they will be announced audibly and visually on the radar. 
        /// </summary>
        public double fadeThreshold = 0.01;
        public string fadeThreshold_DESCRIPTION = "Percentage threshold at which radar pings start to fade out at the edge of the radar range Eg. 0.01 = 0.01*100 = 1%. \r\n" +
            " Also used for notifying the player of new pings. \r\n When pings cross the threshold distance they will be announced audibly and visually on the radar.";

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
        /// 
        //public Vector4 lineColorDefault = new Vector4(1f, 0.5f, 0.0f, 1f);
        public SerializableVector4 lineColorDefault = new SerializableVector4(new Vector4(1f, 0.5f, 0.0f, 1f));
        public string lineColorDefault_DESCRIPTION = "Default color of the HUD lines, used for rendering circles and other shapes.";

        /// <summary>
        /// Position of the star. No idea if we need to change this for "RealStars" mod or not.
        /// </summary>
        //public Vector3D starPos = new Vector3D(0, 0, 0);
        public SerializableVector3D starPos = new SerializableVector3D(new Vector3D(0, 0, 0));
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

        public bool enableVelocityLines = true;
        public string enableVelocityLines_DESCRIPTION = "Show the velocity lines or not.";

        public bool enablePlanetOrbits = true;
        public string enablePlanetOrbits_DESCRIPTION = "Show the planet orbit lines or not.";

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

		/// <summary>
		/// Minimum number of blocks a grid must have to be selectable as a target
		/// </summary>
		public int minTargetBlocksCount = 5;
		public string minTargetBlocksCount_DESCRIPTION = "The minimum number of blocks required for a grid to be selected as a target. Helps reduce pieces of debris being selected";


        /// <summary>
        /// Whether to use a mouse button, or a keybind, for selecting new targets in Eli Dang hud
        /// </summary>
        public bool useMouseTargetSelect = true;
		public string useMouseTargetSelect_DESCRIPTION = "Whether to use a mouse button, or a keybind, for selecting new targets in Eli Dang hud";

        /// <summary>
        /// Mouse button to use: 0 = Left, 1 = Right, 2 = Middle
        /// </summary>
        public int selectTargetMouseButton = 1;
		public string selectTargetMouseButton_DESCRIPTION = "Mouse button to use: 0 = Left, 1 = Right, 2 = Middle. (Default is Right)";

        /// <summary>
        /// Key to select target, if mouse button is disabled
        /// </summary>
        public int selectTargetKey = 84; // (int)MyKeys.T;
		public string selectTargetKey_DESCRIPTION = "Key to select target, if mouse button is disabled (Default is T)";

        /// <summary>
        /// Key to rotate the static hologram view in the +X axis
        /// </summary>
        public int rotateLeftKey = 100; // (int)MyKeys.NumPad4;
		public string rotateLeftKey_DESCRIPTION = "Key to rotate the static hologram view in the +X axis (Ctrl modifier changes local hologram, no Ctrl changes target, Default is NumPad4)";

        /// <summary>
        /// Key to rotate the static hologram view in the -X axis
        /// </summary>
        public int rotateRightKey = 102; // (int)MyKeys.NumPad6;
        public string rotateRightKey_DESCRIPTION = "Key to rotate the static hologram view in the -X axis (Ctrl modifier changes local hologram, no Ctrl changes target, Default is NumPad6)";

        /// <summary>
        /// Key to rotate the static hologram view in the +Y axis
        /// </summary>
        public int rotateUpKey = 104; // (int)MyKeys.NumPad8;
        public string rotateUpKey_DESCRIPTION = "Key to rotate the static hologram view in the +Y axis (Ctrl modifier changes local hologram, no Ctrl changes target, Default is NumPad8)";

        /// <summary>
        /// Key to rotate the static hologram view in the -Y axis
        /// </summary>
        public int rotateDownKey = 98; // (int)MyKeys.NumPad2;
        public string rotateDownKey_DESCRIPTION = "Key to rotate the static hologram view in the -Y axis (Ctrl modifier changes local hologram, no Ctrl changes target, Default is NumPad2)";

        /// <summary>
        /// Key to rotate the static hologram view in the +Z axis
        /// </summary>
        public int rotatePosZKey = 103; // (int)MyKeys.NumPad7;
        public string rotatePosZKey_DESCRIPTION = "Key to rotate the static hologram view in the +Z axis (Ctrl modifier changes local hologram, no Ctrl changes target, Default is NumPad7)";

        /// <summary>
        /// Key to rotate the static hologram view in the -Z axis
        /// </summary>
        public int rotateNegZKey = 105; // (int)MyKeys.NumPad9;
        public string rotateNegZKey_DESCRIPTION = "Key to rotate the static hologram view in the -Z axis (Ctrl modifier changes local hologram, no Ctrl changes target, Default is NumPad9)";

        /// <summary>
        /// Key to reset static hologram view (Ctrl modifier cycles local hologram, no Ctrl cycles target)
        /// </summary>
        public int resetKey = 101; // (int)MyKeys.NumPad5;
		public string resetKey_DESCRIPTION = "Key to reset hologram view (Ctrl modifier cycles local hologram, no Ctrl cycles target, Default is NumPad5)";

        /// <summary>
        /// Key to set hologram view to Orbit cam (Only for target)
        /// </summary>
        public int orbitViewKey = 97; // (int)MyKeys.NumPad1;
		public string orbitViewKey_DESCRIPTION = "Key to set hologram view to Orbit cam (Only for target, Default is NumPad1)";

        /// <summary>
        /// Key to set hologram view to Perspective cam (Only for target)
        /// </summary>
        public int perspectiveViewKey = 99; // (int)MyKeys.NumPad3;
		public string perspectiveViewKey_DESCRIPTION = "Key to set hologram view to Perspective cam (Only for target, Default is NumPad3)";

        public int largeGridSizeOneMaxBlocks = 1500;
        public int largeGridSizeTwoMaxBlocks = 2500;
        public int largeGridSizeThreeMaxBlocks = 5000;
        public int largeGridSizeFourMaxBlocks = 7500;
        public int largeGridSizeFiveMaxBlocks = 15000;
        public string largeGridSizeTiers_DESCRIPTION = "Set the various size in blocks for the tiers that determine blip icons used. There are six (6) large grid icons.";

        public int smallGridSizeOneMaxBlocks = 600;
        public int smallGridSizeTwoMaxBlocks = 1200;
        public int smallGridSizeThreeMaxBlocks = 1800;
        public string smallGridSizeTiers_DESCRIPTION = "Set the various size in blocks for the tiers that determine blip icons used. There are four (4) small grid icons.";

        public bool renderHoloRadarsInSeat = false;
        public string renderHoloRadarsInSeat_DESCRIPTION = "Whether holo table radars should still render if you are in a cockpit that has active hud.";

        public bool renderHoloHologramInSeat = false;
        public string renderHoloHologramsInSeat_DESCRIPTION = "Whether holo table holograms should still render if you are in a cockpit that has active hud.";

        public int blockCountClusterStep = 10000;
        public string blockCountClusterStep_DESCRIPTION = "Used for setting max and min cluster sizes when building the grid hologram (There is a complex relationship with this and clusterSplitRatio for fidelity). \r\n " +
            "Basically we don't want to draw more than this number of squares on screen for performance so we step up the size of clusters when over this number. \r\n " +
            "A grid with this number of blocks or lower will be set to cluster size 1 (ie 1x1x1). A grid over this number of blocks will be set to size 2 (2x2x2). \r\n " +
            "Because this is cubic after this point it isn't about block count, but about how many clusters would be drawn on screen which is blockCount / (#x#x#) where # = clusterSize. \r\n " +
            "Max cluster size is set to clusterSize +1, Min is set to clusterSize -1 (but with a min of 1). Which allows for more fidelity where blocks are not concentrated but still clusters them larger where possible to reduce draw calls. \r\n " +
            "This provides the biggest performance increase of all the changes I've made. Lowering this value reduces fidelity as it will increase clusterSize sooner. \r\n " +
            "Eg. At blockClusterStep = 10000: a grid of 9999 blocks will range between 1x1x1 and 2x2x2 clusters and a grid of 10001 - 80000 blocks will range between 1x1x1 and 3x3x3 clusters. \r\n " +
            "A grid of 80001 blocks will range between 2x2x2 and 4x4x4 (because the next step is at blockCount / (2x2x2) and we do clusterSize-1 to clusterSize+1). \r\n " +
            "This clustering logic is the largest performance increase we can make, the number of squares drawn is the largest bottleneck. \r\n " +
            "We can adjust this to cluster sooner/later and adjust the splitThreshold to allow more/less fidelity around sparse blocks.";

        public int blockClusterAddlMax = 1;
        public string blockCLusterAddlMax_DESCRIPTION = "The value to add to get the max cluster range. eg. for a grid identified as clusterSize = 2, the max would be 3 if this value is 1, meaning it would start at 3x3x3 clusters and size down where sparse.";

        public int blockClusterAddlMin = 1;
        public string blockCLusterAddlMin_DESCRIPTION = "The value to subtract to get the min cluster range. eg. for a grid identified as clusterSize = 3, the min would be 2 if this value is 1, meaning it wouldn't go smaller than 2x2x2 clusters for sparse regions.";

        public float clusterSplitThreshold = 0.33f;
        public string clusterSplitThreshold_DESCRIPTION = "The fill ratio threshold at which a larger block cluster will be split into smaller clusters. Eg at 0.33 (33%) a larger cluster will only break down to smaller clusters if less than 33% full. \r\n " +
            "So for a 3x3x3 cluster at 8/27 blocks it will break to smaller 2x2x2 and 1x1x1 clusters. At 9/27 blocks it will show as one 3x3x3 cluster. Combined with the blockCountClusterStep this allows drawing larger grids at larger cluster sizes with \r\n " +
            "less fidelity but more performance by reducing the number of block squares drawn on screen. By tweaking the splitThreshold we can add fidelity for sparse clusters, while allowing larger clusters for the rest of the grid.";
        
        public int clusterRebuildClustersPerTick = 200;
        public string clusterRebuildClustersPerTick_DESCRIPTION = "The number of block clusters built per game tick on a rebuild of the hologram clusters triggered due to blocks being removed or added. This spreads load over time to reduce stutter. \r\n " +
            "Lowering this value will increase the speed at which add/removal of blocks reprocesses the hologram but increases load. ";

        public int ticksUntilClusterRebuildAfterChange = 100;
        public string ticksUntilClusterRebuildAfterChange_DESCRIPTION = "The number of game ticks that must pass after add or remove of a block before re-processing the hologram. Prevents rebuilds until after a period of inactivity to reduce stutter. \r\n " +
            "Note: On taking damage clusters will update integrity/maxIntegrity immediately and thus update hologram color. \r\n " +
            "Loss of blocks in a cluster will also update integrity (or remove if no blocks left), but the hologram won't reprocess fully until after this many ticks have passed. \r\n" +
            "Then it will start a rebuild and process clusterRebuildClustersPerTick clusters each tick. \r\n " +
            "If another add/removal occurs that process stops, and only restarts after this number of ticks have passed. \r\n " +
            "Lowering this values will increase the speed at which the hologram reprocess starts. ";

        public int maxSpeedSmallGrid = 100;
        public string maxSpeedSmallGrid_DESCRIPTION = "The max speed in m/s for small grids. Default is 100. Set this manually to match any speed mods you may have, for RelativeTopSpeed I suggest the max with boost."; 

        public int maxSpeedLargeGrid = 100;
        public string maxSpeedLargeGrid_DESCRIPTION = "The max speed in m/s for large grids. Default is 100. Set this manually to match any speed mods you may have, for RelativeTopSpeed I suggest the max with boost.";

    }

    /// <summary>
    /// CustomData object for the controlled entity (cockpit or seat for eg.) Holds settings specific to that seat.
    /// </summary>
    public class ControlledEntityCustomData
    {
        public bool masterEnabled = true;
        public bool enableHolograms = true;
        public bool enableHologramsLocalGrid = true;
        public bool enableHologramsTargetGrid = true;
        public HologramViewType localGridHologramViewType = HologramViewType.Static;
        public HologramViewType targetGridHologramViewType = HologramViewType.Perspective;
        public bool localGridHologramAngularWiggle = true;
        public float holoLocalRotationX = 0f;
        public float holoLocalRotationY = 0f;
        public float holoLocalRotationZ = 0f;
        public float holoTargetRotationX = 0f;
        public float holoTargetRotationY = 0f;
        public float holoTargetRotationZ = 0f;
        public double holoLocalX = 0;
        public double holoLocalY = 0;
        public double holoLocalZ = 0;
        public float holoLocalScale = 1f;
        public double holoTargetX = 0;
        public double holoTargetY = 0;
        public double holoTargetZ = 0;
        public float holoTargetScale = 1f;
        public bool enableToolbars = true;
        public bool enableGauges = true;
        public bool enableMoney = true;
        public double scannerX = 0.0;
        public double scannerY = -0.2;
        public double scannerZ = -0.575;
        public Vector3D radarOffset = new Vector3D(0.0, -0.2, -0.575);
        public float radarScannerScale = 1;
        public float radarRadius = 1f * 0.125f;
        public float radarBrightness = 1;
        public Color scannerColor = new Vector3(1f, 0.5f, 0.0f);
        public Vector3 lineColorRGB = new Vector3(1f, 0.5f, 0.0f);
        public Vector3 lineColorRGBComplimentary = (new Vector3(0f, 0.5f, 1f) * 2f) + new Vector3(0.01f, 0.01f, 0.01f);
        public Vector4 lineColor = new Vector4(1f, 0.5f, 0.0f, 1f);
        public Vector4 lineColorComp = new Vector4((new Vector3(0f, 0.5f, 1f) * 2f) + new Vector3(0.01f, 0.01f, 0.01f), 1f);
        public bool enableVelocityLines = true;
        public float velocityLineSpeedThreshold;
        public bool enablePlanetOrbits = true;
        public float planetOrbitSpeedThreshold = 10;
        public bool scannerShowVoxels = true;
        public bool scannerOnlyPoweredGrids = true;
    }

    /// <summary>
    /// CustomData object for the holo radar settings that can be stored for each holo radar terminal
    /// </summary>
    public class HoloRadarCustomData
    {
        public bool scannerEnable = true;
        public double scannerX = 0;
        public double scannerY = 0.7;
        public double scannerZ = 0;
        public float scannerRadius = 1;
        public bool scannerShowVoxels = true;
        public bool scannerOnlyPoweredGrids = true;
    }

    /// <summary>
    /// CustomData object for the hologram settings that can be stored for each hologram terminal
    /// </summary>
    public class HologramCustomData
    {
        public bool holoEnable = true;
        public double holoX = 0;
        public double holoY = 0.7;
        public double holoZ = 0;
        public float holoScale = 0.1f;
        public int holoSide = 0;
        public int holoRotationX = 0;
        public int holoRotationY = 0;
        public int holoRotationZ = 0;
        public double holoBaseX = 0;
        public double holoBaseY = -0.5;
        public double holoBaseZ = 0;
        public float holoBrightness = 1;
        public Color holoColor = new Vector3(1f, 0.5f, 0.0f);
        public Vector3 lineColorRGB = new Vector3(1f, 0.5f, 0.0f);
        public Vector3 lineColorRGBComplimentary = (new Vector3(0f, 0.5f, 1f) * 2f) + new Vector3(0.01f, 0.01f, 0.01f);
        public Vector4 lineColor = new Vector4(1f, 0.5f, 0.0f, 1f);
        public Vector4 lineColorComp = new Vector4((new Vector3(0f, 0.5f, 1f) * 2f) + new Vector3(0.01f, 0.01f, 0.01f), 1f);
    }

    /// <summary>
    /// Used to store the customdata class along with the pure string so we can check if it changed
    /// </summary>
    public class HologramCustomDataTerminalPair
    {
        public HologramCustomData HologramCustomData;
        public string HologramCustomDataString;
    }

    /// <summary>
    /// Used to store the customdata class along with the pure string so we can check if it changed
    /// </summary>
    public class HoloRadarCustomDataTerminalPair
    {
        public HoloRadarCustomData HoloRadarCustomData;
        public string HoloRadarCustomDataString;
    }

    

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation)]
	public class CircleRenderer : MySessionComponentBase
	{
        public bool _modInitialized = false;

        /// <summary>
        /// Whether the list of entities has been initialized. Once initialized event handlers will manage adding and removal of new entities. 
        /// </summary>
        private bool _entitiesInitialized = false;
        private bool _audioInitialized = false;

        private bool _weaponCoreInitialized = false;
        private bool _weaponCoreWeaponsInitialized = false;

        // File to store the settings in, and settings object to hold them.
        private const string settingsFile = "EDHH_settings.xml";
		/// <summary>
		/// This holds the settings that the mod uses globally, not per ship/seat. "theSettings" gets saved to XML in the save world folder.
		/// </summary>
        public ModSettings theSettings = new ModSettings(); // Is instantiated with default values as a new ModSettings object, which is then overwritten by the settings file if it exists.
		// For multiplayer the client requests this from the server. For single player it loads from the saved world folder. 
		// This means I've completely overhauled the code so any reference to static constants like "someConstant" are now "theSettings.someConstant" instead. 
		// Saves having to update 4 places when adding a new setting for eg.

        // Values for sending the file and syncing between server and clients
        private const ushort MessageId = 10203;
        private const ushort RequestMessageId = 30201;

        // Materials used for rendering various elements of the HUD
        // These are configured in the file TransparentMaterials_ED.sbc
        private readonly MyStringId MaterialDust1 = MyStringId.GetOrCompute("ED_DUST1");
		private readonly MyStringId MaterialDust2 = MyStringId.GetOrCompute("ED_DUST2");
        private readonly MyStringId MaterialDust3 = MyStringId.GetOrCompute("ED_DUST3");
        private readonly MyStringId MaterialVisor = MyStringId.GetOrCompute ("ED_visor");
		private readonly MyStringId Material = MyStringId.GetOrCompute("Square");
		private readonly MyStringId MaterialLaser = MyStringId.GetOrCompute("WeaponLaser");
		private readonly MyStringId MaterialBorder = MyStringId.GetOrCompute("ED_Border");
		private readonly MyStringId MaterialCompass = MyStringId.GetOrCompute("ED_Compass");
		private readonly MyStringId MaterialCross = MyStringId.GetOrCompute("ED_Targetting");
		private readonly MyStringId MaterialCrossOutter = MyStringId.GetOrCompute("ED_Targetting_Outter");
		private readonly MyStringId MaterialLockOn = MyStringId.GetOrCompute("ED_LockOn");
        private readonly MyStringId MaterialLockOnHollow = MyStringId.GetOrCompute("ED_LockOn_Hollow");
        private readonly MyStringId MaterialToolbarBack = MyStringId.GetOrCompute("ED_ToolbarBack");
		private readonly MyStringId MaterialCircle = MyStringId.GetOrCompute("ED_Circle");
		private readonly MyStringId MaterialCircleHollow = MyStringId.GetOrCompute("ED_CircleHollow");
		private readonly MyStringId MaterialCircleSeeThrough =	MyStringId.GetOrCompute("ED_CircleSeeThrough");
		private readonly MyStringId MaterialCircleSeeThroughAdd = MyStringId.GetOrCompute("ED_CircleSeeThroughAdd");
		private readonly MyStringId MaterialTarget = MyStringId.GetOrCompute("ED_TargetArrows");
		private readonly MyStringId MaterialSquare = MyStringId.GetOrCompute("ED_Square");
        private readonly MyStringId MaterialTinyCircle = MyStringId.GetOrCompute("ED_TinyCircle");
        private readonly MyStringId MaterialTriangle =MyStringId.GetOrCompute("ED_Triangle");
		private readonly MyStringId MaterialDiamond = MyStringId.GetOrCompute("ED_Diamond");
		private readonly MyStringId MaterialCube = MyStringId.GetOrCompute("ED_Cube");
		private readonly MyStringId MaterialShipFlare = MyStringId.GetOrCompute("ED_SHIPFLARE");
        private readonly MyStringId MaterialSG_1 = MyStringId.GetOrCompute("ED_GRID_SG_1");
        private readonly MyStringId MaterialSG_2 = MyStringId.GetOrCompute("ED_GRID_SG_2");
        private readonly MyStringId MaterialSG_3 = MyStringId.GetOrCompute("ED_GRID_SG_3");
        private readonly MyStringId MaterialSG_4 = MyStringId.GetOrCompute("ED_GRID_SG_4");
        private readonly MyStringId MaterialLG_1 = MyStringId.GetOrCompute("ED_GRID_LG_1");
        private readonly MyStringId MaterialLG_2 = MyStringId.GetOrCompute("ED_GRID_LG_2");
        private readonly MyStringId MaterialLG_3 = MyStringId.GetOrCompute("ED_GRID_LG_3");
        private readonly MyStringId MaterialLG_4 = MyStringId.GetOrCompute("ED_GRID_LG_4");
        private readonly MyStringId MaterialLG_5 = MyStringId.GetOrCompute("ED_GRID_LG_5");
        private readonly MyStringId MaterialLG_6 = MyStringId.GetOrCompute("ED_GRID_LG_6");
        private List<string> MaterialFont = new List<string> ();


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
        //public static float GLOW = 1f; // This is overwritten by the configurable brightness settings per cockpit. But is here as a default, it is not something in the mod settings but rather the BLOCK settings.
        public float OrbitDimmer = 1f;
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

		/// <summary>
		/// At what percent power usage does the glitch effect start?
		/// </summary>
		private double powerLoadGlitchStart = 0.667;

        // Random floats for random number generation?
        private List<float> randomFloats = new List<float>();
		private int nextRandomFloat = 0;

		// Track the radar animations
		private List<RadarAnimation> RadarAnimations = new List<RadarAnimation>();

        // The radar variables
        private float min_blip_scale = 0.05f;

        Dictionary<VRage.ModAPI.IMyEntity, RadarPing> radarPings = new Dictionary<VRage.ModAPI.IMyEntity, RadarPing>();

        //private float SpeedThreshold = 10f;

        private IMyPlayer _thePlayer;
        private bool _isFirstPerson; 

        private Vector3D _hologramRightOffset_HardCode = new Vector3D(-0.2, 0.075, 0);
        //private Vector3D _hologramOffsetLeft_HardCode = new Vector3D(0.2, 0.075, 0);

        private bool _chatCommandRegistered = false;
		

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

            if (!_chatCommandRegistered)
            {
                MyAPIGateway.Utilities.MessageEntered += OnMessageEntered;
                _chatCommandRegistered = true;
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

            if (_chatCommandRegistered)
            {
                MyAPIGateway.Utilities.MessageEntered -= OnMessageEntered;
                _chatCommandRegistered = false;
            }
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
		public static ModSettings DeserializeSettingsFromXML(string settingsInXML)
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
			using (TextWriter writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(settingsFile, typeof(ModSettings)))
			{
				writer.Write(data);
			}
		}

        /// <summary>
        /// Loads the mod settings from persistent storage from XML format.
        /// </summary>
        /// <returns>An instance of <see cref="ModSettings"/> populated with data from the XML file in world storage. Or a new one with default values if none exists. </returns>
        public static ModSettings LoadSettings()
		{
			if (MyAPIGateway.Utilities.FileExistsInWorldStorage(settingsFile, typeof(ModSettings)))
			{
				using (TextReader reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(settingsFile, typeof(ModSettings)))
				{
					string data = reader.ReadToEnd();
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


        private void OnMessageEntered(string messageText, ref bool sendToOthers)
        {
            if (!messageText.StartsWith("/"))
                return;

            sendToOthers = false;

            var parts = messageText.TrimStart('/').Split(' ');
            string command = parts[0].ToLowerInvariant();

            // Only allow in SP or admins in MP
            if (MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                var player = MyAPIGateway.Session?.Player;
                if (player == null || !MyAPIGateway.Session.IsUserAdmin(player.SteamUserId))
                {
                    MyAPIGateway.Utilities.ShowMessage("EliDang", "You must be an admin to use this command.");
                    return;
                }
            }

            if (command == "elidang" && parts.Length >= 3)
            {
                string setting = parts[1].ToLowerInvariant();
                string value = parts[2];

                if (setting == "blockcountclusterstep")
                {
                    int valueInt;
                    if (int.TryParse(value, out valueInt))
                    {
                        theSettings.blockCountClusterStep = valueInt;
                        MyAPIGateway.Utilities.ShowMessage("EliDang", $"blockCountClusterStep set to {valueInt}");
                    }
                    else
                    {
                        MyAPIGateway.Utilities.ShowMessage("EliDang", $"Invalid value: {value}");
                    }
                }

                if (gHandler != null) 
                {
                    gHandler.theSettings = theSettings;
                }
            }
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
        private void DrawText(string text, double size, Vector3D pos, Vector3D dir, Vector4 color, float brightness, float dim, bool flipUp)
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
					DrawQuadRigid(pos + offset, dir, size, getFontMaterial(parsedText[i]), color * brightness * dim, flipUp);
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
			Vector4 color = gHandler.localGridControlledEntityCustomData.lineColor;
            float brightness = gHandler.localGridControlledEntityCustomData.radarBrightness;


            if (gHandler.localGridPowerUsagePercentage > powerLoadGlitchStart) 
			{
				float powerLoad_offset = (float)gHandler.localGridPowerUsagePercentage - (float)powerLoadGlitchStart;
				powerLoad_offset /= 0.333f;

				color.X = LerpF(color.X, 1, powerLoad_offset);
				color.Y = LerpF(color.Y, 0, powerLoad_offset);
				color.Z = LerpF(color.Z, 0, powerLoad_offset);
			}

			double powerLoadCurrentPercent = Math.Round(gHandler.localGridPowerUsagePercentage * 100);
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

			DrawText(powerLoadCurrentPercentString, size, _powerLoadCurrentPercentPosition, _powerLoadCurrentPercentDirection, color, brightness, 1f, false);
			DrawText(" 000 ", size, _powerLoadCurrentPercentPosition, _powerLoadCurrentPercentDirection, color, brightness, 0.333f, false);

			double powerSeconds = (double)gHandler.localGridPowerHoursRemaining * 3600;
			string powerSecondsS = "~" + FormatSecondsToReadableTime(powerSeconds);
			DrawText(powerSecondsS, _powerSecondsRemainingSizeModifier, _powerSecondsRemainingPosition, radarMatrix.Forward, gHandler.localGridControlledEntityCustomData.lineColorComp, brightness, 1, false);

			float powerPer = 60 * (gHandler.localGridPowerStored / gHandler.localGridPowerStoredMax);

			//----ARC----
			float arcLength = 56f*(float)gHandler.localGridPowerUsagePercentage;
			float arcLengthTime = 70f*(float)gHandler.localGridPowerUsagePercentage;
			DrawArc(worldRadarPos-radarMatrix.Up*0.0025, gHandler.localGridControlledEntityCustomData.radarRadius*1.27, radarMatrix.Up, 35, 35+arcLengthTime, color, 0.007f, 0.5f);
			DrawArc(worldRadarPos-radarMatrix.Up*0.0025, gHandler.localGridControlledEntityCustomData.radarRadius*1.27 + 0.01, radarMatrix.Up, 42, 42+powerPer, gHandler.localGridControlledEntityCustomData.lineColorComp, 0.002f, 0.75f);
		}

		
        /// <summary>
        /// This function draws the speed gauge on the HUD.
        /// </summary>
        private void DrawSpeed()
		{
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

			DrawText(PLS, size, _speedPosition, _speedDirection, gHandler.localGridControlledEntityCustomData.lineColor, gHandler.localGridControlledEntityCustomData.radarBrightness, 1f, false);
			DrawText("0000 ", size, _speedPosition, _speedDirection, gHandler.localGridControlledEntityCustomData.lineColor, gHandler.localGridControlledEntityCustomData.radarBrightness, 0.333f, false);

			//----ARC----
			float arcLength = (float)(gHandler.localGridSpeed/ gHandler.localGridMaxSpeed);
			arcLength = Clamped(arcLength, 0, 1);
			arcLength *= 70;
            
			DrawArc(worldRadarPos-radarMatrix.Up*0.0025, gHandler.localGridControlledEntityCustomData.radarRadius*1.27, radarMatrix.Up, 360-30-arcLength, 360-30, gHandler.localGridControlledEntityCustomData.lineColor, 0.007f, 0.5f);

			
			double powerSeconds = (double)gHandler.localGridHydrogenRemainingSeconds;
			string powerSecondsS = "@" + FormatSecondsToReadableTime(powerSeconds);
			Vector3D hydrogenSecondsRemainingPos = _hydrogenSecondsRemainingPosition + radarMatrix.Left * powerSecondsS.Length * _hydrogenSecondsRemainingSizeModifier * 1.8;
			DrawText(powerSecondsS, _hydrogenSecondsRemainingSizeModifier, hydrogenSecondsRemainingPos, radarMatrix.Forward, gHandler.localGridControlledEntityCustomData.lineColorComp, gHandler.localGridControlledEntityCustomData.radarBrightness, 1, false);

			float powerPer = 56f*gHandler.localGridHydrogenFillRatio;
			DrawArc(worldRadarPos-radarMatrix.Up*0.0025, gHandler.localGridControlledEntityCustomData.radarRadius*1.27 + 0.01, radarMatrix.Up, 360-37-powerPer, 360-37, gHandler.localGridControlledEntityCustomData.lineColorComp, 0.002f, 0.75f);
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
				for(int i = 0; i < dustList.Count; i++) {
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

        /// <summary>
        /// Draw the dust effect on screen.
        /// </summary>
		private void DrawDust()
		{
			if (dustList.Count > 0)
			{
				//update Dust
				for (int i = 0; i < dustList.Count; i++)
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

            // TODO check for third person and draw in front of camera instead of head if so.
            if (character != null)
            {
                // Extract the head matrix
                _headMatrix = character.GetHeadMatrix(true);

                // Get the head position from the head matrix
                _headPosition = _headMatrix.Translation;
                _headPosition -= gHandler.localGridControlledEntity.GetPosition();
            }
			//==============================================================


			// COCKPIT RADAR SCREEN POSITION AND OFFSET //
			// Apply the radar offset to the ship's position
			radarMatrix = gHandler.localGridControlledEntity.WorldMatrix; // Set the radar matrix = the localGrid controlled entity matrix, ie. the cockpit or control seat.
            worldRadarPos = Vector3D.Transform(gHandler.localGridControlledEntityCustomData.radarOffset, radarMatrix) + _headPosition;

           
            // Positions
            // Power Load / Power Remaining
            _powerLoadCurrentPercentPosition = worldRadarPos + (radarMatrix.Forward * gHandler.localGridControlledEntityCustomData.radarRadius * 0.4) + (radarMatrix.Left * gHandler.localGridControlledEntityCustomData.radarRadius * 1.3) + (radarMatrix.Up * 0.0085);
            _powerLoadCurrentPercentDirection = Vector3D.Normalize(_powerLoadCurrentPercentPosition - worldRadarPos);
            _powerLoadCurrentPercentDirection = Vector3D.Normalize((_powerLoadCurrentPercentDirection + radarMatrix.Forward) / 2);
            _powerSecondsRemainingPosition = worldRadarPos + radarMatrix.Left * gHandler.localGridControlledEntityCustomData.radarRadius * 0.88 + radarMatrix.Backward * gHandler.localGridControlledEntityCustomData.radarRadius * 1.1 + radarMatrix.Down * _powerSecondsRemainingSizeModifier * 2;


			// Speed / Hydrogen Remaining
            double speedSize = 0.0070;
            _speedPosition = worldRadarPos + (radarMatrix.Forward * gHandler.localGridControlledEntityCustomData.radarRadius * 0.4) + (radarMatrix.Right * gHandler.localGridControlledEntityCustomData.radarRadius * 1.3) + (radarMatrix.Up * 0.0085);
            _speedDirection = Vector3D.Normalize(_speedPosition - worldRadarPos);
            _speedDirection = Vector3D.Normalize((_speedDirection + radarMatrix.Forward) / 2);
            Vector3D left = Vector3D.Cross(radarMatrix.Up, _speedDirection);
            _speedPosition = (left * speedSize * 7) + _speedPosition;
			_hydrogenSecondsRemainingPosition = worldRadarPos + radarMatrix.Right * gHandler.localGridControlledEntityCustomData.radarRadius * 0.88 + radarMatrix.Backward * gHandler.localGridControlledEntityCustomData.radarRadius * 1.1 + radarMatrix.Down * _hydrogenSecondsRemainingSizeModifier * 2;

            // Radar scale/range
            _radarCurrentRangeTextPosition = _powerSecondsRemainingPosition + (radarMatrix.Down * gHandler.localGridControlledEntityCustomData.radarRadius * 0.1);

            // Holograms
            _hologramPositionRight = worldRadarPos + radarMatrix.Left * -gHandler.localGridControlledEntityCustomData.radarRadius + radarMatrix.Left * _hologramRightOffset_HardCode.X + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
            _hologramPositionLeft = worldRadarPos + radarMatrix.Left * -gHandler.localGridControlledEntityCustomData.radarRadius * -1 + radarMatrix.Left * _hologramRightOffset_HardCode.X * -1 + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
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
				MyPlanet planet = MyGamePruningStructure.GetClosestPlanet(gHandler.localGrid.GetPosition());
				Vector3D surfacePosition = planet.GetClosestSurfacePointGlobal(gHandler.localGrid.GetPosition());
				gHandler.localGridAltitude = Vector3D.Distance(gHandler.localGrid.GetPosition(), surfacePosition);
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
            //EvaluateGridAntennaStatus(gHandler.localGrid, out _playerHasPassiveRadar, out _playerHasActiveRadar, out _playerMaxPassiveRange, out _playerMaxActiveRange);

            // Radar distance/scale should be based on our highest functional, powered antenna (regardless of mode).
            localGridRadarScaleRange = gHandler.localGridMaxPassiveRadarRange; // Already limited to global max. Uses passive only because in active mode active and passive will be equal. 

            _currentTargetPositionReturned = new Vector3D(); // Will reuse this.
            if (gHandler.targetGrid != null && !gHandler.targetGrid.Closed)
            {
                // In here we will check if the player can still actively or passively target the current target and release them if not, or adjust range brackets accordingly if still targetted. 
                CanGridRadarDetectTarget(gHandler.localGridHasPassiveRadar, gHandler.localGridHasActiveRadar, gHandler.localGridMaxPassiveRadarRange, 
                    gHandler.localGridMaxActiveRadarRange, gHandler.localGrid.GetPosition(), out _playerCanDetectCurrentTarget, 
                    out _currentTargetPositionReturned, out _distanceToCurrentTargetSqr);

                VRage.Game.ModAPI.IMyCubeGrid targetGrid = gHandler.targetGrid as VRage.Game.ModAPI.IMyCubeGrid;
				if (targetGrid != null && _playerCanDetectCurrentTarget == true) // Added condition that we must be able to detect them otherwise, save CPU cycles checking blocks for power/batteries if we can't detect them anyway. 
                {
                    // Store whether radar ping is powered regardless of whether we only show powered or not, in case we want to do something like make the non powered
                    // grids darker and powered ones lighter. Then we check if _onlyPoweredGrids is true and we don't have power for current entity we skip it. 
                    if (gHandler.localGridControlledEntityCustomData.scannerOnlyPoweredGrids && !HasPowerProduction(targetGrid))
                    {
						_playerCanDetectCurrentTarget = false;
                    }
                }
                double distanceToCamera = Vector3D.DistanceSquared(_cameraPosition, _currentTargetPositionReturned); // Additional check on target distance to our CAMERA. 
                if (distanceToCamera > 50000d * 50000d)
                {
                    _playerCanDetectCurrentTarget = false;
                }
                if (_playerCanDetectCurrentTarget)
                {
                    _distanceToCurrentTarget = Math.Sqrt(_distanceToCurrentTargetSqr);
                    radarScaleRange_Goal = GetRadarScaleBracket(_distanceToCurrentTarget);
                    squishValue_Goal = 0.0;
                }
                else
                {
                    ReleaseTarget();
                    radarScaleRange_Goal = GetRadarScaleBracket(localGridRadarScaleRange);
                    squishValue_Goal = 0.75;
                }
            }
            else
            {
                radarScaleRange_Goal = GetRadarScaleBracket(localGridRadarScaleRange);
                squishValue_Goal = 0.75;
            }
            squishValue = LerpD(squishValue, squishValue_Goal, 0.1);

            // Handle smooth animation when first starting up or sitting in seat, along with smooth animation for switching range bracket due to targetting. 
            radarScaleRange_CurrentLogin = LerpD(radarScaleRange_CurrentLogin, radarScaleRange_GoalLogin, 0.01);
            radarScaleRange_Current = LerpD(radarScaleRange_Current, radarScaleRange_Goal, 0.1);
            localGridRadarScaleRange = radarScaleRange_Current * radarScaleRange_CurrentLogin;

            //radarScale = (gHandler.localGridControlledEntityCustomData.radarRadius / localGridRadarScaleRange);

            _fadeDistance = localGridRadarScaleRange * (1 - theSettings.fadeThreshold); // Eg at 0.01 would be 0.99%. For a radar distance of 20,000m this means the last 19800-19999 becomes fuzzy/dims the blip.
            _fadeDistanceSqr = _fadeDistance * _fadeDistance; // Sqr for fade distance in comparisons.
            _radarShownRangeSqr = localGridRadarScaleRange * localGridRadarScaleRange; // Anything over this range, even if within sensor range, can't be drawn on screen. 

            bool onlyPowered = gHandler.localGridControlledEntityCustomData.scannerOnlyPoweredGrids;
            if (onlyPowered)
            {
                // If main cockpit is set to filter out unpowered grids, check all holotables. If at least one is NOT set to filter out unpowered then we process them. 
                // If all possible radars are set to filter them out, THEN we can skip them to save cycles.
                foreach (HoloRadarCustomDataTerminalPair thePair in gHandler.localGridRadarTerminalsData.Values)
                {
                    HoloRadarCustomData theData = thePair.HoloRadarCustomData;
                    if (!theData.scannerOnlyPoweredGrids)
                    {
                        onlyPowered = false;
                    }
                }
            }

            int maxPingCheck = 0;
            List<VRage.ModAPI.IMyEntity> pingsPendingRemoval = new List<VRage.ModAPI.IMyEntity>();
            foreach (KeyValuePair<VRage.ModAPI.IMyEntity, RadarPing> entityPingPair in radarPings) 
            {
                maxPingCheck++;
                if (maxPingCheck > theSettings.maxPings) 
                {
                    break;
                }

                VRage.ModAPI.IMyEntity entity = entityPingPair.Key;
                if (entity == null)
                {
                    pingsPendingRemoval.Add(entity);
                    continue; // Clear the ping then continue
                }
                if (entity.GetTopMostParent() == gHandler.localGridControlledEntity.GetTopMostParent())
                {
                    continue; // Skip drawing yourself on the radar.
                }

                VRage.Game.ModAPI.IMyCubeGrid entityGrid = entity as VRage.Game.ModAPI.IMyCubeGrid;
                if (entityGrid != null)
                {
                    radarPings[entity].RadarPingHasPower = HasPowerProduction(entityGrid);
                    // Store whether radar ping is powered regardless of whether we only show powered or not, in case we want to do something like make the non powered
                    // grids darker and powered ones lighter. Then we check if _onlyPoweredGrids is true and we don't have power for current entity we skip it. 
                    if (onlyPowered && !radarPings[entity].RadarPingHasPower)
                    {
                        continue;
                    }
                }

                bool playerCanDetect = false;
                Vector3D entityPos;
                double entityDistanceSqr;
                bool entityHasActiveRadar = false;
                double entityMaxActiveRange = 0;

                CanGridRadarDetectEntity(gHandler.localGridHasPassiveRadar, gHandler.localGridHasActiveRadar, gHandler.localGridMaxPassiveRadarRange,
                    gHandler.localGridMaxActiveRadarRange, gHandler.localGrid.GetPosition(), entity, out playerCanDetect,
                    out entityPos, out entityDistanceSqr, out entityHasActiveRadar, out entityMaxActiveRange);

                radarPings[entity].PlayerCanDetect = playerCanDetect;
                radarPings[entity].RadarPingPosition = entityPos;
                radarPings[entity].RadarPingDistanceSqr = entityDistanceSqr;
                radarPings[entity].RadarPingHasActiveRadar = entityHasActiveRadar;
                radarPings[entity].RadarPingMaxActiveRange = entityMaxActiveRange;

                if (!playerCanDetect || entityDistanceSqr > _radarShownRangeSqr) // If can't detect it, or the radar scale prevents showing it move on.
                {
                    continue;
                }

                // Check if relationship status has changed and update ping (eg. if a ship flipped from neutral to hostile or hostile to friendly via capture). 
                UpdateExistingRadarPingStatus(entity);

                if (entityDistanceSqr < _radarShownRangeSqr * (1 - theSettings.fadeThreshold)) // Once we pass the fade threshold an auidible and visual cue should be applied. 
                {
                    if (!radarPings[entity].Announced)
                    {
                        radarPings[entity].Announced = true;
                        if (radarPings[entity].Status == RelationshipStatus.Hostile && entityDistanceSqr > 250000) //500^2 = 250,000
                        {
                            PlayCustomSound(SP_ENEMY, worldRadarPos);
                            NewAlertAnim(entity);
                        }
                        else if (radarPings[entity].Status == RelationshipStatus.Friendly && entityDistanceSqr > 250000)
                        {
                            PlayCustomSound(SP_NEUTRAL, worldRadarPos);
                            NewBlipAnim(entity);
                        }
                        else if (radarPings[entity].Status == RelationshipStatus.Neutral && entityDistanceSqr > 250000)
                        {
                            // No Sound
                        }
                    }
                }
                else
                {
                    if (radarPings[entity].Announced)
                    {
                        radarPings[entity].Announced = false;
                    }
                }
            }
            // Can't edit a collection while iterating through it, so we remove after if necessary.
            foreach (VRage.ModAPI.IMyEntity keyToRemove in pingsPendingRemoval) 
            {
                radarPings.Remove(keyToRemove);
            }
        }

        /// <summary>
        /// Lightweight update for when we aren't directly controlling a grid. 
        /// </summary>
        public void UpdateRadarDetectionsHoloTable()
        {
            IMyCharacter character = MyAPIGateway.Session.Player?.Character;
            if (character == null) //.....33154 Contribution from baby <3
            {
                return;
            }
            VRage.Game.ModAPI.IMyCubeGrid playerGrid = gHandler.localGrid;
            Vector3D playerGridPos = playerGrid.GetPosition();

            // Check radar active/passive status and broadcast range for player grid.
            // playerGrid is set once earlier in the Draw() method when determining if cockpit is eligible, player controlled etc, and is used to get power draw among other things. Saved for re-use here and elsewhere.
            //EvaluateGridAntennaStatus(playerGrid, out _playerHasPassiveRadar, out _playerHasActiveRadar, out _playerMaxPassiveRange, out _playerMaxActiveRange);

            // Radar distance/scale should be based on our highest functional, powered antenna (regardless of mode).
            localGridRadarScaleRange = gHandler.localGridMaxPassiveRadarRange; // Already limited to global max. Uses passive only because in active mode active and passive will be equal. 

            radarScaleRange_Goal = GetRadarScaleBracket(localGridRadarScaleRange);
            squishValue_Goal = 0.75;
            squishValue = LerpD(squishValue, squishValue_Goal, 0.1);

            // Handle smooth animation when first starting up or sitting in seat, along with smooth animation for switching range bracket due to targetting. 
            radarScaleRange_Current = LerpD(radarScaleRange_Current, radarScaleRange_Goal, 0.1);
            localGridRadarScaleRange = radarScaleRange_Current;

            _fadeDistance = localGridRadarScaleRange * (1 - theSettings.fadeThreshold); // Eg at 0.01 would be 0.99%. For a radar distance of 20,000m this means the last 19800-19999 becomes fuzzy/dims the blip.
            _fadeDistanceSqr = _fadeDistance * _fadeDistance; // Sqr for fade distance in comparisons.
            _radarShownRangeSqr = localGridRadarScaleRange * localGridRadarScaleRange; // Anything over this range, even if within sensor range, can't be drawn on screen. 

            bool onlyPowered = true;
            foreach (HoloRadarCustomDataTerminalPair thePair in gHandler.localGridRadarTerminalsData.Values)
            {
                HoloRadarCustomData theData = thePair.HoloRadarCustomData;
                if (!theData.scannerOnlyPoweredGrids)
                {
                    onlyPowered = false;
                }
            }

            int maxPingCheck = 0;
            List<VRage.ModAPI.IMyEntity> pingsPendingRemoval = new List<VRage.ModAPI.IMyEntity>();
            foreach (KeyValuePair<VRage.ModAPI.IMyEntity, RadarPing> entityPingPair in radarPings)
            {
                maxPingCheck++;
                if (maxPingCheck > theSettings.maxPings)
                {
                    break;
                }

                VRage.ModAPI.IMyEntity entity = entityPingPair.Key;
                if (entity == null)
                {
                    pingsPendingRemoval.Add(entity);
                    continue; // Clear the ping then continue
                }
                VRage.Game.ModAPI.IMyCubeGrid entityGrid = entity as VRage.Game.ModAPI.IMyCubeGrid;
                if (entityGrid != null)
                {
                    radarPings[entity].RadarPingHasPower = HasPowerProduction(entityGrid);
                    // Store whether radar ping is powered regardless of whether we only show powered or not, in case we want to do something like make the non powered
                    // grids darker and powered ones lighter. Then we check if _onlyPoweredGrids is true and we don't have power for current entity we skip it. 
                    if (onlyPowered && !radarPings[entity].RadarPingHasPower) // Only check power if at least one holo table is set to only show powered grids, to save cycles when not needed.
                    {
                        continue;
                    }
                }

                bool playerCanDetect = false;
                Vector3D entityPos;
                double entityDistanceSqr;
                bool entityHasActiveRadar = false;
                double entityMaxActiveRange = 0;

                CanGridRadarDetectEntity(gHandler.localGridHasPassiveRadar, gHandler.localGridHasActiveRadar, gHandler.localGridMaxPassiveRadarRange,
                    gHandler.localGridMaxActiveRadarRange, playerGridPos, entity, out playerCanDetect, out entityPos,
                    out entityDistanceSqr, out entityHasActiveRadar, out entityMaxActiveRange);

                radarPings[entity].PlayerCanDetect = playerCanDetect;
                radarPings[entity].RadarPingPosition = entityPos;
                radarPings[entity].RadarPingDistanceSqr = entityDistanceSqr;
                radarPings[entity].RadarPingHasActiveRadar = entityHasActiveRadar;
                radarPings[entity].RadarPingMaxActiveRange = entityMaxActiveRange;

                if (!playerCanDetect || entityDistanceSqr > _radarShownRangeSqr) // If can't detect it, or the radar scale prevents showing it move on.
                {
                    continue;
                }

                // Check if relationship status has changed and update ping (eg. if a ship flipped from neutral to hostile or hostile to friendly via capture). 
                UpdateExistingRadarPingStatus(entity);

                if (entityDistanceSqr < _radarShownRangeSqr * (1 - theSettings.fadeThreshold)) // Once we pass the fade threshold an auidible and visual cue should be applied. 
                {
                    if (!radarPings[entity].Announced)
                    {
                        radarPings[entity].Announced = true;
                        if (radarPings[entity].Status == RelationshipStatus.Hostile && entityDistanceSqr > 250000) //500^2 = 250,000
                        {
                            foreach (Sandbox.ModAPI.IMyTerminalBlock holoTable in gHandler.localGridRadarTerminals.Values)
                            {
                                PlayCustomSound(SP_ENEMY, holoTable.GetPosition());
                            }
                            NewAlertAnim(entity);
                        }
                        else if (radarPings[entity].Status == RelationshipStatus.Friendly && entityDistanceSqr > 250000)
                        {
                            foreach (Sandbox.ModAPI.IMyTerminalBlock holoTable in gHandler.localGridRadarTerminals.Values)
                            {
                                PlayCustomSound(SP_NEUTRAL, holoTable.GetPosition());
                            }
                            NewBlipAnim(entity);
                        }
                        else if (radarPings[entity].Status == RelationshipStatus.Neutral && entityDistanceSqr > 250000)
                        {
                            // No Sound
                        }
                    }
                }
                else
                {
                    if (radarPings[entity].Announced)
                    {
                        radarPings[entity].Announced = false;
                    }
                }

            }
            // Can't edit a collection while iterating through it, so we remove after if necessary.
            foreach (VRage.ModAPI.IMyEntity keyToRemove in pingsPendingRemoval)
            {
                radarPings.Remove(keyToRemove);
            }
        }

        

        /// <summary>
        /// Update values for the Velocity Lines.
        /// </summary>
        public void UpdateVelocityLines() 
		{
            ControlDimmer += 0.05f;
            SpeedDimmer = Clamped((float)gHandler.localGridSpeed * 0.01f + 0.05f, 0f, 1f);

            SpeedDimmer = (float)(Math.Pow(SpeedDimmer, 2));

            SpeedDimmer = MathHelper.Clamp(Remap((float)gHandler.localGridSpeed, (gHandler.localGridControlledEntityCustomData.velocityLineSpeedThreshold * 0.75f), gHandler.localGridControlledEntityCustomData.velocityLineSpeedThreshold, 0f, 1f), 0f, 1f);
            ControlDimmer = Clamped(ControlDimmer, 0f, 1f);
            OrbitDimmer = ControlDimmer * SpeedDimmer;

            if (OrbitDimmer > 0.01f)
            {
                //Get Sun direction
                if (theSettings.starFollowSky)
                {
                    MyOrientation sunOrientation = GetSunOrientation();

                    Quaternion sunRotation = sunOrientation.ToQuaternion();
                    Vector3D sunForwardDirection = Vector3D.Transform(Vector3D.Forward, sunRotation);

                    theSettings.starPos = new SerializableVector3D(sunForwardDirection * 100000000);
                }
            }
        }

        /// <summary>
        /// Draw velocity lines and planet orbits if applicable.
        /// </summary>
		public void DrawVelocityLinesAndPlanetOrbits()
		{
            if (theSettings.enableVelocityLines && gHandler.localGridControlledEntityCustomData.enableVelocityLines && (float)gHandler.localGridSpeed > gHandler.localGridControlledEntityCustomData.velocityLineSpeedThreshold)
            {
                //DrawSpeedGaugeLines(gHandler.localGridControlledEntity, gHandler.localGridVelocity); // Original author disabled this function
                UpdateAndDrawVerticalSegments(gHandler.localGridControlledEntity, gHandler.localGridVelocity);
            }

            if (theSettings.enablePlanetOrbits && gHandler.localGridControlledEntityCustomData.enablePlanetOrbits && (float)gHandler.localGridSpeed > gHandler.localGridControlledEntityCustomData.planetOrbitSpeedThreshold && OrbitDimmer > 0.01f)
            {
                //Draw orbit lines
                foreach (PlanetInfo planetInfo in planetListDetails)
                {
                    // Cast the entity to IMyPlanet
                    MyPlanet planet = (MyPlanet)planetInfo.Entity;
                    Vector3D parentPos = (planetInfo.ParentEntity != null) ? planetInfo.ParentEntity.GetPosition() : theSettings.starPos.ToVector3D();

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
			
        }

        //------------ A F T E R ------------------
		/// <summary>
		/// UpdateAfterSimulation should be used to handle anything that requires the results of physics simulation. Playing sounds for eg. or updating a variable that tracks damage (UI updates that depnd on the result of physics simulation).
		/// </summary>
        public override void UpdateAfterSimulation()
		{
            _thePlayer = MyAPIGateway.Session?.LocalHumanPlayer;
            if (_thePlayer == null)
            {
                // This is only null for dedicated servers, prevents execution on dedicated servers
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

            if (gHandler == null) 
            {
                gHandler = new GridHelper();
            }

            if (gHandler != null)
            {
                gHandler.theSettings = theSettings; // Keep settings in sync
                gHandler.CheckForLocalGrid(); // Check if we are controlling or near enough to a grid to set the localGrid to it.
                gHandler.UpdateLocalGrid(); // Always update current grid each tick
            }

            // If localGrid is not initialized or unpowered then we can skip all of this. 
            if (gHandler.localGrid != null && gHandler.localGridInitialized && gHandler.localGridHasPower) 
            {

                if ((gHandler.localGridControlledEntity != null && gHandler.localGridControlledEntityInitialized 
                    && gHandler.localGridControlledEntityCustomData.masterEnabled) || gHandler.localGridRadarTerminals.Count > 0)
                {
                    // Only update radar detections if there is a valid Radar (either seated hud or holo table radar)
                    if (gHandler.localGridControlledEntity == null || !gHandler.localGridControlledEntityInitialized)
                    {
                        // Can use the lighter weight detections logic for just holo tables when not controlling a grid directly. 
                        UpdateRadarDetectionsHoloTable();
                    }
                    else 
                    {
                        UpdateRadarDetections();
                    }    
                }

                // We can always render holo tables if initialized, and maybe a HUD too if in a controlled block properly enabled and tagged. 
                if (gHandler.localGridControlledEntity != null && gHandler.localGridControlledEntityInitialized && gHandler.localGridControlledEntityCustomData.masterEnabled)
                {
                    // We can update/draw the HUD
                    if (!isSeated)
                    {
                        //Seated Event!
                        OnSitDown();
                    }

                    CheckPlayerInput();

                    if (theSettings.enableVelocityLines && gHandler.localGridControlledEntityCustomData.enableVelocityLines && (float)gHandler.localGridSpeed > gHandler.localGridControlledEntityCustomData.velocityLineSpeedThreshold)
                    {
                        UpdateVelocityLines();
                    }
                    if (theSettings.enableCockpitDust)
                    {
                        UpdateDust();
                    }
                    if (gHandler.localGridControlledEntityCustomData.enableMoney)
                    {
                        UpdateCredits();
                    }
                    if (gHandler.localGridControlledEntityCustomData.enableToolbars)
                    {
                        UpdateToolbars();
                    }
                }
                else if (gHandler.localGridControlledEntity == null || !gHandler.localGridControlledEntityInitialized)
                {
                    if (isSeated) 
                    {
                        OnStandUp();
                    }
                }
            }
        }


        //------------ D R A W -----------------
        /// <summary>
        /// Draw runs many times per tick (your FPS). But rendering can be skipped and it runs at different rates. We should not be updating game states here. No updating variables, no complex math calculations etc. This is for rendering only. 
        /// Heavy calculations in here will affect your framerate. 
        /// </summary>
        public override void Draw()
		{
			base.Draw();

            _thePlayer = MyAPIGateway.Session?.LocalHumanPlayer;
            if (_thePlayer == null)
            {
                // This is only null for dedicated servers, prevents execution on dedicated servers
                return;
            }

            // Debug log writes every 360 frames to reduce log spam
            if (debugFrameCount >= 360)
            {
                debug = true;
                debugFrameCount = 0;
            }
            else
            {
                debug = false;
                debugFrameCount++;
            }

            _isFirstPerson = MyAPIGateway.Session.CameraController.IsInFirstPersonView;
            UpdateCameraPosition();

            // Draw grid flares (glint) behind grids (if enabled). 
            if (theSettings.enableGridFlares) 
            {
                // Draw grid flares (glint) behind grids (if enabled). 
                DrawGridFlare();
            }
            if (theSettings.enableVisor && _isFirstPerson) 
            {
                // Update visor effects (if enabled)
                DrawVisor();
            }

            // If localGrid is not initialized or unpowered then we can skip all of this. 
            if (gHandler.localGrid != null && gHandler.localGridInitialized && gHandler.localGridHasPower)
            {
                // We can always render holo tables if initialized, and maybe a HUD too if in a controlled block properly enabled and tagged. 
                if (gHandler.localGridControlledEntity != null && gHandler.localGridControlledEntityInitialized && gHandler.localGridControlledEntityCustomData.masterEnabled)
                {
                    UpdateUIPositions(); // Update the positions of the UI elements based on the current grid and camera position.

                    DrawVelocityLinesAndPlanetOrbits();

                    DrawRadar();

                    if (theSettings.enableHologramsGlobal && gHandler.localGridControlledEntityCustomData.enableHolograms)
                    {
                        // Update the final rotation matrix that will be re-used for each block in draw, takes a view rotation updated only in UpdateAfterSimulation and multiplies
                        // it by the current grid world matrix. Call this in Draw() so it uses the latest worldMatrix for the grid(s). But the view matrix can lag behind without risk. 
                        DrawHolograms();
                    }
                    if (theSettings.enableCockpitDust)
                    {
                        DrawDust();
                    }
                    if (gHandler.localGridControlledEntityCustomData.enableGauges) 
                    {
                        DrawGauges();
                    }
                    if (gHandler.localGridControlledEntityCustomData.enableMoney)
                    {
                        DrawCredits();
                    }
                    if (gHandler.localGridControlledEntityCustomData.enableToolbars)
                    {
                        DrawToolbars();
                    }
                }
                if (gHandler.localGridHologramTerminals.Count > 0)
                {
                    if (!(gHandler.isPlayerControlling && !theSettings.renderHoloHologramInSeat))
                    {
                        // If on a grid not in a seat, or in a seat and drawing not disabled when in seat
                        DrawHologramsHoloTables();
                    }
                }
                if (gHandler.localGridRadarTerminals.Count > 0)
                {
                    if (!(gHandler.isPlayerControlling && !theSettings.renderHoloRadarsInSeat))
                    {
                        // If on a grid, or in a seat and not disabled
                        DrawRadarHoloTable();
                    }
                }
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
			List<Sandbox.ModAPI.IMyBatteryBlock> batteries = new List<Sandbox.ModAPI.IMyBatteryBlock>();
            MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid)?.GetBlocksOfType(producers);
			MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid)?.GetBlocksOfType(batteries);

            foreach (Sandbox.ModAPI.IMyPowerProducer producer in producers)
            {
                if (producer.IsWorking && producer.Enabled && producer.CurrentOutput > 0.01f)
                {
                    return true;
                }
            }
			foreach (Sandbox.ModAPI.IMyBatteryBlock battery in batteries)
			{
				if (battery.IsWorking && battery.Enabled && battery.CurrentStoredPower > 0.01f)
				{
					return true;
				}
			}
            return false;
        }

        //ACTIVATION=========================================================================
        private double visorLife = 0;
		private bool visorDown = false;

        /// <summary>
        /// Draw the visor effect if the player has a helmet closed/open.
        /// </summary>
		private void DrawVisor()
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

        /// <summary>
        /// Does the player have their helmet open or closed?
        /// </summary>
        /// <returns></returns>
		private bool IsPlayerHelmetOn()
		{
			IMyPlayer player = MyAPIGateway.Session?.LocalHumanPlayer;
			if (player == null)
			{
				return false;
			}

			VRage.Game.ModAPI.Interfaces.IMyControllableEntity controlledEntity = player.Controller?.ControlledEntity;
			if (controlledEntity == null)
			{
				return false;
			}

            // Check if the controlled entity is a character
            IMyCharacter character = controlledEntity.Entity as IMyCharacter;
			if (character != null)
			{
				return character.EnabledHelmet;
			}

            // Check if the controlled entity is a cockpit
            Sandbox.ModAPI.IMyCockpit cockpit = controlledEntity.Entity as Sandbox.ModAPI.IMyCockpit;
			if (cockpit != null && cockpit.Pilot != null)
			{
				return cockpit.Pilot.EnabledHelmet;
			}

            // Check if the controlled entity is a turret or remote control
            Sandbox.ModAPI.IMyLargeTurretBase turret = controlledEntity.Entity as Sandbox.ModAPI.IMyLargeTurretBase;
			if (turret != null)
			{
				return false;
			}

            Sandbox.ModAPI.IMyRemoteControl remoteControl = controlledEntity.Entity as Sandbox.ModAPI.IMyRemoteControl;
			if (remoteControl != null && remoteControl.Pilot != null)
			{
				return remoteControl.Pilot.EnabledHelmet;
			}

			// Default to false if no valid controlled entity is found
			return false;
		}

        /// <summary>
        /// Handle the events on entering a control seat
        /// </summary>
		private void OnSitDown()
		{
			isSeated = true;
            radarScaleRange_CurrentLogin = 0.001;
            gHandler.localGridGlitchAmountOverload = 0.25;
            PlayCustomSound(SP_BOOTUP, worldRadarPos);
		}

        /// <summary>
        /// Handle the events on exiting a control seat
        /// </summary>
		private void OnStandUp()
		{
			isSeated = false;
			TB_activationTime = 0;
            ReleaseTarget();
			DeleteDust();
		}

		//-----------------------------------------------------------------------------------
		//Stolen from the Rotate With Skybox Mod mod=========================================
		public MyOrientation GetSunOrientation()
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

        /// <summary>
        /// Gathers planet info from a set of filtered entitites
        /// </summary>
        /// <returns></returns>
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
			foreach (VRage.ModAPI.IMyEntity entity in entities)
			{
				planets.Add(entity);
			}
			return planets;
		}

        /// <summary>
        /// Subscribe to OnEntityAdd/OnEntityRemoved events so new entities added/removed from the game get picked up after initialization.
        /// </summary>
		void SubscribeToEvents()
		{
			// Subscribe to the OnEntityAdd event
			MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
			MyAPIGateway.Entities.OnEntityRemove += OnEntityRemove;
		}

        /// <summary>
        /// Unsubscribes from OnEntityAdd/OnEntityRemoved events
        /// </summary>
		void UnsubscribeFromEvents()
		{
			// Unsubscribe from the OnEntityAdd event
			MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
			MyAPIGateway.Entities.OnEntityRemove -= OnEntityRemove;
		}


        /// <summary>
        /// Subscribe to OnEntityAdd event so new entities added from the game get picked up after initialization.
        /// </summary>
        /// <param name="entity"></param>
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

			if (entity == null || entity.MarkedForClose || entity.Closed || entity is MyPlanet)
			{
				// Only add to entities OR pings if valid.
				return;
			}
			else 
			{

                VRage.Game.ModAPI.IMyCubeGrid gridEntity = entity as VRage.Game.ModAPI.IMyCubeGrid;

                // If it is a grid entity we get topMostParent and check if block count is within our limit to "detect" 
                // Helps reduce junk AND subgrids. 
                if (gridEntity != null)
                {
                    VRage.Game.ModAPI.IMyCubeGrid parent = entity.GetTopMostParent() as VRage.Game.ModAPI.IMyCubeGrid;
                    if (parent != null)
                    {
                        List<VRage.Game.ModAPI.IMySlimBlock> tempBlocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
                        parent.GetBlocks(tempBlocks);
                        if (tempBlocks.Count >= theSettings.minTargetBlocksCount)
                        {
                            VRage.ModAPI.IMyEntity parentEntity = parent as VRage.ModAPI.IMyEntity;
                            RadarPing newPing = NewRadarPing(parentEntity);

                            // Only add if width is valid
                            if (newPing.Width > 0.00001f && newPing.Width <= 4f) // safety bounds
                            {
                                if (!radarPings.ContainsKey(parentEntity))
                                {
                                    radarPings.Add(parentEntity, newPing);
                                }
                            }
                        }
                    }   
                }
                else
                {
                    RadarPing newPing = NewRadarPing(entity);

                    // Only add if width is valid
                    if (newPing.Width > 0.00001f && newPing.Width <= 4f) // safety bounds
                    {
                        if (!radarPings.ContainsKey(entity))
                        {
                            radarPings.Add(entity, newPing);
                        }
                    }
                }
            }
			
		}

        /// <summary>
        /// Subscribe to OnEntityRemoved event so new entities removed from the game get picked up after initialization.
        /// </summary>
        /// <param name="entity"></param>
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
                radarPings.Remove(entity);
			}
		}
		//-----------------------------------------------------------------------------------



		//DRAW LINES=========================================================================
		/// <summary>
		/// If grid flares are enabled draws a flare on screen so they show up better in the void. 
		/// </summary>
		void DrawGridFlare()
		{
            MatrixD cameraMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            Vector3 viewUp = cameraMatrix.Up;
            Vector3 viewLeft = cameraMatrix.Left;
            foreach (VRage.ModAPI.IMyEntity entity in radarPings.Keys)
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

        /// <summary>
        /// Draws a line billboard with some glitch effect if enabled.
        /// </summary>
        /// <param name="material"></param>
        /// <param name="color"></param>
        /// <param name="origin"></param>
        /// <param name="directionNormalized"></param>
        /// <param name="length"></param>
        /// <param name="thickness"></param>
        /// <param name="blendType"></param>
        /// <param name="customViewProjection"></param>
        /// <param name="intensity"></param>
        /// <param name="persistentBillboards"></param>
		void DrawLineBillboard(MyStringId material, Vector4 color, Vector3D origin, Vector3 directionNormalized, float length, float thickness, BlendTypeEnum blendType = 0, int customViewProjection = -1, float intensity = 1, List<VRageRender.MyBillboard> persistentBillboards = null)
		{
			if (GetRandomBoolean()) 
			{
				if (gHandler.localGridGlitchAmount > 0.001) 
				{
					float glitchValue = (float)gHandler.localGridGlitchAmount;

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

        /// <summary>
        /// Draws a circle in 3D space given a center, radius, and plane direction.
        /// </summary>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <param name="planeDirection"></param>
        /// <param name="color"></param>
        /// <param name="brightness"></param>
        /// <param name="dotted"></param>
        /// <param name="isOrbit"></param>
        /// <param name="dimmerOverride"></param>
        /// <param name="thicknessOverride"></param>
        void DrawCircle(Vector3D center, double radius, Vector3 planeDirection, Vector4 color, float brightness, bool dotted = false, bool isOrbit = false, float dimmerOverride = 0f, 
            float thicknessOverride = 0f)
		{
            // Define circle parameters
            Vector3D circleCenter = center;   // Position of the center of the circle
			double circleRadius = radius;        // Radius of the circle
			int segments = theSettings.lineDetail;                 // Number of segments to approximate the circle
			bool dotFlipper = true;

			//Lets adjust the tesselation based on radius instead of an arbitrary value.
			int segmentsInterval = (int)Math.Round(Remap((float)circleRadius,1000, 10000000, 1, 8));
			segments = segments * (int)Clamped((float)segmentsInterval, 1, 16);

			float lineLength = 1.01f;           // Length of each line segment
			float lineThickness = theSettings.lineThickness;        // Thickness of the lines

			//Lets adjust the line thickness based on screen resolution so that it doesn't get to pixelated.
			lineThickness = ((float)GetScreenHeight()/1080f)*lineThickness;

			BlendTypeEnum blendType = BlendTypeEnum.Standard;  // Blend type for rendering
			Vector4 lineColor = color * brightness;  // Color of the lines

			Vector3D cameraPosition = MyAPIGateway.Session.Camera.Position;  // Position of the camera

            // Calculate points on the circle
            Vector3D[] circlePoints = new Vector3D[segments];  // Array to hold points on the circle
			double angleIncrement = 2 * Math.PI / segments;   // Angle increment for each segment

			// Normalize the plane direction vector
			planeDirection.Normalize();

			// Calculate the rotation matrix based on the plane direction
			MatrixD rotationMatrix = MatrixD.CreateFromDir(planeDirection);

            // Generate points on the circle
            for (int i = 0; i < segments; i++)
			{
				double angle = angleIncrement * i;
				double x = circleRadius * Math.Cos(angle);
				double y = circleRadius * Math.Sin(angle);

				// Apply rotation transformation to the point
				Vector3D point = new Vector3D(x, y, 0);
				point = Vector3D.Transform(point, rotationMatrix);

				// Translate the point to the planet's position
				point += circleCenter;

				circlePoints[i] = point;  // Store the transformed point
			}

			int count = circlePoints.Length;

			// Draw lines between adjacent points on the circle to form the circle
			for (int i = 0; i < segments; i++)
			{
				Vector3D point1 = circlePoints[i];
				Vector3D point2 = circlePoints[(i + 1) % count];  // Wrap around to the beginning
				Vector3 direction = (point2 - point1);  // Direction vector of the line segment

				Vector3D normalizedPoint1 = Vector3D.Normalize(point1-center);
				double dotPoint1 = Vector3D.Dot(normalizedPoint1, MyAPIGateway.Session.Camera.WorldMatrix.Backward);
				dotPoint1 = RemapD(dotPoint1, -1, 1, 0.25, 1);
				dotPoint1 = ClampedD(dotPoint1, 0.1, 1);

				// Calculate camera distance from whole segment.
				float distanceToSegment = DistanceToLineSegment(cameraPosition, point1, point2);

                float segmentThickness = 1f;
                float dimmer = 1f;

                if (isOrbit)
                {
                    // Calculate the segment thickness based on distance from camera.
                    segmentThickness = Math.Max(Remap(distanceToSegment, 1000f, 1000000f, 0f, 1000f) * lineThickness, 0f);

                    // Calculate the segment brightness based on distance from camera.
                    dimmer = Clamped(Remap(distanceToSegment, -10000f, 10000000f, 1f, 0f), 0f, 1f) * 1f;
                    dimmer *= OrbitDimmer;
                }
                else 
                {
                    segmentThickness = thicknessOverride;
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

        /// <summary>
        /// Draws an outline around a planet using a circle, will be dotted.
        /// </summary>
        /// <param name="entity"></param>
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
			DrawCircle(planetPosition, (float)planetRadius, aimDirection, gHandler.localGridControlledEntityCustomData.lineColor, 
                gHandler.localGridControlledEntityCustomData.radarBrightness, true, true);
		}

        /// <summary>
        /// Fake an orbit line for a planet (since there are no orbits)
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="parentPosition"></param>
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
			DrawCircle(parentPosition, (float)orbitRadius, orbitDirection, gHandler.localGridControlledEntityCustomData.lineColor,
                gHandler.localGridControlledEntityCustomData.radarBrightness, false, true);
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

        /// <summary>
        /// Draw a line segment with thickness and distance-based dimming
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
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
				DrawLineBillboard(Material, gHandler.localGridControlledEntityCustomData.lineColor * gHandler.localGridControlledEntityCustomData.radarBrightness * dimmer, 
                    start, Vector3D.Normalize(end - start), segmentLength, segmentThickness, blendType);
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

        /// <summary>
        /// Update and draw the vertical line segments for the grids current velocity vector
        /// </summary>
        /// <param name="gridEntity"></param>
        /// <param name="velocity"></param>
		private void UpdateAndDrawVerticalSegments(VRage.ModAPI.IMyEntity gridEntity, Vector3D velocity)
		{
			float segmentSpeedFactor = 0.01f;	// Factor to adjust responsiveness of segment spacing to speed changes
			float totalLineLength = 100f;		// Total length along which to place line segments
			float segmentLength = 1f;			// Length of each line segment
			float lineThickness = 0.05f;		// Thickness of the lines
			int totalNumLines = 10;             // Nmber of line segments active at a time.

            Sandbox.ModAPI.IMyCockpit cockpit = gridEntity as Sandbox.ModAPI.IMyCockpit;
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

			foreach (Vector3D segmentPosition in verticalSegments)
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
                Color theColor = gHandler.localGridControlledEntityCustomData.lineColor * gHandler.localGridControlledEntityCustomData.radarBrightness;
				if (dimmer > 0.01f) 
				{
					zeroBase = (leftBase + segmentPosition - direction * totalLineLength / 2);
					zeroBase = (segmentLength * 0.5 * -segmentUp) + zeroBase;
					DrawLineBillboard(Material, theColor * dimmer, zeroBase, segmentUp, segmentLength, segmentThickness);

					zeroBase = (rightBase + segmentPosition - direction * totalLineLength / 2);
					zeroBase = (segmentLength * 0.5 * -segmentUp) + zeroBase;
					DrawLineBillboard(Material, theColor * dimmer, zeroBase, segmentUp, segmentLength, segmentThickness);
				}
			}
		}

        /// <summary>
        /// Draw a quad on screen
        /// </summary>
        /// <param name="position"></param>
        /// <param name="normal"></param>
        /// <param name="radius"></param>
        /// <param name="materialId"></param>
        /// <param name="color"></param>
        /// <param name="mode"></param>
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
				if (gHandler.localGridGlitchAmount > 0.001) 
				{
					float glitchValue = (float)gHandler.localGridGlitchAmount;

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

        /// <summary>
        /// Draw a quad on screen with rigid up direction
        /// </summary>
        /// <param name="position"></param>
        /// <param name="normal"></param>
        /// <param name="radius"></param>
        /// <param name="materialId"></param>
        /// <param name="color"></param>
        /// <param name="upward"></param>
		public void DrawQuadRigid(Vector3D position, Vector3D normal, double radius, MyStringId materialId, Vector4 color, bool upward = false)
		{
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
				if (gHandler.localGridGlitchAmount > 0.001) 
				{
					float glitchValue = (float)gHandler.localGridGlitchAmount;

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
        /// <summary>
        /// Calculate the gravitational range of a planet
        /// </summary>
        /// <param name="radius"></param>
        /// <returns></returns>
		private double CalculateGravitationalRange(double radius)
		{
			return radius * GravitationalRangeScaleFactor;
		}

        /// <summary>
        /// Gather planet info from a planet list
        /// </summary>
        /// <param name="planetList"></param>
        /// <returns></returns>
		public List<PlanetInfo> GatherPlanetInfo(HashSet<VRage.ModAPI.IMyEntity> planetList)
		{
			List<PlanetInfo> planetInfoList = new List<PlanetInfo>();

			planetInfoList = OrderPlanetsBySize(planetList);
			planetInfoList = DetermineParentChildRelationships(planetInfoList);

			return planetInfoList;
		}

        /// <summary>
        /// Order planets by size (radius) in descending order
        /// </summary>
        /// <param name="planetList"></param>
        /// <returns></returns>
		public List<PlanetInfo> OrderPlanetsBySize(HashSet<VRage.ModAPI.IMyEntity> planetList)
		{
			// Create a list to hold planet information
			List<PlanetInfo> planetInfoList = new List<PlanetInfo>();

			// Iterate through each planet in the planet list
			foreach (VRage.ModAPI.IMyEntity entity in planetList)
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

        /// <summary>
        /// Determine parent-child relationships between planets based on gravitational ranges and sizes
        /// </summary>
        /// <param name="orderedPlanets"></param>
        /// <returns></returns>
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
        /// <summary>
        /// Clamp a float between a min and max value
        /// </summary>
        /// <param name="value"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
		float Clamped(float value, float min, float max)
		{
			if (value < min)
				return min;
			else if (value > max)
				return max;
			else
				return value;
		}

        /// <summary>
        /// Clamp a double between a min and max value
        /// </summary>
        /// <param name="value"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
		double ClampedD(double value, double min, double max)
		{
			if (value < min)
				return min;
			else if (value > max)
				return max;
			else
				return value;
		}

        /// <summary>
        /// Remap a float from one range to another. eg. remap 5 from a range of 0-10 to a range of 0-100 would return 50. 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="fromLow"></param>
        /// <param name="fromHigh"></param>
        /// <param name="toLow"></param>
        /// <param name="toHigh"></param>
        /// <returns></returns>
		float Remap(float value, float fromLow, float fromHigh, float toLow, float toHigh)
		{
			return toLow + (value - fromLow) * (toHigh - toLow) / (fromHigh - fromLow);
		}

        /// <summary>
        /// Remap a double from one range to another. eg. remap 5 from a range of 0-10 to a range of 0-100 would return 50. 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="fromLow"></param>
        /// <param name="fromHigh"></param>
        /// <param name="toLow"></param>
        /// <param name="toHigh"></param>
        /// <returns></returns>
		double RemapD(double value, double fromLow, double fromHigh, double toLow, double toHigh)
		{
			return toLow + (value - fromLow) * (toHigh - toLow) / (fromHigh - fromLow);
		}
	    
        /// <summary>
        /// Get the direction toward the camera from a given position.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Calculate a new direction vector aligned to the upward axis of a direction specified. By taking the direction+stable world down 
        /// </summary>
        /// <param name="newDirection"></param>
        /// <returns></returns>
		Vector3D AlignAxis(Vector3D newDirection)
		{
			// Calculate the rotation matrix to align the upward axis with the new direction
			MatrixD rotationMatrix = MatrixD.CreateFromDir(newDirection, Vector3D.Down);
			newDirection = Vector3D.Transform(newDirection, rotationMatrix);

			return newDirection;
		}

        /// <summary>
        /// Get the screen width
        /// </summary>
        /// <returns></returns>
		int GetScreenWidth()
		{
			return MyAPIGateway.Session.Config.ScreenWidth ?? 1920;
		}

        /// <summary>
        /// Get the screen height
        /// </summary>
        /// <returns></returns>
		int GetScreenHeight()
		{
			return MyAPIGateway.Session.Config.ScreenHeight ?? 1080;
		}

        /// <summary>
        /// Calculate the shortest distance from a point to a line segment defined by two endpoints.
        /// </summary>
        /// <param name="cameraPosition"></param>
        /// <param name="segmentStart"></param>
        /// <param name="segmentEnd"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Smoothly linearly interpolate between two double values based on a rate of interoplation
        /// </summary>
        /// <param name="currentDouble"></param>
        /// <param name="goalDouble"></param>
        /// <param name="rate"></param>
        /// <returns></returns>
		double LerpD(double currentDouble, double goalDouble, double rate)
		{
			return currentDouble + (goalDouble - currentDouble) * rate;
		}

        /// <summary>
        /// Smoothly linearly interpolate between two float values based on a rate of interoplation
        /// </summary>
        /// <param name="currentFloat"></param>
        /// <param name="goalFloat"></param>
        /// <param name="rate"></param>
        /// <returns></returns>
		float LerpF(float currentFloat, float goalFloat, float rate)
		{
			return currentFloat + (goalFloat - currentFloat) * rate;
		}
        //-----------------------------------------------------------------------------------



       


        public double maxRadarRange = 50000; // Set by config to draw distance or value specified in config. This is a fallback value. 

		// Radar scale range. Ie. how zoomed in or out is the radar screen. 
		public double localGridRadarScaleRange = 5000;
		public double radarScaleRange_Goal = 5000;
		public double radarScaleRange_Current = 5000;

		public double radarScaleRange_GoalLogin = 1;
		public double radarScaleRange_CurrentLogin = 0.01;

		//public static double radarScale = 0.000025;
		public static float targetHologramRadius = 0.125f;
        //public static double holoRadarScale = 0.0001;
        //public static float holoRadarRadius = 1f;

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
        //public Vector3D radarOffset = new Vector3D(0, -0.20, -0.575);

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

        /// <summary>
        /// Get the scale range bracket that our current radar range fits into. This is a max value. For eg. if our range is 325 and our bracket is at 500 we return 500.
        /// </summary>
        /// <param name="rangeToBracket"></param>
        /// <returns></returns>
        public int GetRadarScaleBracket(double rangeToBracket) 
		{
            // Add buffer to encourage zooming out slightly beyond the target
            double bufferedDistance = rangeToBracket + rangeBracketBuffer;
            int bracketedRange = ((int)(bufferedDistance / theSettings.rangeBracketDistance) + 1) * theSettings.rangeBracketDistance;

			// Clamp to the user-defined max radar range
			return Math.Min(bracketedRange, (int)maxRadarRange);
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

			

            // Okay FenixPK is pretty sure the "problems" I've been having with vectors, matrices, and rotations and perspective etc. are all coming from the fact that the holograms share a position vector and matrix with the radar which needs to rotate.
            // So we will split this into three positions, three offsets. And it solves a request where users wanted to move all of this stuff around independently too. If I can finish it in time haha.

            // Fetch the player's cockpit entity and it's world position (ie. the block the player is controlling). 
            Vector3D playerGridControlledEntityPosition = gHandler.localGridControlledEntity.GetPosition();

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

            Vector4 lineColor = gHandler.localGridControlledEntityCustomData.lineColor;
            float radarBrightness = gHandler.localGridControlledEntityCustomData.radarBrightness;

            // Can always show our own hologram
            if (theSettings.enableHologramsGlobal && gHandler.localGridControlledEntityCustomData.enableHolograms && gHandler.localGridControlledEntityCustomData.enableHologramsLocalGrid)
            {
                double lockInTime_Right = gHandler.localGridHologramActivationTime;
                lockInTime_Right = ClampedD(lockInTime_Right, 0, 1);
                lockInTime_Right = Math.Pow(lockInTime_Right, 0.1);

                DrawQuad(_hologramPositionRight + (radarUp * _hologramRightOffset_HardCode.Y * 0.5), viewBackward, 0.15, MaterialCircleSeeThroughAdd, lineColor * 0.125f, true);

                DrawCircle(_hologramPositionRight, 0.04 * lockInTime_Right, radarUp, lineColor, radarBrightness, false, false, 0.5f, 0.00075f);
                DrawCircle(_hologramPositionRight - (radarUp * 0.015), 0.045, radarUp, lineColor, radarBrightness, false, false, 0.25f, 0.00125f);

                DrawQuad(_hologramPositionRight + (radarUp * _hologramRightOffset_HardCode.Y), viewBackward, 0.1, MaterialCircleSeeThrough, new Vector4(0, 0, 0, 0.75f)); //Dim Backing
            }

            if (theSettings.enableHologramsGlobal && gHandler.localGridControlledEntityCustomData.enableHolograms && gHandler.localGridControlledEntityCustomData.enableHologramsTargetGrid)
            {
                double lockInTime_Left = gHandler.targetGridHologramActivationTime;
                lockInTime_Left = ClampedD(lockInTime_Left, 0, 1);
                lockInTime_Left = Math.Pow(lockInTime_Left, 0.1);

                DrawCircle(_hologramPositionLeft, 0.04 * lockInTime_Left, radarUp, lineColor, radarBrightness, false, false, 0.5f, 0.00075f);
                DrawCircle(_hologramPositionLeft - (radarUp * 0.015), 0.045, radarUp, lineColor, radarBrightness, false, false, 0.25f, 0.00125f);

                // We will have cleared target lock earlier in loop if no longer able to target due to distance or lack of antenna etc.
                if (gHandler.targetGrid != null && !gHandler.targetGrid.Closed)
                {
                    DrawQuad(_hologramPositionLeft + (radarUp * _hologramRightOffset_HardCode.Y * 0.5), viewBackward, 0.15, MaterialCircleSeeThroughAdd,
                        gHandler.localGridControlledEntityCustomData.lineColorComp * 0.125f, true);
                    DrawQuad(_hologramPositionLeft + (radarUp * _hologramRightOffset_HardCode.Y), viewBackward, 0.1, MaterialCircleSeeThrough, new Vector4(0, 0, 0, 0.75f)); //Dim Backing
                }
                else
                {
                    DrawQuad(_hologramPositionLeft + (radarUp * _hologramRightOffset_HardCode.Y * 0.5), viewBackward, 0.15, MaterialCircleSeeThroughAdd, lineColor * 0.125f, true);
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
			double activeRangeDisp = Math.Round(gHandler.localGridMaxActiveRadarRange/1000, 1);
			double passiveRangeDisp = Math.Round(gHandler.localGridMaxPassiveRadarRange/1000, 1);
			double radarScaleDisp = Math.Round(localGridRadarScaleRange / 1000, 1);
            string rangeText = $"RDR{(gHandler.localGridHasActiveRadar ? $"[ACT]:{activeRangeDisp}" : gHandler.localGridHasPassiveRadar ? $"[PAS]:{passiveRangeDisp}" : "[OFF]:0")}KM [SCL]:{radarScaleDisp}KM {(gHandler.targetGrid != null ? "(T)" : "")}";
            DrawText(rangeText, 0.0045, _radarCurrentRangeTextPosition, textDir, lineColor, radarBrightness, 1f, false);

			Vector4 color_Current = color_VoxelBase; // Default to voxelbase color

			// Radar Pulse Timer for the pulse animation
            double radarPulseTime = timerRadarElapsedTimeTotal.Elapsed.TotalSeconds / 3;
			radarPulseTime = 1 - (radarPulseTime - Math.Truncate(radarPulseTime))*2;

			// Radar attack timer to flip the blips and make them pulsate. Goal is to tie this to being targetted by those grids rather than by distance to them.
			double attackTimer = timerRadarElapsedTimeTotal.Elapsed.TotalSeconds*4;
			attackTimer = (attackTimer - Math.Truncate(attackTimer));
			if (attackTimer > 0.5) 
			{
				targetingFlipper = true;
			} 
			else 
			{
				targetingFlipper = false;
			}
			// Draw Rings
            DrawQuad(radarPos, radarUp, (double)gHandler.localGridControlledEntityCustomData.radarRadius * 0.03f, MaterialCircle, lineColor * 5f); //Center
			// Draw perspective lines
            float radarFov = Clamped(GetCameraFOV ()/2, 0, 90)/90;
			DrawLineBillboard(Material, lineColor * radarBrightness * 0.4f, radarPos, Vector3D.Lerp(radarMatrix.Forward,radarMatrix.Left,radarFov), gHandler.localGridControlledEntityCustomData.radarRadius*1.45f, 0.0005f); //Perspective Lines
			DrawLineBillboard(Material, lineColor * radarBrightness * 0.4f, radarPos, Vector3D.Lerp(radarMatrix.Forward,radarMatrix.Right,radarFov), gHandler.localGridControlledEntityCustomData.radarRadius*1.45f, 0.0005f);

			// Animate radar pulse
			for (int i = 1; i < 11; i++) 
			{
				int radarPulseSteps = (int)Math.Truncate(10 * radarPulseTime);
				float radarPulse = 1;
				if (radarPulseSteps == i) 
				{
					radarPulse = 2;
				}
				DrawCircle(radarPos-(radarUp*0.003), (gHandler.localGridControlledEntityCustomData.radarRadius*0.95)-(gHandler.localGridControlledEntityCustomData.radarRadius*0.95)*(Math.Pow((float)i/10, 4)), radarUp, lineColor, radarBrightness, 
                    false, false, 0.25f *radarPulse, 0.00035f);
			}
            
			// Draw border
            DrawQuadRigid(radarPos, radarUp, (double)gHandler.localGridControlledEntityCustomData.radarRadius*1.18, MaterialBorder, lineColor, true); //Border

			// Draw compass
			if(gHandler.localGridControlledEntityCustomData.enableGauges)
			{
				DrawQuadRigid(radarPos-(radarUp*0.005), -radarUp, (double)gHandler.localGridControlledEntityCustomData.radarRadius*1.5, MaterialCompass, lineColor, true); //Compass
			}
			DrawQuad(radarPos-(radarUp*0.010), radarUp, (double)gHandler.localGridControlledEntityCustomData.radarRadius*2.25, MaterialCircleSeeThrough, new Vector4(0, 0, 0, 0.75f)); //Dim Backing
			DrawQuad(radarPos + (radarUp*0.025), viewBackward, 0.25, MaterialCircleSeeThroughAdd, lineColor*0.075f, true);

            UpdateRadarAnimations(playerGridControlledEntityPosition);

            // Okay I had to do some research on this. The way this works is Math.Sin() returns a value between -1 and 1, so we add 1 to it to get a value between 0 and 2, then divide by 2 to get a value between 0 and 1.
			// Basically shifting the wave up so we are into positive value only territory, and then scaling it to a 0-1 range where 0 is completely invisible and 1 is fully visible. Neat.
            float haloPulse = (float)((Math.Sin(timerRadarElapsedTimeTotal.Elapsed.TotalSeconds * (4*Math.PI)) + 1.0) * 0.5); // 0 -> 1 -> 0 smoothly. Should give 2 second pulses. Could do (2*Math.PI) for 1 second pulses

            int maxPingCheck = 0;
            List<VRage.ModAPI.IMyEntity> pingsPendingRemoval = new List<VRage.ModAPI.IMyEntity>();
            foreach (KeyValuePair<VRage.ModAPI.IMyEntity, RadarPing> entityPingPair in radarPings)
            {
                maxPingCheck++;
                if (maxPingCheck > theSettings.maxPings)
                {
                    break;
                }

                VRage.ModAPI.IMyEntity entity = entityPingPair.Key;
                if (entity == null)
                {
                    continue; // continue
                }
                if (!gHandler.localGridControlledEntityCustomData.scannerShowVoxels && radarPings[entity].Status == RelationshipStatus.Vox)
                {
                    continue; // Skip voxels if not set to show them. 
                }
                if (entity.GetTopMostParent() == gHandler.localGridControlledEntity.GetTopMostParent())
                {
                    continue; // Skip drawing yourself on the radar.
                }

                if (!radarPings[entity].PlayerCanDetect || radarPings[entity].RadarPingDistanceSqr > _radarShownRangeSqr) // If can't detect it, or the radar scale prevents showing it move on.
                {
                    continue;
                }

                VRage.Game.ModAPI.IMyCubeGrid entityGrid = entity as VRage.Game.ModAPI.IMyCubeGrid;
                if (entityGrid != null)
                {
                    if (gHandler.localGridControlledEntityCustomData.scannerOnlyPoweredGrids && !radarPings[entity].RadarPingHasPower)
                    {
                        continue;
                    }
                }

                // Handle fade for edge
                float fadeDimmer = 1f;
                // Have to invert old logic, original author made this extend radar range past configured limit. 
                // I am having the limit as a hard limit be it global config or antenna broadcast range, so we instead make a small range before that "fuzzy" instead. 
                // If it was outside max range it wouldn't have been detected by the player actively or passively, so we skipped it already. Meaning all we have to do is check if it is in the fade region and fade it or draw it regularly. 
                if (radarPings[entity].RadarPingDistanceSqr >= _fadeDistanceSqr)
                {
                    fadeDimmer = 1 - Clamped(1 - (float)((_fadeDistanceSqr - radarPings[entity].RadarPingDistanceSqr) / (_fadeDistanceSqr - _radarShownRangeSqr)), 0, 1);
                }
                Vector3D scaledPos = ApplyLogarithmicScaling(radarPings[entity].RadarPingPosition, playerGridControlledEntityPosition, gHandler.localGridControlledEntityCustomData.radarRadius); // Apply radar scaling

                if (debug)
                {
                    //MyLog.Default.WriteLine($"FENIX_HUD: radarScaleRange = {radarScaleRange}, radarScale = {radarScale}, scaledPos = {scaledPos} ");
                }


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
                // If gridWidth is something ridiculous like 0.0f then fall back on min scale. 
                if (radarPings[entity].Width <= 0.000001 || double.IsNaN(radarPings[entity].Width)) // Compare to EPS rather than 0. If invalid skip now.
                {
                    continue;
                }

                // Do color of blip based on relationship to player
                color_Current = radarPings[entity].Color;

                // Detect being targeted (under attack) and modify color based on flipper so the blip flashes
                if (radarPings[entity].Status == RelationshipStatus.Hostile && IsEntityTargetingPlayer(radarPings[entity].Entity))
                {
                    if (!targetingFlipper)
                    {
                        color_Current = color_GridEnemyAttack;
                    }
                }

                // Pulse timers for animation
                Vector3D pulsePos = radarEntityPos + (lineDir * vertDistance);
                double pulseDistance = Vector3D.Distance(pulsePos, radarPos);
                float pulseTimer = (float)(ClampedD(radarPulseTime, 0, 1) + 0.5 + Math.Min(pulseDistance, gHandler.localGridControlledEntityCustomData.radarRadius) / gHandler.localGridControlledEntityCustomData.radarRadius);
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
                MyStringId drawMat = radarPings[entity].Material;
                float powerDimmer = !radarPings[entity].RadarPingHasPower ? 0.5f : 1.0f;
                // Draw each entity as a billboard on the radar
                float blipSize = 0.004f * fadeDimmer * radarPings[entity].Width * gHandler.localGridControlledEntityCustomData.radarScannerScale; // Scale blip size based on radar scale
                Vector3D blipPos = radarEntityPos + (lineDir * vertDistance);
                // Draw the blip with vertical elevation +/- relative to radar plane, with line connecting it to the radar plane.
                DrawLineBillboard(MaterialSquare, color_Current * 0.25f * fadeDimmer * pulseTimer, radarEntityPos, lineDir, vertDistance, 0.001f * fadeDimmer);
                DrawQuad(blipPos, radarUp, blipSize * 0.75f, MaterialCircle, color_Current * upDownDimmer * 0.5f * pulseTimer * 0.5f); // Add blip "shadow" on radar plane
                MyTransparentGeometry.AddBillboardOriented(drawMat, color_Current * powerDimmer * upDownDimmer * pulseTimer, radarEntityPos, viewLeft, viewUp, blipSize);

                // DONE UNTESTED: need to check that they have it AND it can reach us, if we aren't being painted by it don't show it. For situations where our active radar is set far enough to pickup grids
                // that also have active radar and thus the bool would be true but it's range is low enough they can't reach us (see us). As in only show grids that can see us. 
                if (radarPings[entity].RadarPingHasActiveRadar && radarPings[entity].RadarPingDistanceSqr <= (radarPings[entity].RadarPingMaxActiveRange * radarPings[entity].RadarPingMaxActiveRange))
                {
                    float pulseScale = 1.0f + (haloPulse * 0.20f); // Scale the halo by 15% at max pulse
                    float pulseAlpha = 1.0f * (1.0f - haloPulse); // Alpha goes from 0.25 to 0 at max pulse, so it fades out as pulse increases.
                    float ringSize = blipSize * pulseScale; // Scale the ring size based on the pulse
                    Vector4 haloColor = color_Current * 0.25f * (float)haloPulse; // subtle, fading
                    // Use the same position and orientation as the blip
                    DrawCircle(radarEntityPos, ringSize, radarUp, Color.WhiteSmoke, radarBrightness, false, false, 1.0f * pulseAlpha, 0.00035f * gHandler.localGridControlledEntityCustomData.radarScannerScale); //0.5f * haloPulse
                }

                if (gHandler.targetGrid != null && entityGrid == gHandler.targetGrid)
                {
                    float outlineSize = blipSize * 1.15f; // Slightly larger than the blip
                    Vector4 color = (Color.Yellow).ToVector4() * 2;

                    MyTransparentGeometry.AddBillboardOriented(
                        MaterialTarget,
                        color,
                        radarEntityPos,
                        viewLeft, // Align with radar plane
                        viewUp,
                        outlineSize
                    );
                }
            }

            // Targeting CrossHair
            double crossSize = 0.30;

			Vector3D crossOffset = radarMatrix.Left*gHandler.localGridControlledEntityCustomData.radarRadius*1.65 + radarMatrix.Up*gHandler.localGridControlledEntityCustomData.radarRadius*crossSize + radarMatrix.Forward*gHandler.localGridControlledEntityCustomData.radarRadius*0.35;
			Vector4 crossColor = lineColor;

			if (gHandler.targetGrid != null) 
			{
                targettingLerp_goal = 1;
                targettingLerp = LerpD(targettingLerp, targettingLerp_goal, deltaTimeSinceLastTick * 10);

                Vector3D targetDir = Vector3D.Normalize(gHandler.targetGrid.WorldVolume.Center - cameraPos) * 0.5;
                double dis2Cam = Vector3D.Distance(cameraPos, cameraPos + targetDir);
                DrawQuadRigid(cameraPos + targetDir, cameraMatrix.Forward, dis2Cam * 0.125 * targettingLerp, 
                    theSettings.useHollowReticle ? MaterialLockOnHollow : MaterialLockOn, lineColor * (float)targettingLerp, false);
                DrawText(gHandler.targetGrid.DisplayName, dis2Cam * 0.01, cameraPos + targetDir + (cameraMatrix.Up * dis2Cam * 0.02) + (cameraMatrix.Right * dis2Cam * 0.10 * targettingLerp), 
                    cameraMatrix.Forward, lineColor, radarBrightness, 1f, false);

                string disUnits = "m";
                double distanceToTargetFromLocalGrid = _distanceToCurrentTarget;
                if (_distanceToCurrentTarget > 1500)
                {
                    disUnits = "km";
                    distanceToTargetFromLocalGrid = distanceToTargetFromLocalGrid / 1000;
                }
                DrawText(Convert.ToString(Math.Round(distanceToTargetFromLocalGrid)) + " " + disUnits, dis2Cam * 0.01, cameraPos + targetDir + (cameraMatrix.Up * dis2Cam * -0.02) + (cameraMatrix.Right * dis2Cam * 0.11 * targettingLerp), 
                    cameraMatrix.Forward, lineColor, radarBrightness, 1f, false);

                double dotProduct = Vector3D.Dot(radarMatrix.Forward, targetDir);

                Vector3D targetPipPos = radarPos;
                Vector3D targetPipDir = Vector3D.Normalize(gHandler.targetGrid.WorldVolume.Center - playerGridControlledEntityPosition) * ((double)gHandler.localGridControlledEntityCustomData.radarRadius * crossSize * 0.55);

                // Calculate the component of the position along the forward vector
                Vector3D forwardComponent = Vector3D.Dot(targetPipDir, radarMatrix.Forward) * radarMatrix.Forward;

                // Subtract the forward component from the original position to remove the forward/backward contribution
                targetPipDir = targetPipDir - forwardComponent;

                targetPipPos += targetPipDir;
                targetPipPos += crossOffset;

                if (dotProduct > 0.49)
                {
                    crossColor = gHandler.localGridControlledEntityCustomData.lineColorComp;
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

                        DrawText(Convert.ToString(Math.Round(arivalTime)) + " " + unitsTime, dis2Cam * 0.01, 
                            cameraPos + targetDir + (cameraMatrix.Up * dis2Cam * -0.05) + (cameraMatrix.Right * dis2Cam * 0.11 * targettingLerp), 
                            cameraMatrix.Forward, gHandler.localGridControlledEntityCustomData.lineColorComp, gHandler.localGridControlledEntityCustomData.radarBrightness, 1f, false);
                    }
                }
                else
                {
                    alignLerp_goal = 1;
                }
                alignLerp = LerpD(alignLerp, alignLerp_goal, deltaTimeSinceLastTick * 20);

                if (dotProduct > 0)
                {
                    DrawQuadRigid(targetPipPos, radarMatrix.Backward, crossSize * 0.125 * 0.125, MaterialCircle, gHandler.localGridControlledEntityCustomData.lineColorComp, false); //LockOn
                }
                else
                {
                    DrawQuadRigid(targetPipPos, radarMatrix.Backward, crossSize * 0.125 * 0.125, MaterialCircleHollow, gHandler.localGridControlledEntityCustomData.lineColorComp, false); //LockOn
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
			DrawQuadRigid(radarPos+crossOffset, radarMatrix.Backward, crossSize * 0.125, MaterialCrossOutter, lineColor, false); //Target
			DrawQuad(radarPos+crossOffset, radarMatrix.Backward, crossSize * 0.25, MaterialCircleSeeThrough, new Vector4(0, 0, 0, 0.75f)); //Dim Backing
		}

        /// <summary>
        /// Draw he radar for a holo table or other terminal block with holo radar functionality.
        /// </summary>
        private void DrawRadarHoloTable()
        {
            IMyCharacter character = MyAPIGateway.Session.Player?.Character;
            if (character == null)
            {
                return;
            }
            VRage.Game.ModAPI.IMyCubeGrid playerGrid = gHandler.localGrid;
            Vector3D playerPos = character.GetPosition();
            Vector3D playerGridPos = playerGrid?.GetPosition() ?? playerPos;
            

            // CAMERA POSITION //
            // Get the camera's vectors
            MatrixD cameraMatrix = _cameraMatrix;
            Vector3 viewUp = cameraMatrix.Up;
            Vector3 viewLeft = cameraMatrix.Left;
            Vector3 viewForward = cameraMatrix.Forward;
            Vector3 viewBackward = cameraMatrix.Backward;
            Vector3D viewBackwardD = cameraMatrix.Backward;
            Vector3D cameraPos = _cameraPosition;

            foreach (KeyValuePair<Vector3I, Sandbox.ModAPI.IMyTerminalBlock> holoTableKeyValuePair in gHandler.localGridRadarTerminals)
            {
                Sandbox.ModAPI.IMyTerminalBlock holoTable = holoTableKeyValuePair.Value;
                if (!holoTable.IsFunctional || !holoTable.IsWorking) 
                {
                    continue;
                }
                HoloRadarCustomData theData = gHandler.localGridRadarTerminalsData[holoTableKeyValuePair.Key].HoloRadarCustomData;

                MatrixD holoTableMatrix = holoTable.WorldMatrix;
                Vector3D holoTableOffset = new Vector3D(theData.scannerX, theData.scannerY, theData.scannerZ);
                Vector3D holoTablePos = holoTableMatrix.Translation + Vector3D.TransformNormal(holoTableOffset, holoTableMatrix);

                bool onlyPoweredGrids = theData.scannerOnlyPoweredGrids;
                bool showVoxels = theData.scannerShowVoxels;
                float holoRadarRadius = theData.scannerRadius;
                //holoRadarScale = (holoRadarRadius / localGridRadarScaleRange);

                // Draw the radar circle
                Vector3D holoUp = holoTableMatrix.Up;
                Vector3D holoDown = holoTableMatrix.Down;
                Vector3D holoLeft = holoTableMatrix.Left;
                Vector3D holoForward = holoTableMatrix.Forward;
                Vector3D holoBackward = holoTableMatrix.Backward;

                Vector4 color_Current = color_VoxelBase; // Default to voxelbase color

                // Radar Pulse Timer for the pulse animation
                double radarPulseTime = timerRadarElapsedTimeTotal.Elapsed.TotalSeconds / 3;
                radarPulseTime = 1 - (radarPulseTime - Math.Truncate(radarPulseTime)) * 2;

                // Radar attack timer to flip the blips and make them pulsate. Goal is to tie this to being targetted by those grids rather than by distance to them.
                double attackTimer = timerRadarElapsedTimeTotal.Elapsed.TotalSeconds * 4;
                attackTimer = (attackTimer - Math.Truncate(attackTimer));
                if (attackTimer > 0.5)
                {
                    targetingFlipper = true;
                }
                else
                {
                    targetingFlipper = false;
                }

                // Draw border
                Color radiusColor = (Color.Orange) * 0.33f;
                MatrixD sphereMatrix = MatrixD.CreateTranslation(holoTablePos);
                MySimpleObjectDraw.DrawTransparentSphere(
                    ref sphereMatrix,
                    holoRadarRadius,                 
                    ref radiusColor,                          
                    MySimpleObjectRasterizer.Solid,
                    36
                );

                UpdateRadarAnimationsHoloTable(playerGridPos, holoTablePos, holoTableMatrix, holoRadarRadius);

                // Okay I had to do some research on this. The way this works is Math.Sin() returns a value between -1 and 1, so we add 1 to it to get a value between 0 and 2, then divide by 2 to get a value between 0 and 1.
                // Basically shifting the wave up so we are into positive value only territory, and then scaling it to a 0-1 range where 0 is completely invisible and 1 is fully visible. Neat.
                float haloPulse = (float)((Math.Sin(timerRadarElapsedTimeTotal.Elapsed.TotalSeconds * (4 * Math.PI)) + 1.0) * 0.5); // 0 -> 1 -> 0 smoothly. Should give 2 second pulses. Could do (2*Math.PI) for 1 second pulses

                int maxPingCheck = 0;
                List<VRage.ModAPI.IMyEntity> pingsPendingRemoval = new List<VRage.ModAPI.IMyEntity>();
                foreach (KeyValuePair<VRage.ModAPI.IMyEntity, RadarPing> entityPingPair in radarPings)
                {
                    maxPingCheck++;
                    if (maxPingCheck > theSettings.maxPings)
                    {
                        break;
                    }

                    VRage.ModAPI.IMyEntity entity = entityPingPair.Key;
                    if (entity == null)
                    {
                        continue; // continue
                    }
                    if (!showVoxels && radarPings[entity].Status == RelationshipStatus.Vox)
                    {
                        continue; // Skip voxels if not set to show them. 
                    }
                    if (!radarPings[entity].PlayerCanDetect || radarPings[entity].RadarPingDistanceSqr > _radarShownRangeSqr) // If can't detect it, or the radar scale prevents showing it move on.
                    {
                        continue;
                    }

                    VRage.Game.ModAPI.IMyCubeGrid entityGrid = entity as VRage.Game.ModAPI.IMyCubeGrid;
                    if (entityGrid != null)
                    {
                        if (onlyPoweredGrids && !radarPings[entity].RadarPingHasPower)
                        {
                            continue;
                        }
                    }

                    // Handle fade for edge
                    float fadeDimmer = 1f;
                    // Have to invert old logic, original author made this extend radar range past configured limit. 
                    // I am having the limit as a hard limit be it global config or antenna broadcast range, so we instead make a small range before that "fuzzy" instead. 
                    // If it was outside max range it wouldn't have been detected by the player actively or passively, so we skipped it already. Meaning all we have to do is check if it is in the fade region and fade it or draw it regularly. 
                    if (radarPings[entity].RadarPingDistanceSqr >= _fadeDistanceSqr)
                    {
                        fadeDimmer = 1 - Clamped(1 - (float)((_fadeDistanceSqr - radarPings[entity].RadarPingDistanceSqr) / (_fadeDistanceSqr - _radarShownRangeSqr)), 0, 1);
                    }
                    Vector3D scaledPos = ApplyLogarithmicScaling(radarPings[entity].RadarPingPosition, playerGridPos, holoRadarRadius); // Apply radar scaling

                    // Position on the radar
                    Vector3D radarEntityPos = holoTablePos + scaledPos;

                    // If there are any problems that would make displaying the entity fail or do something ridiculous that tanks FPS just skip to the next one.
                    if (double.IsNaN(radarEntityPos.X) || double.IsInfinity(radarEntityPos.X))
                    {
                        continue;
                    }
                    if (!radarEntityPos.IsValid())
                    {
                        continue;
                    }

                    Vector3D v = radarEntityPos - holoTablePos;
                    Vector3D uNorm = Vector3D.Normalize(holoUp);
                    float vertDistance = (float)Vector3D.Dot(v, uNorm);

                    float upDownDimmer = 1f;
                    if (vertDistance < 0)
                    {
                        upDownDimmer = 0.8f;
                    }

                    float lineLength = (float)Vector3D.Distance(radarEntityPos, holoTablePos);
                    Vector3D lineDir = holoDown;

                    // If invalid, skip now.
                    if (!lineDir.IsValid())
                    {
                        continue;
                    }

                    if (radarPings[entity].Width <= 0.000001 || double.IsNaN(radarPings[entity].Width)) // Compare to EPS rather than 0. If invalid skip now.
                    {
                        continue;
                    }

                    // Do color of blip based on relationship to player
                    color_Current = radarPings[entity].Color;

                    // Detect being targeted (under attack) and modify color based on flipper so the blip flashes
                    if (radarPings[entity].Status == RelationshipStatus.Hostile && IsEntityTargetingPlayerHolo(radarPings[entity].Entity))
                    {
                        if (!targetingFlipper)
                        {
                            color_Current = color_GridEnemyAttack;
                        }
                    }

                    // Pulse timers for animation
                    Vector3D pulsePos = radarEntityPos + (lineDir * vertDistance);
                    double pulseDistance = Vector3D.Distance(pulsePos, holoTablePos);
                    float pulseTimer = (float)(ClampedD(radarPulseTime, 0, 1) + 0.5 + Math.Min(pulseDistance, holoRadarRadius) / holoRadarRadius);
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
                    MyStringId drawMat = radarPings[entity].Material;
                    float powerDimmer = !radarPings[entity].RadarPingHasPower ? 0.5f : 1.0f;

                    // Draw each entity as a billboard on the radar
                    float blipSize = 0.015f * fadeDimmer * radarPings[entity].Width * holoRadarRadius; // was 0.005f
                    Vector3D blipPos = radarEntityPos + (lineDir * vertDistance);
                    // Draw the blip with vertical elevation +/- relative to radar plane, with line connecting it to the radar plane.

                    if (entity.GetTopMostParent() == playerGrid.GetTopMostParent())
                    {
                        color_Current = (Color.Yellow).ToVector4() * 2;
                        drawMat = MaterialCircle;
                        blipSize = blipSize * 0.66f;
                        //DrawLineBillboard(MaterialSquare, color_Current * 0.25f * fadeDimmer * pulseTimer, radarEntityPos, lineDir, vertDistance, 0.004f * fadeDimmer);
                        DrawQuad(blipPos, holoUp, holoRadarRadius * 1.15f, MaterialCircle, radiusColor * 0.5f); // Add blip "shadow" on radar plane
                        MyTransparentGeometry.AddBillboardOriented(drawMat, color_Current * powerDimmer * upDownDimmer * pulseTimer, radarEntityPos, viewLeft, viewUp, blipSize);
                    }
                    else
                    {
                        DrawLineBillboard(MaterialSquare, color_Current * 0.25f * fadeDimmer * pulseTimer, radarEntityPos, lineDir, vertDistance, 0.004f * fadeDimmer);
                        DrawQuad(blipPos, holoUp, blipSize * 0.75f, MaterialCircle, color_Current * upDownDimmer * 0.5f * pulseTimer * 0.5f);
                        // Add blip "shadow" on radar plane
                        MyTransparentGeometry.AddBillboardOriented(drawMat, color_Current * powerDimmer * upDownDimmer * pulseTimer, radarEntityPos, viewLeft, viewUp, blipSize);
                    }

                    if (radarPings[entity].RadarPingHasActiveRadar && radarPings[entity].RadarPingDistanceSqr <= (radarPings[entity].RadarPingMaxActiveRange * radarPings[entity].RadarPingMaxActiveRange))
                    {
                        float pulseScale = 1.0f + (haloPulse * 0.20f); // Scale the halo by 15% at max pulse
                        float pulseAlpha = 1.0f * (1.0f - haloPulse); // Alpha goes from 0.25 to 0 at max pulse, so it fades out as pulse increases.
                        float ringSize = blipSize * pulseScale; // Scale the ring size based on the pulse
                        if (entity.GetTopMostParent() == playerGrid.GetTopMostParent())
                        {
                            ringSize = ringSize * 1.25f;
                        }

                        DrawCircle(radarEntityPos, ringSize, holoUp, Color.WhiteSmoke, 1f, false, false, 1.0f * pulseAlpha, 0.0005f * holoRadarRadius); //0.5f * haloPulse
                    }

                    if (gHandler.targetGrid != null && entityGrid == gHandler.targetGrid)
                    {
                        float outlineSize = blipSize * 1.15f; // Slightly larger than the blip
                        Vector4 color = (Color.Yellow).ToVector4() * 2;

                        MyTransparentGeometry.AddBillboardOriented(
                            MaterialTarget,
                            color,
                            radarEntityPos,
                            viewLeft,
                            viewUp,
                            outlineSize
                        );
                    }
                }
            }
        }

        
		
        /// <summary>
        /// Gets the entities from the game with a filter applied to only pull eligible radar pings.
        /// </summary>
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
            HashSet<VRage.ModAPI.IMyEntity> radarEntities = new HashSet<VRage.ModAPI.IMyEntity>();
            radarPings.Clear();

            MyAPIGateway.Entities.GetEntities(radarEntities, entity =>
            {
				if (entity == null) return false;
                if (entity.MarkedForClose || entity.Closed) return false;
                if (entity is MyPlanet) return false;

                return true;
            });


            foreach (VRage.ModAPI.IMyEntity entity in radarEntities)
            {
                VRage.Game.ModAPI.IMyCubeGrid gridEntity = entity as VRage.Game.ModAPI.IMyCubeGrid;

                // If it is a grid entity we get topMostParent and check if block count is within our limit to "detect" 
                // Helps reduce junk AND subgrids. 
                if (gridEntity != null)
                {
                    VRage.Game.ModAPI.IMyCubeGrid parent = gridEntity.GetTopMostParent() as VRage.Game.ModAPI.IMyCubeGrid;
                    if (parent == null)
                    {
                        continue;
                    }

                    List<VRage.Game.ModAPI.IMySlimBlock> tempBlocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
                    parent.GetBlocks(tempBlocks);
                    if (tempBlocks.Count < theSettings.minTargetBlocksCount)
                    {
                        continue;
                    }

                    VRage.ModAPI.IMyEntity parentEntity = parent as VRage.ModAPI.IMyEntity;

                    RadarPing ping = NewRadarPing(parentEntity);

                    // Only add if width is valid
                    if (ping.Width > 0.00001f && ping.Width <= 4f) // safety bounds
                    {
                        if (!radarPings.ContainsKey(parentEntity))
                        {
                            radarPings.Add(parentEntity, ping);
                        }
                    }
                }
                else 
                {
                    RadarPing ping = NewRadarPing(entity);

                    // Only add if width is valid
                    if (ping.Width > 0.00001f && ping.Width <= 4f) // safety bounds
                    {
                        if (!radarPings.ContainsKey(entity))
                        {
                            radarPings.Add(entity, ping);
                        }
                    }
                }
                  
            }
        }
		
        /// <summary>
        /// Get the local player ID.
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Return the relationship between the grid and the player for determining friend/foe
        /// </summary>
        /// <param name="grid"></param>
        /// <param name="playerId"></param>
        /// <returns></returns>
		public RelationshipStatus GetGridRelationship(VRage.Game.ModAPI.IMyCubeGrid grid, long playerId)
		{
            List<VRage.Game.ModAPI.IMySlimBlock> blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
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
            MyRelationsBetweenPlayerAndBlock relation = MyIDModule.GetRelationPlayerBlock(owner, playerId, MyOwnershipShareModeEnum.Faction);

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
		}

        /// <summary>
        /// Get the FOV of the camera. 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
		public float GetCameraFOV()
		{
			IMyCamera camera = MyAPIGateway.Session.Camera;

			if (camera != null)
			{
				return camera.FieldOfViewAngle;
			}
			else
			{
				throw new InvalidOperationException("Camera is not available.");
			}
		}


        /// <summary>
        /// Apply logarithmic like scaling to the radar blip position based on distance to the player and the radar scale range.
        /// </summary>
        /// <param name="entityPos"></param>
        /// <param name="referencePos"></param>
        /// <param name="radius"></param>
        /// <returns></returns>
        public Vector3D ApplyLogarithmicScaling(Vector3D entityPos, Vector3D referencePos, float radius)
        {
            Vector3D offset = entityPos - referencePos;
            double distance = offset.Length();
            if (distance < 1e-6) 
            {
                return Vector3D.Zero;
            }

            // Normalize the distance vs radarScaleRange to a 0-1 range. 
            double distanceToScaleRatio = Math.Min(distance / Math.Max(1e-6, localGridRadarScaleRange), 1.0);

            const double BETA = 2.0; // BETA, the larger this value the farther away blips are from the center of the screen when nearby.
            // EG if our radar scale is 10km and we have a blip at 10km it the function will not modify it at all and it will be placed at the edge. 
            // if we have a blip at 500m then BETA will make that appear farther from the center than it would be linearly, to make the nearby stuff more readable. 
            // How much of an effect it has is based on the value of BETA, larger pushes nearby stuff farther, smaller keeps it near. At 1.0 it is almost linear.
            // Basically the closer an entity distance is to the max range of our radar the less effect BETA has, the closer it is to 0 distance away the more effect it has.
            // This can prevent all the nearby stuff from clumping up at the center of the radar screen when max range is set farther out.
            double distanceToScaleRatioCompressed = Math.Log(1.0 + BETA * distanceToScaleRatio) / Math.Log(1.0 + BETA);

            // project to screen radius
            //double radius = gHandler.localGridControlledEntityCustomData.radarRadius; // keep this constant in screen space
            Vector3D dir = offset / distance;
            return dir * (radius * distanceToScaleRatioCompressed);
        }




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

        /// <summary>
        /// Check if an entity is targeting the player grid
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Check if a grid is targeting the player grid
        /// </summary>
        /// <param name="grid"></param>
        /// <returns></returns>
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


        public bool IsEntityTargetingPlayerHolo(VRage.ModAPI.IMyEntity entityToCheck)
        {
            VRage.Game.ModAPI.IMyCubeGrid entityGrid = entityToCheck as VRage.Game.ModAPI.IMyCubeGrid; // If a cube grid we can get that here.
            // Check if even a grid first, can only passively detect grids that are broadcasting.
            if (entityGrid == null)
            {
                return false;
            }
            return IsGridTargetingPlayerHolo(entityGrid);

        }
        public bool IsGridTargetingPlayerHolo(VRage.Game.ModAPI.IMyCubeGrid grid)
        {
            if (grid == null || MyAPIGateway.Session?.Player == null)
            {
                return false;
            }

            // Get all turret blocks on the grid
            List<Sandbox.ModAPI.IMyLargeTurretBase> turrets = new List<Sandbox.ModAPI.IMyLargeTurretBase>();
            turrets = grid.GetFatBlocks<Sandbox.ModAPI.IMyLargeTurretBase>().ToList();


            IMyCharacter character = MyAPIGateway.Session.Player?.Character;
            if (character == null)
            {
                return false;
            }
            VRage.Game.ModAPI.IMyCubeGrid playerGrid = character?.Parent as VRage.Game.ModAPI.IMyCubeGrid;

            // We store playerGrid for re-use. 
            long playerShipId = playerGrid?.EntityId ?? 0;

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
			List<MyObjectBuilder_Checkpoint.ModItem> mods = MyAPIGateway.Session.Mods;
			foreach (MyObjectBuilder_Checkpoint.ModItem mod in mods)
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

        /// <summary>
        /// Play a custom sound at the specified position.
        /// </summary>
        /// <param name="soundId"></param>
        /// <param name="position"></param>
		public void PlayCustomSound(MySoundPair soundId, Vector3D position)
		{

			if (timerElapsedTimeSinceLastSound != null && !timerElapsedTimeSinceLastSound.IsRunning) 
			{
				timerElapsedTimeSinceLastSound.Start();
			}

			// Only play one sound per 0.05 seconds.
			if (timerElapsedTimeSinceLastSound.Elapsed.TotalSeconds > 0.05) 
			{
				ED_soundEmitter.SetPosition(position);
				ED_soundEmitter.PlaySound(soundId);

				timerElapsedTimeSinceLastSound.Restart();
			}
		}

        /// <summary>
        /// Play queued sounds
        /// </summary>
		public void PlayQueuedSounds()
		{
			foreach(queuedSound qs in queuedSounds)
			{

				if (ED_soundEmitter != null && qs.soundId != null && soundEntity != null)
				{
					ED_soundEmitter.SetPosition (qs.position);
					ED_soundEmitter.PlaySound (qs.soundId);
				}
			}

			queuedSounds.Clear ();
		}

        /// <summary>
        /// Initialize the audio and set the files to use
        /// </summary>
		private void InitializeAudio()
		{
			soundEntity = MyAPIGateway.Session.LocalHumanPlayer?.Controller?.ControlledEntity?.Entity;

			ED_soundEmitter = 	new MyEntity3DSoundEmitter (soundEntity as VRage.Game.Entity.MyEntity, false, 0.01f);
			ED_soundEmitter.Entity = soundEntity as VRage.Game.Entity.MyEntity;

			SP_DAMAGE = 	new MySoundPair(SOUND_DAMAGE);
			SP_BROKEN = 	new MySoundPair(SOUND_BROKEN);
			SP_ENEMY = 		new MySoundPair(SOUND_ENEMY);
			SP_NEUTRAL = 	new MySoundPair(SOUND_NEUTRAL);
			SP_ZOOMINIT = 	new MySoundPair(SOUND_ZOOMINIT);
			SP_ZOOMIN = 	new MySoundPair(SOUND_ZOOMIN);
			SP_ZOOMOUT = 	new MySoundPair(SOUND_ZOOMOUT);
			SP_BOOTUP = 	new MySoundPair(SOUND_BOOTUP);
			SP_MONEY = 		new MySoundPair(SOUND_MONEY);
		}
        //-------------------------------------------------------------------------------------------

        /// <summary>
        /// Get the blip icon based on block count and grid size.
        /// </summary>
        /// <param name="blockCount"></param>
        /// <param name="gridCubeSize"></param>
        /// <returns></returns>
        private MyStringId GetBlipMaterial(int blockCount, MyCubeSize gridCubeSize) 
        {
            if (gridCubeSize == MyCubeSize.Large)
            {
                if (blockCount <= theSettings.largeGridSizeOneMaxBlocks)
                {
                    return MaterialLG_1;
                }
                else if (blockCount <= theSettings.largeGridSizeTwoMaxBlocks)
                {
                    return MaterialLG_2;
                }
                else if (blockCount <= theSettings.largeGridSizeThreeMaxBlocks)
                {
                    return MaterialLG_3;
                }
                else if (blockCount <= theSettings.largeGridSizeFourMaxBlocks)
                {
                    return MaterialLG_4;
                }
                else if (blockCount <= theSettings.largeGridSizeFiveMaxBlocks)
                {
                    return MaterialLG_5;
                }
                else
                {
                    return MaterialLG_6;
                }
            }
            else 
            {
                if (blockCount <= theSettings.smallGridSizeOneMaxBlocks)
                {
                    return MaterialSG_1;
                }
                else if (blockCount <= theSettings.smallGridSizeTwoMaxBlocks)
                {
                    return MaterialSG_2;
                }
                else if (blockCount <= theSettings.smallGridSizeThreeMaxBlocks)
                {
                    return MaterialSG_3;
                }
                else
                {
                    return MaterialSG_4;
                }
            }
        }

        /// <summary>
        /// Update the status of the new radarping, this includes the blockcount of the pinged entity, its relationship to the player, and the color and material to use for the blip.
        /// </summary>
        /// <param name="ping"></param>
		private void UpdateNewRadarPingStatus(ref RadarPing ping) 
		{
			VRage.ModAPI.IMyEntity entity = ping.Entity;

            if (entity is VRage.Game.ModAPI.IMyCubeGrid)
            {
                VRage.Game.ModAPI.IMyCubeGrid gridEntity = entity as VRage.Game.ModAPI.IMyCubeGrid;

                if (gridEntity != null)
                {
                    

                    // Get Grid size (Large/Small and block count).
                    MyCubeGrid cubeGrid = gridEntity as MyCubeGrid;
                    if (cubeGrid != null) 
                    {
                        ping.BlockCount = cubeGrid.BlocksCount;
                    }
                    ping.GridCubeSize = gridEntity.GridSizeEnum;
                    ping.Material = GetBlipMaterial(ping.BlockCount, ping.GridCubeSize);
                    ping.Width = ping.GridCubeSize == MyCubeSize.Small ? 1.5f : 2.0f;

                    long lpID = GetLocalPlayerId();
                    if (lpID != -1)
                    {
                        //DETECT FACTION STATUS
                        RelationshipStatus gridFaction = GetGridRelationship(gridEntity, lpID);
                        switch (gridFaction)
                        {
                            case RelationshipStatus.Friendly:
                                ping.Color = color_GridFriend;
                                ping.Status = RelationshipStatus.Friendly;
                                break;
                            case RelationshipStatus.Hostile:
                                ping.Color = color_GridEnemy;
                                ping.Status = RelationshipStatus.Hostile;
                                break;
                            case RelationshipStatus.Neutral:
                                ping.Color = color_GridNeutral;
                                ping.Status = RelationshipStatus.Neutral;
                                ping.Announced = true;
                                break;
                            default:
                                ping.Color = color_GridNeutral;
                                ping.Status = RelationshipStatus.Neutral;
                                ping.Announced = true;
                                break;
                        }
                    }
                    else
                    {
                        ping.Color = color_GridNeutral;
                        ping.Status = RelationshipStatus.Neutral;
                        ping.Announced = true;
                    }
                }
            }
            else if (entity is IMyFloatingObject)
            {
                ping.Color = color_FloatingObject;
                ping.Status = RelationshipStatus.FObj;
                ping.Width = 0.5f;
                ping.Material = MaterialDiamond;
            }
            else if (entity is MyPlanet)
            {
                ping.Color = new Vector4(0, 0, 0, 0);
                ping.Status = RelationshipStatus.FObj;
                ping.Width = 0.00001f;
                ping.Announced = true;
                ping.Material = MaterialCircle;
            }
            else if (entity is IMyVoxelBase)
            {
                ping.Color = color_VoxelBase; //color_VoxelBase;
                ping.Status = RelationshipStatus.Vox;
                IMyVoxelBase voxelEntity = entity as IMyVoxelBase;
                if (voxelEntity != null)
                {
                    double voxelWidth = voxelEntity.PositionComp.WorldVolume.Radius;
                    ping.Width = (float)voxelWidth / 250;
                    ping.Width = Clamped(ping.Width, 1f, 4f);
                    ping.Material = MaterialCircle;
                }
            }
        }

        /// <summary>
        /// Update the status of an existing radar ping, this includes the blockcount of the pinged entity, its relationship to the player, and the color and material to use for the blip.
        /// </summary>
        /// <param name="entityKey"></param>
		private void UpdateExistingRadarPingStatus(VRage.ModAPI.IMyEntity entityKey) 
		{

            VRage.ModAPI.IMyEntity entity = entityKey;

            if (entity is VRage.Game.ModAPI.IMyCubeGrid)
            {
                VRage.Game.ModAPI.IMyCubeGrid gridEntity = entity as VRage.Game.ModAPI.IMyCubeGrid;
				RelationshipStatus startingStatus = radarPings[entityKey].Status;
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
                                radarPings[entityKey].Color = color_GridFriend;
                                radarPings[entityKey].Status = RelationshipStatus.Friendly;
                                break;
                            case RelationshipStatus.Hostile:
                                radarPings[entityKey].Color = color_GridEnemy;
                                radarPings[entityKey].Status = RelationshipStatus.Hostile;
                                break;
                            case RelationshipStatus.Neutral:
                                radarPings[entityKey].Color = color_GridNeutral;
                                radarPings[entityKey].Status = RelationshipStatus.Neutral;
                                break;
                            default:
                                radarPings[entityKey].Color = color_GridNeutral;
                                radarPings[entityKey].Status = RelationshipStatus.Neutral;
                                break;
                        }
                    }
                    else
                    {
                        radarPings[entityKey].Color = color_GridNeutral;
                        radarPings[entityKey].Status = RelationshipStatus.Neutral;
                    }
					if (startingStatus != radarPings[entityKey].Status) 
					{
                        radarPings[entityKey].Announced = false;
					}

                    MyCubeGrid cubeGrid = gridEntity as MyCubeGrid;
                    if (cubeGrid != null) 
                    {
                        radarPings[entityKey].BlockCount = cubeGrid.BlocksCount;
                    }
                    radarPings[entityKey].GridCubeSize = gridEntity.GridSizeEnum;
                    radarPings[entityKey].Material = GetBlipMaterial(radarPings[entityKey].BlockCount, radarPings[entityKey].GridCubeSize);
                }
            }
        }

        /// <summary>
        /// Create a new radar ping
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
		private RadarPing NewRadarPing(VRage.ModAPI.IMyEntity entity){
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

        /// <summary>
        /// Add a new radar animation that will play on the radar screen until completion
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="loops"></param>
        /// <param name="lifeTime"></param>
        /// <param name="sizeStart"></param>
        /// <param name="sizeStop"></param>
        /// <param name="fadeStart"></param>
        /// <param name="fadeStop"></param>
        /// <param name="material"></param>
        /// <param name="colorStart"></param>
        /// <param name="colorStop"></param>
        /// <param name="offsetStart"></param>
        /// <param name="offsetStop"></param>
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
			
        /// <summary>
        /// Add an alert animation for blips that have just appeared on screen.
        /// </summary>
        /// <param name="entity"></param>
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

        /// <summary>
        /// Add a new blip animation
        /// </summary>
        /// <param name="entity"></param>
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

        /// <summary>
        /// Update radar animations that are currently active until completion.
        /// </summary>
        /// <param name="shipPos"></param>
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
					Vector3D scaledPos = ApplyLogarithmicScaling(entityPos, shipPos, gHandler.localGridControlledEntityCustomData.radarRadius); 

					// Position on the radar
					Vector3D radarEntityPos = worldRadarPos + scaledPos + offset;

					MyTransparentGeometry.AddBillboardOriented(RadarAnimations[i].Material, color*fade, radarEntityPos, viewLeft, viewUp, (float)size);
				}
			}

			//Delete all animations that were flagged in the prior step.
			foreach(RadarAnimation d in deleteList)
			{
				if (RadarAnimations.Contains(d)) 
				{
					d.Time.Stop();
					RadarAnimations.Remove(d);
				}
			}
			deleteList.Clear();
		}

        /// <summary>
        /// Update radar animations that are currently active until completion, for the holotable position defined.
        /// </summary>
        /// <param name="shipPos"></param>
        /// <param name="holoPos"></param>
        /// <param name="holoMatrix"></param>
        /// <param name="holoRadius"></param>
        public void UpdateRadarAnimationsHoloTable(Vector3D shipPos, Vector3D holoPos, MatrixD holoMatrix, float holoRadius)
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
                    Vector3D upSquish = Vector3D.Dot(entityPos, holoMatrix.Up) * holoMatrix.Up;
                    //entityPos -= (upSquish*squishValue); //------Squishing the vertical axis on the radar to make it easier to read... but less vertically accurate.

                    // Apply radar scaling
                    Vector3D scaledPos = ApplyLogarithmicScaling(entityPos, shipPos, holoRadius);

                    // Position on the radar
                    Vector3D radarEntityPos = holoPos + scaledPos + offset;

                    MyTransparentGeometry.AddBillboardOriented(RadarAnimations[i].Material, color * fade, radarEntityPos, viewLeft, viewUp, (float)size);
                }
            }

            //Delete all animations that were flagged in the prior step.
            foreach (RadarAnimation d in deleteList)
            {
                if (RadarAnimations.Contains(d))
                {
                    d.Time.Stop();
                    RadarAnimations.Remove(d);
                }
            }
            deleteList.Clear();
        }

        /// <summary>
        /// Draw an arc for the HUD
        /// </summary>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <param name="planeDirection"></param>
        /// <param name="startAngle"></param>
        /// <param name="endAngle"></param>
        /// <param name="color"></param>
        /// <param name="width"></param>
        /// <param name="gap"></param>
        void DrawArc(Vector3D center, double radius, Vector3D planeDirection, float startAngle, float endAngle, Vector4 color, float width = 0.005f, float gap = 0)
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
					Vector4 drawColor = color;
					if (GetRandomBoolean()) 
					{
						if (gHandler.localGridGlitchAmount > 0.001) {
							float glitchValue = (float)gHandler.localGridGlitchAmount;

							Vector3D offsetRan = new Vector3D ((GetRandomDouble () - 0.5) * 2, (GetRandomDouble () - 0.5) * 2, (GetRandomDouble () - 0.5) * 2);
							double dis2Cam = Vector3D.Distance (MyAPIGateway.Session.Camera.Position, position);

							position += offsetRan * dis2Cam * glitchValue * 0.005;
							drawColor *= GetRandomFloat();
						}
					}

					double dis = Vector3D.Distance(position, orbitPoints [(i + 1) % segments]);
					Vector3D dir = Vector3D.Normalize(orbitPoints [(i + 1) % segments] - position);
					MyTransparentGeometry.AddBillboardOriented(MaterialSquare,drawColor, position,dir,left,(float)dis*gap,width);
				}
			}
		}

		void DrawSegment(Vector3D point1, Vector3D point2, float lineThickness, float dimmerOverride, float thicknessOverride)
		{

			Vector3D cameraPosition = MyAPIGateway.Session.Camera.Position;
			Vector3 direction = point2 - point1;
			float distanceToSegment = DistanceToLineSegment(cameraPosition, point1, point2);
			float segmentThickness = lineThickness;//Math.Max(Remap(distanceToSegment, 1000f, 1000000f, 0f, 1000f) * lineThickness, 0f);
			float dimmer = OrbitDimmer;//Clamped(Remap(distanceToSegment, -10000f, 10000000f, 1f, 0f), 0f, 1f) * GlobalDimmer;

			if (thicknessOverride != 0)
				segmentThickness = thicknessOverride;

			if (dimmerOverride != 0)
				dimmer = dimmerOverride;

			if (dimmer > 0 && segmentThickness > 0)
				DrawLineBillboard(MaterialSquare, gHandler.localGridControlledEntityCustomData.lineColor * dimmer, point1, direction, 0.9f, segmentThickness, BlendTypeEnum.Standard);
		}

		//==================================SHIP HOLOGRAMS============================================================
		//private VRage.ModAPI.IMyEntity currentTarget = null;
		private bool isTargetLocked = false;
		private bool isTargetAnnounced = false;

        /// <summary>
        /// Check if the key or mouse button for Select Target is pressed
        /// </summary>
        /// <returns></returns>
        public bool IsSelectTargetPressed()
        {
            if (theSettings.useMouseTargetSelect)
            {
                switch ((int)theSettings.selectTargetMouseButton)
                {
                    case 0: return MyAPIGateway.Input.IsNewLeftMousePressed();
                    case 1: return MyAPIGateway.Input.IsNewRightMousePressed();
                    case 2: return MyAPIGateway.Input.IsNewMiddleMousePressed();
                    default: return false;
                }
            }
            else
            {
                return MyAPIGateway.Input.IsNewKeyPressed((MyKeys)theSettings.selectTargetKey);
            }
        }

        


        /// <summary>
        /// Check player input, could handle various things. Currently just right-click target locking. 
        /// </summary>
        private void CheckPlayerInput()
		{
            MyKeys rotateLeftKey = (MyKeys)theSettings.rotateLeftKey;
            MyKeys rotateRightKey = (MyKeys)theSettings.rotateRightKey;
            MyKeys rotateUpKey = (MyKeys)theSettings.rotateUpKey;
            MyKeys rotateDownKey = (MyKeys)theSettings.rotateDownKey;
            MyKeys rotatePosZKey = (MyKeys)theSettings.rotatePosZKey;
            MyKeys rotateNegZKey = (MyKeys)theSettings.rotateNegZKey;
            MyKeys resetKey = (MyKeys)theSettings.resetKey;
            MyKeys orbitKey = (MyKeys)theSettings.orbitViewKey;
            MyKeys perspectiveKey = (MyKeys)theSettings.perspectiveViewKey;

            if (IsSelectTargetPressed())
			{
				//Echo ("Press");
				if (isTargetLocked && gHandler.targetGrid != null && !gHandler.targetGrid.MarkedForClose)
				{
					// Toggle off if the same target is still valid
					ReleaseTarget();
				}
				else
				{
					// Attempt to lock on a new target, gets the nearest entity in camera sights.
					VRage.ModAPI.IMyEntity newTarget = FindEntityInSight();

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
                        //EvaluateGridAntennaStatus(gHandler.localGrid, out playerHasPassiveRadar, out playerHasActiveRadar, out playerMaxPassiveRange, out playerMaxActiveRange);

                        bool canPlayerDetect = false;
                        Vector3D entityPos = new Vector3D();
                        CanGridRadarDetectEntity(gHandler.localGridHasPassiveRadar, gHandler.localGridHasActiveRadar, gHandler.localGridMaxPassiveRadarRange, gHandler.localGridMaxActiveRadarRange, gHandler.localGridControlledEntity.GetPosition(), 
                            newTarget, out canPlayerDetect, out entityPos, out relativeDistanceSqr, out entityHasActiveRadar, out entityMaxActiveRange);

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

            bool ctrl = MyAPIGateway.Input.IsAnyCtrlKeyPressed();

			if (ctrl)
			{
                if (MyAPIGateway.Input.IsNewKeyPressed(rotateLeftKey))
                {
                    if (gHandler.localGridControlledEntityCustomData.localGridHologramViewType != HologramViewType.Static)
                    {
                        gHandler.SetHologramViewType(false, HologramViewType.Static);
                    }
                    else 
                    {
                        gHandler.SetHologramRotation(false, 0, 90);
                    }
                    //HologramViewLocal_Current = ((int)HologramViewLocal_Current - 1 < 0 ? HologramViewType.Bottom : HologramViewLocal_Current - 1); // Step down, or roll over to max.
                }
                if (MyAPIGateway.Input.IsNewKeyPressed(rotateRightKey))
                {
                    if (gHandler.localGridControlledEntityCustomData.localGridHologramViewType != HologramViewType.Static)
                    {
                        gHandler.SetHologramViewType(false, HologramViewType.Static);
                    }
                    else 
                    {
                        gHandler.SetHologramRotation(false, 0, -90);
                    }
                    //HologramViewLocal_Current = ((int)HologramViewLocal_Current + 1 > HologramViewLocal_Current_MaxSide ? HologramViewType.Rear : HologramViewLocal_Current + 1); // Step up, or roll over to min.
                }
                if (MyAPIGateway.Input.IsNewKeyPressed(rotateUpKey))
                {
                    if (gHandler.localGridControlledEntityCustomData.localGridHologramViewType != HologramViewType.Static)
                    {
                        gHandler.SetHologramViewType(false, HologramViewType.Static);
                    }
                    else 
                    {
                        gHandler.SetHologramRotation(false, 1, 90);
                    }    
                }
                if (MyAPIGateway.Input.IsNewKeyPressed(rotateDownKey))
                {
                    if (gHandler.localGridControlledEntityCustomData.localGridHologramViewType != HologramViewType.Static)
                    {
                        gHandler.SetHologramViewType(false, HologramViewType.Static);
                    }
                    else 
                    {
                        gHandler.SetHologramRotation(false, 1, -90);
                    }  
                }
                if (MyAPIGateway.Input.IsNewKeyPressed(rotatePosZKey))
                {
                    if (gHandler.localGridControlledEntityCustomData.localGridHologramViewType != HologramViewType.Static)
                    {
                        gHandler.SetHologramViewType(false, HologramViewType.Static);
                    }
                    else 
                    {
                        gHandler.SetHologramRotation(false, 2, 90);
                    }   
                }
                if (MyAPIGateway.Input.IsNewKeyPressed(rotateNegZKey))
                {
                    if (gHandler.localGridControlledEntityCustomData.localGridHologramViewType != HologramViewType.Static)
                    {
                        gHandler.SetHologramViewType(false, HologramViewType.Static);
                    }
                    else 
                    {
                        gHandler.SetHologramRotation(false, 2, -90);
                    }
                }
                if (MyAPIGateway.Input.IsNewKeyPressed(resetKey))
                {
                    if (gHandler.localGridControlledEntityCustomData.localGridHologramViewType != HologramViewType.Static)
                    {
                        gHandler.SetHologramViewType(false, HologramViewType.Static);
                    }
                    else 
                    {
                        gHandler.SetHologramRotation(false, 0, 361); // Anything >= 360 gets set back to 0.
                        gHandler.SetHologramRotation(false, 1, 361); // Anything >= 360 gets set back to 0.
                        gHandler.SetHologramRotation(false, 2, 361); // Anything >= 360 gets set back to 0.
                    }
                }
            }
			else 
			{
                if (MyAPIGateway.Input.IsNewKeyPressed(rotateLeftKey))
                {
                    if (gHandler.localGridControlledEntityCustomData.targetGridHologramViewType != HologramViewType.Static)
                    {
                        gHandler.SetHologramViewType(true, HologramViewType.Static);
                    }
                    else
                    {
                        gHandler.SetHologramRotation(true, 0, 90);
                    }
                    //HologramViewLocal_Current = ((int)HologramViewLocal_Current - 1 < 0 ? HologramViewType.Bottom : HologramViewLocal_Current - 1); // Step down, or roll over to max.
                }
                if (MyAPIGateway.Input.IsNewKeyPressed(rotateRightKey))
                {
                    if (gHandler.localGridControlledEntityCustomData.targetGridHologramViewType != HologramViewType.Static)
                    {
                        gHandler.SetHologramViewType(true, HologramViewType.Static);
                    }
                    else
                    {
                        gHandler.SetHologramRotation(true, 0, -90);
                    }
                    //HologramViewLocal_Current = ((int)HologramViewLocal_Current + 1 > HologramViewLocal_Current_MaxSide ? HologramViewType.Rear : HologramViewLocal_Current + 1); // Step up, or roll over to min.
                }
                if (MyAPIGateway.Input.IsNewKeyPressed(rotateUpKey))
                {
                    if (gHandler.localGridControlledEntityCustomData.targetGridHologramViewType != HologramViewType.Static)
                    {
                        gHandler.SetHologramViewType(true, HologramViewType.Static);
                    }
                    else
                    {
                        gHandler.SetHologramRotation(true, 1, 90);
                    }
                }
                if (MyAPIGateway.Input.IsNewKeyPressed(rotateDownKey))
                {
                    if (gHandler.localGridControlledEntityCustomData.targetGridHologramViewType != HologramViewType.Static)
                    {
                        gHandler.SetHologramViewType(true, HologramViewType.Static);
                    }
                    else
                    {
                        gHandler.SetHologramRotation(true, 1, -90);
                    }
                }
                if (MyAPIGateway.Input.IsNewKeyPressed(rotatePosZKey))
                {
                    if (gHandler.localGridControlledEntityCustomData.targetGridHologramViewType != HologramViewType.Static)
                    {
                        gHandler.SetHologramViewType(true, HologramViewType.Static);
                    }
                    else
                    {
                        gHandler.SetHologramRotation(true, 2, 90);
                    }
                }
                if (MyAPIGateway.Input.IsNewKeyPressed(rotateNegZKey))
                {
                    if (gHandler.localGridControlledEntityCustomData.targetGridHologramViewType != HologramViewType.Static)
                    {
                        gHandler.SetHologramViewType(true, HologramViewType.Static);
                    }
                    else
                    {
                        gHandler.SetHologramRotation(true, 2, -90);
                    }
                }
                if (MyAPIGateway.Input.IsNewKeyPressed(resetKey))
                {
                    if (gHandler.localGridControlledEntityCustomData.targetGridHologramViewType != HologramViewType.Static)
                    {
                        gHandler.SetHologramViewType(true, HologramViewType.Static);
                    }
                    else
                    {
                        gHandler.SetHologramRotation(true, 0, 361); // Anything >= 360 gets set back to 0.
                        gHandler.SetHologramRotation(true, 1, 361); // Anything >= 360 gets set back to 0.
                        gHandler.SetHologramRotation(true, 2, 361); // Anything >= 360 gets set back to 0.
                    }
                }
                if (MyAPIGateway.Input.IsNewKeyPressed(orbitKey))
                {
                    gHandler.SetHologramViewType(true, HologramViewType.Orbit);
                }
                if (MyAPIGateway.Input.IsNewKeyPressed(perspectiveKey))
                {
                    gHandler.SetHologramViewType(true, HologramViewType.Perspective);
                    //HologramViewTarget_Current = HologramViewType.Perspective; // Perspective
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

            List<Sandbox.ModAPI.IMyRadioAntenna> antennas = new List<Sandbox.ModAPI.IMyRadioAntenna>();
            MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid).GetBlocksOfType(antennas, a => a.IsWorking && a.Enabled && a.IsFunctional);

            foreach (Sandbox.ModAPI.IMyRadioAntenna antenna in antennas)
            {
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
            // Store calculate variables once here and re-use.
            VRage.Game.ModAPI.IMyCubeGrid entityGrid = entity as VRage.Game.ModAPI.IMyCubeGrid; // Is this a grid? will be Null if not.
			Vector3D entityPos = entity.GetPosition();
            double gridMaxActiveRangeSqr = gridMaxActiveRange * gridMaxActiveRange;
            double gridMaxPassiveRangeSqr = gridMaxPassiveRange * gridMaxPassiveRange;
            Vector3D relativePos = entityPos - gridPos; // Position of entity relative to grid
            double relativeDistanceSqr = relativePos.LengthSquared();

            if (!theSettings.useSigIntLite) 
			{
				// When not using SigInt lite we use the maxPassiveRange check only, which should already be limitted to the max radar range setting.
				if (IsWithinRadarRadius(relativeDistanceSqr, gridMaxPassiveRangeSqr))
				{
					canDetect = true;
					entityPosReturn = entityPos;
					relativeDistanceSqrReturn = relativeDistanceSqr;
					return;  // Detected, set values and return
				}
				else 
				{
					return; // Can't detect it, return defaults.
                }
            }

            // Check if even a grid first
            if (entityGrid == null && !gridHasActiveRadar)
            {
				// If grid has no active radar, and the entity to detect is not a grid, we can safely exit and return canDetect = false.
				// We make this assertation that anything that isn't a grid can't possibly have active radar.
				// In future I may have to change this if I do SigInt rework to allow other active grids signals to paint nearby entities...
				return; // Can't detect it, return defaults.
            }

			

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

        /// <summary>
        /// This checks if a grid can detect another grid. Requires you know the grid's radar status and pass values in first. Will query the target grid's radar status as required.
        /// </summary>
        /// <param name="gridHasPassiveRadar"></param>
        /// <param name="gridHasActiveRadar"></param>
        /// <param name="gridMaxPassiveRange"></param>
        /// <param name="gridMaxActiveRange"></param>
        /// <param name="gridPos"></param>
        /// <param name="canDetect"></param>
        /// <param name="entityPosReturn"></param>
        /// <param name="relativeDistanceSqrReturn"></param>
        public void CanGridRadarDetectTarget(bool gridHasPassiveRadar, bool gridHasActiveRadar, double gridMaxPassiveRange, double gridMaxActiveRange, Vector3D gridPos,
           out bool canDetect, out Vector3D entityPosReturn, out double relativeDistanceSqrReturn)
        {
            canDetect = false;
            entityPosReturn = new Vector3D();
            relativeDistanceSqrReturn = 0;

            if (!gridHasPassiveRadar && !gridHasActiveRadar)
            {
                // If no functional radar we can't detect anything
                return; // Can't detect it, return defaults.
            }
            // Store calculate variables once here and re-use.
            VRage.Game.ModAPI.IMyCubeGrid entityGrid = gHandler.targetGrid; // Is this a grid? will be Null if not.
            Vector3D entityPos = entityGrid.GetPosition();
            double gridMaxActiveRangeSqr = gridMaxActiveRange * gridMaxActiveRange;
            double gridMaxPassiveRangeSqr = gridMaxPassiveRange * gridMaxPassiveRange;
            Vector3D relativePos = entityPos - gridPos; // Position of entity relative to grid
            double relativeDistanceSqr = relativePos.LengthSquared();

            if (!theSettings.useSigIntLite)
            {
                // When not using SigInt lite we use the maxPassiveRange check only, which should already be limitted to the max radar range setting.
                if (IsWithinRadarRadius(relativeDistanceSqr, gridMaxPassiveRangeSqr))
                {
                    canDetect = true;
                    entityPosReturn = entityPos;
                    relativeDistanceSqrReturn = relativeDistanceSqr;
                    return;  // Detected, set values and return
                }
                else
                {
                    return; // Can't detect it, return defaults.
                }
            }

            // Check if even a grid first
            if (entityGrid == null && !gridHasActiveRadar)
            {
                // If grid has no active radar, and the entity to detect is not a grid, we can safely exit and return canDetect = false.
                // We make this assertation that anything that isn't a grid can't possibly have active radar.
                // In future I may have to change this if I do SigInt rework to allow other active grids signals to paint nearby entities...
                return; // Can't detect it, return defaults.
            }

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
                bool entityHasPassiveRadar = gHandler.targetGridHasPassiveRadar; // Not used
                bool entityHasActiveRadar = gHandler.targetGridHasActiveRadar; // Is entityGrid sending out signals grid can passively receive?
                double entityMaxPassiveRange = gHandler.targetGridMaxPassiveRadarRange; // Not used
                double entityMaxActiveRange = gHandler.targetGridMaxActiveRadarRange; // Grid can receive entityGrid's signals if within this range.

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
        /// <summary>
        /// Check if a distance is within the radar detection range.
        /// </summary>
        /// <param name="relativeDistanceSqr"></param>
        /// <param name="radarDetectionRangeSqr"></param>
        /// <returns></returns>
        public bool IsWithinRadarRadius(double relativeDistanceSqr, double radarDetectionRangeSqr) 
		{
			if (relativeDistanceSqr <= radarDetectionRangeSqr)
			{
				return true;
			}
			return false;
		}

		

		/// <summary>
		/// Clears the target
		/// </summary>
		private void ReleaseTarget()
		{
            isTargetLocked = false;
            gHandler.ResetTargetGrid();
            //HG_initializedTarget = false;
            //HG_activationTimeTarget = 0;
            //currentTarget = null;
            PlayCustomSound(SP_ZOOMIN, worldRadarPos);
        }

		/// <summary>
		/// Target locks the entity passed in
		/// </summary>
		/// <param name="newTarget"></param>
		private void LockTarget(VRage.ModAPI.IMyEntity newTarget) 
		{
            VRage.Game.ModAPI.IMyCubeGrid newTargetGrid = newTarget as VRage.Game.ModAPI.IMyCubeGrid;
            if (newTargetGrid != null) 
            {
                gHandler.SetTargetGrid(newTargetGrid);
                isTargetLocked = true;
                NewBlipAnim(newTarget);
                PlayCustomSound(SP_ZOOMOUT, worldRadarPos);
                if (_isWeaponCore && _weaponCoreInitialized && _weaponCoreWeaponsInitialized)
                {
                    LockTargetWeaponCore(newTarget);
                }
            }
        }

        private Dictionary<string, Delegate> weaponCoreApi = null;
        private Action<Sandbox.ModAPI.IMyTerminalBlock, VRage.ModAPI.IMyEntity> setTarget;
        Action<Sandbox.ModAPI.IMyTerminalBlock, VRage.ModAPI.IMyEntity, bool, bool> setTargetFocus;
        Func<Sandbox.ModAPI.IMyTerminalBlock, bool> hasCoreWeapon;
        private const long WEAPONCORE_MOD_ID = 5649865810; // WC's Mod Communication ID (check your version if this changes)

        /// <summary>
        /// Initialize weapon core, WIP.
        /// </summary>
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

        /// <summary>
        /// Initialize weapon core weapons, WIP.
        /// </summary>
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
                            //if (!_localGridWCWeapons.Contains(terminalBlock))
                            //{
                            //    _localGridWCWeapons.Add(terminalBlock);
                            //}

                        }
                    }
                }
                _weaponCoreWeaponsInitialized = true;
            }
        }

        /// <summary>
        /// Locks target using WeaponCore API, WIP. Broken, does not work. 
        /// </summary>
        /// <param name="newTarget"></param>
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
			//setTargetFocus(_localGridWCWeapons.First<Sandbox.ModAPI.IMyTerminalBlock>(), newTarget, true, false);
        }

        /// <summary>
        /// Is a weaponcore weapon? How can we even tell this?
        /// </summary>
        /// <param name="block"></param>
        /// <returns></returns>
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
                VRage.Game.ModAPI.IMyCubeGrid cubeGrid = e as VRage.Game.ModAPI.IMyCubeGrid;
                if (cubeGrid != null)
                {
                    if (cubeGrid.EntityId == gHandler.localGrid.EntityId) 
                    {
                        return false;
                    }
                    if (gHandler.targetGrid != null && cubeGrid.EntityId == gHandler.targetGrid.EntityId)
                    {
                        return false; // Don't select the same target twice. 
                    }
					if (gHandler.localGridControlledEntityCustomData.scannerOnlyPoweredGrids && !HasPowerProduction(cubeGrid))
					{
						// If powered only don't allow selecting unpowered grids. 
						return false;
					}
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
        private void DrawHologramStatus(Vector3D hgPos, Vector3D shieldPos, Vector3D textPosOffset, double activationTime, double shieldLast, double shieldCurrent, double shieldMax, double healthCurrent, double healthMax, 
			double deltaTime, double timeToReady, double fontSize, bool isTarget)
        {
            if (isTarget) 
            {
                //MyLog.Default.WriteLine($"FENIX_HUD: TargetGrid drawstatus healthCurrent = {healthCurrent}");
            }
            // An alpha value that ramps up over time, used for boot-up effect
            double bootUpAlpha = activationTime;
            bootUpAlpha = ClampedD(bootUpAlpha, 0, 1);
            bootUpAlpha = Math.Pow(bootUpAlpha, 0.25);

            //bootUpAlpha (bootTime in drawShields) seems to be the problem, it never increases? 

            //Shields, 
            if (shieldMax > 0.01)
            {
                double tempShields = LerpD(shieldLast, shieldCurrent, deltaTime * 2);

               // Vector3D ShieldPos_Right = worldRadarPos + radarMatrix.Left * -radarRadius + radarMatrix.Left * HG_Offset.X + radarMatrix.Up * HG_Offset.Y + radarMatrix.Forward * HG_Offset.Z;
                DrawShields(shieldPos, tempShields, shieldMax, bootUpAlpha, timeToReady);
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
            if (!isTarget)
            {
                DrawArc(hgPos, 0.065, radarMatrix.Up, 0, 90 * ((float)hitPoints / 100) * (float)bootUpAlpha, gHandler.localGridControlledEntityCustomData.lineColor, 0.015f, 1f);
            }
            else 
            {
                DrawArc(hgPos, 0.065, radarMatrix.Up, 368 - (88 * (float)hitPoints / 100) * (float)bootUpAlpha, 360, gHandler.localGridControlledEntityCustomData.lineColor, 0.015f, 1f);
            }
            string hitPointsString = Convert.ToString(Math.Ceiling(healthCurrent / 100 * bootUpAlpha));
            Vector3D textPos = hgPos + (radarMatrix.Left * (hitPointsString.Length * fontSize)) + (radarMatrix.Up * fontSize) + (radarMatrix.Forward * fontSize * 0.5) + textPosOffset;
            DrawText(hitPointsString, fontSize, textPos, radarMatrix.Forward, gHandler.localGridControlledEntityCustomData.lineColor, gHandler.localGridControlledEntityCustomData.radarBrightness, 1f, false);
        }


       /// <summary>
       /// Draw the holograms on screen for an active seat
       /// </summary>
        public void DrawHolograms() 
        {
            if (theSettings.enableHologramsGlobal) 
            {
                // TODO for some reason the local hologram rotation is not being cancelled out so it moves as you rotate :C
                double fontSize = 0.005;
                if (gHandler.localGridControlledEntityCustomData.enableHologramsLocalGrid && gHandler.localGridInitialized && gHandler.localGridBlocksInitialized && gHandler.localGridBlockClusters != null)
                {
                    double flipperAxis = 1;
                    Vector3D hologramOffset = radarMatrix.Left * -gHandler.localGridControlledEntityCustomData.radarRadius * flipperAxis + radarMatrix.Left * _hologramRightOffset_HardCode.X * flipperAxis + radarMatrix.Up * _hologramRightOffset_HardCode.Y + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
                    Vector3D hologramDrawPosition = worldRadarPos + hologramOffset;
                    Vector3D holoCenterOffset = radarMatrix.Left * -gHandler.localGridControlledEntityCustomData.radarRadius * flipperAxis + radarMatrix.Left * _hologramRightOffset_HardCode.X * flipperAxis + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
                    Vector3D holoCenterPosition = worldRadarPos + holoCenterOffset;
                    Vector4 initialColor = gHandler.localGridControlledEntityCustomData.lineColor * 0.5f;
                    initialColor.W = 1;

                    MatrixD finalScalingAndRotationMatrix = gHandler.localHologramFinalRotation * gHandler.localHologramScalingMatrix;

                    double thicc = gHandler.hologramScaleFactor / (gHandler.localGrid.WorldVolume.Radius / gHandler.localGrid.GridSize);
                    float size = (float)gHandler.hologramScale * 0.65f * (float)thicc;

                    DrawHologramFromClusters(gHandler.localGridBlockClusters, false, hologramDrawPosition, holoCenterPosition, 
                        initialColor, finalScalingAndRotationMatrix, size, (float)gHandler.hologramScale * 0.5f);

                    Vector3D hgPos_Right = worldRadarPos + radarMatrix.Right * gHandler.localGridControlledEntityCustomData.radarRadius + radarMatrix.Left * _hologramRightOffset_HardCode.X + radarMatrix.Down * 0.0075 + (radarMatrix.Forward * fontSize * 2);
                    Vector3D textPos_Offset = (radarMatrix.Left * 0.065);
                    Vector3D shieldPos_Right = worldRadarPos + radarMatrix.Left * -gHandler.localGridControlledEntityCustomData.radarRadius + radarMatrix.Left * _hologramRightOffset_HardCode.X + radarMatrix.Up * _hologramRightOffset_HardCode.Y + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
                    DrawHologramStatus(hgPos_Right, shieldPos_Right, textPos_Offset, gHandler.localGridHologramActivationTime, gHandler.localGridJumpDrivePowerStoredPrevious, gHandler.localGridJumpDrivePowerStored,
                        gHandler.localGridJumpDrivePowerStoredMax, gHandler.localGridCurrentIntegrity, gHandler.localGridMaxIntegrity, gHandler.deltaTimeSinceLastTick, gHandler.localGridJumpDriveTimeToReady, fontSize, false);
                }

                if (gHandler.localGridControlledEntityCustomData.enableHologramsTargetGrid && gHandler.targetGridInitialized && gHandler.targetGridBlocksInitialized && gHandler.targetGridBlockClusters != null)
                {
                    double flipperAxis = -1;
                    Vector3D hologramOffset = radarMatrix.Left * -gHandler.localGridControlledEntityCustomData.radarRadius * flipperAxis + radarMatrix.Left * _hologramRightOffset_HardCode.X * flipperAxis + radarMatrix.Up * _hologramRightOffset_HardCode.Y + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
                    Vector3D hologramDrawPosition = worldRadarPos + hologramOffset;
                    Vector3D holoCenterOffset = radarMatrix.Left * -gHandler.localGridControlledEntityCustomData.radarRadius * flipperAxis + radarMatrix.Left * _hologramRightOffset_HardCode.X * flipperAxis + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
                    Vector3D holoCenterPosition = worldRadarPos + holoCenterOffset;
                    Vector4 initialColor = gHandler.localGridControlledEntityCustomData.lineColorComp * 0.5f;
                    initialColor.W = 1;

                    MatrixD finalScalingAndRotationMatrix = gHandler.targetHologramScalingMatrix * gHandler.targetHologramFinalRotation;

                    double thicc = gHandler.hologramScaleFactor / (gHandler.targetGrid.WorldVolume.Radius / gHandler.targetGrid.GridSize);
                    float size = (float)gHandler.hologramScale * 0.65f * (float)thicc;

                    DrawHologramFromClusters(gHandler.targetGridBlockClusters, true, hologramDrawPosition, holoCenterPosition, 
                        initialColor, finalScalingAndRotationMatrix, size, (float)gHandler.hologramScale * 0.5f);
                   
                    Vector3D hgPos_Left = worldRadarPos + radarMatrix.Left * gHandler.localGridControlledEntityCustomData.radarRadius + radarMatrix.Right * _hologramRightOffset_HardCode.X + radarMatrix.Down * 0.0075 + (radarMatrix.Forward * fontSize * 2);
                    Vector3D textPos_Offset = (radarMatrix.Right * 0.065);
                    Vector3D shieldPos_Left = worldRadarPos + radarMatrix.Right * -gHandler.localGridControlledEntityCustomData.radarRadius + radarMatrix.Right * _hologramRightOffset_HardCode.X + radarMatrix.Up * _hologramRightOffset_HardCode.Y + radarMatrix.Forward * _hologramRightOffset_HardCode.Z;
                    DrawHologramStatus(hgPos_Left, shieldPos_Left, textPos_Offset, gHandler.targetGridHologramActivationTime, gHandler.targetGridJumpDrivePowerStoredPrevious, gHandler.targetGridJumpDrivePowerStored,
                        gHandler.targetGridJumpDrivePowerStoredMax, gHandler.targetGridCurrentIntegrity, gHandler.targetGridMaxIntegrity, gHandler.deltaTimeSinceLastTick, gHandler.targetGridJumpDriveTimeToReady, fontSize, true);
                }
            }
        }

        /// <summary>
        /// Draw the holograms on screen for all holotables.
        /// </summary>
        public void DrawHologramsHoloTables() 
        {
            if (gHandler.localGridHologramTerminals.Count > 0 && gHandler.localGridBlockClusters != null)
            {
                MatrixD rotationOnlyLocalGridMatrix = gHandler.localGrid.WorldMatrix;
                rotationOnlyLocalGridMatrix.Translation = Vector3D.Zero;

                foreach (KeyValuePair<Vector3I, Sandbox.ModAPI.IMyTerminalBlock> holoTableKeyValuePair in gHandler.localGridHologramTerminals)
                {
                    Sandbox.ModAPI.IMyTerminalBlock holoTable = holoTableKeyValuePair.Value;
                    if (!holoTable.IsFunctional || !holoTable.IsWorking)
                    {
                        continue;
                    }
                    HologramCustomData theData;
                    HologramCustomDataTerminalPair thePair;
                    if (gHandler.localGridHologramTerminalsData.TryGetValue(holoTableKeyValuePair.Key, out thePair)) 
                    {
                        theData = thePair.HologramCustomData;
                        MatrixD localHologramViewRotationHoloTable = MatrixD.CreateFromYawPitchRoll(MathHelper.ToRadians(theData.holoRotationX),
                        MathHelper.ToRadians(theData.holoRotationY),
                        MathHelper.ToRadians(theData.holoRotationZ));
                        MatrixD localHologramFinalRotationHoloTable = localHologramViewRotationHoloTable * rotationOnlyLocalGridMatrix;

                        double thicc = gHandler.hologramScaleFactor / (gHandler.localGrid.WorldVolume.Radius / gHandler.localGrid.GridSize);
                        float size = (float)theData.holoScale * 0.65f * (float)thicc;
                        MatrixD holoTableScalingMatrix = MatrixD.CreateScale(theData.holoScale * thicc);

                        MatrixD finalScalingAndRotationMatrix = localHologramFinalRotationHoloTable * holoTableScalingMatrix;
                        
                        MatrixD holoTableMatrix = holoTable.WorldMatrix;
                        Vector3D holoTableOffset = new Vector3D(theData.holoX, theData.holoY, theData.holoZ);
                        Vector3D holoTableBaseOffset = new Vector3D(theData.holoBaseX, theData.holoBaseY, theData.holoBaseZ);
                        Vector3D holoTablePos = holoTableMatrix.Translation + Vector3D.TransformNormal(holoTableOffset, holoTableMatrix);
                        Vector3D holoCenterPosition = holoTableMatrix.Translation + Vector3D.TransformNormal(holoTableBaseOffset, holoTableMatrix);
                        Vector4 initialColor = theData.lineColor * 0.5f;
                        initialColor.W = 1;

                        Vector4[] colorPalletHoloTable;
                        if(!gHandler.localGridHologramTerminalPallets.TryGetValue(holoTableKeyValuePair.Key, out colorPalletHoloTable))
                        { 
                            colorPalletHoloTable = gHandler.BuildIntegrityColors(theData.lineColor * 0.5f);
                        }

                        DrawHologramFromClusters(gHandler.localGridBlockClusters, false, holoTablePos, holoCenterPosition, 
                            initialColor, finalScalingAndRotationMatrix, size, (float)theData.holoScale * 0.5f);
                        
                    }
                }
            }
        }

        /// <summary>
        /// Draw the shields for a hologram.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="sp_cur"></param>
        /// <param name="sp_max"></param>
        /// <param name="bootTime"></param>
        /// <param name="time2ready"></param>
        private void DrawShields(Vector3D pos, double sp_cur, double sp_max, double bootTime, double time2ready)
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
					DrawCircle(pos, 0.08 - (1 - boot3) * 0.08, dir, gHandler.localGridControlledEntityCustomData.lineColorComp * (float)Math.Ceiling (boot3), gHandler.localGridControlledEntityCustomData.radarBrightness, dotty, false, 1, 0.001f * (float)yShieldPer1);
				}
				if (yShieldPer2 > 0.01) 
				{
					dotty = yShieldPer2 < 0.25;
					DrawCircle(pos, 0.085 - (1-boot2)*0.085, dir, gHandler.localGridControlledEntityCustomData.lineColorComp * (float)Math.Ceiling(boot2), gHandler.localGridControlledEntityCustomData.radarBrightness, dotty, false, 1, 0.001f * (float)yShieldPer2);
				}
				if (yShieldPer3 > 0.01) 
				{
					dotty = yShieldPer3 < 0.25;
					DrawCircle(pos, 0.09 - (1-boot1)*0.09, dir, gHandler.localGridControlledEntityCustomData.lineColorComp * (float)Math.Ceiling(boot1), gHandler.localGridControlledEntityCustomData.radarBrightness, dotty, false, 1, 0.001f * (float)yShieldPer3);
				}
			}
			else
			{
				DrawCircle(pos, 0.08, dir, new Vector4(1,0,0,1), 1f, true, false, 0.5f, 0.001f);
			}

			double fontSize = 0.005;
			string ShieldValueS = Convert.ToString(Math.Round(sp_cur * bootTime * 1000));
			Vector4 ShieldValueC = gHandler.localGridControlledEntityCustomData.lineColorComp;

			if (sp < 0.01) 
			{
				ShieldValueS = "SHIELDS DOWN";
				ShieldValueC = new Vector4(1, 0, 0, 1);

				string time2readyS = FormatSecondsToReadableTime(time2ready);

				Vector3D timePos = pos + (radarMatrix.Backward * 0.065) + (radarMatrix.Down * fontSize * 8) + (radarMatrix.Left * time2readyS.Length * fontSize);
				DrawText(time2readyS, fontSize, timePos, radarMatrix.Forward, ShieldValueC, gHandler.localGridControlledEntityCustomData.radarBrightness, 1, false);
			}
			Vector3D textPos = pos + (radarMatrix.Backward * 0.065) + (radarMatrix.Down * fontSize * 4) + (radarMatrix.Left * ShieldValueS.Length * fontSize);
			DrawText(ShieldValueS, fontSize, textPos, radarMatrix.Forward, ShieldValueC, gHandler.localGridControlledEntityCustomData.radarBrightness, 1, false);
        }

        /// <summary>
        /// Format seconds to a readable time
        /// </summary>
        /// <param name="seconds"></param>
        /// <returns></returns>
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


        /// <summary>
        /// Computes a stable left and up vector for oriented billboards.
        /// Always faces the camera, keeps hologram's Up, and avoids degenerate cross products.
        /// </summary>
        public static void GetBillboardAxes(Vector3D hologramUp, Vector3D cameraPos, Vector3D clusterPos, out Vector3D axisLeft, out Vector3D axisUp)
        {
            // Vector from cluster to camera
            Vector3D cameraDir = cameraPos - clusterPos;
            if (cameraDir.LengthSquared() < 1e-9)
                cameraDir = Vector3D.Forward; // fallback if camera is exactly at cluster

            cameraDir.Normalize();

            // Check if cameraDir is almost parallel to Up
            double dot = Math.Abs(Vector3D.Dot(cameraDir, hologramUp));
            Vector3D tempUp = hologramUp;

            if (dot > 0.90) // nearly parallel, pick fallback
            {
                Vector3D fallback = Vector3D.Right;
                if (Math.Abs(Vector3D.Dot(tempUp, fallback)) > 0.90)
                    fallback = Vector3D.Forward;

                axisLeft = Vector3D.Cross(tempUp, fallback);
                axisLeft.Normalize();
                axisUp = Vector3D.Cross(cameraDir, axisLeft);
                axisUp.Normalize();
            }
            else
            {
                axisLeft = Vector3D.Cross(tempUp, cameraDir);
                axisLeft.Normalize();
                axisUp = Vector3D.Cross(cameraDir, axisLeft);
                axisUp.Normalize();
            }
        }

        /// <summary>
        /// Draw a hologram from block clusters instead of drawing every block.
        /// </summary>
        /// <param name="blockClusters"></param>
        /// <param name="isTarget"></param>
        /// <param name="hologramDrawPosition"></param>
        /// <param name="holoCenterPosition"></param>
        /// <param name="initialColor"></param>
        /// <param name="finalScalingAndRotationMatrix"></param>
        /// <param name="size"></param>
        /// <param name="scale"></param>
        private void DrawHologramFromClusters(Dictionary<Vector3I, BlockCluster> blockClusters, bool isTarget, Vector3D hologramDrawPosition, 
            Vector3D holoCenterPosition, Vector4 initialColor, MatrixD finalScalingAndRotationMatrix, float size, float scale) 
        {
            IMyCamera camera = MyAPIGateway.Session.Camera;
            Vector3D AxisLeft = camera.WorldMatrix.Left;
            Vector3D AxisUp = camera.WorldMatrix.Up;
            Vector3D AxisForward = camera.WorldMatrix.Forward;


            double bootUpAlpha = 1;
            if (isTarget)
            {
                bootUpAlpha = gHandler.localGridHologramBootUpAlpha;
            }
            else
            {
                bootUpAlpha = gHandler.targetGridHologramBootUpAlpha;
            }

            //double thicc = gHandler.hologramScaleFactor / (isTarget ? gHandler.targetGrid.WorldVolume.Radius / gHandler.targetGrid.GridSize : gHandler.localGrid.WorldVolume.Radius / gHandler.localGrid.GridSize);
            //float size = (float)gHandler.hologramScale * 0.65f * (float)thicc * (isTarget ? gHandler.targetGridClusterSize : gHandler.localGridClusterSize);

            MyStringId drawMaterial = MaterialSquare;

            float projectorRatio = Math.Min(0.5f, 100f / blockClusters.Count); // Clamp at max of 50% of the time, but base it around trying to draw around 100 projector lines.
                                                                               // For larger grids it will limit it at 100 lines. But for smaller grids it will draw only 50% of the time so the lines still look good.

            foreach (KeyValuePair<Vector3I, BlockCluster> blockClusterKeyPair in blockClusters) 
            {
                BlockCluster blockCluster = blockClusterKeyPair.Value;
                //Vector3I blockClusterPosition = blockClusterKeyPair.Key;
                Vector3D blockClusterPosition = blockClusterKeyPair.Value.DrawPosition;
                float blockClusterIntegrityRatio = (blockCluster.MaxIntegrity != 0) ? blockCluster.Integrity / blockCluster.MaxIntegrity : 0;
                Vector3D clusterDrawPosition = Vector3D.Zero;

                if (blockClusterIntegrityRatio < 0.01)
                {
                    continue; // Skip clusters too low integrity to draw. 
                }

                Vector3I[] neighborOffsets = new Vector3I[]
                {
                    new Vector3I(blockCluster.ClusterSize, 0, 0),    // +X
                    new Vector3I(-blockCluster.ClusterSize, 0, 0),   // -X
                    new Vector3I(0, blockCluster.ClusterSize, 0),    // +Y
                    new Vector3I(0, -blockCluster.ClusterSize, 0),   // -Y
                    new Vector3I(0, 0, blockCluster.ClusterSize),    // +Z
                    new Vector3I(0, 0, -blockCluster.ClusterSize)    // -Z
                };

                bool fullySurrounded = true;
                foreach (Vector3I offset in neighborOffsets)
                {
                    if (!blockClusters.ContainsKey(blockClusterKeyPair.Key + offset))
                    {
                        fullySurrounded = false;
                        break;
                    }
                }

                if (fullySurrounded) 
                {
                    continue; // skip drawing fully surrounded clusters
                }

                float drawSize = size * blockCluster.ClusterSize;

                bool randomize = false;
                if (gHandler.localGridGlitchAmount > 0.5 || GetRandomFloat() > 0.95f  || GetRandomDouble() > bootUpAlpha)
                {
                    // Randomize 5% of the time, or if glitch amount > 0.5. 
                    randomize = true;
                }

                // key colors
                Vector4 hologramColor = initialColor; // starting color
                Vector4 yellow = new Vector4(1f, 1f, 0f, 1f);
                Vector4 red = new Vector4(1f, 0f, 0f, 1f);
                if (randomize)
                {
                    // Randomize the color a bit if glitch or boot up. 
                    hologramColor *= Clamped(GetRandomFloat(), 0.25f, 1);
                }

                Vector4 finalColor;

                if (blockClusterIntegrityRatio >= 0.5f)
                {
                    // Map [0.5–1.0] -> [0–1], then blend hologramColor -> yellow
                    float interpolationFactor = (blockClusterIntegrityRatio - 0.5f) / 0.5f;
                    finalColor = Vector4.Lerp(yellow, hologramColor, interpolationFactor);
                }
                else
                {
                    // Map [0–0.5] -> [0–1], then blend red -> yellow
                    float interpolationFactor = blockClusterIntegrityRatio / 0.5f;
                    finalColor = Vector4.Lerp(red, yellow, interpolationFactor);
                }

                // finalScalingAndRotationMatrix takes the local or target final rotation matrix. The final matrix is re-calculated each draw tick by taking the view rotation matrix (calculated each physics tick) * the localGrid worldMatrix to offset
                // current grid rotation AND apply the user selected "view" override (ie. yaw 90 degrees, pitch 90 degrees etc.)
                MatrixD hologramTransform = finalScalingAndRotationMatrix * MatrixD.CreateTranslation(hologramDrawPosition);
                clusterDrawPosition = Vector3D.Transform(blockClusterPosition, hologramTransform);  

                MyTransparentGeometry.AddBillboardOriented(
                            drawMaterial,
                            finalColor * (float)bootUpAlpha,
                            clusterDrawPosition,
                            AxisLeft, // Billboard orientation
                            AxisUp, // Billboard orientation
                            drawSize,
                            MyBillboard.BlendTypeEnum.AdditiveBottom);

                if (GetRandomFloat() < projectorRatio)
                {
                    Vector3D holoDir = Vector3D.Normalize(clusterDrawPosition - holoCenterPosition);
                    double holoLength = Vector3D.Distance(holoCenterPosition, clusterDrawPosition);
                    DrawLineBillboard(MaterialSquare, initialColor * 0.15f * (float)bootUpAlpha, holoCenterPosition, holoDir, (float)holoLength, scale, BlendTypeEnum.AdditiveBottom);
                }
            }
        }


        /// <summary>
        /// Not in use, but keeping for memories. Was going to use transparent boxes to better bucket blocks, but they have many drawbacks without access to full render pipeline as we cant do face occlusion 
        /// and it draws all adjacent faces between touching boxes...
        /// </summary>
        /// <param name="clusterSlicesByFloor"></param>
        /// <param name="isTarget"></param>
        /// <param name="hologramDrawPosition"></param>
        /// <param name="holoCenterPosition"></param>
        /// <param name="finalScalingAndRotationMatrix"></param>
        /// <param name="colorPallet"></param>
        private void DrawHologramFromClusterSlices(Dictionary<int, List<ClusterBox>> clusterSlicesByFloor, bool isTarget, Vector3D hologramDrawPosition, Vector3D holoCenterPosition, MatrixD finalScalingAndRotationMatrix, Vector4[] colorPallet)    
        {
            MatrixD worldMatrix = finalScalingAndRotationMatrix * MatrixD.CreateTranslation(hologramDrawPosition);
            foreach (KeyValuePair<int, List<ClusterBox>> floorClusterKeyValuePair in clusterSlicesByFloor) 
            {
                int floor = floorClusterKeyValuePair.Key;
                List<ClusterBox> clusterSlicesThisFloor = floorClusterKeyValuePair.Value;
                // Floor by floor draw all cluster boxes
                foreach (ClusterBox cluster in clusterSlicesThisFloor) 
                {
                    if (cluster != null) 
                    {
                        Color color = (Color)colorPallet[cluster.IntegrityBucket];
                        

                        // Local cluster box (axis-aligned, no transforms baked in)
                        BoundingBoxD box = new BoundingBoxD(cluster.Min, cluster.Max + Vector3I.One);

                        // Build the final transform matrix

                        // Draw it
                        MySimpleObjectDraw.DrawTransparentBox(
                            ref worldMatrix,
                            ref box,
                            ref color,
                            MySimpleObjectRasterizer.Solid,
                            1,
                            0.01f,
                            null,
                            null,
                            false
                        );
                    }
                }
            
            }
            //zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz - Contribution from baby 2025-08-25
        }


        /// <summary>
        /// Helper function to scale and clamp angular velocity to rotation angle
        /// </summary>
        /// <param name="angularVelocity"></param>
        /// <param name="maxVelocity"></param>
        /// <param name="maxAngle"></param>
        /// <returns></returns>
        private double ClampAndScale(double angularVelocity, double maxVelocity, double maxAngle)
        {
            // Normalize the angular velocity to (-1, 1) range
            double normalizedVelocity = angularVelocity / maxVelocity;

            // Clamp to prevent values outside (-1, 1)
            normalizedVelocity = Math.Max(-1.0, Math.Min(1.0, normalizedVelocity));

            // Scale to the desired angle range
            return normalizedVelocity * maxAngle;
        }
        
		//------------------------------------------------------------------------------------------------------------




		//==================MONEY=====================================================================================
		private double creditBalance = 0;
		private double creditBalance_fake = 0;
		private double creditBalance_dif = 0;
		private bool credit_counting = false;

        /// <summary>
        /// Format the credits on screen into a readable and rounded value. Converts to larger Unit of Measure as needed.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public string FormatCredits(double value)
        {
            // Billions, Millions, Thousands, or Exact. 
            CultureInfo culture = System.Globalization.CultureInfo.InvariantCulture; // Invariant should use decimal, but no commas. 
            if (value >= 1000000000)
				return (value / 1000000000.0).ToString("0.##", culture) + " B";
			else if (value >= 1000000)
				return (value / 1000000.0).ToString("0.##", culture) + " M";
			else if (value >= 100000)
				return (value / 1000.0).ToString("0.##", culture) + " K";
			else
				return value.ToString("0", culture); 
        }

        /// <summary>
        /// Update the crdit balance
        /// </summary>
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

        /// <summary>
        /// Draw the credit balance on screen
        /// </summary>
        public void DrawCredits()
		{
			// Example usage: Get credits for the local player
			if (MyAPIGateway.Session?.Player != null)
			{
                DrawCreditBalance(creditBalance_fake, creditBalance);
			}
		}

        /// <summary>
        /// Get the credits a player has.
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Convert a string to a double
        /// </summary>
        /// <param name="cr"></param>
        /// <returns></returns>
		private double Convert2Credits(string cr)
		{
			double cr_d = Convert.ToDouble(cr);

			return cr_d;
		}

        /// <summary>
        /// Update the credit balance difference for smooth counting effect.
        /// </summary>
        /// <param name="cr"></param>
        /// <param name="new_cr"></param>
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

        /// <summary>
        /// Draw the credit balance on screen
        /// </summary>
        /// <param name="cr"></param>
        /// <param name="new_cr"></param>
        private void DrawCreditBalance(double cr, double new_cr)
		{
			cr = Math.Round(cr);
			new_cr = Math.Round(new_cr);
			double cr_dif = new_cr - cr;

			Vector4 color = gHandler.localGridControlledEntityCustomData.lineColor;
			double fontSize = 0.005;

			if (cr != new_cr) 
			{
				color *= 2;
				fontSize = 0.006;
			}

			//string crs = "$"+Convert.ToString (cr);
			string crs = $"${FormatCredits(cr)}";
			Vector3D pos = worldRadarPos + (radarMatrix.Right*gHandler.localGridControlledEntityCustomData.radarRadius*1.2) + (radarMatrix.Backward*gHandler.localGridControlledEntityCustomData.radarRadius*0.9);
			Vector3D dir = Vector3D.Normalize((radarMatrix.Forward*4 + radarMatrix.Right)/5);
			DrawText(crs, fontSize, pos, dir, color, gHandler.localGridControlledEntityCustomData.radarBrightness, 1f, false);

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
				DrawText(cr_difs, fontSize, pos, dir, gHandler.localGridControlledEntityCustomData.lineColorComp, gHandler.localGridControlledEntityCustomData.radarBrightness, 1f, false);
			} 
		}
		//------------------------------------------------------------------------------------------------------------












		//== HUD TOOL BARS ==========================================================================================
		private double TB_activationTime = 0;
		private List<AmmoInfo> ammoInfos;

        /// <summary>
        /// Update the hud toolbars
        /// </summary>
		private void UpdateToolbars()
		{
            if (gHandler.localGridControlledEntity == null)
            {
                return;
            }

            Sandbox.ModAPI.IMyCockpit cockpit = gHandler.localGridControlledEntity as Sandbox.ModAPI.IMyCockpit;
            VRage.Game.ModAPI.IMyCubeGrid grid = cockpit.CubeGrid;
            if (grid == null)
            {
                return;
            }
            ammoInfos = GetAmmoInfos(grid);

            TB_activationTime += deltaTimeSinceLastTick * 0.1;
            TB_activationTime = ClampedD(TB_activationTime, 0, 1);
            TB_activationTime = Math.Pow(TB_activationTime, 0.9);
        }


        /// <summary>
        /// Draw the hud toolbars
        /// </summary>
		private void DrawToolbars()
		{
			if (gHandler.localGridControlledEntity == null) {
				return;
			}

            Sandbox.ModAPI.IMyCockpit cockpit = gHandler.localGridControlledEntity as Sandbox.ModAPI.IMyCockpit;
            VRage.Game.ModAPI.IMyCubeGrid grid = cockpit.CubeGrid;
            if (grid != null) 
			{
			} else {
				return;
			}

			Vector3D pos = getToolbarPos();
			//two arcs

			DrawCircle(pos, 0.01, radarMatrix.Forward, gHandler.localGridControlledEntityCustomData.lineColor, gHandler.localGridControlledEntityCustomData.radarBrightness, false, false, 0.25f, 0.001f); // Center Dot
			DrawToolbarBack(pos);			// Left
			DrawToolbarBack(pos, true);	// Right
		}


        /// <summary>
        /// Draw the toolbar background and action slots
        /// </summary>
        /// <param name="position"></param>
        /// <param name="flippit"></param>
		public void DrawToolbarBack(Vector3D position, bool flippit = false)
		{
			Vector3D radarUp = radarMatrix.Up;
			Vector3D radarDown = radarMatrix.Down;

			Vector3D normal = radarMatrix.Forward;

			Vector4 color = gHandler.localGridControlledEntityCustomData.lineColor;


			double TB_bootUp = LerpD(12, 4, TB_activationTime);


			double scale = 1.75;
			double height = 0.1024 * scale;
			double width = 0.0256 * scale;

			if (flippit) 
			{
				position += (radarMatrix.Right * gHandler.localGridControlledEntityCustomData.radarRadius * 2) + (radarMatrix.Right * width * TB_bootUp);
			} 
			else 
			{
				position += (radarMatrix.Left * gHandler.localGridControlledEntityCustomData.radarRadius * 2) + (radarMatrix.Left * width * TB_bootUp);
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
				if (gHandler.localGridGlitchAmount > 0.001) 
				{
					float glitchValue = (float)gHandler.localGridGlitchAmount;

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

        /// <summary>
        /// Draw an action name at a position
        /// </summary>
        /// <param name="name"></param>
        /// <param name="pos"></param>
        /// <param name="flippit"></param>
		private void DrawAction(string name, Vector3D pos, bool flippit = false)
		{
			double fontSize = 0.0075;

			if (!flippit) 
			{
				pos += (radarMatrix.Left * fontSize * 1.8 * name.Length) - (radarMatrix.Left * fontSize * 1.8);
			}
			//DrawCircle (pos, 0.005, radarMatrix.Forward, LINECOLOR_Comp, false, 1f, 0.001f);
			DrawText(name, fontSize, pos, radarMatrix.Forward, gHandler.localGridControlledEntityCustomData.lineColor, gHandler.localGridControlledEntityCustomData.radarBrightness, 1f, false);
		}

        /// <summary>
        /// Get the position to draw the toolbars
        /// </summary>
        /// <returns></returns>
		private Vector3D getToolbarPos()
		{
			Vector3D pos = Vector3D.Zero;

			Vector3D cameraPos = MyAPIGateway.Session.Camera.Position;

			double elevation = GetHeadElevation(worldRadarPos, radarMatrix);

			pos = worldRadarPos + (gHandler.localGridControlledEntityCustomData.radarRadius * 1.5 * radarMatrix.Forward) + (elevation * radarMatrix.Up) +  (radarMatrix.Up * 0.05);

			return pos;
		}

        /// <summary>
        /// Get the player's head elevation relative to a reference position and matrix.
        /// </summary>
        /// <param name="referencePosition"></param>
        /// <param name="referenceMatrix"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Draw an action slot with name and value at a position
        /// </summary>
        /// <param name="slot"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="position"></param>
        /// <param name="height"></param>
        /// <param name="flippit"></param>
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
			DrawText(Convert.ToString(name), 0.005, aPos + (radarMatrix.Up * 0.005 * 3), radarMatrix.Forward, gHandler.localGridControlledEntityCustomData.lineColor, gHandler.localGridControlledEntityCustomData.radarBrightness, 0.75f, false);
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

        /// <summary>
        /// Get information about the currently controlled cockpit, including its name and integrity.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="current"></param>
        /// <param name="max"></param>
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

        /// <summary>
        /// An ammo info class to contain info about ammo type and count
        /// </summary>
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

			foreach (VRage.Game.ModAPI.IMySlimBlock slimBlock in slimBlocks)
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

			}

			List<AmmoInfo> ammoInfos = new List<AmmoInfo>();
			foreach (KeyValuePair<string, int> entry in ammoCounts)
			{
				ammoInfos.Add(new AmmoInfo(entry.Key, entry.Value));
			}

			return ammoInfos;
		}

        /// <summary>
        /// Get the count of an ammo type on a grid by searching all inventories on the grid.
        /// </summary>
        /// <param name="grid"></param>
        /// <param name="ammoType"></param>
        /// <returns></returns>
		private int GetAmmoCount(VRage.Game.ModAPI.IMyCubeGrid grid, string ammoType)
		{
			int count = 0;
			List<VRage.Game.ModAPI.IMySlimBlock> slimBlocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
			grid.GetBlocks(slimBlocks, block => block.FatBlock is IMyInventoryOwner);

			foreach (VRage.Game.ModAPI.IMySlimBlock slimBlock in slimBlocks)
			{
				IMyInventoryOwner inventoryOwner = slimBlock.FatBlock as IMyInventoryOwner;
				if (inventoryOwner != null)
				{

					for (int i = 0; i < inventoryOwner.InventoryCount; i++)
					{
						VRage.Game.ModAPI.Ingame.IMyInventory inventory = inventoryOwner.GetInventory(i);
						List<MyInventoryItem> items = new List<MyInventoryItem>();
						inventory.GetItems(items);

						foreach (MyInventoryItem item in items)
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