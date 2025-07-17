using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Planet;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.ModAPI;

using SpaceEngineers.Game.Entities.Blocks;
using SpaceEngineers.Game.ModAPI;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.IO;
using System.Reflection;

using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.Components;
using VRage.Game.Components.Interfaces;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
//using VRage.ModAPI;
using VRage.Utils;
//using VRage.Input;
using VRageMath;
using Sandbox.ModAPI.Ingame;
using VRageRender;
using System.Diagnostics;
using Sandbox.Game.Lights;
using VRage.Library.Utils;
using Sandbox.Game.World;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Game.Screens.Helpers;
using VRage.Game.ModAPI.Ingame;
using VRage.ModAPI;
using ParallelTasks;
using Sandbox.Game.WorldEnvironment.Modules;

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
This allows for fun things like having a passive antenna up to 50km to pick up grids with active radar on up to that distance (if their active signals are pinging you anyway). 
And have a separate active radar set to 2km to pick up anything near you. This way your leaked signals don't give you away as far, but passive grids (or asteroids!) can't totally sneak up on you. 


--TODO--
TODO: Rework the glitch code regarding the radar, I removed it when trying to hunt down the performance issues when I first started and had no idea what anything in this code even did. 
TODO: (PARTIAL) Make player condition hologram better... Could rotate it on a timer to show all sides, and then if taking damage rotate it so it shows the side being impacted? I have standardized the code so it is far easier to modify and maintain
but made no actual changes to it yet.
TODO: Make target condition hologram better - ideally would show orientation relative to local grid. Ie. if they are facing you it shows them facing you.
TODO: Can we color code power, weapon, antenna, control blocks in the target/player hologram? Then apply a yellow/red color gradient overtop for damage. 

TODO: Make altitude readout

TODO: Maybe enable squish value again? If users want that verticality squish we could toggle it in settings and allow specific value. Ie. 0.0 is off, 0.0-1.0 range. 

TODO: Advanced SigInt logic where active signals from any ship can bounce around and be picked up in passive mode? So one active ship can feed passive fleet?
TODO: Make broadcast range only affect active signal sent out, and make scale be something else configurable that gets stored in the cockpit's block data that can have up/down toggles added to toolbar. 
No idea how to do this so using the broadcast range for now.
TODO: Make entities with active radar stand out visually. 

TODO: Make radar work on a holotable, especially if the above is accomplished!
TODO: Make radar work on LCD with up or down triangles for verticality for eg. Only after holotable. 

TODO: Make target lockon and hologram use GetTopMostParent() so it locks onto the main grid, not subgrids.
TODO: Apparently asteroids can cause phantom pings on planets, should try and look into that for Cherub.
TODO: Compatibility with frame shift drive? Likely around the jumpdrive mechanic? Will look into this.
TODO: Show modded ammo types in the gauges.
TODO: Ability to key-rebind the targetting instead of right click for eg.
TODO: Do above in concert with weaponcore compat, so targetting is shared somehow in a nice way?
TODO: Look into color/scale sliders re-setting or not changing per ship as you leave one and enter another. Might already be fixed?
TODO: Apparently splitting/merging a ship with merge blocks causes a crash.
TODO: Remote control ship and then get out of antenna range (or have remote controlled ship explode for eg) was causing crash. Another author may have fixed that in this already.
TODO: Check shield mod integrations, how can we make that universal across shield mods?
*/





namespace EliDangHUD
{
	// Define configuration settings
	[System.Serializable]
	public class ConfigData
	{
		public float lineThickness = 1.5f;        					// Thickness of the lines
		public int lineDetail = 90;									// Number of segments per circle base
		public Vector3D starPos = new Vector3D(0, 0, 0);			// Star Position
		public  bool starFollowSky = true;							// Does the star position follow the skybox?
		public bool enableCockpitDust = true;
		public bool enableGridFlares = true; // Show a flare graphic on ships to make them more visible in the void
        public bool enableVisor = true;
		public double radarRange = -1;
		public double maxRadarRangeGlobal = -1; // Max global radar range, -1 will use draw distance, otherwise can override if users want to limit radar range below draw limit.
        public int maxPings = 500; // Max pings on radar.
        public int rangeBracketDistance = 500; // Distance in meters for range bracketing. Used in calculations like when targetting to select the "bracket" the target fits in to zoom the radar.
        public bool integrityDisplay = true; // Whether to show ship holograms for player and target globally. 
        public bool useHollowReticle = true; // Use a hollow reticle. Sometimes center dot blocks view of ship targetted, prefer hollow reticle. 
		public double fadeThreshhold = 0.01d; // In percent, distance at edge of radar range where resolution gets "fuzzy" and blips dim/fade away. Allows for smooth transition rather than sudden disappearance off radar. 
    }

	// Define a class to hold planet information
	public class PlanetInfo
	{
		public VRage.ModAPI.IMyEntity Entity 			{ get; set; }
		public double Mass 					{ get; set; }	// We'll use radius as a stand-in for mass
		public double GravitationalRange 	{ get; set; }	// Gravitational range of the planet
		public VRage.ModAPI.IMyEntity ParentEntity 		{ get; set; }	// Parent entity of the planet
	}

	// Define class to contain info about velocity hash marks
	public class VelocityLine
	{
		public float velBirth 				{ get; set; }
		public Vector3D velPosition 		{ get; set; }
		public float velScale 				{ get; set; }
	}

	public enum RelationshipStatus
	{
		Friendly,
		Hostile,
		Neutral,
		FObj,
		Vox
	}

