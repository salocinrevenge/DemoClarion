
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using ClarionApp;
using ClarionApp.Model;
using ClarionApp.Exceptions;
using Gtk;

namespace ClarionApp
{
	class MainClass
	{
		#region properties
		private WSProxy ws = null;
        private ClarionAgent agent;
        String creatureId = String.Empty;
        String creatureName = String.Empty;
		#endregion

		#region constructor
		public MainClass() {
			Application.Init(); // Tirar = Quebrar
			Console.WriteLine ("ClarionApp V0.8");
			try
            {
                ws = new WSProxy("localhost", 4011);

                String message = ws.Connect();

                if (ws != null && ws.IsConnected)
                {
                    Console.Out.WriteLine ("[SUCCESS] " + message + "\n");
					ws.SendWorldReset();
                    ws.NewCreature(400, 200, 0, out creatureId, out creatureName);
					ws.SendCreateLeaflet();
                    ws.NewBrick(4, 747, 2, 800, 567);
                    ws.NewBrick(4, 50, -4, 747, 47);
                    ws.NewBrick(4, 49, 562, 796, 599);
                    ws.NewBrick(4, -2, 6, 50, 599);
					Console.Out.WriteLine ("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\n");

					if (!String.IsNullOrWhiteSpace(creatureId))
                    {
						ws.SendStartCamera(creatureId); // O que isso faz??
                        ws.SendStartCreature(creatureId);
                    }


					Console.Out.WriteLine("Creature created with name: " + creatureId + "\n");
					agent = new ClarionAgent(ws,creatureId,creatureName);
					agent.Run ();
					Console.Out.WriteLine("Running Simulation ...\n");
					Console.Out.WriteLine ("BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB\n");
				}
				else {
					Console.Out.WriteLine("The WorldServer3D engine was not found ! You must start WorldServer3D before running this application !");
					System.Environment.Exit(1);
				}
				Console.Out.WriteLine ("DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD\n");
			}
            catch (WorldServerInvalidArgument invalidArtgument)
            {
                Console.Out.WriteLine(String.Format("[ERROR] Invalid Argument: {0}\n", invalidArtgument.Message));
            }
            catch (WorldServerConnectionError serverError)
            {
                Console.Out.WriteLine(String.Format("[ERROR] Is is not possible to connect to server: {0}\n", serverError.Message));
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine(String.Format("[ERROR] Unknown Error: {0}\n", ex.Message));
            }
			Console.Out.WriteLine ("EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE\n");
			//Application.Run();
			simulationLoop();
			Console.Out.WriteLine ("FFFFFFFFFFFFFFFFFFFFF\n");
		}
		#endregion

		#region Methods
		public static void Main (string[] args)	{
			new MainClass();
			Console.Out.WriteLine ("CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC\n");
		}

		#endregion

		private void simulationLoop() {
			DateTime timer = DateTime.Now;
			Random rnd = new Random ();


			ws.createDeliverySpot(500,500);
			spawnThings();

			while (true) {
				if (ws != null && ws.IsConnected) {

					if ((DateTime.Now - timer).TotalSeconds >= 10) {
						try {
							spawnThings();
							timer = DateTime.Now; // Reinicia o timer
						} catch (Exception ex) {
							Console.WriteLine("[ERROR] Food failed: " + ex.Message);
						}
						
					}
					
				}
				Thread.Sleep(100); // Pequena pausa para evitar uso excessivo da CPU
			}
		}



		private void spawnThings ()
		{
			Random rnd = new Random ();
			for (int i = 0; i < 10; i++) {
				int tipo = rnd.Next (0, 8);
				int randomX = rnd.Next (50, 750);
				int randomY = rnd.Next (50, 550);
				if (tipo >= 6) {
					ws.NewFood (tipo - 6, randomX, randomY);
				} else {
					ws.NewJewel (tipo, randomX, randomY);
				}
				Console.WriteLine ("[SERVER] Novas comidas geradas automaticamente. Posicaos: (" + randomX + ", " + randomY + ") Tipo: " + tipo);
			}
		}


	}
	
	
}
