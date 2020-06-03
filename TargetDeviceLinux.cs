using Renci.SshNet;
using Renci.SshNet.Sftp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeploymentTool
{
	public class Shell
	{
		private SshClient SshLinuxClient = null;

		private string AsyncCommandOutput = string.Empty;
		
		public Shell(SshClient SshLinuxClient)
		{
			this.SshLinuxClient = SshLinuxClient;
		}

		public string Execute(string Argument)
		{
			if (!SshLinuxClient.IsConnected)
			{
				SshLinuxClient.Connect();
			}

			var Command = SshLinuxClient.CreateCommand(Argument);
			var Result = Command.Execute();
			var OutputStreamReader = new StreamReader(Command.OutputStream);
			var ErrorStreamReader = new StreamReader(Command.ExtendedOutputStream);

			string ErrorMessage = ErrorStreamReader.ReadToEnd();

			if (ErrorMessage.Length > 0)
			{
				throw new Exception(string.Format("Failed to run shell command '{0}' with error '{1}'", Argument, ErrorMessage));
			}

			return Result;
		}

		public string ExecuteAsync(string Argument)
		{
			var Command = SshLinuxClient.CreateCommand(Argument);
			var Result = Command.BeginExecute();

			/*
			using (var Reader = new StreamReader(Command.OutputStream, Encoding.UTF8, true, 1024, true))
			{
				while (!Result.IsCompleted || !Reader.EndOfStream)
				{
					string Line = Reader.ReadLine();
					if (Line != null)
					{
						AsyncCommandOutput += Line;
					}
				}
			}
			*/
			Command.EndExecute(Result);

			return string.Empty;
		}

	}

	public class TargetDeviceLinux : ITargetDevice
	{
		private ILogger Logger;
		private Shell Command;
		private ConnectionInfo Connection;
		private IDeploymentCallback Callback;
		private BuildNode Build;
		private CancellationToken Token;
		public Device DeviceConfig { get; }

		public TargetDeviceLinux(Device DeviceConfig, BuildNode Build, ILogger Logger)
		{
            this.Build = Build;
            this.Logger = Logger;
			this.DeviceConfig = DeviceConfig;
			this.Connection = new ConnectionInfo(DeviceConfig.Address, DeviceConfig.Username,
					new PasswordAuthenticationMethod(DeviceConfig.Username, DeviceConfig.Password));
			this.Command = new Shell(new SshClient(Connection));
		}

        public bool Ping()
        {
            return NetworkHelper.PingDevice(DeviceConfig.Address, Logger);
        }

        public bool DeployBuild(IDeploymentCallback Callback, CancellationToken Token)
		{
			this.Callback = Callback;
			this.Token = Token;

			try
			{
				if (!ResetProgress())
                {
                    return CheckCancelationRequestAndReport();
                }

				if (!StopProcesses())
				{
					return CheckCancelationRequestAndReport();
				}

				if (!InstallBuild())
				{
					return CheckCancelationRequestAndReport();
				}

				if (!StartProcess())
				{
					return CheckCancelationRequestAndReport();
				}

				Logger.Info(string.Format("Build {0} successfully deployed to Linux machine {1}", Build.Number, DeviceConfig.Address));

				Callback.OnBuildDeployed(this, Build);

				return true;
			}
			catch(Exception e)
			{
				Logger.Error(string.Format("Deploy build {0} failed with error {1}", Build.Number, e.Message));
			}
			
			return CheckCancelationRequestAndReport();
		}

		private bool ResetProgress()
		{
			Logger.Info(string.Format("Start deploying build {0} to device {1}", Build.Number, DeviceConfig.Address));

			const int ProgressStopProcess = 2;
			const int ProgressStartProcess = 2;

			DeviceConfig.Progress = 0;
			DeviceConfig.Status = "";
			DeviceConfig.ProgressMax = Directory.GetFiles(Build.Path, "*", SearchOption.AllDirectories).Length + ProgressStopProcess + ProgressStartProcess;

            return Ping();
        }

		private bool CheckCancelationRequestAndReport()
		{
			if (Token.IsCancellationRequested)
			{
				Logger.Warning(string.Format("User aborted deployment of build {0} on Linux machine {1}", Build.Number, DeviceConfig.Address));

				Callback.OnBuildDeployedAborted(this, Build);

				return false;
			}

			Callback.OnBuildDeployedError(this, Build, "Deploy build failed, see logs for more information");

			return false;
		}

		private bool InstallBuild()
		{
			bool InstallSuccess = false;

			try
			{
				if (Token.IsCancellationRequested)
				{
					return false;
				}

				DeviceConfig.Status = "Installing Build";

				using (var Sftp = new SftpClient(Connection))
				{
					Sftp.Connect();

					Logger.Info(string.Format("Deleting any old build(s) from {0}/{1}", DeviceConfig.Address, DeviceConfig.DeploymentPath));

					// Delete any old builds
					DeleteDirectory(Sftp, DeviceConfig.DeploymentPath);

					if (!Sftp.Exists(DeviceConfig.DeploymentPath))
					{
						Sftp.CreateDirectory(DeviceConfig.DeploymentPath);
					}

					DirectoryInfo BuildPathInfo = new DirectoryInfo(Build.Path);
					string ParentPath = BuildPathInfo.Parent.FullName;

					if (UploadDirectory(Callback, Sftp, ParentPath, DeviceConfig.DeploymentPath, BuildPathInfo.Name))
					{
						InstallSuccess = true;
					}

					Sftp.Disconnect();
				}

				return InstallSuccess;
			}
			catch (Exception e)
			{
				Logger.Error(string.Format("Failed to install build '{0}' to target device '{1}'. Ex: {2}", Build.Number, DeviceConfig.Address, e.Message));
			}

			return false;
		}

        public bool StopProcess()
        {
            string ProcessName = GetLinuxDedicatedProcessName();
            return StopProcess(ProcessName);
        }

		private bool StopProcesses()
		{
			try
			{
				// Increase progress once when stop processes is started
				DeviceConfig.Progress++;
				DeviceConfig.Status = "Stopping Processes";

				string GameProjectName = GetGameProjectName();

				string DedicatedTestServerName = string.Format("{0}Server-Linux-Test", GameProjectName);
				if (!StopProcess(DedicatedTestServerName))
				{
					return false;
				}

				string DedicatedDevelopmentServerName = string.Format("{0}Server", GameProjectName);
				if(!StopProcess(DedicatedDevelopmentServerName))
				{
					return false;
				}

				string DedicatedShippingServerName = string.Format("{0}Server-Linux-Shipping", GameProjectName);
				if (!StopProcess(DedicatedShippingServerName))
				{
					return false;
				}

				// Increase progress when stop processes is finished.
				DeviceConfig.Progress++;
				return true;
			}
			catch(Exception e)
			{
				Logger.Error(string.Format("Stop process threw an exception during build install '{0}' to target device '{1}'. Ex: {2}", Build.Number, DeviceConfig.Address, e.Message));
			}

			return false;
		}

		private string GetGameProjectName()
		{
			DirectoryInfo BuildInfo = new DirectoryInfo(Build.Path);

			var BuildInfoStringList = BuildInfo.Name.Split('-').Select(x => { return x; }).ToList();
			if (BuildInfoStringList.Count() < 3)
			{
                // @HACK needs to be fixed.
                return "ShooterGame";
				//throw new Exception(string.Format("Build folder name '{0}' does not contain expected fields.", Build.Path));
			}

			string GameProjectName = BuildInfoStringList[2];
			return GameProjectName;
		}

		private bool StopProcess(string ProcessName, string ExpectedPath = "")
		{
			try
			{
				var CommandResult = Command.Execute(string.Format("pidof {0}", ProcessName));

				string[] ProcessIds = CommandResult.Split(' ');

				// Kill all processes with the current name running on the Linux machine.
				foreach (var ProcessId in ProcessIds)
				{
					if (string.IsNullOrEmpty(ProcessId))
					{
						continue;
					}

					int ProcessID = 0;
					if (!int.TryParse(ProcessId, out ProcessID))
					{
						continue;
					}

					Logger.Info(string.Format("Stopping dedicated server '{0}' with process id {1}", ProcessName, ProcessID));

					Command.Execute(string.Format("kill -9 {0}", ProcessID));
				}

				return (IsProcessRunning(ProcessName) == false);
			}
			catch(Exception e)
			{
				Logger.Error(string.Format("Stop process threw an exception during deployment of build '{0}' to target device '{1}'. Ex: {2}", Build.Number, DeviceConfig.Address, e.Message));
			}

			return false;
		}

        public bool IsProcessRunning()
        {
            string ProcessName = GetLinuxDedicatedProcessName();

            return IsProcessRunning(ProcessName);
        }

		private bool IsProcessRunning(string ProcessName)
		{
			var CommandResult = Command.Execute(string.Format("pidof {0}", ProcessName));

			string[] ProcessIds = CommandResult.Split(' ');

			int ProcessCount = 0;

			foreach (var ProcessId in ProcessIds)
			{
				if (string.IsNullOrEmpty(ProcessId))
				{
					continue;
				}

				ProcessCount++;
			}

			return (ProcessCount > 0);
		}

		public bool StartProcess()
		{
			string LinuxServerProcessName = GetLinuxDedicatedProcessName();

			try
			{
				// Increase progress when start process is started
				DeviceConfig.Progress++;
				DeviceConfig.Status = "Starting Process";

				if (Token.IsCancellationRequested)
				{
					return false;
				}

				Logger.Info(string.Format("Starting dedicated server process '{0}' on Linux machine: {1}. Cmd Line Args: {2}", LinuxServerProcessName, DeviceConfig.Address, DeviceConfig.CmdLineArguments));
				// Get the full path including the build directory.
				string FullBuildDeploymentPath = GetFullBuildDeploymentPath();

                string CrashReportClientPath = Path.Combine(FullBuildDeploymentPath, "Engine", "Binaries", "Linux").Replace("\\", "/");
                // Change access permissions for  crash report client
                Command.Execute(string.Format("chmod u+x {0}/CrashReportClient", CrashReportClientPath));
                // Get the name of the shell script to start the Linux dedicated server.
                string LinuxStartServerShell = string.Format("{0}Server.sh", GetGameProjectName());
				// Set Working Directory
				Command.Execute(string.Format("pushd {0}", FullBuildDeploymentPath));
				// Change access permissions so that we can start the dedicated server
				Command.Execute(string.Format("chmod u+x {0}/{1}", FullBuildDeploymentPath, LinuxStartServerShell));
				// Start the dedicated Linux server.
				//new Task(() => Command.ExecuteAsync(string.Format("{0}/{1} {2} -crashreports -core", FullBuildDeploymentPath, LinuxStartServerShell, DeviceConfig.CmdLineArguments))).Start();
				// @Hack Lets give the async task some time to execute our request before checking if the server has started.
				//Thread.Sleep(6000);

                string SshCommandArguments = string.Format("{0}/{1} {2} -crashreports -core", FullBuildDeploymentPath, LinuxStartServerShell, DeviceConfig.CmdLineArguments);

                SshCommand.ExecuteAsync(Logger, SshCommandArguments, this);

                var StartTime = DateTime.Now;

                int ProcessID = GetProcessID();

                const int ProcessStartedTimeThreshold = 20;

                // Spin and check if process started for maximum of 20 seconds.
                while (ProcessID == -1 && (DateTime.Now - StartTime).TotalSeconds < ProcessStartedTimeThreshold)
                {
                    Thread.Sleep(250);

                    ProcessID = GetProcessID();
                }

                // Increase progress when start process is finished.
                DeviceConfig.Progress++;

                if (GetProcessID() == -1)
                {
                    Logger.Error(string.Format("Failed to start process '{0}' for build '{1}' on target device '{2}'", LinuxServerProcessName, Build.Number, DeviceConfig.Address));
                    return false;
                }

                if (SetProcessAffinity() == false)
                {
                    Logger.Error(string.Format("Failed to set process affinity '{0}' for process '{1}' with process id '{2}' on Linux device '{3}'", DeviceConfig.CpuAffinity, LinuxServerProcessName, ProcessID, DeviceConfig.Address));
                    return false;
                }

                Logger.Info(string.Format("Start process '{0}' successful for build '{1}' on target device '{2}'", LinuxServerProcessName, Build.Number, DeviceConfig.Address));
                return true;





                /*



                // Increase progress when start process is finished.
                DeviceConfig.Progress++;

                int ProcessID = GetProcessID();

                if (ProcessID > 0)
				//if (IsProcessRunning(LinuxServerProcessName))
				{
                    SetProcessAffinity();

					Logger.Info(string.Format("Start process '{0}' successful for build '{1}' on target device '{2}'", LinuxServerProcessName, Build.Number, DeviceConfig.Address));
					return true;
				}

				Logger.Error(string.Format("Failed to start process '{0}' for build '{1}' on target device '{2}'", LinuxServerProcessName, Build.Number, DeviceConfig.Address));
                
                return false;
                */
            }
            catch (Exception e)
			{
				Logger.Error(string.Format("Start process '{0}' threw an exception for build '{1}' on target device '{2}'. Ex: {3}", LinuxServerProcessName, Build.Number, DeviceConfig.Address, e.Message));
			}

			return false;
		}

        private bool SetProcessAffinity()
        {
            int ProcessID = GetProcessID();
            if (ProcessID == -1)
            {
                return false;
            }

            string ReturnValue = string.Empty, LogInfo = string.Empty, LogError = string.Empty;

            var CmdResult = SshCommand.ExecuteCommand(Logger, string.Format("taskset -cp {0} {1}", DeviceConfig.CpuAffinity, ProcessID), this, out ReturnValue, out LogInfo, out LogError);

            Logger.Info(string.Format("Process Affinity Command Result: {0}. {1}. {2}. {3} for process {4}({5}) on device {6}", ReturnValue, LogInfo, LogInfo, LogError, GetLinuxDedicatedProcessName(), ProcessID, DeviceConfig.Address));

            if (CmdResult == CommandResult.Success)
            {
                Logger.Info(string.Format("Process affinity for process '{0}' set to '{1}'", GetLinuxDedicatedProcessName(), DeviceConfig.CpuAffinity));
                return true;
            }

            Logger.Error(string.Format("Failed to set process affinity '{0}' for process '{1}'. {2}. {3}. {4}", DeviceConfig.CpuAffinity, GetLinuxDedicatedProcessName(), ReturnValue, LogInfo, LogError));
            return false;
        }

        private string GetLinuxDedicatedProcessName()
		{
			string LinuxServerProcessName = string.Format("{0}Server", GetGameProjectName());

			if (Build.Solution.Equals(Solution.Test.ToString()))
			{
				LinuxServerProcessName = string.Format("{0}-Linux-Test", LinuxServerProcessName);
			}
			else if (Build.Solution.Equals(Solution.Shipping.ToString()))
			{
				LinuxServerProcessName = string.Format("{0}-Linux-Shipping", LinuxServerProcessName);
			}

			return LinuxServerProcessName;
		}

		private string GetFullBuildDeploymentPath()
		{
			DirectoryInfo BuildInfo = new DirectoryInfo(Build.Path);
			string FullBuildDeploymentPath = Path.Combine(DeviceConfig.DeploymentPath, BuildInfo.Name).Replace("\\", "/");
			return FullBuildDeploymentPath;
		}

		private bool UploadDirectory(IDeploymentCallback Callback, SftpClient Sftp, string SourcePath, string DestinationPath, string DirSearchPattern)
		{
			if (Token.IsCancellationRequested)
			{
				return false;
			}

			var SourceDirectories = Directory.GetDirectories(SourcePath, DirSearchPattern, SearchOption.TopDirectoryOnly);
			foreach (var SourceDirectory in SourceDirectories)
			{
				try
				{
					DirectoryInfo SourceDirectoryInfo = new DirectoryInfo(SourceDirectory);
					string DestinationDirectory = Path.Combine(DestinationPath, SourceDirectoryInfo.Name).Replace("\\", "/");
					Sftp.CreateDirectory(DestinationDirectory);

					var SourceFiles = Directory.GetFiles(SourceDirectory, "*.*", SearchOption.TopDirectoryOnly);
					foreach (var SourceFile in SourceFiles)
					{
						if (Token.IsCancellationRequested)
						{
							return false;
						}

						DirectoryInfo SourceFileInfo = new DirectoryInfo(SourceFile);
						string UploadFilePath = Path.Combine(DestinationDirectory, SourceFileInfo.Name).Replace("\\", "/");

						using (var UploadFileStream = System.IO.File.OpenRead(SourceFile))
						{
							Sftp.UploadFile(UploadFileStream, UploadFilePath, true);

							Logger.Info(string.Format(@"Copying file to {0}/{1}", DeviceConfig.Address, UploadFilePath));

							Callback.OnFileDeployed(this, SourceFile);
						}
					}

					if (!UploadDirectory(Callback, Sftp, SourceDirectory, DestinationDirectory, "*"))
					{
						return false;
					}
				}
				catch (Exception e)
				{
					Logger.Error(string.Format("Upload directory threw an exception during file transfer '{0}'. Ex: {1}", SourceDirectory, e.Message));

					return false;
				}
			}

			return true;
		}

		private void DeleteDirectory(SftpClient Sftp, string Path)
		{
			try
			{
				if (Sftp.Exists(Path))
				{
					foreach (SftpFile File in Sftp.ListDirectory(Path))
					{
						if ((File.Name != ".") && (File.Name != ".."))
						{
							if (File.IsDirectory)
							{
								DeleteDirectory(Sftp, File.FullName);
							}
							else
							{
								Sftp.DeleteFile(File.FullName);
							}
						}
					}

					Sftp.DeleteDirectory(Path);
				}
			}
			catch (Exception e)
			{
				Logger.Error(string.Format("Delete directory {0} threw an exception. Ex: {1}", Path, e.Message));
			}
		}

        public int GetProcessID()
        {
            int ProcessID = -1;

            string ReturnValue = string.Empty, LogInfo = string.Empty, LogError = string.Empty;

            try
            {
                var CmdResult = SshCommand.ExecuteCommand(Logger, string.Format("pidof {0}", GetLinuxDedicatedProcessName()), this, out ReturnValue, out LogInfo, out LogError);
                if (CmdResult == CommandResult.Failure)
                {
                    return ProcessID;
                }

                string[] ProcessIds = ReturnValue.Split(' ');

                foreach (var ProcessId in ProcessIds)
                {
                    if (string.IsNullOrEmpty(ProcessId))
                    {
                        continue;
                    }

                    int.TryParse(ProcessId, out ProcessID);
                }

                if (ProcessID > 0)
                {
                    return ProcessID;
                }
            }
            catch (Exception e)
            {
                Logger.Warning(string.Format("Failed to get process id for process '{0}' on Linux device '{1}'. {2}. {3}. {4}. Ex: {5}", GetLinuxDedicatedProcessName(), DeviceConfig.Address, ReturnValue, LogInfo, LogError, e.Message));
            }

            return ProcessID;
        }

    }
}
