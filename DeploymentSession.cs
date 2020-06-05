using BrightIdeasSoftware;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeploymentTool
{
	public enum BuildDeploymentResult {  Success, Failure, Aborted };

	public interface IDeploymentCallback
	{
		void OnFileDeployed(ITargetDevice Device, string File);
		void OnBuildDeployed(ITargetDevice Device, BuildNode Build);
		void OnBuildDeployedError(ITargetDevice Device, BuildNode Build, string ErrorMessage);
		void OnBuildDeployedAborted(ITargetDevice Device, BuildNode Build);
	}

	public interface ITargetDevice
	{
        bool Ping();
		//Device DeviceConfig { get; }
		bool DeployBuild(IDeploymentCallback Callback, CancellationToken Token);
        bool IsProcessRunning();
        bool StartProcess();
        bool StopProcess();
        bool UseDevice { get; set; }
        string Platform { get; set; }
        string Role { get; set; }
        string Name { get; set; }
        string Address { get; set; }
        string Username { get; set; }
        string Password { get; set; }
        int CpuAffinity { get; set; }
        string DeploymentPath { get; set; }
        string CmdLineArguments { get; set; }
        BuildNode Build { get; set; }
        Project ProjectConfig { get; set; }
    }

    public class DeviceFactory
    {
        public static ITargetDevice CreateTargetDevice(bool UseDevice, string Platform, string Role, string Name, string Address, string Username, string Password, int CpuAffinity, string DeploymentPath, string CmdLineArguments)
        {
            if (Platform.Equals(PlatformType.Linux.ToString()))
            {
                return new TargetDeviceLinux(UseDevice, Platform, Role, Name, Address, Username, Password, CpuAffinity, DeploymentPath, CmdLineArguments);
            }

            if (Platform.Equals(PlatformType.Win64.ToString()))
            {
                return new TargetDeviceWin64(UseDevice, Platform, Role, Name, Address, Username, Password, CpuAffinity, DeploymentPath, CmdLineArguments);
            }

            if (Platform.Equals(PlatformType.PS4.ToString()))
            {
                return new TargetDevicePS4(UseDevice, Platform, Role, Name, Address, Username, Password, CpuAffinity, DeploymentPath, CmdLineArguments);
            }

            return null;
        }
    }

	public class DeploymentSession
	{
		private Form MainForm;

		private IDeploymentCallback Callback;

		private ObjectListView ListView;

		private List<PlatformNode> DeviceList;

        private Project ProjectConfig;

        private Task<bool> DeploymentTask;

		private CancellationTokenSource CancellationTaskTokenSource;

		public ITargetDevice Device { get; private set; }

        public BuildNode Build { get; private set; }

        public ITargetDevice TargetDevice { get; private set; }

        public DeploymentSession(Form MainForm, IDeploymentCallback Callback, ITargetDevice Device, ObjectListView ListView, List<PlatformNode> DeviceList, Project ProjectConfig)
		{
			this.MainForm = MainForm;
			this.Callback = Callback;
			this.Device = Device;
			this.ListView = ListView;
			this.DeviceList = DeviceList;
            this.ProjectConfig = ProjectConfig;
        }

		public async Task<bool> Deploy(BuildNode Build)
		{
            this.Build = Build;
			
			CancellationTaskTokenSource = new CancellationTokenSource();

			DeploymentTask = Task.Run(() => Device.DeployBuild(Callback, CancellationTaskTokenSource.Token), CancellationTaskTokenSource.Token);

			await DeploymentTask;

			return DeploymentTask.Result;
		}

		public void Abort()
		{
			try
			{
				CancellationTaskTokenSource.Cancel();
			}
			catch(Exception e)
			{
				MessageBox.Show(string.Format("Failed to cancel deployment for device {0}. {1}", Device.Address, e.Message), "Abort Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
	}

}
