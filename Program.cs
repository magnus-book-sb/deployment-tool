using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeploymentTool
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] commandLineArgs)
		{
			if (commandLineArgs.Length > 0) //then we have command line args
			{
                var CommandLineApp = new ConsoleApp();

                CommandLineApp.ProcessCommandLineArgs(commandLineArgs);

                CommandLineApp.RetrieveBuildsAndDevices();

				ConsoleAppMode CurrentMode = CommandLineApp.GetConsoleMode();
				if (CurrentMode == ConsoleAppMode.DeployAndStartDS)
				{
					CommandLineApp.CheckAndDeploy();
				}
				else if (CurrentMode == ConsoleAppMode.StartDS)
				{
					CommandLineApp.StartDedicatedServer();
				}

            }
			else
			{
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);
				Application.Run(new MainForm());
			}
		}
	}
}
