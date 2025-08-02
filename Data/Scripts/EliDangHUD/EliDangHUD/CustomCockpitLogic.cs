using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using System.Collections.Generic;
using VRage.Utils;
using VRageMath;
using System;
using VRage.Game.ModAPI.Ingame.Utilities;
using System.Text.RegularExpressions;
using System.Text;

[MyEntityComponentDescriptor(typeof(MyObjectBuilder_Cockpit), false)]
public class CustomCockpitLogic : MyGameLogicComponent
{
	private IMyCockpit _cockpit;
	private static bool _controlsCreated = false;  // Static flag to track control creation

	private MyIni ini = new MyIni();

	public override void Init(MyObjectBuilder_EntityBase objectBuilder)
	{
		base.Init(objectBuilder);
		_cockpit = Entity as IMyCockpit;
		NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;

	}

	public override void UpdateOnceBeforeFrame()
	{
		base.UpdateOnceBeforeFrame();

		if (_cockpit != null && !_controlsCreated)
		{
			CreateCustomControls();
			_controlsCreated = true;  // Set the flag to true after creating controls
		}
	}


    private static bool IsEligibleCockpit(IMyTerminalBlock block)
    {
		IMyCockpit cockpit = block as IMyCockpit;
		if (cockpit == null || !GridHasAntenna(block))
		{ 
			return false; 
		}
        return cockpit.CustomName.Contains("[ELI_HUD]");
    }

    

    public static bool GridHasAntenna(IMyTerminalBlock block)
	{
		IEnumerable<IMyRadioAntenna> antennas = block.CubeGrid.GetFatBlocks<IMyRadioAntenna>();

		int count = 0;
		foreach(var i in antennas)
		{
			foreach (IMyRadioAntenna antenna in antennas)
			{
				if (antenna.IsWorking)
				{
					// Check if the antenna is broadcasting
					if (antenna.IsBroadcasting)
					{
						if (!i.CustomData.Contains("[NORADAR]"))
							count++;
					}
				}
			}
		}
		if(count>0)
		{
			return true;
		}
		return false;

	}

	private bool IsHologramsEnabled(IMyTerminalBlock block){
		bool result;
		// Attempt to parse the string from CustomData. If it fails, return a default value.
		if (bool.TryParse(GetParameter(block, "ScannerHolo"), out result))
			return result;
		return false;  // Default value if parsing fails or parameter does not exist
	}

