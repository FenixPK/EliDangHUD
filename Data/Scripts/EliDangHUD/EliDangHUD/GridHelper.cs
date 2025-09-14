using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.Models;
using VRage.Game.ObjectBuilders.Components;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using VRageRender;
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

        public Vector3D localGridWorldVolumeCenterInit;
        private ClusterBuildState localGridClusterBuildState;
        private int localGridClusterBlocksUpdateTicksCounter = 0;
        Dictionary<Vector3I, BlockCluster> localGridNewClusters = new Dictionary<Vector3I, BlockCluster>();
        Dictionary<Vector3I, Vector3I> localGridNewBlockToClusterMap = new Dictionary<Vector3I, Vector3I>();
        

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
        
        public Dictionary<Vector3I, GridBlock> localGridAllBlocksDict = new Dictionary<Vector3I, GridBlock>();
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

        public Vector3D targetGridWorldVolumeCenterInit;
        private ClusterBuildState targetGridClusterBuildState;
        private int targetGridClusterBlocksUpdateTicksCounter = 0;
        Dictionary<Vector3I, BlockCluster> targetGridNewClusters = new Dictionary<Vector3I, BlockCluster>();
        Dictionary<Vector3I, Vector3I> targetGridNewBlockToClusterMap = new Dictionary<Vector3I, Vector3I>();

        public Dictionary<Vector3I, GridBlock> targetGridAllBlocksDict = new Dictionary<Vector3I, GridBlock>();
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

        private MatrixD CreateNormalizedLocalGridRotationMatrix()
        {
            Vector3D angularVelocity = localGrid?.Physics?.AngularVelocity ?? Vector3D.Zero;

            // Transform the angular velocity from world space to grid space. This makes it relative to the grid instead of the world plane so it doesn't matter which way
            // the grid is currently facing.
            MatrixD worldToGrid = MatrixD.Invert(localGrid.WorldMatrix);
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
        private double ClampAndScale(double angularVelocity, double maxVelocity, double maxAngle)
        {
            // Normalize the angular velocity to (-1, 1) range
            double normalizedVelocity = angularVelocity / maxVelocity;

            // Clamp to prevent values outside (-1, 1)
            normalizedVelocity = Math.Max(-1.0, Math.Min(1.0, normalizedVelocity));

            // Scale to the desired angle range
            return normalizedVelocity * maxAngle;
        }

        
        
        private void OnLocalBlockAdded(VRage.Game.ModAPI.IMySlimBlock block)
        {
            if (localGridBlocksInitialized && localGrid != null) // Safety check
            {
                Vector3D delta = localGrid.WorldVolume.Center - localGridWorldVolumeCenterInit; // Calculate delta so all new blocks are added relative to where the initial blocks would be added.

                // New logic, store the pre-computed worldCenter. This is because we want holograms to rotate around the grid's world volume center.
                // I could apply an offset at Draw, but this is more computationally efficient. It also is required for building the block clusters at the appropriate position. 
                MatrixD inverseMatrix = MatrixD.Invert(GetRotationMatrix(localGrid.WorldMatrix));
                Vector3D blockWorldPosition;
                Vector3D blockScaledInvertedPosition;
                block.ComputeWorldCenter(out blockWorldPosition); // Gets world position for the center of the block
                blockScaledInvertedPosition = Vector3D.Transform((blockWorldPosition - localGrid.WorldVolume.Center) + delta, inverseMatrix) / localGrid.GridSize; // set scaledPosition to be relative to the center of the grid, invert it, then scale it to block units.

                GridBlock gridBlock = new GridBlock();
                gridBlock.Block = block;
                gridBlock.DrawPosition = blockScaledInvertedPosition;

                localGridAllBlocksDict[block.Position] = gridBlock;


                //localGridAllBlocksDict[block.Position] = block;
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
                        if (!string.IsNullOrEmpty(terminal.CustomName) && terminal.CustomName.Contains("[ELI_LOCAL]"))
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
                        if (!string.IsNullOrEmpty(terminal.CustomName) && terminal.CustomName.Contains("[ELI_HOLO]"))
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
                        localGridAntennasDict[antenna.Position] = antenna;
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
                
                localGridClusterBuildState = null;
                localGridClusterBlocksUpdateTicksCounter = 0;
                localGridClustersNeedRefresh = true; // In case we are using block clusters instead of slices
                
            }
        }

        private void OnLocalBlockRemoved(VRage.Game.ModAPI.IMySlimBlock block)
        {
            if (localGridBlocksInitialized && localGrid != null) // Safety check
            {
                if (block.ComponentStack != null) 
                {
                    block.ComponentStack.IntegrityChanged -= OnLocalBlockIntegrityChanged;
                    localGridBlockComponentStacks.Remove(block.ComponentStack);
                }

                Dictionary<Vector3I, IMySlimBlock> floorDict;
                if (localGridAllBlocksDictByFloor.TryGetValue(block.Position.Y, out floorDict))
                {
                    floorDict.Remove(block.Position);
                }

                localGridAllBlocksDict.Remove(block.Position);

                if (block.FatBlock != null) 
                {
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
                }
               
                localGridCurrentIntegrity -= block.Integrity;
                localGridMaxIntegrity -= block.MaxIntegrity;

                localHologramScaleNeedsRefresh = true;
                
                RemoveBlockFromCluster(block.Position, ref localGridBlockClusters, ref localGridBlockToClusterMap);
                localGridClusterBuildState = null;
                localGridClusterBlocksUpdateTicksCounter = 0;
                localGridClustersNeedRefresh = true; // In case we are using block clusters instead of slices  
                
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
                    GridBlock gridBlock;
                    if (localGridAllBlocksDict.TryGetValue(stackPositionKey, out gridBlock)) 
                    {
                        block = gridBlock.Block;
                        if (block != null)
                        {
                            float integrityDiff = newIntegrity - oldIntegrity;
                            localGridCurrentIntegrity += integrityDiff;

                            // If we are using block clusters we can update the integrity of the cluster this block belongs to. 
                            Vector3I clusterKey;
                            if (localGridBlockToClusterMap.TryGetValue(block.Position, out clusterKey))
                            {
                                BlockCluster clusterEntry;
                                if (localGridBlockClusters.TryGetValue(clusterKey, out clusterEntry))
                                {
                                    clusterEntry.Integrity += integrityDiff;
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
                Vector3D delta = localGrid.WorldVolume.Center - localGridWorldVolumeCenterInit; // Calculate delta so all new blocks are added relative to where the initial blocks would be added.

                MatrixD inverseMatrix = MatrixD.Invert(GetRotationMatrix(targetGrid.WorldMatrix));
                Vector3D blockWorldPosition;
                Vector3D blockScaledInvertedPosition;
                block.ComputeWorldCenter(out blockWorldPosition); // Gets world position for the center of the block
                blockScaledInvertedPosition = Vector3D.Transform((blockWorldPosition - targetGrid.WorldVolume.Center) + delta, inverseMatrix) / targetGrid.GridSize; // set scaledPosition to be relative to the center of the grid, invert it, then scale it to block units.

                GridBlock gridBlock = new GridBlock();
                gridBlock.Block = block;
                gridBlock.DrawPosition = blockScaledInvertedPosition;

                targetGridAllBlocksDict[block.Position] = gridBlock;

                //targetGridAllBlocksDict[block.Position] = block;
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
                        targetGridAntennasDict[antenna.Position] = antenna;
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
                
                targetGridClusterBuildState = null;
                targetGridClusterBlocksUpdateTicksCounter = 0;
                targetGridClustersNeedRefresh = true; // In case we are using block clusters instead of slices
                
            }
        }

        private void OnTargetBlockRemoved(VRage.Game.ModAPI.IMySlimBlock block)
        {
            if (targetGridBlocksInitialized && targetGrid != null) // Safety check
            {
                if (block.ComponentStack != null) 
                {
                    block.ComponentStack.IntegrityChanged -= OnTargetBlockIntegrityChanged;
                    targetGridBlockComponentStacks.Remove(block.ComponentStack);
                }

                Dictionary<Vector3I, IMySlimBlock> floorDict;
                if (targetGridAllBlocksDictByFloor.TryGetValue(block.Position.Y, out floorDict))
                {
                    floorDict.Remove(block.Position);
                }

                targetGridAllBlocksDict.Remove(block.Position);

                if (block.FatBlock != null) 
                {
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
                }
               
                targetGridCurrentIntegrity -= block.Integrity;
                targetGridMaxIntegrity -= block.MaxIntegrity;

                targetHologramScaleNeedsRefresh = true;

                RemoveBlockFromCluster(block.Position, ref targetGridBlockClusters, ref targetGridBlockToClusterMap);
                targetGridClusterBuildState = null;
                targetGridClusterBlocksUpdateTicksCounter = 0;
                targetGridClustersNeedRefresh = true; // In case we are using block clusters instead of slices
                
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
                    //block = targetGridAllBlocksDict[stackPositionKey];
                    GridBlock gridBlock;
                    if (targetGridAllBlocksDict.TryGetValue(stackPositionKey, out gridBlock)) 
                    {
                        block = gridBlock.Block;
                        if (block != null)
                        {
                            float integrityDiff = newIntegrity - oldIntegrity;
                            targetGridCurrentIntegrity += integrityDiff;

                            // If we are using block clusters we can updaste the integrity of the cluster this block belongs to. 
                            Vector3I clusterKey;
                            if (targetGridBlockToClusterMap.TryGetValue(block.Position, out clusterKey))
                            {
                                BlockCluster clusterEntry;
                                if (targetGridBlockClusters.TryGetValue(clusterKey, out clusterEntry))
                                {
                                    clusterEntry.Integrity += integrityDiff;
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

                // Trigger Update
                targetGridClusterSize = GetClusterSize(targetGridAllBlocksDict.Count, theSettings.blockCountClusterStep);
                int minClusterSizeTarget = targetGridClusterSize - 1 > 0 ? targetGridClusterSize - 1 : 1;
                int maxClusterSizeTarget = targetGridClusterSize + minClusterSizeTarget;

                // Start a new build
                if (targetGridClusterBuildState == null)
                {
                    targetGridNewClusters = new Dictionary<Vector3I, BlockCluster>();
                    targetGridNewBlockToClusterMap = new Dictionary<Vector3I, Vector3I>();
                    StartClusterBlocksIterativeThreshold(targetGridAllBlocksDict, maxClusterSizeTarget);
                }

                // Then every tick:
                bool done = ContinueClusterBlocksIterativeThreshold(targetGridAllBlocksDict, ref targetGridNewClusters, ref targetGridNewBlockToClusterMap, 999999, minClusterSizeTarget, theSettings.clusterSplitThreshhold);

                if (done)
                {
                    targetGridBlockClusters = targetGridNewClusters;
                    targetGridNewClusters = null;
                    targetGridBlockToClusterMap = targetGridNewBlockToClusterMap;
                    targetGridNewBlockToClusterMap = null;
                    targetGridClusterBuildState = null;
                    targetGridClustersNeedRefresh = false;
                }

                //targetGridClusterSize = GetClusterSize(targetGridAllBlocksDict.Count, theSettings.blockCountClusterStep);
                ////ClusterBlocks(targetGridAllBlocksDict, ref targetGridBlockClusters, ref targetGridBlockToClusterMap, targetGridClusterSize);
                ////ClusterBlocksGreedyTiled(targetGridAllBlocksDict, ref targetGridBlockClusters, ref targetGridBlockToClusterMap, 3);
                //int minClusterSize = targetGridClusterSize - 1 > 0 ? targetGridClusterSize - 1 : 1;
                //int maxClusterSize = targetGridClusterSize + minClusterSize;
                //ClusterBlocksIterativeThreshold(targetGridAllBlocksDict, ref targetGridBlockClusters, ref targetGridBlockToClusterMap, maxClusterSize, minClusterSize, 0.33);
                //targetGridClustersNeedRefresh = false;

                // Add Event Handlers
                targetGrid.OnBlockAdded += OnTargetBlockAdded;
                targetGrid.OnBlockRemoved += OnTargetBlockRemoved;

                targetGridHologramActivationTime = 0;
                targetGridHologramBootUpAlpha = ClampedD(targetGridHologramActivationTime, 0, 1);
                targetGridHologramBootUpAlpha = Math.Pow(targetGridHologramBootUpAlpha, 0.25);

                if (localGridControlledEntityInitialized) 
                {
                    Quaternion initialQuat = Quaternion.CreateFromYawPitchRoll(
                              MathHelper.ToRadians(localGridControlledEntityCustomData.holoTargetRotationX),
                              MathHelper.ToRadians(localGridControlledEntityCustomData.holoTargetRotationY),
                              MathHelper.ToRadians(localGridControlledEntityCustomData.holoTargetRotationZ));
                    // Convert back to matrix
                    targetHologramViewRotationCurrent = MatrixD.CreateFromQuaternion(initialQuat);
                    targetHologramFinalRotation = targetHologramViewRotationCurrent;
                }
                

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
            targetGridJumpDrivesDict.Clear();

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

            targetGridJumpDrivePowerStoredPrevious = 0;
            targetGridJumpDrivePowerStoredMax = 0;
            targetGridJumpDrivePowerStored = 0;
            targetGridJumpDriveTimeToReady = 0;

            targetGridWorldVolumeCenterInit = Vector3D.Zero;

            targetHologramScalingMatrix = MatrixD.Identity;
            targetHologramViewRotationCurrent = MatrixD.Identity;
            targetHologramViewRotationGoal = MatrixD.Identity;
            targetHologramFinalRotation = MatrixD.Identity;


            targetGridHasPassiveRadar = false;
            targetGridHasActiveRadar = false;
            targetGridMaxPassiveRadarRange = 0.0;
            targetGridMaxActiveRadarRange = 0.0;


            targetGridClusterSize = 1;
            targetGridClustersNeedRefresh = false;

 

            targetGridHologramActivationTime = 0;
            targetGridHologramBootUpAlpha = 1;
        }


        private void InitializeTargetGridBlocks()
        {
            if (targetGrid == null)
            {
                return;
            }

            List<VRage.Game.ModAPI.IMySlimBlock> blocks = new List<VRage.Game.ModAPI.IMySlimBlock>();
            targetGrid.GetBlocks(blocks);
            MatrixD inverseMatrix = MatrixD.Invert(GetRotationMatrix(targetGrid.WorldMatrix));
            targetGridWorldVolumeCenterInit = targetGrid.WorldVolume.Center;

            foreach (VRage.Game.ModAPI.IMySlimBlock block in blocks)
            {
                
                Vector3D blockWorldPosition;
                Vector3D blockScaledInvertedPosition;
                block.ComputeWorldCenter(out blockWorldPosition); // Gets world position for the center of the block
                blockScaledInvertedPosition = Vector3D.Transform((blockWorldPosition - targetGrid.WorldVolume.Center), inverseMatrix) / targetGrid.GridSize; // set scaledPosition to be relative to the center of the grid, invert it, then scale it to block units.
                blockScaledInvertedPosition.X = -blockScaledInvertedPosition.X;
                GridBlock gridBlock = new GridBlock();
                gridBlock.Block = block;
                gridBlock.DrawPosition = blockScaledInvertedPosition;

                targetGridAllBlocksDict[block.Position] = gridBlock;

                //targetGridAllBlocksDict[block.Position] = block;
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
                float difPower = (jumpDriveChargeCurrent - targetGridJumpDrivePowerStoredPrevious);

                if (difPower > 0)
                {
                    double powerPerSecond = difPower / deltaTimeSinceLastTick;

                    if (powerPerSecond > 0)
                    {
                        double timeRemaining = ((jumpDriveChargeMax - jumpDriveChargeCurrent) / powerPerSecond) * 100;

                        if (timeRemaining < minTime)
                        {
                            minTime = timeRemaining;
                        }
                    }
                }
            }
            targetGridJumpDrivePowerStoredPrevious = targetGridJumpDrivePowerStored;
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

                
                // On initialization we start a build, but set the number of clusters to process to an amount sufficient to build the whole hologram in one go. 
                localGridClusterSize = GetClusterSize(localGridAllBlocksDict.Count, theSettings.blockCountClusterStep);
                int minClusterSize = localGridClusterSize - 1 > 0 ? localGridClusterSize - 1 : 1;
                int maxClusterSize = localGridClusterSize + minClusterSize;

                if (localGridClusterBuildState == null)
                {
                    localGridNewClusters = new Dictionary<Vector3I, BlockCluster>();
                    localGridNewBlockToClusterMap = new Dictionary<Vector3I, Vector3I>();
                    StartClusterBlocksIterativeThreshold(localGridAllBlocksDict, maxClusterSize);
                }
                bool done = ContinueClusterBlocksIterativeThreshold(localGridAllBlocksDict, ref localGridNewClusters, ref localGridNewBlockToClusterMap, 999999, minClusterSize, theSettings.clusterSplitThreshhold);

                if (done)
                {
                    localGridBlockClusters = localGridNewClusters;
                    localGridNewClusters = null;
                    localGridBlockToClusterMap = localGridNewBlockToClusterMap;
                    localGridNewBlockToClusterMap = null;
                    localGridClusterBuildState = null;
                    localGridClustersNeedRefresh = false;
                }

                //localGridClusterSize = GetClusterSize(localGridAllBlocksDict.Count, theSettings.blockCountClusterStep);
                //int minClusterSize = localGridClusterSize - 1 > 0 ? localGridClusterSize - 1 : 1;
                //int maxClusterSize = localGridClusterSize + minClusterSize;
                //ClusterBlocksIterativeThreshold(localGridAllBlocksDict, ref localGridBlockClusters, ref localGridBlockToClusterMap, maxClusterSize, minClusterSize, 0.33);
                ////ClusterBlocks(localGridAllBlocksDict, ref localGridBlockClusters, ref localGridBlockToClusterMap, localGridClusterSize);
                //localGridClustersNeedRefresh = false;

                // Add Event Handlers
                localGrid.OnBlockAdded += OnLocalBlockAdded;
                localGrid.OnBlockRemoved += OnLocalBlockRemoved;

                InitializeTheSettings();

                localGridHologramActivationTime = 0;
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

               

                Quaternion initialQuat = Quaternion.CreateFromYawPitchRoll(
                           MathHelper.ToRadians(localGridControlledEntityCustomData.holoLocalRotationX),
                           MathHelper.ToRadians(localGridControlledEntityCustomData.holoLocalRotationY),
                           MathHelper.ToRadians(localGridControlledEntityCustomData.holoLocalRotationZ));
                // Convert back to matrix
                localHologramViewRotationCurrent = MatrixD.CreateFromQuaternion(initialQuat);
                localHologramFinalRotation = localHologramViewRotationCurrent;

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

            localGridWorldVolumeCenterInit = Vector3D.Zero;
            localGridVelocity = Vector3D.Zero;
            localGridVelocityAngular = Vector3D.Zero;
            localGridSpeed = 0f;

            localHologramScalingMatrix = MatrixD.Identity;
            localHologramViewRotationCurrent = MatrixD.Identity;
            localHologramViewRotationGoal = MatrixD.Identity;
            localHologramFinalRotation = MatrixD.Identity;
            
            localGridHasPassiveRadar = false;
            localGridHasActiveRadar = false;
            localGridMaxPassiveRadarRange = 0.0;
            localGridMaxActiveRadarRange = 0.0;

            localGridClusterSize = 1;


            localGridHologramActivationTime = 0;
            localGridHologramBootUpAlpha = 1;

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
                    if (localGridControlledEntity.CubeGrid == localGrid)
                    {
                        localGridControlledEntity.CustomDataChanged -= OnControlledEntityCustomDataChanged;
                        localGridControlledEntity = null;
                    }
                    else
                    {
                        // If the seat belongs to a different grid now (undock/merge/split), do a full reset.
                        ResetLocalGrid();
                    }
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
            MatrixD inverseMatrix = MatrixD.Invert(GetRotationMatrix(localGrid.WorldMatrix));
            localGridWorldVolumeCenterInit = localGrid.WorldVolume.Center;
            foreach (VRage.Game.ModAPI.IMySlimBlock block in blocks)
            {
                // New logic, store the pre-computed worldCenter. This is because we want holograms to rotate around the grid's world volume center.
                // I could apply an offset at Draw, but this is more computationally efficient. It also is required for building the block clusters at the appropriate position. 
                
                Vector3D blockWorldPosition;
                Vector3D blockScaledInvertedPosition;
                block.ComputeWorldCenter(out blockWorldPosition); // Gets world position for the center of the block
                blockScaledInvertedPosition = Vector3D.Transform((blockWorldPosition - localGrid.WorldVolume.Center), inverseMatrix) / localGrid.GridSize; // set scaledPosition to be relative to the center of the grid, invert it, then scale it to block units.

                GridBlock gridBlock = new GridBlock();
                gridBlock.Block = block;
                gridBlock.DrawPosition = blockScaledInvertedPosition;

                localGridAllBlocksDict[block.Position] = gridBlock;

                //localGridAllBlocksDict[block.Position] = block;
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

                // This is set to 0 on initialization, then increments each tick from there so long as a target is selected. Resets on loss of target, and is reset again on initialization. 
                localGridHologramActivationTime += deltaTimeSinceLastTick * 0.667;
                localGridHologramBootUpAlpha = ClampedD(localGridHologramActivationTime, 0, 1);
                localGridHologramBootUpAlpha = Math.Pow(localGridHologramBootUpAlpha, 0.25);

                if (localHologramScaleNeedsRefresh)
                {
                    UpdateLocalGridScalingMatrix();
                }

                UpdateLocalGridControlledEntityCustomData();
                UpdateLocalGridRadarCustomData();
                UpdateLocalGridHologramCustomData();
                UpdateLocalGridRotationMatrix();

                if (localGridClustersNeedRefresh)
                {
                    if (localGridClusterBlocksUpdateTicksCounter > theSettings.ticksUntilClusterRebuildAfterChange)
                    {
                        // Trigger Update
                        localGridClusterSize = GetClusterSize(localGridAllBlocksDict.Count, theSettings.blockCountClusterStep);
                        int minClusterSize = localGridClusterSize - 1 > 0 ? localGridClusterSize - 1 : 1;
                        int maxClusterSize = localGridClusterSize + minClusterSize;

                        // Start a new build
                        if (localGridClusterBuildState == null)
                        {
                            localGridNewClusters = new Dictionary<Vector3I, BlockCluster>();
                            localGridNewBlockToClusterMap = new Dictionary<Vector3I, Vector3I>();
                            StartClusterBlocksIterativeThreshold(localGridAllBlocksDict, maxClusterSize);
                        }

                        // Then every tick:
                        bool done = ContinueClusterBlocksIterativeThreshold(localGridAllBlocksDict, ref localGridNewClusters, ref localGridNewBlockToClusterMap, theSettings.clusterRebuildClustersPerTick, minClusterSize, theSettings.clusterSplitThreshhold);


                        if (done)
                        {
                            localGridBlockClusters = localGridNewClusters;
                            localGridNewClusters = null;
                            localGridBlockToClusterMap = localGridNewBlockToClusterMap;
                            localGridNewBlockToClusterMap = null;
                            localGridClusterBuildState = null;
                            localGridClustersNeedRefresh = false;
                        }
                    }
                    localGridClusterBlocksUpdateTicksCounter++;



                    //localGridClusterSize = GetClusterSize(localGridAllBlocksDict.Count, theSettings.blockCountClusterStep);
                    //int minClusterSize = localGridClusterSize - 1 > 0 ? localGridClusterSize - 1 : 1;
                    //int maxClusterSize = localGridClusterSize + minClusterSize;
                    //ClusterBlocksIterativeThreshold(localGridAllBlocksDict, ref localGridBlockClusters, ref localGridBlockToClusterMap, maxClusterSize, minClusterSize, 0.33);
                    ////ClusterBlocks(localGridAllBlocksDict, ref localGridBlockClusters, ref localGridBlockToClusterMap, localGridClusterSize);
                    //localGridClustersNeedRefresh = false;

                }

                if (targetGrid != null) 
                {
                    if (!targetGridInitialized) 
                    {
                        InitializeTargetGrid();
                    }

                    UpdateTargetGridJumpDrives();

                    // This is set to 0 on initialization, then increments each tick from there so long as a target is selected. Resets on loss of target, and is reset again on initialization. 
                    targetGridHologramActivationTime += deltaTimeSinceLastTick * 0.667;
                    targetGridHologramBootUpAlpha = ClampedD(targetGridHologramActivationTime, 0, 1);
                    targetGridHologramBootUpAlpha = Math.Pow(targetGridHologramBootUpAlpha, 0.25);

                    if (targetHologramScaleNeedsRefresh)
                    {
                        UpdateTargetGridScalingMatrix();
                    }
                    UpdateTargetGridRotationMatrix();

                    if (targetGridClustersNeedRefresh)
                    {
                        if (targetGridClusterBlocksUpdateTicksCounter > theSettings.ticksUntilClusterRebuildAfterChange)
                        {
                            // Trigger Update
                            targetGridClusterSize = GetClusterSize(targetGridAllBlocksDict.Count, theSettings.blockCountClusterStep);
                            int minClusterSizeTarget = targetGridClusterSize - 1 > 0 ? targetGridClusterSize - 1 : 1;
                            int maxClusterSizeTarget = targetGridClusterSize + minClusterSizeTarget;

                            // Start a new build
                            if (targetGridClusterBuildState == null)
                            {
                                targetGridNewClusters = new Dictionary<Vector3I, BlockCluster>();
                                targetGridNewBlockToClusterMap = new Dictionary<Vector3I, Vector3I>();
                                StartClusterBlocksIterativeThreshold(targetGridAllBlocksDict, maxClusterSizeTarget);
                            }

                            // Then every tick:
                            bool done = ContinueClusterBlocksIterativeThreshold(targetGridAllBlocksDict, ref targetGridNewClusters, ref targetGridNewBlockToClusterMap, theSettings.clusterRebuildClustersPerTick, minClusterSizeTarget, theSettings.clusterSplitThreshhold);


                            if (done)
                            {
                                targetGridBlockClusters = targetGridNewClusters;
                                targetGridNewClusters = null;
                                targetGridBlockToClusterMap = targetGridNewBlockToClusterMap;
                                targetGridNewBlockToClusterMap = null;
                                targetGridClusterBuildState = null;
                                targetGridClustersNeedRefresh = false;
                            }
                        }
                        targetGridClusterBlocksUpdateTicksCounter++;


                        //targetGridClusterSize = GetClusterSize(targetGridAllBlocksDict.Count, theSettings.blockCountClusterStep);
                        ////ClusterBlocks(targetGridAllBlocksDict, ref targetGridBlockClusters, ref targetGridBlockToClusterMap, targetGridClusterSize);
                        ////ClusterBlocksGreedyTiled(targetGridAllBlocksDict, ref targetGridBlockClusters, ref targetGridBlockToClusterMap, 3);
                        //int minClusterSize = targetGridClusterSize - 1 > 0 ? targetGridClusterSize - 1 : 1;
                        //int maxClusterSize = targetGridClusterSize + minClusterSize;
                        //ClusterBlocksIterativeThreshold(targetGridAllBlocksDict, ref targetGridBlockClusters, ref targetGridBlockToClusterMap, maxClusterSize, minClusterSize, 0.33);
                        //targetGridClustersNeedRefresh = false;
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



                MatrixD rotationOnlyLocalGridMatrix = localGrid.WorldMatrix;
                rotationOnlyLocalGridMatrix.Translation = Vector3D.Zero;
                MatrixD angularRotationWiggle = MatrixD.Identity;
                

                // Compute interpolation factor, clamped 0–1
                float t = (float)Math.Min(deltaTimeSinceLastTick * lerpFactor, 1.0);

                // Build goal quaternion from Euler rotations (degrees in custom data)
                Quaternion goalQuat = Quaternion.CreateFromYawPitchRoll(
                    MathHelper.ToRadians(localGridControlledEntityCustomData.holoLocalRotationX),
                    MathHelper.ToRadians(localGridControlledEntityCustomData.holoLocalRotationY),
                    MathHelper.ToRadians(localGridControlledEntityCustomData.holoLocalRotationZ));

                // Extract current quaternion from the matrix
                Quaternion currentQuat;
                Quaternion.CreateFromRotationMatrix(ref localHologramViewRotationCurrent, out currentQuat);

                // Ensure shortest-path interpolation by flipping sign if needed
                //if (Quaternion.Dot(currentQuat, goalQuat) < 0)
                //    goalQuat = -goalQuat;

                // Smoothly interpolate
                currentQuat = Quaternion.Slerp(currentQuat, goalQuat, t);

                // Normalize to prevent floating point drift
                currentQuat.Normalize();

                // Convert back to matrix
                localHologramViewRotationCurrent = MatrixD.CreateFromQuaternion(currentQuat);

                // Final hologram rotation
                if (localGridControlledEntityCustomData.localGridHologramAngularWiggle)
                {
                    angularRotationWiggle = CreateNormalizedLocalGridRotationMatrix();  // Used to apply a "wiggle" effect to the hologram based on the angular velocity of the grid.
                    localHologramFinalRotation = angularRotationWiggle * localHologramViewRotationCurrent * rotationOnlyLocalGridMatrix;
                }
                else
                {
                    localHologramFinalRotation = localHologramViewRotationCurrent * rotationOnlyLocalGridMatrix;
                }
            }
        }

        public void UpdateFinalLocalGridHologramRotation()
        {
            if (localGrid != null && localGridInitialized)
            {
                //MatrixD rotationOnlyLocalGridMatrix = localGrid.WorldMatrix;
                //rotationOnlyLocalGridMatrix.Translation = Vector3D.Zero;
                //MatrixD angularRotationWiggle = MatrixD.Identity;
                //if (localGridControlledEntityCustomData.localGridHologramAngularWiggle)
                //{
                //    angularRotationWiggle = CreateNormalizedLocalGridRotationMatrix();  // Used to apply a "wiggle" effect to the hologram based on the angular velocity of the grid.
                //    localHologramFinalRotation = angularRotationWiggle * localHologramViewRotationCurrent * rotationOnlyLocalGridMatrix;
                //}
                //else 
                //{
                //    localHologramFinalRotation = localHologramViewRotationCurrent * rotationOnlyLocalGridMatrix;
                //}  
            }
        }


        public void UpdateTargetGridRotationMatrix()
        {
            if (targetGrid != null && targetGridInitialized && localGridControlledEntity != null && localGridControlledEntityInitialized && localGridControlledEntityCustomData != null) 
            {
                MatrixD rotationOnlyLocalGridMatrix = localGrid.WorldMatrix;
                rotationOnlyLocalGridMatrix.Translation = Vector3D.Zero;

                MatrixD rotationOnlyTargetGridMatrix = targetGrid.WorldMatrix;
                rotationOnlyTargetGridMatrix.Translation = Vector3D.Zero;

                //// Optional X flip (if needed for handedness)
                MatrixD flipX = MatrixD.Identity;
                flipX.M11 = -1; // Because getting the perspective cam to work required storing -X positions and mirroring together to look right. 

                switch (localGridControlledEntityCustomData.targetGridHologramViewType)
                {
                    case HologramViewType.Static:
                        double lerpFactor = Math.Min(deltaTimeSinceLastTick * 60, 60.0);
                        // Compute interpolation factor, clamped 0–1
                        float t = (float)Math.Min(deltaTimeSinceLastTick * lerpFactor, 1.0);

                        // Build goal quaternion from Euler rotations (degrees in custom data)
                        Quaternion goalQuat = Quaternion.CreateFromYawPitchRoll(
                            MathHelper.ToRadians(localGridControlledEntityCustomData.holoTargetRotationX),
                            MathHelper.ToRadians(localGridControlledEntityCustomData.holoTargetRotationY),
                            MathHelper.ToRadians(localGridControlledEntityCustomData.holoTargetRotationZ));

                        // Extract current quaternion from the matrix
                        Quaternion currentQuat;
                        Quaternion.CreateFromRotationMatrix(ref targetHologramViewRotationCurrent, out currentQuat);

                        // Ensure shortest-path interpolation by flipping sign if needed
                        //if (Quaternion.Dot(currentQuat, goalQuat) < 0)
                        //    goalQuat = -goalQuat;

                        // Smoothly interpolate
                        currentQuat = Quaternion.Slerp(currentQuat, goalQuat, t);

                        // Normalize to prevent floating point drift
                        currentQuat.Normalize();

                        // Convert back to matrix
                        targetHologramViewRotationCurrent = MatrixD.CreateFromQuaternion(currentQuat);

                        // Final hologram rotation
                        targetHologramFinalRotation = (targetHologramViewRotationCurrent * flipX) * rotationOnlyLocalGridMatrix;
                        break;

                    case HologramViewType.Orbit:

                        //// ORBIT CAM
                        targetHologramFinalRotation = flipX * rotationOnlyTargetGridMatrix;

                        break;


                    case HologramViewType.Perspective:

                        // This is nearly perfect, degenerates at directly up or below and has some odd behaviour when grid is to the left/right of target AND up or down but I'll live with it. 
                        Vector3D targetPos = targetGrid.WorldVolume.Center;
                        Vector3D targetBack = targetGrid.WorldMatrix.Backward; // directly behind
                        Vector3D idealObserver = targetPos + targetBack;

                        Vector3D up = targetGrid.WorldMatrix.Up;
                        Vector3D right = targetGrid.WorldMatrix.Right;

                        Vector3D dirActual = localGrid.WorldMatrix.Translation - targetPos;

                        // Detect front vs back using dot with target.Forward
                        double dot = Vector3D.Dot(targetGrid.WorldMatrix.Forward, dirActual);

                        Vector3D localGridPosition = localGrid.WorldMatrix.Translation;
                        if (dot > 0)
                        {
                            // If localGrid is behind, mirror across plane perpendicular to target up
                            Vector3D relativePerspective = localGridPosition - targetPos;
                            relativePerspective = relativePerspective - 2 * Vector3D.Dot(relativePerspective, up) * up;
                            localGridPosition = targetPos + relativePerspective;
                        }

                        // Compute a stable forward vector from target to observer
                        Vector3D forward = Vector3D.Normalize(localGridPosition - targetPos);

                        // Compute a stable right vector in the target’s horizontal plane
                        Vector3D stableRight = Vector3D.Cross(up, forward);
                        if (stableRight.LengthSquared() < 1e-6)
                        {
                            // Degenerate case: observer nearly along up axis
                            stableRight = right; // fallback to target’s right
                        }
                        stableRight.Normalize();

                        // Recompute up from stable right and forward to ensure consistent roll
                        Vector3D stableUp = Vector3D.Cross(forward, stableRight);

                        // Construct look matrices
                        MatrixD lookIdeal = MatrixD.CreateWorld(targetPos, idealObserver - targetPos, up);
                        MatrixD lookActual = MatrixD.CreateWorld(targetPos, forward, stableUp);

                        // Compute offset
                        MatrixD offset = lookActual * MatrixD.Invert(lookIdeal);

                        // Final hologram rotation
                        targetHologramFinalRotation = (offset * flipX) * rotationOnlyLocalGridMatrix;
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

       //---------------------------------------------------------------------------------

        public void UpdateFinalTargetHologramRotation() 
        {
            if (targetGrid != null && targetGridInitialized) 
            {
                // We do this at the view level now. 
                //MatrixD rotationOnlyLocalGridMatrix = localGrid.WorldMatrix;
                //rotationOnlyLocalGridMatrix.Translation = Vector3D.Zero;

                //targetHologramFinalRotation = targetHologramViewRotationCurrent * rotationOnlyLocalGridMatrix;
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
                float difPower = (jumpDriveChargeCurrent - localGridJumpDrivePowerStoredPrevious);

                if (difPower > 0)
                {
                    double powerPerSecond = difPower / deltaTimeSinceLastTick;

                    if (powerPerSecond > 0)
                    {
                        double timeRemaining = ((jumpDriveChargeMax - jumpDriveChargeCurrent) / powerPerSecond) * 100;

                        if (timeRemaining < minTime)
                        {
                            minTime = timeRemaining;
                        }
                    }
                }
            }
            localGridJumpDrivePowerStoredPrevious = localGridJumpDrivePowerStored;
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

        private bool IsProjectedGrid(IMyCubeGrid grid)
        {
            // Projected grids should have no physics and also have a projector reference
            MyCubeGrid cubeGrid = grid as Sandbox.Game.Entities.MyCubeGrid;
            return (grid.Physics == null && (cubeGrid?.Projector != null));
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
                if (IsProjectedGrid(grid))
                {
                    return null; // Skip projected grids!
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
                    if (IsProjectedGrid(grid)) 
                    {
                        continue; // Skip projected grids!
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


        int GetClusterSize(int blockCount, int targetClusters = 10000)
        {
            if (blockCount <= targetClusters) 
            {
                return 1; // Don't cluster
            }

            int size = 1;

            // Step up until our cluster size limits the number of block "clusters" drawn on screen to be under our limit. 
            while (blockCount / Math.Pow(size, 3) > targetClusters)
            {
                size++;
            }

            return size;
        }

        public void ClusterBlocks(Dictionary<Vector3I, GridBlock> allBlocksDict, ref Dictionary<Vector3I, BlockCluster> blockClusters,  ref Dictionary<Vector3I, Vector3I> blockToClusterMap, int clusterSize)
        {
            if (allBlocksDict == null || allBlocksDict.Count == 0)
            {
                return;
            }

            blockClusters = new Dictionary<Vector3I, BlockCluster>(); // CLear it

            foreach (KeyValuePair<Vector3I, GridBlock> blockKeyValuePair in allBlocksDict)
            {
                Vector3I blockKey = blockKeyValuePair.Key; // Grid block position of this block
                Vector3D blockPos = blockKeyValuePair.Value.DrawPosition; // World position of this block in grid reference
                IMySlimBlock block = blockKeyValuePair.Value.Block; // The block itself

                // Compute which cluster this block belongs to
                Vector3I clusterKey = new Vector3I(
                    (blockKey.X / clusterSize) * clusterSize,
                    (blockKey.Y / clusterSize) * clusterSize,
                    (blockKey.Z / clusterSize) * clusterSize
                );

                // Compute the draw position of this cluster
                Vector3D clusterPos = new Vector3D(
                    (blockPos.X / clusterSize) * clusterSize,
                    (blockPos.Y / clusterSize) * clusterSize,
                    (blockPos.Z / clusterSize) * clusterSize
                );

                if (blockClusters.ContainsKey(clusterKey))
                {
                    blockClusters[clusterKey].Integrity += block.Integrity;
                    blockClusters[clusterKey].MaxIntegrity += block.MaxIntegrity;
                    blockClusters[clusterKey].DrawPosition = clusterPos;
                    blockToClusterMap[blockKey] = clusterKey; // Map this block to its cluster position
                }
                else
                {
                    blockClusters[clusterKey] = new BlockCluster
                    {
                        Integrity = block.Integrity,
                        MaxIntegrity = block.MaxIntegrity,
                        DrawPosition = clusterPos
                    };
                    blockToClusterMap[blockKey] = clusterKey; // Map this block to its cluster position
                }
            }
        }





        public void ClusterBlocksIterativeThreshold(Dictionary<Vector3I, GridBlock> allBlocksDict, ref Dictionary<Vector3I, BlockCluster> blockClusters, ref Dictionary<Vector3I, Vector3I> blockToClusterMap,
            int maxClusterSize, int minClusterSize = 1, double splitThreshold = 0.9)
        {
            if (allBlocksDict == null || allBlocksDict.Count == 0)
            {
                return;
            }

            blockClusters = new Dictionary<Vector3I, BlockCluster>();
            blockToClusterMap = new Dictionary<Vector3I, Vector3I>();

            Vector3D center = GetGridCenter(allBlocksDict.Keys);

            // Precompute clusterKeys for each cluster size
            Dictionary<int, Dictionary<Vector3I, Vector3I>> clusterKeysPerBlockSize = new Dictionary<int, Dictionary<Vector3I, Vector3I>>();
            for (int size = minClusterSize; size <= maxClusterSize; size++)
            {
                Dictionary<Vector3I, Vector3I> dict = new Dictionary<Vector3I, Vector3I>();
                foreach (Vector3I position in allBlocksDict.Keys)
                {
                    dict[position] = new Vector3I(
                        (position.X / size) * size,
                        (position.Y / size) * size,
                        (position.Z / size) * size
                    );
                }
                clusterKeysPerBlockSize[size] = dict;
            }

            // Stack to iterate through
            Stack<ClusterWorkItem> clusterStack = new Stack<ClusterWorkItem>(); // Using a class because we can't use Tuples in C#6, sad because I love tuples. 
            clusterStack.Push(new ClusterWorkItem { ClusterSize = maxClusterSize, Positions = allBlocksDict.Keys.ToList() });

            // Track blocks we have already used so we don't overlap anything
            HashSet<Vector3I> used = new HashSet<Vector3I>();

            while (clusterStack.Count > 0)
            {
                ClusterWorkItem work = clusterStack.Pop();
                int clusterSize = work.ClusterSize;
                List<Vector3I> positions = work.Positions;

                if (clusterSize < minClusterSize || positions.Count == 0)
                { 
                    continue;
                }

                // Order positions by distance to center
                IEnumerable<Vector3I> positionsByCenter = positions.OrderBy(p => Vector3D.DistanceSquared(p, center));

                Dictionary<Vector3I, List<Vector3I>> clusters = new Dictionary<Vector3I, List<Vector3I>>();
                Dictionary<Vector3I, Vector3I> clusterKeys = clusterKeysPerBlockSize[clusterSize];

                foreach (Vector3I position in positionsByCenter)
                {
                    if (used.Contains(position))
                    { 
                        continue; 
                    }

                    Vector3I clusterKey = clusterKeys[position];
                    if (!clusters.ContainsKey(clusterKey))
                    {
                        clusters[clusterKey] = new List<Vector3I>();
                    }
                    clusters[clusterKey].Add(position);
                }

                foreach (KeyValuePair<Vector3I, List<Vector3I>> clusterKeyValuePair in clusters)
                {
                    Vector3I clusterKey = clusterKeyValuePair.Key;
                    List<Vector3I> clusterBlockPositions = clusterKeyValuePair.Value;

                    int maxBlocks = clusterSize * clusterSize * clusterSize;
                    double fillRatio = clusterBlockPositions.Count / (double)maxBlocks;

                    if (clusterSize > minClusterSize && fillRatio < splitThreshold)
                    {
                        clusterStack.Push(new ClusterWorkItem { ClusterSize = clusterSize - 1, Positions = clusterBlockPositions });
                    }
                    else
                    {
                        BlockCluster cluster = new BlockCluster
                        {
                            Blocks = new Dictionary<Vector3I, GridBlock>(),
                            ClusterSize = clusterSize,
                            Integrity = 0,
                            MaxIntegrity = 0
                        };

                        double sumX = 0, sumY = 0, sumZ = 0;

                        foreach (Vector3I position in clusterBlockPositions)
                        {
                            GridBlock gridBlock = allBlocksDict[position];
                            cluster.Blocks[position] = gridBlock;
                            cluster.Integrity += gridBlock.Block.Integrity;
                            cluster.MaxIntegrity += gridBlock.Block.MaxIntegrity;

                            sumX += gridBlock.DrawPosition.X;
                            sumY += gridBlock.DrawPosition.Y;
                            sumZ += gridBlock.DrawPosition.Z;

                            used.Add(position);
                            blockToClusterMap[position] = clusterKey;
                        }

                        int count = cluster.Blocks.Count;
                        cluster.DrawPosition = new Vector3D(sumX / count, sumY / count, sumZ / count);

                        blockClusters[clusterKey] = cluster;
                    }
                }
            }
        }

        // Keeps the state of an incremental build
        private class ClusterBuildState
        {
            public Stack<ClusterWorkItem> ClusterStack;
            public HashSet<Vector3I> Used;
            public Dictionary<int, Dictionary<Vector3I, Vector3I>> ClusterKeysPerBlockSize;
            public Vector3D Center;
            public bool IsComplete;
        }

        // Updated ClusterWorkItem with persistent cluster list & indexes
        private class ClusterWorkItem
        {
            public int ClusterSize;
            public List<Vector3I> Positions;

            // Grouping progress 
            public int PositionIndex = 0;

            // Once grouping is done we store the list of grouped clusters here
            // Each entry: (clusterKey, list-of-positions-in-that-cluster)
            public List<KeyValuePair<Vector3I, List<Vector3I>>> ClusterList = null;

            // Where we are in ClusterList while consuming/creating BlockClusters
            public int ClusterListIndex = 0;
        }

        public void StartClusterBlocksIterativeThreshold(Dictionary<Vector3I, GridBlock> allBlocksDict, int maxClusterSize, int minClusterSize = 1)
        {
            if (allBlocksDict == null || allBlocksDict.Count == 0)
            {
                localGridClusterBuildState = null;
                return;
            }

            localGridClusterBuildState = new ClusterBuildState
            {
                ClusterStack = new Stack<ClusterWorkItem>(),
                Used = new HashSet<Vector3I>(),
                ClusterKeysPerBlockSize = new Dictionary<int, Dictionary<Vector3I, Vector3I>>(),
                Center = GetGridCenter(allBlocksDict.Keys),
                IsComplete = false
            };

            // Precompute clusterKeys for each cluster size
            for (int size = minClusterSize; size <= maxClusterSize; size++)
            {
                Dictionary<Vector3I, Vector3I> dict = new Dictionary<Vector3I, Vector3I>();
                foreach (Vector3I position in allBlocksDict.Keys)
                {
                    dict[position] = new Vector3I(
                        (position.X / size) * size,
                        (position.Y / size) * size,
                        (position.Z / size) * size
                    );
                }
                localGridClusterBuildState.ClusterKeysPerBlockSize[size] = dict;
            }

            // Seed the stack with one top-level work item
            localGridClusterBuildState.ClusterStack.Push(
                new ClusterWorkItem
                {
                    ClusterSize = maxClusterSize,
                    Positions = allBlocksDict.Keys.ToList(),
                    PositionIndex = 0,
                    ClusterList = null,
                    ClusterListIndex = 0
                }
            );
        }

        // Call once per tick until it returns true (done)
        public bool ContinueClusterBlocksIterativeThreshold(Dictionary<Vector3I, GridBlock> allBlocksDict, ref Dictionary<Vector3I, BlockCluster> blockClusters, ref Dictionary<Vector3I, Vector3I> blockToClusterMap,
            int clustersPerTick, int minClusterSize = 1, double splitThreshold = 0.9)
        {
            if (localGridClusterBuildState == null || localGridClusterBuildState.IsComplete)
            {
                return true;  // already done
            }

            if (blockClusters == null) 
            { 
                blockClusters = new Dictionary<Vector3I, BlockCluster>(); 
            }
            if (blockToClusterMap == null) 
            { 
                blockToClusterMap = new Dictionary<Vector3I, Vector3I>(); 
            }

            int processedThisTick = 0;

            // Process work items until budget exhausted or stack empty
            while (localGridClusterBuildState.ClusterStack.Count > 0 && processedThisTick < clustersPerTick)
            {
                ClusterWorkItem work = localGridClusterBuildState.ClusterStack.Pop();
                int clusterSize = work.ClusterSize;

                if (clusterSize < minClusterSize || work.Positions == null || work.Positions.Count == 0)
                { 
                    continue; 
                }

                // If we haven't grouped positions into cluster buckets yet, do it once now and store it.
                if (work.ClusterList == null)
                {
                    // Order positions by distance to center to preserve deterministic ordering
                    work.Positions = work.Positions
                        .OrderBy(p => Vector3D.DistanceSquared(p, localGridClusterBuildState.Center))
                        .ToList();

                    Dictionary<Vector3I, Vector3I> clusterKeys = localGridClusterBuildState.ClusterKeysPerBlockSize[clusterSize];
                    Dictionary<Vector3I, List<Vector3I>> clustersDict = new Dictionary<Vector3I, List<Vector3I>>();

                    // Group positions into clusterKey -> list
                    foreach (Vector3I pos in work.Positions)
                    {
                        if (localGridClusterBuildState.Used.Contains(pos))
                        { 
                            continue;
                        }

                        Vector3I clusterKey = clusterKeys[pos];
                        List<Vector3I> list;
                        if (!clustersDict.TryGetValue(clusterKey, out list))
                        {
                            list = new List<Vector3I>();
                            clustersDict[clusterKey] = list;
                        }
                        list.Add(pos);
                    }

                    // Create a stable list of clusters for resumeable consumption
                    work.ClusterList = new List<KeyValuePair<Vector3I, List<Vector3I>>>(clustersDict.Count);
                    foreach (KeyValuePair<Vector3I, List<Vector3I>> kv in clustersDict)
                    { 
                        work.ClusterList.Add(new KeyValuePair<Vector3I, List<Vector3I>>(kv.Key, kv.Value)); 
                    }

                    work.ClusterListIndex = 0;
                    // Positions grouping complete for this work item
                }

                // Consume clusters from the stored ClusterList, resuming at ClusterListIndex
                List<KeyValuePair<Vector3I, List<Vector3I>>> clist = work.ClusterList;
                for (int ci = work.ClusterListIndex; ci < clist.Count; ci++)
                {
                    KeyValuePair<Vector3I, List<Vector3I>> kvp = clist[ci];
                    Vector3I clusterKey = kvp.Key;
                    List<Vector3I> clusterBlockPositions = kvp.Value;

                    int maxBlocks = clusterSize * clusterSize * clusterSize;
                    double fillRatio = clusterBlockPositions.Count / (double)maxBlocks;

                    if (clusterSize > minClusterSize && fillRatio < splitThreshold)
                    {
                        // Push a smaller work item to split this cluster further
                        localGridClusterBuildState.ClusterStack.Push(new ClusterWorkItem
                        {
                            ClusterSize = clusterSize - 1,
                            Positions = clusterBlockPositions,
                            PositionIndex = 0,
                            ClusterList = null,
                            ClusterListIndex = 0
                        });
                    }
                    else
                    {
                        // Create the BlockCluster
                        BlockCluster cluster = new BlockCluster
                        {
                            Blocks = new Dictionary<Vector3I, GridBlock>(),
                            ClusterSize = clusterSize,
                            Integrity = 0,
                            MaxIntegrity = 0
                        };

                        double sumX = 0, sumY = 0, sumZ = 0;

                        foreach (Vector3I pos in clusterBlockPositions)
                        {
                            GridBlock gridBlock = allBlocksDict[pos];
                            cluster.Blocks[pos] = gridBlock;
                            cluster.Integrity += gridBlock.Block.Integrity;
                            cluster.MaxIntegrity += gridBlock.Block.MaxIntegrity;

                            sumX += gridBlock.DrawPosition.X;
                            sumY += gridBlock.DrawPosition.Y;
                            sumZ += gridBlock.DrawPosition.Z;

                            localGridClusterBuildState.Used.Add(pos);
                            blockToClusterMap[pos] = clusterKey;
                        }

                        int count = cluster.Blocks.Count;
                        if (count > 0) 
                        {
                            cluster.DrawPosition = new Vector3D(sumX / count, sumY / count, sumZ / count);
                        }
                        blockClusters[clusterKey] = cluster;
                    }

                    processedThisTick++;

                    // hit cluster per tick budget? Save progress and return
                    if (processedThisTick >= clustersPerTick)
                    {
                        // Save index to resume next tick
                        work.ClusterListIndex = ci + 1; // next cluster to process
                        localGridClusterBuildState.ClusterStack.Push(work);
                        return false;
                    }
                }

                // Finished consuming this work's clusters, continue to next work item
            }

            // Stack drained means we are done
            if (localGridClusterBuildState.ClusterStack.Count == 0)
            {
                localGridClusterBuildState.IsComplete = true;
                return true;
            }

            return false;
        }



        private Vector3D GetGridCenter(IEnumerable<Vector3I> positions)
        {
            int minX = positions.Min(p => p.X);
            int minY = positions.Min(p => p.Y);
            int minZ = positions.Min(p => p.Z);

            int maxX = positions.Max(p => p.X);
            int maxY = positions.Max(p => p.Y);
            int maxZ = positions.Max(p => p.Z);

            return new Vector3D(
                (minX + maxX) / 2.0,
                (minY + maxY) / 2.0,
                (minZ + maxZ) / 2.0
            );
        }


        public void RemoveBlockFromCluster(Vector3I position, ref Dictionary<Vector3I, BlockCluster> blockClusters, ref Dictionary<Vector3I, Vector3I> blockToClusterMap)
        {
            Vector3I clusterKey;
            if (!blockToClusterMap.TryGetValue(position, out clusterKey)) 
            {
                return;
            }

            BlockCluster cluster;
            if (!blockClusters.TryGetValue(clusterKey, out cluster))
            { 
                return; 
            }

            if (!cluster.Blocks.ContainsKey(position))
            {
                return;
            }

            GridBlock block = cluster.Blocks[position];
            cluster.Blocks.Remove(position);

            cluster.Integrity -= block.Block.Integrity;
            cluster.MaxIntegrity -= block.Block.MaxIntegrity;

            blockToClusterMap.Remove(position);

            if (cluster.Blocks.Count == 0)
            {
                // Cluster is empty, remove entirely
                blockClusters.Remove(clusterKey);
            }
            else
            {
                // Recompute draw position as average of remaining blocks
                double sumX = 0, sumY = 0, sumZ = 0;
                foreach (GridBlock gb in cluster.Blocks.Values)
                {
                    sumX += gb.DrawPosition.X;
                    sumY += gb.DrawPosition.Y;
                    sumZ += gb.DrawPosition.Z;
                }
                int count = cluster.Blocks.Count;
                cluster.DrawPosition = new Vector3D(sumX / count, sumY / count, sumZ / count);
            }
        }



        private BlockClusterType GetClusterType(IMySlimBlock block)
        {
            IMyCubeBlock fat = block.FatBlock;
            if (fat == null) 
            { 
                return BlockClusterType.Structure; 
            }

            string subtype = fat.BlockDefinition.SubtypeName.ToLowerInvariant();

            if (fat is IMyLargeTurretBase) return BlockClusterType.Turret;
            if (fat is IMyUserControllableGun) return BlockClusterType.FixedWeapon;

            if (subtype.Contains("missile") || subtype.Contains("torpedo"))
                return BlockClusterType.Missile;

            if (fat is IMyReactor) return BlockClusterType.Reactor;
            if (fat is IMyBatteryBlock) return BlockClusterType.Battery;
            if (fat is IMyRadioAntenna) return BlockClusterType.Antenna;

            if (fat is IMyThrust)
            {
                if (subtype.Contains("ion")) return BlockClusterType.IonThruster;
                if (subtype.Contains("hydrogen")) return BlockClusterType.HydrogenThruster;
                if (subtype.Contains("atmospheric")) return BlockClusterType.AtmosphericThruster;
            }

            if (fat is IMyGasTank)
            {
                if (subtype.Contains("hydrogen")) return BlockClusterType.HydrogenTank;
                if (subtype.Contains("oxygen")) return BlockClusterType.OxygenTank;
            }

            if (fat is IMyJumpDrive) return BlockClusterType.JumpDrive;

            return BlockClusterType.Structure;
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
            theData.radarScannerScale = ini.Get(mySection, "ScannerS").ToSingle(1);
            ini.Set(mySection, "ScannerS", theData.radarScannerScale.ToString());
            theData.radarRadius = 0.125f * theData.radarScannerScale;

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

