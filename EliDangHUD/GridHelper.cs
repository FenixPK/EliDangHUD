using System;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game.ModAPI.Interfaces;
using VRage.Game.ModAPI;
using VRageMath;
using VRage.Utils;
using System.Collections.Generic;
using Sandbox.Definitions;
using SpaceEngineers.Game.ModAPI;
using System.Diagnostics;

namespace EliDangHUD
{
	// Defines a session component that updates both before and after simulation.
	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation)]
	public class GridHelper : MySessionComponentBase
	{
		// Singleton instance to access session component from other classes.
		public static GridHelper Instance;

		public string localGridName;

		// Stores the current entity associated with the local player's grid.
		public IMyEntity localGridEntity;
		public Vector3D localGridPosition;
		// Stores the current velocity of the local player's grid.
		public Vector3D localGridVelocity;
		public Vector3D localGridVelocityAngular;
		// Stores the speed derived from the velocity of the local player's grid.
		public double localGridSpeed;
		// Indicates whether the local player is controlling a grid.
		public bool IsPlayerControlling;

		private float damageAmount = 0;

		private Stopwatch deltaTimer = new Stopwatch ();
		private double deltaTime = 0;

		// Initializes the singleton instance upon data loading.
		public override void LoadData()
		{   
			Instance = this;
		}

		// Cleans up the singleton instance when data is unloaded.
		protected override void UnloadData()
		{
			Instance = null;
			base.UnloadData();
			// Ensure we detach from any grid we might still be attached to
			DetachFromGrid();
		}

		// Updates the grid information each simulation tick.
		public void UpdateGrid()
		{
			// Check if the local player is controlling a grid entity.
			IsPlayerControlling = IsLocalPlayerControllingGrid();

			if (IsPlayerControlling) {
				// Retrieve the current entity being controlled.
				localGridEntity = LocalPlayerEntity();
				if (localGridEntity != null) {
					if (!deltaTimer.IsRunning) {
						deltaTimer.Start ();
					}

					deltaTime = deltaTimer.Elapsed.TotalSeconds;
					deltaTimer.Restart();

					localGridPosition = localGridEntity.GetPosition ();
					// Obtain the current velocity of the grid entity.
					localGridVelocity = GetEntityVelocity(localGridEntity);
					localGridVelocityAngular = GetEntityVelocityAngular(localGridEntity);
					// Calculate the speed from the velocity vector.
					localGridSpeed = localGridVelocity.Length();
					localGridName = localGridEntity.DisplayName;
					CalculateHydrogenTime ();
				} else {
					// Reset values if the entity is not available.
					localGridVelocity = Vector3D.Zero;
					localGridPosition = Vector3D.Zero;
					localGridSpeed = 0f;
					localGridName = " ";
				}
			}
		}

		// Checks if the local player is currently controlling a grid entity.
		public bool IsLocalPlayerControllingGrid()
		{
			// Returns true if the controlled object is a ship controller.
			return MyAPIGateway.Session.ControlledObject is IMyShipController;
		}

		// Retrieves the current entity controlled by the local player.
		public IMyEntity LocalPlayerEntity()
		{
			IMyControllableEntity controlledEntity = MyAPIGateway.Session.ControlledObject;
			// Return the entity if available; otherwise, null.
			return controlledEntity?.Entity;
		}

		// Retrieves the velocity of the specified entity.
		public Vector3D GetEntityVelocity(IMyEntity entity)
		{
			if (entity == null)
				return Vector3D.Zero; // Return zero velocity if the entity is null.

			// Attempt to cast the entity to IMyCockpit if it's part of a cockpit.
			var cockpit = entity as IMyCockpit;
			if (cockpit != null && cockpit.CubeGrid != null && cockpit.CubeGrid.Physics != null)
			{
				// Return the linear velocity if the physics component is available.
				return cockpit.CubeGrid.Physics.LinearVelocity;
			}
			else
			{
				// Log an error and return zero velocity if the physics component is not found.
				MyLog.Default.WriteLine("Failed to retrieve velocity: No valid physics component found.");
				return Vector3D.Zero;
			}
		}

		// Retrieves the velocity of the specified entity.
		public Vector3D GetEntityVelocityAngular(IMyEntity entity)
		{
			if (entity == null)
				return Vector3D.Zero; // Return zero velocity if the entity is null.

			// Attempt to cast the entity to IMyCockpit if it's part of a cockpit.
			var cockpit = entity as IMyCockpit;
			if (cockpit != null && cockpit.CubeGrid != null && cockpit.CubeGrid.Physics != null)
			{
				// Return the linear velocity if the physics component is available.
				return cockpit.CubeGrid.Physics.AngularVelocityLocal;
			}
			else
			{
				// Log an error and return zero velocity if the physics component is not found.
				MyLog.Default.WriteLine("Failed to retrieve velocity: No valid physics component found.");
				return Vector3D.Zero;
			}
		}

		//		public IMyRadioAntenna CheckAndModifyAntennaProperties(IMyCubeGrid grid)
		//		{
		//			// Get all blocks of type IMyRadioAntenna
		//			//var antennas = new List<IMyTerminalBlock>();
		//			//grid.GetBlocksOfType<IMyRadioAntenna>(antennas);
		//			//grid.GetBlocks<IMyRadioAntenna> (antennas);
		//
		//			var antennas = new List<IMyRadioAntenna>();
		//			grid.GetBlocks(antennas, (antennas);
		//
		//			foreach (IMyRadioAntenna antenna in antennas)
		//			{
		//				if (antenna != null && antenna.IsFunctional && antenna.Enabled)
		//				{
		//					// Check if the antenna is broadcasting
		//					if (antenna.IsBroadcasting)
		//					{
		//						return antenna;
		//					}
		//				}
		//			}
		//
		//			return null;
		//		}

		public bool GridHasPassiveAntenna(IMyCubeGrid grid) 
		{
            IEnumerable<IMyRadioAntenna> antennas = grid.GetFatBlocks<IMyRadioAntenna>();
            int count = 0;
            foreach (var i in antennas)
            {
                foreach (IMyRadioAntenna antenna in antennas)
                {
                    if (antenna.IsWorking)
                    {
						if (!i.CustomData.Contains("[NORADAR]"))
						{ 
							count++;
						}
                    }
                }
            }
            if (count > 0) // If we have any powered+functional antennas that are not flagged as [NORADAR] then we can receive passive signals. 
            {
                return true;
            }
            return false;
        }

		public bool GridHasActiveAntenna(IMyCubeGrid grid) 
		{
            IEnumerable<IMyRadioAntenna> antennas = grid.GetFatBlocks<IMyRadioAntenna>();
            int count = 0;
            foreach (var i in antennas)
            {
                foreach (IMyRadioAntenna antenna in antennas)
                {
                    if (antenna.IsWorking && antenna.IsBroadcasting)
                    {
                        if (!i.CustomData.Contains("[NORADAR]"))
                        {
                            count++;
                        }
                    }
                }
            }
            if (count > 0) // If we have any powerd+functional+active/broadcasting antennas that are not flagged as [NORADAR] then we can send active signals. 
            {
                return true;
            }
            return false;
        }

		public double GridActiveAntennaRange(IMyCubeGrid grid)
		{
            IEnumerable<IMyRadioAntenna> antennas = grid.GetFatBlocks<IMyRadioAntenna>();
			double maxRange = 0.0;
            foreach (var i in antennas)
            {
                foreach (IMyRadioAntenna antenna in antennas)
                {
                    if (antenna.IsWorking && antenna.IsBroadcasting)
                    {
                        if (!i.CustomData.Contains("[NORADAR]"))
                        {
							maxRange = Math.Max(maxRange, i.Radius);
                        }
                    }
                }
            }
            return maxRange;
        }

		/// <summary>
		/// Skips antennas tagged as [NORADAR]. Checks a grid for passive radar: any antenna that is functional/powered. Checks a grid for active radar: any antenna that is functional/powered, and broadcasting. Returns max active range if present.
		/// </summary>
		/// <param name="grid">The grid to check</param>
		/// <param name="hasPassive">Does the grid have passive radar capability?</param>
		/// <param name="hasActive">Does the grid have active radar turned on?</param>
		/// <param name="maxActiveRange">Active radar range if on.</param>
        public void EvaluateGridAntennaStatus(IMyCubeGrid grid, out bool hasPassive, out bool hasActive, out double maxActiveRange)
        {
            hasPassive = false;
            hasActive = false;
            maxActiveRange = 0.0;

            var antennas = grid.GetFatBlocks<IMyRadioAntenna>();

            foreach (var antenna in antennas)
            {
                if (!antenna.IsWorking)
                    continue;

                if (antenna.CustomData.Contains("[NORADAR]"))
                    continue;

                hasPassive = true;

                if (antenna.IsBroadcasting)
                {
                    hasActive = true;
                    if (antenna.Radius > maxActiveRange)
                        maxActiveRange = antenna.Radius;
                }
            }
        }


        public bool GridHasAntenna(IMyCubeGrid grid)
		{
			IEnumerable<IMyRadioAntenna> antennas = grid.GetFatBlocks<IMyRadioAntenna>();

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

		//==============DAMAGE HANDLER===========================================================
		private Queue<IMySlimBlock> blocksToCheck = new Queue<IMySlimBlock>();
		private HashSet<IMySlimBlock> monitoredBlocks = new HashSet<IMySlimBlock>();
		private IMyCubeGrid currentGrid;

		public override void UpdateBeforeSimulation()
		{
//			UpdateDamageCheck();
//			CheckNextBlock();  // Check one block per frame for damage


		}

		public void UpdateDamageCheck()
		{
			var cockpit = MyAPIGateway.Session.ControlledObject as Sandbox.ModAPI.IMyCockpit;
			if (cockpit == null) return;

			IMyCubeGrid grid = cockpit.CubeGrid;

			if (grid != null && grid != currentGrid)
			{
				// Detach from the previous grid
				DetachFromGrid();
				// Attach to the new grid
				AttachToGrid(grid);
				currentGrid = grid;
			}

			CheckNextBlock();
		}

		private void AttachToGrid(IMyCubeGrid grid)
		{
			var blocks = new List<IMySlimBlock>();
			grid.GetBlocks(blocks);

			foreach (var block in blocks)
			{
				if (block.FatBlock != null)
				{
					block.ComponentStack.IsFunctionalChanged += OnBlockFunctionalChanged;
					monitoredBlocks.Add(block);
				}
				blocksToCheck.Enqueue(block);  // Enqueue all blocks for periodic integrity check
			}
		}

		private void DetachFromGrid()
		{
			foreach (var block in monitoredBlocks)
			{
				block.ComponentStack.IsFunctionalChanged -= OnBlockFunctionalChanged;
			}
			monitoredBlocks.Clear();
			blocksToCheck.Clear();
			currentGrid = null;
		}

		private void OnBlockFunctionalChanged()
		{
			//MyAPIGateway.Utilities.ShowMessage("Damage", "A block on your grid has been destroyed.");
			damageAmount += 1;
		}

		private void CheckNextBlock()
		{
			if (blocksToCheck.Count > 0)
			{
				var block = blocksToCheck.Dequeue();
				blocksToCheck.Enqueue(block);  // Re-enqueue the block for continuous checking

				if (block.MaxIntegrity > block.Integrity)
				{
					float integrityDif = block.Integrity/block.MaxIntegrity;
					//damageAmount += (1-integrityDif)*0.01f;

					//string displayName = block.BlockDefinition.DisplayNameText;
					//MyAPIGateway.Utilities.ShowMessage(displayName, Convert.ToString(Math.Round(integrityDif*100)) + "%");
				}
			}
		}
			
		public double getDamageAmount ()
		{
			double value = damageAmount;
			damageAmount = 0;
			return value;
		}

		public float powerProduced;
		public float powerConsumed;
		public float powerStored;
		public float powerStoredMax;
		public float powerHours;

		public float GetGridPowerUsagePercentage(IMyCubeGrid grid)
		{
			float totalPowerProduced = 0f;
			float totalPowerConsumed = 0f;
			float currentCharge = 0f;
			float maxCharge = 0f;

			var blocks = new List<IMySlimBlock>();
			grid.GetBlocks(blocks);

			foreach (var block in blocks)
			{
				if (block.FatBlock != null)
				{
					// Check if the block is a battery or reactor or solar panel, etc.
					if (block.FatBlock is IMyBatteryBlock && block.FatBlock.IsWorking)
					{
						var battery = block.FatBlock as IMyBatteryBlock;
						totalPowerConsumed += battery.CurrentOutput;
						totalPowerProduced += battery.MaxOutput;
						currentCharge += battery.CurrentStoredPower;
						maxCharge += battery.MaxStoredPower;
					}
					else if (block.FatBlock is IMyFunctionalBlock && block.FatBlock is IMyPowerProducer && block.FatBlock.IsWorking)
					{
						// This catches other types of power producers
						var producer = block.FatBlock as IMyPowerProducer;
						totalPowerConsumed += producer.CurrentOutput;
						totalPowerProduced += producer.MaxOutput;
					}
				}
			}

			float powerUsagePercentage = 0f;
			if (totalPowerProduced > 0)
			{
				powerUsagePercentage = (totalPowerConsumed / totalPowerProduced);
			}

			powerProduced = totalPowerProduced;
			powerConsumed = totalPowerConsumed;
			powerStored = currentCharge;
			powerStoredMax = maxCharge;

			// Estimate time remaining
			float timeRemaining = currentCharge / totalPowerConsumed;
			powerHours = timeRemaining;

			//MyAPIGateway.Utilities.ShowMessage("Debug", $"Total Produced: {totalPowerProduced}, Total Consumed: {totalPowerConsumed}, Usage: {powerUsagePercentage}%");
			return powerUsagePercentage;
		}


		public double H2powerSeconds;
		private double HydrogenThrusterConsumptionRate = 0;
		private double remainingH2 = 0;
		private double remainingH2_prev = 0;
		public float H2Ratio = 0;

        //============ H 2 =========================================================

        bool IsHydrogenTank(IMyGasTank tank)
        {
			// If a block can store something and has a capacity, and it mentions hydrogen, assume it's a hydrogen tank. 
            return tank.Capacity > 0 &&
                   (tank.DetailedInfo.Contains("Hydrogen") ||
                    tank.DefinitionDisplayNameText.Contains("Hydrogen") ||
                    tank.BlockDefinition.SubtypeName.Contains("Hydrogen"));
        }

        private void CalculateHydrogenTime()
		{
			var grids = new List<IMyCubeGrid>();
			MyAPIGateway.GridGroups.GetGroup(MyAPIGateway.Session.LocalHumanPlayer.Controller.ControlledEntity.Entity.GetTopMostParent() as IMyCubeGrid, GridLinkTypeEnum.Logical, grids);

			double totalCapacity = 0;
			double currentHydrogen = 0;
			double totalConsumptionRate = 0;

			foreach (var grid in grids)
			{
				var blocks = new List<IMySlimBlock>();
				grid.GetBlocks(blocks, block => block.FatBlock is IMyGasTank || block.FatBlock is IMyThrust);

				foreach (var block in blocks)
				{
					if (block.FatBlock is IMyGasTank)
					{
						IMyGasTank tank = block.FatBlock as IMyGasTank;
						if(tank.IsWorking && IsHydrogenTank(tank))
                        {
								totalCapacity += tank.Capacity;
								currentHydrogen += tank.Capacity * tank.FilledRatio;
							}
					}
				}
			}

			remainingH2 = currentHydrogen;
			if (remainingH2 == remainingH2_prev) {

			} else {
				HydrogenThrusterConsumptionRate = (remainingH2_prev - remainingH2) / deltaTime;
			}
			remainingH2_prev = currentHydrogen;

			double timeRemaining = totalConsumptionRate > 0 ? (currentHydrogen * 1000) / totalConsumptionRate : 0;


			timeRemaining = currentHydrogen / HydrogenThrusterConsumptionRate;

			// Display the result to the player (HUD message, etc.)
			//Echo($"Hydrogen Time Remaining: {timeRemaining} seconds");
			//Echo ($"Capacity: {totalCapacity} - Current: {currentHydrogen} - Consumption: {HydrogenThrusterConsumptionRate}");

			H2powerSeconds = timeRemaining;

			H2Ratio = (float)(remainingH2 / totalCapacity);
		}

		private List<string> Echo_String_Prev = new List<string>();
		private void Echo(string message)
		{
			bool isGuiVisible = MyAPIGateway.Gui.IsCursorVisible;
			if (isGuiVisible) {
				return;
			}

			if(!Echo_String_Prev.Contains(message)){
				// This method should be replaced with your actual logging or display method
				MyAPIGateway.Utilities.ShowMessage ("Echo", message);
				Echo_String_Prev.Add(message);
			}
		}
			
	}
}

