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
using System.Linq;

namespace EliDangHUD
{
	// Defines a session component that updates both before and after simulation.
	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation)]
	public class GridHelper : MySessionComponentBase
	{
		// Singleton instance to access session component from other classes.
		public static GridHelper Instance;

		//----THE CONTROLLED ENTITY----//
		/// <summary>
		/// We find the entity the player is controlling and store it here, which is a cockpit or control block (or perhaps even a passenger seat?)
		/// </summary>
		public IMyEntity localGridControlledEntity;

        /// <summary>
        /// Name of the controlled entity block (seat/cockpit)
        /// </summary>
        public string localGridControlledEntityName;

		/// <summary>
		/// Custom Name of the controlled entity block (seat/cockpit)
		/// </summary>
		public string localGridControlledEntityCustomName = "";

        /// <summary>
        /// The matrix of the controlled entity (seat/cockpit) in worldspace
        /// </summary>
        public MatrixD localGridControlledEntityMatrix;

        /// <summary>
        /// The position of the controlled entity (seat/cockpit) in worldspace
        /// </summary>
        public Vector3D localGridControlledEntityPosition;

		/// <summary>
		/// Stores the current CustomData for the controlled entity (seat/cockpit)
		/// </summary>
		public string localGridControlledEntityCustomData;

		/// <summary>
		/// Stores the linear velocity of the CubeGrid belonging to the controlled entity (seat/cockpit)
		/// </summary>
		public Vector3D localGridVelocity;

        /// <summary>
        /// Stores the angular velocity of the CubeGrid belonging to the controlled entity (seat/cockpit)
        /// </summary>
        public Vector3D localGridVelocityAngular;

        /// <summary>
        /// Stores the speed of the CubeGrid belonging to the controlled entity (seat/cockpit) derived from linear velocity
        /// </summary>
        public double localGridSpeed;

        /// <summary>
        /// Stores the altitude of the CubeGrid belonging to the controlled entity (seat/cockpit) derived from linear velocity, only if in planet gravity
        /// </summary>
        public double localGridAltitude;

        /// <summary>
        /// Stores whether the player is in control of a grid (cockpit/seat)
        /// </summary>
        public bool IsPlayerControlling;

		//----THE GRID ITSELF----//
        /// <summary>
        /// We find the CubeGrid the controlled entity (seat/cockpit) belongs to and store it here
        /// </summary>
        public IMyCubeGrid localGrid;

        /// <summary>
        /// Name of the CubeGrid the localGridControlledEntity belongs to
        /// </summary>
        public string localGridName;

        /// <summary>
        /// The matrix in world space of the CubeGrid the localGridControlledEntity belongs to
        /// </summary>
        public MatrixD localGridMatrix;

        /// <summary>
        /// The position in world space of the CubeGrid the localGridControlledEntity belongs to
        /// </summary>
        public Vector3D localGridPosition;

		/// <summary>
		/// Current damage to local grid calculated by incrementing +1 when a block is damaged to the point of being non-functional. 
		/// </summary>
        private float damageAmount = 0;

        /// <summary>
        /// Stores max possible output of batteries and power producers for the CubeGrid the localGridControlledEntity belongs to
        /// </summary>
        public float localGridPowerProduced;

        /// <summary>
        /// Stores current output of batteries and power producers for the CubeGrid the localGridControlledEntity belongs to
        /// </summary>
        public float localGridPowerConsumed;

		/// <summary>
		/// Stores the current charge of all batteries
		/// </summary>
        public float localGridPowerStored;

		/// <summary>
		/// Stores the max possible charge of all batteries
		/// </summary>
        public float localGridPowerStoredMax;

        /// <summary>
        /// Stores the result of dividing localGridPowerStored / localGridPowerConsumed. Ie at current rate of consumption how long will stored battery power last?
        /// </summary>
        public float localGridPowerHoursRemaining;

        /// <summary>
        /// Stopwatch used for calculating elapsed time since last check performed, stored in deltaTime as a double
        /// </summary>
        private Stopwatch timerGameTickDelta = new Stopwatch ();

		/// <summary>
		/// Stores the elapsed time since last check was performed, calculated by deltaTimer and set by UpdateElapsedTimeDeltaTimer(). In theory we would be doing one check per tick, storing elapsed time, and re-setting the timer
		/// and not re-setting it multiple times per tick.
		/// </summary>
		private double deltaTimeSinceLastTick = 0;

		/// <summary>
		/// Initializes the singleton instance upon loading data
		/// </summary>
		public override void LoadData()
		{   
			Instance = this;
		}

		/// <summary>
		/// Starts deltaTimer if not already running. Stores the current elapsed time as deltaTime, then restarts the timer back to 0. 
		/// </summary>
		private void UpdateElapsedTimeDeltaTimerGHandler()
		{
            if (!timerGameTickDelta.IsRunning)
            {
                timerGameTickDelta.Start();
            }
            deltaTimeSinceLastTick = timerGameTickDelta.Elapsed.TotalSeconds;
            timerGameTickDelta.Restart();
        }

		// Cleans up the singleton instance when data is unloaded.
		protected override void UnloadData()
		{
			Instance = null;
			base.UnloadData();
			// Ensure we detach from any grid we might still be attached to
			DetachDamageHandlerFromGrid();
		}

		/// <summary>
		/// Updates local grid information, to be called each game tick to update stored positions, velocities, names, and calculate remaining hydrogen based on current consumption
		/// </summary>
		public void UpdateLocalGrid()
		{
			// Check if the local player is controlling a grid entity.
			IsPlayerControlling = IsLocalPlayerControllingGrid();

			if (IsPlayerControlling) 
			{
				// Retrieve the current entity being controlled.
				localGridControlledEntity = LocalPlayerEntity(); // Store the controlled entity for re-use
				if (localGridControlledEntity != null) 
				{
                    IMyCockpit cockpit = localGridControlledEntity as Sandbox.ModAPI.IMyCockpit;
                    if (cockpit == null)
                    {
                        return;
                    }
                    localGrid = cockpit.CubeGrid; // Store the parent CubeGrid of the controlled entity for re-use
					localGridControlledEntityCustomData = cockpit.CustomData;
					UpdateElapsedTimeDeltaTimerGHandler(); // Store deltaTime and reset deltaTimer

					// Update stored positions
                    localGridControlledEntityPosition = localGridControlledEntity.GetPosition();
					localGridPosition = localGrid.GetPosition();

					// Update stored matrices
					localGridControlledEntityMatrix = localGridControlledEntity.WorldMatrix;
					localGridMatrix = localGrid.WorldMatrix;

					// Update stored velocities and speed
					localGridVelocity = GetEntityCubeGridVelocity(localGridControlledEntity);
					localGridVelocityAngular = GetEntityCubeGridVelocityAngular(localGridControlledEntity);
					localGridSpeed = localGridVelocity.Length();

					// Update stored names
					localGridControlledEntityName = localGridControlledEntity.DisplayName;
					localGridControlledEntityCustomName = cockpit.CustomName;

                    localGridName = localGrid.DisplayName;

					CalculateLocalGridAndSubgridHydrogenTime();
				} 
				else 
				{
					// Reset values if the entity is not available.
					localGridVelocity = Vector3D.Zero;
					localGridControlledEntityPosition = Vector3D.Zero;
					localGridSpeed = 0f;
					localGridControlledEntityName = " ";
				}
			}
		}

		/// <summary>
		/// Checks if the local player is currently controlling a grid entity
		/// </summary>
		/// <returns></returns>
		public bool IsLocalPlayerControllingGrid()
		{
			// Returns true if the controlled object is a ship controller.
			return MyAPIGateway.Session.ControlledObject is IMyShipController;
		}

		/// <summary>
		/// Retrieves the current entity controlled by the local player, a cockpit or seat for eg
		/// </summary>
		/// <returns></returns>
		public IMyEntity LocalPlayerEntity()
		{
			IMyControllableEntity controlledEntity = MyAPIGateway.Session.ControlledObject;
			// Return the entity if available; otherwise, null.
			return controlledEntity?.Entity;
		}

		/// <summary>
		/// Retrieves the linear velocity of the CubeGrid the entity belongs to, if entity is an IMyCockpit block
		/// </summary>
		/// <param name="entity"></param>
		/// <returns></returns>
		public Vector3D GetEntityCubeGridVelocity(IMyEntity entity)
		{
			if (entity == null) 
			{
                return Vector3D.Zero; // Return zero velocity if the entity is null.
            }

            // Attempt to cast the entity to IMyCockpit if it's part of a cockpit.
            IMyCockpit cockpit = entity as IMyCockpit;
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

        /// <summary>
        /// Retrieves the angular velocity of the CubeGrid the entity belongs to, if entity is an IMyCockpit block
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public Vector3D GetEntityCubeGridVelocityAngular(IMyEntity entity)
		{
			if (entity == null) 
			{
                return Vector3D.Zero; // Return zero velocity if the entity is null.
            }

            // Attempt to cast the entity to IMyCockpit if it's part of a cockpit.
            IMyCockpit cockpit = entity as IMyCockpit;
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


        

		//==============DAMAGE HANDLER===========================================================
		//private Queue<IMySlimBlock> blocksToCheck = new Queue<IMySlimBlock>();
		private HashSet<IMySlimBlock> damageHandlerMonitoredBlocks = new HashSet<IMySlimBlock>();
		public IMyCubeGrid damageHandlerCurrentGrid;

		/// <summary>
		/// Should only be called once upon loading onto a grid to initialize the event handlers that monitor for damage changes.
		/// </summary>
		public void InitializeLocalGridDamageHandler()
		{
            IMyCockpit cockpit = MyAPIGateway.Session.ControlledObject as Sandbox.ModAPI.IMyCockpit;
			if (cockpit == null) 
			{
                return;
            }
			IMyCubeGrid grid = cockpit.CubeGrid;

			if (grid != null && grid != damageHandlerCurrentGrid)
			{
				// Detach from the previous grid
				DetachDamageHandlerFromGrid();
				// Attach to the new grid
				AttachDamageHandlerToGrid(grid);
			}
		}


		private void AttachDamageHandlerToGrid(IMyCubeGrid grid)
		{
			// 2025-07-24 This appears currently like the only useful component is attaching the event handler to OnBlockFunctionalChanged. 
			// Otherwise it enques each block in the grid to the blocksToCheck Queue, which when CheckNextBlock is called will check a block and de-queue then re-queue it. But what is currently done with
			// that is nothing, it calculates a damage differential and then does nothing with it...?
			// Now monitoredBlocks IS used, when we detach from a grid later we remove the event handler on each block in that list. 
			// I suspect the old way was periodic integrity check but the original author added event handlers instead and didn't remove the old queing of blocks?
			var blocks = new List<IMySlimBlock>();
			grid.GetBlocks(blocks);

			foreach (var block in blocks)
			{
				if (block.FatBlock != null)
				{
					block.ComponentStack.IsFunctionalChanged += OnBlockFunctionalChanged;
					damageHandlerMonitoredBlocks.Add(block);
				}
				//blocksToCheck.Enqueue(block);  // Enqueue all blocks for periodic integrity check
			}
            damageHandlerCurrentGrid = grid;
        }

		private void DetachDamageHandlerFromGrid()
		{
			foreach (var block in damageHandlerMonitoredBlocks)
			{
				block.ComponentStack.IsFunctionalChanged -= OnBlockFunctionalChanged;
			}
			damageHandlerMonitoredBlocks.Clear();
			//blocksToCheck.Clear();
			damageHandlerCurrentGrid = null;
		}

		private void OnBlockFunctionalChanged()
		{
			//MyAPIGateway.Utilities.ShowMessage("Damage", "A block on your grid has been destroyed.");
			damageAmount += 1;
		}

		//private void CheckNextBlock()
		//{
		//	if (blocksToCheck.Count > 0)
		//	{
		//		IMySlimBlock block = blocksToCheck.Dequeue();
		//		blocksToCheck.Enqueue(block);  // Re-enqueue the block for continuous checking

		//		if (block.MaxIntegrity > block.Integrity)
		//		{
		//			float integrityDif = block.Integrity/block.MaxIntegrity;
		//			//damageAmount += (1-integrityDif)*0.01f;

		//			//string displayName = block.BlockDefinition.DisplayNameText;
		//			//MyAPIGateway.Utilities.ShowMessage(displayName, Convert.ToString(Math.Round(integrityDif*100)) + "%");
		//		}
		//	}
		//}
			
		/// <summary>
		/// Returns the current damage amount and re-sets it to 0. Ie. stores the current value, resets global var to 0, then returns current value stored.
		/// </summary>
		/// <returns></returns>
		public double GetDamageAmount()
		{
			double value = damageAmount;
			damageAmount = 0;
			return value;
		}

		

		/// <summary>
		/// Calculate the grid power produced vs consumed. For batteries we use Current Output as consumed and Max Output as produced. For power producers (reactors, solar) we use Current Output as consumed and Max Output as produced.
		/// </summary>
		/// <param name="grid"></param>
		/// <returns>The percentage of power being used currently vs max total capable</returns>
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

			localGridPowerProduced = totalPowerProduced;
			localGridPowerConsumed = totalPowerConsumed;
			localGridPowerStored = currentCharge;
			localGridPowerStoredMax = maxCharge;

			// Estimate time remaining
			float timeRemaining = currentCharge / totalPowerConsumed;
			localGridPowerHoursRemaining = timeRemaining;

			//MyAPIGateway.Utilities.ShowMessage("Debug", $"Total Produced: {totalPowerProduced}, Total Consumed: {totalPowerConsumed}, Usage: {powerUsagePercentage}%");
			return powerUsagePercentage;
		}


		public double H2powerSeconds;
		private double HydrogenThrusterConsumptionRate = 0;
		private double remainingH2 = 0;
		private double remainingH2_prev = 0;
		public float H2Ratio = 0;

        //============ H 2 =========================================================

		/// <summary>
		/// Checks if an IMyGasTank block has capacity > 0, and if DetailedInfo contains "Hydrogen", or display name contains "Hydrogen", or block definition subtype name contains "Hydrogen"
		/// </summary>
		/// <param name="tank"></param>
		/// <returns></returns>
        bool IsHydrogenTank(IMyGasTank tank)
        {
			// If a block can store something and has a capacity, and it mentions hydrogen, assume it's a hydrogen tank. 
            return tank.Capacity > 0 && (tank.DetailedInfo.Contains("Hydrogen") || tank.DefinitionDisplayNameText.Contains("Hydrogen") || tank.BlockDefinition.SubtypeName.Contains("Hydrogen"));
        }

        private void CalculateLocalGridAndSubgridHydrogenTime()
		{
			var grids = new List<IMyCubeGrid>();

			// Use the below logic to calculate the hydrogen capacity of the grid the player controlls and any attached subgrids. 
			MyAPIGateway.GridGroups.GetGroup(MyAPIGateway.Session.LocalHumanPlayer.Controller.ControlledEntity.Entity.GetTopMostParent() as IMyCubeGrid, GridLinkTypeEnum.Logical, grids);

			double totalCapacity = 0;
			double currentHydrogen = 0;
			double totalConsumptionRate = 0;

			// Loop through grid and subgrids
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
			if (remainingH2 != remainingH2_prev) 
			{
				// If we have lower currentHydrogen than we did last check, calculate consumption based on time elapsed.
                HydrogenThrusterConsumptionRate = (remainingH2_prev - remainingH2) / deltaTimeSinceLastTick;
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

