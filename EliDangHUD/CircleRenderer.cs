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
using System.Text;
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
using VRage.Input;
using VRageMath;

using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

//---
//Hey, I made this mod because I wanted to learn a bit about the VRage API and make some fun tweaks to SE.
//Thank you to all the people who have shared their own mods over the years that I may follow the pathes you forged.
//Please forgive my foolish and ignorant choices in this script. I had fun!
//
//																					---CHERUB 04-18-2024
//---
using Sandbox.ModAPI.Ingame;
using VRageRender;
using System.Diagnostics;
using Sandbox.Game.Lights;
using VRage.Library.Utils;
using Sandbox.Game.World;
using VRage.Game.Entity;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Game.Screens.Helpers;
using VRage.Game.ModAPI.Ingame;
using VRage.ModAPI;
using System.Text.RegularExpressions;



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
		public bool enableGridFlares = true;
		public bool enableVisor = true;
		public double radarRange = -1;
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
			public static float LINETHICKNESS = 		1.5f;
			public static Vector4 LINECOLOR = 			new Vector4(1f, 0.5f, 0.0f, 1f);
			public static Vector3 LINECOLOR_RGB = 		new Vector3(1f, 0.5f, 0.0);
			public static int LINEDETAIL = 				90;
			public static Vector3D STARPOS = 			new Vector3D(0, 0, 0);
			public static bool STARFOLLOWSKY = 			true;
			public static bool ENABLECOCKPITDUST = 		true;
			public static bool ENABLEGRIDFLARES = 		true;
			public static bool ENABLEVISOR =	 		true;

			public string configDataFile = 				"dangConfig.xml";
			public ConfigData configData = 				null;

			public static float GLOW = 					1f;

			public static double RIPPYRANGE = 			-1;
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
			public double radarRange = -1;								// Radar Range (-1 for draw distance)
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
				MyAPIGateway.Multiplayer.RegisterMessageHandler(RequestMessageId, OnSettingsRequestReceived);
			}

			MyAPIGateway.Multiplayer.RegisterMessageHandler(MessageId, OnSyncSettingsReceived);
			MyAPIGateway.Multiplayer.RegisterMessageHandler(RequestMessageId, OnSettingsRequestReceived);

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
				MyAPIGateway.Multiplayer.UnregisterMessageHandler(RequestMessageId, OnSettingsRequestReceived);
			}

			MyAPIGateway.Multiplayer.UnregisterMessageHandler(MessageId, OnSyncSettingsReceived);
		}

		private void RequestSettingsFromServer()
		{
			MyAPIGateway.Multiplayer.SendMessageToServer(RequestMessageId, new byte[0]);
		}

		private void OnSettingsRequestReceived(byte[] data)
		{
			if (MyAPIGateway.Session.IsServer)
			{
				var senderId = MyAPIGateway.Multiplayer.MyId; // Get sender ID
				SyncSettingsWithClient(senderId, theSettings);
			}
		}

		public void SyncSettingsWithClient(ulong clientId, ModSettings settings)
		{
			string data = SerializeSettings(settings);
			var msg = MyAPIGateway.Utilities.SerializeToBinary(data);
			MyAPIGateway.Multiplayer.SendMessageTo(MessageId, msg, clientId);
		}

		private void OnSyncSettingsReceived(byte[] data)
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
			CircleRenderer.LINETHICKNESS	= 		settings.lineThickness;
			CircleRenderer.LINEDETAIL		= 		settings.lineDetail;
			CircleRenderer.STARPOS			= 		settings.starPos;
			CircleRenderer.STARFOLLOWSKY	= 		settings.starFollowSky;
			CircleRenderer.RIPPYRANGE 		= 		settings.radarRange;
		}

		private ModSettings GatherCurrentSettings()
		{
			// Gather current settings from your mod's state
			ModSettings AllTheSettings = new ModSettings();

			AllTheSettings.lineThickness 	= 	CircleRenderer.LINETHICKNESS;
			AllTheSettings.lineDetail 		= 	CircleRenderer.LINEDETAIL;
			AllTheSettings.starPos 			= 	CircleRenderer.STARPOS;
			AllTheSettings.starFollowSky 	= 	CircleRenderer.STARFOLLOWSKY;
			AllTheSettings.radarRange 		= 	CircleRenderer.RIPPYRANGE;

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
					return DeserializeSettings(data);
				}
			}
			return new ModSettings(); // Return default settings if no file exists
		}
		//=============================================================================================================================
















		private void FontTestRender(){
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

			List<string> parsedText = StringToList (text);
			for (int i = 0; i < parsedText.Count; i++) {
				Vector3D offset = -left * (size*i*1.8);

				if (parsedText [i] != " ") {
					DrawQuadRigid (pos + offset, dir, size, getFontMaterial (parsedText [i]), color * GLOW * dim, flipUp);
				}
			}

		}

		private void drawPower(){
			Vector4 color = LINECOLOR;

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

			drawText (PLS, size, pos, dir, LINECOLOR);
			drawText ("0000 ", size, pos, dir, LINECOLOR, 0.333f);

			//----ARC----
			float arcLength = (float)(gHandler.localGridSpeed/maxSpeed);
			arcLength = Clamped (arcLength, 0, 1);
			arcLength *= 70;
			DrawArc(worldRadarPos-radarMatrix.Up*0.0025, radarRadius*1.27, radarMatrix.Up, 360-30-arcLength, 360-30, LINECOLOR, 0.007f, 0.5f);

			double sizePow = 0.0045;
			double powerSeconds = (double)gHandler.H2powerSeconds;
			string powerSecondsS = "@" + FormatSecondsToReadableTime (powerSeconds);
			Vector3D powerPos = worldRadarPos + radarMatrix.Right * radarRadius * 0.88 + radarMatrix.Backward * radarRadius * 1.1 + radarMatrix.Down * sizePow * 2 + radarMatrix.Left *powerSecondsS.Length * sizePow * 1.8;
			drawText (powerSecondsS, sizePow, powerPos, radarMatrix.Forward, LINECOLOR_Comp, 1);

			float powerPer = 56f*gHandler.H2Ratio;
			DrawArc(worldRadarPos-radarMatrix.Up*0.0025, radarRadius*1.27 + 0.01, radarMatrix.Up, 360-37-powerPer, 360-37, LINECOLOR_Comp, 0.002f, 0.75f);
		}

		private void populateFonts(){
			int num = 46;

			for (int y = 0; y < num; y++) {
				string name = "ED_FONT_" + Convert.ToString (y);
				MaterialFont.Add(name);
			}
		}

		private MyStringId getFontMaterial(string S){
			MyStringId mat;
			switch (S){
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

			LINECOLOR_Comp_RPG = secondaryColor (LINECOLOR_RGB)*2 + new Vector3(0.01f,0.01f,0.01f);
			LINECOLOR_Comp = new Vector4 (LINECOLOR_Comp_RPG, 1f);



			if (STARFOLLOWSKY) {
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
						CircleRenderer.LINETHICKNESS	= 		configgy.lineThickness;
						CircleRenderer.LINEDETAIL		= 		configgy.lineDetail;
						CircleRenderer.STARPOS			= 		configgy.starPos;
						CircleRenderer.STARFOLLOWSKY	= 		configgy.starFollowSky;
						CircleRenderer.ENABLECOCKPITDUST = 		configgy.enableCockpitDust;
						CircleRenderer.ENABLEGRIDFLARES = 		configgy.enableGridFlares;
						CircleRenderer.ENABLEVISOR = 			configgy.enableVisor;
						CircleRenderer.RIPPYRANGE = 			configgy.radarRange;
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

			if(theSettings != null){
				SaveSettings (theSettings);
			}

			if(configData==null)
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

			if (!findEntitiesOnce) {
				// Initialize the planet manager
				planetList = GetPlanets ();
				planetListDetails = GatherPlanetInfo(planetList);

				// Initialize Entity List for Radar
				FindEntities ();
				findEntitiesOnce = true;
			}

			if (gHandler != null) {
				gHandler.UpdateGrid ();
				populateRandoms ();

				gHandler.UpdateDamageCheck ();
				double damageAmount = gHandler.getDamageAmount ();
				glitchAmount_overload += damageAmount;

			}

			if (deltaTimer != null) {
				if (!deltaTimer.IsRunning) {
					deltaTimer.Start ();
				}
				deltaTime = deltaTimer.Elapsed.TotalSeconds;
				deltaTimer.Restart ();
			}

		}

		private bool InitializedAudio = false;

		//------------ A F T E R ------------------
		public override void UpdateAfterSimulation()
		{
			if(!EnableMASTER){
				return;
			}

			if (!InitializedAudio) {
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

			if (!cockpit.IsMainCockpit) {
				
				Echo ("Set to Main Cockpit to enable Scanner.");

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

		//------------ D R A W -----------------
		public override void Draw()
		{

			base.Draw();

			if(!EnableMASTER){
				if (MyAPIGateway.Gui.IsCursorVisible) {
					CheckCustomData (); //We don't need to keep check the colors unless we have the thing open.
				}
				return;
			}

			//Flares ------------------------------------------------------------------------------------------------------
			DrawShipFlare ();
			//-------------------------------------------------------------------------------------------------------------

			var playa = MyAPIGateway.Session?.LocalHumanPlayer;
			if (playa == null)
			{
				return;
			}

			var controlledEntity = playa.Controller?.ControlledEntity;
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

			var remoteControl = controlledEntity.Entity as Sandbox.ModAPI.IMyRemoteControl;
			if (remoteControl != null && remoteControl.Pilot != null)
			{
				return;
			}

			//visor -------------------------------------------------------------------------------------------------------
			UpdateVisor();
			//-------------------------------------------------------------------------------------------------------------

			//Is the player in a vehichle? Fade or show the orbit lines!
			bool IsPlayerControlling = gHandler != null && gHandler.IsPlayerControlling;
			bool GridHasAntenna = false;

			if (IsPlayerControlling) {
				if (gHandler == null || gHandler.localGridEntity == null)
					return;

				var cockpit = gHandler.localGridEntity as Sandbox.ModAPI.IMyCockpit;
				if (cockpit == null || cockpit.CubeGrid == null || !cockpit.IsMainCockpit)
					return;

				VRage.Game.ModAPI.IMyCubeGrid grid = cockpit.CubeGrid;

				GridHasAntenna = gHandler.GridHasAntenna(grid);
				if (!GridHasAntenna)
					return;

				powerLoad = gHandler.GetGridPowerUsagePercentage(grid);


				glitchAmount_min = MathHelper.Clamp(gHandler.GetGridPowerUsagePercentage(cockpit.CubeGrid), 0.85, 1.0)-0.85;
				glitchAmount_overload = MathHelper.Lerp (glitchAmount_overload, 0, deltaTime * 2);
				glitchAmount = MathHelper.Clamp (glitchAmount_overload, glitchAmount_min, 1);
				if (glitchAmount_overload < 0.01) {
					glitchAmount_overload = 0;
				}

				if (!cockpit.IsMainCockpit) {
					return;
				}
				grid = cockpit.CubeGrid;
				GridHasAntenna = gHandler.GridHasAntenna (cockpit.CubeGrid);
			} else {
				return;
			}

			if (!GridHasAntenna) {
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
				if (STARFOLLOWSKY) {
					MyOrientation sunOrientation = getSunOrientation ();

					Quaternion sunRotation = sunOrientation.ToQuaternion ();
					Vector3D sunForwardDirection = Vector3D.Transform (Vector3D.Forward, sunRotation);

					STARPOS = sunForwardDirection * 100000000;
				}
				//Draw orbit lines
				foreach (var i in planetListDetails) {
					// Cast the entity to IMyPlanet
					MyPlanet planet = (MyPlanet)i.Entity;
					Vector3D parentPos = (i.ParentEntity != null) ? i.ParentEntity.GetPosition () : STARPOS;

					DrawPlanetOutline (planet);
					DrawPlanetOrbit (planet, parentPos);
				}

				//DEBUG
				//DrawWorldUpAxis (STARPOS);

			}

			CheckPlayerInput();

			DrawRadar ();

			double dis2camera = Vector3D.Distance (MyAPIGateway.Session.Camera.Position, worldRadarPos);
			if (dis2camera > 2) {
				return;
			}

			//if dust ----------------------------------------------------------------------------------
			if (EnableDust) {
				updateDust ();
			}
			//------------------------------------------------------------------------------------------

			if (EnableGauges) {
				FontTestRender ();
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
			if (MyAPIGateway.Session.CameraController.IsInFirstPersonView) {

				MatrixD cameraMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
				Vector3D camUp = cameraMatrix.Up;
				Vector3D camLeft = cameraMatrix.Left;
				Vector3D camForward = cameraMatrix.Forward;
				Vector3D camPos = MyAPIGateway.Session.Camera.Position;
				camPos += camForward * MyAPIGateway.Session.Camera.NearPlaneDistance * 1.001;


				float fov = (float)GetCameraFOV ();
				float scale = 50 / fov;

				visorDown = IsPlayerHelmetOn ();

				if (visorDown) {
					visorLife -= deltaTime * 1.5;
				} else {
					visorLife += deltaTime * 1.5;
				}

				visorLife = ClampedD (visorLife, 0, 1);

				Vector3D visorVert = (LerpD (-0.75, 0.75, visorLife)) * camUp;

//			if (MyAPIGateway.Input.IsNewGameControlPressed (MyControlsSpace.HELMET)) {
//				if (visorUp) {
//					visorUp = false;
//				} else {
//					visorUp = true;
//				}
//			}

				camPos += visorVert;
				if (visorLife > 0 && visorLife < 1) {
					MyTransparentGeometry.AddBillboardOriented (MaterialVisor, new Vector4 (1, 1, 1, 1), camPos, camLeft, camUp, scale);
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
			radarRange_CurrentLogin = 0.001;
				
			//Check for CustomData variables in cockpit and register.
			CheckCustomData();
			glitchAmount_overload = 0.25;

			PlayCustomSound (SP_BOOTUP, worldRadarPos);
			//PlayCustomSound (SP_ZOOMINIT, worldRadarPos);

			HG_Initialize ();

		}

		private void OnStandUp(){
			isSeated = false;
			//MyAPIGateway.Utilities.ShowMessage("NOTICE", "Leaving Cockpit.");
			//Trigger animations and sounds for console.
			HG_initialized = false;
			HG_initializedTarget = false;
			YourBlocks = new List<BlockTracker>();
			TheirBlocks = new List<BlockTracker>();
			YourDrives = new List<BlockTracker>();
			TheirDrives = new List<BlockTracker>();
			TB_activationTime = 0;
			HG_activationTime = 0;
			HG_activationTimeTarget = 0;
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
			EnableHolograms = ini.Get(mySection, "ScannerHolo").ToBoolean(true);
			EnableHolograms_them = ini.Get(mySection, "ScannerHoloThem").ToBoolean(true);
			EnableHolograms_you = ini.Get(mySection, "ScannerHoloYou").ToBoolean(true);

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
			string colorString = ini.Get(mySection, "ScannerColor").ToString(Convert.ToString(LINECOLOR_RGB));
			Color radarColor = ParseColor(colorString);
			LINECOLOR = (radarColor).ToVector4() * GLOW;

			// Velocity Toggle
			ShowVelocityLines = ini.Get(mySection, "ScannerLines").ToBoolean(true);

			// Orbit Line Speed Threshold
			SpeedThreshold = ini.Get(mySection, "ScannerOrbits").ToSingle(500);

			LINECOLOR_RGB = new Vector3(LINECOLOR.X, LINECOLOR.Y, LINECOLOR.Z);
			LINECOLOR_Comp_RPG = secondaryColor(LINECOLOR_RGB) * 2 + new Vector3(0.01f, 0.01f, 0.01f);
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
				return LINECOLOR_RGB*GLOW;  // Default color if no data

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
			return LINECOLOR_RGB*GLOW;  // Default color on parse failure
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
			MyAPIGateway.Entities.GetEntities(entities);

			// Iterate through the entities and filter out planets
			foreach (var entity in entities)
			{
				// Check if the entity is a planet
				if (entity is MyPlanet)
				{			// Add the planet to the set
					planets.Add(entity);
				}
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
		void DrawShipFlare(){
			MatrixD cameraMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
			Vector3 viewUp = cameraMatrix.Up;
			Vector3 viewLeft = cameraMatrix.Left;
			foreach (VRage.ModAPI.IMyEntity entity in radarEntities) {
				if (entity is VRage.Game.ModAPI.IMyCubeGrid) {
					double dis2Cam = Vector3D.Distance (MyAPIGateway.Session.Camera.Position, entity.GetPosition ());
					if (dis2Cam >= 100) {
						double radius = entity.WorldVolume.Radius;
						radius = RemapD (radius, 10, 1000, 0.5, 10);

						float scale = (((GetRandomFloat () - 0.5f) * 0.01f) + 0.1f) * (float)dis2Cam;
						double scaleF = RemapD (dis2Cam, 10000, 20000, 1, 0);
						scaleF = ClampedD (scaleF, 0, 1);

						Vector4 color = new Vector4 (1, 1, 1, 1);
						color.W += ((GetRandomFloat () - 1f) * 0.1f);

						double fadder = RemapD (dis2Cam, 100, 2000, 0, 1);
						fadder = ClampedD (fadder, 0, 1);

						MyTransparentGeometry.AddBillboardOriented (MaterialShipFlare, color * 1f * (float)fadder, entity.GetPosition (), viewLeft, viewUp, scale * (float)scaleF * (float)radius, MyBillboard.BlendTypeEnum.AdditiveBottom);
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
			int segments = LINEDETAIL;                 // Number of segments to approximate the circle
			bool dotFlipper = true;

			//Lets adjust the tesselation based on radius instead of an arbitrary value.
			int segmentsInterval = (int)Math.Round(Remap((float)orbitRadius,1000, 10000000, 1, 8));
			segments = segments * (int)Clamped((float)segmentsInterval, 1, 16);

			float lineLength = 1.01f;           // Length of each line segment
			float lineThickness = LINETHICKNESS;        // Thickness of the lines

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
			DrawCircle(planetPosition, (float)planetRadius, aimDirection, LINECOLOR, true);
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
			DrawCircle(parentPosition, (float)orbitRadius, orbitDirection, LINECOLOR);
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

			float lineThickness = LINETHICKNESS;        // Thickness of the lines

			//Lets adjust the line thickness based on screen resolution so that it doesn't get to pixelated.
			lineThickness = ((float)GetScreenHeight()/1080f)*lineThickness;

			// Calculate camera distance from whole segment.
			float distanceToSegment = DistanceToLineSegment(cameraPosition, start, end);

			// Calculate the segment thickness based on distance from camera.
			float segmentThickness = Math.Max(Remap(distanceToSegment, 0f, 1000000f, 0f, 1000f) * lineThickness, 0f);

			// Calculate the segment brightness based on distance from camera.
			float dimmer = Clamped(Remap(distanceToSegment, 0, 10000000f, 1f, 0f), 0f, 1f)*7f;

			if (dimmer > 0.01f) {
				DrawLineBillboard (Material, LINECOLOR * GLOW * dimmer, start, Vector3D.Normalize (end - start), segmentLength, segmentThickness, blendType);
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
			lineThickness = LINETHICKNESS;        // Thickness of the lines

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

				lineThickness = LINETHICKNESS;        // Thickness of the lines
				//Lets adjust the line thickness based on screen resolution so that it doesn't get to pixelated.
				lineThickness = ((float)GetScreenHeight()/1080f)*lineThickness;
				// Calculate camera distance from whole segment.
				distanceToSegment = (float)Vector3D.Distance(cameraPosition, leftBase + segmentPosition - direction * totalLineLength / 2);
				// Calculate the segment thickness based on distance from camera.
				segmentThickness = Math.Max(Remap(distanceToSegment, 0f, 1000000f, 0f, 1000f) * lineThickness, 0f);

				if (dimmer > 0.01f) {
					zeroBase = (leftBase + segmentPosition - direction * totalLineLength / 2);
					zeroBase = (segmentLength * 0.5 * -segmentUp) + zeroBase;
					DrawLineBillboard (Material, LINECOLOR*GLOW * dimmer, zeroBase, segmentUp, segmentLength, segmentThickness);

					zeroBase = (rightBase + segmentPosition - direction * totalLineLength / 2);
					zeroBase = (segmentLength * 0.5 * -segmentUp) + zeroBase;
					DrawLineBillboard (Material, LINECOLOR*GLOW * dimmer, zeroBase, segmentUp, segmentLength, segmentThickness);
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





		public double radarRange = 5000;
		public double radarRange_Goal = 5000;
		public double radarRange_Current = 5000;

		public double radarRange_GoalLogin = 1;
		public double radarRange_CurrentLogin = 0.01;

		public float radarRadius = 0.125f;
		public double radarScale = 0.000025;
		public Vector3D radarOffset = new Vector3D(0, -0.20, -0.575);
		private bool targetingFlipper = false; 
		private bool underAttack = false;
		private MatrixD radarMatrix;

		private Vector3D worldRadarPos;
		private double targettingLerp = 1.5;
		private double targettingLerp_goal = 1.5;
		private double alignLerp = 1.0;
		private double alignLerp_goal = 1.0;

		private double squishValue = 0.75;
		private double squishValue_Goal = 0.75;


		//===================================================================================
		//Radar==============================================================================
		//===================================================================================
		private void DrawRadar(){

			//FindEntities ();
			if (RIPPYRANGE == -1) {
				radarRange = (double)MyAPIGateway.Session.SessionSettings.ViewDistance;
			} else {
				radarRange = RIPPYRANGE;
			}

			if (underAttack) {
				radarRange_Goal = radarRange * 0.25;
				squishValue_Goal = 0.0;
			} else {
				radarRange_Goal = radarRange;
				squishValue_Goal = 0.75;
			}
			squishValue = LerpD(squishValue, squishValue_Goal, 0.1);
			radarRange_CurrentLogin = LerpD(radarRange_CurrentLogin, radarRange_GoalLogin, 0.01);

			radarRange_Current = LerpD(radarRange_Current, radarRange_Goal, 0.1);
			radarRange = radarRange_Current*radarRange_CurrentLogin;

			radarScale = (radarRadius / radarRange);

			underAttack = false;

			if (gHandler == null) {
				return;
			}

			bool IsPlayerControlling = gHandler.IsPlayerControlling;
			if (!IsPlayerControlling) {
				return;
			}

			//===Icon Colors===
			Vector4 color_GridFriend =		(Color.Green).ToVector4()*1;		//IMyCubeGrid
			Vector4 color_GridEnemy =		(Color.Red).ToVector4()*1;		//   "
			Vector4 color_GridEnemyAttack =	(Color.Pink).ToVector4()*4;		//   "
			Vector4 color_GridNeutral =		(Color.White).ToVector4()*1;		//   "
			Vector4 color_FloatingObject =	(Color.DarkGray).ToVector4();	//IMyFloatingObject
			Vector4 color_VoxelBase =		(Color.DimGray).ToVector4();		//IMyVoxelBase
			//-----------------
			Vector4 color_Current = color_VoxelBase;
			float scale_Current = 1;

			if (stopwatch == null) {
				stopwatch = new Stopwatch ();
				stopwatch.Start ();
			}

			if (!stopwatch.IsRunning) {
				stopwatch.Start ();
			}

			double radarTimer = stopwatch.Elapsed.TotalSeconds / 3;
			radarTimer = 1 - (radarTimer - Math.Truncate (radarTimer))*2;

			double attackTimer = stopwatch.Elapsed.TotalSeconds*4;
			attackTimer = (attackTimer - Math.Truncate (attackTimer));
			if (attackTimer > 0.5) {
				targetingFlipper = true;
			} else {
				targetingFlipper = false;
			}


			if (gHandler.localGridEntity == null) {
				return;
			}

			// Fetch the ship's position and its world matrix
			Vector3D shipPos = gHandler.localGridEntity.GetPosition();
			radarMatrix = gHandler.localGridEntity.WorldMatrix;



			//Head Location=================================================
			// Get the player's character entity
			Vector3D headPosition = Vector3D.Zero;
			IMyCharacter character = MyAPIGateway.Session.Player.Character;

			if (character != null) {
				// Extract the head matrix
				MatrixD headMatrix = character.GetHeadMatrix (true);

				// Get the head position from the head matrix
				headPosition = headMatrix.Translation;
				headPosition -= shipPos;
			}
			//==============================================================



			// Apply the radar offset to the ship's position
			Vector3D radarPos = Vector3D.Transform(radarOffset, radarMatrix) + headPosition;

			// Draw the radar circle
			Vector3D radarUp = radarMatrix.Up;
			Vector3D radarDown = radarMatrix.Down;
			Vector3D radarLeft = radarMatrix.Left;
			Vector3D radarForward = radarMatrix.Forward;
			Vector3D radarBackward = radarMatrix.Backward;

			//====Draw Rings====
			//DrawCircle(radarPos, radarRadius, radarUp, false, 0.75f, 0.001f); //Border Ring
			DrawQuad(radarPos, radarUp, (double)radarRadius*0.03f, MaterialCircle, LINECOLOR*5f); //Center
			//DrawCircle(radarPos-(radarUp*0.006), radarRadius*1.035, radarUp, false, 0.25f, 0.0025f); //outer decorative ring

			worldRadarPos = radarPos;

			double dis2camera = Vector3D.Distance (MyAPIGateway.Session.Camera.Position, worldRadarPos);
			if (dis2camera > 2) {
				return;
			}

			float radarFov = Clamped(GetCameraFOV ()/2, 0, 90)/90;
			DrawLineBillboard (Material, LINECOLOR*GLOW*0.4f, radarPos, Vector3D.Lerp(radarMatrix.Forward,radarMatrix.Left,radarFov), radarRadius*1.45f, 0.0005f); //Perspective Lines
			DrawLineBillboard (Material, LINECOLOR*GLOW*0.4f, radarPos, Vector3D.Lerp(radarMatrix.Forward,radarMatrix.Right,radarFov), radarRadius*1.45f, 0.0005f);

			for (int i = 1; i < 11; i++) {
				int radarPulseTime = (int)Math.Truncate (10 * radarTimer);
				float radarPulse = 1;
				if (radarPulseTime == i) {
					radarPulse = 2;
				}
				DrawCircle(radarPos-(radarUp*0.003), (radarRadius*0.95)-(radarRadius*0.95)*(Math.Pow((float)i/10, 4)), radarUp, LINECOLOR, false, 0.25f*radarPulse, 0.00035f);
			}

			//DrawQuad(radarPos, radarUp, (double)radarRadius*1.18, MaterialBorder, LINECOLOR); //Border
			DrawQuadRigid(radarPos, radarUp, (double)radarRadius*1.18, MaterialBorder, LINECOLOR, true); //Border
			if(EnableGauges){
				DrawQuadRigid(radarPos-(radarUp*0.005), -radarUp, (double)radarRadius*1.5, MaterialCompass, LINECOLOR, true); //Compass
			}
			DrawQuad(radarPos-(radarUp*0.010), radarUp, (double)radarRadius*2.25, MaterialCircleSeeThrough, new Vector4(0, 0, 0, 0.75f)); //Dim Backing
			//DrawQuad(radarPos-(radarUp*0.011), radarUp, (double)radarRadius*3, MaterialCircleSeeThrough, new Vector4(0, 0, 0, 1f)); //Dim Backing
			//----End Draw Ring----



			// Get the camera's up and left vectors
			MatrixD cameraMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
			Vector3 viewUp = cameraMatrix.Up;
			Vector3 viewLeft = cameraMatrix.Left;
			Vector3 viewForward = cameraMatrix.Forward;
			Vector3 viewBackward = cameraMatrix.Backward;
			Vector3D viewBackwardD = cameraMatrix.Backward;

			DrawQuad (radarPos + (radarUp*0.025), viewBackward, 0.25, MaterialCircleSeeThroughAdd, LINECOLOR*0.075f, true);

			//===============HOLOGRAMS=================
			if (EnableHolograms) {
				Vector3D hgPos = HG_Offset;
				Vector3D hgPos_Right = radarPos + radarMatrix.Left*-radarRadius + radarMatrix.Left*HG_Offset.X + radarMatrix.Forward*HG_Offset.Z;
				Vector3D hgPos_Left = radarPos + radarMatrix.Left*-radarRadius*-1 + radarMatrix.Left*HG_Offset.X*-1 + radarMatrix.Forward*HG_Offset.Z;

				if (EnableHolograms_you) {
					double lockInTime_Right = HG_activationTime;
					lockInTime_Right = ClampedD (lockInTime_Right, 0, 1);
					lockInTime_Right = Math.Pow (lockInTime_Right, 0.1);

					DrawQuad (hgPos_Right + (radarUp*HG_Offset.Y * 0.5), viewBackward, 0.15, MaterialCircleSeeThroughAdd, LINECOLOR*0.125f, true);

					DrawCircle (hgPos_Right, 0.04 * lockInTime_Right, radarUp, LINECOLOR, false, 0.5f, 0.00075f);
					DrawCircle (hgPos_Right - (radarUp*0.015), 0.045, radarUp, LINECOLOR, false, 0.25f, 0.00125f);

					DrawQuad(hgPos_Right + (radarUp*HG_Offset.Y), viewBackward, 0.1, MaterialCircleSeeThrough, new Vector4(0, 0, 0, 0.75f)); //Dim Backing
				}


				if (EnableHolograms_them) {
					double lockInTime_Left = HG_activationTimeTarget;
					lockInTime_Left = ClampedD (lockInTime_Left, 0, 1);
					lockInTime_Left = Math.Pow (lockInTime_Left, 0.1);

					DrawCircle (hgPos_Left, 0.04 * lockInTime_Left, radarUp, LINECOLOR, false, 0.5f, 0.00075f);
					DrawCircle (hgPos_Left - (radarUp*0.015), 0.045, radarUp, LINECOLOR, false, 0.25f, 0.00125f);

					if (isTargetLocked) {
						DrawQuad (hgPos_Left + (radarUp * HG_Offset.Y * 0.5), viewBackward, 0.15, MaterialCircleSeeThroughAdd, LINECOLOR_Comp * 0.125f, true);
						DrawQuad(hgPos_Left + (radarUp*HG_Offset.Y), viewBackward, 0.1, MaterialCircleSeeThrough, new Vector4(0, 0, 0, 0.75f)); //Dim Backing
					} else {
						DrawQuad (hgPos_Left + (radarUp * HG_Offset.Y * 0.5), viewBackward, 0.15, MaterialCircleSeeThroughAdd, LINECOLOR * 0.125f, true);
					}
				}
			}
			//---------------HOLOGRAMS-----------------



			

			updateRadarAnimations (shipPos);

			if (radarPings == null) {
				return;
			}

			//foreach (var entityPing in radarPings) {
			for (int i = 0; i < radarPings.Count; i++){

				if (radarPings [i].Entity == null) {
					radarPings.Clear();
					//FindEntities ();
					return;
				}

				if (radarPings [i].Entity.DisplayName != "Stone" && radarPings [i].Entity.DisplayName != null) {
					if (radarPings [i].Width == 0f) {
						//Something went wrong. Reinit this ping.
						radarPings [i] = newRadarPing (radarPings [i].Entity);
					}
				}

				VRage.ModAPI.IMyEntity entity = radarPings[i].Entity;

				// Calculate the position of the entity relative to the ship, scaled and transformed
				Vector3D entityPos = entity.GetPosition();

				Vector3D upSquish = Vector3D.Dot (entityPos, radarMatrix.Up) * radarMatrix.Up;
				//entityPos -= (upSquish*squishValue); //------Squishing the vertical axis on the radar to make it easier to read... but less vertically accurate.

				Vector3D relativePos = entityPos - shipPos; // Position relative to the ship
				double entityDistance = Vector3D.Distance(entityPos, shipPos);

				double fadeDistance = radarRange*1.01;
				float fadeDimmer = 1f;

				if (entityDistance <= fadeDistance){

					if (entityDistance > radarRange) {

						//entityPos = Vector3D.Normalize (entityPos - shipPos) * radarRange;
						fadeDimmer = 1-Clamped(1 - (float)((fadeDistance - entityDistance) / (fadeDistance - radarRange)), 0,1);
					}

					Vector3D scaledPos = ApplyLogarithmicScaling(entityPos, shipPos); // Apply radar scaling

					// Position on the radar
					Vector3D radarEntityPos = radarPos + scaledPos;

					Vector3D v = radarEntityPos - radarPos;
					Vector3D uNorm = Vector3D.Normalize (radarUp);
					float vertDistance = (float)Vector3D.Dot(v, uNorm);

					float upDownDimmer = 1f;
					if (vertDistance < 0) {
						upDownDimmer = 0.8f;
					}

					float lineLength = (float)Vector3D.Distance (radarEntityPos, radarPos);
					Vector3D lineDir = radarDown;

					//---What kind of entity?---
					double gridWidth = radarPings[i].Width;
					scale_Current = (float)gridWidth;

					color_Current = radarPings[i].Color;

					if (entityDistance < radarRange*0.985) {
						if (!radarPings [i].Announced) {
							radarPings [i].Announced = true;
							if (radarPings [i].Status == RelationshipStatus.Hostile && entityDistance > 500) {
								PlayCustomSound (SP_ENEMY, worldRadarPos);
								newAlertAnim(entity);
							}else if (radarPings [i].Status == RelationshipStatus.Friendly && entityDistance > 500) {
								PlayCustomSound (SP_NEUTRAL, worldRadarPos);
								newBlipAnim(entity);
							}else if (radarPings [i].Status == RelationshipStatus.Neutral && entityDistance > 500) {
								//PlayCustomSound (SP_NEUTRAL, worldRadarPos);
								//newBlipAnim(entity);
							}
						}
					} else {
						if (radarPings [i].Announced) {
							radarPings [i].Announced = false;
						}
					}

					//DETECT FIRING RANGE
					switch (radarPings[i].Status){
					case RelationshipStatus.Hostile:
						if (entityDistance < 800) {
							if (!targetingFlipper) {
								color_Current = color_GridEnemyAttack;
							}
							underAttack = true;
						}
						break;
					}
					//--------------------------

					Vector3D pulsePos = radarEntityPos+(lineDir*vertDistance);
					double pulseDistance = Vector3D.Distance(pulsePos, radarPos);
					float pulseTimer = (float)(ClampedD(radarTimer, 0,1)+0.5 + Math.Min(pulseDistance, radarRadius)/radarRadius);
					if (pulseTimer > 1) {
						pulseTimer = pulseTimer - 1;//(float)Math.Truncate (pulseTimer);
						if (pulseTimer > 1) {
							pulseTimer = pulseTimer - 1;
						}
					}
					pulseTimer = Math.Max(pulseTimer*2, 1);

					MyStringId drawMat = MaterialCircle;

					bool skipThis = false;

					switch (radarPings [i].Status){
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
						color_Current = LINECOLOR;
						if (!ShowVoxels) {
							skipThis = true;
						}
						break;
					case RelationshipStatus.FObj:
						drawMat = MaterialDiamond;
						break;
					default:
						drawMat = MaterialCircle;
						break;
					}


					if (skipThis) {
					}else{
						// Draw each entity as a billboard on the radar
						DrawLineBillboard(MaterialSquare, color_Current*0.25f*fadeDimmer*pulseTimer, radarEntityPos, lineDir, vertDistance, 0.001f*fadeDimmer);
						//MyTransparentGeometry.AddBillboardOriented (MaterialCircle, color_Current*upDownDimmer*0.25f*pulseTimer, radarEntityPos+(lineDir*vertDistance), viewForward, viewLeft, 0.0025f*fadeDimmer*scale_Current);
						DrawQuad(radarEntityPos+(lineDir*vertDistance), radarUp, (double)(0.005*fadeDimmer*scale_Current), MaterialCircle, color_Current*upDownDimmer*0.25f*pulseTimer*0.5f);

						Vector3D radarEntityPos2 = radarEntityPos;
						double dis2Cam = Vector3D.Distance (MyAPIGateway.Session.Camera.Position, radarEntityPos);
						float glitchValue = (float)glitchAmount;

						//Let's add some glitch to hostile entities that are a certain distance away.
						if (entityDistance > radarRange*0.9) {
							if (radarPings [i].Status == RelationshipStatus.Hostile) {
								double tempDistance = Math.Min (entityDistance, radarRange);
								tempDistance = (entityDistance - (radarRange * 0.9)) / (radarRange * 0.1);
								tempDistance = MathHelper.Clamp (tempDistance, 0, 1);
								glitchValue = (float)tempDistance;
							}
						}

						if (GetRandomBoolean()) {
							if (glitchAmount > 0.001) {
								Vector3D offsetRan = new Vector3D (
									(GetRandomDouble () - 0.5) * 2,
									(GetRandomDouble () - 0.5) * 2,
									(GetRandomDouble () - 0.5) * 2
								);

								radarEntityPos2 = radarEntityPos + offsetRan * dis2Cam * glitchValue * 0.025;
								color_Current *= GetRandomFloat ();
							}
						}
						if (glitchValue < 0.8) {
							MyTransparentGeometry.AddBillboardOriented (drawMat, color_Current * upDownDimmer * pulseTimer, radarEntityPos2, viewLeft, viewUp, 0.0025f * fadeDimmer * scale_Current);
						} else {
							Vector4 cRed = new Vector4 (1, 0, 0, 1);
							Vector4 cGrn = new Vector4 (0, 1, 0, 1);
							Vector4 cblu = new Vector4 (0, 0, 1, 1);

							Vector3D offsetRed = new Vector3D (
								(GetRandomDouble () - 0.5) * 2,
								(GetRandomDouble () - 0.5) * 2,
								(GetRandomDouble () - 0.5) * 2
							);
							Vector3D offsetGrn = new Vector3D (
								(GetRandomDouble () - 0.5) * 2,
								(GetRandomDouble () - 0.5) * 2,
								(GetRandomDouble () - 0.5) * 2
							);
							Vector3D offsetBlu = new Vector3D (
								(GetRandomDouble () - 0.5) * 2,
								(GetRandomDouble () - 0.5) * 2,
								(GetRandomDouble () - 0.5) * 2
							);

							radarEntityPos2 = radarEntityPos + offsetRed * dis2Cam * glitchValue * 0.025;
							MyTransparentGeometry.AddBillboardOriented (drawMat, cRed*color_Current * upDownDimmer * pulseTimer, radarEntityPos2, viewLeft, viewUp, 0.0025f * fadeDimmer * scale_Current);
							radarEntityPos2 = radarEntityPos + offsetGrn * dis2Cam * glitchValue * 0.025;
							MyTransparentGeometry.AddBillboardOriented (drawMat, cGrn*color_Current * upDownDimmer * pulseTimer, radarEntityPos2, viewLeft, viewUp, 0.0025f * fadeDimmer * scale_Current);
							radarEntityPos2 = radarEntityPos + offsetBlu * dis2Cam * glitchValue * 0.025;
							MyTransparentGeometry.AddBillboardOriented (drawMat, cblu*color_Current * upDownDimmer * pulseTimer, radarEntityPos2, viewLeft, viewUp, 0.0025f * fadeDimmer * scale_Current);

						}
						}
				}
			}

			//Targeting CrossHair
			double crossSize = 0.30;

			Vector3D crossOffset = radarMatrix.Left*radarRadius*1.65 + radarMatrix.Up*radarRadius*crossSize + radarMatrix.Forward*radarRadius*0.35;
			Vector4 crossColor = LINECOLOR;

			if (currentTarget != null) {
				double dis2Cam1 = Vector3D.Distance (MyAPIGateway.Session.Camera.Position, currentTarget.GetPosition ());
				if (dis2Cam1 > 50000) {
					currentTarget = null;
					isTargetLocked = false;

				} else {

					targettingLerp_goal = 1;
					targettingLerp = LerpD (targettingLerp, targettingLerp_goal, deltaTime*10);

					Vector3D targetDir = Vector3D.Normalize (currentTarget.WorldVolume.Center - MyAPIGateway.Session.Camera.Position) * 0.5;
					double dis2Cam = Vector3D.Distance (MyAPIGateway.Session.Camera.Position, MyAPIGateway.Session.Camera.Position + targetDir);
					DrawQuadRigid (MyAPIGateway.Session.Camera.Position + targetDir, cameraMatrix.Forward, dis2Cam * 0.125 * targettingLerp, MaterialLockOn, LINECOLOR*(float)targettingLerp, false);
					drawText (currentTarget.DisplayName, dis2Cam * 0.01, MyAPIGateway.Session.Camera.Position + targetDir + (cameraMatrix.Up * dis2Cam * 0.02) + (cameraMatrix.Right * dis2Cam * 0.10 * targettingLerp), cameraMatrix.Forward, LINECOLOR);

					string disUnits = "m";
					double dis2CamDisplay = dis2Cam1;
					if (dis2Cam1 > 1500) {
						disUnits = "km";
						dis2CamDisplay = dis2CamDisplay / 1000;
					}
					drawText (Convert.ToString (Math.Round (dis2CamDisplay)) + " " + disUnits, dis2Cam * 0.01, MyAPIGateway.Session.Camera.Position + targetDir + (cameraMatrix.Up * dis2Cam * -0.02) + (cameraMatrix.Right * dis2Cam * 0.11 * targettingLerp), cameraMatrix.Forward, LINECOLOR);

					double dotProduct = Vector3D.Dot (radarMatrix.Forward, targetDir);

					Vector3D targetPipPos = radarPos;
					Vector3D targetPipDir = Vector3D.Normalize (currentTarget.WorldVolume.Center - shipPos) * ((double)radarRadius * crossSize * 0.55);

					// Calculate the component of the position along the forward vector
					Vector3D forwardComponent = Vector3D.Dot (targetPipDir, radarMatrix.Forward) * radarMatrix.Forward;

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
							drawText (Convert.ToString (Math.Round (arivalTime)) + " " + unitsTime, dis2Cam * 0.01, MyAPIGateway.Session.Camera.Position + targetDir + (cameraMatrix.Up * dis2Cam * -0.05) + (cameraMatrix.Right * dis2Cam * 0.11 * targettingLerp), cameraMatrix.Forward, LINECOLOR_Comp);
						}
					} else {
						alignLerp_goal = 1;
					}
					alignLerp = LerpD (alignLerp, alignLerp_goal, deltaTime * 20);

					if (dotProduct > 0) {
						DrawQuadRigid (targetPipPos, radarMatrix.Backward, crossSize * 0.125 * 0.125, MaterialCircle, LINECOLOR_Comp, false); //LockOn
					} else {
						DrawQuadRigid (targetPipPos, radarMatrix.Backward, crossSize * 0.125 * 0.125, MaterialCircleHollow, LINECOLOR_Comp, false); //LockOn
					}
				}
			} else {
				targettingLerp = 1.5;
				targettingLerp_goal = 1.5;
				alignLerp = 1;
				alignLerp_goal = 1;
			}

			DrawQuadRigid(radarPos+crossOffset, radarMatrix.Backward, crossSize*alignLerp * 0.125, MaterialCross, crossColor, false); //Target
			DrawQuadRigid(radarPos+crossOffset, radarMatrix.Backward, crossSize * 0.125, MaterialCrossOutter, LINECOLOR, false); //Target
			DrawQuad(radarPos+crossOffset, radarMatrix.Backward, crossSize * 0.25, MaterialCircleSeeThrough, new Vector4(0, 0, 0, 0.75f)); //Dim Backing
		}



		// List to hold all entities found
		HashSet<VRage.ModAPI.IMyEntity> radarEntities = new HashSet<VRage.ModAPI.IMyEntity>();
		List<RadarPing> radarPings = new List<RadarPing>();

		private void FindEntities(){
			MyAPIGateway.Entities.GetEntities(radarEntities, IMyEntity => true);
			radarPings.Clear ();
			foreach (var entity in radarEntities) {
				if (entity is MyPlanet || entity.DisplayName == "Stone"){
				} else {
					if(!ShowVoxels && entity is IMyVoxelBase){

					}else{
						RadarPing ping = newRadarPing (entity);
						radarPings.Add (ping);
					}
				}
			}
			//radarEntities.Clear ();
		}

		private void DrawRadarEntity(VRage.ModAPI.IMyEntity entity){

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

			if (playerFaction == null || ownerFaction == null)
			{
				return RelationshipStatus.Hostile; // No faction info available
			}

			int reputation = MyAPIGateway.Session.Factions.GetReputationBetweenFactions(playerFaction.FactionId, ownerFaction.FactionId);

			if (reputation > 0)
				return RelationshipStatus.Friendly;
			else if (reputation < 0)
				return RelationshipStatus.Hostile;
			else
				return RelationshipStatus.Neutral;
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

		//==============LOG SCALING==================================================================
		public MatrixD CreateLogarithmicScaleMatrix(Vector3D entityPosition, Vector3D referencePoint)
		{
			double scaleBase = 10; //Base of the logarithm for scaling calculations

			// Calculate distance from the reference point
			double distance = Vector3D.Distance(entityPosition, referencePoint);

			// Avoid taking logarithm of zero or negative numbers
			distance = Math.Max(distance, 0.01); // A small positive value

			// Calculate logarithmic scale factor
			//double scaleFactor = Math.Max(Math.Exp(distance/radarRange)/2, radarScale); 
			double scaleFactor = Math.Max(Math.Exp(distance/radarRange)/2.8, radarScale); 


			// Create a scale matrix
			//MatrixD scaleMatrix = MatrixD.CreateScale(Math.Pow(1 / scaleFactor, 3));
			MatrixD scaleMatrix = MatrixD.CreateScale(1 / scaleFactor);

			// Translate entity position to origin, scale, and translate back
			MatrixD translationMatrixToOrigin = MatrixD.CreateTranslation(-referencePoint);
			//MatrixD translationMatrixBack = MatrixD.CreateTranslation(referencePoint);

			// Combine the matrices
			MatrixD transformationMatrix = translationMatrixToOrigin * scaleMatrix; //* translationMatrixBack;

			return transformationMatrix;
		}

		// public Vector3D ApplyLogarithmicScaling(Vector3D entityPosition, Vector3D referencePoint)
		// {
		// 	MatrixD transformationMatrix = CreateLogarithmicScaleMatrix(entityPosition, referencePoint);
		// 	MatrixD scalingMatrix = MatrixD.CreateScale(radarScale,radarScale,radarScale);

		// 	return Vector3D.Transform(Vector3D.Transform(entityPosition, transformationMatrix),scalingMatrix);
		// }

		public Vector3D ApplyLogarithmicScaling(Vector3D entityPos, Vector3D referencePos)
		{
			Vector3D offset = entityPos - referencePos;
			double distance = offset.Length();

			// Avoid unnecessary work for very close targets
			if (distance < 100) return offset * radarScale;

			// Apply logarithmic scaling to distance
			// This compresses distant items to not overflow the radar
			double logDistance = Math.Log10(distance + 1); // +1 to prevent log(0)

			// Normalize vector and scale
			Vector3D direction = offset / distance;
			Vector3D scaledOffset = direction * logDistance;

			// Scale to radar space
			return scaledOffset * radarScale * 100; // scale factor tweakable
		}
		//------------------------------------------------------------------------------------------



		//===============IS GRID ATTACKING?==========================================================
		public bool IsGridTargetingPlayer(VRage.Game.ModAPI.IMyCubeGrid grid)
		{
			var turrets = new List<Sandbox.ModAPI.IMyLargeTurretBase>();

			IMyPlayer player = MyAPIGateway.Session.Player;
			IMyCharacter character = player.Character;
			VRage.ModAPI.IMyEntity playerShip = character.Parent as VRage.ModAPI.IMyEntity; // If the player is piloting a ship

			foreach (Sandbox.ModAPI.IMyLargeTurretBase turret in turrets)
			{
				var currentTarget = turret.GetTargetedEntity();
					if (currentTarget.EntityId == character.EntityId || (playerShip != null && currentTarget.EntityId == playerShip.EntityId))
					{
						return true; // The turret is targeting the player or the player's ship
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


		private RadarPing newRadarPing(VRage.ModAPI.IMyEntity entity){
			RadarPing ping = new RadarPing();

			//===Icon Colors===
			Vector4 color_GridFriend =		(Color.Green).ToVector4()*2;		//IMyCubeGrid
			Vector4 color_GridEnemy =		(Color.Red).ToVector4()*4;			//   "
			Vector4 color_GridNeutral =		(Color.Yellow).ToVector4()*2;		//   "
			Vector4 color_FloatingObject =	(Color.DarkGray).ToVector4();		//IMyFloatingObject
			Vector4 color_VoxelBase =		(Color.DimGray).ToVector4();		//IMyVoxelBase
			//-----------------

			ping.Entity = entity;
			ping.Announced = false;
			ping.Width = 0f;
			ping.Color  = color_GridNeutral;
			ping.Status = RelationshipStatus.Neutral;

			ping.Time = new Stopwatch ();
			ping.Time.Start ();

			//---What kind of entity?---
			if (entity is VRage.Game.ModAPI.IMyCubeGrid){
				VRage.Game.ModAPI.IMyCubeGrid gridEntity = entity as VRage.Game.ModAPI.IMyCubeGrid;

				if (gridEntity != null) {
					double gridWidth = gridEntity.PositionComp.WorldVolume.Radius;
					ping.Width = (float)gridWidth / 25;

					long lpID = GetLocalPlayerId ();
					if (lpID != -1) {
						//DETECT FACTION STATUS
						RelationshipStatus gridFaction = GetGridRelationship (gridEntity, lpID);
						switch (gridFaction) {
						case RelationshipStatus.Friendly:
							ping.Color = color_GridFriend * GLOW;
							ping.Status = RelationshipStatus.Friendly;
							break;
						case RelationshipStatus.Hostile:
							ping.Color = color_GridEnemy * GLOW;
							ping.Status = RelationshipStatus.Hostile;
							break;
						case RelationshipStatus.Neutral:
							ping.Color  = color_GridNeutral * GLOW;
							ping.Status = RelationshipStatus.Neutral;
							ping.Announced = true;
							break;
						default:
							ping.Color = color_GridNeutral * GLOW;
							ping.Status = RelationshipStatus.Neutral;
							ping.Announced = true;
							break;
						}
					} else {
						ping.Color = color_GridNeutral * GLOW;
						ping.Status = RelationshipStatus.Neutral;
						ping.Announced = true;
					}
					ping.Width = Clamped (ping.Width, 1f, 3f);
				}
			}else if (entity is IMyFloatingObject){
				ping.Color = color_FloatingObject*GLOW;
				ping.Status = RelationshipStatus.FObj;
				ping.Width = 0.5f;
			}else if (entity is MyPlanet){
				ping.Color = new Vector4 (0,0,0,0);
				ping.Status = RelationshipStatus.FObj;
				ping.Width = 0.00001f;
				ping.Announced = true;
			}else if (entity is IMyVoxelBase){
				ping.Color = LINECOLOR*GLOW; //color_VoxelBase;
				ping.Status = RelationshipStatus.Vox;
				IMyVoxelBase voxelEntity = entity as IMyVoxelBase;
				if (voxelEntity != null) {
					double vixelWidth = voxelEntity.PositionComp.WorldVolume.Radius;
					ping.Width = (float)vixelWidth / 250;
					ping.Width = Clamped (ping.Width, 1f, 3f);
				}
			}
			//--------------------------

			//Echo(entity.DisplayName);
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

		public void newBlipAnim(VRage.ModAPI.IMyEntity entity){
			if (entity == null) {
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
				if (RadarAnimations [i].Time.Elapsed.TotalSeconds > (RadarAnimations [i].LifeTime * RadarAnimations [i].Loops)) {
					//If time is greater than length of animation, add to the deletion list and skip rendering.
					//deleteList.Add (RadarAnimations [i]);
				}else{
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
			foreach(var d in deleteList){
				if (RadarAnimations.Contains (d)) {
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
			int segments = LINEDETAIL * Convert.ToInt32(Clamped(Remap((float)radius, 1000, 10000000, 1, 8), 1, 16));
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
				DrawLineBillboard(MaterialSquare, LINECOLOR * dimmer, point1, direction, 0.9f, segmentThickness, BlendTypeEnum.Standard);
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

		private void CheckPlayerInput()
		{
			if (MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.SECONDARY_TOOL_ACTION))
			{
				//Echo ("Press");
				if (isTargetLocked && currentTarget != null && !currentTarget.MarkedForClose)
				{
					// Toggle off if the same target is still valid
					isTargetLocked = false;
					HG_initializedTarget = false;
					HG_activationTimeTarget = 0;
					currentTarget = null;
					//Echo("Lock-on released.");
					PlayCustomSound (SP_ZOOMIN, worldRadarPos);
				}
				else
				{
					// Attempt to lock on a new target
					//Echo ("Find target");
					var newTarget = FindEntityInSight();
					if (newTarget != null) {
						currentTarget = newTarget;
						isTargetLocked = true;
						newBlipAnim (newTarget);
						//Echo ("New target locked.");
						PlayCustomSound (SP_ZOOMOUT, worldRadarPos);
					} else if (isTargetLocked) {
						// No valid new target found, toggle off existing target
						isTargetLocked = false;
						HG_initializedTarget = false;
						HG_activationTimeTarget = 0;
						currentTarget = null;
						//Echo ("Lock-on released.");
						PlayCustomSound (SP_ZOOMIN, worldRadarPos);
					}
				}
			}
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
			public double Health_Max;
			public double Health_Cur;
			public double Health_Last;
			public bool IsJumpDrive;
			public double JumpDrive_LC;
			public double JumpDrive_Avg;
			public Sandbox.ModAPI.Ingame.IMyJumpDrive JumpDrive;
		}

		private List<BlockTracker> YourBlocks = new List<BlockTracker>();
		private List<BlockTracker> TheirBlocks = new List<BlockTracker>();

		private List<BlockTracker> YourDrives = new List<BlockTracker>();
		private List<BlockTracker> TheirDrives = new List<BlockTracker>();

		private double YourHealth_Max = 0;
		private double YourHealth_Cur = 0;

		private double TheirHealth_Max = 0;
		private double TheirHealth_Cur = 0;

		private int YourBlockCounter = 0;
		private int TheirBlockCounter = 0;

		private double YourShields_Max = 0;
		private double YourShields_Cur = 0;
		private double YourShields_Last = 0;

		private double TheirShields_Max = 0;
		private double TheirShields_cur = 0;
		private double TheirShields_Last = 0;

		private double YourTime2Ready = 0;
		private double TheirTime2Ready = 0;

		private VRage.Game.ModAPI.IMyCubeGrid HG_controlledGrid;
		private VRage.Game.ModAPI.IMyCubeGrid HG_TargetGrid;
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
				YourHealth_Max = 0;
				YourHealth_Cur = 0;
				YourBlockCounter = 0;
				YourShields_Max = 0;
				YourShields_Cur = 0;

				HG_Initialize();
				HG_initialized = true;
				HG_activationTime = 0;
			}

			double fontSize = 0.005;

			if (HG_controlledGrid != null && EnableHolograms_you) //God, I really should have made this far more generic so I don't have to manage the same code for the player and the target seperately...
			{
				HG_DrawHologram(HG_controlledGrid, YourBlocks);

				double bootUpAlphaY = HG_activationTime;
				bootUpAlphaY = ClampedD (bootUpAlphaY, 0, 1);
				bootUpAlphaY = Math.Pow (bootUpAlphaY, 0.25);

				Vector3D hgPos_Right = worldRadarPos + radarMatrix.Left*-radarRadius + radarMatrix.Left*HG_Offset.X + radarMatrix.Forward*(HG_Offset.Z - 0.04) + radarMatrix.Down*0.0075;

				//Shields
				if (YourShields_Max > 0.01) {
					double yTempShields = LerpD (YourShields_Last, YourShields_Cur, deltaTime * 2);

					Vector3D ShieldPos_Right = worldRadarPos + radarMatrix.Left * -radarRadius + radarMatrix.Left * HG_Offset.X + radarMatrix.Up * HG_Offset.Y + radarMatrix.Forward * HG_Offset.Z;
					drawShields (ShieldPos_Right, yTempShields, YourShields_Max, bootUpAlphaY, YourTime2Ready);

					YourShields_Last = yTempShields;
				}

				double YourHP = 1;
				if (YourHealth_Max > 0) {
					YourHP = YourHealth_Cur / YourHealth_Max;
					YourHP = Math.Pow (YourHP, 4);
				}

				YourHP = Math.Round (YourHP * 100 * bootUpAlphaY);
				string YourHPS = Convert.ToString (YourHP) + "%";

				if (YourHP < 100) {
					YourHPS = " " + YourHPS;
					if (YourHP < 10) {
						YourHPS = " " + YourHPS;
					}
				}

				hgPos_Right = 	worldRadarPos + radarMatrix.Right*radarRadius + 	radarMatrix.Left*HG_Offset.X + 		radarMatrix.Down*0.0075 + (radarMatrix.Forward*fontSize*2);
				DrawArc(hgPos_Right, 0.065, radarMatrix.Up, 0, 90 * ((float)YourHP/100)*(float)bootUpAlphaY, LINECOLOR, 0.015f, 1f);

				//hgPos_Right += (radarMatrix.Left * (YourHPS.Length * fontSize)) + (radarMatrix.Up*fontSize) + (radarMatrix.Forward*fontSize*0.5) + (radarMatrix.Left * fontSize*11);
				//drawText (YourHPS, fontSize, hgPos_Right, radarMatrix.Forward, LINECOLOR);
				//drawText ("000 ", fontSize, hgPos_Right, radarMatrix.Forward, LINECOLOR*0.25f);

				string yHPS = Convert.ToString (Math.Ceiling(YourHealth_Cur/100*bootUpAlphaY));
				hgPos_Right += (radarMatrix.Left * (yHPS.Length * fontSize)) + (radarMatrix.Up*fontSize) + (radarMatrix.Forward*fontSize*0.5) + (radarMatrix.Left * 0.065);
				drawText (yHPS, fontSize, hgPos_Right, radarMatrix.Forward, LINECOLOR);

				HG_activationTime += deltaTime*0.667;

				HG_UpdateBlockStatus (YourBlockCounter, YourBlocks, YourHealth_Cur, out YourBlockCounter, out YourHealth_Cur);
				double temp_YourTime2Ready = 0;
				HG_UpdateDriveStatus (YourDrives, out YourShields_Cur, out YourShields_Max, out temp_YourTime2Ready);
				if (temp_YourTime2Ready > 0) {
					YourTime2Ready = temp_YourTime2Ready;
				}

			}


			if (isTargetLocked && currentTarget != null && EnableHolograms_them) {
				if (!HG_initializedTarget) {

					TheirHealth_Max = 0;
					TheirHealth_Cur = 0;
					TheirBlockCounter = 0;
					TheirShields_Max = 0;
					TheirShields_cur = 0;

					HG_InitializeTarget (currentTarget);
					HG_initializedTarget = true;
					HG_activationTimeTarget = 0;
				}
				if (HG_initializedTarget) 
				{
					HG_DrawHologram (HG_TargetGrid, TheirBlocks, true);

					double bootUpAlphaT = HG_activationTimeTarget;
					bootUpAlphaT = ClampedD (bootUpAlphaT, 0, 1);
					bootUpAlphaT = Math.Pow (bootUpAlphaT, 0.25);

					Vector3D hgPos_Left = worldRadarPos + radarMatrix.Left*-radarRadius*-1 + radarMatrix.Left*HG_Offset.X*-1 + radarMatrix.Forward*(HG_Offset.Z - 0.04) + radarMatrix.Down*0.0075;

					//Shields
					if (TheirShields_Max > 0.01) {
						double tTempShields = LerpD (TheirShields_Last, TheirShields_cur, deltaTime * 2);

						Vector3D ShieldPos_Right = worldRadarPos + radarMatrix.Right * -radarRadius + radarMatrix.Right * HG_Offset.X + radarMatrix.Up * HG_Offset.Y + radarMatrix.Forward * HG_Offset.Z;
						drawShields (ShieldPos_Right, tTempShields, TheirShields_Max, bootUpAlphaT, TheirTime2Ready);

						TheirShields_Last = tTempShields;
					}

					double TheirHP = 1;
					if (TheirHealth_Max > 0) {
						TheirHP = TheirHealth_Cur / TheirHealth_Max;
						TheirHP = Math.Pow (TheirHP, 4);
					}

					TheirHP = Math.Round (TheirHP * 100 * bootUpAlphaT);
					string TheirHPS = Convert.ToString (TheirHP) + "%";

					if (TheirHP < 100) {
						TheirHPS = " " + TheirHPS;
						if (TheirHP < 10) {
							TheirHPS = " " + TheirHPS;
						}
					}
						
					hgPos_Left = 	worldRadarPos + radarMatrix.Left*radarRadius + 		radarMatrix.Right*HG_Offset.X + 	radarMatrix.Down*0.0075 + (radarMatrix.Forward*fontSize*2);
					DrawArc(hgPos_Left, 0.065, radarMatrix.Up, 368 - (88 * (float)TheirHP/100)*(float)bootUpAlphaT, 360, LINECOLOR, 0.015f, 1f);

					string tHPS = Convert.ToString (Math.Ceiling(TheirHealth_Cur/100*bootUpAlphaT));
					hgPos_Left += (radarMatrix.Left * (tHPS.Length * fontSize)) + (radarMatrix.Up*fontSize) + (radarMatrix.Forward*fontSize*0.5) + (radarMatrix.Right * 0.065);
					drawText (tHPS, fontSize, hgPos_Left, radarMatrix.Forward, LINECOLOR);
					//drawText ("000 ", fontSize, hgPos_Left, radarMatrix.Forward, LINECOLOR*0.25f);

					HG_activationTimeTarget += deltaTime*0.667;

					HG_UpdateBlockStatus (TheirBlockCounter, TheirBlocks, TheirHealth_Cur, out TheirBlockCounter, out TheirHealth_Cur);

					double temp_TheirTime2Ready = 0;
					HG_UpdateDriveStatus (TheirDrives, out TheirShields_cur, out TheirShields_Max, out temp_TheirTime2Ready);
					if (temp_TheirTime2Ready > 0) {
						TheirTime2Ready = temp_TheirTime2Ready;
					}
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

		private void HG_UpdateBlockStatus (int cnt, List<BlockTracker> BT, double HP, out int cnt_out, out double HP_out)
		{
			if (cnt >= 0 && cnt < BT.Count) {

				if (BT [cnt].Block != null) {
					VRage.Game.ModAPI.IMySlimBlock block = BT [cnt].Block;
					BT [cnt].Health_Cur = block.Integrity;
					BT [cnt].Health_Max = block.MaxIntegrity;

					HP -= BT [cnt].Health_Last - BT [cnt].Health_Cur;

					BT [cnt].Health_Last = BT [cnt].Health_Cur;

					//DAMAGE EVENT if Last is more than Current
				}

				cnt += 1;
				if (cnt >= BT.Count) {
					cnt = 0;
				}

			} else {
				cnt = 0;
			}

			cnt_out = cnt;
			HP_out = HP;
				
		}

		private Stopwatch Drives_deltaTimer = new Stopwatch ();
		private double Drives_deltaTime = 0;

		private void HG_UpdateDriveStatus(List<BlockTracker> BT, out double HP_cur, out double HP_max, out double time2ready)
		{
			HP_cur = 0;
			HP_max = 0;

			double minTime = double.MaxValue;

			foreach (BlockTracker block in BT)
			{
				if (block.JumpDrive != null)
				{
					bool isReady = block.JumpDrive.Status == MyJumpDriveStatus.Ready;
					if (block.JumpDrive.IsWorking && isReady)
					{
						HP_cur += block.JumpDrive.CurrentStoredPower;
					}
					HP_max += block.JumpDrive.MaxStoredPower;

					if (HP_max != HP_cur) {
						double currentStoredPower = 	block.JumpDrive.CurrentStoredPower;
						double lastStoredPower = 		block.JumpDrive_LC;
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
							block.JumpDrive_LC = currentStoredPower;

						}
					}
				}
			}



			time2ready = minTime != double.MaxValue ? minTime : 0;
			//Echo ($"Time till ready: {time2ready}");
		}

		private void HG_Initialize()
		{
			// Get the player's controlled grid
			var controlledEntity = GetControlledGrid();
			HG_controlledGrid = controlledEntity as VRage.Game.ModAPI.IMyCubeGrid;

			if (HG_controlledGrid != null)
			{
				YourBlocks = new List<BlockTracker>();
				YourDrives = new List<BlockTracker> ();
				YourBlocks = HG_GetBlockInfo(HG_controlledGrid);
				// Define a scaling matrix for positioning on the dashboard
				double thicc = HG_scaleFactor / (HG_controlledGrid.WorldVolume.Radius / HG_controlledGrid.GridSize);
				HG_scalingMatrix = MatrixD.CreateScale(HG_Scale*thicc);

				for ( int b = 0 ; b < YourBlocks.Count ; b++){
					if (YourBlocks [b].IsJumpDrive) {
						YourDrives.Add (YourBlocks [b]);
					}
				}
			}
		}

		private void HG_InitializeTarget(VRage.ModAPI.IMyEntity target)
		{
			var controlledEntity = target;

			if (target == null) {
				return;
			}

			HG_TargetGrid = controlledEntity as VRage.Game.ModAPI.IMyCubeGrid;

			if (HG_TargetGrid != null)
			{
				TheirBlocks = new List<BlockTracker>();
				TheirDrives = new List<BlockTracker>();
				TheirBlocks = HG_GetBlockInfo(HG_TargetGrid, true); 
				// Define a scaling matrix for positioning on the dashboard
				double thicc = HG_scaleFactor / (HG_TargetGrid.WorldVolume.Radius / HG_TargetGrid.GridSize);
				HG_scalingMatrixTarget = MatrixD.CreateScale(HG_Scale*thicc);

				for ( int b = 0 ; b < TheirBlocks.Count ; b++){
					if (TheirBlocks [b].IsJumpDrive) {
						TheirDrives.Add (TheirBlocks [b]);
					}
				}
			}
		}

		private List<BlockTracker> HG_GetBlockInfo(VRage.Game.ModAPI.IMyCubeGrid grid, bool flippit = false)
		{
			var center = grid.WorldVolume.Center;
			var pos = grid.GetPosition ();

			MatrixD inverseMatrix = GetRotationMatrix (grid.WorldMatrix);
			inverseMatrix = MatrixD.Invert (inverseMatrix);

			var blockInfo = new List<BlockTracker>();

			var blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
			grid.GetBlocks(blocks);

			foreach (var block in blocks)
			{
				Vector3D sc;
				block.ComputeWorldCenter (out sc);
				sc -= center;
				sc = Vector3D.Transform (sc, inverseMatrix);
				sc /= grid.GridSize;

				VRage.Game.ModAPI.IMySlimBlock Blocker = block as VRage.Game.ModAPI.IMySlimBlock;
				Vector3D Position = sc;
				double Health_Max = block.MaxIntegrity;
				double Health_Cur = block.Integrity;

				BlockTracker BT = new BlockTracker ();
				BT.Position = Position;
				BT.Block = Blocker;
				BT.Health_Max = Health_Max;
				BT.Health_Cur = Health_Cur;
				BT.Health_Last = Health_Cur;

				if (!flippit) {
					YourHealth_Cur += Health_Cur;
					YourHealth_Max += Health_Max;
				} else {
					TheirHealth_Cur += Health_Cur;
					TheirHealth_Max += Health_Max;
				}

				BT.IsJumpDrive = false;

				if (block.FatBlock != null) {
					if (IsJumpDrive (block.FatBlock)) {
						var jumpDrive = block.FatBlock as Sandbox.ModAPI.Ingame.IMyJumpDrive;
						if (jumpDrive != null) {
							BT.IsJumpDrive = true;
							BT.JumpDrive = jumpDrive;
							BT.JumpDrive_LC = 0;
						}
					}
				}

				blockInfo.Add(BT);
			}

			return blockInfo;
		}

		private bool IsJumpDrive(VRage.Game.ModAPI.IMyCubeBlock block)
		{
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

		private void HG_DrawHologram(VRage.Game.ModAPI.IMyCubeGrid grid, List<BlockTracker> blockInfo, bool flippit = false)
		{
			if (grid != null) {
				Vector3D angularVelocity = gHandler.localGridVelocityAngular;
				MatrixD rotationMatrix = CreateRotationMatrix(angularVelocity);
				//MatrixD rotatedMatrix = ApplyRotation(grid.WorldMatrix, rotationMatrix);

				foreach (var BT in blockInfo) {
					Vector3D positionR = BT.Position;
					if (!flippit) {
						positionR = Vector3D.Rotate (BT.Position, rotationMatrix);
					}
					Vector3D positionT = Vector3D.Transform (positionR, grid.WorldMatrix);

					double HealthPercent = ClampedD(BT.Health_Cur / BT.Health_Max, 0, 1);

					HG_DrawBillboard (positionT-grid.GetPosition(), grid, flippit, HealthPercent);
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





			var color = LINECOLOR * 0.5f;
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
				position = Vector3D.Transform (position, HG_scalingMatrixTarget);
			} else {
				position = Vector3D.Transform (position, HG_scalingMatrix);
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

				DrawLineBillboard (MaterialSquare, color * 0.15f * (float)dotProd * (float)bootUpAlpha, holoCenter, holoDir, (float)holoLength, 0.0025f, BlendTypeEnum.AdditiveTop);
			}
		}
			
		private MatrixD CreateRotationMatrix(Vector3D angularVelocity)
		{
			// Create the rotation angle vector (angular velocity * deltaTime)
			Vector3D rotationAngle = angularVelocity * deltaTime * 10;

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

			Vector4 color = LINECOLOR;
			double fontSize = 0.005;

			if (cr != new_cr) {
				color *= 2;
				fontSize = 0.006;
			}

			string crs = "$"+Convert.ToString (cr);
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

			DrawCircle (pos, 0.01, radarMatrix.Forward, LINECOLOR, false, 0.25f, 0.001f); // Center Dot

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

			Vector4 color = LINECOLOR;


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
			drawText (name, fontSize, pos, radarMatrix.Forward, LINECOLOR, 1f);
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
			drawText (Convert.ToString (name), 0.005, aPos + (radarMatrix.Up * 0.005 * 3), radarMatrix.Forward, LINECOLOR, 0.75f);
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