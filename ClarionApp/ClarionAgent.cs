using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Clarion;
using Clarion.Framework;
using Clarion.Framework.Core;
using Clarion.Framework.Templates;
using ClarionApp.Model;
using ClarionApp;
using System.Threading;
using Gtk;

namespace ClarionApp
{


    /// <summary>
    /// Public enum that represents all possibilities of agent actions
    /// </summary>
    public enum CreatureActions
    {
        DO_NOTHING,
        ROTATE_CLOCKWISE,
        GO_AHEAD
    }

    public class ClarionAgent
    {
        public Leaflet bestLeaflet = null;
        
        #region Constants
        /// <summary>
        /// Constant that represents the Visual Sensor
        /// </summary>
        private String SENSOR_VISUAL_DIMENSION = "VisualSensor";
        /// <summary>
        /// Constant that represents that there is at least one wall ahead
        /// </summary>
        private String DIMENSION_WALL_AHEAD = "WallAhead";
		double prad = 0;
        #endregion

        #region Properties
		public MindViewer mind;
		String creatureId = String.Empty;
		String creatureName = String.Empty;
        #region Simulation
        /// <summary>
        /// If this value is greater than zero, the agent will have a finite number of cognitive cycle. Otherwise, it will have infinite cycles.
        /// </summary>
        public double MaxNumberOfCognitiveCycles = -1;
        /// <summary>
        /// Current cognitive cycle number
        /// </summary>
        private double CurrentCognitiveCycle = 0;
        /// <summary>
        /// Time between cognitive cycle in miliseconds
        /// </summary>
        public Int32 TimeBetweenCognitiveCycles = 0;
        /// <summary>
        /// A thread Class that will handle the simulation process
        /// </summary>
        private Thread runThread;
        #endregion

        #region Agent
		private WSProxy worldServer;
        /// <summary>
        /// The agent 
        /// </summary>
        private Clarion.Framework.Agent CurrentAgent;
        #endregion

        #region Perception Input
        /// <summary>
        /// Perception input to indicates a wall ahead
        /// </summary>
		private DimensionValuePair inputWallAhead;

        private DimensionValuePair inputNextJewelTheta;
        private DimensionValuePair inputNextJewelRay;
        private DimensionValuePair inputNextFoodTheta;
        private DimensionValuePair inputNextFoodRay;
        private DimensionValuePair inputFuel;
        #endregion

        #region Action Output
        /// <summary>
        /// Output action that makes the agent to rotate clockwise
        /// </summary>
		private ExternalActionChunk outputRotateClockwise;
        /// <summary>
        /// Output action that makes the agent go ahead
        /// </summary>
		private ExternalActionChunk outputGoAhead;

        private ExternalActionChunk outputDirectionWalk;
        private ExternalActionChunk outputInteract;
        #endregion

        #endregion

