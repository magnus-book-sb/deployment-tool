using BrightIdeasSoftware;
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
		Device DeviceConfig { get; }
		bool DeployBuild(IDeploymentCallback Callback, CancellationToken Token);
        bool IsProcessRunning();
        bool StartProcess();
        bool StopProcess();
    }

    public class DeviceFactory
    {
        public static ITargetDevice CreateTargetDevice(Device DeviceConfig, BuildNode Build, ILogger Logger)
        {
            if (DeviceConfig.Platform.Equals("Linux"))
            {
                return new TargetDeviceLinux(DeviceConfig, Build, Logger); 
            }

            if (DeviceConfig.Platform.Equals("Win64"))
            {
                return new TargetDeviceWin64(DeviceConfig, Build, Logger);
            }

            return null;
        }
    }

	public class DeploymentSession
	{
		private Form MainForm;

		private IDeploymentCallback Callback;

		private ObjectListView ListView;

		private List<Device> DeviceList;

		private Task<bool> DeploymentTask;

		private CancellationTokenSource CancellationTaskTokenSource;

		public Device Device { get; private set; }

        public BuildNode Build { get; private set; }

        public ITargetDevice TargetDevice { get; private set; }

        public DeploymentSession(Form MainForm, IDeploymentCallback Callback, Device Device, ObjectListView ListView, List<Device> DeviceList)
		{
			this.MainForm = MainForm;
			this.Callback = Callback;
			this.Device = Device;
			this.ListView = ListView;
			this.DeviceList = DeviceList;
		}

		public async Task<bool> Deploy(BuildNode Build)
		{
            this.Build = Build;
			this.TargetDevice = DeviceFactory.CreateTargetDevice(Device, Build, new FileLogger(Device, ListView, DeviceList));;

			if (TargetDevice == null)
			{
				MessageBox.Show(string.Format("Target platform {0} is currently not supported", Device.Platform), "Invalid Platform", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}

			CancellationTaskTokenSource = new CancellationTokenSource();

			DeploymentTask = Task.Run(() => TargetDevice.DeployBuild(Callback, CancellationTaskTokenSource.Token), CancellationTaskTokenSource.Token);

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
