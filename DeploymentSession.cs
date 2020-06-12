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

    public interface IDeploymentSession
    {
        Task<bool> Deploy(BuildNode Build);
        void Abort();
        List<ITargetDevice> Devices { get; }
        void OnFileDeployed(ITargetDevice Device, string File);
        void OnBuildDeployed(ITargetDevice Device, BuildNode Build);
        void OnBuildDeployedError(ITargetDevice Device, BuildNode Build, string ErrorMessage);
        void OnBuildDeployedAborted(ITargetDevice Device, BuildNode Build);
    }

	public interface IDeploymentCallback
	{
        void OnDeploymentDone(IDeploymentSession Session);
	}

	public interface ITargetDevice
	{
        bool Ping();
		bool DeployBuild(BuildNode Build, IDeploymentSession Callback, CancellationToken Token);
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
        string Status { get; }
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

	public class DeploymentSession : IDeploymentSession
    {
		private MainForm WinForm;

        private TreeListView DeviceView;

        private IDeploymentCallback Callback;

        private Project ProjectConfig;

        private Task<bool> DeploymentTask;

		private CancellationTokenSource CancellationTaskTokenSource;

		public List<ITargetDevice> Devices { get; private set; }


        public DeploymentSession(IDeploymentCallback Callback, MainForm WinForm, TreeListView DeviceView, List<ITargetDevice> Devices)
		{
			this.WinForm = WinForm;
            this.DeviceView = DeviceView;
			this.Callback = Callback;
            this.Devices = Devices;
        }

        public async Task<bool> Deploy(BuildNode InBuild)
        {
            CancellationTaskTokenSource = new CancellationTokenSource();

            var Build = new BuildNode(InBuild.UseBuild, InBuild.Number, InBuild.Timestamp, InBuild.Path, InBuild.Platform, InBuild.Solution, InBuild.Role, InBuild.AutomatedTestStatus);

            Devices[0].ProjectConfig = WinForm.GetProject(Build);
            Devices[0].Build = Build;

            ThreadHelperClass.DeployBuild(WinForm, DeviceView, Devices[0]);

            DeploymentTask = Task.Run(() => Devices[0].DeployBuild(Build, this, CancellationTaskTokenSource.Token), CancellationTaskTokenSource.Token);

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
				MessageBox.Show(string.Format("Failed to cancel deployment for device {0}. {1}", Devices[0].Address, e.Message), "Abort Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

        public void OnFileDeployed(ITargetDevice Device, string SourceFile)
        {
            ThreadHelperClass.UpdateDeviceDeploymentProgress(WinForm, DeviceView, Device);
        }

        public void OnBuildDeployed(ITargetDevice Device, BuildNode Build)
        {
            ThreadHelperClass.SetDeviceDeploymentResult(WinForm, DeviceView, Device, BuildDeploymentResult.Success);

            Callback.OnDeploymentDone(this);
        }

        public void OnBuildDeployedError(ITargetDevice Device, BuildNode Build, string ErrorMessage)
        {
            ThreadHelperClass.SetDeviceDeploymentResult(WinForm, DeviceView, Device, BuildDeploymentResult.Failure);

            Callback.OnDeploymentDone(this);
        }

        public void OnBuildDeployedAborted(ITargetDevice Device, BuildNode Build)
        {
            Build.Progress = 0;

            ThreadHelperClass.SetDeviceDeploymentResult(WinForm, DeviceView, Device, BuildDeploymentResult.Aborted);

            Callback.OnDeploymentDone(this);
        }
    }

}
