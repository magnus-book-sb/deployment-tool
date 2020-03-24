using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeploymentTool
{
	public static class NetworkShare
	{
		/// <summary>
		/// Connects to the remote share
		/// </summary>
		/// <returns>Null if successful, otherwise error message.</returns>
		public static void ConnectToShare(string Uri, string Username, string Password)
		{
			//Create netresource and point it at the share
			NETRESOURCE NetResource = new NETRESOURCE();
			NetResource.dwType = RESOURCETYPE_DISK;
			NetResource.lpRemoteName = Uri;

			//Create the share
			int ErrorID = WNetUseConnection(IntPtr.Zero, NetResource, Password, Username, 0, null, null, null);
			if (ErrorID != NO_ERROR)
			{
				throw new Exception(string.Format("Connect to share '{0}' failed. {1}", Uri, GetError(ErrorID)));
			}
		}

		/// <summary>
		/// Remove the share from cache.
		/// </summary>
		/// <returns>Null if successful, otherwise error message.</returns>
		public static void DisconnectFromShare(string Uri, bool Force)
		{
			//remove the share
			int ErrorID = WNetCancelConnection(Uri, Force);
			if (ErrorID != NO_ERROR)
			{
				throw new Exception(string.Format("Disconnect from share '{0}' failed. {1}", Uri, GetError(ErrorID)));
			}
		}

		#region P/Invoke Stuff
		[DllImport("Mpr.dll")]
		private static extern int WNetUseConnection(
			IntPtr hwndOwner,
			NETRESOURCE lpNetResource,
			string lpPassword,
			string lpUserID,
			int dwFlags,
			string lpAccessName,
			string lpBufferSize,
			string lpResult
			);

		[DllImport("Mpr.dll")]
		private static extern int WNetCancelConnection(
			string lpName,
			bool fForce
			);

		[StructLayout(LayoutKind.Sequential)]
		private class NETRESOURCE
		{
			public int dwScope = 0;
			public int dwType = 0;
			public int dwDisplayType = 0;
			public int dwUsage = 0;
			public string lpLocalName = "";
			public string lpRemoteName = "";
			public string lpComment = "";
			public string lpProvider = "";
		}

		#region Consts
		const int RESOURCETYPE_DISK = 0x00000001;
		const int CONNECT_UPDATE_PROFILE = 0x00000001;
		#endregion

		#region Errors
		const int NO_ERROR = 0;

		const int ERROR_ACCESS_DENIED = 5;
		const int ERROR_ALREADY_ASSIGNED = 85;
		const int ERROR_BAD_DEVICE = 1200;
		const int ERROR_BAD_NET_NAME = 67;
		const int ERROR_BAD_PROVIDER = 1204;
		const int ERROR_CANCELLED = 1223;
		const int ERROR_EXTENDED_ERROR = 1208;
		const int ERROR_INVALID_ADDRESS = 487;
		const int ERROR_INVALID_PARAMETER = 87;
		const int ERROR_INVALID_PASSWORD = 1216;
		const int ERROR_MORE_DATA = 234;
		const int ERROR_NO_MORE_ITEMS = 259;
		const int ERROR_NO_NET_OR_BAD_PATH = 1203;
		const int ERROR_NO_NETWORK = 1222;
		const int ERROR_SESSION_CREDENTIAL_CONFLICT = 1219;

		const int ERROR_BAD_PROFILE = 1206;
		const int ERROR_CANNOT_OPEN_PROFILE = 1205;
		const int ERROR_DEVICE_IN_USE = 2404;
		const int ERROR_NOT_CONNECTED = 2250;
		const int ERROR_OPEN_FILES = 2401;

		private class ErrorClass
		{
			public int ErrorID;

			public string Message;
			public ErrorClass(int ErrorID, string Message)
			{
				this.ErrorID = ErrorID;
				this.Message = Message;
			}
		}

		private static ErrorClass[] ErrorList = new ErrorClass[] {
			new ErrorClass(ERROR_ACCESS_DENIED, "Error: Access Denied"),
			new ErrorClass(ERROR_ALREADY_ASSIGNED, "Error: Already Assigned"),
			new ErrorClass(ERROR_BAD_DEVICE, "Error: Bad Device"),
			new ErrorClass(ERROR_BAD_NET_NAME, "Error: Bad Net Name"),
			new ErrorClass(ERROR_BAD_PROVIDER, "Error: Bad Provider"),
			new ErrorClass(ERROR_CANCELLED, "Error: Cancelled"),
			new ErrorClass(ERROR_EXTENDED_ERROR, "Error: Extended Error"),
			new ErrorClass(ERROR_INVALID_ADDRESS, "Error: Invalid Address"),
			new ErrorClass(ERROR_INVALID_PARAMETER, "Error: Invalid Parameter"),
			new ErrorClass(ERROR_INVALID_PASSWORD, "Error: Invalid Password"),
			new ErrorClass(ERROR_MORE_DATA, "Error: More Data"),
			new ErrorClass(ERROR_NO_MORE_ITEMS, "Error: No More Items"),
			new ErrorClass(ERROR_NO_NET_OR_BAD_PATH, "Error: No Net Or Bad Path"),
			new ErrorClass(ERROR_NO_NETWORK, "Error: No Network"),
			new ErrorClass(ERROR_BAD_PROFILE, "Error: Bad Profile"),
			new ErrorClass(ERROR_CANNOT_OPEN_PROFILE, "Error: Cannot Open Profile"),
			new ErrorClass(ERROR_DEVICE_IN_USE, "Error: Device In Use"),
			new ErrorClass(ERROR_EXTENDED_ERROR, "Error: Extended Error"),
			new ErrorClass(ERROR_NOT_CONNECTED, "Error: Not Connected"),
			new ErrorClass(ERROR_OPEN_FILES, "Error: Open Files"),
			new ErrorClass(ERROR_SESSION_CREDENTIAL_CONFLICT, "Error: Credential Conflict"),
		};

		private static string GetError(int ErrorID)
		{
			ErrorClass Error = ErrorList.ToList().Find(x => x.ErrorID == ErrorID);
			if (Error != null)
			{
				return string.Format("{0} ({1})", Error.Message, ErrorID);
			}
			
			return string.Format("Error: Unknown {0}", ErrorID);
		}
		#endregion

		#endregion
	}

	public class TargetDeviceWin64 : ITargetDevice
	{
		private ILogger Logger;		
		private IDeploymentCallback Callback;
		private BuildNode Build;
		private CancellationToken Token;
		public Device DeviceConfig { get; }

		public TargetDeviceWin64(Device DeviceConfig, ILogger Logger)
		{
			this.Logger = Logger;
			this.DeviceConfig = DeviceConfig;
		}

		

		public bool DeployBuild(IDeploymentCallback Callback, BuildNode Build, CancellationToken Token)
		{
			this.Callback = Callback;
			this.Build = Build;
			this.Token = Token;

			try
			{
				ResetProgress();

				if (!StopProcesses())
				{
					return CheckCancelationRequestAndReport();
				}

				if (!DeployBuild())
				{
					return CheckCancelationRequestAndReport();
				}

				if (!StartProcess())
				{
					return CheckCancelationRequestAndReport();
				}

				Logger.Info(string.Format("Build {0} successfully deployed to Win64 machine {1}", Build.Number, DeviceConfig.Address));

				Callback.OnBuildDeployed(this, Build);

				return true;
			}
			catch(Exception e)
			{
				Logger.Error(string.Format("Failed to deploy Win64 build {0} to device {1}. {2}", Build.Number, DeviceConfig.Address, e.Message));
			}

			return CheckCancelationRequestAndReport();
		}

		private bool CheckCancelationRequestAndReport()
		{
			if (Token.IsCancellationRequested)
			{
				Logger.Warning(string.Format("User aborted deployment of build {0} on Win64 machine {1}", Build.Number, DeviceConfig.Address));

				Callback.OnBuildDeployedAborted(this, Build);

				return false;
			}

			Callback.OnBuildDeployedError(this, Build, "Deploy build failed, see logs for more information");

			return false;
		}

		private bool DeployBuild()
		{
			if (Token.IsCancellationRequested)
			{
				return false;
			}

			var DeploymentPath = GetDeploymentPath();

			NetworkShare.ConnectToShare(DeploymentPath.FullName, DeviceConfig.Username, DeviceConfig.Password);

			bool InstallResult = InstallBuild(DeploymentPath);

			NetworkShare.DisconnectFromShare(DeploymentPath.FullName, true);

			return InstallResult;
		}

		private bool InstallBuild(DirectoryInfo DeploymentPath)
		{
			if (Token.IsCancellationRequested)
			{
				return false;
			}

			DeviceConfig.Progress++;
			DeviceConfig.Status = "Installing Build";

			foreach (var DeploymentDirectory in DeploymentPath.GetDirectories())
			{
				Directory.Delete(DeploymentDirectory.FullName, true);
			}

			var InstallBuildPath = GetInstallBuildPath();

			if (!Directory.Exists(InstallBuildPath.FullName))
			{
				Directory.CreateDirectory(InstallBuildPath.FullName);
			}

			if (Token.IsCancellationRequested)
			{
				return false;
			}

			if (!Copy(Build.Path, InstallBuildPath.FullName))
			{
				return false;
			}

			DeviceConfig.Progress++;

			return true;
		}


		private DirectoryInfo GetDeploymentPath()
		{
			string FullDeploymentPath = string.Format(@"\\{0}\{1}", DeviceConfig.Address, DeviceConfig.DeploymentPath);

			return new DirectoryInfo(FullDeploymentPath);
		}

		private DirectoryInfo GetInstallBuildPath()
		{
			var DeploymentPath = GetDeploymentPath();

			DirectoryInfo InstallBuildPathInfo = new DirectoryInfo(Build.Path);

			string InstallBuildPath = Path.Combine(DeploymentPath.FullName, InstallBuildPathInfo.Name);

			return new DirectoryInfo(InstallBuildPath);
		}

		private DirectoryInfo GetExecutablePath(string BuildSolution)
		{
			var InstallBuildPath = GetInstallBuildPath();
			string ExecutablePath = Path.Combine(InstallBuildPath.FullName, GetGameProjectName(), "Binaries", "Win64", GetProcessName(BuildSolution));
			return new DirectoryInfo(ExecutablePath);
		}

		private bool Copy(string SourceDirectory, string TargetDirectory)
		{
			DirectoryInfo DiSource = new DirectoryInfo(SourceDirectory);
			DirectoryInfo TiTarget = new DirectoryInfo(TargetDirectory);

			return CopyAll(DiSource, TiTarget);
		}

		private bool CopyAll(DirectoryInfo Source, DirectoryInfo Target)
		{
			try
			{
				if (Token.IsCancellationRequested)
				{
					return false;
				}

				Directory.CreateDirectory(Target.FullName);

				// Copy each file into the new directory.
				foreach (FileInfo SourceFile in Source.GetFiles())
				{
					Logger.Info(string.Format(@"Copying file to {0}{1}\{2}", DeviceConfig.Address, Target.FullName, SourceFile.Name));
					SourceFile.CopyTo(Path.Combine(Target.FullName, SourceFile.Name), true);

					if (Token.IsCancellationRequested)
					{
						return false;
					}

					Callback.OnFileDeployed(this, SourceFile.FullName);
				}

				// Copy each subdirectory using recursion.
				foreach (DirectoryInfo diSourceSubDir in Source.GetDirectories())
				{
					DirectoryInfo nextTargetSubDir =
						Target.CreateSubdirectory(diSourceSubDir.Name);

					if (!CopyAll(diSourceSubDir, nextTargetSubDir))
					{
						return false;
					}
				}

				return true;
			}
			catch(Exception e)
			{
				Logger.Error(string.Format("Failed to copy files from source {0} to destination {1}. {0}", Source.FullName, Target.FullName, e.Message));
			}

			return false;
		}

		private string GetGameProjectName()
		{
			DirectoryInfo BuildInfo = new DirectoryInfo(Build.Path);

			var BuildInfoStringList = BuildInfo.Name.Split('-').Select(x => { return x; }).ToList();
			if (BuildInfoStringList.Count() < 3)
			{
				throw new Exception(string.Format("Build folder name '{0}' does not contain expected fields.", Build.Path));
			}

			string GameProjectName = BuildInfoStringList[2];
			return GameProjectName;
		}

		private bool StartProcess()
		{
			DeviceConfig.Progress++;

			if (Build.Role.Equals(Role.Client.ToString()))
			{
				// Just return true if we are client we do not want to start the process on the client but treat it as success.
				return true;
			}

			DeviceConfig.Status = "Starting Process";

			var ExecutablePath = GetExecutablePath(Build.Solution);

			try
			{
				if (Token.IsCancellationRequested)
				{
					return false;
				}

				Logger.Info(string.Format("Starting process '{0}'", ExecutablePath.FullName));

				var ManagementScope = CreateManagementScope();

				if (ManagementScope == null)
				{
					return false;
				}

				if (Token.IsCancellationRequested)
				{
					return false;
				}

				using (var MngmntClass = new ManagementClass(ManagementScope, new ManagementPath("Win32_Process"), new ObjectGetOptions()))
				{
					MngmntClass.InvokeMethod("Create", new object[] { string.Format("{0} {1}", ExecutablePath, DeviceConfig.CmdLineArguments) } );

					var Query = new SelectQuery(string.Format("select * from Win32_process where name = '{0}'", GetProcessName(Build.Solution)));

					using (var Searcher = new ManagementObjectSearcher(ManagementScope, Query))
					{
						foreach (ManagementObject Process in Searcher.Get())
						{
							Logger.Info(string.Format("Process '{0}' started with Cmd Line Arguments: {1}", GetProcessName(Build.Solution), (Process["CommandLine"] != null) ? Process["CommandLine"] : string.Empty));
						}

						if (Searcher.Get().Count == 0)
						{
							Logger.Error(string.Format("Failed to start process '{0}'", GetProcessName(Build.Solution)));

							return false;
						}

						if (Searcher.Get().Count > 1)
						{
							Logger.Error(string.Format("More than one instance of the server is running"));

							return false;
						}
					}
				}

				return true;
			}
			catch(Exception e)
			{
				Logger.Error(string.Format("Failed to start Windows process '{0}' on device '{1}'. {2}", ExecutablePath.FullName, DeviceConfig.Address, e.Message));
			}

			return false;
		}

		private void ResetProgress()
		{
			Logger.Info(string.Format("Start deploying build {0} to device {1}", Build.Number, DeviceConfig.Address));

			const int ProgressStopProcess = 2;
			const int ProgressStartProcess = 2;

			DeviceConfig.Progress = 0;
			DeviceConfig.Status = "";
			DeviceConfig.ProgressMax = Directory.GetFiles(Build.Path, "*", SearchOption.AllDirectories).Length + ProgressStopProcess + ProgressStartProcess;
		}

		private bool StopProcesses()
		{
			DeviceConfig.Status = "Stopping Processes";
			DeviceConfig.Progress++;

			if (Build.Role.Equals(Role.Server.ToString()))
			{
				Logger.Info(string.Format("Stopping any running Win64 server process on target device '{0}'", DeviceConfig.Address));

				var ManagementScope = CreateManagementScope();

				if (ManagementScope == null)
				{
					return false;
				}

				// Stop any running windows server.
				var ExecutablePathDevelopmentBuild = GetProcessName(Solution.Development.ToString());
				if (IsProcessRunning(ExecutablePathDevelopmentBuild, ManagementScope) && !StopProcess(ExecutablePathDevelopmentBuild, ManagementScope))
				{
					return false;
				}

				var ExecutablePathTestBuild = GetProcessName(Solution.Test.ToString());
				if (IsProcessRunning(ExecutablePathTestBuild, ManagementScope) && !StopProcess(ExecutablePathTestBuild, ManagementScope))
				{
					return false;
				}

				var ExecutablePathShippingBuild = GetProcessName(Solution.Shipping.ToString());
				if (IsProcessRunning(ExecutablePathShippingBuild, ManagementScope) && !StopProcess(ExecutablePathShippingBuild, ManagementScope))
				{
					return false;
				}
			}

			return true;
		}

		private bool StopProcess(string ProcessName, ManagementScope Scope)
		{
			try
			{
				var Query = new SelectQuery(string.Format("select * from Win32_process where name = '{0}'", ProcessName));

				using (var Searcher = new ManagementObjectSearcher(Scope, Query))
				{
					foreach (ManagementObject Process in Searcher.Get())
					{
						Logger.Info(string.Format("Found process '{0}' with id {1} running on target device '{2}'. The process will be terminated", ProcessName, Process["ProcessID"], DeviceConfig.Address));

						Process.InvokeMethod("Terminate", null);
					}
				}

				Thread.Sleep(1000);

				if (IsProcessRunning(ProcessName, Scope))
				{
					Logger.Error(string.Format("Failed to terminate process {0}", ProcessName));

					return false;
				}

				Logger.Info(string.Format("Process {0} terminated", ProcessName));

				return true;
			}
			catch (Exception e)
			{
				Logger.Error(string.Format("Failed to terminate windows process '{0}' on device '{1}'. {2}", ProcessName, DeviceConfig.Address, e.Message));
			}

			return false;
		}

		private bool IsProcessRunning(string ProcessName, ManagementScope Scope)
		{
			var Query = new SelectQuery(string.Format("select * from Win32_process where name = '{0}'", ProcessName));

			using (var Searcher = new ManagementObjectSearcher(Scope, Query))
			{
				if (Searcher.Get().Count > 0)
				{
					return true;
				}
			}

			return false;
		}

		private ManagementScope CreateManagementScope()
		{
			try
			{
				ManagementScope Scope = null;

				if (IsLocalAddress())
				{
					Logger.Info(string.Format("Creating management scope to local host '{0}'", DeviceConfig.Address));

					var Path = new ManagementPath();
					Path.Server = "";
					Path.NamespacePath = @"\root\cimv2";
					Scope = new ManagementScope(Path);
				}
				else
				{
					Logger.Info(string.Format("Creating management scope to remote machine '{0}'", DeviceConfig.Address));

					var Connection = new ConnectionOptions();
					Connection.Username = DeviceConfig.Username;
					Connection.Password = DeviceConfig.Password;
					Scope = new ManagementScope(string.Format(@"\\{0}\root\cimv2", DeviceConfig.Address), Connection);
				}

				Scope.Connect();

				return Scope;
			}
			catch (Exception e)
			{
				Logger.Error(string.Format("Failed to create Windows management scope for device '{0}'. {1}", DeviceConfig.Address, e.Message));
			}

			return null;
		}

		private bool IsLocalAddress()
		{
			if (DeviceConfig.Address.Equals("127.0.0.1"))
			{
				return true;
			}

			var Host = Dns.GetHostEntry(Dns.GetHostName());

			foreach (var Address in Host.AddressList)
			{
				if (Address.AddressFamily == AddressFamily.InterNetwork && Address.ToString().Equals(DeviceConfig.Address)) 
				{
					return true;
				}
			}

			return false;
		}

		private string GetProcessName(string BuildSolution)
		{
			if (Build.Role.Equals(Role.Server.ToString()))
			{
				if (BuildSolution.Equals(Solution.Development.ToString()))
				{
					return "ShooterGameServer.exe";
				}

				if (BuildSolution.Equals(Solution.Test.ToString()))
				{
					return "ShooterGameServer-Win64-Test.exe";
				}

				return "ShooterGameServer-Win64-Shipping.exe";
			}

			if (BuildSolution.Equals(Solution.Development.ToString()))
			{
				return "ShooterGame.exe";
			}

			if (BuildSolution.Equals(Solution.Test.ToString()))
			{
				return "ShooterGame-Win64-Test.exe";
			}

			return "ShooterGame-Win64-Shipping.exe";
		}
	}
}
