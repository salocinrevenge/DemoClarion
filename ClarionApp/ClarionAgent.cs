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
        GO_TO_FOOD,
        GO_TO_JEWEL,
        INTERACT,
        EXPLORE,
        GO_TO_DELIVERY_SPOT
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

        private bool Searching_food = false;
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
		private ExternalActionChunk outputGoToFood;
        /// <summary>
        /// Output action that makes the agent go ahead
        /// </summary>
		private ExternalActionChunk outputGoToJewel;
        /// <summary>
        /// Output action that makes the agent go ahead
        /// </summary>
        private ExternalActionChunk outputInteract;
        /// <summary>
        /// Output action that makes the agent go ahead
        /// </summary>
        private ExternalActionChunk outputExplore;

        private ExternalActionChunk outputGoToDeliverySpot;
        #endregion

        private SimplifiedQBPNetwork bottomLevelNetwork = null;
        private Thing deliverSpotLocation = null;
        private Thing bestJewel = null;
        private Thing bestFood = null;

        private Creature creature = null;

        private double Fuel = 0;

        // Training state
        private double lastJewelTheta = 0;
        private double lastJewelRay = 0;
        private double lastFoodTheta = 0;
        private double lastFoodRay = 0;
        private DateTime lastCollectionTime;
        private int lastSackCount = 0;
        private double lastFuel = 0;

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
            lastCollectionTime = DateTime.Now;

            // Initialize Input Information
            inputWallAhead = World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, DIMENSION_WALL_AHEAD);
            inputNextJewelTheta = World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, "NextJewelTheta");
            inputNextJewelRay = World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, "NextJewelRay");
            inputNextFoodTheta = World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, "NextFoodTheta");
            inputNextFoodRay = World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, "NextFoodRay");
            inputFuel = World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, "Fuel");

            // Initialize Output actions
            outputGoToFood = World.NewExternalActionChunk(CreatureActions.GO_TO_FOOD.ToString());
            outputGoToJewel = World.NewExternalActionChunk(CreatureActions.GO_TO_JEWEL.ToString());
            outputInteract = World.NewExternalActionChunk(CreatureActions.INTERACT.ToString());
            outputExplore = World.NewExternalActionChunk(CreatureActions.EXPLORE.ToString());
            outputGoToDeliverySpot = World.NewExternalActionChunk(CreatureActions.GO_TO_DELIVERY_SPOT.ToString());

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

		public bool IsRunning()
        {
            return runThread != null && runThread.IsAlive;
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

		void processSelectedAction(ExternalActionChunk externalAction)
		{   Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
			if (worldServer != null && worldServer.IsConnected)
			{
                CreatureActions action = (CreatureActions)Enum.Parse(typeof(CreatureActions), externalAction.LabelAsIComparable.ToString());
				switch (action)
				{
				case CreatureActions.DO_NOTHING:
					// Do nothing as the own value says
					break;
                case CreatureActions.GO_TO_FOOD:
                    if (bestFood != null)
                    {
                        Console.WriteLine("Moving towards Food: " + bestFood.Name + "\n");
                        worldServer.SendSetGoTo(creatureId, 3, 3, bestFood.X1, bestFood.Y1);
                    }
                    break;
                case CreatureActions.GO_TO_JEWEL:
                    if (bestJewel != null)
                    {
                        Console.WriteLine("Moving towards Jewel: " + bestJewel.Name + "\n");
                        worldServer.SendSetGoTo(creatureId, 3, 3, bestJewel.X1, bestJewel.Y1);
                    }
                    break;
                case CreatureActions.INTERACT:
                    interact();
                    break;
                case CreatureActions.EXPLORE:
                    Console.WriteLine("No specific action taken. Rotating to explore...\n");
                    worldServer.SendSetAngle(creatureId, 2, -2, 2);
					break;
                case CreatureActions.GO_TO_DELIVERY_SPOT:
                    if (deliverSpotLocation != null)
                    {
                        Console.WriteLine("Moving towards Delivery Spot: " + deliverSpotLocation.Name + "\n");
                        worldServer.SendSetGoTo(creatureId, 3, 3, deliverSpotLocation.X1, deliverSpotLocation.Y1);
                    }
                    else
                    {
                        Console.WriteLine("Moving towards 500 500: " + bestJewel.Name + "\n");
                        worldServer.SendSetGoTo(creatureId, 3, 3, 500, 500);
                    }
                    break;
				default:
					break;
				}
			}
		}

        public double GetNextJewelTheta()
        {
            // Compute smallest signed angle (radians) to rotate creature to face the nearest jewel
            try {

                if (creature == null || bestJewel == null) return 0.0;

                double dx = bestJewel.X1 - creature.X1;
                double dy = bestJewel.Y1 - creature.Y1;
                double targetAngle = Math.Atan2(dy, dx);


                // current orientation (prad) is maintained in processSensoryInformation
                double diff = targetAngle - prad;
                while (diff > Math.PI) diff -= 2 * Math.PI;
                while (diff <= -Math.PI) diff += 2 * Math.PI;
                return diff;
            }
            catch (Exception ex) {
                System.Console.WriteLine("Error in GetNextJewelTheta. Returning 0.0. Exception: " + ex.Message + "\n");
                return 0.0;
            }
        }
        public double GetNextJewelRay() 
        { 
            // System.Console.WriteLine("GetNextJewelRay called ...\n");
            try {

                if (creature == null || bestJewel == null) return 0.0;

                double dx = bestJewel.X1 - creature.X1;
                double dy = bestJewel.Y1 - creature.Y1;
                
                double dist = Math.Sqrt(dx * dx + dy * dy);

                System.Console.WriteLine("Target Ray Jewel: " + dist + "\n");
                return dist;
            }
            catch (Exception ex) {
                System.Console.WriteLine("Error in GetNextJewelTheta. Returning 0.0. Exception: " + ex.Message + "\n");
                return 0.0;
            }
        }
        public double GetNextFoodTheta() 
        { 
            try {

                if (creature == null || bestFood == null) return 0.0;

                double dx = bestFood.X1 - creature.X1;
                double dy = bestFood.Y1 - creature.Y1;
                double targetAngle = Math.Atan2(dy, dx);


                // current orientation (prad) is maintained in processSensoryInformation
                double diff = targetAngle - prad;
                while (diff > Math.PI) diff -= 2 * Math.PI;
                while (diff <= -Math.PI) diff += 2 * Math.PI;
                System.Console.WriteLine("Target Angle Food: " + targetAngle + " | Current Angle: " + prad + " | Diff: " + diff + "\n");
                return diff;
            }
            catch (Exception ex) {
                System.Console.WriteLine("Error in GetNextFoodTheta. Returning 0.0. Exception: " + ex.Message + "\n");
                return 0.0;
            }
        }
        public double GetNextFoodRay() 
        { 
            try {

                if (creature == null || bestFood == null) return 0.0;

                double dx = bestFood.X1 - creature.X1;
                double dy = bestFood.Y1 - creature.Y1;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                System.Console.WriteLine("Target Ray Food: " + dist + "\n");
                return dist;
            }
            catch (Exception ex) {
                System.Console.WriteLine("Error in GetNextFoodRay. Returning 0.0. Exception: " + ex.Message + "\n");
                return 0.0;
            }
        }
        public double GetFuel() 
        { 
            return Fuel; 
        }

        public void SetDirectionWalk(float value) 
        { 
            System.Console.WriteLine("SetDirectionWalk called with value: " + value + "\n"); 
        }
        public void SetInteract(float value) 
        { 
            System.Console.WriteLine("SetInteract called with value: " + value + "\n"); 
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
            
            // Rule to go to food when fuel is low
            SupportCalculator goToFoodSupportCalculator = (currentInput, target) =>
            {
                if (Searching_food && bestFood != null && bestFood.DistanceToCreature >= 50)
                {
                    return 1.0;
                }
                return 0.0;
            };
            FixedRule ruleGoToFood = AgentInitializer.InitializeActionRule(CurrentAgent, FixedRule.Factory, outputGoToFood, goToFoodSupportCalculator);
            CurrentAgent.Commit(ruleGoToFood);

            // Rule to go to jewel when fuel is not low
            SupportCalculator goToJewelSupportCalculator = (currentInput, target) =>
            {
                if (!Searching_food && bestJewel != null && bestJewel.DistanceToCreature >= 50)
                {
                    return 1.0;
                }
                return 0.0;
            };
            FixedRule ruleGoToJewel = AgentInitializer.InitializeActionRule(CurrentAgent, FixedRule.Factory, outputGoToJewel, goToJewelSupportCalculator);
            CurrentAgent.Commit(ruleGoToJewel);

            // Rule to interact when close to an object
            SupportCalculator interactSupportCalculator = (currentInput, target) =>
            {
                bool canInteractWithFood = Searching_food && bestFood != null && bestFood.DistanceToCreature < 50;
                bool canInteractWithJewel = !Searching_food && bestJewel != null && bestJewel.DistanceToCreature < 50;
                if (canInteractWithFood || canInteractWithJewel)
                {
                    return 1.0;
                }
                return 0.0;
            };
            FixedRule ruleInteract = AgentInitializer.InitializeActionRule(CurrentAgent, FixedRule.Factory, outputInteract, interactSupportCalculator);
            CurrentAgent.Commit(ruleInteract);

            // Rule to explore when no other action is taken
            SupportCalculator exploreSupportCalculator = (currentInput, target) =>
            {
                // This rule has a lower priority and will be chosen if others don't apply.
                // We can return a small constant value to make it a default action.
                return 0.1;
            };
            FixedRule ruleExplore = AgentInitializer.InitializeActionRule(CurrentAgent, FixedRule.Factory, outputExplore, exploreSupportCalculator);
            CurrentAgent.Commit(ruleExplore);

            // Rule to go to delivery spot
            SupportCalculator goToDeliverySpotSupportCalculator = (currentInput, target) =>
            {
                int n_joias_faltantes = 0;
                if (bestLeaflet != null)
                {
                    foreach (LeafletItem li in bestLeaflet.items)
                    {
                        n_joias_faltantes += (li.totalNumber - li.collected);
                    }
                }

                if (!Searching_food && n_joias_faltantes == 0 && deliverSpotLocation != null && deliverSpotLocation.DistanceToCreature >= 50)
                {
                    return 1.0;
                }
                return 0.0;
            };
            FixedRule ruleGoToDeliverySpot = AgentInitializer.InitializeActionRule(CurrentAgent, FixedRule.Factory, outputGoToDeliverySpot, goToDeliverySpotSupportCalculator);
            CurrentAgent.Commit(ruleGoToDeliverySpot);
            

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
            si.Add(inputNextFoodTheta, GetNextJewelTheta());
            si.Add(inputNextFoodRay, GetNextJewelRay());
            si.Add(inputFuel, GetFuel());


			//Console.WriteLine(sensorialInformation);
			Creature c = (Creature) listOfThings.Where(item => (item.CategoryId == Thing.CATEGORY_CREATURE)).First();
            creature = c;
            Fuel = c.Fuel;
			int n = 0;
            int bestPagamento = 0;
			foreach(Leaflet l in c.getLeaflets()) {
                if (l.payment > bestPagamento) {
                    bestPagamento = l.payment;
                    bestLeaflet = l;
                }
				mind.updateLeaflet(n,l);
				n++;
			}


            double menorDistanciaPorCor = double.MaxValue;
            double menorDistanciaFood = double.MaxValue;

            bestJewel = null;
            bestFood = null;

            if (bestLeaflet != null)
            {
                
                foreach(LeafletItem item in bestLeaflet.items)
                {

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
                            bestJewel = joia;
                        }
                    }
                    if(bestJewel == null) {
                        IEnumerable<Thing> deliverList = listOfThings.Where(t =>
                        t.CategoryId == Thing.CATEGORY_DeliverySPOT);
                        foreach (Thing deliverSpot in deliverList)
                        {
                            deliverSpotLocation = deliverSpot;
                        }

                    int n_joias_faltantes = 0;
                    foreach (LeafletItem li in bestLeaflet.items) {
                        n_joias_faltantes += (li.totalNumber - li.collected);
                    }
                    System.Console.WriteLine("Número de joias faltantes: " + n_joias_faltantes + "\n");


                    if (bestJewel == null && n_joias_faltantes == 0) {

                        bestJewel = new Thing() {
                            Name = "DeliverySPOT",
                            CategoryId = Thing.CATEGORY_DeliverySPOT,
                            DistanceToCreature = double.MaxValue,
                            X1 = 500,
                            Y1 = 500
                        };
                    }
                    }




                    IEnumerable<Thing> comidasVisivel = listOfThings.Where(t =>
                        (t.CategoryId == Thing.CATEGORY_FOOD || t.CategoryId == Thing.categoryPFOOD || t.CategoryId == Thing.CATEGORY_NPFOOD));

                    foreach (Thing comida in comidasVisivel)
                    {
                        if (comida.DistanceToCreature < menorDistanciaFood)
                        {
                            menorDistanciaFood = comida.DistanceToCreature;
                            bestFood = comida;
                        }
                    }

                    

                }

            }
            else
            {
                System.Console.WriteLine("No leaflet found in creature's inventory.\n");
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

        private void interact()
        {
            // se bestFood for uma comida e estiver com distancia menor que 50, interage (come a comida)
            if (bestFood != null && bestFood.CategoryId == Thing.CATEGORY_FOOD && bestFood.DistanceToCreature <= 50)
            {
                worldServer.SendEatIt(creatureId, bestFood.Name);
                System.Console.WriteLine("Eating Food: " + bestFood.Name + "\n");
            }
            // se bestJewel for uma joia e estiver com distancia menor que 50, interage (pega a joia)
            else if (bestJewel != null && bestJewel.CategoryId == Thing.CATEGORY_JEWEL && bestJewel.DistanceToCreature <= 50)
            {
                worldServer.SendSackIt(creatureId, bestJewel.Name);
                System.Console.WriteLine("Picking Up Jewel: " + bestJewel.Name + "\n");
            }
            else if (bestJewel != null && bestJewel.CategoryId == Thing.CATEGORY_DeliverySPOT && bestJewel.DistanceToCreature <= 50)
            {
                worldServer.deliverLeaflet(creatureId, bestLeaflet.leafletID.ToString());
                System.Console.WriteLine("Delivering Items at Delivery Spot: " + bestJewel.Name + "\n");
            }
            else if (deliverSpotLocation != null && deliverSpotLocation.DistanceToCreature <= 50)
            {
                worldServer.deliverLeaflet(creatureId, bestLeaflet.leafletID.ToString());
                System.Console.WriteLine("Delivering Items at Delivery Spot: " + deliverSpotLocation.Name + "\n");
            }
        }

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

                    // Regras explícitas
                    if (GetFuel() > 600)
                    {
                        Searching_food = false;
                    }
                    else if (GetFuel() < 300)
                    {
                        Searching_food = true;
                    }

                    bool madeSomeAction = false;

                    if (Searching_food)
                    {
                        if (bestFood != null)
                        {
                            if (bestFood.DistanceToCreature < 50)
                            {
                                interact();
                                madeSomeAction = true;
                            }
                            else
                            {
                                // Move towards food
                                Console.WriteLine("Moving towards Food: " + bestFood.Name + " Position: (" + bestFood.X1 + ", " + bestFood.Y1 + ") My Position: (" + creature.X1 + ", " + creature.Y1 + ")\n");
                                worldServer.SendSetGoTo(creatureId, 3, 3, bestFood.X1, bestFood.Y1);
                                madeSomeAction = true;
                            }
                        }
                    }
                    else // Searching for jewel
                    {
                        System.Console.WriteLine("Searching for Jewel, bestJewel: " + (bestJewel != null ? bestJewel.Name : "null") + "\n");
                        if (bestJewel != null)
                        {
                            if (bestJewel.DistanceToCreature < 50)
                            {
                                interact();
                                madeSomeAction = true;
                            }
                            else
                            {
                                // Move towards jewel
                                Console.WriteLine("Moving towards Jewel: " + bestJewel.Name + "\n");
                                worldServer.SendSetGoTo(creatureId, 3, 3, bestJewel.X1, bestJewel.Y1);
                                madeSomeAction = true;
                            }
                        }
                    }
                    if (!madeSomeAction)
                    {
                        // If no specific action was taken, just rotate to explore the environment
                        Console.WriteLine("No specific action taken. Rotating to explore...\n");
                        worldServer.SendSetAngle(creatureId, 2, -2, 2);
                    }


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
