using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using static EliDangHUD.CircleRenderer;
using static VRageRender.Utils.MyWingedEdgeMesh;

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
		/// We find the entity the player is controlling and store it here, which is a cockpit or control block (or perhaps even a passenger seat?) This is an IMyCockpit entity only.
		/// </summary>
		public IMyCockpit localGridControlledEntity;

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

		public float localGridCurrentIntegrity = 0f;

		public float localGridMaxIntegrity = 0f;

        /// <summary>
        /// Stopwatch used for calculating elapsed time since last check performed, stored in deltaTime as a double
        /// </summary>
        private Stopwatch timerGameTickDelta = new Stopwatch ();

		/// <summary>
		/// Stores the elapsed time since last check was performed, calculated by deltaTimer and set by UpdateElapsedTimeDeltaTimer(). In theory we would be doing one check per tick, storing elapsed time, and re-setting the timer
		/// and not re-setting it multiple times per tick.
		/// </summary>
		private double deltaTimeSinceLastTick = 0;

		// New idea is to consolidate everything into gHandler, we will loop through all blocks once and store them into dictionaries by type.
		// Then OnBlockAdded or OnBlockRemoved event handlers at the grid level we can add or remove from dictionaries as needed.
		// We can also add OnDamage handlers for each block to track damage changes. Especially required if we use Slices instead of individual blocks so we can separate out damaged blocks from undamaged slices.
		// And we can add event handlers for custom name changes and custom data changes if they are holo tables or cockpits/seats. 

		// IMPORTANT: must handle IMyCubeGrid.OnGridSplit = triggered when grids separate.
		// IMyCubeGrid.OnGridMerge = triggered when grids merge(e.g., merge blocks, welding).
		// For rotors/pistons/connectors = you usually check Block.OnClose(for detached heads) or use IMyMechanicalConnectionBlock’s TopGridChanged event.
		// Connectors: IMyShipConnector has IsConnectedChanged event. 
		// I need to give this some thought... on the one hand the idea of connecting a battery pack, or external tank, or external jump ring, exeternal reactor etc. is appealing.
		// On the other hand docking to a station/grid that has a huge number of tanks/batteries/reactors/jump drives would mean a lot more overhead. 
		// Yeah on second thought I think we should only handle the merges, not connectors/rotors/pistons. 

        public bool localGridBlocksInitialized = false;
        public Dictionary<Vector3I, IMySlimBlock> localGridAllBlocksDict = new Dictionary<Vector3I, IMySlimBlock>();
		public Dictionary<IMyComponentStack, Vector3I> localGridBlockComponentStacks = new Dictionary<IMyComponentStack, Vector3I>();
        public Dictionary<Vector3I, IMyGasTank> localGridHydrogenTanksDict = new Dictionary<Vector3I, IMyGasTank>();
		public Dictionary<Vector3I, IMyPowerProducer> localGridPowerProducersDict = new Dictionary<Vector3I, IMyPowerProducer>();
		public Dictionary<Vector3I, IMyBatteryBlock> localGridBatteriesDict = new Dictionary<Vector3I, IMyBatteryBlock>();
		public Dictionary<Vector3I, IMyJumpDrive> localGridJumpDrivesDict = new Dictionary<Vector3I, IMyJumpDrive>(); // TODO check if the FrameShiftDrive is a subtype of this or new block type? Want support for that mod over time. 
		public Dictionary<Vector3I, IMyTerminalBlock> localGridEligibleTerminals = new Dictionary<Vector3I, IMyTerminalBlock>();
		public Dictionary<Vector3I, IMyTerminalBlock> localGridRadarTerminals = new Dictionary<Vector3I, IMyTerminalBlock>();
		public Dictionary<Vector3I, IMyTerminalBlock> localGridHologramTerminals = new Dictionary<Vector3I, IMyTerminalBlock>();
		
		// TODO handle the custom data of the terminals eligible as a dictionary as well. 

		// TODO add shield generators from shield mods? Midnight something or other was a new one I saw?

		public bool localGridClustersNeedRefresh = false;
		public bool localGridClusterSlicesNeedRefresh = false;


		// I may not leave this in the gHandler...
		public bool targetGridBlocksInitialized = false;
        public Dictionary<Vector3I, IMySlimBlock> targetGridAllBlocksDict = new Dictionary<Vector3I, IMySlimBlock>();


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
			ResetLocalGrid();
		}

        private void OnLocalBlockAdded(VRage.Game.ModAPI.IMySlimBlock block)
        {
			if (localGridBlocksInitialized && localGrid != null) // Safety check
			{
				
                localGridAllBlocksDict[block.Position] = block;
				localGridBlockComponentStacks[block.ComponentStack] = block.Position;
                block.ComponentStack.IntegrityChanged += OnBlockIntegrityChanged;
				if (block.FatBlock is IMyTerminalBlock terminal)
				{
					localGridEligibleTerminals[block.Position] = terminal;
					// If for some reason already tagged (welding/merging existing block?) add them to appropriate Dict. 
					if (terminal.CustomName.Contains("[ELI_LOCAL]"))
					{
						localGridHologramTerminals[block.Position] = terminal;
					}
					if (terminal.CustomName.Contains("[ELI_HOLO]"))
                    {
						localGridRadarTerminals[block.Position] = terminal;
                    }
                }
                if (block.FatBlock is IMyGasTank tank)
				{
					localGridHydrogenTanksDict[block.Position] = tank;
				}
				else if (block.FatBlock is IMyPowerProducer producer)
				{
					localGridPowerProducersDict[block.Position] = producer;
                }
				else if (block.FatBlock is IMyBatteryBlock battery)
				{
					localGridBatteriesDict[block.Position] = battery;
                }
				else if (block.FatBlock is IMyJumpDrive jumpDrive)
				{
					localGridJumpDrivesDict[block.Position] = jumpDrive;
                }
                localGridCurrentIntegrity += block.Integrity;
                localGridMaxIntegrity += block.MaxIntegrity;

                localGridClustersNeedRefresh = true; // In case we are using block clusters instead of slices
				localGridClusterSlicesNeedRefresh = true; // In case we are using cluster slices instead of block clusters
            }
        }

        private void OnLocalBlockRemoved(VRage.Game.ModAPI.IMySlimBlock block)
        {
			if (localGridBlocksInitialized && localGrid != null) // Safety check
			{
                block.ComponentStack.IntegrityChanged -= OnBlockIntegrityChanged;
                localGridBlockComponentStacks.Remove(block.ComponentStack);
				localGridAllBlocksDict.Remove(block.Position);

                if (block.FatBlock is IMyTerminalBlock terminal)
                {
					localGridEligibleTerminals.Remove(terminal.Position);
					localGridHologramTerminals.Remove(terminal.Position);
					localGridRadarTerminals.Remove(terminal.Position); 
                }
                if (block.FatBlock is IMyGasTank tank)
				{
					localGridHydrogenTanksDict.Remove(tank.Position);
				}
				else if (block.FatBlock is IMyPowerProducer producer)
				{
					localGridPowerProducersDict.Remove(producer.Position);
                }
				else if (block.FatBlock is IMyBatteryBlock battery)
				{
					localGridBatteriesDict.Remove(battery.Position);
                }
				else if (block.FatBlock is IMyJumpDrive jumpDrive)
				{
					localGridJumpDrivesDict.Remove(jumpDrive.Position);
                }
				localGridCurrentIntegrity -= block.Integrity;
				localGridMaxIntegrity -= block.MaxIntegrity;

				localGridClustersNeedRefresh = true; // In case we are using block clusters instead of slices
                localGridClusterSlicesNeedRefresh = true; // In case we are using cluster slices instead of block clusters
            }
        }

        private void OnBlockIntegrityChanged(IMyComponentStack stack, float oldIntegrity, float newIntegrity)
        {
			if (localGridBlocksInitialized && localGrid != null) // Safety Check
			{
				IMySlimBlock block = null;
				Vector3I stackPositionKey = Vector3I.Zero;
				if (localGridBlockComponentStacks.TryGetValue(stack, out stackPositionKey))
				{
					block = localGridAllBlocksDict[stackPositionKey];
					if (block != null) 
					{
                        localGridClustersNeedRefresh = true; // In case we are using block clusters instead of slices
                        localGridClusterSlicesNeedRefresh = true; // In case we are using cluster slices instead of block clusters
                    }
				}
			}
        }


        public void InitializeLocalGrid()
        {
            if (localGrid != null) // Safety check
            {
				// Process blocks on grid
				InitializeLocalGridBlocks();

				// Add Event Handlers
				localGrid.OnBlockAdded += OnLocalBlockAdded;
				localGrid.OnBlockRemoved += OnLocalBlockRemoved;
            }
        }

        public void ResetLocalGrid()
		{
			if (localGrid != null) // Safety check
			{
				// Remove event handlers
				foreach (MyComponentStack componentStack in localGridBlockComponentStacks.Keys) 
				{
					componentStack.IntegrityChanged -= OnBlockIntegrityChanged;
				}
                localGrid.OnBlockAdded -= OnLocalBlockAdded;
                localGrid.OnBlockRemoved -= OnLocalBlockRemoved;
            }

			// Local grid and controlled entity if applicable
			localGrid = null;
			localGridControlledEntity = null;

			// Local grid blocks
			localGridAllBlocksDict.Clear();
			localGridBlockComponentStacks.Clear();
			localGridHydrogenTanksDict.Clear();
			localGridPowerProducersDict.Clear();
			localGridBatteriesDict.Clear();
			localGridJumpDrivesDict.Clear();
			localGridEligibleTerminals.Clear();
			localGridHologramTerminals.Clear();
			localGridRadarTerminals.Clear();
            localGridBlocksInitialized = false;

			// Local Grid vars
			localGridCurrentIntegrity = 0f;
			localGridMaxIntegrity = 0f;

			localGridPowerConsumed = 0f;
			localGridPowerHoursRemaining = 0f;
			localGridPowerProduced = 0f;
			localGridPowerStored = 0f;
			localGridPowerStoredMax = 0f;

			localGridVelocity = Vector3D.Zero;
            localGridVelocityAngular = Vector3D.Zero;
			localGridSpeed = 0f;

            // Target grid (if no local grid implies no target grid possible)
            targetGridAllBlocksDict.Clear();
            targetGridBlocksInitialized = false;
        }

		public void CheckForLocalGrid()
		{
			// Check if the player is currently controlling a grid entity, if so it becomes the local grid.
            IsPlayerControlling = IsLocalPlayerControllingGrid();
			if (IsPlayerControlling)
			{
				// Retrieve the current entity being controlled.
				localGridControlledEntity = GetLocalPlayerControlledEntity(); // Store the controlled entity
				if (localGridControlledEntity != null)
				{
					IMyCockpit cockpit = localGridControlledEntity;

					// At this point we know the player is controlling a seat that belongs to a grid entity or IsPlayerControlling would be false (remote control/no grid = false)
					// We also know that they are controlling SOME entity or localGridControlledEntity would be null.
					// So now we are checking if it is a cockpit block versus some seat that can't control the grid?
					if (localGrid != null && localGrid != cockpit.CubeGrid)
					{
						// If the current grid we are controlling is not the same as the last local grid we were a part of we need to re-init everything.
						ResetLocalGrid();

						// We just reset everything so we re-store the localGridControlledEntity and the localGrid it belongs to.
						localGridControlledEntity = cockpit;
						localGrid = cockpit.CubeGrid;
					}
					else 
					{
                        localGrid = localGridControlledEntity.CubeGrid; // Store the parent CubeGrid of the controlled entity
                    }
				}
				else 
				{
					// If somehow we are controlling an entity, which checks if it is IMyCockpit, but then fail to get that entity as IMyCockpit we reset everything. This should be redundant. 
					ResetLocalGrid();
                }
			}
			else
			{
				// If not directly controlling a grid we find out if the player is inside the AABB of a grid, and if inside multiple AABB we use the nearest one. 
				localGridControlledEntity = null;
				IMyCubeGrid nearestGrid = GetNearestGridToPlayer();
				if (nearestGrid == null)
				{
					// If no nearest grid then we re-set everything
					ResetLocalGrid();
				}
				else if (localGrid != null && localGrid != nearestGrid) 
				{
                    // If the current grid we are controlling is not the same as the last local grid we were a part of we need to re-init everything.
                    ResetLocalGrid();
					localGrid = nearestGrid;
				}
            }
        }

        private void InitializeLocalGridBlocks()
        {
            if (localGrid == null)
            {
                return;
            }

            Dictionary<Vector3I, BlockTracker> blockInfo = new Dictionary<Vector3I, BlockTracker>();

            List<VRage.Game.ModAPI.IMySlimBlock> blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
            localGrid.GetBlocks(blocks);

            foreach (VRage.Game.ModAPI.IMySlimBlock block in blocks)
            {
                localGridAllBlocksDict[block.Position] = block;
                localGridBlockComponentStacks[block.ComponentStack] = block.Position;
                block.ComponentStack.IntegrityChanged += OnBlockIntegrityChanged;
                if (block.FatBlock is IMyTerminalBlock terminal)
                {
                    localGridEligibleTerminals[block.Position] = terminal;
                    // If for some reason already tagged (welding/merging existing block?) add them to appropriate Dict. 
                    if (terminal.CustomName.Contains("[ELI_LOCAL]"))
                    {
                        localGridHologramTerminals[block.Position] = terminal;
                    }
                    if (terminal.CustomName.Contains("[ELI_HOLO]"))
                    {
                        localGridRadarTerminals[block.Position] = terminal;
                    }
                }
                if (block.FatBlock is IMyGasTank tank)
                {
                    localGridHydrogenTanksDict[block.Position] = tank;
                }
                else if (block.FatBlock is IMyPowerProducer producer)
                {
                    localGridPowerProducersDict[block.Position] = producer;
					// TODO handle updating power like integrity on initialize instead of only on repeating update
                }
                else if (block.FatBlock is IMyBatteryBlock battery)
                {
                    localGridBatteriesDict[block.Position] = battery;
                    // TODO handle updating power like integrity on initialize instead of only on repeating update
                }
                else if (block.FatBlock is IMyJumpDrive jumpDrive)
                {
                    localGridJumpDrivesDict[block.Position] = jumpDrive;
                }

				localGridCurrentIntegrity += block.Integrity;
				localGridMaxIntegrity += block.MaxIntegrity;
            }
        }

        public void UpdateLocalGrid() 
		{ 
		
		}


        /// <summary>
        /// Updates local grid information, to be called each game tick to update stored positions, velocities, names, and calculate remaining hydrogen based on current consumption
        /// </summary>
        public void UpdateLocalGridOLD()
		{
			// Check if the local player is controlling a grid entity.
			IsPlayerControlling = IsLocalPlayerControllingGrid();

			if (IsPlayerControlling) 
			{
				// Retrieve the current entity being controlled.
				localGridControlledEntity = GetLocalPlayerControlledEntity(); // Store the controlled entity for re-use
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

					if (!localGridHydrogenTanksInitialized)
					{
						InitializeLocalGridAndSubgridHydrogenTanks();
                    }
					else 
					{
                        CalculateLocalGridAndSubgridHydrogenTime();
                    }
						
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
            IMyShipController controlledObj = MyAPIGateway.Session?.ControlledObject as IMyShipController;
			if (controlledObj == null)
			{ 
				return false; 
			}

            // Check if this is a Remote Control block
            if (controlledObj is IMyRemoteControl)
            { 
				return false;
			}

            // Get player's character
            IMyCharacter character = MyAPIGateway.Session.Player?.Character;
			if (character == null)
			{ 
				return false; 
			}

            // If the player's character is not in the same grid, it means remote control
            IMyCubeGrid charGrid = character.Parent as IMyCubeGrid;
			if (charGrid != null && charGrid != controlledObj.CubeGrid)
			{ 
				return false; 
			}

			IMyCockpit cockpit = controlledObj as IMyCockpit;
			if (cockpit == null) 
			{
				return false;
			}
            
            return true; // Physically sitting in this grid
		}

        /// <summary>
        /// Get and return the nearest grid to the player, returns null if none nearby or player not found
        /// </summary>
        private IMyCubeGrid GetNearestGridToPlayer()
        {
            IMyCharacter character = MyAPIGateway.Session.Player?.Character;
            if (character == null)
            {
                return null;
            }

            Vector3D playerPos = character.GetPosition();
            BoundingSphereD sphere = new BoundingSphereD(playerPos, 100); // Get all grids within 100m of the player
            List<VRage.Game.Entity.MyEntity> entities = new List<VRage.Game.Entity.MyEntity>();
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, entities); // This should use the AABB (bounding box) of the grid, so if even a corner of the AABB is in the sphere it returns it.

            VRage.Game.ModAPI.IMyCubeGrid nearestGrid = null;

            if (entities == null)
            {
                return null;
            }
			if (entities.Count == 1)
			{
				MyEntity entity = entities[0];
				VRage.Game.ModAPI.IMyCubeGrid grid = entity as VRage.Game.ModAPI.IMyCubeGrid;
				if (grid?.Physics == null || grid.MarkedForClose)
				{
                    return null;
                }
				if (grid.WorldAABB.Contains(playerPos) == ContainmentType.Disjoint)
				{
                    return null;
                }
				nearestGrid = grid;
			}
			else 
			{
                double closestDistSqr = double.MaxValue;
                foreach (MyEntity entity in entities)
                {
                    VRage.Game.ModAPI.IMyCubeGrid grid = entity as VRage.Game.ModAPI.IMyCubeGrid;
                    if (grid?.Physics == null || grid.MarkedForClose)
                    {
                        continue;
                    }
                    if (grid.WorldAABB.Contains(playerPos) == ContainmentType.Disjoint)
                    {
                        continue; // Not inside or intersecting this grid's AABB
                    }
                    double distSqr = Vector3D.DistanceSquared(playerPos, grid.WorldAABB.Center);
                    if (distSqr < closestDistSqr)
                    {
                        closestDistSqr = distSqr;
                        nearestGrid = grid;
                    }
                }
            }
			return nearestGrid;
        }

        /// <summary>
        /// Retrieves the current entity controlled by the local player, a cockpit or seat for eg. Only returns IMyCockpit entities to alleviate all the extra casts to IMyCockpit the original author was doing later on.
        /// </summary>
        /// <returns></returns>
        public IMyCockpit GetLocalPlayerControlledEntity()
		{
			VRage.Game.ModAPI.Interfaces.IMyControllableEntity controlledEntity = MyAPIGateway.Session.ControlledObject;
			IMyCockpit cockpit = controlledEntity?.Entity as IMyCockpit;
			if (cockpit != null)
			{
				return cockpit;
			}
			else
			{
				return null;
			}
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

		private void InitializeLocalGridAndSubgridHydrogenTanks() 
		{
            List<IMyCubeGrid> grids = new List<IMyCubeGrid>();

            // Use the below logic to calculate the hydrogen capacity of the grid the player controlls and any attached subgrids. 
            MyAPIGateway.GridGroups.GetGroup(MyAPIGateway.Session.LocalHumanPlayer.Controller.ControlledEntity.Entity.GetTopMostParent() as IMyCubeGrid, GridLinkTypeEnum.Logical, grids);

            double totalCapacity = 0;
            double currentHydrogen = 0;
            double totalConsumptionRate = 0;

            // Loop through grid and subgrids
            foreach (IMyCubeGrid grid in grids)
            {
                List<IMySlimBlock> blocks = new List<IMySlimBlock>();
                grid.GetBlocks(blocks, block => block.FatBlock is IMyGasTank);

                foreach (IMySlimBlock block in blocks)
                {
                    IMyGasTank tank = block.FatBlock as IMyGasTank;
                    if (tank.IsWorking && IsHydrogenTank(tank))
                    {
						localGridHydrogenTanksDict[block.Position] = tank;
                        totalCapacity += tank.Capacity;
                        currentHydrogen += tank.Capacity * tank.FilledRatio;
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
        private void CalculateLocalGridAndSubgridHydrogenTime()
		{
			double totalCapacity = 0;
			double currentHydrogen = 0;
			double totalConsumptionRate = 0;

			foreach (KeyValuePair<Vector3I, IMyGasTank> blockKeyPair in localGridHydrogenTanksDict)
			{
				IMyGasTank tank = blockKeyPair.Value;
				if(tank.IsWorking && IsHydrogenTank(tank))
                {
					totalCapacity += tank.Capacity;
					currentHydrogen += tank.Capacity * tank.FilledRatio;
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

