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

        public double hologramScale = 0.0075;//0.0125;
        public double hologramScaleFactor = 10;

        public MatrixD localHologramScalingMatrix = MatrixD.Identity;

        public double localGridHydrogenRemainingSeconds;
        private double localGridHydrogenConsumptionRate = 0;
        private double localGridRemainingHydrogen = 0;
        private double localGridRemainingHydrogenPrevious = 0;
        public float localGridHydrogenFillRatio = 0;

        public double localGridOxygenRemainingSeconds;
        private double localGridOxygenConsumptionRate = 0;
        private double localGridRemainingOxygen = 0;
        private double localGridRemainingOxygenPrevious = 0;
        public float localGridOxygenFillRatio = 0;

        /// <summary>
        /// Stopwatch used for calculating elapsed time since last check performed, stored in deltaTime as a double
        /// </summary>
        private Stopwatch timerGameTickDelta = new Stopwatch();

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

        public bool localGridInitialized = false;
        public bool localGridBlocksInitialized = false;
        public Dictionary<Vector3I, IMySlimBlock> localGridAllBlocksDict = new Dictionary<Vector3I, IMySlimBlock>();
        public Dictionary<int, Dictionary<Vector3I, IMySlimBlock>> localGridAllBlocksDictByFloor = new Dictionary<int, Dictionary<Vector3I, IMySlimBlock>>();
        public Dictionary<IMyComponentStack, Vector3I> localGridBlockComponentStacks = new Dictionary<IMyComponentStack, Vector3I>();
        public Dictionary<Vector3I, IMyGasTank> localGridHydrogenTanksDict = new Dictionary<Vector3I, IMyGasTank>();
        public Dictionary<Vector3I, IMyGasTank> localGridOxygenTanksDict = new Dictionary<Vector3I, IMyGasTank>();
        public Dictionary<Vector3I, IMyPowerProducer> localGridPowerProducersDict = new Dictionary<Vector3I, IMyPowerProducer>();
        public Dictionary<Vector3I, IMyBatteryBlock> localGridBatteriesDict = new Dictionary<Vector3I, IMyBatteryBlock>();
        public Dictionary<Vector3I, IMyJumpDrive> localGridJumpDrivesDict = new Dictionary<Vector3I, IMyJumpDrive>(); // TODO check if the FrameShiftDrive is a subtype of this or new block type? Want support for that mod over time. 
        public Dictionary<Vector3I, IMyTerminalBlock> localGridEligibleTerminals = new Dictionary<Vector3I, IMyTerminalBlock>();
        public Dictionary<Vector3I, IMyTerminalBlock> localGridRadarTerminals = new Dictionary<Vector3I, IMyTerminalBlock>();
        public Dictionary<Vector3I, IMyTerminalBlock> localGridHologramTerminals = new Dictionary<Vector3I, IMyTerminalBlock>();

        // TODO handle the custom data of the terminals eligible as a dictionary as well. 

        // TODO add shield generators from shield mods? Midnight something or other was a new one I saw?

        public bool localHologramScaleNeedsRefresh = false;
        public bool targetHologramScaleNeedsRefresh = false;

        public bool localGridClustersNeedRefresh = false;
        public Dictionary<Vector3I, BlockCluster> localGridBlockClusters = new Dictionary<Vector3I, BlockCluster>();
        public Dictionary<Vector3I, Vector3I> localGridBlockToClusterMap = new Dictionary<Vector3I, Vector3I>();

        public bool localGridClusterSlicesNeedRefresh = false;
        public List<ClusterBox> localGridBlockClusterSlices = new List<ClusterBox>();
        public Dictionary<Vector3I, int> localGridBlockToClusterSliceIndexMap = new Dictionary<Vector3I, int>(); // WIP, probably will re-factor the slice logic first before doing more.


        // I may not leave this in the gHandler...
        public bool targetGridBlocksInitialized = false;
        public IMyEntity currentTarget = null;
        public MatrixD targetHologramScalingMatrix = MatrixD.Identity;
        public Dictionary<Vector3I, IMySlimBlock> targetGridAllBlocksDict = new Dictionary<Vector3I, IMySlimBlock>();

        public ModSettings theSettings = new ModSettings();


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

        /// <summary>
        /// Checks if an IMyGasTank block has capacity > 0, and if DetailedInfo contains "Oxygen", or display name contains "Oxygen", or block definition subtype name contains "Oxygen"
        /// </summary>
        /// <param name="tank"></param>
        /// <returns></returns>
        bool IsOxygenTank(IMyGasTank tank)
        {
            // If a block can store something and has a capacity, and it mentions hydrogen, assume it's a hydrogen tank. 
            return tank.Capacity > 0 && (tank.DetailedInfo.Contains("Oxygen") || tank.DefinitionDisplayNameText.Contains("Oxygen") || tank.BlockDefinition.SubtypeName.Contains("Oxygen"));
        }

        private void OnLocalBlockAdded(VRage.Game.ModAPI.IMySlimBlock block)
        {
            if (localGridBlocksInitialized && localGrid != null) // Safety check
            {

                localGridAllBlocksDict[block.Position] = block;
                localGridAllBlocksDictByFloor[block.Position.Y][block.Position] = block;
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
                    if (IsHydrogenTank(tank))
                    {
                        localGridHydrogenTanksDict[block.Position] = tank;
                    }
                    if (IsOxygenTank(tank))
                    {
                        localGridOxygenTanksDict[block.Position] = tank;
                    }
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

                localHologramScaleNeedsRefresh = true;
                if (!theSettings.useClusterSlices) 
                {
                    localGridClustersNeedRefresh = true; // In case we are using block clusters instead of slices
                } 
                else
                {
                    // TODO might want to change this to only fill a list of Y levels that need to be rebuilt, and have that done in update instead of every time a block is removed. It could then fire every x seconds too. 
                    RebuildFloorClustersForY(localGridAllBlocksDictByFloor[block.Position.Y], block.Position.Y);
                    localGridClusterSlicesNeedRefresh = true; // In case we are using cluster slices instead of block clusters
                }
            }
        }

        private void OnLocalBlockRemoved(VRage.Game.ModAPI.IMySlimBlock block)
        {
            if (localGridBlocksInitialized && localGrid != null) // Safety check
            {
                block.ComponentStack.IntegrityChanged -= OnBlockIntegrityChanged;
                localGridBlockComponentStacks.Remove(block.ComponentStack);
                localGridAllBlocksDictByFloor[block.Position.Y].Remove(block.Position);
                localGridAllBlocksDict.Remove(block.Position);

                if (block.FatBlock is IMyTerminalBlock terminal)
                {
                    localGridEligibleTerminals.Remove(terminal.Position);
                    localGridHologramTerminals.Remove(terminal.Position);
                    localGridRadarTerminals.Remove(terminal.Position);
                }
                if (block.FatBlock is IMyGasTank tank)
                {
                    if (IsHydrogenTank(tank))
                    {
                        localGridHydrogenTanksDict.Remove(tank.Position);
                    }
                    if (IsOxygenTank(tank))
                    {
                        localGridOxygenTanksDict.Remove(tank.Position);
                    }
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

                localHologramScaleNeedsRefresh = true;
                if (!theSettings.useClusterSlices)
                {
                    localGridClustersNeedRefresh = true; // In case we are using block clusters instead of slices
                }
                else
                {
                    // TODO might want to change this to only fill a list of Y levels that need to be rebuilt, and have that done in update instead of every time a block is removed. It could then fire every x seconds too. 
                    RebuildFloorClustersForY(localGridAllBlocksDictByFloor[block.Position.Y], block.Position.Y);
                    localGridClusterSlicesNeedRefresh = true; // In case we are using cluster slices instead of block clusters
                }
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
                        float integrityDiff = newIntegrity - oldIntegrity;
                        localGridCurrentIntegrity += integrityDiff;

                        if (!theSettings.useClusterSlices)
                        {
                            // If we are using block clusters we can updaste the integrity of the cluster this block belongs to. 
                            Vector3I clusterKey = localGridBlockToClusterMap[block.Position];
                            if (localGridBlockToClusterMap.TryGetValue(block.Position, out clusterKey))
                            {
                                BlockCluster clusterEntry;
                                if (localGridBlockClusters.TryGetValue(clusterKey, out clusterEntry))
                                {
                                    clusterEntry.Integrity += integrityDiff;
                                }
                            }
                        }
                        else
                        {
                            if (block.MaxIntegrity != 0)
                            {
                                float oldRatio = oldIntegrity / block.MaxIntegrity;
                                float newRatio = newIntegrity / block.MaxIntegrity;
                                int oldSliceIndex = 0;
                                int newSliceIndex = 0;

                                if (oldRatio < 0.2f) 
                                {
                                    oldSliceIndex = 0;
                                }
                                if (oldRatio < 0.4f) 
                                {
                                    oldSliceIndex = 1;
                                }
                                if (oldRatio < 0.6f) 
                                {
                                    oldSliceIndex = 2;
                                }
                                if (oldRatio < 0.8f)
                                {
                                    oldSliceIndex = 3;
                                }
                                else 
                                {
                                    oldSliceIndex = 4;
                                }

                                if (newRatio < 0.2f)
                                {
                                    newSliceIndex = 0;
                                }
                                if (newRatio < 0.4f)
                                {
                                    newSliceIndex = 1;
                                }
                                if (newRatio < 0.6f)
                                {
                                    newSliceIndex = 2;
                                }
                                if (newRatio < 0.8f)
                                {
                                    newSliceIndex = 3;
                                }
                                else
                                {
                                    newSliceIndex = 4;
                                }

                                if (oldSliceIndex != newSliceIndex) 
                                {
                                    RebuildFloorClustersForY(localGridAllBlocksDictByFloor[block.Position.Y], block.Position.Y);
                                    localGridClusterSlicesNeedRefresh = true; // In case we are using cluster slices instead of block clusters
                                    // If we are using cluster slices we can trigger a refresh, since we want slices to contain only blocks of the same integrity. So if one in a slice gets damaged it
                                    // breaks off the slice it was a part of and becomes it's own slice. 
                                }
                            }
                        }
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

                localGridInitialized = true;
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
            localGridInitialized = false;

            // Local grid blocks
            localGridAllBlocksDict.Clear();
            localGridAllBlocksDictByFloor.Clear();
            localGridBlockComponentStacks.Clear();
            localGridHydrogenTanksDict.Clear();
            localGridOxygenTanksDict.Clear();
            localGridPowerProducersDict.Clear();
            localGridBatteriesDict.Clear();
            localGridJumpDrivesDict.Clear();
            localGridEligibleTerminals.Clear();
            localGridHologramTerminals.Clear();
            localGridRadarTerminals.Clear();
            localGridBlockToClusterMap.Clear();
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
                localGridAllBlocksDictByFloor[block.Position.Y][block.Position] = block;
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
            localGridBlocksInitialized = true;
        }

        public void UpdateLocalGrid()
        {
            if (localGrid != null) // Safety check
            {
                if (!localGridInitialized)
                {
                    InitializeLocalGrid();
                }

                UpdateElapsedTimeDeltaTimerGHandler();

                localGridVelocity = GetEntityCubeGridVelocity(localGridControlledEntity);
                localGridVelocityAngular = GetEntityCubeGridVelocityAngular(localGridControlledEntity);
                localGridSpeed = localGridVelocity.Length();

                UpdateLocalGridPower();
                UpdateLocalGridHydrogen();
                UpdateLocalGridOxygen();

                if (localHologramScaleNeedsRefresh)
                {
                    UpdateLocalGridScalingMatrix();
                }
                if (!theSettings.useClusterSlices && localGridClustersNeedRefresh)
                {
                    ClusterLocalBlocks();
                }
                else if (localGridClusterSlicesNeedRefresh)
                {
                    ClusterLocalBlocksIntoSlices();
                }
            }
        }

        public void UpdateLocalGridPower()
        {
            if (localGrid != null) // Safety check
            {
                float totalPowerProduced = 0f;
                float totalPowerConsumed = 0f;
                float currentCharge = 0f;
                float maxCharge = 0f;

                foreach (IMyPowerProducer producer in localGridPowerProducersDict.Values)
                {
                    if (producer.IsWorking)
                    {
                        totalPowerConsumed += producer.CurrentOutput;
                        totalPowerProduced += producer.MaxOutput;
                    }
                }
                foreach (IMyBatteryBlock battery in localGridBatteriesDict.Values)
                {
                    if (battery.IsWorking)
                    {
                        totalPowerConsumed += battery.CurrentOutput;
                        totalPowerProduced += battery.MaxOutput;
                        currentCharge += battery.CurrentStoredPower;
                        maxCharge += battery.MaxStoredPower;
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
            }
        }


        private void UpdateLocalGridHydrogen()
        {
            double totalHydrogenCapacity = 0;
            double currentHydrogen = 0;
            double totalHydrogenConsumptionRate = 0;

            foreach (IMyGasTank tank in localGridHydrogenTanksDict.Values)
            {
                if (tank.IsWorking && IsHydrogenTank(tank))
                {
                    totalHydrogenCapacity += tank.Capacity;
                    currentHydrogen += tank.Capacity * tank.FilledRatio;
                }
            }

            localGridRemainingHydrogen = currentHydrogen;
            if (localGridRemainingHydrogen != localGridRemainingHydrogenPrevious)
            {
                // If we have lower currentHydrogen than we did last check, calculate consumption based on time elapsed.
                localGridHydrogenConsumptionRate = (localGridRemainingHydrogenPrevious - localGridRemainingHydrogen) / deltaTimeSinceLastTick;
            }
            localGridRemainingHydrogenPrevious = currentHydrogen;

            double timeRemaining = totalHydrogenConsumptionRate > 0 ? (currentHydrogen * 1000) / totalHydrogenConsumptionRate : 0;
            timeRemaining = currentHydrogen / localGridHydrogenConsumptionRate;

            localGridHydrogenRemainingSeconds = timeRemaining;

            localGridHydrogenFillRatio = (float)(localGridRemainingHydrogen / totalHydrogenCapacity);
        }

        private void UpdateLocalGridOxygen()
        {
            double totalOxygenCapacity = 0;
            double currentOxygen = 0;
            double totalOxygenConsumptionRate = 0;

            foreach (IMyGasTank tank in localGridOxygenTanksDict.Values)
            {
                if (tank.IsWorking && IsOxygenTank(tank))
                {
                    totalOxygenCapacity += tank.Capacity;
                    currentOxygen += tank.Capacity * tank.FilledRatio;
                }
            }

            localGridRemainingOxygen = currentOxygen;
            if (localGridRemainingOxygen != localGridRemainingOxygenPrevious)
            {
                // If we have lower currentOxygen than we did last check, calculate consumption based on time elapsed.
                localGridOxygenConsumptionRate = (localGridRemainingOxygenPrevious - localGridRemainingOxygen) / deltaTimeSinceLastTick;
            }
            localGridRemainingOxygenPrevious = currentOxygen;

            double timeRemaining = totalOxygenConsumptionRate > 0 ? (currentOxygen * 1000) / totalOxygenConsumptionRate : 0;
            timeRemaining = currentOxygen / localGridOxygenConsumptionRate;

            localGridOxygenRemainingSeconds = timeRemaining;

            localGridOxygenFillRatio = (float)(localGridRemainingOxygen / totalOxygenCapacity);
        }

        public void UpdateLocalGridScalingMatrix()
        {
            double thicc = hologramScaleFactor / (localGrid.WorldVolume.Radius / localGrid.GridSize);
            localHologramScalingMatrix = MatrixD.CreateScale(hologramScale * thicc);
            localHologramScaleNeedsRefresh = false;
        }
        public void UpdateTargetGridScalingMatrix()
        {
            if (currentTarget != null)
            {
                VRage.Game.ModAPI.IMyCubeGrid currentTargetGrid = currentTarget as VRage.Game.ModAPI.IMyCubeGrid;
                double thicc = hologramScaleFactor / (currentTargetGrid.WorldVolume.Radius / currentTargetGrid.GridSize);
                targetHologramScalingMatrix = MatrixD.CreateScale(hologramScale * thicc);
                targetHologramScaleNeedsRefresh = false;
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



      
        public class BlockCluster
        {
            public float Integrity = 0f;
            public float MaxIntegrity = 0f;
        }

        public void ClusterLocalBlocks()
        {
            if (localGridAllBlocksDict == null || localGridAllBlocksDict.Count == 0)
            {
                return;
            }

            // Decide fidelity level
            int clusterSize = 1;
            int count = localGridAllBlocksDict.Count;

            if (count >= 60000)
            {
                clusterSize = 4;
            }
            else if (count >= 30000)
            {
                clusterSize = 3;
            }
            else if (count >= 15000)
            {
                clusterSize = 2;
            }

            localGridBlockClusters = new Dictionary<Vector3I, BlockCluster>(); // CLear it

            foreach (KeyValuePair<Vector3I, IMySlimBlock> blockKeyValuePair in localGridAllBlocksDict)
            {
                var blockPos = blockKeyValuePair.Key; // Grid position of this block
                var block = blockKeyValuePair.Value;

                // Compute which cluster this block belongs to
                var clusterPos = new Vector3I(
                    (blockPos.X / clusterSize) * clusterSize,
                    (blockPos.Y / clusterSize) * clusterSize,
                    (blockPos.Z / clusterSize) * clusterSize
                );

                // If we don’t already have a placeholder BlockTracker, add one
                if (localGridBlockClusters.ContainsKey(clusterPos))
                {
                    localGridBlockClusters[clusterPos].Integrity += block.Integrity;
                    localGridBlockClusters[clusterPos].MaxIntegrity += block.MaxIntegrity;
                    localGridBlockToClusterMap[blockPos] = clusterPos; // Map this block to its cluster position
                }
                else
                {
                    localGridBlockClusters[clusterPos] = new BlockCluster
                    {
                        Integrity = block.Integrity,
                        MaxIntegrity = block.MaxIntegrity
                    };
                    localGridBlockToClusterMap[blockPos] = clusterPos; // Map this block to its cluster position
                }
            }
            localGridClustersNeedRefresh = false;
        }

        // Old approach:
        //public void ClusterLocalBlocksIntoSlices()
        //{
        //    HashSet<Vector3I> visited = new HashSet<Vector3I>();
        //    localGridBlockClusterSlices = new List<ClusterBox>();

        //    // group blocks by Y level
        //    IEnumerable<IGrouping<int, KeyValuePair<Vector3I, IMySlimBlock>>> groupedByY = localGridAllBlocksDict.GroupBy(kvp => kvp.Key.Y);

        //    foreach (IGrouping<int, KeyValuePair<Vector3I, IMySlimBlock>> yGroup in groupedByY.OrderBy(g => g.Key))
        //    {
        //        int y = yGroup.Key;

        //        foreach (KeyValuePair<Vector3I, IMySlimBlock> kvp in yGroup)
        //        {
        //            Vector3I start = kvp.Key;

        //            if (visited.Contains(start))
        //            {
        //                continue;
        //            }
        //            ClusterBox cluster = ExpandXZCluster(start, y, localGridAllBlocksDict, visited);
        //            localGridBlockClusterSlices.Add(cluster);
        //        }
        //    }
        //}

        //private ClusterBox ExpandXZCluster(Vector3I start, int y, Dictionary<Vector3I, IMySlimBlock> blocks, HashSet<Vector3I> visited)
        //{
        //    Vector3I min = start;
        //    Vector3I max = start;

        //    // expand in +X
        //    while (true)
        //    {
        //        Vector3I next = new Vector3I(max.X + 1, y, max.Z);
        //        if (blocks.ContainsKey(next) && !visited.Contains(next))
        //        {
        //            max.X++;
        //        }
        //        else
        //        {
        //            break;
        //        }
        //    }

        //    // expand in +Z
        //    bool canExpandZ = true;
        //    while (canExpandZ)
        //    {
        //        int newZ = max.Z + 1;
        //        for (int x = min.X; x <= max.X; x++)
        //        {
        //            Vector3I pos = new Vector3I(x, y, newZ);
        //            if (!blocks.ContainsKey(pos) || visited.Contains(pos))
        //            {
        //                canExpandZ = false;
        //                break;
        //            }
        //        }
        //        if (canExpandZ) max.Z++;
        //    }

        //    // mark all blocks in this slice box
        //    ClusterBox cluster = new ClusterBox { Min = min, Max = max };

        //    for (int x = min.X; x <= max.X; x++)
        //    {
        //        for (int z = min.Z; z <= max.Z; z++)
        //        {
        //            Vector3I pos = new Vector3I(x, y, z);
        //            if (blocks.ContainsKey(pos))
        //            {
        //                visited.Add(pos);
        //            }
        //        }
        //    }

        //    return cluster;
        //}


        // New approach:
        public class ClusterBox
        {
            public Vector3I Min;
            public Vector3I Max;
            public List<IMySlimBlock> Blocks = new List<IMySlimBlock>();
            public int IntegrityBucket; // which bucket this cluster belongs to
        }

        public Dictionary<int, List<ClusterBox>> floorClusters = new Dictionary<int, List<ClusterBox>>();
        public Dictionary<Vector3I, ClusterBox> blockToClusterMap = new Dictionary<Vector3I, ClusterBox>();

        public void ClusterLocalBlocksIntoSlices()
        {
            floorClusters.Clear();
            blockToClusterMap.Clear();

            // Group blocks by Y level
            IEnumerable<IGrouping<int, KeyValuePair<Vector3I, IMySlimBlock>>> groupedByY = localGridAllBlocksDict.GroupBy(kvp => kvp.Key.Y);

            foreach (IGrouping<int, KeyValuePair<Vector3I, IMySlimBlock>> yGroup in groupedByY)
            {
                int y = yGroup.Key;

                // further group by integrity bucket
                var groupedByIntegrity = yGroup.GroupBy(kvp => GetIntegrityBucket(kvp.Value));

                List<ClusterBox> clustersThisFloor = new List<ClusterBox>();
                floorClusters[y] = clustersThisFloor;

                foreach (var bucketGroup in groupedByIntegrity)
                {
                    int bucket = bucketGroup.Key;
                    HashSet<Vector3I> visited = new HashSet<Vector3I>();

                    foreach (var kvp in bucketGroup)
                    {
                        Vector3I start = kvp.Key;

                        if (visited.Contains(start))
                            continue;

                        ClusterBox cluster = ExpandXZCluster(start, y, bucketGroup.ToDictionary(x => x.Key, x => x.Value), visited, bucket);
                        clustersThisFloor.Add(cluster);

                        // Map all blocks in this cluster to it
                        foreach (IMySlimBlock b in cluster.Blocks)
                        {
                            blockToClusterMap[b.Position] = cluster;
                        }
                    }
                }
            }
            localGridClusterSlicesNeedRefresh = false;
        }

        private ClusterBox ExpandXZCluster(Vector3I start, int y, Dictionary<Vector3I, IMySlimBlock> blocks, HashSet<Vector3I> visited, int bucket)
        {
            Vector3I min = start;
            Vector3I max = start;

            // expand in +X
            while (true)
            {
                Vector3I next = new Vector3I(max.X + 1, y, max.Z);
                if (blocks.ContainsKey(next) && !visited.Contains(next))
                    max.X++;
                else
                    break;
            }

            // expand in +Z
            bool canExpandZ = true;
            while (canExpandZ)
            {
                int newZ = max.Z + 1;
                for (int x = min.X; x <= max.X; x++)
                {
                    Vector3I pos = new Vector3I(x, y, newZ);
                    if (!blocks.ContainsKey(pos) || visited.Contains(pos))
                    {
                        canExpandZ = false;
                        break;
                    }
                }
                if (canExpandZ) max.Z++;
            }

            ClusterBox cluster = new ClusterBox { Min = min, Max = max, IntegrityBucket = bucket };

            // Add all blocks inside bounds
            for (int x = min.X; x <= max.X; x++)
            {
                for (int z = min.Z; z <= max.Z; z++)
                {
                    Vector3I pos = new Vector3I(x, y, z);
                    if (blocks.TryGetValue(pos, out IMySlimBlock block))
                    {
                        visited.Add(pos);
                        cluster.Blocks.Add(block);
                    }
                }
            }

            return cluster;
        }


        private int GetIntegrityBucket(IMySlimBlock block)
        {
            if (block.MaxIntegrity <= 0) return 0; // avoid div/0
            float ratio = block.Integrity / block.MaxIntegrity;
            if (ratio < 0.2f) return 0;
            if (ratio < 0.4f) return 1;
            if (ratio < 0.6f) return 2;
            if (ratio < 0.8f) return 3;
            return 4;
        }

        public void RebuildFloorClustersForY(Dictionary<Vector3I, IMySlimBlock> allBlocks, int y)
        {
            // Remove old clusters for this Y if present
            if (floorClusters.ContainsKey(y))
            {
                // Also clear block->cluster mappings for this floor
                foreach (var cluster in floorClusters[y])
                {
                    foreach (var b in cluster.Blocks)
                    {
                        blockToClusterMap.Remove(b.Position);
                    }
                }
                floorClusters.Remove(y);
            }

            // Collect all blocks at this Y
            var yBlocks = allBlocks
                .Where(kvp => kvp.Key.Y == y)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            if (yBlocks.Count == 0)
                return; // nothing left on this floor

            // Group by integrity bucket
            var groupedByIntegrity = yBlocks.GroupBy(kvp => GetIntegrityBucket(kvp.Value));

            List<ClusterBox> clustersThisFloor = new List<ClusterBox>();
            floorClusters[y] = clustersThisFloor;

            foreach (var bucketGroup in groupedByIntegrity)
            {
                int bucket = bucketGroup.Key;
                HashSet<Vector3I> visited = new HashSet<Vector3I>();

                var bucketDict = bucketGroup.ToDictionary(x => x.Key, x => x.Value);

                foreach (var kvp in bucketGroup)
                {
                    Vector3I start = kvp.Key;

                    if (visited.Contains(start))
                        continue;

                    ClusterBox cluster = ExpandXZCluster(start, y, bucketDict, visited, bucket);
                    clustersThisFloor.Add(cluster);

                    // Map all blocks in this cluster
                    foreach (IMySlimBlock b in cluster.Blocks)
                    {
                        blockToClusterMap[b.Position] = cluster;
                    }
                }
            }
        }


    }
}

