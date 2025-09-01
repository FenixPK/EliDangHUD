using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Components;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using static EliDangHUD.CircleRenderer;
using static VRage.Game.MyObjectBuilder_CurveDefinition;

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

        public ControlledEntityCustomData localGridControlledEntityCustomData;

        public string localGridControlledEntityLastCustomData;

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
        public bool isPlayerControlling;

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

        public float localGridPowerUsagePercentage;

        public float localGridJumpDrivePowerStoredPrevious;
        public float localGridJumpDrivePowerStoredMax;
        public float localGridJumpDrivePowerStored;
        public double localGridJumpDriveTimeToReady = 0;
        public double localGridGlitchAmount = 0;
        public double localGridGlitchAmountMin = 0;
        public double localGridGlitchAmountOverload = 0;

        /// <summary>
        /// Stores the result of dividing localGridPowerStored / localGridPowerConsumed. Ie at current rate of consumption how long will stored battery power last?
        /// </summary>
        public float localGridPowerHoursRemaining;

        public bool localGridHasPower;

        public float localGridCurrentIntegrity = 0f;
        public float localGridMaxIntegrity = 0f;

        public double hologramScale = 0.0075;//0.0125;
        public double hologramScaleFactor = 10;

        public MatrixD localHologramScalingMatrix = MatrixD.Identity;
        public MatrixD localHologramViewRotationCurrent = MatrixD.Identity;
        public MatrixD localHologramViewRotationGoal = MatrixD.Identity;
        public MatrixD localHologramFinalRotation = MatrixD.Identity;

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
        public double deltaTimeSinceLastTick = 0;

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
        public bool localGridControlledEntityInitialized = false; 
        public bool localGridBlocksInitialized = false;
        public bool localHologramScaleNeedsRefresh = false;
        public bool targetHologramScaleNeedsRefresh = false;
        
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
        public Dictionary<Vector3I, HoloRadarCustomDataTerminalPair> localGridRadarTerminalsData = new Dictionary<Vector3I, HoloRadarCustomDataTerminalPair>();
        public Dictionary<Vector3I, IMyTerminalBlock> localGridHologramTerminals = new Dictionary<Vector3I, IMyTerminalBlock>();
        public Dictionary<Vector3I, HologramCustomDataTerminalPair> localGridHologramTerminalsData = new Dictionary<Vector3I, HologramCustomDataTerminalPair>();
        public Dictionary<Vector3I, Vector4[]> localGridHologramTerminalPallets = new Dictionary<Vector3I, Vector4[]>();
        public Dictionary<Vector3I, MatrixD> localGridHologramTerminalRotations = new Dictionary<Vector3I, MatrixD>();
        // TODO add shield generators from shield mods? Midnight something or other was a new one I saw?

        public Dictionary<Vector3I, IMyRadioAntenna> localGridAntennasDict = new Dictionary<Vector3I, IMyRadioAntenna>();
        public bool localGridHasPassiveRadar = false;
        public bool localGridHasActiveRadar = false;
        public double localGridMaxPassiveRadarRange = 0.0;
        public double localGridMaxActiveRadarRange = 0.0;
        public double maxRadarRange = 50000;

        public Dictionary<Vector3I, BlockCluster> localGridBlockClusters = new Dictionary<Vector3I, BlockCluster>();
        public Dictionary<Vector3I, Vector3I> localGridBlockToClusterMap = new Dictionary<Vector3I, Vector3I>();
        public int localGridClusterSize = 1;
        public bool localGridClustersNeedRefresh = false;

        //public List<ClusterBox> localGridBlockClusterSlices = new List<ClusterBox>();
        //public Dictionary<Vector3I, int> localGridBlockToClusterSliceIndexMap = new Dictionary<Vector3I, int>(); // WIP, probably will re-factor the slice logic first before doing more.
        public Dictionary<int, List<ClusterBox>> localGridFloorClusterSlices = new Dictionary<int, List<ClusterBox>>();
        public Dictionary<int, Dictionary<Vector3I, int>> localGridBlockToFloorClusterSlicesMap = new Dictionary<int, Dictionary<Vector3I, int>>();
        public bool localGridClusterSlicesNeedRefresh = false;

        public double localGridHologramActivationTime = 0;
        public double localGridHologramBootUpAlpha = 1;

        public Vector4[] localGridHologramPalleteForClusterSlices;


        //--Target Grid--
        public bool targetGridInitialized = false;
        public bool targetGridBlocksInitialized = false;
        public IMyCubeGrid targetGrid = null;

        public float targetGridCurrentIntegrity = 0f;
        public float targetGridMaxIntegrity = 0f;

        public float targetGridJumpDrivePowerStoredPrevious;
        public float targetGridJumpDrivePowerStoredMax;
        public float targetGridJumpDrivePowerStored;
        public double targetGridJumpDriveTimeToReady = 0;

        public MatrixD targetHologramScalingMatrix = MatrixD.Identity;
        public MatrixD targetHologramViewRotationCurrent = MatrixD.Identity;
        public MatrixD targetHologramViewRotationGoal = MatrixD.Identity;
        public MatrixD targetHologramFinalRotation = MatrixD.Identity;

        public Dictionary<Vector3I, IMySlimBlock> targetGridAllBlocksDict = new Dictionary<Vector3I, IMySlimBlock>();
        public Dictionary<int, Dictionary<Vector3I, IMySlimBlock>> targetGridAllBlocksDictByFloor = new Dictionary<int, Dictionary<Vector3I, IMySlimBlock>>();
        public Dictionary<IMyComponentStack, Vector3I> targetGridBlockComponentStacks = new Dictionary<IMyComponentStack, Vector3I>();
        public Dictionary<Vector3I, IMyJumpDrive> targetGridJumpDrivesDict = new Dictionary<Vector3I, IMyJumpDrive>();
        public Dictionary<Vector3I, IMyRadioAntenna> targetGridAntennasDict = new Dictionary<Vector3I, IMyRadioAntenna>();
        public bool targetGridHasPassiveRadar = false;
        public bool targetGridHasActiveRadar = false;
        public double targetGridMaxPassiveRadarRange = 0.0;
        public double targetGridMaxActiveRadarRange = 0.0;

        public Dictionary<Vector3I, BlockCluster> targetGridBlockClusters = new Dictionary<Vector3I, BlockCluster>();
        public Dictionary<Vector3I, Vector3I> targetGridBlockToClusterMap = new Dictionary<Vector3I, Vector3I>();
        public int targetGridClusterSize = 1;
        public bool targetGridClustersNeedRefresh = false;

        public Dictionary<int, List<ClusterBox>> targetGridFloorClusterSlices = new Dictionary<int, List<ClusterBox>>();
        public Dictionary<int, Dictionary<Vector3I, int>> targetGridBlockToFloorClusterSlicesMap = new Dictionary<int, Dictionary<Vector3I, int>>();
        public bool targetGridClusterSlicesNeedRefresh = false;

        public double targetGridHologramActivationTime = 0;
        public double targetGridHologramBootUpAlpha = 1;

        public Vector4[] targetGridHologramPalleteForClusterSlices;

        //----------------

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
            ResetLocalGrid();
            ResetTargetGrid();
            Instance = null;
            base.UnloadData();  
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

        public int GetIntegrityIndex(float integrityRatio)
        {
            int sliceIndex = 0;
            if (integrityRatio < 0.2f)
            {
                sliceIndex = 0;
            }
            if (integrityRatio < 0.4f)
            {
                sliceIndex = 1;
            }
            if (integrityRatio < 0.6f)
            {
                sliceIndex = 2;
            }
            if (integrityRatio < 0.8f)
            {
                sliceIndex = 3;
            }
            else
            {
                sliceIndex = 4;
            }
            return sliceIndex;
        }

        private void OnLocalBlockAdded(VRage.Game.ModAPI.IMySlimBlock block)
        {
            if (localGridBlocksInitialized && localGrid != null) // Safety check
            {
                //MatrixD inverseMatrix = MatrixD.Invert(GetRotationMatrix(localGrid.WorldMatrix));
                //Vector3D blockWorldPosition;
                //Vector3D blockScaledInvertedPosition;
                //block.ComputeWorldCenter(out blockWorldPosition); // Gets world position for the center of the block
                //blockScaledInvertedPosition = Vector3D.Transform((blockWorldPosition - localGrid.WorldVolume.Center), inverseMatrix) / localGrid.GridSize; // set scaledPosition to be relative to the center of the grid, invert it, then scale it to block units.

                //GridBlock gridBlock = new GridBlock();
                //gridBlock.Block = block;
                //gridBlock.Position = blockScaledInvertedPosition;

                //localGridAllBlocksDict[block.Position] = gridBlock;
                localGridAllBlocksDict[block.Position] = block;
                Dictionary<Vector3I, IMySlimBlock> blockDictForFloor;
                if (!localGridAllBlocksDictByFloor.TryGetValue(block.Position.Y, out blockDictForFloor))
                {
                    blockDictForFloor = new Dictionary<Vector3I, IMySlimBlock>();
                    localGridAllBlocksDictByFloor[block.Position.Y] = blockDictForFloor;
                }
                blockDictForFloor[block.Position] = block;
                localGridBlockComponentStacks[block.ComponentStack] = block.Position;
                block.ComponentStack.IntegrityChanged += OnLocalBlockIntegrityChanged;

                IMyTerminalBlock terminal = block.FatBlock as IMyTerminalBlock;
                if (terminal != null)
                {
                    IMyCockpit cockpit = terminal as IMyCockpit;
                    IMyRadioAntenna antenna = terminal as IMyRadioAntenna;
                    if (cockpit == null && antenna == null)
                    {
                        // Don't add cockpits or antennas
                        localGridEligibleTerminals[block.Position] = terminal;
                        terminal.CustomNameChanged += OnTerminalCustomNameChanged;
                        terminal.CustomDataChanged += OnTerminalCustomDataChanged;
                        // If for some reason already tagged (welding/merging existing block?) add them to appropriate Dict. 
                        if (terminal.CustomName.Contains("[ELI_LOCAL]"))
                        {
                            localGridHologramTerminals[block.Position] = terminal;
                            HologramCustomData theData = InitializeCustomDataHologram(terminal);
                            HologramCustomDataTerminalPair thePair = new HologramCustomDataTerminalPair();
                            thePair.HologramCustomData = theData;
                            thePair.HologramCustomDataString = terminal.CustomData;
                            localGridHologramTerminalsData[block.Position] = thePair;
                            Vector4[] colorPallet = BuildIntegrityColors(theData.lineColor);
                            localGridHologramTerminalPallets[terminal.Position] = colorPallet;
                        }
                        if (terminal.CustomName.Contains("[ELI_HOLO]"))
                        {
                            localGridRadarTerminals[block.Position] = terminal;
                            HoloRadarCustomData theData = InitializeCustomDataHoloRadar(terminal);
                            HoloRadarCustomDataTerminalPair thePair = new HoloRadarCustomDataTerminalPair();
                            thePair.HoloRadarCustomData = theData;
                            thePair.HoloRadarCustomDataString = terminal.CustomData;
                            localGridRadarTerminalsData[block.Position] = thePair;
                        }
                    }
                    if (antenna != null) 
                    {
                        localGridAntennasDict.Add(antenna.Position, antenna);
                    }
                }

                IMyGasTank tank = block.FatBlock as IMyGasTank;
                if (tank != null)
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

                IMyPowerProducer producer = block.FatBlock as IMyPowerProducer;
                IMyBatteryBlock battery = block.FatBlock as IMyBatteryBlock;
                if (producer != null)
                {
                    localGridPowerProducersDict[block.Position] = producer;
                }
                else if (battery != null)
                {
                    localGridBatteriesDict[block.Position] = battery;
                }

                IMyJumpDrive jumpDrive = block.FatBlock as IMyJumpDrive;
                if (jumpDrive != null)
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
                    RebuildFloorClustersForGridFloor(block.Position.Y, ref localGridFloorClusterSlices, ref localGridBlockToFloorClusterSlicesMap, localGridAllBlocksDictByFloor);
                    //localGridClusterSlicesNeedRefresh = true; // In case we are using cluster slices instead of block clusters
                }
            }
        }

        private void OnLocalBlockRemoved(VRage.Game.ModAPI.IMySlimBlock block)
        {
            if (localGridBlocksInitialized && localGrid != null) // Safety check
            {
                block.ComponentStack.IntegrityChanged -= OnLocalBlockIntegrityChanged;
                localGridBlockComponentStacks.Remove(block.ComponentStack);
                localGridAllBlocksDictByFloor[block.Position.Y].Remove(block.Position);
                localGridAllBlocksDict.Remove(block.Position);

                IMyTerminalBlock terminal = block.FatBlock as IMyTerminalBlock;
                if (terminal != null)
                {
                    IMyCockpit cockpit = terminal as IMyCockpit;
                    IMyRadioAntenna antenna = terminal as IMyRadioAntenna;
                    if (cockpit == null && antenna == null)
                    {
                        // Only handle non-cockpits and non-antennas
                        terminal.CustomNameChanged -= OnTerminalCustomNameChanged;
                        terminal.CustomDataChanged -= OnTerminalCustomDataChanged;
                        localGridEligibleTerminals.Remove(terminal.Position);
                        localGridHologramTerminals.Remove(terminal.Position);
                        localGridRadarTerminals.Remove(terminal.Position);
                        localGridHologramTerminalsData.Remove(terminal.Position);
                        localGridHologramTerminalPallets.Remove(terminal.Position);
                        localGridRadarTerminalsData.Remove(terminal.Position);
                    }
                    if (antenna != null)
                    {
                        localGridAntennasDict.Remove(antenna.Position);
                    }
                }

                IMyGasTank tank = block.FatBlock as IMyGasTank;
                if (tank != null)
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

                IMyPowerProducer producer = block.FatBlock as IMyPowerProducer;
                IMyBatteryBlock battery = block.FatBlock as IMyBatteryBlock;
                if (producer != null)
                {
                    localGridPowerProducersDict.Remove(producer.Position);
                }
                else if (battery != null)
                {
                    localGridBatteriesDict.Remove(battery.Position);
                }

                IMyJumpDrive jumpDrive = block.FatBlock as IMyJumpDrive;
                if (jumpDrive != null)
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
                    RebuildFloorClustersForGridFloor(block.Position.Y, ref localGridFloorClusterSlices, ref localGridBlockToFloorClusterSlicesMap, localGridAllBlocksDictByFloor);
                    //localGridClusterSlicesNeedRefresh = true; // In case we are using cluster slices instead of block clusters
                }
            }
        }

        private void OnLocalBlockIntegrityChanged(IMyComponentStack stack, float oldIntegrity, float newIntegrity)
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
                                int oldSliceIndex = GetIntegrityIndex(oldRatio);
                                int newSliceIndex = GetIntegrityIndex(newRatio);

                                if (oldSliceIndex != newSliceIndex) 
                                {
                                    RebuildFloorClustersForGridFloor(block.Position.Y, ref localGridFloorClusterSlices, ref localGridBlockToFloorClusterSlicesMap, localGridAllBlocksDictByFloor);
                                    //localGridClusterSlicesNeedRefresh = true; // In case we are using cluster slices instead of block clusters
                                    // If we are using cluster slices we can trigger a refresh, since we want slices to contain only blocks of the same integrity. So if one in a slice gets damaged it
                                    // breaks off the slice it was a part of and becomes it's own slice. 
                                }
                            }
                        }
                    }
                }
            }
        }

        private void OnTerminalCustomNameChanged(IMyTerminalBlock terminal)
        {
            if (localGridBlocksInitialized && localGrid != null) // Safety check
            {
                if (terminal.CustomName.Contains("[ELI_LOCAL]"))
                {
                    localGridHologramTerminals[terminal.Position] = terminal;
                    HologramCustomData theData = InitializeCustomDataHologram(terminal);
                    HologramCustomDataTerminalPair thePair = new HologramCustomDataTerminalPair();
                    thePair.HologramCustomData = theData;
                    thePair.HologramCustomDataString = terminal.CustomData;
                    localGridHologramTerminalsData[terminal.Position] = thePair;

                    localGridRadarTerminals.Remove(terminal.Position);
                    localGridRadarTerminalsData.Remove(terminal.Position);
                }
                else if (terminal.CustomName.Contains("[ELI_HOLO]"))
                {
                    localGridRadarTerminals[terminal.Position] = terminal;
                    HoloRadarCustomData theData = InitializeCustomDataHoloRadar(terminal);
                    HoloRadarCustomDataTerminalPair thePair = new HoloRadarCustomDataTerminalPair();
                    thePair.HoloRadarCustomData = theData;
                    thePair.HoloRadarCustomDataString = terminal.CustomData;
                    localGridRadarTerminalsData[terminal.Position] = thePair;

                    localGridHologramTerminals.Remove(terminal.Position);
                    localGridHologramTerminalsData.Remove(terminal.Position);
                }
                else 
                {
                    localGridRadarTerminals.Remove(terminal.Position);
                    localGridRadarTerminalsData.Remove(terminal.Position);
                    localGridHologramTerminals.Remove(terminal.Position);
                    localGridHologramTerminalsData.Remove(terminal.Position);
                }
            }
        }

        // TODO this doesn't seem to be firing on changing CustomData of a holo table?
        private void OnTerminalCustomDataChanged(IMyTerminalBlock terminal)
        {
            if (localGridBlocksInitialized && localGrid != null) // Safety check
            {
                if (localGridHologramTerminals.ContainsKey(terminal.Position))
                {
                    HologramCustomDataTerminalPair thePair = new HologramCustomDataTerminalPair();
                    HologramCustomData theData = new HologramCustomData();
                    if (!localGridHologramTerminalsData.TryGetValue(terminal.Position, out thePair)) // Get current values if present
                    {
                        thePair = new HologramCustomDataTerminalPair();
                        thePair.HologramCustomData = new HologramCustomData();
                    }
                    theData = thePair.HologramCustomData;

                    MyIni ini = new MyIni();
                    MyIniParseResult result;
                    ini.TryParse(terminal.CustomData, out result);
                    ReadCustomDataHologram(ini, theData, terminal); // Might trigger OnTerminalCustomDataChanged again but only once and only if it writes new default values for new keys etc. 

                    thePair.HologramCustomData = theData;
                    thePair.HologramCustomDataString = terminal.CustomData;
                    localGridHologramTerminalsData[terminal.Position] = thePair;
                }
                else if (localGridRadarTerminals.ContainsKey(terminal.Position)) 
                {
                    HoloRadarCustomData theData = new HoloRadarCustomData();
                    HoloRadarCustomDataTerminalPair thePair = new HoloRadarCustomDataTerminalPair();
                    if (!localGridRadarTerminalsData.TryGetValue(terminal.Position, out thePair)) // Get current values if present
                    {
                        thePair = new HoloRadarCustomDataTerminalPair();
                        thePair.HoloRadarCustomData = new HoloRadarCustomData();
                    }
                    theData = thePair.HoloRadarCustomData;

                    MyIni ini = new MyIni();
                    MyIniParseResult result;
                    ini.TryParse(terminal.CustomData, out result);
                    ReadCustomDataHoloRadar(ini, theData, terminal); // Might trigger OnTerminalCustomDataChanged again but only once and only if it writes new default values for new keys etc. 

                    thePair.HoloRadarCustomData = theData;
                    thePair.HoloRadarCustomDataString = terminal.CustomData;

                    localGridRadarTerminalsData[terminal.Position] = thePair;
                }
            }
        }

        private void OnControlledEntityCustomDataChanged(IMyTerminalBlock terminal)
        {
            if (localGridBlocksInitialized && localGrid != null && localGridControlledEntity != null) // Safety check
            {
                ControlledEntityCustomData theData = new ControlledEntityCustomData();
                if (localGridControlledEntityCustomData != null)
                {
                    theData = localGridControlledEntityCustomData;
                }

                MyIni ini = new MyIni();
                MyIniParseResult result;
                ini.TryParse(terminal.CustomData, out result);
                ReadCustomDataControlledEntity(ini, theData, terminal); // Might trigger OnTerminalCustomDataChanged again but only once and only if it writes new default values for new keys etc. 

                localGridControlledEntityCustomData = theData;
                localGridControlledEntityLastCustomData = terminal.CustomData;

                localGridHologramPalleteForClusterSlices = BuildIntegrityColors(theData.scannerColor * 0.5f);
                targetGridHologramPalleteForClusterSlices = BuildIntegrityColors(theData.lineColorComp * 0.5f);
            }
        }

        public Vector4[] BuildIntegrityColors(Vector4 hologramColor)
        {
            Vector4 yellow = new Vector4(1f, 1f, 0f, 1f);
            Vector4 red = new Vector4(1f, 0f, 0f, 1f);

            return new Vector4[]
            {
                red,                                 // Bucket 0
                Vector4.Lerp(yellow, red, 0.5f),     // Bucket 1
                yellow,                              // Bucket 2
                Vector4.Lerp(hologramColor, yellow, 0.5f), // Bucket 3
                hologramColor                        // Bucket 4
            };
        }

        //--Target Grid

        private void OnTargetBlockAdded(VRage.Game.ModAPI.IMySlimBlock block)
        {
            if (targetGridBlocksInitialized && targetGrid != null) // Safety check
            {
                targetGridAllBlocksDict[block.Position] = block;
                Dictionary<Vector3I, IMySlimBlock> blockDictForFloor;
                if (!targetGridAllBlocksDictByFloor.TryGetValue(block.Position.Y, out blockDictForFloor))
                {
                    blockDictForFloor = new Dictionary<Vector3I, IMySlimBlock>();
                    targetGridAllBlocksDictByFloor[block.Position.Y] = blockDictForFloor;
                }
                blockDictForFloor[block.Position] = block;
                targetGridBlockComponentStacks[block.ComponentStack] = block.Position;
                block.ComponentStack.IntegrityChanged += OnTargetBlockIntegrityChanged;

                IMyTerminalBlock terminal = block.FatBlock as IMyTerminalBlock;
                if (terminal != null)
                {
                    IMyRadioAntenna antenna = terminal as IMyRadioAntenna;
                    if (antenna != null)
                    {
                        targetGridAntennasDict.Add(antenna.Position, antenna);
                    }
                }
                IMyJumpDrive jumpDrive = block.FatBlock as IMyJumpDrive;
                if (jumpDrive != null)
                {
                    targetGridJumpDrivesDict[block.Position] = jumpDrive;
                }
                targetGridCurrentIntegrity += block.Integrity;
                targetGridMaxIntegrity += block.MaxIntegrity;

                targetHologramScaleNeedsRefresh = true;
                if (!theSettings.useClusterSlices)
                {
                    targetGridClustersNeedRefresh = true; // In case we are using block clusters instead of slices
                }
                else
                {
                    // TODO might want to change this to only fill a list of Y levels that need to be rebuilt, and have that done in update instead of every time a block is removed. It could then fire every x seconds too. 
                    RebuildFloorClustersForGridFloor(block.Position.Y, ref targetGridFloorClusterSlices, ref targetGridBlockToFloorClusterSlicesMap, targetGridAllBlocksDictByFloor);
                    //targetGridClusterSlicesNeedRefresh = true; // In case we are using cluster slices instead of block clusters
                }
            }
        }

        private void OnTargetBlockRemoved(VRage.Game.ModAPI.IMySlimBlock block)
        {
            if (targetGridBlocksInitialized && targetGrid != null) // Safety check
            {
                block.ComponentStack.IntegrityChanged -= OnTargetBlockIntegrityChanged;
                targetGridBlockComponentStacks.Remove(block.ComponentStack);
                targetGridAllBlocksDictByFloor[block.Position.Y].Remove(block.Position);
                targetGridAllBlocksDict.Remove(block.Position);

                IMyTerminalBlock terminal = block.FatBlock as IMyTerminalBlock;
                if (terminal != null)
                {
                    IMyRadioAntenna antenna = terminal as IMyRadioAntenna;
                    if (antenna != null)
                    {
                        targetGridAntennasDict.Remove(antenna.Position);
                    }
                }
                IMyJumpDrive jumpDrive = block.FatBlock as IMyJumpDrive;
                if (jumpDrive != null)
                {
                    targetGridJumpDrivesDict.Remove(jumpDrive.Position);
                }
                targetGridCurrentIntegrity -= block.Integrity;
                targetGridMaxIntegrity -= block.MaxIntegrity;

                targetHologramScaleNeedsRefresh = true;
                if (!theSettings.useClusterSlices)
                {
                    targetGridClustersNeedRefresh = true; // In case we are using block clusters instead of slices
                }
                else
                {
                    // TODO might want to change this to only fill a list of Y levels that need to be rebuilt, and have that done in update instead of every time a block is removed. It could then fire every x seconds too. 
                    RebuildFloorClustersForGridFloor(block.Position.Y, ref targetGridFloorClusterSlices, ref targetGridBlockToFloorClusterSlicesMap, targetGridAllBlocksDictByFloor);
                    //targetGridClusterSlicesNeedRefresh = true; // In case we are using cluster slices instead of block clusters
                }
            }
        }

        private void OnTargetBlockIntegrityChanged(IMyComponentStack stack, float oldIntegrity, float newIntegrity)
        {
            if (targetGridBlocksInitialized && targetGrid != null) // Safety Check
            {
                IMySlimBlock block = null;
                Vector3I stackPositionKey = Vector3I.Zero;
                if (targetGridBlockComponentStacks.TryGetValue(stack, out stackPositionKey))
                {
                    block = targetGridAllBlocksDict[stackPositionKey];
                    if (block != null)
                    {
                        float integrityDiff = newIntegrity - oldIntegrity;
                        targetGridCurrentIntegrity += integrityDiff;

                        if (!theSettings.useClusterSlices)
                        {
                            // If we are using block clusters we can updaste the integrity of the cluster this block belongs to. 
                            Vector3I clusterKey = targetGridBlockToClusterMap[block.Position];
                            if (targetGridBlockToClusterMap.TryGetValue(block.Position, out clusterKey))
                            {
                                BlockCluster clusterEntry;
                                if (targetGridBlockClusters.TryGetValue(clusterKey, out clusterEntry))
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
                                int oldSliceIndex = GetIntegrityIndex(oldRatio);
                                int newSliceIndex = GetIntegrityIndex(newRatio);

                                if (oldSliceIndex != newSliceIndex)
                                {
                                    RebuildFloorClustersForGridFloor(block.Position.Y, ref targetGridFloorClusterSlices, ref targetGridBlockToFloorClusterSlicesMap, targetGridAllBlocksDictByFloor);
                                    //targetGridClusterSlicesNeedRefresh = true; // In case we are using cluster slices instead of block clusters
                                    // If we are using cluster slices we can trigger a refresh, since we want slices to contain only blocks of the same integrity. So if one in a slice gets damaged it
                                    // breaks off the slice it was a part of and becomes it's own slice. 
                                }
                            }
                        }
                    }
                }
            }
        }

        public void InitializeTargetGrid()
        {
            if (targetGrid != null) // Safety check
            {
                // Process blocks on grid
                InitializeTargetGridBlocks();

                if (!theSettings.useClusterSlices)
                {
                    targetGridClusterSize = GetClusterSize(targetGridAllBlocksDict.Count);
                    ClusterBlocks(targetGridAllBlocksDict, ref targetGridBlockClusters, ref targetGridBlockToClusterMap, targetGridClusterSize);
                    targetGridClustersNeedRefresh = false;
                }
                else
                {
                    ClusterBlocksIntoSlices(ref targetGridFloorClusterSlices, ref targetGridBlockToFloorClusterSlicesMap, targetGridAllBlocksDict);
                    targetGridClusterSlicesNeedRefresh = false;
                }

                // Add Event Handlers
                targetGrid.OnBlockAdded += OnTargetBlockAdded;
                targetGrid.OnBlockRemoved += OnTargetBlockRemoved;

                targetGridHologramActivationTime += deltaTimeSinceLastTick * 0.667;
                targetGridHologramBootUpAlpha = ClampedD(targetGridHologramActivationTime, 0, 1);
                targetGridHologramBootUpAlpha = Math.Pow(targetGridHologramBootUpAlpha, 0.25);

                targetHologramScaleNeedsRefresh = true;
                targetGridInitialized = true;
            }
        }

        public void SetTargetGrid(IMyCubeGrid newGrid)
        {
            if (targetGrid != null) 
            {
                ResetTargetGrid();
            }
            targetGrid = newGrid;
            InitializeTargetGrid();
        }

        public void ResetTargetGrid() 
        {
            if (targetGrid != null) // Safety check
            {
                // Remove event handlers
                foreach (MyComponentStack componentStack in targetGridBlockComponentStacks.Keys)
                {
                    componentStack.IntegrityChanged -= OnTargetBlockIntegrityChanged;
                }
                targetGrid.OnBlockAdded -= OnTargetBlockAdded;
                targetGrid.OnBlockRemoved -= OnTargetBlockRemoved;
            }

            // target grid and controlled entity if applicable
            targetGrid = null;
            targetGridInitialized = false;

            // target grid blocks
            targetGridAllBlocksDict.Clear();
            targetGridAllBlocksDictByFloor.Clear();
            targetGridBlockComponentStacks.Clear();
            targetGridAntennasDict.Clear();

            // target grid clusters
            targetGridBlockClusters.Clear();
            targetGridBlockToClusterMap.Clear();
            targetGridClustersNeedRefresh = false;

            // target grid cluster slices
            targetGridFloorClusterSlices.Clear();
            targetGridBlockToFloorClusterSlicesMap.Clear();
            targetGridClusterSlicesNeedRefresh = false;

            // target grid jump drives
            targetGridJumpDrivesDict.Clear();

            targetGridBlocksInitialized = false;

            // Target Grid vars
            targetGridCurrentIntegrity = 0f;
            targetGridMaxIntegrity = 0f;

            targetHologramScalingMatrix = MatrixD.Identity;
            targetHologramViewRotationCurrent = MatrixD.Identity;
            targetHologramViewRotationGoal = MatrixD.Identity;
            targetHologramFinalRotation = MatrixD.Identity;
        }

        private void InitializeTargetGridBlocks()
        {
            if (targetGrid == null)
            {
                return;
            }

            List<VRage.Game.ModAPI.IMySlimBlock> blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
            targetGrid.GetBlocks(blocks);

            foreach (VRage.Game.ModAPI.IMySlimBlock block in blocks)
            {
                targetGridAllBlocksDict[block.Position] = block;
                Dictionary<Vector3I, IMySlimBlock> blockDictForFloor;
                if (!targetGridAllBlocksDictByFloor.TryGetValue(block.Position.Y, out blockDictForFloor))
                {
                    blockDictForFloor = new Dictionary<Vector3I, IMySlimBlock>();
                    targetGridAllBlocksDictByFloor[block.Position.Y] = blockDictForFloor;
                }
                blockDictForFloor[block.Position] = block;
                targetGridBlockComponentStacks[block.ComponentStack] = block.Position;
                block.ComponentStack.IntegrityChanged += OnTargetBlockIntegrityChanged;

                IMyTerminalBlock terminal = block.FatBlock as IMyTerminalBlock;
                if (terminal != null)
                {
                    IMyRadioAntenna antenna = terminal as IMyRadioAntenna;
                    if (antenna != null)
                    {
                        targetGridAntennasDict.Add(antenna.Position, antenna);
                    }
                }
                IMyJumpDrive jumpDrive = block.FatBlock as IMyJumpDrive;
                if (jumpDrive != null)
                {
                    targetGridJumpDrivesDict[block.Position] = jumpDrive;
                }

                targetGridCurrentIntegrity += block.Integrity;
                targetGridMaxIntegrity += block.MaxIntegrity;
            }
            targetGridBlocksInitialized = true;
        }

        public void UpdateTargetGridAntennaStatus()
        {
            targetGridHasPassiveRadar = false;
            targetGridHasActiveRadar = false;
            targetGridMaxPassiveRadarRange = 0.0;
            targetGridMaxActiveRadarRange = 0.0;

            foreach (IMyRadioAntenna antenna in targetGridAntennasDict.Values)
            {
                // Check if antenna is non-functional and continue to check next antenna.
                if (!antenna.IsWorking || !antenna.Enabled || !antenna.IsFunctional)
                {
                    continue;
                }
                // If we are here the antenna must be functional and powered on, even if not broadcasting. So set passive to true.
                targetGridHasPassiveRadar = true;
                double antennaRadius = antenna.Radius; // Store radius once. 
                targetGridMaxPassiveRadarRange = Math.Max(targetGridMaxPassiveRadarRange, antennaRadius);

                if (antenna.IsBroadcasting)
                {
                    targetGridHasActiveRadar = true;
                    targetGridMaxActiveRadarRange = Math.Max(targetGridMaxActiveRadarRange, antennaRadius);
                }
            }
            // Safety check if config limits radar range. 
            targetGridMaxPassiveRadarRange = Math.Min(targetGridMaxPassiveRadarRange, maxRadarRange);
            targetGridMaxActiveRadarRange = Math.Min(targetGridMaxActiveRadarRange, maxRadarRange);
        }

        private void UpdateTargetGridJumpDrives()
        {
            float jumpDriveChargeCurrent = 0;
            float jumpDriveChargeMax = 0;

            double minTime = double.MaxValue;

            foreach (IMyJumpDrive jumpDrive in targetGridJumpDrivesDict.Values)
            {
                bool isReady = jumpDrive.Status == Sandbox.ModAPI.Ingame.MyJumpDriveStatus.Ready;
                if (jumpDrive.IsWorking && isReady)
                {
                    jumpDriveChargeCurrent += jumpDrive.CurrentStoredPower;
                }
                jumpDriveChargeMax += jumpDrive.MaxStoredPower;
            }
            if (jumpDriveChargeMax != jumpDriveChargeCurrent)
            {
                float currentStoredPower = jumpDriveChargeCurrent;
                float lastStoredPower = targetGridJumpDrivePowerStoredPrevious;
                float difPower = (currentStoredPower - lastStoredPower);

                if (difPower > 0)
                {
                    double powerPerSecond = (currentStoredPower - lastStoredPower) / deltaTimeSinceLastTick;

                    if (powerPerSecond > 0)
                    {
                        double timeRemaining = ((jumpDriveChargeMax - currentStoredPower) / powerPerSecond) * 100;

                        if (timeRemaining < minTime)
                        {
                            minTime = timeRemaining;
                        }
                    }
                    targetGridJumpDrivePowerStoredPrevious = currentStoredPower;
                }
            }
            targetGridJumpDrivePowerStored = jumpDriveChargeCurrent;
            targetGridJumpDrivePowerStoredMax = jumpDriveChargeMax;
            targetGridJumpDriveTimeToReady = minTime != double.MaxValue ? minTime : 0;
        }

        //------------



        public HologramCustomData InitializeCustomDataHologram(IMyTerminalBlock terminal) 
        {
            HologramCustomData theData = new HologramCustomData();

            MyIni ini = new MyIni();
            MyIniParseResult result;
            ini.TryParse(terminal.CustomData, out result);
            ReadCustomDataHologram(ini, theData, terminal); // Might trigger OnTerminalCustomDataChanged again but only once and only if it writes new default values for new keys etc. 

            return theData;
        }

        public HoloRadarCustomData InitializeCustomDataHoloRadar(IMyTerminalBlock terminal)
        {
            HoloRadarCustomData theData = new HoloRadarCustomData();

            MyIni ini = new MyIni();
            MyIniParseResult result;
            ini.TryParse(terminal.CustomData, out result);
            ReadCustomDataHoloRadar(ini, theData, terminal); // Might trigger OnTerminalCustomDataChanged again but only once and only if it writes new default values for new keys etc. 

            return theData;
        }

        public void InitializeLocalGrid()
        {
            if (localGrid != null) // Safety check
            {
                // Process blocks on grid
                InitializeLocalGridBlocks();

                if (!theSettings.useClusterSlices)
                {
                    localGridClusterSize = GetClusterSize(localGridAllBlocksDict.Count);
                    ClusterBlocks(localGridAllBlocksDict, ref localGridBlockClusters, ref localGridBlockToClusterMap, localGridClusterSize);
                    localGridClustersNeedRefresh = false;
                }
                else 
                {
                    //MyLog.Default.WriteLine($"FENIX_HUD: InitializeLocalGrid about to ClusterBlocksIntoSlices");
                    ClusterBlocksIntoSlices(ref localGridFloorClusterSlices, ref localGridBlockToFloorClusterSlicesMap, localGridAllBlocksDict);
                    //MyLog.Default.WriteLine($"FENIX_HUD: InitializeLocalGrid done clustering, floorCount = {localGridFloorClusterSlices.Count}");
                    localGridClusterSlicesNeedRefresh = false;
                }

                // Add Event Handlers
                localGrid.OnBlockAdded += OnLocalBlockAdded;
                localGrid.OnBlockRemoved += OnLocalBlockRemoved;

                InitializeTheSettings();

                localGridHologramActivationTime += deltaTimeSinceLastTick * 0.667;
                localGridHologramBootUpAlpha = ClampedD(localGridHologramActivationTime, 0, 1);
                localGridHologramBootUpAlpha = Math.Pow(localGridHologramBootUpAlpha, 0.25);

                localHologramScaleNeedsRefresh = true;
                localGridInitialized = true;
            }
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

        public void InitializeTheSettings() 
        {
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
        }

        public void InitializeLocalGridControlledEntity() 
        {
            if (localGridControlledEntity != null) 
            {
                ControlledEntityCustomData theData = new ControlledEntityCustomData();
                if (localGridControlledEntityCustomData != null)
                {
                    theData = localGridControlledEntityCustomData;
                }

                MyIni ini = new MyIni();
                MyIniParseResult result;
                ini.TryParse(localGridControlledEntity.CustomData, out result);
                ReadCustomDataControlledEntity(ini, theData, localGridControlledEntity); // Might trigger OnTerminalCustomDataChanged again but only once and only if it writes new default values for new keys etc. 

                localGridControlledEntityCustomData = theData;
                localGridControlledEntityLastCustomData = localGridControlledEntity.CustomData;
                localGridControlledEntity.CustomDataChanged += OnControlledEntityCustomDataChanged;

                localGridHologramPalleteForClusterSlices = BuildIntegrityColors(theData.scannerColor * 0.5f);
                targetGridHologramPalleteForClusterSlices = BuildIntegrityColors(theData.lineColorComp * 0.5f);

                localGridControlledEntityInitialized = true;
            }
        }

        public void ResetLocalGrid()
        {
            if (localGrid != null) // Safety check
            {
                // Remove event handlers
                foreach (MyComponentStack componentStack in localGridBlockComponentStacks.Keys)
                {
                    componentStack.IntegrityChanged -= OnLocalBlockIntegrityChanged;
                }
                foreach (IMyTerminalBlock terminal in localGridEligibleTerminals.Values) 
                {
                    terminal.CustomDataChanged -= OnTerminalCustomDataChanged;
                    terminal.CustomNameChanged -= OnTerminalCustomNameChanged;
                }
                localGrid.OnBlockAdded -= OnLocalBlockAdded;
                localGrid.OnBlockRemoved -= OnLocalBlockRemoved;
            }

            if (localGridControlledEntity != null) 
            {
                localGridControlledEntityCustomData = null;
                localGridControlledEntity.CustomDataChanged -= OnControlledEntityCustomDataChanged;
            }

            // Local grid and controlled entity if applicable
            localGrid = null;
            localGridControlledEntity = null;
            localGridControlledEntityInitialized = false;
            localGridInitialized = false;

            // Local grid blocks
            localGridAllBlocksDict.Clear();
            localGridAllBlocksDictByFloor.Clear();
            localGridBlockComponentStacks.Clear();

            // Local grid clusters
            localGridBlockClusters.Clear();
            localGridBlockToClusterMap.Clear();
            localGridClustersNeedRefresh = false;

            // Local grid cluster slices
            localGridFloorClusterSlices.Clear();
            localGridBlockToFloorClusterSlicesMap.Clear();
            localGridClusterSlicesNeedRefresh = false;

            // Local grid tanks/containers
            localGridHydrogenTanksDict.Clear();
            localGridOxygenTanksDict.Clear();

            // Local grid power
            localGridPowerProducersDict.Clear();
            localGridBatteriesDict.Clear();

            // Local grid jump drives
            localGridJumpDrivesDict.Clear();

            // Local grid terminals
            localGridEligibleTerminals.Clear();
            localGridHologramTerminals.Clear();
            localGridHologramTerminalsData.Clear();
            localGridHologramTerminalRotations.Clear();
            localGridRadarTerminals.Clear();
            localGridRadarTerminalsData.Clear();
            localGridAntennasDict.Clear();
            localGridBlocksInitialized = false;

            // Local Grid vars
            localGridCurrentIntegrity = 0f;
            localGridMaxIntegrity = 0f;

            localGridPowerConsumed = 0f;
            localGridPowerHoursRemaining = 0f;
            localGridPowerProduced = 0f;
            localGridPowerStored = 0f;
            localGridPowerStoredMax = 0f;

            localGridHydrogenConsumptionRate = 0;
            localGridHydrogenFillRatio = 0;
            localGridHydrogenRemainingSeconds = 0;
            localGridRemainingHydrogen = 0;
            localGridRemainingHydrogenPrevious = 0;

            localGridOxygenConsumptionRate = 0;
            localGridOxygenFillRatio = 0;
            localGridOxygenRemainingSeconds = 0;
            localGridRemainingOxygen = 0;
            localGridRemainingOxygenPrevious = 0;

            localGridJumpDrivePowerStored = 0;
            localGridJumpDrivePowerStoredMax = 0;
            localGridJumpDrivePowerStoredPrevious = 0;
            localGridJumpDriveTimeToReady = 0;

            localGridVelocity = Vector3D.Zero;
            localGridVelocityAngular = Vector3D.Zero;
            localGridSpeed = 0f;

            localHologramScalingMatrix = MatrixD.Identity;
            localHologramViewRotationCurrent = MatrixD.Identity;
            localHologramViewRotationGoal = MatrixD.Identity;
            localHologramFinalRotation = MatrixD.Identity;

            ResetTargetGrid(); // If re-setting the local grid, we also re-set the target grid. 
        }

        public void CheckForLocalGrid()
        {
            // Check if the player is currently controlling a grid entity, if so it becomes the local grid.
            isPlayerControlling = IsLocalPlayerControllingGrid();
            if (isPlayerControlling)
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
                        //MyLog.Default.WriteLine($"FENIX_HUD: Cleared old localGrid then Set localGrid = cockpit.CubeGrid");
                    }
                    else
                    {
                        localGrid = localGridControlledEntity.CubeGrid; // Store the parent CubeGrid of the controlled entity
                        //MyLog.Default.WriteLine($"FENIX_HUD: Set localGrid = localGridControlledEntity.CubeGrid");
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
                if (localGridControlledEntity != null) 
                {
                    // If we were controlling a grid prior, and aren't anymore, clear the localGridControlledEntity and eventHandlers. 
                    localGridControlledEntity.CustomDataChanged -= OnControlledEntityCustomDataChanged;
                    localGridControlledEntity = null;
                }

                IMyCubeGrid nearestGrid = GetNearestGridToPlayer();
                //MyLog.Default.WriteLine($"FENIX_HUD: Not controlling, just checked nearest grid. {(nearestGrid != null ? "it is not null" : "it is null")}");
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
                    //MyLog.Default.WriteLine($"FENIX_HUD: Cleared old localGrid then Set localGrid = nearestGrid");
                }
                else if (nearestGrid != null) 
                {
                    localGrid = nearestGrid;
                    //MyLog.Default.WriteLine($"FENIX_HUD: Set localGrid = nearestGrid");
                }
                
            }
        }

        private void InitializeLocalGridBlocks()
        {
            if (localGrid == null)
            {
                return;
            }

            List<VRage.Game.ModAPI.IMySlimBlock> blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
            localGrid.GetBlocks(blocks);

            foreach (VRage.Game.ModAPI.IMySlimBlock block in blocks)
            {
                localGridAllBlocksDict[block.Position] = block;
                Dictionary<Vector3I, IMySlimBlock> blockDictForFloor;
                if (!localGridAllBlocksDictByFloor.TryGetValue(block.Position.Y, out blockDictForFloor)) 
                {
                    blockDictForFloor = new Dictionary<Vector3I, IMySlimBlock>();
                    localGridAllBlocksDictByFloor[block.Position.Y] = blockDictForFloor;
                }
                blockDictForFloor[block.Position] = block;
                localGridBlockComponentStacks[block.ComponentStack] = block.Position;
                block.ComponentStack.IntegrityChanged += OnLocalBlockIntegrityChanged;
                IMyTerminalBlock terminal = block.FatBlock as IMyTerminalBlock;
                if (terminal != null)
                {
                    IMyCockpit cockpit = terminal as IMyCockpit;
                    IMyRadioAntenna antenna = terminal as IMyRadioAntenna;
                    if (cockpit == null && antenna == null) 
                    {
                        // don't add cockpits or antennas
                        localGridEligibleTerminals[block.Position] = terminal;
                        terminal.CustomNameChanged += OnTerminalCustomNameChanged;
                        terminal.CustomDataChanged += OnTerminalCustomDataChanged;
                        // If for some reason already tagged (welding/merging existing block?) add them to appropriate Dict. 
                        if (terminal.CustomName.Contains("[ELI_LOCAL]"))
                        {
                            localGridHologramTerminals[block.Position] = terminal;
                            HologramCustomData theData = InitializeCustomDataHologram(terminal);
                            HologramCustomDataTerminalPair thePair = new HologramCustomDataTerminalPair();
                            thePair.HologramCustomData = theData;
                            thePair.HologramCustomDataString = terminal.CustomData;
                            localGridHologramTerminalsData[block.Position] = thePair;
                            Vector4[] colorPallet = BuildIntegrityColors(theData.lineColor * 0.5f);
                            localGridHologramTerminalPallets[terminal.Position] = colorPallet;
                        }
                        if (terminal.CustomName.Contains("[ELI_HOLO]"))
                        {
                            localGridRadarTerminals[block.Position] = terminal;
                            HoloRadarCustomData theData = InitializeCustomDataHoloRadar(terminal);
                            HoloRadarCustomDataTerminalPair thePair = new HoloRadarCustomDataTerminalPair();
                            thePair.HoloRadarCustomData = theData;
                            thePair.HoloRadarCustomDataString = terminal.CustomData;
                            localGridRadarTerminalsData[block.Position] = thePair;
                        }
                    }
                    if (antenna != null)
                    {
                        localGridAntennasDict.Add(antenna.Position, antenna);
                    }
                }

                IMyGasTank tank = block.FatBlock as IMyGasTank;
                if (tank != null)
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

                IMyPowerProducer producer = block.FatBlock as IMyPowerProducer;
                IMyBatteryBlock battery = block.FatBlock as IMyBatteryBlock;
                if (producer != null)
                {
                    localGridPowerProducersDict[block.Position] = producer;
                }
                else if (battery != null)
                {
                    localGridBatteriesDict[block.Position] = battery;
                }

                IMyJumpDrive jumpDrive = block.FatBlock as IMyJumpDrive;
                if (jumpDrive != null)
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
                UpdateElapsedTimeDeltaTimerGHandler();

                if (!localGridInitialized)
                {
                    InitializeLocalGrid();
                }
                if (!localGridControlledEntityInitialized) 
                {
                    InitializeLocalGridControlledEntity();
                }

                localGridVelocity = GetCubeGridVelocity(localGrid);
                localGridVelocityAngular = GetCubeGridVelocityAngular(localGrid);
                localGridSpeed = localGridVelocity.Length();

                UpdateLocalGridAntennaStatus();
                UpdateLocalGridPower();
                UpdateLocalGridHydrogen();
                UpdateLocalGridOxygen();
                UpdateLocalGridJumpDrives();

                if (localHologramScaleNeedsRefresh)
                {
                    UpdateLocalGridScalingMatrix();
                }

                UpdateLocalGridControlledEntityCustomData();
                UpdateLocalGridRadarCustomData();
                UpdateLocalGridHologramCustomData();
                UpdateLocalGridRotationMatrix();

                if (!theSettings.useClusterSlices && localGridClustersNeedRefresh)
                {
                    localGridClusterSize = GetClusterSize(localGridAllBlocksDict.Count);
                    ClusterBlocks(localGridAllBlocksDict, ref localGridBlockClusters, ref localGridBlockToClusterMap, localGridClusterSize);
                    localGridClustersNeedRefresh = false;
                }
                else if (localGridClusterSlicesNeedRefresh)
                {
                    ClusterBlocksIntoSlices(ref localGridFloorClusterSlices, ref localGridBlockToFloorClusterSlicesMap, localGridAllBlocksDict);
                    localGridClusterSlicesNeedRefresh = false;
                }

                if (targetGrid != null) 
                {
                    if (!targetGridInitialized) 
                    {
                        InitializeTargetGrid();
                    }

                    UpdateTargetGridJumpDrives();

                    if (targetHologramScaleNeedsRefresh)
                    {
                        UpdateTargetGridScalingMatrix();
                    }
                    UpdateTargetGridRotationMatrix();
                    if (!theSettings.useClusterSlices && targetGridClustersNeedRefresh)
                    {
                        targetGridClusterSize = GetClusterSize(targetGridAllBlocksDict.Count);
                        ClusterBlocks(targetGridAllBlocksDict, ref targetGridBlockClusters, ref targetGridBlockToClusterMap, targetGridClusterSize);
                        targetGridClustersNeedRefresh = false;
                    }
                    else if (targetGridClusterSlicesNeedRefresh)
                    {
                        ClusterBlocksIntoSlices(ref targetGridFloorClusterSlices, ref targetGridBlockToFloorClusterSlicesMap, targetGridAllBlocksDict);
                        targetGridClusterSlicesNeedRefresh = false;
                    }
                }
            }
        }

        public void UpdateLocalGridControlledEntityCustomData() 
        {
            if (localGrid != null && localGridInitialized && localGridControlledEntity != null && localGridControlledEntityInitialized) 
            {
                if (localGridControlledEntityLastCustomData != null && !localGridControlledEntity.CustomData.Equals(localGridControlledEntityLastCustomData))
                {
                    ControlledEntityCustomData theData = new ControlledEntityCustomData();
                    if (localGridControlledEntityCustomData != null)
                    {
                        theData = localGridControlledEntityCustomData;
                    }

                    MyIni ini = new MyIni();
                    MyIniParseResult result;
                    ini.TryParse(localGridControlledEntity.CustomData, out result);
                    ReadCustomDataControlledEntity(ini, theData, localGridControlledEntity); // Might trigger OnTerminalCustomDataChanged again but only once and only if it writes new default values for new keys etc. 

                    localGridControlledEntityCustomData = theData;
                    localGridControlledEntityLastCustomData = localGridControlledEntity.CustomData;

                    localGridHologramPalleteForClusterSlices = BuildIntegrityColors(theData.scannerColor * 0.5f);
                    targetGridHologramPalleteForClusterSlices = BuildIntegrityColors(theData.lineColorComp * 0.5f);
                }
            }
        }

        public void UpdateLocalGridHologramCustomData() 
        {
            if (localGrid != null && localGridInitialized && localGridBlocksInitialized) 
            {
                Dictionary<Vector3I, HologramCustomDataTerminalPair> checkTerminals = new Dictionary<Vector3I, HologramCustomDataTerminalPair>(localGridHologramTerminalsData);
                foreach (KeyValuePair<Vector3I, HologramCustomDataTerminalPair> hologramCustomDataPair in checkTerminals)
                {
                    IMyTerminalBlock terminal = localGridHologramTerminals[hologramCustomDataPair.Key];
                    if (hologramCustomDataPair.Value.HologramCustomDataString != terminal.CustomData)
                    {
                        HologramCustomDataTerminalPair thePair = new HologramCustomDataTerminalPair();
                        HologramCustomData theData = new HologramCustomData();
                        if (!localGridHologramTerminalsData.TryGetValue(terminal.Position, out thePair)) // Get current values if present
                        {
                            thePair = new HologramCustomDataTerminalPair();
                            thePair.HologramCustomData = new HologramCustomData();
                        }
                        theData = thePair.HologramCustomData;

                        MyIni ini = new MyIni();
                        MyIniParseResult result;
                        ini.TryParse(terminal.CustomData, out result);
                        ReadCustomDataHologram(ini, theData, terminal); // Might trigger OnTerminalCustomDataChanged again but only once and only if it writes new default values for new keys etc. 

                        thePair.HologramCustomData = theData;
                        thePair.HologramCustomDataString = terminal.CustomData;
                        localGridHologramTerminalsData[terminal.Position] = thePair;
                        Vector4[] colorPallet = BuildIntegrityColors(theData.lineColor * 0.5f);
                        localGridHologramTerminalPallets[terminal.Position] = colorPallet;

                    }
                }
            }
        }

        public void UpdateLocalGridRadarCustomData()
        {
            if (localGrid != null && localGridInitialized && localGridBlocksInitialized)
            {
                Dictionary<Vector3I, HoloRadarCustomDataTerminalPair> checkTerminals = new Dictionary<Vector3I, HoloRadarCustomDataTerminalPair>(localGridRadarTerminalsData);
                foreach (KeyValuePair<Vector3I, HoloRadarCustomDataTerminalPair> holoRadarCustomDataPair in checkTerminals)
                {
                    IMyTerminalBlock terminal = localGridRadarTerminals[holoRadarCustomDataPair.Key];
                    if (holoRadarCustomDataPair.Value.HoloRadarCustomDataString != terminal.CustomData)
                    {
                        HoloRadarCustomDataTerminalPair thePair = new HoloRadarCustomDataTerminalPair();
                        HoloRadarCustomData theData = new HoloRadarCustomData();
                        if (!localGridRadarTerminalsData.TryGetValue(terminal.Position, out thePair)) // Get current values if present
                        {
                            thePair = new HoloRadarCustomDataTerminalPair();
                            thePair.HoloRadarCustomData = new HoloRadarCustomData();
                        }
                        theData = thePair.HoloRadarCustomData;

                        MyIni ini = new MyIni();
                        MyIniParseResult result;
                        ini.TryParse(terminal.CustomData, out result);
                        ReadCustomDataHoloRadar(ini, theData, terminal); // Might trigger OnTerminalCustomDataChanged again but only once and only if it writes new default values for new keys etc. 

                        thePair.HoloRadarCustomData = theData;
                        thePair.HoloRadarCustomDataString = terminal.CustomData;
                        localGridRadarTerminalsData[terminal.Position] = thePair;
                    }
                }
            }
        }

        public void UpdateLocalGridRotationMatrix() 
        {
            if (localGrid != null && localGridInitialized && localGridControlledEntity != null && localGridControlledEntityInitialized && localGridControlledEntityCustomData != null) 
            {
                double lerpFactor = Math.Min(deltaTimeSinceLastTick * 60, 60.0);

                localHologramViewRotationGoal = MatrixD.CreateFromYawPitchRoll(MathHelper.ToRadians(localGridControlledEntityCustomData.holoLocalRotationX),
                            MathHelper.ToRadians(localGridControlledEntityCustomData.holoLocalRotationY),
                            MathHelper.ToRadians(localGridControlledEntityCustomData.holoLocalRotationZ));
                MatrixD.Slerp(localHologramViewRotationCurrent, localHologramViewRotationGoal, deltaTimeSinceLastTick * lerpFactor, out localHologramViewRotationCurrent);
            }
        }

        public void UpdateFinalLocalGridHologramRotation()
        {
            if (localGrid != null && localGridInitialized)
            {
                MatrixD rotationOnlyLocalGridMatrix = localGrid.WorldMatrix;
                rotationOnlyLocalGridMatrix.Translation = Vector3D.Zero;
                localHologramFinalRotation = localHologramViewRotationCurrent * rotationOnlyLocalGridMatrix;
            }
        }

        // This MIGHT need to be called in Draw() only for Orbit and Perspective?
        public void UpdateTargetGridRotationMatrix()
        {
            if (targetGrid != null && targetGridInitialized && localGridControlledEntity != null && localGridControlledEntityInitialized && localGridControlledEntityCustomData != null) 
            {
                switch (localGridControlledEntityCustomData.targetGridHologramViewType)
                {
                    case HologramViewType.Static:
                        double lerpFactor = Math.Min(deltaTimeSinceLastTick * 60, 60.0);
                        targetHologramViewRotationGoal = MatrixD.CreateFromYawPitchRoll(MathHelper.ToRadians(localGridControlledEntityCustomData.holoTargetRotationX),
                                MathHelper.ToRadians(localGridControlledEntityCustomData.holoTargetRotationY),
                                MathHelper.ToRadians(localGridControlledEntityCustomData.holoTargetRotationZ));
                        MatrixD.Slerp(targetHologramViewRotationCurrent, targetHologramViewRotationGoal, deltaTimeSinceLastTick * lerpFactor, out targetHologramViewRotationCurrent);
                        break;
                    case HologramViewType.Orbit:

                        // This is technically an "Orbit" cam... It will show a hologram of the target grid EXACTLY as it appears in world space relative to your orientation
                        // but not your translation/position in the world. So you can rotate your ship around and see all sides of gridB from anywhere in the world.
                        // if you are facing a direction and the target is also facing the same direction you see it's backside, even if it is behind you lol.

                        //What transformation gets us from gridA's coordinate system to gridB's?
                        MatrixD localGridToTargetGrid = MatrixD.Invert(this.localGrid.WorldMatrix) * this.targetGrid.WorldMatrix;
                        //Use this as the rotation (this naturally includes both position and orientation differences)
                        targetHologramViewRotationCurrent = MatrixD.CreateFromQuaternion(Quaternion.CreateFromRotationMatrix(localGridToTargetGrid));
                        break;
                    case HologramViewType.Perspective:
                        // FenixPK 2025-07-28 there are still problems with this when gridA is rolled but this is sooo beyond my understanding at this point I give up haha. 
                        // Round 3, the winner, this perpendicular on the left/right being handled made all the difference. It has the effect I was hoping for. 
                        // Create proxy matrix: gridB's orientation at gridA's position
                        MatrixD rotationMatrixForView = MatrixD.Identity;
                        MatrixD proxyMatrix = targetGrid.WorldMatrix;
                        proxyMatrix.Translation = localGrid.WorldMatrix.Translation;
                        MatrixD relativeOrientation = targetGrid.WorldMatrix * MatrixD.Invert(proxyMatrix);
                        // Extract just the rotation part (remove any translation)
                        rotationMatrixForView = MatrixD.CreateFromQuaternion(Quaternion.CreateFromRotationMatrix(relativeOrientation));

                        // Create a "should be looking at" orientation
                        Vector3D directionToGridB = Vector3D.Normalize(targetGrid.WorldMatrix.Translation - proxyMatrix.Translation);

                        // Project gridA's up vector onto the plane perpendicular to the direction vector
                        Vector3D gridAUpInWorldSpace = localGrid.WorldMatrix.Up;  //gridA.WorldMatrix.Up;
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
                            Vector3D rightCandidate = localGrid.WorldMatrix.Right;
                            Vector3D forwardCandidate = localGrid.WorldMatrix.Forward;
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
                        targetHologramViewRotationCurrent = rotationMatrixForView;
                        break;
                }
            }
            
        }

        /// <summary>
        /// This function evaluates the localGrid's stored antenna blocks. Pseudo SIGINT logic where we can receive passive or output active.
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
        public void UpdateLocalGridAntennaStatus()
        {
            localGridHasPassiveRadar = false;
            localGridHasActiveRadar = false;
            localGridMaxPassiveRadarRange = 0.0;
            localGridMaxActiveRadarRange = 0.0;

            foreach (IMyRadioAntenna antenna in localGridAntennasDict.Values)
            {
                // Check if antenna is non-functional and continue to check next antenna.
                if (!antenna.IsWorking || !antenna.Enabled || !antenna.IsFunctional)
                {
                    continue;
                }
                // If we are here the antenna must be functional and powered on, even if not broadcasting. So set passive to true.
                localGridHasPassiveRadar = true;
                double antennaRadius = antenna.Radius; // Store radius once. 
                localGridMaxPassiveRadarRange = Math.Max(localGridMaxPassiveRadarRange, antennaRadius);

                if (antenna.IsBroadcasting)
                {
                    localGridHasActiveRadar = true;
                    localGridMaxActiveRadarRange = Math.Max(localGridMaxActiveRadarRange, antennaRadius);
                }
            }
            // Safety check if config limits radar range. 
            localGridMaxPassiveRadarRange = Math.Min(localGridMaxPassiveRadarRange, maxRadarRange);
            localGridMaxActiveRadarRange = Math.Min(localGridMaxActiveRadarRange, maxRadarRange);
        }

        

        public void UpdateFinalTargetHologramRotation() 
        {
            if (targetGrid != null && targetGridInitialized) 
            {
                MatrixD rotationOnlyLocalGridMatrix = localGrid.WorldMatrix;
                rotationOnlyLocalGridMatrix.Translation = Vector3D.Zero;
                MatrixD rotationOnlyTargetGridMatrix = targetGrid.WorldMatrix;
                rotationOnlyTargetGridMatrix.Translation = Vector3D.Zero;
                MatrixD rotationMatrixCancelGrids = MatrixD.Invert(rotationOnlyTargetGridMatrix) * rotationOnlyLocalGridMatrix;
                targetHologramFinalRotation = targetHologramViewRotationCurrent * rotationMatrixCancelGrids;
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
                bool gridHasPower = false;

                foreach (IMyPowerProducer producer in localGridPowerProducersDict.Values)
                {
                    if (producer.IsWorking)
                    {
                        totalPowerConsumed += producer.CurrentOutput;
                        totalPowerProduced += producer.MaxOutput;
                        if (producer.Enabled && producer.CurrentOutput > 0.01f)
                        {
                            gridHasPower = true;
                        }
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
                        if (battery.Enabled && battery.CurrentStoredPower > 0.01f) 
                        {
                            gridHasPower = true;
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
                localGridPowerUsagePercentage = powerUsagePercentage;

                // Calculate glitch effect vars for power overloads
                localGridGlitchAmountMin = MathHelper.Clamp(localGridPowerUsagePercentage, 0.85, 1.0) - 0.85;
                localGridGlitchAmountOverload = MathHelper.Lerp(localGridGlitchAmountOverload, 0, deltaTimeSinceLastTick * 2);
                localGridGlitchAmount = MathHelper.Clamp(localGridGlitchAmountOverload, localGridGlitchAmountMin, 1);
                if (localGridGlitchAmountOverload < 0.01)
                {
                    localGridGlitchAmountOverload = 0;
                }

                // Estimate time remaining
                float timeRemaining = currentCharge / totalPowerConsumed;
                localGridPowerHoursRemaining = timeRemaining;
                localGridHasPower = gridHasPower;
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

        private void UpdateLocalGridJumpDrives()
        {
            float jumpDriveChargeCurrent = 0;
            float jumpDriveChargeMax = 0;

            double minTime = double.MaxValue;

            foreach (IMyJumpDrive jumpDrive in localGridJumpDrivesDict.Values)
            {
                bool isReady = jumpDrive.Status == Sandbox.ModAPI.Ingame.MyJumpDriveStatus.Ready;
                if (jumpDrive.IsWorking && isReady)
                {
                    jumpDriveChargeCurrent += jumpDrive.CurrentStoredPower;
                }
                jumpDriveChargeMax += jumpDrive.MaxStoredPower;
            }
            if (jumpDriveChargeMax != jumpDriveChargeCurrent)
            {
                float currentStoredPower = jumpDriveChargeCurrent;
                float lastStoredPower = localGridJumpDrivePowerStoredPrevious;
                float difPower = (currentStoredPower - lastStoredPower);

                if (difPower > 0)
                {
                    double powerPerSecond = (currentStoredPower - lastStoredPower) / deltaTimeSinceLastTick;

                    if (powerPerSecond > 0)
                    {
                        double timeRemaining = ((jumpDriveChargeMax - currentStoredPower) / powerPerSecond) * 100;

                        if (timeRemaining < minTime)
                        {
                            minTime = timeRemaining;
                        }
                    }
                    localGridJumpDrivePowerStoredPrevious = currentStoredPower;
                }
            }
            localGridJumpDrivePowerStored = jumpDriveChargeCurrent;
            localGridJumpDrivePowerStoredMax = jumpDriveChargeMax;
            localGridJumpDriveTimeToReady = minTime != double.MaxValue ? minTime : 0;
        }

        public void UpdateLocalGridScalingMatrix()
        {
            if (localGrid != null) 
            {
                double thicc = hologramScaleFactor / (localGrid.WorldVolume.Radius / localGrid.GridSize);
                localHologramScalingMatrix = MatrixD.CreateScale(hologramScale * thicc);
                localHologramScaleNeedsRefresh = false;
            }  
        }
        public void UpdateTargetGridScalingMatrix()
        {
            if (targetGrid != null)
            {
                VRage.Game.ModAPI.IMyCubeGrid currentTargetGrid = targetGrid as VRage.Game.ModAPI.IMyCubeGrid;
                double thicc = hologramScaleFactor / (currentTargetGrid.WorldVolume.Radius / currentTargetGrid.GridSize);
                targetHologramScalingMatrix = MatrixD.CreateScale(hologramScale * thicc);
                targetHologramScaleNeedsRefresh = false;
            }
        }


        /// <summary>
        /// Checks if the local player is currently controlling a grid entity, and that it is tagged correctly for the mod
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

            if (theSettings.useMainCockpitInsteadOfTag)
            {
                if (!cockpit.IsMainCockpit)
                {
                    return false;
                }
            }
            else 
            {
                if (!cockpit.CustomName.Contains("[ELI_HUD]")) 
                {
                    return false;
                }
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
        /// Retrieves the linear velocity of the CubeGrid
        /// </summary>
        /// <param name="cubeGrid"></param>
        /// <returns></returns>
        public Vector3D GetCubeGridVelocity(IMyCubeGrid cubeGrid)
        {
            if (cubeGrid == null)
            {
                return Vector3D.Zero; // Return zero velocity if the entity is null.
            }
            if (cubeGrid.Physics != null)
            {
                // Return the linear velocity if the physics component is available.
                return cubeGrid.Physics.LinearVelocity;
            }
            else
            {
                // Log an error and return zero velocity if the physics component is not found.
                MyLog.Default.WriteLine("Failed to retrieve velocity: No valid physics component found.");
                return Vector3D.Zero;
            }
        }

        /// <summary>
        /// Retrieves the angular velocity of the CubeGrid
        /// </summary>
        /// <param name="cubeGrid"></param>
        /// <returns></returns>
        public Vector3D GetCubeGridVelocityAngular(IMyCubeGrid cubeGrid)
        {
            if (cubeGrid == null)
            {
                return Vector3D.Zero; // Return zero velocity if the entity is null.
            }
            if (cubeGrid.Physics != null)
            {
                // Return the linear velocity if the physics component is available.
                return cubeGrid.Physics.AngularVelocityLocal;
            }
            else
            {
                // Log an error and return zero velocity if the physics component is not found.
                MyLog.Default.WriteLine("Failed to retrieve velocity: No valid physics component found.");
                return Vector3D.Zero;
            }
        }

        //public class BlockCluster
        //{
        //    public float Integrity = 0f;
        //    public float MaxIntegrity = 0f;
        //}

        public int GetClusterSize(int blockCount) 
        {
            int clusterSize = 1;
            if (blockCount >= 80000)
            {
                clusterSize = 4;
            }
            else if (blockCount >= 50000)
            {
                clusterSize = 3;
            }
            else if (blockCount >= 20000)
            {
                clusterSize = 2;
            }
            return clusterSize;
        }

        public void ClusterBlocks(Dictionary<Vector3I, IMySlimBlock> allBlocksDict, ref Dictionary<Vector3I, BlockCluster> blockClusters,  ref Dictionary<Vector3I, Vector3I> blockToClusterMap, int clusterSize)
        {
            if (allBlocksDict == null || allBlocksDict.Count == 0)
            {
                return;
            }

            blockClusters = new Dictionary<Vector3I, BlockCluster>(); // CLear it

            foreach (KeyValuePair<Vector3I, IMySlimBlock> blockKeyValuePair in allBlocksDict)
            {
                Vector3I blockPos = blockKeyValuePair.Key; // Grid position of this block
                IMySlimBlock block = blockKeyValuePair.Value;

                // Compute which cluster this block belongs to
                Vector3I clusterPos = new Vector3I(
                    (blockPos.X / clusterSize) * clusterSize,
                    (blockPos.Y / clusterSize) * clusterSize,
                    (blockPos.Z / clusterSize) * clusterSize
                );

                if (blockClusters.ContainsKey(clusterPos))
                {
                    blockClusters[clusterPos].Integrity += block.Integrity;
                    blockClusters[clusterPos].MaxIntegrity += block.MaxIntegrity;
                    blockToClusterMap[blockPos] = clusterPos; // Map this block to its cluster position
                }
                else
                {
                    blockClusters[clusterPos] = new BlockCluster
                    {
                        Integrity = block.Integrity,
                        MaxIntegrity = block.MaxIntegrity
                    };
                    blockToClusterMap[blockPos] = clusterPos; // Map this block to its cluster position
                }
            }
        }

      
        public void ClusterBlocksIntoSlices(ref Dictionary<int, List<ClusterBox>> floorClusterSlicesDict,
            ref Dictionary<int, Dictionary<Vector3I, int>> blockToFloorClusterSlicesMap, Dictionary<Vector3I, IMySlimBlock> allBlocksDict)
        {
            floorClusterSlicesDict.Clear();
            blockToFloorClusterSlicesMap.Clear();

            // Group blocks by Y level
            IEnumerable<IGrouping<int, KeyValuePair<Vector3I, IMySlimBlock>>> groupedByFloor = allBlocksDict.GroupBy(kvp => kvp.Key.Y);

            foreach (IGrouping<int, KeyValuePair<Vector3I, IMySlimBlock>> yGroup in groupedByFloor)
            {
                int gridFloor = yGroup.Key;

                // further group by integrity bucket
                IEnumerable<IGrouping<int, KeyValuePair<Vector3I, IMySlimBlock>>> groupedByIntegrity = yGroup.GroupBy(kvp => GetIntegrityBucket(kvp.Value));

                List<ClusterBox> clustersThisFloor = new List<ClusterBox>();
                floorClusterSlicesDict[gridFloor] = clustersThisFloor;
                blockToFloorClusterSlicesMap[gridFloor] = new Dictionary<Vector3I, int>();

                foreach (IGrouping<int, KeyValuePair<Vector3I, IMySlimBlock>> bucketGroup in groupedByIntegrity)
                {
                    int bucket = bucketGroup.Key;
                    HashSet<Vector3I> visited = new HashSet<Vector3I>();

                    foreach (KeyValuePair<Vector3I, IMySlimBlock> kvp in bucketGroup)
                    {
                        Vector3I start = kvp.Key;

                        if (visited.Contains(start))
                        {
                            continue;
                        }

                        ClusterBox cluster = ExpandXZCluster3(start, gridFloor, bucketGroup.ToDictionary(x => x.Key, x => x.Value), visited, bucket);
                        clustersThisFloor.Add(cluster);

                        // Map all blocks in this cluster to it
                        foreach (IMySlimBlock b in cluster.Blocks)
                        {
                            blockToFloorClusterSlicesMap[gridFloor][b.Position] = clustersThisFloor.Count - 1;
                        }
                    }
                }
            }
        }
       


        // Better, largest detailed rectangle with holes not included, so corridors etc cause breaks. 
        private ClusterBox ExpandXZCluster3(Vector3I start, int gridFloor, Dictionary<Vector3I, IMySlimBlock> blocks, HashSet<Vector3I> visited, int bucket)
        {
            // Start with the seed point
            Vector3I min = start;
            Vector3I max = start;

            // --- Expand fully in X (-X and +X)
            while (blocks.ContainsKey(new Vector3I(min.X - 1, gridFloor, min.Z)) &&
                   !visited.Contains(new Vector3I(min.X - 1, gridFloor, min.Z)))
            {
                min.X--;
            }
            while (blocks.ContainsKey(new Vector3I(max.X + 1, gridFloor, max.Z)) &&
                   !visited.Contains(new Vector3I(max.X + 1, gridFloor, max.Z)))
            {
                max.X++;
            }

            // --- Expand fully in +Z
            bool expanded;
            do
            {
                expanded = false;
                int newZ = max.Z + 1;
                bool canExpand = true;
                for (int x = min.X; x <= max.X; x++)
                {
                    Vector3I pos = new Vector3I(x, gridFloor, newZ);
                    if (!blocks.ContainsKey(pos) || visited.Contains(pos))
                    {
                        canExpand = false;
                        break;
                    }
                }
                if (canExpand)
                {
                    max.Z++;
                    expanded = true;
                }
            } while (expanded);

            // --- Expand fully in -Z
            do
            {
                expanded = false;
                int newZ = min.Z - 1;
                bool canExpand = true;
                for (int x = min.X; x <= max.X; x++)
                {
                    Vector3I pos = new Vector3I(x, gridFloor, newZ);
                    if (!blocks.ContainsKey(pos) || visited.Contains(pos))
                    {
                        canExpand = false;
                        break;
                    }
                }
                if (canExpand)
                {
                    min.Z--;
                    expanded = true;
                }
            } while (expanded);

            // --- Build the final rectangle cluster
            ClusterBox cluster = new ClusterBox { Min = min, Max = max, IntegrityBucket = bucket };

            for (int x = min.X; x <= max.X; x++)
            {
                for (int z = min.Z; z <= max.Z; z++)
                {
                    Vector3I pos = new Vector3I(x, gridFloor, z);
                    IMySlimBlock block;
                    if (blocks.TryGetValue(pos, out block))
                    {
                        if (block != null) 
                        {
                            visited.Add(pos);
                            cluster.Blocks.Add(block);
                        }
                    }
                }
            }

            return cluster;
        }

        private ClusterBox ExpandXZCluster2(Vector3I start, int gridFloor, Dictionary<Vector3I, IMySlimBlock> blocks, HashSet<Vector3I> visited, int bucket)
        {
            Vector3I min = start;
            Vector3I max = start;

            // --- Expand in -X
            while (true)
            {
                Vector3I next = new Vector3I(min.X - 1, gridFloor, min.Z);
                if (blocks.ContainsKey(next) && !visited.Contains(next))
                {
                    min.X--;
                }
                else break;
            }

            // --- Expand in +X
            while (true)
            {
                Vector3I next = new Vector3I(max.X + 1, gridFloor, max.Z);
                if (blocks.ContainsKey(next) && !visited.Contains(next))
                {
                    max.X++;
                }
                else break;
            }

            // --- Expand in +Z
            bool canExpandPosZ = true;
            while (canExpandPosZ)
            {
                int newZ = max.Z + 1;
                for (int x = min.X; x <= max.X; x++)
                {
                    Vector3I pos = new Vector3I(x, gridFloor, newZ);
                    if (!blocks.ContainsKey(pos) || visited.Contains(pos))
                    {
                        canExpandPosZ = false;
                        break;
                    }
                }
                if (canExpandPosZ) max.Z++;
            }

            // --- Expand in -Z
            bool canExpandNegZ = true;
            while (canExpandNegZ)
            {
                int newZ = min.Z - 1;
                for (int x = min.X; x <= max.X; x++)
                {
                    Vector3I pos = new Vector3I(x, gridFloor, newZ);
                    if (!blocks.ContainsKey(pos) || visited.Contains(pos))
                    {
                        canExpandNegZ = false;
                        break;
                    }
                }
                if (canExpandNegZ) min.Z--;
            }

            // --- Build cluster
            ClusterBox cluster = new ClusterBox { Min = min, Max = max, IntegrityBucket = bucket };

            // Fill with blocks + mark visited
            for (int x = min.X; x <= max.X; x++)
            {
                for (int z = min.Z; z <= max.Z; z++)
                {
                    Vector3I pos = new Vector3I(x, gridFloor, z);
                    IMySlimBlock block;
                    if (blocks.TryGetValue(pos, out  block))
                    {
                        if (block != null) 
                        {
                            visited.Add(pos);
                            cluster.Blocks.Add(block);
                        }
                        
                    }
                }
            }

            return cluster;
        }

        private ClusterBox ExpandXZCluster(Vector3I start, int gridFloor, Dictionary<Vector3I, IMySlimBlock> blocks, HashSet<Vector3I> visited, int bucket)
        {
            Vector3I min = start;
            Vector3I max = start;

            // expand in +X
            while (true)
            {
                Vector3I next = new Vector3I(max.X + 1, gridFloor, max.Z);
                if (blocks.ContainsKey(next) && !visited.Contains(next))
                {
                    max.X++;
                }
                else
                { 
                    break;
                }
            }

            // expand in +Z
            bool canExpandZ = true;
            while (canExpandZ)
            {
                int newZ = max.Z + 1;
                for (int x = min.X; x <= max.X; x++)
                {
                    Vector3I pos = new Vector3I(x, gridFloor, newZ);
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
                    Vector3I pos = new Vector3I(x, gridFloor, z);
                    IMySlimBlock block;
                    if (blocks.TryGetValue(pos, out block))
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
            int integrityBucket = 4; // Max integrity 100% or 1.0
            if (block.MaxIntegrity <= 0)
            {
                integrityBucket = 0;
                return integrityBucket; // avoid div/0
            }

            float ratio = block.Integrity / block.MaxIntegrity;

            if (ratio < 0.2f) 
            {
                integrityBucket = 0; 
            }
            if (ratio < 0.4f) 
            {
                integrityBucket = 1; 
            }
            if (ratio < 0.6f) 
            {
                integrityBucket = 2;
            }
            if (ratio < 0.8f) 
            {
                integrityBucket = 3; 
            }
            return integrityBucket;
        }

        public void RebuildFloorClustersForGridFloor(int gridFloor, ref Dictionary<int, List<ClusterBox>> floorClusterSlicesDict, 
            ref Dictionary<int, Dictionary<Vector3I, int>> blockToFloorClusterSlicesMap, Dictionary<int, Dictionary<Vector3I, IMySlimBlock>> allBlocksDictByFloor)
        {
            // Remove old clusters for this Y if present
            if (floorClusterSlicesDict.ContainsKey(gridFloor))
            {
                // Also clear block->cluster mappings for this floor
                blockToFloorClusterSlicesMap.Remove(gridFloor);
                floorClusterSlicesDict.Remove(gridFloor);
            }

            Dictionary<Vector3I, IMySlimBlock> floorBlocks = allBlocksDictByFloor[gridFloor];

            //// Collect all blocks at this Y
            //Dictionary<Vector3I, IMySlimBlock> floorBlocks = allBlocks
            //    .Where(kvp => kvp.Key.Y == gridFloor)
            //    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            if (floorBlocks.Count == 0)
            {
                return; // nothing left on this floor
            }

            // Group by integrity bucket
            IEnumerable<IGrouping<int, KeyValuePair<Vector3I, IMySlimBlock>>> groupedByIntegrity = floorBlocks.GroupBy(kvp => GetIntegrityBucket(kvp.Value));

            List<ClusterBox> clustersThisFloor = new List<ClusterBox>();
            floorClusterSlicesDict[gridFloor] = clustersThisFloor;

            foreach (IGrouping<int, KeyValuePair<Vector3I, IMySlimBlock>> bucketGroup in groupedByIntegrity)
            {
                int bucket = bucketGroup.Key;
                HashSet<Vector3I> visited = new HashSet<Vector3I>();

                Dictionary<Vector3I, IMySlimBlock> bucketDict = bucketGroup.ToDictionary(x => x.Key, x => x.Value);

                foreach (KeyValuePair<Vector3I, IMySlimBlock> kvp in bucketGroup)
                {
                    Vector3I start = kvp.Key;

                    if (visited.Contains(start)) 
                    {
                        continue;
                    }

                    ClusterBox cluster = ExpandXZCluster(start, gridFloor, bucketDict, visited, bucket);
                    clustersThisFloor.Add(cluster);

                    // Map all blocks in this cluster
                    foreach (IMySlimBlock block in cluster.Blocks)
                    {
                        blockToFloorClusterSlicesMap[gridFloor][block.Position] = clustersThisFloor.Count - 1; // Should be the index of the last added element, which is this cluster containing these blocks. 
                    }
                }
            }
        }

        private Color ParseColor(string colorString)
        {
            if (string.IsNullOrEmpty(colorString))
            {
                return new Vector3(1f, 0.5f, 0.0) * 1f;  // Default color if no data
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
            return new Vector3(1f, 0.5f, 0.0) * 1f;  // Default color on parse failure
        }

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
        public Vector3 secondaryColor(Vector3 color)
        {
            float h, s, v;
            float r = color.X, g = color.Y, b = color.Z; // Red color example
            RGBtoHSV(r, g, b, out h, out s, out v); // Convert from RGB to HSV
            float complementaryHue = GetComplementaryHue(h); // Get complementary hue
            HSVtoRGB(complementaryHue, s, v, out r, out g, out b); // Convert back to RGB

            Vector3 retColor = new Vector3(r, g, b);
            return retColor;
        }

        public void SetHologramRotation(bool isTarget, int direction, float degrees)
        {
            if (localGridControlledEntity != null && localGrid != null)
            {
                if (!isTarget)
                {
                    switch (direction)
                    {
                        case 0:
                            localGridControlledEntityCustomData.holoLocalRotationX += degrees;
                            if (localGridControlledEntityCustomData.holoLocalRotationX >= 360f) 
                            {
                                localGridControlledEntityCustomData.holoLocalRotationX = 0f;
                            }
                            SetParameter(localGridControlledEntity, "HoloLocalRotationX", localGridControlledEntityCustomData.holoLocalRotationX.ToString());
                            break;
                        case 1:
                            localGridControlledEntityCustomData.holoLocalRotationY += degrees;
                            if (localGridControlledEntityCustomData.holoLocalRotationY >= 360f)
                            {
                                localGridControlledEntityCustomData.holoLocalRotationY = 0f;
                            }
                            SetParameter(localGridControlledEntity, "HoloLocalRotationY", localGridControlledEntityCustomData.holoLocalRotationY.ToString());
                            break;
                        case 2:
                            localGridControlledEntityCustomData.holoLocalRotationZ += degrees;
                            if (localGridControlledEntityCustomData.holoLocalRotationZ >= 360f)
                            {
                                localGridControlledEntityCustomData.holoLocalRotationZ = 0f;
                            }
                            SetParameter(localGridControlledEntity, "HoloLocalRotationZ", localGridControlledEntityCustomData.holoLocalRotationZ.ToString());
                            break;
                    }
                }
                else
                {
                    switch (direction)
                    {
                        case 0:
                            localGridControlledEntityCustomData.holoTargetRotationX += degrees;
                            if (localGridControlledEntityCustomData.holoTargetRotationX >= 360f)
                            {
                                localGridControlledEntityCustomData.holoTargetRotationX = 0f;
                            }
                            SetParameter(localGridControlledEntity, "HoloTargetRotationX", localGridControlledEntityCustomData.holoTargetRotationX.ToString());
                            break;
                        case 1:
                            localGridControlledEntityCustomData.holoTargetRotationY += degrees;
                            if (localGridControlledEntityCustomData.holoTargetRotationY >= 360f)
                            {
                                localGridControlledEntityCustomData.holoTargetRotationY = 0f;
                            }
                            SetParameter(localGridControlledEntity, "HoloTargetRotationY", localGridControlledEntityCustomData.holoTargetRotationY.ToString());
                            break;
                        case 2:
                            localGridControlledEntityCustomData.holoTargetRotationZ += degrees;
                            if (localGridControlledEntityCustomData.holoTargetRotationZ >= 360f)
                            {
                                localGridControlledEntityCustomData.holoTargetRotationZ = 0f;
                            }
                            SetParameter(localGridControlledEntity, "HoloTargetRotationZ", localGridControlledEntityCustomData.holoTargetRotationZ.ToString());
                            break;
                    }
                }
            }
        }

        public void SetHologramViewType(bool isTarget, HologramViewType viewType) 
        {
            if (!isTarget)
            {
                localGridControlledEntityCustomData.localGridHologramViewType = viewType;
                SetParameter(localGridControlledEntity, "HologramViewLocal_Current", localGridControlledEntityCustomData.localGridHologramViewType.ToString());
            }
            else 
            {
                localGridControlledEntityCustomData.targetGridHologramViewType = viewType;
                SetParameter(localGridControlledEntity, "HologramViewTarget_Current", localGridControlledEntityCustomData.targetGridHologramViewType.ToString());
            }
        }


        public void SetParameter(IMyTerminalBlock block, string key, string value)
        {
            MyIni ini = new MyIni();
            string mySection = "EliDang"; // Your section
            MyIniParseResult result;

            ini.Clear();
            ini.TryParse(block.CustomData, out result);

            // Set or update the key
            ini.Set(mySection, key, value);

            // Write back with preserved raw text (EndContent)
            block.CustomData = ini.ToString();
        }


        private void ReadCustomDataControlledEntity(MyIni ini, ControlledEntityCustomData theData, IMyTerminalBlock block)
        {
            string mySection = "EliDang";

            // Master
            theData.masterEnabled = ini.Get(mySection, "ScannerEnable").ToBoolean(true);
            ini.Set(mySection, "ScannerEnable", theData.masterEnabled.ToString());

            // Holograms Master
            theData.enableHolograms = ini.Get(mySection, "ScannerHolo").ToBoolean(true);
            ini.Set(mySection, "ScannerHolo", theData.enableHolograms.ToString());

            // Hologram Local Grid
            theData.enableHologramsLocalGrid = ini.Get(mySection, "ScannerHoloYou").ToBoolean(true);
            ini.Set(mySection, "ScannerHoloYou", theData.enableHologramsLocalGrid.ToString());

            // Hologram Target Grid
            theData.enableHologramsTargetGrid = ini.Get(mySection, "ScannerHoloThem").ToBoolean(true);
            ini.Set(mySection, "ScannerHoloThem", theData.enableHologramsTargetGrid.ToString());

            // Local Hologram view
            HologramViewType theLocalSide;
            string hologramViewLocal_CurrentString = ini.Get(mySection, "HologramViewLocal_Current").ToString("0");
            if (Enum.TryParse<HologramViewType>(hologramViewLocal_CurrentString, out theLocalSide))
            {
                theData.localGridHologramViewType = theLocalSide;
            }
            ini.Set(mySection, "HologramViewLocal_Current", theData.localGridHologramViewType.ToString());

            // Target Hologram view
            HologramViewType theTargetSide;
            string hologramViewTarget_CurrentString = ini.Get(mySection, "HologramViewTarget_Current").ToString("7");
            if (Enum.TryParse<HologramViewType>(hologramViewTarget_CurrentString, out theTargetSide))
            {
                theData.targetGridHologramViewType = theTargetSide;
            }
            ini.Set(mySection, "HologramViewTarget_Current", theData.targetGridHologramViewType.ToString());

            // Local Hologram Rotation
            theData.holoLocalRotationX = ini.Get(mySection, "HoloLocalRotationX").ToSingle(0f);
            ini.Set(mySection, "HoloLocalRotationX", theData.holoLocalRotationX.ToString());

            theData.holoLocalRotationY = ini.Get(mySection, "HoloLocalRotationY").ToSingle(0f);
            ini.Set(mySection, "HoloLocalRotationY", theData.holoLocalRotationY.ToString());

            theData.holoLocalRotationZ = ini.Get(mySection, "HoloLocalRotationZ").ToSingle(0f);
            ini.Set(mySection, "HoloLocalRotationZ", theData.holoLocalRotationZ.ToString());

            // Target Hologram Rotation
            theData.holoTargetRotationX = ini.Get(mySection, "HoloTargetRotationX").ToSingle(0f);
            ini.Set(mySection, "HoloTargetRotationX", theData.holoTargetRotationX.ToString());

            theData.holoTargetRotationY = ini.Get(mySection, "HoloTargetRotationY").ToSingle(0f);
            ini.Set(mySection, "HoloTargetRotationY", theData.holoTargetRotationY.ToString());

            theData.holoTargetRotationZ = ini.Get(mySection, "HoloTargetRotationZ").ToSingle(0f);
            ini.Set(mySection, "HoloTargetRotationZ", theData.holoTargetRotationZ.ToString());

            // Local grid hologram angular velocity wiggle
            theData.localGridHologramAngularWiggle = ini.Get(mySection, "HologramViewFlat_AngularWiggle").ToBoolean(true);
            ini.Set(mySection, "HologramViewFlat_AngularWiggle", theData.localGridHologramAngularWiggle.ToString());

            // Toolbars
            theData.enableToolbars = ini.Get(mySection, "ScannerTools").ToBoolean(true);
            ini.Set(mySection, "ScannerTools", theData.enableToolbars.ToString());

            // Gauges
            theData.enableGauges = ini.Get(mySection, "ScannerGauges").ToBoolean(true);
            ini.Set(mySection, "ScannerGauges", theData.enableGauges.ToString());

            // Money
            theData.enableMoney = ini.Get(mySection, "ScannerMoney").ToBoolean(true);
            ini.Set(mySection, "ScannerMoney", theData.enableMoney.ToString());

            // Offset
            theData.scannerX = ini.Get(mySection, "ScannerX").ToDouble(0d);
            ini.Set(mySection, "ScannerX", theData.scannerX.ToString());

            theData.scannerY = ini.Get(mySection, "ScannerY").ToDouble(-0.2d);
            ini.Set(mySection, "ScannerY", theData.scannerY.ToString());

            theData.scannerZ = ini.Get(mySection, "ScannerZ").ToDouble(-0.575d);
            ini.Set(mySection, "ScannerZ", theData.scannerZ.ToString());
            theData.radarOffset = new Vector3D(theData.scannerX, theData.scannerY, theData.scannerZ);

            // Radar Scale and Radius
            theData.radarScale = ini.Get(mySection, "ScannerS").ToSingle(1);
            ini.Set(mySection, "ScannerS", theData.radarScale.ToString());
            theData.radarRadius = 0.125f * theData.radarScale;

            // Radar Brightness (glow)
            theData.radarBrightness = ini.Get(mySection, "ScannerB").ToSingle(1f);
            ini.Set(mySection, "ScannerB", theData.radarBrightness.ToString());

            // Radar/HUD color
            theData.scannerColor = ParseColor(ini.Get(mySection, "ScannerColor").ToString(Convert.ToString(theSettings.lineColorDefault))); //new Vector3(1f, 0.5f, 0.0)
            ini.Set(mySection, "ScannerColor", $"{theData.scannerColor.R},{theData.scannerColor.G},{theData.scannerColor.B}");
            Vector4 tempColor = (theData.scannerColor).ToVector4() * theData.radarBrightness;
            theData.lineColorRGB = new Vector3(tempColor.X, tempColor.Y, tempColor.Z);
            theData.lineColor = new Vector4(theData.lineColorRGB, 1f);
            theData.lineColorRGBComplimentary = secondaryColor(theData.lineColorRGB) * 2 + new Vector3(0.01f, 0.01f, 0.01f);
            theData.lineColorComp = new Vector4(theData.lineColorRGBComplimentary, 1f);

            // Velocity Toggle
            theData.enableVelocityLines = ini.Get(mySection, "ScannerLines").ToBoolean(true);
            ini.Set(mySection, "ScannerLines", theData.enableVelocityLines.ToString());

            // Orbit Line Speed Threshold
            theData.orbitSpeedThreshold = ini.Get(mySection, "ScannerOrbits").ToSingle(500);
            ini.Set(mySection, "ScannerOrbits", theData.orbitSpeedThreshold.ToString());

            // Voxel toggle
            theData.scannerShowVoxels = ini.Get(mySection, "ScannerShowVoxels").ToBoolean(true);
            ini.Set(mySection, "ScannerShowVoxels", theData.scannerShowVoxels.ToString());

            // Powered only toggle
            theData.scannerOnlyPoweredGrids = ini.Get(mySection, "ScannerOnlyPoweredGrids").ToBoolean(true);
            ini.Set(mySection, "ScannerOnlyPoweredGrids", theData.scannerOnlyPoweredGrids.ToString());

            // Save custom data back to block (preserves EndContent too, so text and other config sections don't get eliminated)
            block.CustomData = ini.ToString();
        }

        

        private void ReadCustomDataHoloRadar(MyIni ini, HoloRadarCustomData theData, IMyTerminalBlock block)
        {
            string mySection = "EliDang";

            // Master
            theData.scannerEnable = ini.Get(mySection, "ScannerEnable").ToBoolean(true);
            ini.Set(mySection, "ScannerEnable", theData.scannerEnable.ToString());

            // Offset
            theData.scannerX = ini.Get(mySection, "ScannerX").ToDouble(0d);
            ini.Set(mySection, "ScannerX", theData.scannerX.ToString());

            theData.scannerY = ini.Get(mySection, "ScannerY").ToDouble(0.7d);
            ini.Set(mySection, "ScannerY", theData.scannerY.ToString());

            theData.scannerZ = ini.Get(mySection, "ScannerZ").ToDouble(0d);
            ini.Set(mySection, "ScannerZ", theData.scannerZ.ToString());

            // Radius
            theData.scannerRadius = ini.Get(mySection, "ScannerRadius").ToSingle(1f);
            ini.Set(mySection, "ScannerRadius", theData.scannerRadius.ToString());

            // Voxel toggle
            theData.scannerShowVoxels = ini.Get(mySection, "ScannerShowVoxels").ToBoolean(true);
            ini.Set(mySection, "ScannerShowVoxels", theData.scannerShowVoxels.ToString());

            // Powered only toggle
            theData.scannerOnlyPoweredGrids = ini.Get(mySection, "ScannerOnlyPoweredGrids").ToBoolean(true);
            ini.Set(mySection, "ScannerOnlyPoweredGrids", theData.scannerOnlyPoweredGrids.ToString());

            // Save custom data back to block (preserves EndContent too, so text and other config sections don't get eliminated)
            block.CustomData = ini.ToString();
        }



        private void ReadCustomDataHologram(MyIni ini, HologramCustomData theData, IMyTerminalBlock block)
        {
            string mySection = "EliDang";

            // Master
            theData.holoEnable = ini.Get(mySection, "HoloEnable").ToBoolean(true);
            ini.Set(mySection, "HoloEnable", theData.holoEnable.ToString());

            // Offset
            theData.holoX = ini.Get(mySection, "HoloX").ToDouble(0d);
            ini.Set(mySection, "HoloX", theData.holoX.ToString());

            theData.holoY = ini.Get(mySection, "HoloY").ToDouble(0.7d);
            ini.Set(mySection, "HoloY", theData.holoY.ToString());

            theData.holoZ = ini.Get(mySection, "HoloZ").ToDouble(0d);
            ini.Set(mySection, "HoloZ", theData.holoZ.ToString());

            // Scale
            theData.holoScale = ini.Get(mySection, "HoloScale").ToSingle(0.1f);
            ini.Set(mySection, "HoloScale", theData.holoScale.ToString());

            // Side
            theData.holoSide = ini.Get(mySection, "HoloSide").ToInt32(0);
            ini.Set(mySection, "HoloSide", theData.holoSide.ToString());

            // Rotation
            theData.holoRotationX = ini.Get(mySection, "HoloRotationX").ToInt32(0);
            ini.Set(mySection, "HoloRotationX", theData.holoRotationX.ToString());

            theData.holoRotationY = ini.Get(mySection, "HoloRotationY").ToInt32(0);
            ini.Set(mySection, "HoloRotationY", theData.holoRotationY.ToString());

            theData.holoRotationZ = ini.Get(mySection, "HoloRotationZ").ToInt32(0);
            ini.Set(mySection, "HoloRotationZ", theData.holoRotationZ.ToString());

            // Base
            theData.holoBaseX = ini.Get(mySection, "HoloBaseX").ToDouble(0d);
            ini.Set(mySection, "HoloBaseX", theData.holoBaseX.ToString());

            theData.holoBaseY = ini.Get(mySection, "HoloBaseY").ToDouble(-0.5d);
            ini.Set(mySection, "HoloBaseY", theData.holoBaseY.ToString());

            theData.holoBaseZ = ini.Get(mySection, "HoloBaseZ").ToDouble(0d);
            ini.Set(mySection, "HoloBaseZ", theData.holoBaseZ.ToString());

            // Holo Brightness (glow)
            theData.holoBrightness = ini.Get(mySection, "HoloB").ToSingle(1f);
            ini.Set(mySection, "HoloB", theData.holoBrightness.ToString());

            // Radar/HUD color
            theData.holoColor = ParseColor(ini.Get(mySection, "HoloColor").ToString(Convert.ToString(theSettings.lineColorDefault))); //new Vector3(1f, 0.5f, 0.0)
            ini.Set(mySection, "HoloColor", $"{theData.holoColor.R},{theData.holoColor.G},{theData.holoColor.B}");
            Vector4 tempColor = (theData.holoColor).ToVector4() * theData.holoBrightness;
            theData.lineColorRGB = new Vector3(tempColor.X, tempColor.Y, tempColor.Z);
            theData.lineColor = new Vector4(theData.lineColorRGB, 1f);
            theData.lineColorRGBComplimentary = secondaryColor(theData.lineColorRGB) * 2 + new Vector3(0.01f, 0.01f, 0.01f);
            theData.lineColorComp = new Vector4(theData.lineColorRGBComplimentary, 1f);

            // Save custom data back to block (preserves EndContent too, so text and other config sections don't get eliminated)
            block.CustomData = ini.ToString();
        }


    }
}