        #region Constructor
		public ClarionAgent(WSProxy nws, String creature_ID, String creature_Name)
        {
			worldServer = nws;
			// Initialize the agent
            CurrentAgent = World.NewAgent("Current Agent");
			mind = new MindViewer();
			mind.Show ();
			creatureId = creature_ID;
			creatureName = creature_Name;

            // Initialize Input Information
            inputWallAhead = World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, DIMENSION_WALL_AHEAD);
            inputNextJewelTheta = World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, "NextJewelTheta");
            inputNextJewelRay = World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, "NextJewelRay");
            inputNextFoodTheta = World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, "NextFoodTheta");
            inputNextFoodRay = World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, "NextFoodRay");
            inputFuel = World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, "Fuel");

            // Initialize Output actions
            outputRotateClockwise = World.NewExternalActionChunk(CreatureActions.ROTATE_CLOCKWISE.ToString());
            outputGoAhead = World.NewExternalActionChunk(CreatureActions.GO_AHEAD.ToString());

            outputDirectionWalk = World.NewExternalActionChunk("DirectionWalk");
            outputInteract = World.NewExternalActionChunk("Interact");

            SimplifiedQBPNetwork net = AgentInitializer.InitializeImplicitDecisionNetwork(CurrentAgent, SimplifiedQBPNetwork.Factory);
            net.Input.Add(inputNextJewelTheta);
            net.Input.Add(inputNextJewelRay);
            net.Input.Add(inputNextFoodTheta);
            net.Input.Add(inputNextFoodRay);
            net.Input.Add(inputFuel);

            net.Output.Add(outputDirectionWalk);
            net.Output.Add(outputInteract);
            CurrentAgent.Commit(net);

            net.Parameters.LEARNING_RATE = 1;
            CurrentAgent.ACS.Parameters.PERFORM_RER_REFINEMENT = false;

            //Create thread to simulation
            runThread = new Thread(CognitiveCycle);
			Console.WriteLine("Agent started");
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Run the Simulation in World Server 3d Environment
        /// </summary>
        public void Run()
        {                
			Console.WriteLine ("Running ...");
            // Setup Agent to run
            if (runThread != null && !runThread.IsAlive)
            {
                SetupAgentInfraStructure();
				// Start Simulation Thread                
                runThread.Start(null);
            }
        }

        /// <summary>
        /// Abort the current Simulation
        /// </summary>
        /// <param name="deleteAgent">If true beyond abort the current simulation it will die the agent.</param>
        public void Abort(Boolean deleteAgent)
        {   Console.WriteLine ("Aborting ...");
            if (runThread != null && runThread.IsAlive)
            {
                runThread.Abort();
            }

            if (CurrentAgent != null && deleteAgent)
            {
                CurrentAgent.Die();
            }
        }

		IList<Thing> processSensoryInformation()
		{
			IList<Thing> response = null;

			if (worldServer != null && worldServer.IsConnected)
			{
				response = worldServer.SendGetCreatureState(creatureName);
				prad = (Math.PI / 180) * response.First().Pitch;
				while (prad > Math.PI) prad -= 2 * Math.PI;
				while (prad < - Math.PI) prad += 2 * Math.PI;
				Sack s = worldServer.SendGetSack("0");
				mind.setBag(s);
			}

			return response;
		}

		void processSelectedAction(CreatureActions externalAction)
		{   Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
			if (worldServer != null && worldServer.IsConnected)
			{
				switch (externalAction)
				{
				case CreatureActions.DO_NOTHING:
					// Do nothing as the own value says
					break;
				case CreatureActions.ROTATE_CLOCKWISE:
					worldServer.SendSetAngle(creatureId, 2, -2, 2);
                    // System.Console.WriteLine("Rotating Clockwise ...\n");
                    // List<Thing> thingsInVision = worldServer.GetWorldEntities();
                    // System.Console.WriteLine("Things in vision count: " + thingsInVision.Count + "\n");
                    // foreach (Thing t in thingsInVision)
                    // {
                    //     if (t.CategoryId == Thing.CATEGORY_JEWEL)
                    //     {
                    //         System.Console.WriteLine("Thing in vision: " + t.Name + " - Category: " + t.CategoryId + " X: " + t.X1 + " Y: " + t.Y1 + "\n");
                    //     }
                    // }
					break;
				case CreatureActions.GO_AHEAD:
					worldServer.SendSetAngle(creatureId, 1, 1, prad);
					break;
				default:
					break;
				}
			}
		}

        public double GetNextJewelTheta()
        {
            // System.Console.WriteLine("GetNextJewelTheta called ...\n");
            return 1.0;
        }
        public double GetNextJewelRay() 
        { 
            // System.Console.WriteLine("GetNextJewelRay called ...\n");
            return 1.0; 
        }
        public double GetNextFoodTheta() 
        { 
            // System.Console.WriteLine("GetNextFoodTheta called ...\n");
            return 1.0; 
        }
        public double GetNextFoodRay() 
        { 
            // System.Console.WriteLine("GetNextFoodRay called ...\n");
            return 1.0; 
        }
        public double GetFuel() 
        { 
            // System.Console.WriteLine("GetFuel called ...\n");
            return 1.0; 
        }

        public void SetDirectionWalk(float value) 
        { 
            // System.Console.WriteLine("SetDirectionWalk called with value: " + value + "\n"); 
        }
        public void SetInteract(float value) 
        { 
            // System.Console.WriteLine("SetInteract called with value: " + value + "\n"); 
        }

        #endregion

        #region Setup Agent Methods
        /// <summary>
        /// Setup agent infra structure (ACS, NACS, MS and MCS)
        /// </summary>
        private void SetupAgentInfraStructure()
        {
            // Setup the ACS Subsystem
            SetupACS();
        }

        private void SetupMS()
        {            
            //RichDrive
        }

        /// <summary>
        /// Setup the ACS subsystem
        /// </summary>
        private void SetupACS() // EXPLAIN: executado uma vez quando o agente é criado
        {
            // Create Rule to avoid collision with wall
            SupportCalculator avoidCollisionWallSupportCalculator = FixedRuleToAvoidCollisionWall;
            FixedRule ruleAvoidCollisionWall = AgentInitializer.InitializeActionRule(CurrentAgent, FixedRule.Factory, outputRotateClockwise, avoidCollisionWallSupportCalculator);

            // Commit this rule to Agent (in the ACS)
            CurrentAgent.Commit(ruleAvoidCollisionWall);

            // Create Colission To Go Ahead
            SupportCalculator goAheadSupportCalculator = FixedRuleToGoAhead;
            FixedRule ruleGoAhead = AgentInitializer.InitializeActionRule(CurrentAgent, FixedRule.Factory, outputGoAhead, goAheadSupportCalculator);
            
            // Commit this rule to Agent (in the ACS)
            CurrentAgent.Commit(ruleGoAhead);

            // Disable Rule Refinement
            CurrentAgent.ACS.Parameters.PERFORM_RER_REFINEMENT = false;

            // The selection type will be probabilistic
            CurrentAgent.ACS.Parameters.LEVEL_SELECTION_METHOD = ActionCenteredSubsystem.LevelSelectionMethods.STOCHASTIC;

            // The action selection will be fixed (not variable) i.e. only the statement defined above.
            CurrentAgent.ACS.Parameters.LEVEL_SELECTION_OPTION = ActionCenteredSubsystem.LevelSelectionOptions.FIXED;

            // Define Probabilistic values
            CurrentAgent.ACS.Parameters.FIXED_FR_LEVEL_SELECTION_MEASURE = 1;
            CurrentAgent.ACS.Parameters.FIXED_IRL_LEVEL_SELECTION_MEASURE = 0;
            CurrentAgent.ACS.Parameters.FIXED_BL_LEVEL_SELECTION_MEASURE = 0;
            CurrentAgent.ACS.Parameters.FIXED_RER_LEVEL_SELECTION_MEASURE = 0;
        }

        /// <summary>
        /// Make the agent perception. In other words, translate the information that came from sensors to a new type that the agent can understand
        /// </summary>
        /// <param name="sensorialInformation">The information that came from server</param>
        /// <returns>The perceived information</returns>
		private SensoryInformation prepareSensoryInformation(IList<Thing> listOfThings)
        {
            // New sensory information
            SensoryInformation si = World.NewSensoryInformation(CurrentAgent);

            // Detect if we have a wall ahead
            Boolean wallAhead = listOfThings.Where(item => (item.CategoryId == Thing.CATEGORY_BRICK && item.DistanceToCreature <= 61)).Any();
            double wallAheadActivationValue = wallAhead ? CurrentAgent.Parameters.MAX_ACTIVATION : CurrentAgent.Parameters.MIN_ACTIVATION;
            si.Add(inputWallAhead, wallAheadActivationValue);

            si.Add(inputNextJewelTheta, GetNextJewelTheta());
            si.Add(inputNextJewelRay, GetNextJewelRay());
            si.Add(inputNextFoodTheta, GetNextFoodTheta());
            si.Add(inputNextFoodRay, GetNextFoodRay());
            si.Add(inputFuel, GetFuel());


			//Console.WriteLine(sensorialInformation);
			Creature c = (Creature) listOfThings.Where(item => (item.CategoryId == Thing.CATEGORY_CREATURE)).First();
			int n = 0;
            int bestPagamento = 0;
			foreach(Leaflet l in c.getLeaflets()) {
                System.Console.WriteLine("Leaflet ID: " + l.leafletID + " payment: " + l.payment + " Tipo: " + l.items[0].itemKey + "Quantity: " +l.items[0].totalNumber + "\n");
                if (l.payment > bestPagamento) {
                    bestPagamento = l.payment;
                    bestLeaflet = l;
                }
				mind.updateLeaflet(n,l);
				n++;
			}


            double menorDistanciaPorCor = double.MaxValue;
            if (bestLeaflet != null)
            {
                
                foreach(LeafletItem item in bestLeaflet.items)
                {

                    System.Console.WriteLine("Item: " + item.itemKey + " | Required: " + item.totalNumber + " | Collected: " + item.collected + "\n");
                    if(item.totalNumber - item.collected <= 0) continue;
                    
                    IEnumerable<Thing> joiasDaCor = listOfThings.Where(t =>
                        t.CategoryId == Thing.CATEGORY_JEWEL &&
                        !String.IsNullOrEmpty(t.Material.Color) &&
                        t.Material.Color.IndexOf(item.itemKey, StringComparison.OrdinalIgnoreCase) >= 0);


                    foreach (Thing joia in joiasDaCor)
                    {
                        if (joia.DistanceToCreature < menorDistanciaPorCor)
                        {
                            menorDistanciaPorCor = joia.DistanceToCreature;
                        }
                        System.Console.WriteLine("Joia da cor " + item.itemKey + " encontrada a distância: " + joia.DistanceToCreature);
                    }

                    System.Console.WriteLine("Cor: " + item.itemKey + " | Menor distância encontrada: " + menorDistanciaPorCor);
                }

            }
            return si;
        }
        #endregion

        #region Fixed Rules
        private double FixedRuleToAvoidCollisionWall(ActivationCollection currentInput, Rule target)
        {
            // See partial match threshold to verify what are the rules available for action selection
            return ((currentInput.Contains(inputWallAhead, CurrentAgent.Parameters.MAX_ACTIVATION))) ? 1.0 : 0.0;
        }

        private double FixedRuleToGoAhead(ActivationCollection currentInput, Rule target)
        {
            // Here we will make the logic to go ahead
            return ((currentInput.Contains(inputWallAhead, CurrentAgent.Parameters.MIN_ACTIVATION))) ? 1.0 : 0.0;
        }
        #endregion

        #region Run Thread Method
        private void CognitiveCycle(object obj)
        {

			Console.WriteLine("Starting Cognitive Cycle ... press CTRL-C to finish !");
            // Cognitive Cycle starts here getting sensorial information
            while (CurrentCognitiveCycle != MaxNumberOfCognitiveCycles)
            {
                // Get current sensory information                    
                try
                {
				    IList<Thing> currentSceneInWS3D = processSensoryInformation();
                    // Make the perception
                    SensoryInformation si = prepareSensoryInformation(currentSceneInWS3D);

                    //Perceive the sensory information
                    CurrentAgent.Perceive(si);

                    //Choose an action
                    ExternalActionChunk chosen = CurrentAgent.GetChosenExternalAction(si);

                    System.Console.WriteLine("Best leaflet payment: " + bestLeaflet.payment + "\n");

                    // Get the selected action
                    String actionLabel = chosen.LabelAsIComparable.ToString();
                    CreatureActions actionType = (CreatureActions)Enum.Parse(typeof(CreatureActions), actionLabel, true);

                    // Call the output event handler
                    processSelectedAction(actionType);

                    // CurrentAgent.NACS does not expose GetOutput; use default values or implement the correct retrieval method here.
                    float directionWalk = 0f;
                    float interact = 0f;

                    SetDirectionWalk(directionWalk);
                    SetInteract(interact);

                    // Increment the number of cognitive cycles
                    CurrentCognitiveCycle++;

                    //Wait to the agent accomplish his job
                    if (TimeBetweenCognitiveCycles > 0)
                    {
                        Thread.Sleep(TimeBetweenCognitiveCycles);
                    }
                    
                }
                catch (Exception ex)
                {
                    Console.Out.WriteLine(String.Format("[ERROR] Unknown Error: {0}\n", ex.Message));
                }

			}
        }
        #endregion

    }
}