	// Define class to hold information about radar targets
	public class RadarPing
	{
		public VRage.ModAPI.IMyEntity Entity 			{ get; set; }
		public Stopwatch Time 				{ get; set; }
		public float Width 					{ get; set; }
		public RelationshipStatus Status 	{ get; set; }
		public bool Announced 				{ get; set; }
		public Vector4 Color 				{ get; set; }
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

	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation)]
	public class CircleRenderer : MySessionComponentBase
	{

		//-----------CONFIG DATA--------------
		public static float LINE_THICKNESS = 1.5f;
		public static Vector4 LINE_COLOR = new Vector4(1f, 0.5f, 0.0f, 1f);
		public static Vector3 LINE_COLOR_RGB = new Vector3(1f, 0.5f, 0.0);
		public static int LINE_DETAIL = 90;
		public static Vector3D STAR_POS = new Vector3D(0, 0, 0);
		public static bool STAR_FOLLOW_SKY = true;
		public static bool ENABLE_COCKPIT_DUST = true;
		public static bool ENABLE_GRID_FLARES = true; // Show a flare graphic on ships to make them more visible in the void
        public static bool ENABLE_VISOR = true;

		public string configDataFile = 	"dangConfig.xml";
		public ConfigData configData = 	null;

		public static float GLOW = 1f;

		public static double RIPPYRANGE = -1;
		public static double MAX_RADAR_RANGE_GLOBAL = -1; // Max global radar range, -1 will use draw distance, otherwise can override if users want to limit radar range below draw limit.
		public static int MAX_PINGS = 500;
		public static int RANGE_BRACKET_DISTANCE = 2000; // Range brackets in meters. 
		public static bool ENABLE_HOLOGRAMS_GLOBAL = true; // Global toggle for showing ship holograms. 
		public static bool USE_HOLLOW_RETICLE = true; // Use a hollow reticle. Sometimes center dot blocks view of ship targetted, prefer hollow reticle. 
        public static double FADE_THRESHHOLD = 0.01; // In percent, distance at edge of radar range where resolution gets "fuzzy" and blips dim/fade away. Allows for smooth transition rather than sudden disappearance of radar. 



        //------------------------------------

        private MyStringId MaterialDust1 = 				MyStringId.GetOrCompute("ED_DUST1");
		private MyStringId MaterialDust2 = 				MyStringId.GetOrCompute("ED_DUST2");
		private MyStringId MaterialDust3 = 				MyStringId.GetOrCompute("ED_DUST3");
		private MyStringId MaterialVisor = 				MyStringId.GetOrCompute ("ED_visor");

		private MyStringId Material = 					MyStringId.GetOrCompute("Square");
		private MyStringId MaterialLaser = 				MyStringId.GetOrCompute("WeaponLaser");
		private MyStringId MaterialBorder = 			MyStringId.GetOrCompute("ED_Border");
		private MyStringId MaterialCompass = 			MyStringId.GetOrCompute("ED_Compass");
		private MyStringId MaterialCross = 				MyStringId.GetOrCompute("ED_Targetting");
		private MyStringId MaterialCrossOutter = 		MyStringId.GetOrCompute("ED_Targetting_Outter");
		private MyStringId MaterialLockOn = 			MyStringId.GetOrCompute("ED_LockOn");
        private MyStringId MaterialLockOnHollow = MyStringId.GetOrCompute("ED_LockOn_Hollow");
        private MyStringId MaterialToolbarBack = 		MyStringId.GetOrCompute("ED_ToolbarBack");
		private MyStringId MaterialCircle = 			MyStringId.GetOrCompute("ED_Circle");
		private MyStringId MaterialCircleHollow = 		MyStringId.GetOrCompute("ED_CircleHollow");
		private MyStringId MaterialCircleSeeThrough = 	MyStringId.GetOrCompute("ED_CircleSeeThrough");
		private MyStringId MaterialCircleSeeThroughAdd = 	MyStringId.GetOrCompute("ED_CircleSeeThroughAdd");
		private MyStringId MaterialTarget = 			MyStringId.GetOrCompute("ED_TargetArrows");
		private MyStringId MaterialSquare = 			MyStringId.GetOrCompute("ED_Square");
		private MyStringId MaterialTriangle = 			MyStringId.GetOrCompute("ED_Triangle");
		private MyStringId MaterialDiamond = 			MyStringId.GetOrCompute("ED_Diamond");
		private MyStringId MaterialCube = 				MyStringId.GetOrCompute("ED_Cube");
		private MyStringId MaterialShipFlare = 			MyStringId.GetOrCompute("ED_SHIPFLARE");
		private List<string> MaterialFont = 			new List<string> ();

		private Vector4 LINECOLOR_Comp;
		private Vector3 LINECOLOR_Comp_RPG;

        Vector4 color_GridFriend = (Color.Green).ToVector4() * 2;      
        Vector4 color_GridEnemy = (Color.Red).ToVector4() * 4;
        Vector4 color_GridEnemyAttack = (Color.Pink).ToVector4() * 4;	
        Vector4 color_GridNeutral = (Color.Cyan).ToVector4() * 2;     
        Vector4 color_FloatingObject = (Color.DarkGray).ToVector4();        
        Vector4 color_VoxelBase = (Color.DimGray).ToVector4();      

        public HashSet<VRage.ModAPI.IMyEntity> planetList = new HashSet<VRage.ModAPI.IMyEntity>();

		public List<PlanetInfo> planetListDetails;

		// Constant factor to scale the radius to determine gravitational range
		private const double GravitationalRangeScaleFactor = 10; // Adjust as needed

		Vector3 SunRotationAxis;
		GridHelper gHandler = new GridHelper ();

		public float GlobalDimmer = 1f;
		public float ControlDimmer = 1f;
		public float SpeedDimmer = 1f;

		private Stopwatch stopwatch;
		private Stopwatch deltaTimer;
		private double deltaTime = 0;

		public bool isSeated = false;

		private double glitchAmount = 0;
		private double glitchAmount_overload = 0;
		private double glitchAmount_min = 0;
		private List<float> randomFloats = new List<float>();
		private int nextRandomFloat = 0;

		private double powerLoad = 0;

		private List<RadarAnimation> RadarAnimations = new List<RadarAnimation>();

		public bool EnableMASTER = true;

		public bool EnableGauges = true;
		public bool EnableMoney = true;
		public bool EnableHolograms = true;
		public bool EnableHolograms_them = true;
		public bool EnableHolograms_you = true;
		public bool EnableDust = true;
		public bool EnableToolbars = true;
		public bool EnableSpeedLines = true;

		private IMyPlayer player;
		private bool client;
		private VRage.Game.ModAPI.IMyCubeGrid playerGrid;
		private float min_blip_scale = 0.05f;

        private MyIni ini = new MyIni();

		//================= WEAPON CORE ===============================================================================================
		private bool isWC = false;

		public bool IsWeaponCoreLoaded(){
			bool isWeaponCorePresent = MyAPIGateway.Session.Mods.Any(mod => mod.PublishedFileId == 3154371364); // Replace with actual WeaponCore ID
			isWC = isWeaponCorePresent;
			return isWeaponCorePresent;
		}
		//=============================================================================================================================











		//This entire section is a complete nightmare. Holy moly, I'm out of my depth.
		//I really hope this works.

		//=====================SYNC SETTINGS WITH CLIENTS===============================================================================
		public class ModSettings
		{
			public float lineThickness = 1.5f;        					// Thickness of the lines
			public int lineDetail = 90;									// Number of segments per circle base
			public Vector3D starPos = new Vector3D(0, 0, 0);			// Star Position
			public  bool starFollowSky = true;							// Does the star position follow the skybox?
			public double radarRange = -1;                              // Radar Range (-1 for draw distance)
			public double maxRadarRangeGlobal = -1; // Max global radar range, -1 will use draw distance, otherwise can override if users want to limit radar range below draw limit.
            public bool enableGridFlares = true;  // Show a flare graphic on ships to make them more visible in the void
			public bool enableVisor = true; // Show visor effects.
            public int maxPings = 500; // Max pings on radar.
			public int rangeBracketDistance = 500; // Distance in meters for range bracketing. Used in calculations like when targetting to select the "bracket" the target fits in to zoom the radar.
			public bool enableHologramsGlobal = true; // Whether to show ship holograms for player and target globally. 
            public bool useHollowReticle = true; // Use a hollow reticle. Sometimes center dot blocks view of ship targetted, prefer hollow reticle. 
            public double fadeThreshhold = 0.01; // In percent, distance at edge of radar range where resolution gets "fuzzy" and blips dim/fade away. Allows for smooth transition rather than sudden disappearance of radar. 
        }

		private const ushort MessageId = 10203;
		private const ushort RequestMessageId = 30201;
		private const string settingsFile = "EDHH_settings.xml";
		private ModSettings theSettings = null;

		public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
		{
			client = !MyAPIGateway.Utilities.IsDedicated;

			if (client)
			{
				player = MyAPIGateway.Session.LocalHumanPlayer;
			}
			base.Init(sessionComponent);

			if (MyAPIGateway.Session.IsServer)
			{
				theSettings = LoadSettings();
				ApplySettings(theSettings);
				MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(RequestMessageId, OnSettingsRequestReceived);
			}

			MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(MessageId, OnSyncSettingsReceived);
			MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(RequestMessageId, OnSettingsRequestReceived);

			if (!MyAPIGateway.Session.IsServer)
			{
				RequestSettingsFromServer();
			}
		}

		public void Unload()
		{
			if (MyAPIGateway.Session.IsServer)
			{
				SaveSettings(theSettings);
				MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(RequestMessageId, OnSettingsRequestReceived);
			}

			MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(MessageId, OnSyncSettingsReceived);
		}

		private void RequestSettingsFromServer()
		{
			MyAPIGateway.Multiplayer.SendMessageToServer(RequestMessageId, new byte[0]);
		}

		private void OnSettingsRequestReceived(ushort messageId, byte[] data, ulong sender, bool fromServer)
		{
			if (MyAPIGateway.Session.IsServer)
			{
                SyncSettingsWithClient(sender, theSettings); // Use sender param.
    //            var senderId = MyAPIGateway.Multiplayer.MyId; // Get sender ID
				//SyncSettingsWithClient(senderId, theSettings);
			}
		}

		public void SyncSettingsWithClient(ulong clientId, ModSettings settings)
		{
			string data = SerializeSettings(settings);
			var msg = MyAPIGateway.Utilities.SerializeToBinary(data);
			MyAPIGateway.Multiplayer.SendMessageTo(MessageId, msg, clientId);
		}

		private void OnSyncSettingsReceived(ushort messageId, byte[] data, ulong sender, bool fromServer)
		{
			string xmlData = MyAPIGateway.Utilities.SerializeFromBinary<string>(data);
			ModSettings settings = DeserializeSettings(xmlData);
			ApplySettings(settings);
		}

		public string SerializeSettings(ModSettings settings)
		{
			return MyAPIGateway.Utilities.SerializeToXML(settings);
		}

		public ModSettings DeserializeSettings(string data)
		{
			return MyAPIGateway.Utilities.SerializeFromXML<ModSettings>(data);
		}
			
		private void ApplySettings(ModSettings settings)
		{
			CircleRenderer.LINE_THICKNESS	= 		settings.lineThickness;
			CircleRenderer.LINE_DETAIL		= 		settings.lineDetail;
			CircleRenderer.STAR_POS			= 		settings.starPos;
			CircleRenderer.STAR_FOLLOW_SKY	= 		settings.starFollowSky;
			CircleRenderer.RIPPYRANGE 		= 		settings.radarRange;
			CircleRenderer.MAX_RADAR_RANGE_GLOBAL = settings.maxRadarRangeGlobal;
			CircleRenderer.MAX_PINGS        =       settings.maxPings;
			CircleRenderer.RANGE_BRACKET_DISTANCE = settings.rangeBracketDistance;
			CircleRenderer.ENABLE_HOLOGRAMS_GLOBAL = settings.enableHologramsGlobal;
			CircleRenderer.USE_HOLLOW_RETICLE = settings.useHollowReticle;
			CircleRenderer.ENABLE_GRID_FLARES = settings.enableGridFlares;
			CircleRenderer.ENABLE_VISOR = settings.enableVisor;
			CircleRenderer.FADE_THRESHHOLD = settings.fadeThreshhold;
		}

		private ModSettings GatherCurrentSettings()
		{
			// Gather current settings from your mod's state
			ModSettings AllTheSettings = new ModSettings();

			AllTheSettings.lineThickness 	= 	CircleRenderer.LINE_THICKNESS;
			AllTheSettings.lineDetail 		= 	CircleRenderer.LINE_DETAIL;
			AllTheSettings.starPos 			= 	CircleRenderer.STAR_POS;
			AllTheSettings.starFollowSky 	= 	CircleRenderer.STAR_FOLLOW_SKY;
			AllTheSettings.radarRange 		= 	CircleRenderer.RIPPYRANGE;
			AllTheSettings.maxRadarRangeGlobal = CircleRenderer.MAX_RADAR_RANGE_GLOBAL;
			AllTheSettings.maxPings = CircleRenderer.MAX_PINGS;
			AllTheSettings.rangeBracketDistance = CircleRenderer.RANGE_BRACKET_DISTANCE;
			AllTheSettings.enableHologramsGlobal = CircleRenderer.ENABLE_HOLOGRAMS_GLOBAL;
			AllTheSettings.useHollowReticle = CircleRenderer.USE_HOLLOW_RETICLE;
			AllTheSettings.enableGridFlares = CircleRenderer.ENABLE_GRID_FLARES;
			AllTheSettings.enableVisor = CircleRenderer.ENABLE_VISOR;
			AllTheSettings.fadeThreshhold = CircleRenderer.FADE_THRESHHOLD;

			return AllTheSettings;
		}

		public void SaveSettings(ModSettings settings)
		{
			string data = SerializeSettings(settings);
			using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(settingsFile, typeof(ModSettings)))
			{
				writer.Write(data);
			}
		}

		public ModSettings LoadSettings()
		{
			if (MyAPIGateway.Utilities.FileExistsInWorldStorage(settingsFile, typeof(ModSettings)))
			{
				using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(settingsFile, typeof(ModSettings)))
				{
					string data = reader.ReadToEnd();
					MyLog.Default.WriteLine($"FENIX_HUD: {data}");
					return DeserializeSettings(data);
				}
			}
			return new ModSettings(); // Return default settings if no file exists
		}
		//=============================================================================================================================
















		private void DrawGauges(){
			drawPower ();
			drawSpeed ();
		}

		private void drawText(string text, double size, Vector3D pos, Vector3D dir, Vector4 color, float dim = 1, bool flipUp = false){
			text = text.ToLower ();
			Vector3D up = radarMatrix.Up;
			if (flipUp) {
				up = radarMatrix.Forward;
			}
			Vector3D left = Vector3D.Cross(up, dir);
			List<string> parsedText = StringToList(text);
			for (int i = 0; i < parsedText.Count; i++) {
				Vector3D offset = -left * (size*i*1.8);
				if (parsedText [i] != " ") {
					DrawQuadRigid (pos + offset, dir, size, getFontMaterial(parsedText [i]), color * GLOW * dim, flipUp);
				}
			}
		}

		private void drawPower(){
			Vector4 color = LINE_COLOR;

			if (powerLoad > 0.667) {
				float powerLoad_offset = (float)powerLoad - 0.667f;
				powerLoad_offset /= 0.333f;

				color.X = LerpF (color.X, 1, powerLoad_offset);
				color.Y = LerpF (color.Y, 0, powerLoad_offset);
				color.Z = LerpF (color.Z, 0, powerLoad_offset);
			}

			double PL = Math.Round(powerLoad*100);
			double size = 0.0070;
			string PLS = PL.ToString ();
			if (PL < 100) {
				PLS = " " + PLS;
			}
			if (PL < 10) {
				PLS = " " + PLS;
			}
			PLS = "~" + PLS + "%";
			Vector3D pos = worldRadarPos+(radarMatrix.Forward*radarRadius*0.4)+(radarMatrix.Left*radarRadius*1.3)+(radarMatrix.Up*0.0085);
			Vector3D dir = Vector3D.Normalize (pos - worldRadarPos);
			dir = Vector3D.Normalize((dir+radarMatrix.Forward)/2);
			drawText (PLS, size, pos, dir, color);
			drawText (" 000 ", size, pos, dir, color, 0.333f);

			double sizePow = 0.0045;
			double powerSeconds = (double)gHandler.powerHours * 3600;
			string powerSecondsS = "~" + FormatSecondsToReadableTime (powerSeconds);
			Vector3D powerPos = worldRadarPos + radarMatrix.Left * radarRadius * 0.88 + radarMatrix.Backward * radarRadius * 1.1 + radarMatrix.Down * sizePow * 2;
			drawText (powerSecondsS, sizePow, powerPos, radarMatrix.Forward, LINECOLOR_Comp, 1);

			float powerPer = 60 * (gHandler.powerStored / gHandler.powerStoredMax);

			//----ARC----
			float arcLength = 56f*(float)powerLoad;
			float arcLengthTime = 70f*(float)powerLoad;
			DrawArc(worldRadarPos-radarMatrix.Up*0.0025, radarRadius*1.27, radarMatrix.Up, 35, 35+arcLengthTime, color, 0.007f, 0.5f);
			DrawArc(worldRadarPos-radarMatrix.Up*0.0025, radarRadius*1.27 + 0.01, radarMatrix.Up, 42, 42+powerPer, LINECOLOR_Comp, 0.002f, 0.75f);
		}

		double maxSpeed;
		private void drawSpeed(){

			maxSpeed = Math.Max (maxSpeed, gHandler.localGridSpeed);

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

			Vector3D pos = worldRadarPos+(radarMatrix.Forward*radarRadius*0.4)+(radarMatrix.Right*radarRadius*1.3)+(radarMatrix.Up*0.0085);
			Vector3D dir = Vector3D.Normalize (pos - worldRadarPos);
			dir = Vector3D.Normalize((dir+radarMatrix.Forward)/2);

			Vector3D left = Vector3D.Cross(radarMatrix.Up, dir);
			pos = (left * size * 7) + pos;

			drawText (PLS, size, pos, dir, LINE_COLOR);
			drawText ("0000 ", size, pos, dir, LINE_COLOR, 0.333f);

			//----ARC----
			float arcLength = (float)(gHandler.localGridSpeed/maxSpeed);
			arcLength = Clamped (arcLength, 0, 1);
			arcLength *= 70;
			DrawArc(worldRadarPos-radarMatrix.Up*0.0025, radarRadius*1.27, radarMatrix.Up, 360-30-arcLength, 360-30, LINE_COLOR, 0.007f, 0.5f);

			double sizePow = 0.0045;
			double powerSeconds = (double)gHandler.H2powerSeconds;
			string powerSecondsS = "@" + FormatSecondsToReadableTime (powerSeconds);
			Vector3D powerPos = worldRadarPos + radarMatrix.Right * radarRadius * 0.88 + radarMatrix.Backward * radarRadius * 1.1 + radarMatrix.Down * sizePow * 2 + radarMatrix.Left *powerSecondsS.Length * sizePow * 1.8;
			drawText (powerSecondsS, sizePow, powerPos, radarMatrix.Forward, LINECOLOR_Comp, 1);

			float powerPer = 56f*gHandler.H2Ratio;
			DrawArc(worldRadarPos-radarMatrix.Up*0.0025, radarRadius*1.27 + 0.01, radarMatrix.Up, 360-37-powerPer, 360-37, LINECOLOR_Comp, 0.002f, 0.75f);
		}

		private void populateFonts(){
			int num = 51;

			for (int y = 0; y < num; y++) {
				string name = "ED_FONT_" + Convert.ToString (y);
				MaterialFont.Add(name);
			}
		}

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
		private void updateDust()
		{
			int dustAmount = 32;

			if (dustList.Count < dustAmount) {
				for (int i = 0; i < dustAmount - dustList.Count; i++) {
					//Generate Dust
					dustList.Add (formatDust());
				}
			}
				
			if (dustList.Count > 0) {
				//update Dust
				for(var i = 0; i < dustList.Count; i++) {
					dustList[i].life += deltaTime;

					float alpha = (float)(dustList [i].life / dustList [i].lifeTime);
					alpha = (alpha - 0.5f) * 2;
					alpha = Math.Abs (alpha);
					alpha = 1 - alpha;
					alpha = (float)(Math.Pow ((double)alpha, 0.5)) * 0.5f;

					alpha = Clamped (alpha, 0.001f, 1);

					Vector3D pos =  dustList[i].pos + (dustList[i].life * dustList[i].velocity)*0.125;
					pos = Vector3D.Transform (pos, radarMatrix);
					Vector4 color = new Vector4 (1,1,1,1) * alpha * 0.5f;

					Vector3D dir = MyAPIGateway.Session.Camera.WorldMatrix.Up;
					Vector3D lef = MyAPIGateway.Session.Camera.WorldMatrix.Left;

					double scale = dustList [i].scale;// * (double)alpha;
					//scale *= scale;

					//DrawQuad (pos, MyAPIGateway.Session.Camera.WorldMatrix.Backward, dustList[i].scale, dustList[i].Material, color);
					MyTransparentGeometry.AddBillboardOriented (dustList [i].Material, color, pos, lef, dir, (float)scale, BlendTypeEnum.AdditiveTop);

					if (dustList[i].life >= dustList[i].lifeTime *0.985) {
						dustList[i] = formatDust ();
					}
				}
			}
		}

		private DustParticle formatDust()
		{
			DustParticle dust = new DustParticle();
			dust.life = 0;
			dust.Material = MaterialDust1;
			double whichMat = Math.Round (GetRandomDouble () * 3);
			if (whichMat > 1) {
				dust.Material = MaterialDust2;
			} else if (whichMat > 2) {
				dust.Material = MaterialDust3;
			}
			dust.lifeTime = GetRandomDouble () * 8 + 2;
			dust.velocity = new Vector3D ((GetRandomDouble()-0.5)*2, (GetRandomDouble()-0.5)*2, (GetRandomDouble()-0.5)*2);
			dust.velocity = Vector3D.Normalize (dust.velocity) * (GetRandomDouble () * 0.1);
			dust.scale = GetRandomDouble () * 0.1 + 0.025;
			dust.pos = new Vector3D ((GetRandomDouble()-0.5)*2, (GetRandomDouble()-0.5)*2, (GetRandomDouble()-0.5)*2);
			dust.pos *= 0.25;

			return dust;
		}

		private void deleteDust(){
			dustList.Clear();
		}

		//RANDOM=============================================================================
		private Random _random = new Random();
		private void populateRandoms(){
			randomFloats.Clear ();
			int totalRands = 33;
			for(int i = 0 ; i < totalRands ; i++){
				randomFloats.Add (MyRandom.Instance.NextFloat ());
			}
		}

		public float GetRandomFloat()
		{
			float value = randomFloats [nextRandomFloat];
			nextRandomFloat += 1;
			if (nextRandomFloat >= randomFloats.Count) {
				nextRandomFloat = 0;
			}
			return value;
		}


		public double GetRandomDouble()
		{
			float value = randomFloats [nextRandomFloat];
			nextRandomFloat += 1;
			if (nextRandomFloat >= randomFloats.Count) {
				nextRandomFloat = 0;
			}
			return (double)value;
		}

		public bool GetRandomBoolean()
		{
			float value = randomFloats [nextRandomFloat];
			nextRandomFloat += 1;
			if (nextRandomFloat >= randomFloats.Count) {
				nextRandomFloat = 0;
			}
			return Convert.ToBoolean((int)Math.Round(value));
		}
		//-----------------------------------------------------------------------------------


		//LOAD DATA==========================================================================
		public override void LoadData()
		{	
			base.LoadData();
			//configData = ReadConfigFile (); //Distabling for now as I try a new method.

			SubscribeToEvents ();

			randomFloats.Add (0);

			deltaTimer = new Stopwatch ();
			deltaTimer.Start ();

			stopwatch = new Stopwatch ();
			stopwatch.Start ();

			timeSinceSound.Start ();

			populateFonts ();

			LINECOLOR_Comp_RPG = secondaryColor (LINE_COLOR_RGB)*2 + new Vector3(0.01f,0.01f,0.01f);
			LINECOLOR_Comp = new Vector4 (LINECOLOR_Comp_RPG, 1f);

            if (MAX_RADAR_RANGE_GLOBAL == -1)
            {
                maxRadarRange = (double)MyAPIGateway.Session.SessionSettings.ViewDistance;
            }
            else
            {
                maxRadarRange = MAX_RADAR_RANGE_GLOBAL;
            }

            if (STAR_FOLLOW_SKY) {
				//Thank you Rotate With Skybox Mod
				if (MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Session.IsServer)
					return;

				if (!MyAPIGateway.Session.SessionSettings.EnableSunRotation)
					return;

				MyObjectBuilder_Sector saveOB = MyAPIGateway.Session.GetSector ();

				Vector3 baseSunDir;
				Vector3.CreateFromAzimuthAndElevation (saveOB.Environment.SunAzimuth, saveOB.Environment.SunElevation, out baseSunDir);
				baseSunDir.Normalize ();

				SunRotationAxis = (!(Math.Abs (Vector3.Dot (baseSunDir, Vector3.Up)) > 0.95f)) ? Vector3.Cross (Vector3.Cross (baseSunDir, Vector3.Up), baseSunDir) : Vector3.Cross (Vector3.Cross (baseSunDir, Vector3.Left), baseSunDir);
				SunRotationAxis.Normalize ();
			}
		}
		//-----------------------------------------------------------------------------------



		//DATA MANAGEMENT====================================================================
		//Thank you AegisSystems for the example to work from!
		private ConfigData ReadConfigFile()
		{

			ConfigData configgy = null;
			MyLog.Default.WriteLineAndConsole("Dang Config Loading...");
			if (MyAPIGateway.Utilities.FileExistsInWorldStorage(configDataFile, typeof(string)))
			{
				var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(configDataFile, typeof(string));
				if (reader != null)
				{
					string data = reader.ReadToEnd();
					configgy = MyAPIGateway.Utilities.SerializeFromXML<ConfigData>(data);
					if (configgy != null)
					{
						MyLog.Default.WriteLineAndConsole("Dang Config Loaded");
						CircleRenderer.LINE_THICKNESS = configgy.lineThickness;
						CircleRenderer.LINE_DETAIL = configgy.lineDetail;
						CircleRenderer.STAR_POS = configgy.starPos;
						CircleRenderer.STAR_FOLLOW_SKY = configgy.starFollowSky;
						CircleRenderer.ENABLE_COCKPIT_DUST = configgy.enableCockpitDust;
						CircleRenderer.ENABLE_GRID_FLARES = configgy.enableGridFlares;
						CircleRenderer.ENABLE_VISOR = configgy.enableVisor;
						CircleRenderer.RIPPYRANGE = configgy.radarRange;
						CircleRenderer.MAX_RADAR_RANGE_GLOBAL = configgy.maxRadarRangeGlobal;
						CircleRenderer.MAX_PINGS = configgy.maxPings;
						CircleRenderer.RANGE_BRACKET_DISTANCE = configgy.rangeBracketDistance;
						CircleRenderer.ENABLE_HOLOGRAMS_GLOBAL = configgy.integrityDisplay;
						CircleRenderer.USE_HOLLOW_RETICLE = configgy.useHollowReticle;
						CircleRenderer.FADE_THRESHHOLD = configgy.fadeThreshhold;

					}
					else
					{
						MyLog.Default.WriteLineAndConsole("Dang Config File missing. Creating new File.");
						configgy = new ConfigData();
						string configdata = MyAPIGateway.Utilities.SerializeToXML(configgy);
						TextWriter wiggy = MyAPIGateway.Utilities.WriteFileInWorldStorage(configDataFile, typeof(string));
						wiggy.Write(configdata);
						wiggy.Close();
						MyLog.Default.WriteLineAndConsole("Dang Config Created!");

					}
				}
			}
			return configgy;
		}

		public override void SaveData()
		{
			base.SaveData();

			if (theSettings != null)
			{
				SaveSettings(theSettings);
			}

			if (configData == null)
			{
				configData = new ConfigData();
			}
			if (configData != null)
			{
				string configdata = MyAPIGateway.Utilities.SerializeToXML(configData);
				TextWriter wiggy = MyAPIGateway.Utilities.WriteFileInWorldStorage(configDataFile, typeof(string));
				wiggy.Write(configdata);
				wiggy.Close();
				MyLog.Default.WriteLineAndConsole("Dang Config Saved!");
			}
		}

		protected override void UnloadData()
		{
			UnsubscribeFromEvents ();
		}
		//-----------------------------------------------------------------------------------


		private bool findEntitiesOnce = false;
		private int msgWaiter = 0;
		//UPDATE=============================================================================
		//------------- B E F O R E -----------------
		public override void UpdateBeforeSimulation()
		{
			if(!EnableMASTER){
				return;
			}

			if (!findEntitiesOnce) 
			{
                // Initialize the planet manager
                planetList = GetPlanets ();
				planetListDetails = GatherPlanetInfo(planetList);

				// Initialize Entity List for Radar
				FindEntities ();
				findEntitiesOnce = true;
			}

			if (gHandler != null) 
			{
				gHandler.UpdateGrid ();
				populateRandoms ();

				gHandler.UpdateDamageCheck ();
				double damageAmount = gHandler.getDamageAmount ();
				glitchAmount_overload += damageAmount;

			}

			// Delta timer? Not sure how this is used in the code exactly. Used for time2Ready but not sure exactly what it does...
			if (deltaTimer != null) 
			{
				if (!deltaTimer.IsRunning) 
				{
					deltaTimer.Start ();
				}
				deltaTime = deltaTimer.Elapsed.TotalSeconds;
				deltaTimer.Restart ();
			}

			// Halo timer for active radar pulse effect. 
			if (haloTimer != null) 
			{
				if (!haloTimer.IsRunning) 
				{
					haloTimer.Start ();
				}
            }
        }

		private bool InitializedAudio = false;

        //------------ A F T E R ------------------
        public override void UpdateAfterSimulation()
		{
			if(!EnableMASTER)
			{
				return;
			}

			if (!InitializedAudio) 
			{
				InitializeAudio ();
				IsWeaponCoreLoaded ();
				InitializedAudio = true;
			}

			//PlayQueuedSounds ();


			bool IsPlayerControlling = gHandler.IsPlayerControlling;
			var cockpit = gHandler.localGridEntity as Sandbox.ModAPI.IMyCockpit;

			if (cockpit == null) {
				return;
			}
            if (!cockpit.CustomName.Contains("[ELI_HUD]"))
            {
                Echo("Include [ELI_HUD] tag in cockpit name to enable Scanner.");
                return;
            }


			if (IsPlayerControlling) {
				if (!isSeated) {
					//Seated Event!
					OnSitDown();
				}
			} else {
				if (isSeated) {
					//Standing Event!
					OnStandUp();
				}
			}
		}

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

        //------------ D R A W -----------------
        public override void Draw()
		{
			base.Draw();

			if(!EnableMASTER)
			{
				if (MyAPIGateway.Gui.IsCursorVisible) 
				{
					CheckCustomData(); //We don't need to keep check the colors unless we have the thing open.
				}
				return;
			}

			//Flares ------------------------------------------------------------------------------------------------------
			DrawGridFlare();
			//-------------------------------------------------------------------------------------------------------------

			IMyPlayer player = MyAPIGateway.Session?.LocalHumanPlayer;
			if (player == null)
			{
				return;
			}
			var controlledEntity = player.Controller?.ControlledEntity;
			if (controlledEntity == null)
			{
				return;
			}
			// Check if the controlled entity is a turret or remote control
			var turret = controlledEntity.Entity as Sandbox.ModAPI.IMyLargeTurretBase;
            if (turret != null)
            {
                return;
            }
			var remoteControl = controlledEntity as Sandbox.ModAPI.IMyRemoteControl;
            if (remoteControl != null && remoteControl.Pilot != null)
            {
                return;
            }

            //visor -------------------------------------------------------------------------------------------------------
            UpdateVisor();
			//-------------------------------------------------------------------------------------------------------------

			//Is the player in a vehichle? Fade or show the orbit lines!
			bool IsPlayerControlling = gHandler != null && gHandler.IsPlayerControlling;

            if (IsPlayerControlling) {
				if (gHandler == null || gHandler.localGridEntity == null)
				{ 
					return; 
				}
                Sandbox.ModAPI.IMyCockpit cockpit = gHandler.localGridEntity as Sandbox.ModAPI.IMyCockpit;
				if (cockpit == null || cockpit.CubeGrid == null || !cockpit.CustomName.Contains("[ELI_HUD]")) 
				{ 
					return; 
				}
                if (!cockpit.CustomName.Contains("[ELI_HUD]"))
                {
                    return;
                }
                playerGrid = cockpit.CubeGrid;
                if (!HasPowerProduction(playerGrid))
                {
                    return;
                }
                powerLoad = gHandler.GetGridPowerUsagePercentage(playerGrid);

				glitchAmount_min = MathHelper.Clamp(gHandler.GetGridPowerUsagePercentage(playerGrid), 0.85, 1.0)-0.85;
				glitchAmount_overload = MathHelper.Lerp (glitchAmount_overload, 0, deltaTime * 2);
				glitchAmount = MathHelper.Clamp (glitchAmount_overload, glitchAmount_min, 1);
				if (glitchAmount_overload < 0.01) 
				{
					glitchAmount_overload = 0;
				}
			} 
			else 
			{
				return;
			}

			float speed = (float)gHandler.localGridSpeed;

			if (IsPlayerControlling) {
				ControlDimmer += 0.05f;
				//MyVisualScriptLogicProvider.ShowNotification(speed.ToString(), 2, "White");
				SpeedDimmer = Clamped(speed*0.01f+0.05f, 0f, 1f);

				SpeedDimmer = (float)(Math.Pow (SpeedDimmer, 2));

				if (ShowVelocityLines && speed > 10) {
					DrawSpeedGaugeLines (gHandler.localGridEntity, gHandler.localGridVelocity);
					UpdateAndDrawVerticalSegments (gHandler.localGridEntity, gHandler.localGridVelocity);
				}
			} else {
				ControlDimmer -= 0.05f;
			}

			bool isGuiVisible = MyAPIGateway.Gui.IsCursorVisible; 
			if (isGuiVisible) {
				CheckCustomData (); //We don't need to keep check the colors unless we have the thing open.
			}
				
			SpeedDimmer = MathHelper.Clamp(Remap(speed, (SpeedThreshold*0.75f), SpeedThreshold, 0f, 1f), 0f, 1f);
			ControlDimmer = Clamped (ControlDimmer, 0f, 1f);
			GlobalDimmer = ControlDimmer * SpeedDimmer;

			if (GlobalDimmer > 0.01f) {
				//Get Sun direction
				if (STAR_FOLLOW_SKY) {
					MyOrientation sunOrientation = getSunOrientation ();

					Quaternion sunRotation = sunOrientation.ToQuaternion ();
					Vector3D sunForwardDirection = Vector3D.Transform (Vector3D.Forward, sunRotation);

					STAR_POS = sunForwardDirection * 100000000;
				}
				//Draw orbit lines
				foreach (var i in planetListDetails) {
					// Cast the entity to IMyPlanet
					MyPlanet planet = (MyPlanet)i.Entity;
					Vector3D parentPos = (i.ParentEntity != null) ? i.ParentEntity.GetPosition () : STAR_POS;

					DrawPlanetOutline (planet);
					DrawPlanetOrbit (planet, parentPos);
				}

				//DEBUG
				//DrawWorldUpAxis (STARPOS);

			}

			CheckPlayerInput();

            DrawRadar();

            double dis2cameraSqr = Vector3D.DistanceSquared(MyAPIGateway.Session.Camera.Position, worldRadarPos);
            if (dis2cameraSqr > 4) // 2^2 = 4
            {
                return;
            }

			//if dust ----------------------------------------------------------------------------------
			if (EnableDust) {
				updateDust ();
			}
			//------------------------------------------------------------------------------------------

			if (EnableGauges) {
				DrawGauges ();
			}

			if (GlobalDimmer > 0.99f) {
				TB_activationTime = 0;
				HG_activationTime = 0;
				HG_activationTimeTarget = 0;
				return;
			}

			if (EnableHolograms) {
				HG_Update ();
			}
				
			if (EnableMoney) {
				UpdateCredits ();
			}
				
			if (EnableToolbars) {
				UpdateToolbars ();
			}

		}
		//-----------------------------------------------------------------------------------



		//ACTIVATION=========================================================================
		private double visorLife = 0;
		private bool visorDown = false;

		private void UpdateVisor()
		{
			if (ENABLE_VISOR) 
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
                        visorLife -= deltaTime * 1.5;
                    }
                    else
                    {
                        visorLife += deltaTime * 1.5;
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

		private void OnSitDown(){
			isSeated = true;
			//MyAPIGateway.Utilities.ShowMessage("NOTICE", "Entering Cockpit.");

			//Trigger animations and sounds for console.
			radarScaleRange_CurrentLogin = 0.001;
				
			//Check for CustomData variables in cockpit and register.
			CheckCustomData();
			glitchAmount_overload = 0.25;

			PlayCustomSound (SP_BOOTUP, worldRadarPos);
			//PlayCustomSound (SP_ZOOMINIT, worldRadarPos);

			HG_InitializeLocalGrid ();

		}

		private void OnStandUp(){
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
			ReleaseTarget();
			deleteDust ();

		}

		private bool ShowVelocityLines = true;
		private bool ShowVoxels = true;
		private float SpeedThreshold = 10f;

		private void CheckCustomData()
		{
			Sandbox.ModAPI.Ingame.IMyTerminalBlock block = (Sandbox.ModAPI.Ingame.IMyTerminalBlock)gHandler.localGridEntity;

			// Read your specific data
			string mySection = "EliDang"; // Your specific section

			// Ensure CustomData is parsed
			string customData = block.CustomData;
			MyIni ini = new MyIni();
			MyIniParseResult result;

			// Parse the entire CustomData
			if (ini.TryParse(customData, out result))
			{
				ReadCustomData (ini);
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
					ReadCustomData (ini);
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

		private void ReadCustomData(MyIni ini){

			string mySection = "EliDang";

			// Master
			EnableMASTER = ini.Get(mySection, "ScannerEnable").ToBoolean(true);

			// Holograms
			EnableHolograms = !ENABLE_HOLOGRAMS_GLOBAL ? false : ini.Get(mySection, "ScannerHolo").ToBoolean(true); // If disabled at global level don't even check just default to false.
			EnableHolograms_them = !ENABLE_HOLOGRAMS_GLOBAL ? false : ini.Get(mySection, "ScannerHoloThem").ToBoolean(true); // If disabled at global level don't even check just default to false.
            EnableHolograms_you = !ENABLE_HOLOGRAMS_GLOBAL ? false : ini.Get(mySection, "ScannerHoloYou").ToBoolean(true); // If disabled at global level don't even check just default to false.

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
			string colorString = ini.Get(mySection, "ScannerColor").ToString(Convert.ToString(LINE_COLOR_RGB));
			Color radarColor = ParseColor(colorString);
			LINE_COLOR = (radarColor).ToVector4() * GLOW;

			// Velocity Toggle
			ShowVelocityLines = ini.Get(mySection, "ScannerLines").ToBoolean(true);

			// Orbit Line Speed Threshold
			SpeedThreshold = ini.Get(mySection, "ScannerOrbits").ToSingle(500);

			LINE_COLOR_RGB = new Vector3(LINE_COLOR.X, LINE_COLOR.Y, LINE_COLOR.Z);
			LINECOLOR_Comp_RPG = secondaryColor(LINE_COLOR_RGB) * 2 + new Vector3(0.01f, 0.01f, 0.01f);
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
				return LINE_COLOR_RGB*GLOW;  // Default color if no data

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
			return LINE_COLOR_RGB*GLOW;  // Default color on parse failure
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

			bool found = false;
			if (entity is VRage.ModAPI.IMyEntity) {
				foreach (var i in radarPings) {
					if (i.Entity == entity) {
						found = true;
						break;
					}
				}
				if (!found) {
					if(entity.DisplayName != "Stone" && entity.DisplayName != null){
						RadarPing newPing = newRadarPing (entity);
						radarPings.Add (newPing);
					}
				}
			}
			if (!radarEntities.Contains (entity)) {
				if (entity is VRage.Game.ModAPI.IMyCubeGrid) {
					radarEntities.Add (entity);
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

			if (entity is VRage.ModAPI.IMyEntity) {
				foreach (var i in radarPings) {
					if (i.Entity == entity) {
						i.Time.Stop();
						radarPings.Remove (i);
						break;
					}
				}
			}

			if (radarEntities.Contains(entity)) {
				radarEntities.Remove (entity);
			}
		}
		//-----------------------------------------------------------------------------------



		//DRAW LINES=========================================================================
		/// <summary>
		/// If grid flares are enabled draws a flare on screen so they show up better in the void. 
		/// </summary>
		void DrawGridFlare(){
			if (ENABLE_GRID_FLARES) 
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


		void DrawLineBillboard(MyStringId material, Vector4 color, Vector3D origin, Vector3 directionNormalized, float length, float thickness, BlendTypeEnum blendType = 0, int customViewProjection = -1, float intensity = 1, List<VRageRender.MyBillboard> persistentBillboards = null){
			
			if (GetRandomBoolean()) {
				if (glitchAmount > 0.001) {
					float glitchValue = (float)glitchAmount;

					Vector3D offsetRan = new Vector3D (
						                    (GetRandomDouble () - 0.5) * 2,
						                    (GetRandomDouble () - 0.5) * 2,
						                    (GetRandomDouble () - 0.5) * 2
					                    );
					double dis2Cam = Vector3D.Distance (MyAPIGateway.Session.Camera.Position, origin);

					origin += offsetRan * dis2Cam * glitchValue * 0.025;
					color *= GetRandomFloat ();
				}
			}

			MyTransparentGeometry.AddLineBillboard(material, color, origin, directionNormalized, length, thickness, blendType, customViewProjection, intensity, persistentBillboards);
		}

		void DrawWorldUpAxis(Vector3D position)
		{
			BlendTypeEnum blendType = BlendTypeEnum.Standard;
			float lineThickness = 0.001f;
			Vector3D cameraPosition = MyAPIGateway.Session.Camera.Position;
			lineThickness = Convert.ToSingle (Vector3D.Distance (cameraPosition, position)) * lineThickness;

			float lineLength = 0.25f;
			lineLength = Convert.ToSingle (Vector3D.Distance (cameraPosition, position)) * lineLength;

			// Define the colors for each axis
			Color xColor = Color.Red;
			Color yColor = Color.Green;
			Color zColor = Color.Blue;

			// Draw the X-axis (red) RIGHT
			DrawLineBillboard (Material, xColor, position, Vector3D.Right, lineLength, lineThickness, blendType);

			// Draw the Y-axis (green) UP
			DrawLineBillboard (Material, yColor, position, Vector3D.Up, lineLength, lineThickness, blendType);

			// Draw the Z-axis (blue) FORWARD
			DrawLineBillboard (Material, zColor, position, Vector3D.Forward, lineLength, lineThickness, blendType);
		}

		// Method to draw a circle in 3D space
		void DrawCircle(Vector3D center, double radius, Vector3 planeDirection, Vector4 colorOverride, bool dotted = false, float dimmerOverride = 0f, float thicknessOverride = 0f)
		{
			// Define orbit parameters
			Vector3D planetPosition = center;   // Position of the center of the circle
			double orbitRadius = radius;        // Radius of the circle
			int segments = LINE_DETAIL;                 // Number of segments to approximate the circle
			bool dotFlipper = true;

			//Lets adjust the tesselation based on radius instead of an arbitrary value.
			int segmentsInterval = (int)Math.Round(Remap((float)orbitRadius,1000, 10000000, 1, 8));
			segments = segments * (int)Clamped((float)segmentsInterval, 1, 16);

			float lineLength = 1.01f;           // Length of each line segment
			float lineThickness = LINE_THICKNESS;        // Thickness of the lines

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

				Vector3D normalizedPoint1 = Vector3D.Normalize (point1-center);
				double dotPoint1 = Vector3D.Dot (normalizedPoint1, MyAPIGateway.Session.Camera.WorldMatrix.Backward);
				dotPoint1 = RemapD (dotPoint1, -1, 1, 0.25, 1);
				dotPoint1 = ClampedD (dotPoint1, 0.1, 1);

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
						DrawLineBillboard (Material, lineColor * dimmer * (float)dotPoint1, point1, direction, lineLength, segmentThickness, blendType);
					}
				} else {
					dotFlipper = true;
				}
			}
		}

		// Outline a planet with a dotted line
		void DrawPlanetOutline(VRage.ModAPI.IMyEntity entity){
			// Cast the entity to a MyPlanet object
			MyPlanet planet = (MyPlanet)entity;

			// Determine the planet's effective radius (maximum radius or atmosphere radius, whichever is greater)
			double planetRadius = planet.MaximumRadius;
			double planetAtmo = planet.AtmosphereRadius;
			planetRadius = Math.Max(planetAtmo, planetRadius);

			// Get the position of the planet
			Vector3D planetPosition = entity.GetPosition ();

			// Determine the direction to aim the outline towards (camera position)
			Vector3D aimDirection = AimAtCam(planetPosition);

			// Draw a circle representing the outline of the planet
			DrawCircle(planetPosition, (float)planetRadius, aimDirection, LINE_COLOR, true);
		}

		// Fake an orbit for a planet
		void DrawPlanetOrbit(VRage.ModAPI.IMyEntity entity, Vector3D parentPosition){
			// Cast the entity to a MyPlanet object
			MyPlanet planet = (MyPlanet)entity;

			// Determine the planet's effective radius (maximum radius or atmosphere radius, whichever is greater)
			double planetRadius = planet.MaximumRadius;
			double planetAtmo = planet.AtmosphereRadius;
			planetRadius = Math.Max(planetAtmo, planetRadius);

			// Get the position of the planet
			Vector3D planetPosition = entity.GetPosition ();

			// Calculate the distance between the planet and its parent
			double orbitRadius = Vector3D.Distance(planetPosition, parentPosition); 

			// Calculate the direction from the planet to its parent and normalize it
			Vector3D orbitDirection = Vector3D.Normalize(parentPosition - planetPosition); 

			// Create a rotation matrix to align a reference direction with the orbit direction
			MatrixD rotationMatrix = MatrixD.CreateFromDir(orbitDirection);

			// Transform a reference direction (e.g., down) to align with the orbit direction
			orbitDirection = Vector3D.Transform(Vector3D.Down, rotationMatrix);

			// Draw a circle representing the planet's orbit around its parent
			DrawCircle(parentPosition, (float)orbitRadius, orbitDirection, LINE_COLOR);
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

			float lineThickness = LINE_THICKNESS;        // Thickness of the lines

			//Lets adjust the line thickness based on screen resolution so that it doesn't get to pixelated.
			lineThickness = ((float)GetScreenHeight()/1080f)*lineThickness;

			// Calculate camera distance from whole segment.
			float distanceToSegment = DistanceToLineSegment(cameraPosition, start, end);

			// Calculate the segment thickness based on distance from camera.
			float segmentThickness = Math.Max(Remap(distanceToSegment, 0f, 1000000f, 0f, 1000f) * lineThickness, 0f);

			// Calculate the segment brightness based on distance from camera.
			float dimmer = Clamped(Remap(distanceToSegment, 0, 10000000f, 1f, 0f), 0f, 1f)*7f;

			if (dimmer > 0.01f) {
				DrawLineBillboard (Material, LINE_COLOR * GLOW * dimmer, start, Vector3D.Normalize (end - start), segmentLength, segmentThickness, blendType);
			}
		}

		private void DrawSpeedGaugeLines(VRage.ModAPI.IMyEntity gridEntity, Vector3D velocity)
		{
			return;
			// Disabling this function for now.

			float lineLength = 100f;           	// Length of each line segment
			float lineThickness = 0.01f;        // Thickness of the lines

			var cockpit = gridEntity as Sandbox.ModAPI.IMyCockpit;
			if (cockpit == null || cockpit.CubeGrid == null || cockpit.CubeGrid.Physics == null) {
				return;
			}

			VRage.Game.ModAPI.IMyCubeGrid grid = cockpit.CubeGrid;
			if (grid == null) {
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
			if (cockpit == null || cockpit.CubeGrid == null || cockpit.CubeGrid.Physics == null) {
				return;
			}

			VRage.Game.ModAPI.IMyCubeGrid grid = cockpit.CubeGrid;
			if (grid == null) {
				return;
			}

			if (!scrollInit) {
				for (int i = 0; i < totalNumLines; i++) {
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
			for (int i = 0; i < totalNumLines; i++) {

				scrollOffsets[i] = (totalLineLength - (totalLineLength / (float)totalNumLines) * (float)i);
				scrollOffsets [i] += scrollOffset;
				Vector3D segmentPosition = direction * scrollOffsets[i];
				verticalSegments.Add(segmentPosition);
			}

			// Draw vertical segments
			lineThickness = LINE_THICKNESS;        // Thickness of the lines

			//Lets adjust the line thickness based on screen resolution so that it doesn't get to pixelated.
			lineThickness = ((float)GetScreenHeight()/1080f)*lineThickness;

			// Calculate camera distance from whole segment.
			Vector3D cameraPosition = MyAPIGateway.Session.Camera.Position;

			foreach (var segmentPosition in verticalSegments)
			{
				// Calculate the segment brightness based on distance from camera.
				float distanceToSegment = (float)segmentPosition.Length();
				float segmentThickness = Remap(distanceToSegment, 0f, totalLineLength, 0f, 1f);
				segmentThickness = Math.Abs ((segmentThickness - 0.5f) * 2f);
				float dimmer = Clamped(1-segmentThickness, 0f, 1f)*5f;
				dimmer *= SpeedDimmer*0.5f;

				lineThickness = LINE_THICKNESS;        // Thickness of the lines
				//Lets adjust the line thickness based on screen resolution so that it doesn't get to pixelated.
				lineThickness = ((float)GetScreenHeight()/1080f)*lineThickness;
				// Calculate camera distance from whole segment.
				distanceToSegment = (float)Vector3D.Distance(cameraPosition, leftBase + segmentPosition - direction * totalLineLength / 2);
				// Calculate the segment thickness based on distance from camera.
				segmentThickness = Math.Max(Remap(distanceToSegment, 0f, 1000000f, 0f, 1000f) * lineThickness, 0f);

				if (dimmer > 0.01f) {
					zeroBase = (leftBase + segmentPosition - direction * totalLineLength / 2);
					zeroBase = (segmentLength * 0.5 * -segmentUp) + zeroBase;
					DrawLineBillboard (Material, LINE_COLOR*GLOW * dimmer, zeroBase, segmentUp, segmentLength, segmentThickness);

					zeroBase = (rightBase + segmentPosition - direction * totalLineLength / 2);
					zeroBase = (segmentLength * 0.5 * -segmentUp) + zeroBase;
					DrawLineBillboard (Material, LINE_COLOR*GLOW * dimmer, zeroBase, segmentUp, segmentLength, segmentThickness);
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

			if (GetRandomBoolean()) {
				if (glitchAmount > 0.001) {
					float glitchValue = (float)glitchAmount;

					Vector3D offsetRan = new Vector3D (
						(GetRandomDouble () - 0.5) * 2,
						(GetRandomDouble () - 0.5) * 2,
						(GetRandomDouble () - 0.5) * 2
					);
					double dis2Cam = Vector3D.Distance (MyAPIGateway.Session.Camera.Position, position);

					position += offsetRan * dis2Cam * glitchValue * 0.025;
					color *= GetRandomFloat ();
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
			if (!mode) {
				MyTransparentGeometry.AddQuad (materialId, ref quad, color, ref position);
			} else {
				MyTransparentGeometry.AddQuad (materialId, ref quad, color, ref position, -1, MyBillboard.BlendTypeEnum.AdditiveTop);
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

			if (upward) {
				up = radarForward;
			}
			Vector3D left = Vector3D.Cross(up, normal);
			up.Normalize();
			left.Normalize();

			if (GetRandomBoolean()) {
				if (glitchAmount > 0.001) {
					float glitchValue = (float)glitchAmount;

					Vector3D offsetRan = new Vector3D (
						(GetRandomDouble () - 0.5) * 2,
						(GetRandomDouble () - 0.5) * 2,
						(GetRandomDouble () - 0.5) * 2
					);
					double dis2Cam = Vector3D.Distance (MyAPIGateway.Session.Camera.Position, position);

					position += offsetRan * dis2Cam * glitchValue * 0.025;
					color *= GetRandomFloat ();
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

			dList = OrderPlanetsBySize (pList);
			dList = DetermineParentChildRelationships (dList);

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

		public float radarRadius = 0.125f;
		public double radarScale = 0.000025;
		public Vector3D radarOffset = new Vector3D(0, -0.20, -0.575);
		private bool targetingFlipper = false; 
		private MatrixD radarMatrix;

		private Vector3D worldRadarPos;
		private double targettingLerp = 1.5;
		private double targettingLerp_goal = 1.5;
		private double alignLerp = 1.0;
		private double alignLerp_goal = 1.0;

		private double squishValue = 0.75;
		private double squishValue_Goal = 0.75;

		private double rangeBracketBuffer = 500; // Meters to add to a distance as a buffer for purposes of determining bracket.
		private int debugFrameCount = 0;
        private bool debug = false;
        private readonly Stopwatch haloTimer = new Stopwatch();




        //===================================================================================
        //Radar==============================================================================
        //===================================================================================

        public int GetRadarScaleBracket(double rangeToBracket) 
		{
            // Add buffer to encourage zooming out slightly beyond the target
            double bufferedDistance = rangeToBracket + rangeBracketBuffer;
            int bracketedRange = ((int)(bufferedDistance / RANGE_BRACKET_DISTANCE) + 1) * RANGE_BRACKET_DISTANCE;

			// Clamp to the user-defined max radar range
			return Math.Min(bracketedRange, (int)maxRadarRange);
        }


        // DrawRadar modified 2025-07-13 by FenixPK to use Vector3D.DistanceSquared for comparisons.
        // Using .Distance() directly does a square root to get the exact distance since the values are stored squared already.
        // If we need to display a distance, then by all means do the square root.
        // But for things like checking if an entity is within a certain distance etc. we can just use the squares and save CPU cycles. 
        /// <summary>
		/// DrawRadar draws main UI elements of the radar, including the radar circle, holograms, and entities within radar range.
		/// It also handles the targetting reticle and alignment of the radar screen based on the player's position and orientation.
		/// </summary>
		private void DrawRadar()
		{
			// Exit early if not in control or gHandler == null.
            if (gHandler == null)
            {
                return;
            }
            if (gHandler.localGridEntity == null)
            {
                return;
            }
            bool IsPlayerControlling = gHandler.IsPlayerControlling;
            if (!IsPlayerControlling)
            {
                return;
            }

			// Check timer is initialized, and start if not for some reason?
            if (stopwatch == null)
            {
                stopwatch = new Stopwatch();
                stopwatch.Start();
            }

            if (!stopwatch.IsRunning)
            {
                stopwatch.Start();
            }

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

            // Fetch the player's grid position and its world matrix
            Vector3D playerGridPos = gHandler.localGridEntity.GetPosition();
            radarMatrix = gHandler.localGridEntity.WorldMatrix;


            //Head Location=================================================
            // Get the player's character entity
            Vector3D headPosition = Vector3D.Zero;
            IMyCharacter character = MyAPIGateway.Session.Player.Character;

            if (character != null)
            {
                // Extract the head matrix
                MatrixD headMatrix = character.GetHeadMatrix(true);

                // Get the head position from the head matrix
                headPosition = headMatrix.Translation;
                headPosition -= playerGridPos;
            }
            //==============================================================

            // Get the camera's up and left vectors
            MatrixD cameraMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            Vector3 viewUp = cameraMatrix.Up;
            Vector3 viewLeft = cameraMatrix.Left;
            Vector3 viewForward = cameraMatrix.Forward;
            Vector3 viewBackward = cameraMatrix.Backward;
            Vector3D viewBackwardD = cameraMatrix.Backward;

            // Apply the radar offset to the ship's position
            Vector3D radarPos = Vector3D.Transform(radarOffset, radarMatrix) + headPosition;

            // Draw the radar circle
            Vector3D radarUp = radarMatrix.Up;
            Vector3D radarDown = radarMatrix.Down;
            Vector3D radarLeft = radarMatrix.Left;
            Vector3D radarForward = radarMatrix.Forward;
            Vector3D radarBackward = radarMatrix.Backward;

            worldRadarPos = radarPos;

			Vector3D cameraPos = MyAPIGateway.Session.Camera.Position;
            double dis2cameraSqr = Vector3D.DistanceSquared(cameraPos, worldRadarPos);
            if (dis2cameraSqr > 4) // 2^2 = 4
            {
                // If the camera is not positioned sensibly do not draw the UI. My understanding is this is if the radar screen is behind the players head etc. why waste time drawing any of the UI if it can't be seen. 
                return;
            }

            // Can always show our own hologram
            if (EnableHolograms && EnableHolograms_you)
            {
                Vector3D hgPos = HG_Offset;
                Vector3D hgPos_Right = radarPos + radarMatrix.Left * -radarRadius + radarMatrix.Left * HG_Offset.X + radarMatrix.Forward * HG_Offset.Z;
                Vector3D hgPos_Left = radarPos + radarMatrix.Left * -radarRadius * -1 + radarMatrix.Left * HG_Offset.X * -1 + radarMatrix.Forward * HG_Offset.Z;

                double lockInTime_Right = HG_activationTime;
                lockInTime_Right = ClampedD(lockInTime_Right, 0, 1);
                lockInTime_Right = Math.Pow(lockInTime_Right, 0.1);

                DrawQuad(hgPos_Right + (radarUp * HG_Offset.Y * 0.5), viewBackward, 0.15, MaterialCircleSeeThroughAdd, LINE_COLOR * 0.125f, true);

                DrawCircle(hgPos_Right, 0.04 * lockInTime_Right, radarUp, LINE_COLOR, false, 0.5f, 0.00075f);
                DrawCircle(hgPos_Right - (radarUp * 0.015), 0.045, radarUp, LINE_COLOR, false, 0.25f, 0.00125f);

                DrawQuad(hgPos_Right + (radarUp * HG_Offset.Y), viewBackward, 0.1, MaterialCircleSeeThrough, new Vector4(0, 0, 0, 0.75f)); //Dim Backing
            }

            // Check radar active/passive status and broadcast range for player grid.
            bool playerHasPassiveRadar;
            bool playerHasActiveRadar;
			double playerMaxPassiveRange;
            double playerMaxActiveRange;

            // playerGrid is set once earlier in the Draw() method when determining if cockpit is eligible, player controlled etc, and is used to get power draw among other things. Saved for re-use here and elsewhere.
            EvaluateGridAntennaStatus(playerGrid, out playerHasPassiveRadar, out playerHasActiveRadar, out playerMaxPassiveRange, out playerMaxActiveRange);

			// Radar distance/scale should be based on our highest functional, powered antenna (regardless of mode).
			radarScaleRange = playerMaxPassiveRange; // Already limited to global max. Uses passive only because in active mode active and passive will be equal. 

            Vector3D targetPosReturn = new Vector3D(); // Will reuse this.
			if (currentTarget != null && !currentTarget.Closed)
			{
				// In here we will check if the player can still actively or passively target the current target and release them if not, or adjust range brackets accordingly if still targetted. 
				bool playerCanDetect = false;
				double distanceToTargetSqr = 0;
				bool entityHasActiveRadar = false;
				double entityMaxActiveRange = 0;
				CanGridRadarDetectEntity(playerHasPassiveRadar, playerHasActiveRadar, playerMaxPassiveRange, playerMaxActiveRange, playerGridPos, 
					currentTarget, out playerCanDetect, out targetPosReturn, out distanceToTargetSqr, out entityHasActiveRadar, out entityMaxActiveRange);

				if (playerCanDetect)
				{
                    if (debug)
                    {
                        //MyLog.Default.WriteLine($"FENIX_HUD: Target selected, and can detect");
                    }
					double distToTarget = Math.Sqrt(distanceToTargetSqr);
                    radarScaleRange_Goal = GetRadarScaleBracket(distToTarget);
                    squishValue_Goal = 0.0;

					// Handle target hologram here
					if (EnableHolograms && EnableHolograms_them)
					{
						Vector3D hgPos = HG_Offset;
						Vector3D hgPos_Right = radarPos + radarMatrix.Left * -radarRadius + radarMatrix.Left * HG_Offset.X + radarMatrix.Forward * HG_Offset.Z;
						Vector3D hgPos_Left = radarPos + radarMatrix.Left * -radarRadius * -1 + radarMatrix.Left * HG_Offset.X * -1 + radarMatrix.Forward * HG_Offset.Z;

						double lockInTime_Left = HG_activationTimeTarget;
						lockInTime_Left = ClampedD(lockInTime_Left, 0, 1);
						lockInTime_Left = Math.Pow(lockInTime_Left, 0.1);

						DrawCircle(hgPos_Left, 0.04 * lockInTime_Left, radarUp, LINE_COLOR, false, 0.5f, 0.00075f);
						DrawCircle(hgPos_Left - (radarUp * 0.015), 0.045, radarUp, LINE_COLOR, false, 0.25f, 0.00125f);

						// We will have cleared target lock earlier in loop if no longer able to target due to distance or lack of antenna etc.
						if (isTargetLocked)
						{
							DrawQuad(hgPos_Left + (radarUp * HG_Offset.Y * 0.5), viewBackward, 0.15, MaterialCircleSeeThroughAdd, LINECOLOR_Comp * 0.125f, true);
							DrawQuad(hgPos_Left + (radarUp * HG_Offset.Y), viewBackward, 0.1, MaterialCircleSeeThrough, new Vector4(0, 0, 0, 0.75f)); //Dim Backing
						}
						else
						{
							DrawQuad(hgPos_Left + (radarUp * HG_Offset.Y * 0.5), viewBackward, 0.15, MaterialCircleSeeThroughAdd, LINE_COLOR * 0.125f, true);
						}
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
            radarScaleRange = radarScaleRange_Current*radarScaleRange_CurrentLogin;

			radarScale = (radarRadius / radarScaleRange);

			// Draw the current range bracket on screen. 
			// This does a lot. It shows the player if they are in active or passive mode or off.
			// Shows the range of active or passive scanning.
			// Shows the radar scale range which will be bracketed. Ie. your active radar might be set to 4123 meters, your passive radar set to 5512 meters.
			// Your bracket would use your maxPassive and set it in clean increments as the user defined. I use 2000m with 500m buffer. So 5512+buffer would put it into the 8000 range bracket.
			// This means the radar would be zoomed out to show 8k worth of space, despite only detecting objects passively up to 5512 meters and actively up to 4123 meters.
			// Because active mode takes priority I show it's range instead of passive. When active mode off passive will show. Passive's range if different than active can be inferred a bit from scale.
			// Additionally if the radar scale is overriden by having a Target selected a T is show, as scale might be lower than expected. When you select a target nearby it "zooms" the radar scale in. 
            Vector3D textPos = radarPos + radarUp * -0.1 + radarLeft * 0.3; 
            Vector3D textDir = -radarMatrix.Backward; // text faces the player/camera
			double activeRangeDisp = Math.Round(playerMaxActiveRange/1000, 1);
			double passiveRangeDisp = Math.Round(playerMaxPassiveRange/1000, 1);
			double radarScaleDisp = Math.Round(radarScaleRange / 1000, 1);
            string rangeText = $"RDR{(playerHasActiveRadar ? $"[ACT]:{activeRangeDisp}" : playerHasPassiveRadar ? $"[PAS]:{passiveRangeDisp}" : "[OFF]:0")}KM [SCL]:{radarScaleDisp}KM {(currentTarget != null ? "(T)" : "")}";
            drawText(rangeText, 0.0045, textPos, textDir, LINE_COLOR);

			if (!playerHasActiveRadar && !playerHasPassiveRadar) 
			{
                if (debug)
                {
                    //MyLog.Default.WriteLine($"FENIX_HUD: Player has no active or passive radar, exiting DrawRadar()");
                }
            }
            if (debug)
            {
                //MyLog.Default.WriteLine($"FENIX_HUD: playerHasActiveRadar={playerHasActiveRadar}, playerHasPassiveRadar={playerHasPassiveRadar} - continuing to draw.");
            }

			Vector4 color_Current = color_VoxelBase; // Default to voxelbase color
			float scale_Current = 1.0f;

			// Radar Pulse Timer for the pulse animation
            double radarTimer = stopwatch.Elapsed.TotalSeconds / 3;
			radarTimer = 1 - (radarTimer - Math.Truncate (radarTimer))*2;

			// Radar attack timer to flip the blips and make them pulsate. Goal is to tie this to being targetted by those grids rather than by distance to them.
			double attackTimer = stopwatch.Elapsed.TotalSeconds*4;
			attackTimer = (attackTimer - Math.Truncate (attackTimer));
			if (attackTimer > 0.5) {
				targetingFlipper = true;
			} else {
				targetingFlipper = false;
			}
			// Draw Rings
            DrawQuad(radarPos, radarUp, (double)radarRadius * 0.03f, MaterialCircle, LINE_COLOR * 5f); //Center
			// Draw perspective lines
            float radarFov = Clamped(GetCameraFOV ()/2, 0, 90)/90;
			DrawLineBillboard (Material, LINE_COLOR*GLOW*0.4f, radarPos, Vector3D.Lerp(radarMatrix.Forward,radarMatrix.Left,radarFov), radarRadius*1.45f, 0.0005f); //Perspective Lines
			DrawLineBillboard (Material, LINE_COLOR*GLOW*0.4f, radarPos, Vector3D.Lerp(radarMatrix.Forward,radarMatrix.Right,radarFov), radarRadius*1.45f, 0.0005f);

			// Animate radar pulse
			for (int i = 1; i < 11; i++) {
				int radarPulseTime = (int)Math.Truncate (10 * radarTimer);
				float radarPulse = 1;
				if (radarPulseTime == i) {
					radarPulse = 2;
				}
				DrawCircle(radarPos-(radarUp*0.003), (radarRadius*0.95)-(radarRadius*0.95)*(Math.Pow((float)i/10, 4)), radarUp, LINE_COLOR, false, 0.25f*radarPulse, 0.00035f);
			}
            
			// Draw border
            DrawQuadRigid(radarPos, radarUp, (double)radarRadius*1.18, MaterialBorder, LINE_COLOR, true); //Border

			// Draw compass
			if(EnableGauges){
				DrawQuadRigid(radarPos-(radarUp*0.005), -radarUp, (double)radarRadius*1.5, MaterialCompass, LINE_COLOR, true); //Compass
			}
			DrawQuad(radarPos-(radarUp*0.010), radarUp, (double)radarRadius*2.25, MaterialCircleSeeThrough, new Vector4(0, 0, 0, 0.75f)); //Dim Backing
			DrawQuad(radarPos + (radarUp*0.025), viewBackward, 0.25, MaterialCircleSeeThroughAdd, LINE_COLOR*0.075f, true);

            updateRadarAnimations(playerGridPos);

            double fadeDistance = radarScaleRange * (1-FADE_THRESHHOLD); // Eg at 0.01 would be 0.99%. For a radar distance of 20,000m this means the last 19800-19999 becomes fuzzy/dims the blip.
            double fadeDistanceSqr = fadeDistance * fadeDistance; // Sqr for fade distance in comparisons.
            double radarShownRangeSqr = radarScaleRange * radarScaleRange; // Anything over this range, even if within sensor range, can't be drawn on screen. 

            // Okay I had to do some research on this. The way this works is Math.Sin() returns a value between -1 and 1, so we add 1 to it to get a value between 0 and 2, then divide by 2 to get a value between 0 and 1.
			// Basically shifting the wave up so we are into positive value only territory, and then scaling it to a 0-1 range where 0 is completely invisible and 1 is fully visible. Neat.
            float haloPulse = (float)((Math.Sin(haloTimer.Elapsed.TotalSeconds * (4*Math.PI)) + 1.0) * 0.5); // 0 -> 1 -> 0 smoothly. Should give 2 second pulses. Could do (2*Math.PI) for 1 second pulses
			

            for (int i = 0; i < radarPings.Count; i++)
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
				if (entity.GetTopMostParent() == gHandler.localGridEntity.GetTopMostParent()) 
				{
					continue; // Skip drawing yourself on the radar.
				}

				bool playerCanDetect = false;
				Vector3D entityPos;
				double entityDistanceSqr;
				bool entityHasActiveRadar = false;
				double entityMaxActiveRange = 0;

				CanGridRadarDetectEntity(playerHasPassiveRadar, playerHasActiveRadar, playerMaxPassiveRange, playerMaxActiveRange, playerGridPos, 
					entity, out playerCanDetect, out entityPos, out entityDistanceSqr, out entityHasActiveRadar, out entityMaxActiveRange);
				if (!playerCanDetect || entityDistanceSqr > radarShownRangeSqr) // If can't detect it, or the radar scale prevents showing it move on.
				{
					continue;
				}

				// Handle fade for edge
				float fadeDimmer = 1f;
                // Have to invert old logic, original author made this extend radar range past configured limit. 
				// I am having the limit as a hard limit be it global config or antenna broadcast range, so we instead make a small range before that "fuzzy" instead. 
				// If it was outside max range it wouldn't have been detected by the player actively or passively, so we skipped it already. Meaning all we have to do is check if it is in the fade region and fade it or draw it regularly. 
                if (entityDistanceSqr >= fadeDistanceSqr) 	
				{
                    fadeDimmer = 1 - Clamped(1 - (float)((fadeDistanceSqr - entityDistanceSqr) / (fadeDistanceSqr - radarShownRangeSqr)), 0, 1);
                }
                Vector3D scaledPos = ApplyLogarithmicScaling(entityPos, playerGridPos); // Apply radar scaling

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

				// Check if relationship status has changed and update ping (eg. if a ship flipped from neutral to hostile or hostile to friendly via capture). 
				RadarPing currentPing = radarPings[i];
                UpdateExistingRadarPingStatus(ref currentPing);
				radarPings[i] = currentPing;

                if (entityDistanceSqr < radarShownRangeSqr * (1-FADE_THRESHHOLD)) // Once we pass the fade threshold an auidible and visual cue should be applied. 
                {
                    if (!radarPings[i].Announced)
                    {
                        radarPings[i].Announced = true;
                        if (radarPings[i].Status == RelationshipStatus.Hostile && entityDistanceSqr > 250000) //500^2 = 250,000
                        {
                            PlayCustomSound(SP_ENEMY, worldRadarPos);
                            newAlertAnim(entity);
                        }
                        else if (radarPings[i].Status == RelationshipStatus.Friendly && entityDistanceSqr > 250000)
                        {
                            PlayCustomSound(SP_NEUTRAL, worldRadarPos);
                            newBlipAnim(entity);
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
                float pulseTimer = (float)(ClampedD(radarTimer, 0, 1) + 0.5 + Math.Min(pulseDistance, radarRadius) / radarRadius);
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
                        color_Current = LINE_COLOR;
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
                if (entityHasActiveRadar && entityDistanceSqr <= entityMaxActiveRange*entityMaxActiveRange) 
                {
                    float pulseScale = 1.0f + (haloPulse * 0.25f); // Scale the halo by 25% at max pulse
                    float pulseAlpha = 1.0f * (1.0f - haloPulse); // Alpha goes from 0.25 to 0 at max pulse, so it fades out as pulse increases.
                    float ringSize =  blipSize * pulseScale; // Scale the ring size based on the pulse
                    Vector4 haloColor = color_Current * 0.25f * (float)haloPulse; // subtle, fading
                    // Use the same position and orientation as the blip
                    DrawCircle(radarEntityPos, ringSize, radarUp, color_Current, false, 0.5f * pulseAlpha, 0.00035f); //0.5f * haloPulse

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
			Vector4 crossColor = LINE_COLOR;

			if (currentTarget != null) {
				double dis2Cam1 = Vector3D.Distance(cameraPos, targetPosReturn);
				if (dis2Cam1 > 50000) 
				{
					ReleaseTarget();
				} 
				else 
				{
					targettingLerp_goal = 1;
					targettingLerp = LerpD (targettingLerp, targettingLerp_goal, deltaTime*10);

					Vector3D targetDir = Vector3D.Normalize(currentTarget.WorldVolume.Center - cameraPos) * 0.5;
					double dis2Cam = Vector3D.Distance(cameraPos, cameraPos + targetDir);
					DrawQuadRigid(cameraPos + targetDir, cameraMatrix.Forward, dis2Cam * 0.125 * targettingLerp, USE_HOLLOW_RETICLE ? MaterialLockOnHollow : MaterialLockOn, LINE_COLOR*(float)targettingLerp, false);
					drawText(currentTarget.DisplayName, dis2Cam * 0.01, cameraPos + targetDir + (cameraMatrix.Up * dis2Cam * 0.02) + (cameraMatrix.Right * dis2Cam * 0.10 * targettingLerp), cameraMatrix.Forward, LINE_COLOR);

					string disUnits = "m";
					double dis2CamDisplay = dis2Cam1;
					if (dis2Cam1 > 1500) 
					{
						disUnits = "km";
						dis2CamDisplay = dis2CamDisplay / 1000;
					}
					drawText(Convert.ToString(Math.Round(dis2CamDisplay)) + " " + disUnits, dis2Cam * 0.01, cameraPos + targetDir + (cameraMatrix.Up * dis2Cam * -0.02) + (cameraMatrix.Right * dis2Cam * 0.11 * targettingLerp), cameraMatrix.Forward, LINE_COLOR);

					double dotProduct = Vector3D.Dot(radarMatrix.Forward, targetDir);

					Vector3D targetPipPos = radarPos;
					Vector3D targetPipDir = Vector3D.Normalize(currentTarget.WorldVolume.Center - playerGridPos) * ((double)radarRadius * crossSize * 0.55);

					// Calculate the component of the position along the forward vector
					Vector3D forwardComponent = Vector3D.Dot(targetPipDir, radarMatrix.Forward) * radarMatrix.Forward;

					// Subtract the forward component from the original position to remove the forward/backward contribution
					targetPipDir = targetPipDir - forwardComponent;

					targetPipPos += targetPipDir;
					targetPipPos += crossOffset;

					if (dotProduct > 0.49) {
						crossColor = LINECOLOR_Comp;
						alignLerp_goal = 0.8;

						if (gHandler.localGridSpeed > 0.1 && dis2Cam1 > 100) {
							double arivalTime = dis2Cam1 / gHandler.localGridSpeed;
							string unitsTime = "sec";
							if (arivalTime > 120) {
								arivalTime /= 60;
								unitsTime = "min";

								if (arivalTime > 60) {
									arivalTime /= 60;
									unitsTime = "hrs";
								}
							}

							drawText(Convert.ToString(Math.Round(arivalTime)) + " " + unitsTime, dis2Cam * 0.01, cameraPos + targetDir + (cameraMatrix.Up * dis2Cam * -0.05) + (cameraMatrix.Right * dis2Cam * 0.11 * targettingLerp), cameraMatrix.Forward, LINECOLOR_Comp);
						}
					} else {
						alignLerp_goal = 1;
					}
					alignLerp = LerpD (alignLerp, alignLerp_goal, deltaTime * 20);

					if (dotProduct > 0) {
						DrawQuadRigid(targetPipPos, radarMatrix.Backward, crossSize * 0.125 * 0.125, MaterialCircle, LINECOLOR_Comp, false); //LockOn
					} else {
						DrawQuadRigid(targetPipPos, radarMatrix.Backward, crossSize * 0.125 * 0.125, MaterialCircleHollow, LINECOLOR_Comp, false); //LockOn
					}
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
			DrawQuadRigid(radarPos+crossOffset, radarMatrix.Backward, crossSize * 0.125, MaterialCrossOutter, LINE_COLOR, false); //Target
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
			// 2) Cap the MAX_PINGS at 500, so the screen doesn't get too cluttered. At a certain point so many pings becomes impossible to read anyway so the the
			// reality is we can be missing some pings because they aren't there and we saved CPU power,
			// or we can be missing some pings because there are so many it's become a soup but we wasted a bunch of CPU to do it.

            radarEntities.Clear(); // Always clear first
            radarPings.Clear();

            MyAPIGateway.Entities.GetEntities(radarEntities, entity =>
            {
				if (entity == null) return false;
                if (entity.MarkedForClose || entity.Closed) return false;
                if (entity is MyPlanet) return false;
                if (!ShowVoxels && entity is IMyVoxelBase) return false;

                // Skip invalid display names or types
                if (entity.DisplayName == null || entity.DisplayName == "Stone") return false;

                return true;
            });


            foreach (var entity in radarEntities)
            {
				if (radarPings.Count >= MAX_PINGS) 
				{
                    break;
                }
                RadarPing ping = newRadarPing(entity);

                // Only add if width is valid
                if (ping.Width > 0.1f && ping.Width < 10000f) // safety bounds
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

		private Stopwatch timeSinceSound = new Stopwatch();

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

			if (!timeSinceSound.IsRunning) {
				timeSinceSound.Start ();
			}

			if (timeSinceSound.Elapsed.TotalSeconds > 0.05) {

//				queuedSound qs = new queuedSound ();
//				qs.soundId = soundId;
//				qs.position = position;
//				queuedSounds.Add (qs);
//
				ED_soundEmitter.SetPosition (position);
				ED_soundEmitter.PlaySound (soundId);

				timeSinceSound.Restart ();
			}
		}

		public void PlayQueuedSounds()
		{
			foreach(var qs in queuedSounds){

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
                ping.Color = LINE_COLOR * GLOW; //color_VoxelBase;
                ping.Status = RelationshipStatus.Vox;
                IMyVoxelBase voxelEntity = entity as IMyVoxelBase;
                if (voxelEntity != null)
                {
                    double vixelWidth = voxelEntity.PositionComp.WorldVolume.Radius;
                    ping.Width = (float)vixelWidth / 250;
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

			ping.Time = new Stopwatch ();
			ping.Time.Start ();

			UpdateNewRadarPingStatus(ref ping);

			return ping;
		}

		public void newRadarAnimation(VRage.ModAPI.IMyEntity entity, int loops, double lifeTime, double sizeStart, double sizeStop, float fadeStart, float fadeStop, MyStringId material, Vector4 colorStart, Vector4 colorStop, Vector3D offsetStart, Vector3D offsetStop){
			RadarAnimation r = new RadarAnimation ();
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

			r.Time = new Stopwatch ();
			r.Time.Start ();

			RadarAnimations.Add (r);
		}
			
		public void newAlertAnim(VRage.ModAPI.IMyEntity entity){
			if (entity == null) {
				return;
			}

			Vector4 color = new Vector4 (Color.Red, 1);
			Vector3D zero = new Vector3D (0, 0, 0);

			newRadarAnimation (entity, 4, 0.20, 0.02, 0.002, 5, 1, MaterialTarget, color, color, zero, zero);
		}

		public void newBlipAnim(VRage.ModAPI.IMyEntity entity)
		{
			if (entity == null) 
			{
				return;
			}

			Vector4 color = new Vector4 (Color.Yellow, 1);
			Vector3D zero = new Vector3D (0, 0, 0);

			newRadarAnimation (entity, 1, 0.25, 0.02, 0.002, 3, 1, MaterialTarget, color, color, zero, zero);
		}

		public void updateRadarAnimations(Vector3D shipPos){
			List<RadarAnimation> deleteList = new List<RadarAnimation>();

			MatrixD cameraMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
			Vector3 viewUp = cameraMatrix.Up;
			Vector3 viewLeft = cameraMatrix.Left;

			for (int i = 0; i < RadarAnimations.Count; i++){
				if (RadarAnimations [i].Time.Elapsed.TotalSeconds > (RadarAnimations [i].LifeTime * RadarAnimations [i].Loops)) 
				{
					//If time is greater than length of animation, add to the deletion list and skip rendering.
					deleteList.Add (RadarAnimations [i]); // Why was this commented out?
				}
				else
				{
					VRage.ModAPI.IMyEntity entity = 	RadarAnimations[i].Entity;

					//Calculate time scaling
					double cTime = 		RadarAnimations [i].Time.Elapsed.TotalSeconds / RadarAnimations [i].LifeTime;
					cTime = 			cTime - Math.Truncate (cTime);
					cTime = 			MathHelperD.Clamp (cTime, 0, 1);

					//Lerp Attributes based on time scaling
					Vector4 color = 	Vector4.Lerp (RadarAnimations [i].ColorStart, RadarAnimations [i].ColorStop, (float)cTime);
					Vector3D offset = 	Vector3D.Lerp (RadarAnimations [i].OffsetStart, RadarAnimations [i].OffsetStop, (float)cTime);
					float fade =  		LerpF (RadarAnimations [i].FadeStart, RadarAnimations [i].FadeStop, (float)cTime);
					double size = 		LerpD (RadarAnimations [i].SizeStart, RadarAnimations [i].SizeStop, cTime);

					Vector3D entityPos = entity.GetPosition ();
					Vector3D upSquish = Vector3D.Dot (entityPos, radarMatrix.Up) * radarMatrix.Up;
					//entityPos -= (upSquish*squishValue); //------Squishing the vertical axis on the radar to make it easier to read... but less vertically accurate.

					// Apply radar scaling
					Vector3D scaledPos = ApplyLogarithmicScaling(entityPos, shipPos); 

					// Position on the radar
					Vector3D radarEntityPos = worldRadarPos + scaledPos + offset;

					MyTransparentGeometry.AddBillboardOriented (RadarAnimations[i].Material, color*fade, radarEntityPos, viewLeft, viewUp, (float)size);
				}
			}

			//Delete all animations that were flagged in the prior step.
			foreach(var d in deleteList)
			{
				if (RadarAnimations.Contains (d)) 
				{
					d.Time.Stop ();
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
			int segments = LINE_DETAIL * Convert.ToInt32(Clamped(Remap((float)radius, 1000, 10000000, 1, 8), 1, 16));
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
					if (GetRandomBoolean()) {
						if (glitchAmount > 0.001) {
							float glitchValue = (float)glitchAmount;

							Vector3D offsetRan = new Vector3D (
								(GetRandomDouble () - 0.5) * 2,
								(GetRandomDouble () - 0.5) * 2,
								(GetRandomDouble () - 0.5) * 2
							);
							double dis2Cam = Vector3D.Distance (MyAPIGateway.Session.Camera.Position, position);

							position += offsetRan * dis2Cam * glitchValue * 0.005;
							color *= GetRandomFloat ();
						}
					}



					double dis = Vector3D.Distance (position, orbitPoints [(i + 1) % segments]);
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
				DrawLineBillboard(MaterialSquare, LINE_COLOR * dimmer, point1, direction, 0.9f, segmentThickness, BlendTypeEnum.Standard);
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
                        EvaluateGridAntennaStatus(playerGrid, out playerHasPassiveRadar, out playerHasActiveRadar, out playerMaxPassiveRange, out playerMaxActiveRange);

                        bool canPlayerDetect = false;
                        Vector3D entityPos = new Vector3D();
                        CanGridRadarDetectEntity(playerHasPassiveRadar, playerHasActiveRadar, playerMaxPassiveRange, playerMaxActiveRange, gHandler.localGridEntity.GetPosition(), newTarget, out canPlayerDetect, out entityPos, out relativeDistanceSqr, out entityHasActiveRadar, out entityMaxActiveRange);

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
            newBlipAnim(newTarget);
            PlayCustomSound(SP_ZOOMOUT, worldRadarPos);
        }


        private VRage.ModAPI.IMyEntity FindEntityInSight()
		{
			var camera = MyAPIGateway.Session.Camera;
			Vector3D cameraPosition = camera.WorldMatrix.Translation;
			Vector3D cameraForward = camera.WorldMatrix.Forward;

			var entities = new HashSet<VRage.ModAPI.IMyEntity>();
			MyAPIGateway.Entities.GetEntities(entities, e => e is VRage.Game.ModAPI.IMyCubeGrid || e is IMyCharacter);

			VRage.ModAPI.IMyEntity closestEntity = null;
			double highestDotProduct = 0.0;  // This will store the highest dot product found

			foreach (var entity in entities)
			{
				if (entity == null)
					continue;

				Vector3D entityPosition = entity.GetPosition();
				Vector3D directionToEntity = Vector3D.Normalize(entityPosition - cameraPosition);
				double distanceToEntity = Vector3D.Distance(cameraPosition, entityPosition);

				double dotProduct = Vector3D.Dot(cameraForward, directionToEntity);

				// Check both the dot product and the distance
				if (dotProduct > highestDotProduct && dotProduct >= 0.899 && distanceToEntity <= 50000)
				{
					highestDotProduct = dotProduct;
					closestEntity = entity;
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
			if (isGuiVisible) {
				return;
			}

			if (message != Echo_String_Prev) {
				// This method should be replaced with your actual logging or display method
				MyAPIGateway.Utilities.ShowMessage ("Echo", message);
			}

			Echo_String_Prev = message;
		}


		public class BlockTracker
		{
			public VRage.Game.ModAPI.IMySlimBlock Block;
			public Vector3D Position;
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
		private Vector3D HG_Offset = new Vector3D(-0.2,0.075,0);
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
            DrawArc(hgPos, 0.065, radarMatrix.Up, 0, 90 * ((float)hitPoints / 100) * (float)bootUpAlpha, LINE_COLOR, 0.015f, 1f);

            string hitPointsString = Convert.ToString(Math.Ceiling(healthCurrent / 100 * bootUpAlpha));
            Vector3D textPos = hgPos + (radarMatrix.Left * (hitPointsString.Length * fontSize)) + (radarMatrix.Up * fontSize) + (radarMatrix.Forward * fontSize * 0.5) + textPosOffset;
            drawText(hitPointsString, fontSize, textPos, radarMatrix.Forward, LINE_COLOR);

            activationTime += deltaTime * 0.667;

            HG_UpdateBlockStatus(ref blockCount, blocks, ref healthCurrent);
            double tempTimeToReady = 0;
            HG_UpdateDriveStatus(drives, ref shieldCurrent, ref shieldMax, out tempTimeToReady);
            if (tempTimeToReady > 0)
            {
                timeToReady = tempTimeToReady;
            }
        }




        public void HG_Update()
		{
			if (!Drives_deltaTimer.IsRunning) {
				Drives_deltaTimer.Start ();
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

			
			if (hologramLocalGrid != null && ENABLE_HOLOGRAMS_GLOBAL && EnableHolograms_you) //God, I really should have made this far more generic so I don't have to manage the same code for the player and the target seperately...
				// No problem, FenixPK has your got your back jack. Standardized this 2025-07-14. 
			{

				HG_DrawHologramLocal(hologramLocalGrid, localGridBlocks);
				Vector3D hgPos_Right = worldRadarPos + radarMatrix.Right * radarRadius + radarMatrix.Left * HG_Offset.X + radarMatrix.Down * 0.0075 + (radarMatrix.Forward * fontSize * 2);
				Vector3D textPos_Offset = (radarMatrix.Left * 0.065);
				Vector3D shieldPos_Right = worldRadarPos + radarMatrix.Left * -radarRadius + radarMatrix.Left * HG_Offset.X + radarMatrix.Up * HG_Offset.Y + radarMatrix.Forward * HG_Offset.Z;
				DrawHologramStatus(hgPos_Right, shieldPos_Right, textPos_Offset, ref HG_activationTime, ref localGridShieldsLast, ref localGridShieldsCurrent,
					ref localGridShieldsMax, ref localGridHealthCurrent, localGridHealthMax, deltaTime, localGridDrives, localGridBlocks, ref localGridTimeToReady, fontSize, ref localGridBlockCounter);
			}


			if (isTargetLocked && currentTarget != null && ENABLE_HOLOGRAMS_GLOBAL && EnableHolograms_them) {
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
                    Vector3D hgPos_Left = worldRadarPos + radarMatrix.Left * radarRadius + radarMatrix.Right * HG_Offset.X + radarMatrix.Down * 0.0075 + (radarMatrix.Forward * fontSize * 2);
                    Vector3D textPos_Offset = (radarMatrix.Right * 0.065);
                    Vector3D shieldPos_Left = worldRadarPos + radarMatrix.Right * -radarRadius + radarMatrix.Right * HG_Offset.X + radarMatrix.Up * HG_Offset.Y + radarMatrix.Forward * HG_Offset.Z;
                    DrawHologramStatus(hgPos_Left, shieldPos_Left, textPos_Offset, ref HG_activationTimeTarget, ref targetShieldsLast, ref targetShieldsCurrent,
                        ref targetShieldsMax, ref targetGridHealthCurrent, targetGridHealthMax, deltaTime, targetGridDrives, targetGridBlocks, ref targetTimeToReady, fontSize, ref targetBlockCounter);
				}
			} else {
				HG_initializedTarget = false;
			}
		}

		private void drawShields(Vector3D pos, double sp_cur, double sp_max, double bootTime, double time2ready){

			double sp = sp_cur/sp_max;

			double boot1 = ClampedD(RemapD(bootTime, 0.5, 0.8, 0, 1), 0, 1);
			double boot2 = ClampedD(RemapD(bootTime, 0.8, 0.925, 0, 1), 0, 1);
			double boot3 = ClampedD(RemapD(bootTime, 0.925, 1, 0, 1), 0, 1);

			double yShieldPer1 = ClampedD(RemapD(sp, 0, 0.333, 0, 1), 0, 1);
			double yShieldPer2 = ClampedD(RemapD(sp, 0.333, 0.667, 0, 1), 0, 1);
			double yShieldPer3 = ClampedD(RemapD(sp, 0.667, 1, 0, 1), 0, 1);

			Vector3D dir = Vector3D.Normalize ((radarMatrix.Up * 2 + radarMatrix.Backward) / 3);

			if (sp > 0.01) {
				bool dotty = false;
				if (yShieldPer1 > 0.01) {
					dotty = yShieldPer1 < 0.25;
					DrawCircle (pos, 0.08 - (1 - boot3) * 0.08, dir, LINECOLOR_Comp * (float)Math.Ceiling (boot3), dotty, 1, 0.001f * (float)yShieldPer1);
				}
				if (yShieldPer2 > 0.01) {
					dotty = yShieldPer2 < 0.25;
					DrawCircle (pos, 0.085 - (1-boot2)*0.085, dir, LINECOLOR_Comp * (float)Math.Ceiling(boot2), dotty, 1, 0.001f * (float)yShieldPer2);
				}
				if (yShieldPer3 > 0.01) {
					dotty = yShieldPer3 < 0.25;
					DrawCircle (pos, 0.09 - (1-boot1)*0.09, dir, LINECOLOR_Comp * (float)Math.Ceiling(boot1), dotty, 1, 0.001f * (float)yShieldPer3);
				}
			}else{
				DrawCircle (pos, 0.08, dir, new Vector4(1,0,0,1), true, 0.5f, 0.001f);
			}

			double fontSize = 0.005;
			string ShieldValueS = Convert.ToString (Math.Round(sp_cur * bootTime * 1000));
			Vector4 ShieldValueC = LINECOLOR_Comp;

			if (sp < 0.01) {
				ShieldValueS = "SHIELDS DOWN";
				ShieldValueC = new Vector4 (1, 0, 0, 1);

				string time2readyS = FormatSecondsToReadableTime (time2ready);

				Vector3D timePos = pos + (radarMatrix.Backward * 0.065) + (radarMatrix.Down * fontSize * 8) + (radarMatrix.Left * time2readyS.Length * fontSize);
				drawText (time2readyS, fontSize, timePos, radarMatrix.Forward, ShieldValueC, 1);
			}
			Vector3D textPos = pos + (radarMatrix.Backward * 0.065) + (radarMatrix.Down * fontSize * 4) + (radarMatrix.Left * ShieldValueS.Length * fontSize);
			drawText (ShieldValueS, fontSize, textPos, radarMatrix.Forward, ShieldValueC, 1);

		}

		public string FormatSecondsToReadableTime(double seconds)
		{
			if (double.IsNaN(seconds) || double.IsInfinity(seconds))
			{
				return "Invalid time"; // Handle invalid input appropriately
			}

			if (seconds > 86400) {
				seconds /= 86400;
				seconds = Math.Round (seconds);
				string days = $"{seconds} Days";
				return days;
			}
			seconds = ClampedD (seconds, 0.001, double.MaxValue);

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
			if (tempCount >= 0 && tempCount < blocksList.Count) {

				if (blocksList [tempCount].Block != null) {
					VRage.Game.ModAPI.IMySlimBlock block = blocksList [tempCount].Block;
					blocksList [tempCount].HealthCurrent = block.Integrity;
					blocksList [tempCount].HealthMax = block.MaxIntegrity;

					hitPoints -= blocksList [tempCount].HealthLast - blocksList [tempCount].HealthCurrent;

					blocksList [tempCount].HealthLast = blocksList [tempCount].HealthCurrent;

					//DAMAGE EVENT if Last is more than Current
				}

                tempCount += 1;
				if (tempCount >= blocksList.Count) {
                    tempCount = 0;
				}

			} else {
                tempCount = 0;
			}
			blockCount = tempCount;
		}

		private Stopwatch Drives_deltaTimer = new Stopwatch ();
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

					if (hitPointsMax != hitPointsCurrent) {
						double currentStoredPower = 	block.JumpDrive.CurrentStoredPower;
						double lastStoredPower = 		block.JumpDriveLastStoredPower;
						double difPower = 				(currentStoredPower - lastStoredPower);

						if( difPower > 0){
							double powerPerSecond = 	(currentStoredPower-lastStoredPower) / Drives_deltaTime;

							if (powerPerSecond > 0) {
								double timeRemaining = ((block.JumpDrive.MaxStoredPower - currentStoredPower) / powerPerSecond)*100;

								if (timeRemaining < minTime) {
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
			if (target == null) {
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
				block.ComputeWorldCenter(out localPosition); // Gets world position for the center of the block
				scaledPosition = localPosition; // Copy the world position to sc
                scaledPosition -= gridCenter; // set scaledPosition to be relative to the center of the grid
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
				BlockTracker BT = new BlockTracker ();
				BT.Position = Position; // Okay my understanding of this is limited at best.
				// I believe what BT.Position actually contains is "where this block would be if the grid was at origin (0, 0, 0), facing forward, and normalized to unit size." 
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

			if (grid == null) {
				return blockPositions;
			}

			var center = grid.WorldVolume.Center;
			var pos = grid.GetPosition ();

			MatrixD inverseMatrix = GetRotationMatrix (grid.WorldMatrix);
			inverseMatrix = MatrixD.Invert (inverseMatrix);


			var blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
			grid.GetBlocks(blocks);

			foreach (var block in blocks)
			{
				Vector3D sc;
				//block.ComputeScaledCenter(out sc);
				block.ComputeWorldCenter (out sc);
				sc -= center;
				sc = Vector3D.Transform (sc, inverseMatrix);
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

		private void HG_DrawHologramLocal(VRage.Game.ModAPI.IMyCubeGrid localGrid, List<BlockTracker> blockInfo)
		{
			if (localGrid != null)
			{
				bool isEntityTarget = false;
                // This uses the cockpit controlled by the player and gets the angular velocity of the cubeblock.
                MatrixD angularRotationWiggle = CreateNormalizedLocalGridRotationMatrix(); // Used to apply a "wiggle" effect to the hologram based on the angular velocity of the grid.
                
                foreach (var BT in blockInfo)
                {
					//Tests
					//So if I were to create a fake MatrixD I could do whatever I want to the view...
					//MatrixD rotate180X = MatrixD.CreateRotationX(MathHelper.ToRadians(180)); // This rotates the position 180 degrees around the X axis. Was math.pi in radians?
					//Vector3D positionTest = Vector3D.Rotate(BT.Position, rotate180X);
					//positionTest = Vector3D.Transform(positionTest, targetGrid.WorldMatrix);
					// YES! The above did exactly what I expected, it rotated the position of the hologram 180 degrees around the X axis. So it was upside down. 
					// HOWEVER it still rotates based on how the player rotates likely because HG_DrawBillboard uses the camera's position to draw the hologram...

					// Note to self, we can create MatrixD's that have various rotational transformations and then use them here on a period so we get it to rotate through top, bottom, left, right, front, back etc. 
					// Or make it a toggle with a keybind? So many ideas. 
					Vector3D blockPositionWiggled = Vector3D.Rotate(BT.Position, angularRotationWiggle); // This applies the angular rotation based on the angular velocity of the grid.
                    // This is to give a "wiggle" effect to a fixed hologram if you are rotating to represent how quickly you are rotating. For eg. if viewed from the back and you pitch nose up
                    // the hologram will have the nose pitch up based on how fast you are rotating. The faster you are rotating in the upward direction, the more the nose of the hologram will pitch up.
					MatrixD rotationOnlyGridMatrix = localGrid.WorldMatrix;
					rotationOnlyGridMatrix.Translation = Vector3D.Zero; // Set the translation to zero to get only the rotation component.
                    Vector3D blockPositionRotated = Vector3D.Transform(blockPositionWiggled, rotationOnlyGridMatrix);
                    double HealthPercent = ClampedD(BT.HealthCurrent / BT.HealthMax, 0, 1);
                    HG_DrawBillboard(blockPositionRotated, localGrid, isEntityTarget, HealthPercent);
                }
            }
        }

        private void HG_DrawHologramTarget(VRage.Game.ModAPI.IMyCubeGrid targetGrid, List<BlockTracker> blockInfo)
		{
			if (targetGrid != null)
			{
				bool isEntityTarget = true;
				// Now that we know
				//Vector3D angularVelocity = gHandler.localGridVelocityAngular; // This uses the target grid's angular velocity.
				//MatrixD rotationMatrix = CreateAngularRotationMatrix(angularVelocity, 10); // 
				
				foreach (BlockTracker BT in blockInfo)
				{
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
                    MatrixD rotationOnlyGridMatrix = targetGrid.WorldMatrix;
					rotationOnlyGridMatrix.Translation = Vector3D.Zero; // Set the translation to zero to get only the rotation component.
                    Vector3D positionRotated = Vector3D.Transform(BT.Position , rotationOnlyGridMatrix); // This transforms the position of the block to be relative to the target grid's world matrix, but without translation.

					double HealthPercent = ClampedD(BT.HealthCurrent / BT.HealthMax, 0, 1);
                    HG_DrawBillboard(positionRotated, targetGrid, isEntityTarget, HealthPercent);
                }
            }
        }

        private void HG_DrawHologram(VRage.Game.ModAPI.IMyCubeGrid grid, List<BlockTracker> blockInfo, bool isEntityTarget = false)
		{
			if (grid != null)
			{
				

				if (isEntityTarget)
				{
					
                    Vector3D angularVelocity = grid.Physics.AngularVelocity;
                    MatrixD rotationMatrix = CreateAngularRotationMatrix(angularVelocity);
                    //MatrixD rotatedMatrix = ApplyRotation(grid.WorldMatrix, rotationMatrix);
                    foreach (var BT in blockInfo)
                    {
                        Vector3D positionR = BT.Position;
                        positionR = Vector3D.Rotate(BT.Position, rotationMatrix);
                        Vector3D positionT = Vector3D.Transform(positionR, grid.WorldMatrix);
                        double HealthPercent = ClampedD(BT.HealthCurrent / BT.HealthMax, 0, 1);
                        HG_DrawBillboard(positionT - grid.GetPosition(), grid, isEntityTarget, HealthPercent);
                    }
                }
				else
				{
                    Vector3D angularVelocity = gHandler.localGridVelocityAngular;
                    MatrixD rotationMatrix = CreateAngularRotationMatrix(angularVelocity);
                    //MatrixD rotatedMatrix = ApplyRotation(grid.WorldMatrix, rotationMatrix);
                    foreach (var BT in blockInfo)
                    {
                        Vector3D positionR = BT.Position;
                        Vector3D positionT = Vector3D.Transform(positionR, grid.WorldMatrix);
                        double HealthPercent = ClampedD(BT.HealthCurrent / BT.HealthMax, 0, 1);
                        HG_DrawBillboard(positionT - grid.GetPosition(), grid, isEntityTarget, HealthPercent);
                    }
                
            }
			}
		}





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

		private void HG_DrawBillboard(Vector3D position, VRage.Game.ModAPI.IMyCubeGrid grid, bool flippit = false, double HP = 1)
		{
			if (HP < 0.01) {
				return;
			}
			bool randoTime = false;
			if (GetRandomFloat () > 0.95f || glitchAmount > 0.5) {
				randoTime = true;
			}

			double bootUpAlpha = 1;

			if (flippit) {
				bootUpAlpha = HG_activationTimeTarget;
			} else {
				bootUpAlpha = HG_activationTime;
			}

			bootUpAlpha = ClampedD (bootUpAlpha, 0, 1);
			bootUpAlpha = Math.Pow (bootUpAlpha, 0.25);

			if (GetRandomDouble () > bootUpAlpha) {
				position *= bootUpAlpha;
			}
			if (GetRandomDouble () > bootUpAlpha) {
				randoTime = true;
			}

			var camera = MyAPIGateway.Session.Camera;
			Vector3D AxisLeft = camera.WorldMatrix.Left;
			Vector3D AxisUp = camera.WorldMatrix.Up;
			Vector3D AxisForward = camera.WorldMatrix.Forward;

			Vector3D billDir = Vector3D.Normalize (position);
			double dotProd = 1 - (Vector3D.Dot (position, AxisForward) + 1)/2;
			dotProd = RemapD (dotProd, -0.5, 1, 0.25, 1);
			dotProd = ClampedD (dotProd, 0.25, 1);

			var color = LINE_COLOR * 0.5f;
			if (flippit) {
				color = LINECOLOR_Comp * 0.5f;
			}
			color.W = 1;
			if (randoTime) {
				color *= Clamped (GetRandomFloat (), 0.25f, 1);
			}

			Vector4 cRed = new Vector4 (1, 0, 0, 1);
			Vector4 cYel = new Vector4 (1, 1, 0, 1);

			if (HP > 0.5) {
				HP -= 0.5;
				HP *= 2;
				color.X = LerpF (cYel.X, color.X, (float)HP);
				color.Y = LerpF (cYel.Y, color.Y, (float)HP);
				color.Z = LerpF (cYel.Z, color.Z, (float)HP);
				color.W = LerpF (cYel.W, color.W, (float)HP);
			} else {
				HP *= 2;
				color.X = LerpF (cRed.X, cYel.X, (float)HP);
				color.Y = LerpF (cRed.Y, cYel.Y, (float)HP);
				color.Z = LerpF (cRed.Z, cYel.Z, (float)HP);
				color.W = LerpF (cRed.W, cYel.W, (float)HP);
			}

			double thicc = HG_scaleFactor / (grid.WorldVolume.Radius / grid.GridSize);
			var size = (float)HG_Scale * 0.65f * (float)thicc;//*grid.GridSize;
			var material = MaterialSquare;

			double flipperAxis = 1;
			if (flippit) {
				flipperAxis = -1;
			}

			double gridThicc = grid.WorldVolume.Radius;
			Vector3D HG_Offset_tran = radarMatrix.Left*-radarRadius*flipperAxis + radarMatrix.Left*HG_Offset.X*flipperAxis + radarMatrix.Up*HG_Offset.Y + radarMatrix.Forward*HG_Offset.Z;

			if (randoTime) {
				Vector3D randOffset = new Vector3D ((GetRandomDouble () - 0.5) * 2, (GetRandomDouble () - 0.5) * 2, (GetRandomDouble () - 0.5) * 2);
				randOffset *= 0.333;
				position += position * randOffset;
			}
				
			if (flippit) {
				position = Vector3D.Transform(position, HG_scalingMatrixTarget);
			} else {
				position = Vector3D.Transform(position, HG_scalingMatrix);
			}

			position += worldRadarPos + HG_Offset_tran;
				
			double dis2Cam = Vector3.Distance (camera.Position, position);

			MyTransparentGeometry.AddBillboardOriented(
				material,
				color * (float)dotProd * (float)bootUpAlpha,
				position,
				AxisLeft, // Billboard orientation
				AxisUp, // Billboard orientation
				size,
				MyBillboard.BlendTypeEnum.AdditiveTop);

			if (GetRandomFloat () > 0.9f) {
				Vector3D holoCenter = radarMatrix.Left * -radarRadius * flipperAxis + radarMatrix.Left * HG_Offset.X * flipperAxis + radarMatrix.Forward * HG_Offset.Z;
				holoCenter += worldRadarPos;
				Vector3D holoDir = Vector3D.Normalize (position - holoCenter);
				double holoLength = Vector3D.Distance (holoCenter, position);

				DrawLineBillboard(MaterialSquare, color * 0.15f * (float)dotProd * (float)bootUpAlpha, holoCenter, holoDir, (float)holoLength, 0.0025f, BlendTypeEnum.AdditiveTop);
			}
		}


        private MatrixD CreateNormalizedLocalGridRotationMatrix()
        {
			// THIS IS THE NEW ONE I'm TESTING. 
            Sandbox.ModAPI.IMyCockpit cockpit = gHandler.localGridEntity as Sandbox.ModAPI.IMyCockpit;
            if (cockpit == null)
            {
                return MatrixD.Zero; // Return Zero matrix if no cockpit is found
            }
            VRage.Game.ModAPI.IMyCubeGrid grid = cockpit.CubeGrid;
            if (grid == null)
            {
                return MatrixD.Zero; // Return Zero matrix if no grid is found
            }

            Vector3D angularVelocity = gHandler.localGridVelocityAngular; // Gets the angular velocity of the entity the player is currently controlling
            MatrixD cockpitMatrix = cockpit.WorldMatrix; // Get cockpit orientation
            MatrixD gridMatrix = cockpit.CubeGrid.WorldMatrix; // Get grid orientation

            // Calculate the relative orientation of cockpit relative to grid
            MatrixD cockpitToGridMatrix = cockpitMatrix * MatrixD.Invert(gridMatrix);

            // Transform the angular velocity from the cockpit to the grid
            Vector3D gridAngularVelocity = Vector3D.Transform(angularVelocity, cockpitToGridMatrix);

            // Configuration values - adjust these to tune the effect
            const double maxAngularVelocity = 300.0; // Maximum angular velocity in your units
            const double maxTiltAngle = 89.0 * Math.PI / 180.0; // 89 degrees in radians
            const double sensitivityMultiplier = 1.0; // Adjust this to make the effect more or less pronounced

            // Calculate rotation angles based on angular velocity
            // Scale each component to the desired range and clamp to prevent flipping
            double rotationX = ClampAndScale(gridAngularVelocity.X, maxAngularVelocity, maxTiltAngle) * sensitivityMultiplier;
            double rotationY = ClampAndScale(gridAngularVelocity.Y, maxAngularVelocity, maxTiltAngle) * sensitivityMultiplier;
            double rotationZ = ClampAndScale(gridAngularVelocity.Z, maxAngularVelocity, maxTiltAngle) * sensitivityMultiplier;

            // Create rotation matrices in grid-local space (these are temporary tilts based on current velocity)
            MatrixD rotationMatrixX = MatrixD.CreateRotationX(-rotationX);
            MatrixD rotationMatrixY = MatrixD.CreateRotationY(-rotationY);
            MatrixD rotationMatrixZ = MatrixD.CreateRotationZ(-rotationZ);

            // Combine rotations (aerospace convention: Y-X-Z)
            MatrixD localTiltMatrix = rotationMatrixY * rotationMatrixX * rotationMatrixZ;

            // The base orientation should be the "behind view" - typically looking at the grid from behind
            // This creates a base rotation that shows the ship from behind (180° rotation around Y-axis)
            MatrixD baseOrientation = MatrixD.CreateRotationY(Math.PI);

            // Apply the temporary tilt to the base orientation
            MatrixD combinedLocalMatrix = localTiltMatrix * baseOrientation;

            // Transform the combined matrix to world space using the grid's orientation
            MatrixD gridRotationOnly = MatrixD.CreateFromQuaternion(QuaternionD.CreateFromRotationMatrix(gridMatrix));
            MatrixD worldRotationMatrix = combinedLocalMatrix * gridRotationOnly;

            return worldRotationMatrix;
        }

        private MatrixD CreateNormalizedLocalGridRotationMatrix() 
		{   

			Sandbox.ModAPI.IMyCockpit cockpit = gHandler.localGridEntity as Sandbox.ModAPI.IMyCockpit;
			if (cockpit == null)
			{
				return MatrixD.Zero; // Return Zero matrix if no cockpit is found
            }
            VRage.Game.ModAPI.IMyCubeGrid grid = cockpit.CubeGrid;
            if (grid == null)
            {
				return MatrixD.Zero; // Return Zero matrix if no grid is found
            }
			Vector3D angularVelocity = gHandler.localGridVelocityAngular; // Gets the angular velocity of the entity the player is currently controlling, which is a cockpit/seat etc.
            MatrixD cockpitMatrix = cockpit.WorldMatrix; // Get cockpit orientation
			MatrixD gridMatrix = cockpit.CubeGrid.WorldMatrix; // Get grid orientation

			// Clculate the relative orientation of cockpit relative to grid
			MatrixD cockpitToGridMatrix = cockpitMatrix * MatrixD.Invert(gridMatrix);

			// Transform the angular velocity frm the cockpit to the grid
			Vector3D gridAngularVelocity = Vector3D.Transform(angularVelocity, cockpitToGridMatrix);

            // Configuration values - adjust these to tune the effect
            const double maxAngularVelocity = 20; // Maximum angular velocity in your units
            const double maxTiltAngle = 89.0 * Math.PI / 180.0; // 89 degrees in radians
            const double sensitivityMultiplier = 1.0; // Adjust this to make the effect more or less pronounced

            // Calculate rotation angles based on angular velocity
            // Scale each component to the desired range and clamp to prevent flipping
            double rotationX = ClampAndScale(gridAngularVelocity.X, maxAngularVelocity, maxTiltAngle) * sensitivityMultiplier;
            double rotationY = ClampAndScale(gridAngularVelocity.Y, maxAngularVelocity, maxTiltAngle) * sensitivityMultiplier;
            double rotationZ = ClampAndScale(gridAngularVelocity.Z, maxAngularVelocity, maxTiltAngle) * sensitivityMultiplier;

            // Create rotation matrices
            MatrixD rotationMatrixX = MatrixD.CreateRotationX(-rotationX);
            MatrixD rotationMatrixY = MatrixD.CreateRotationY(-rotationY);
            MatrixD rotationMatrixZ = MatrixD.CreateRotationZ(-rotationZ);

            // Combine rotations (aerospace convention: Y-X-Z)
            MatrixD localRotationMatrix = rotationMatrixY * rotationMatrixX * rotationMatrixZ;

            // Transform the local rotation to world space using the grid's orientation
            // This ensures the hologram tilts relative to the grid's current orientation
            MatrixD gridRotationOnly = MatrixD.CreateFromQuaternion(QuaternionD.CreateFromRotationMatrix(gridMatrix));
            MatrixD worldRotationMatrix = localRotationMatrix * gridRotationOnly;

            return worldRotationMatrix;

            //         // Use transformed angular velocity to create a rotation matrix. This, in theory, should mean that regardless of which cardinal directon the cockpit
            //         // is facing on the grid the rotation represented on the hologram will be correct. No more nose pitching up = hologram swiveling left/right or vice versa
            //         Vector3D rotationAngle = gridAngularVelocity * deltaTime;
            //         MatrixD rotationX = MatrixD.CreateRotationX(-rotationAngle.X);// Was x
            //         MatrixD rotationY = MatrixD.CreateRotationY(-rotationAngle.Y); // Was y
            //         MatrixD rotationZ = MatrixD.CreateRotationZ(-rotationAngle.Z);

            //// I believe this is common in aerospace
            //         MatrixD rotationMatrix = rotationY * rotationX * rotationZ;

            //         return rotationMatrix;

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
            Vector3D rotationAngle = angularVelocity * deltaTime * amplificationFactor; // the * 10 here is likely a scaling factor? I'm honestly not sure? FenixPK replaced a magic number of "10" with a variable so this makes sense. 
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
				double balance_double = Convert2Credits (balance);
				//Echo($"You have ${balance_double} space credits.");

				creditBalance = balance_double;
				creditBalance_fake = LerpD (creditBalance_fake, creditBalance, deltaTime * 2);

                drawCreditBalance (creditBalance_fake, creditBalance);
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
			double cr_d = Convert.ToDouble (cr);

			return cr_d;
		}

		private void drawCreditBalance(double cr, double new_cr)
		{
			cr = Math.Round (cr);
			new_cr = Math.Round (new_cr);
			double cr_dif = new_cr - cr;

			Vector4 color = LINE_COLOR;
			double fontSize = 0.005;

			if (cr != new_cr) {
				color *= 2;
				fontSize = 0.006;
			}

			//string crs = "$"+Convert.ToString (cr);
			string crs = $"${FormatCredits(cr)}";
			Vector3D pos = worldRadarPos + (radarMatrix.Right*radarRadius*1.2) + (radarMatrix.Backward*radarRadius*0.9);
			Vector3D dir = Vector3D.Normalize((radarMatrix.Forward*4 + radarMatrix.Right)/5);
			drawText(crs, fontSize, pos, dir, color);

			if (cr_dif != 0) {
				if (!credit_counting) {
					PlayCustomSound (SP_MONEY, worldRadarPos);
					credit_counting = true;
				}


				if (cr_dif < 0) {
					if (cr_dif > creditBalance_dif) {
						cr_dif = creditBalance_dif;
					}
				} else if (cr_dif > 0) {
					if (cr_dif < creditBalance_dif) {
						cr_dif = creditBalance_dif;
					}
				}


				string cr_difs = Convert.ToString (cr_dif);
				double lengthDif = crs.Length - cr_difs.Length;
				if (lengthDif > 0) {
					for (int i = 1; i <= lengthDif; i++) {
						cr_difs = " " + cr_difs;
					}
				}
				pos += radarMatrix.Up * fontSize * 2;
				drawText (cr_difs, fontSize, pos, dir, LINECOLOR_Comp);

				creditBalance_dif = cr_dif;

			} else {
				creditBalance_dif = 0;
				credit_counting = false;
			}
		}
		//------------------------------------------------------------------------------------------------------------












		//== HUD TOOL BARS ==========================================================================================
		private double TB_activationTime = 0;
		private List<AmmoInfo> ammoInfos;

		private void UpdateToolbars()
		{
			DrawToolbars ();
		}

		private void DrawToolbars()
		{
			if (gHandler.localGridEntity == null) {
				return;
			}

			var cockpit = gHandler.localGridEntity as Sandbox.ModAPI.IMyCockpit;
			var grid = cockpit.CubeGrid;
			if (grid != null) {
			} else {
				return;
			}

			Vector3D pos = getToolbarPos ();
			//two arcs

			DrawCircle (pos, 0.01, radarMatrix.Forward, LINE_COLOR, false, 0.25f, 0.001f); // Center Dot

			ammoInfos = GetAmmoInfos(grid);

			DrawToolbarBack (pos);			// Left
			DrawToolbarBack (pos, true);	// Right

			TB_activationTime += deltaTime*0.1;
			TB_activationTime = ClampedD (TB_activationTime, 0, 1);
			TB_activationTime = Math.Pow (TB_activationTime, 0.9);
		}

		public void DrawToolbarBack(Vector3D position, bool flippit = false)
		{
			Vector3D radarUp = radarMatrix.Up;
			Vector3D radarDown = radarMatrix.Down;

			Vector3D normal = radarMatrix.Forward;

			Vector4 color = LINE_COLOR;


			double TB_bootUp = LerpD (12, 4, TB_activationTime);


			double scale = 1.75;
			double height = 0.1024 * scale;
			double width = 0.0256 * scale;

			if (flippit) {
				position += (radarMatrix.Right * radarRadius * 2) + (radarMatrix.Right * width * TB_bootUp);
			} else {
				position += (radarMatrix.Left * radarRadius * 2) + (radarMatrix.Left * width * TB_bootUp);
			}

			// Ensure the normal is normalized
			normal.Normalize();

			// Calculate perpendicular vectors to form the quad
			Vector3D up = radarUp; //Vector3D.CalculatePerpendicularVector(normal);

			Vector3D left = Vector3D.Cross(up, normal);
			up.Normalize();
			left.Normalize();

			if (GetRandomBoolean()) {
				if (glitchAmount > 0.001) {
					float glitchValue = (float)glitchAmount;

					Vector3D offsetRan = new Vector3D (
						(GetRandomDouble () - 0.5) * 2,
						(GetRandomDouble () - 0.5) * 2,
						(GetRandomDouble () - 0.5) * 2
					);
					double dis2Cam = Vector3D.Distance (MyAPIGateway.Session.Camera.Position, position);

					position += offsetRan * dis2Cam * glitchValue * 0.025;
					color *= GetRandomFloat ();
				}
			}

			// Calculate the four corners of the quad
			Vector3D topLeft = position + left * width + up * height;
			Vector3D topRight = position - left * width + up * height;
			Vector3D bottomLeft = position + left * width - up * height;
			Vector3D bottomRight = position - left * width - up * height;

			if (flippit) {
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

			for (int am = 0; am < ammoInfos.Count && am < 7; am++) {
				DrawActionSlot (am, ammoInfos [am].AmmoType, Convert.ToString(ammoInfos [am].AmmoCount), position, height, flippit);
			}

			//-------------------COCKPIT----------------------------------
			string cockPitName;
			float cockPitCur;
			float cockPitMax;
			GetCockpitInfo (out cockPitName, out cockPitCur, out cockPitMax);

			DrawActionSlot (7, cockPitName +" Integrity", Convert.ToString(cockPitCur) + " / " + Convert.ToString(cockPitMax), position, height, flippit);
			//--------------------COCKPIT----------------------------------



		}

		private void DrawAction(string name, Vector3D pos, bool flippit = false)
		{
			double fontSize = 0.0075;

			if (!flippit) {
				pos += (radarMatrix.Left * fontSize * 1.8 * name.Length) - (radarMatrix.Left * fontSize * 1.8);
			}
			//DrawCircle (pos, 0.005, radarMatrix.Forward, LINECOLOR_Comp, false, 1f, 0.001f);
			drawText (name, fontSize, pos, radarMatrix.Forward, LINE_COLOR, 1f);
		}

		private Vector3D getToolbarPos()
		{
			Vector3D pos = Vector3D.Zero;

			Vector3D cameraPos = MyAPIGateway.Session.Camera.Position;

			double elevation = GetHeadElevation (worldRadarPos, radarMatrix);

			pos = worldRadarPos + (radarRadius * 1.5 * radarMatrix.Forward) + (elevation * radarMatrix.Up) +  (radarMatrix.Up * 0.05);

			return pos;
		}

		private double GetHeadElevation(Vector3D referencePosition, MatrixD referenceMatrix)
		{
			// Get the player's character entity
			IMyCharacter character = MyAPIGateway.Session?.Player?.Character;

			if (character == null) {
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

			slot = (int)MathHelper.Clamp ((double)slot, 0, 7);

			if(slot <= 3 && flippit){
				return;
			}

			if(slot >= 4 && !flippit){
				return;
			}

			if (flippit) {
				slot -= 4;
			}

			int actionCount = 4;
			int actionStart = 0;

			slot = (int)MathHelper.Clamp ((double)slot, 0, 7);

			if (flippit) {
				actionStart = 4;
			}

			Vector3D actionDirection = radarMatrix.Left;
			if (flippit) {
				actionDirection = radarMatrix.Right;
			}

			Vector3D aPos = position + (radarMatrix.Up * (height / 2)) - (radarMatrix.Up * ((height*2) / actionCount) * slot) + (radarMatrix.Up * ((height*2) / 8)) - (radarMatrix.Up * ((height*2) / 12));


			double bootOffset = -((height*2) / actionCount) * (actionCount-slot);
			double TB_bootUp2 = LerpD (bootOffset, 0, TB_activationTime);
			aPos += radarMatrix.Up * TB_bootUp2;

			if (slot == 0) {
				aPos += (actionDirection * ((height * 2) / 24));
			}else if(slot == 1){
				aPos += (actionDirection * ((height * 2) / 24))*2;
			}else if(slot == 2){
				aPos += (actionDirection * ((height * 2) / 24))*1.75;
			}

			string theAction = value;
			DrawAction (theAction, aPos, flippit);

			if (!flippit) {
				aPos += (radarMatrix.Left * fontSize * 1.8 * name.Length) - (radarMatrix.Left * fontSize * 1.8);
			}
			drawText (Convert.ToString (name), 0.005, aPos + (radarMatrix.Up * 0.005 * 3), radarMatrix.Forward, LINE_COLOR, 0.75f);
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

			if (controlledEntity == null) {
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

		private List<AmmoInfo> GetAmmoInfos(VRage.Game.ModAPI.IMyCubeGrid grid)
		{
			Dictionary<string, int> ammoCounts = new Dictionary<string, int>();
			HashSet<string> ammoTypes = new HashSet<string>();

			List<VRage.Game.ModAPI.IMySlimBlock> slimBlocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
			grid.GetBlocks(slimBlocks, block => block.FatBlock is Sandbox.ModAPI.IMyUserControllableGun || block.FatBlock is Sandbox.ModAPI.IMyLargeTurretBase);

			foreach (var slimBlock in slimBlocks)
			{
				Sandbox.ModAPI.IMyUserControllableGun weapon = slimBlock.FatBlock as Sandbox.ModAPI.IMyUserControllableGun;
				if (weapon != null)
				{
					var def = (MyWeaponBlockDefinition)slimBlock.BlockDefinition;
					var weaponDef = MyDefinitionManager.Static.GetWeaponDefinition(def.WeaponDefinitionId);

					if (weaponDef != null)
					{
						MyDefinitionId ammoTypeId = weaponDef.AmmoMagazinesId [0];

						MyAmmoMagazineDefinition ammoData = MyDefinitionManager.Static.GetAmmoMagazineDefinition(ammoTypeId);
						MyAmmoMagazineDefinition ammoDefinition = ammoData;

						if (ammoDefinition != null) {
							string ammoType = ammoDefinition.Id.SubtypeName;
							if (!ammoTypes.Contains (ammoType)) {
								ammoTypes.Add (ammoType);
								int count = GetAmmoCount (grid, ammoType);
								ammoCounts [ammoType] = count;
							}
						}
						
					}
				}
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