	private void CreateCustomControls()
	{
		// Check if the slider already exists (pseudo-code, adjust according to actual API capabilities)
		if (!ControlsExist())
		{
			var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyCockpit>(""); // separators don't store the id
			c.SupportsMultipleBlocks = true;
			c.Visible = block => true;

			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(c);
		}
		{
			var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyCockpit>("AntennaScannerE");
			c.Label = MyStringId.GetOrCompute("Antenna Scanner : Enabled");
			c.SupportsMultipleBlocks = true;
			c.Visible = block => IsEligibleCockpit(block);

			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(c);
		}
		{
			var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyCockpit>("AntennaScannerD");
			c.Label = MyStringId.GetOrCompute("Antenna Scanner : Disabled");
			c.SupportsMultipleBlocks = true;
			c.Visible = block => !IsEligibleCockpit(block);

			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(c);
		}
		{
			var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyCockpit>(""); // separators don't store the id
			c.SupportsMultipleBlocks = true;
			c.Visible = block => true;

			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(c);
		}
//		{
//			var sliderX = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyCockpit>("SliderX");
//			sliderX.Title = MyStringId.GetOrCompute("Offset X");
//			sliderX.SetLimits(-2, 2);
//			// Correctly handle string-to-float conversions for the Getter and Setter
//			sliderX.Getter = block => {
//				float result;
//				// Attempt to parse the string from CustomData. If it fails, return a default value.
//				if (float.TryParse(GetParameter(block, "ScannerX"), out result))
//					return result;
//				return 0.0f;  // Default value if parsing fails or parameter does not exist
//			};
//
//			sliderX.Setter = (block, value) => {
//				// Convert float to string and set it using SetParameter
//				SetParameter(block, "ScannerX", value.ToString());
//			};
//
//			sliderX.Writer = (block, sb) => {
//				float currentValue = sliderX.Getter(block);
//				sb.AppendFormat("{0} m", currentValue.ToString("F2"));  // Formatting to two decimal places
//			};
//			sliderX.Enabled = block => true;
//			sliderX.Visible = block => true;
//			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(sliderX);
//		}
		{
			var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyCockpit>("EnableIt");
			c.Title = MyStringId.GetOrCompute("Enable Holo HUD");
			c.Tooltip = MyStringId.GetOrCompute("Override of entire Holo HUD. Uncheck to remove entirely.");
			c.SupportsMultipleBlocks = true;
			c.Visible = block => IsEligibleCockpit(block);
			c.Enabled = block => true; // to see how the grayed out ones look

			c.Getter = block => {
				bool result;
				// Attempt to parse the string from CustomData. If it fails, return a default value.
				if (bool.TryParse(GetParameter(block, "ScannerEnable"), out result))
					return result;
				return true;  // Default value if parsing fails or parameter does not exist
			};

			c.Setter = (block, value) => {
				// Convert float to string and set it using SetParameter
				SetParameter(block, "ScannerEnable", value.ToString());
			};
			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(c);

            var action = MyAPIGateway.TerminalControls.CreateAction<IMyCockpit>("ToggleHoloHud");
            action.Name = new StringBuilder("Toggle Holo HUD");
            action.Icon = "Textures\\GUI\\Icons\\Actions\\Toggle.dds";
            action.Enabled = block => IsEligibleCockpit(block);

            // Toggle the "ScannerEnable" parameter
            action.Action = block =>
            {
                bool current = false;
                bool parsed = bool.TryParse(GetParameter(block, "ScannerEnable"), out current);
                SetParameter(block, "ScannerEnable", (!current).ToString());
            };

            // Show "On"/"Off" based on current value
            action.Writer = (block, sb) =>
            {
                bool current = false;
                bool parsed = bool.TryParse(GetParameter(block, "ScannerEnable"), out current);
                sb.Append(current ? "On" : "Off");
            };

            MyAPIGateway.TerminalControls.AddAction<IMyCockpit>(action);
        }
		{
			var sliderY = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyCockpit>("SliderY");
			sliderY.Title = MyStringId.GetOrCompute("Offset Y");
			sliderY.SetLimits(-2, 2);
			// Correctly handle string-to-float conversions for the Getter and Setter
			sliderY.Getter = block => {
				float result;
				// Attempt to parse the string from CustomData. If it fails, return a default value.
				if (float.TryParse(GetParameter(block, "ScannerY"), out result))
					return result;
				return -0.20f;  // Default value if parsing fails or parameter does not exist
			};

			sliderY.Setter = (block, value) => {
				// Convert float to string and set it using SetParameter
				SetParameter(block, "ScannerY", value.ToString());
			};

			sliderY.Writer = (block, sb) => {
				float currentValue = sliderY.Getter(block);
				sb.AppendFormat("{0} m", currentValue.ToString("F2"));  // Formatting to two decimal places
			};
			sliderY.Enabled = block => true;
			sliderY.Visible = block => IsEligibleCockpit(block);
			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(sliderY);
		}
		{
			var sliderZ = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyCockpit>("SliderZ");
			sliderZ.Title = MyStringId.GetOrCompute("Offset Z");
			sliderZ.SetLimits(-2, 2);
			// Correctly handle string-to-float conversions for the Getter and Setter
			sliderZ.Getter = block => {
				float result;
				// Attempt to parse the string from CustomData. If it fails, return a default value.
				if (float.TryParse(GetParameter(block, "ScannerZ"), out result))
					return result;
				return -0.575f;  // Default value if parsing fails or parameter does not exist
			};

			sliderZ.Setter = (block, value) => {
				// Convert float to string and set it using SetParameter
				SetParameter(block, "ScannerZ", value.ToString());
			};

			sliderZ.Writer = (block, sb) => {
				float currentValue = sliderZ.Getter(block);
				sb.AppendFormat("{0} m", currentValue.ToString("F2"));  // Formatting to two decimal places
			};
			sliderZ.Enabled = block => true;
			sliderZ.Visible = block => IsEligibleCockpit(block);
			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(sliderZ);
		}
		{
			var sliderS = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyCockpit>("Scale");
			sliderS.Title = MyStringId.GetOrCompute("Scale");
			sliderS.SetLimits(0.01f, 3f);
			// Correctly handle string-to-float conversions for the Getter and Setter
			sliderS.Getter = block => {
				float result;
				// Attempt to parse the string from CustomData. If it fails, return a default value.
				if (float.TryParse(GetParameter(block, "ScannerS"), out result))
					return result;
				return 1f;  // Default value if parsing fails or parameter does not exist
			};

			sliderS.Setter = (block, value) => {
				// Convert float to string and set it using SetParameter
				SetParameter(block, "ScannerS", value.ToString());
			};

			sliderS.Writer = (block, sb) => {
				float currentValue = sliderS.Getter(block);
				sb.AppendFormat("{0} m", currentValue.ToString("F2"));  // Formatting to two decimal places
			};
			sliderS.Enabled = block => true;
			sliderS.Visible = block => IsEligibleCockpit(block);
			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(sliderS);
		}
		{
			var colorControl = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlColor, IMyCockpit>("YourCustomColorControl");
			colorControl.Title = MyStringId.GetOrCompute("Scanner Color");

			// Getter: Convert the stored string to a Color
			colorControl.Getter = block => {
				var colorString = GetParameter(block, "ScannerColor");
				return ParseColor(colorString);
			};

			// Setter: Convert the Color to a string and store it
			colorControl.Setter = (block, color) => {
				string colorValue = $"{color.R},{color.G},{color.B}";
				SetParameter(block, "ScannerColor", colorValue);
			};

			colorControl.Enabled = block => true;
			colorControl.Visible = block => IsEligibleCockpit(block);
			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(colorControl);
		}
		{
			var sliderS = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyCockpit>("Brightness");
			sliderS.Title = MyStringId.GetOrCompute("Brightness");
			sliderS.SetLimits(0f, 10f);
			// Correctly handle string-to-float conversions for the Getter and Setter
			sliderS.Getter = block => {
				float result;
				// Attempt to parse the string from CustomData. If it fails, return a default value.
				if (float.TryParse(GetParameter(block, "ScannerB"), out result))
					return result;
				return 1f;  // Default value if parsing fails or parameter does not exist
			};

			sliderS.Setter = (block, value) => {
				// Convert float to string and set it using SetParameter
				SetParameter(block, "ScannerB", value.ToString());
			};

			sliderS.Writer = (block, sb) => {
				float currentValue = sliderS.Getter(block);
				sb.AppendFormat("{0} m", currentValue.ToString("F2"));  // Formatting to two decimal places
			};
			sliderS.Enabled = block => true;
			sliderS.Visible = block => IsEligibleCockpit(block);
			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(sliderS);
		}
		{
			var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyCockpit>(""); // separators don't store the id
			c.SupportsMultipleBlocks = true;
			c.Visible = block => IsEligibleCockpit(block);

			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(c);
		}
		{
			var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyCockpit>("Holograms");
			c.Title = MyStringId.GetOrCompute("Enable Holograms");
			c.Tooltip = MyStringId.GetOrCompute("Enable Target and Local Holograms on HUD");
			c.SupportsMultipleBlocks = true;
			c.Visible = block => IsEligibleCockpit(block);
			c.Enabled = block => true; // to see how the grayed out ones look

			c.Getter = block => {
				bool result;
				// Attempt to parse the string from CustomData. If it fails, return a default value.
				if (bool.TryParse(GetParameter(block, "ScannerHolo"), out result))
					return result;
				return true;  // Default value if parsing fails or parameter does not exist
			};

			c.Setter = (block, value) => {
				// Convert float to string and set it using SetParameter
				SetParameter(block, "ScannerHolo", value.ToString());
			};
			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(c);

            var action = MyAPIGateway.TerminalControls.CreateAction<IMyCockpit>("ToggleHolograms");
            action.Name = new StringBuilder("Toggle Holograms");
            action.Icon = "Textures\\GUI\\Icons\\Actions\\Toggle.dds";
            action.Enabled = block => IsEligibleCockpit(block);

            // Toggle the "ToggleHolograms" parameter
            action.Action = block =>
            {
                bool current = false;
                bool parsed = bool.TryParse(GetParameter(block, "ScannerHolo"), out current);
                SetParameter(block, "ScannerHolo", (!current).ToString());
            };

            // Show "On"/"Off" based on current value
            action.Writer = (block, sb) =>
            {
                bool current = false;
                bool parsed = bool.TryParse(GetParameter(block, "ScannerHolo"), out current);
                sb.Append(current ? "On" : "Off");
            };

            MyAPIGateway.TerminalControls.AddAction<IMyCockpit>(action);
        }
		{
			var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyCockpit>("HologramsYou");
			c.Title = MyStringId.GetOrCompute("Hologram : Your Ship");
			c.Tooltip = MyStringId.GetOrCompute("Enable Local Hologram on HUD");
			c.SupportsMultipleBlocks = true;
			c.Visible = block => IsEligibleCockpit(block);
			c.Enabled = block => true; // to see how the grayed out ones look

			c.Getter = block => {
				bool result;
				// Attempt to parse the string from CustomData. If it fails, return a default value.
				if (bool.TryParse(GetParameter(block, "ScannerHoloYou"), out result))
					return result;
				return true;  // Default value if parsing fails or parameter does not exist
			};

			c.Setter = (block, value) => {
				// Convert float to string and set it using SetParameter
				SetParameter(block, "ScannerHoloYou", value.ToString());
			};
			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(c);
		}
		{
			var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyCockpit>("HologramsThem");
			c.Title = MyStringId.GetOrCompute("Holograms : Target");
			c.Tooltip = MyStringId.GetOrCompute("Enable Target Hologram on HUD");
			c.SupportsMultipleBlocks = true;
			c.Visible = block => IsEligibleCockpit(block);
			c.Enabled = block => true; // to see how the grayed out ones look

			c.Getter = block => {
				bool result;
				// Attempt to parse the string from CustomData. If it fails, return a default value.
				if (bool.TryParse(GetParameter(block, "ScannerHoloThem"), out result))
					return result;
				return true;  // Default value if parsing fails or parameter does not exist
			};

			c.Setter = (block, value) => {
				// Convert float to string and set it using SetParameter
				SetParameter(block, "ScannerHoloThem", value.ToString());
			};
			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(c);
		}
		{
			var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyCockpit>(""); // separators don't store the id
			c.SupportsMultipleBlocks = true;
			c.Visible = block => IsEligibleCockpit(block);

			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(c);
		}
		{
			var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyCockpit>("Toolbars");
			c.Title = MyStringId.GetOrCompute("Ammo Overlay");
			c.Tooltip = MyStringId.GetOrCompute("Enable Ammo stockpile overlay on HUD");
			c.SupportsMultipleBlocks = true;
			c.Visible = block => IsEligibleCockpit(block);
			c.Enabled = block => true; // to see how the grayed out ones look

			c.Getter = block => {
				bool result;
				// Attempt to parse the string from CustomData. If it fails, return a default value.
				if (bool.TryParse(GetParameter(block, "ScannerTools"), out result))
					return result;
				return true;  // Default value if parsing fails or parameter does not exist
			};

			c.Setter = (block, value) => {
				// Convert float to string and set it using SetParameter
				SetParameter(block, "ScannerTools", value.ToString());
			};
			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(c);
		}
		{
			var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyCockpit>("Gauges");
			c.Title = MyStringId.GetOrCompute("Resource Gauges");
			c.Tooltip = MyStringId.GetOrCompute("Enable Energy and H2 gauges around Scanner");
			c.SupportsMultipleBlocks = true;
			c.Visible = block => IsEligibleCockpit(block);
			c.Enabled = block => true; // to see how the grayed out ones look

			c.Getter = block => {
				bool result;
				// Attempt to parse the string from CustomData. If it fails, return a default value.
				if (bool.TryParse(GetParameter(block, "ScannerGauges"), out result))
					return result;
				return true;  // Default value if parsing fails or parameter does not exist
			};

			c.Setter = (block, value) => {
				// Convert float to string and set it using SetParameter
				SetParameter(block, "ScannerGauges", value.ToString());
			};
			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(c);
		}
		{
			var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyCockpit>("DollaDolla");
			c.Title = MyStringId.GetOrCompute("Space Credits");
			c.Tooltip = MyStringId.GetOrCompute("Enable display of Space Credit balance in account");
			c.SupportsMultipleBlocks = true;
			c.Visible = block => IsEligibleCockpit(block);
			c.Enabled = block => true; // to see how the grayed out ones look

			c.Getter = block => {
				bool result;
				// Attempt to parse the string from CustomData. If it fails, return a default value.
				if (bool.TryParse(GetParameter(block, "ScannerDolla"), out result))
					return result;
				return true;  // Default value if parsing fails or parameter does not exist
			};

			c.Setter = (block, value) => {
				// Convert float to string and set it using SetParameter
				SetParameter(block, "ScannerDolla", value.ToString());
			};
			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(c);
		}
		{
			var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyCockpit>("SpeedLines");
			c.Title = MyStringId.GetOrCompute("Velocity Lines");
			c.Tooltip = MyStringId.GetOrCompute("Enable Velocity Lines on HUD");
			c.SupportsMultipleBlocks = true;
			c.Visible = block => IsEligibleCockpit(block);
			c.Enabled = block => true; // to see how the grayed out ones look

			c.Getter = block => {
				bool result;
				// Attempt to parse the string from CustomData. If it fails, return a default value.
				if (bool.TryParse(GetParameter(block, "ScannerLines"), out result))
					return result;
				return true;  // Default value if parsing fails or parameter does not exist
			};

			c.Setter = (block, value) => {
				// Convert float to string and set it using SetParameter
				SetParameter(block, "ScannerLines", value.ToString());
			};
			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(c);
		}
		{
			var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyCockpit>("OrbitLines");
			c.Title = MyStringId.GetOrCompute("Map Speed Threshold");
			c.SetLimits(0f, 50000f);
			// Correctly handle string-to-float conversions for the Getter and Setter
			c.Getter = block => {
				float result;
				// Attempt to parse the string from CustomData. If it fails, return a default value.
				if (float.TryParse(GetParameter(block, "ScannerOrbits"), out result))
					return result;
				return 100f;  // Default value if parsing fails or parameter does not exist
			};

			c.Setter = (block, value) => {
				// Convert float to string and set it using SetParameter
				SetParameter(block, "ScannerOrbits", value.ToString());	
			};

			c.Writer = (block, sb) => {
				float currentValue = c.Getter(block)/1000f;
				sb.AppendFormat("{0} km/s", currentValue.ToString("F2"));  // Formatting to two decimal places

			};
			c.Enabled = block => true;
			c.Visible = block => IsEligibleCockpit(block);
			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(c);
		}
		{
			var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyCockpit>("ShowVoxels");
			c.Title = MyStringId.GetOrCompute("Show Non-Ships");
			c.Tooltip = MyStringId.GetOrCompute("Disable to hide objects like Asteroids");
			c.SupportsMultipleBlocks = true;
			c.Visible = block => IsEligibleCockpit(block);
			c.Enabled = block => true; // to see how the grayed out ones look

			c.Getter = block => {
				bool result;
				// Attempt to parse the string from CustomData. If it fails, return a default value.
				if (bool.TryParse(GetParameter(block, "ScannerShowVoxels"), out result))
					return result;
				return true;  // Default value if parsing fails or parameter does not exist
			};

			c.Setter = (block, value) => {
				// Convert float to string and set it using SetParameter
				SetParameter(block, "ScannerShowVoxels", value.ToString());
			};
			MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(c);

            var action = MyAPIGateway.TerminalControls.CreateAction<IMyCockpit>("ToggleVoxels");
            action.Name = new StringBuilder("Toggle Voxels");
            action.Icon = "Textures\\GUI\\Icons\\Actions\\Toggle.dds";
            action.Enabled = block => IsEligibleCockpit(block);

            // Toggle the "ScannerShowVoxels" parameter
            action.Action = block =>
            {
                bool current = false;
                bool parsed = bool.TryParse(GetParameter(block, "ScannerShowVoxels"), out current);
                SetParameter(block, "ScannerShowVoxels", (!current).ToString());
            };

            // Show "On"/"Off" based on current value
            action.Writer = (block, sb) =>
            {
                bool current = false;
                bool parsed = bool.TryParse(GetParameter(block, "ScannerShowVoxels"), out current);
                sb.Append(current ? "On" : "Off");
            };

            MyAPIGateway.TerminalControls.AddAction<IMyCockpit>(action);
        }
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, IMyCockpit>("ShowPoweredOnly");
            c.Title = MyStringId.GetOrCompute("Show Only Powered Grids");
            c.Tooltip = MyStringId.GetOrCompute("Enable to show only grids with power");
            c.SupportsMultipleBlocks = true;
            c.Visible = block => IsEligibleCockpit(block);
            c.Enabled = block => true; // to see how the grayed out ones look

            c.Getter = block => {
                bool result;
                // Attempt to parse the string from CustomData. If it fails, return a default value.
                if (bool.TryParse(GetParameter(block, "ScannerOnlyPoweredGrids"), out result))
                    return result;
                return true;  // Default value if parsing fails or parameter does not exist
            };

            c.Setter = (block, value) => {
                // Convert float to string and set it using SetParameter
                SetParameter(block, "ScannerOnlyPoweredGrids", value.ToString());
            };
            MyAPIGateway.TerminalControls.AddControl<IMyCockpit>(c);

            var action = MyAPIGateway.TerminalControls.CreateAction<IMyCockpit>("TogglePoweredOnly");
            action.Name = new StringBuilder("Toggle Powered Only");
            action.Icon = "Textures\\GUI\\Icons\\Actions\\Toggle.dds";
            action.Enabled = block => IsEligibleCockpit(block);

            // Toggle the "ScannerOnlyPoweredGrids" parameter
            action.Action = block =>
            {
                bool current = false;
                bool parsed = bool.TryParse(GetParameter(block, "ScannerOnlyPoweredGrids"), out current);
                SetParameter(block, "ScannerOnlyPoweredGrids", (!current).ToString());
            };

            // Show "On"/"Off" based on current value
            action.Writer = (block, sb) =>
            {
                bool current = false;
                bool parsed = bool.TryParse(GetParameter(block, "ScannerOnlyPoweredGrids"), out current);
                sb.Append(current ? "On" : "Off");
            };

            MyAPIGateway.TerminalControls.AddAction<IMyCockpit>(action);
        }

    }

	private bool ControlsExist()
	{
		// Implement logic to check if controls already exist
		return _controlsCreated;
	}

	private static Vector3 LINECOLOR_RGB = 		new Vector3(1f, 0.5f, 0.0f);

	private Color ParseColor(string colorString)
	{
		if (string.IsNullOrEmpty(colorString))
			return LINECOLOR_RGB;  // Default color if no data

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
		return LINECOLOR_RGB;  // Default color on parse failure
	}

	public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
	{
		return Entity.GetObjectBuilder(copy);
	}

	public void SetParameter(IMyTerminalBlock block, string key, string value)
	{
		// Write your specific data
		string mySection = "EliDang"; 	// Your specific section
		string myKey =		key; 		// Your specific key
		string myValue = 	value; 		// Your specific value

		// Ensure CustomData is parsed
		string customData = block.CustomData;
		MyIniParseResult result;
		ini.Clear();

		bool failboat = false;
		bool doubleFailBoat = false;

		string updatedSection;

		if (!ini.TryParse(customData, out result)) {
		} else {
			ini.Set(mySection, myKey, myValue);
			string updatedData = ini.ToString ();
			if (!updatedData.EndsWith("---"))
			{
				if (ini.EndContent == "") {
					updatedData += "---\n";
				}
			}

			block.CustomData = updatedData;
			return;
		}
			

		// Pattern to match the section including the delimiter "---"
		string pattern = $@"(\[{mySection}\].*?---\n)";
		var match = Regex.Match(customData, pattern, RegexOptions.Singleline);

		if (match.Success)
		{
			string sectionData = match.Groups[1].Value;

			if (!ini.TryParse(sectionData, out result))
			{

				ini.Set(mySection, myKey, myValue);
				updatedSection = ini.ToString() + "---\n";

				customData =updatedSection + block.CustomData;

				// Update the block's CustomData with the modified ini data
				block.CustomData = customData;

				return;
			}else{

				ini.Set(mySection, myKey, myValue);
				string updatedData2 = ini.ToString () + "---\n";

				customData = Regex.Replace(customData, pattern, updatedData2, RegexOptions.Singleline);

				block.CustomData = customData;
				return;
			}
		}else{
			ini.Set(mySection, myKey, myValue);
			updatedSection = ini.ToString() + "---\n";

			customData =updatedSection + block.CustomData;

			// Update the block's CustomData with the modified ini data
			block.CustomData = customData;

			return;
		}
	}



	public string GetParameter(IMyTerminalBlock block, string key)
	{
		// Read your specific data
		string mySection = "EliDang"; // Your specific section
		string myKey = key;           // Your specific key

		// Ensure CustomData is parsed
		string customData = block.CustomData;
		MyIni ini = new MyIni();
		MyIniParseResult result;

		// Parse the entire CustomData
		if (ini.TryParse(customData, out result))
		{
			return ini.Get(mySection, key).ToString("");
		}

		// Pattern to match the section including the delimiter "---"
		string pattern = $@"(\[{mySection}\].*?---\n)";
		var match = Regex.Match(customData, pattern, RegexOptions.Singleline);

		if (match.Success)
		{
			string sectionData = match.Groups[1].Value;

			if (ini.TryParse(sectionData, out result))
			{
				return ini.Get(mySection, key).ToString("");
			}
			else
			{
				// Section not found
				return ""; // Or some default value or error indication
			}
		}
		else
		{
			// Section not found
			return ""; // Or some default value or error indication
		}
	}


//	public string GetParameter(IMyTerminalBlock block, string key)
//	{
//		string customData = block.CustomData;
//		string mySection = "EliDang"; // Your specific section
//		MyIni ini = new MyIni();
//		MyIniParseResult result;
//
//		// Parse the entire CustomData
//		if (!ini.TryParse(customData, out result))
//		{
//			// Handle parse error if needed
//			return "";
//		}
//
//		// Retrieve your specific parameter
//		return ini.Get(mySection, key).ToString("");
//	}



}