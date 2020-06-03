using BrightIdeasSoftware;
using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace DeploymentTool
{

    public partial class MainForm : Form, IDeploymentCallback
    {
        private List<BuildMachineNode> BuildList = new List<BuildMachineNode>();

        private MongoDb MongoDatabase = null;

        private List<Device> ServerDeviceList = new List<Device>();

        private Device SelectedServerDevice = null;

        private List<DeploymentSession> DeploySessions = new List<DeploymentSession>();

        private string LocalStagedBuildPath = string.Empty;

        public MainForm()
        {
            InitializeComponent();

            CreateBuildView();

            CreateServerDeviceView();
        }

        public void OnFileDeployed(ITargetDevice Device, string SourceFile)
        {
            ThreadHelperClass.UpdateDeviceDeploymentProgress(this, ServerView, Device.DeviceConfig, ServerDeviceList);
        }

        public void OnBuildDeployed(ITargetDevice Device, BuildNode Build)
        {
            DeploySessions.RemoveAll(x => x.Device.Address.Equals(Device.DeviceConfig.Address) && x.Device.Role.Equals(Device.DeviceConfig.Role));

            ThreadHelperClass.SetDeviceDeploymentResult(this, ServerView, Device.DeviceConfig, ServerDeviceList, BuildDeploymentResult.Success);
        }

        public void OnBuildDeployedError(ITargetDevice Device, BuildNode Build, string ErrorMessage)
        {
            DeploySessions.RemoveAll(x => x.Device.Address.Equals(Device.DeviceConfig.Address) && x.Device.Role.Equals(Device.DeviceConfig.Role));

            ThreadHelperClass.SetDeviceDeploymentResult(this, ServerView, Device.DeviceConfig, ServerDeviceList, BuildDeploymentResult.Failure);
        }

        public void OnBuildDeployedAborted(ITargetDevice Device, BuildNode Build)
        {
            DeploySessions.RemoveAll(x => x.Device.Address.Equals(Device.DeviceConfig.Address) && x.Device.Role.Equals(Device.DeviceConfig.Role));

            ThreadHelperClass.SetDeviceDeploymentResult(this, ServerView, Device.DeviceConfig, ServerDeviceList, BuildDeploymentResult.Aborted);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                this.MongoDatabase = new MongoDb();

                LoadBuildView();

                LoadServerDeviceView();
            }
            catch(Exception ex)
            {
                MessageBox.Show(string.Format("Failed to load deployment tool with error {0}", ex.Message));
            }
        }

        public object ImageGetter(object Object)
        {
            var Build = Object as BuildNode;

            if (Build == null || string.IsNullOrEmpty(Build.AutomatedTestStatus))
            {
                return null;
            }

            if (Build.AutomatedTestStatus.Equals("Passed"))
            {
                return 0;
            }

            if (Build.AutomatedTestStatus.Equals("Failed"))
            {
                return 1;
            }

            return null;
        }

        private void CreateBuildView()
		{
			try
			{
				ImageList BuildImageList = new ImageList();
				BuildImageList.Images.Add("green32", Image.FromFile(@"green32.png"));
				BuildImageList.Images.Add("red32", Image.FromFile(@"red32.png"));

                ContextMenu RightClickMenu = new ContextMenu();
                RightClickMenu.MenuItems.Add("Refresh Builds", new EventHandler(BuildViewRightClickMenuEventHandler));

                BuildView.Columns.Clear();
                BuildView.OwnerDraw = true;
                BuildView.ContextMenu = RightClickMenu;
				BuildView.SmallImageList = BuildImageList;
                BuildView.CellEditActivation = BrightIdeasSoftware.TreeListView.CellEditActivateMode.SingleClickAlways;
				BuildView.CheckBoxes = true;
                BuildView.CanExpandGetter = x => (x as BuildMachineNode != null ? (x as BuildMachineNode).Children.Count > 0 : ((x as BuildRoleNode != null) ? (x as BuildRoleNode).Children.Count > 0 : ((x as BuildPlatformNode != null) ? (x as BuildPlatformNode).Children.Count > 0 : ((x as BuildSolutionNode != null) ? (x as BuildSolutionNode).Children.Count > 0 : false))));
                BuildView.ChildrenGetter = x => (x as BuildMachineNode != null) ? new ArrayList((x as BuildMachineNode).Children) : ((x as BuildRoleNode != null) ? new ArrayList((x as BuildRoleNode).Children) : ((x as BuildPlatformNode != null) ? new ArrayList((x as BuildPlatformNode).Children) : ((x as BuildSolutionNode != null) ? new ArrayList((x as BuildSolutionNode).Children) : null)));

                var MachineNameColumn = new BrightIdeasSoftware.OLVColumn("Machine", "Machine");
                MachineNameColumn.Width = 150;
                MachineNameColumn.IsEditable = false;
                MachineNameColumn.ImageGetter += new ImageGetterDelegate(ImageGetter);
                MachineNameColumn.AspectGetter = x => (x as BuildMachineNode != null ? (x as BuildMachineNode).Machine : ((x as BuildRoleNode != null ? (x as BuildRoleNode).Role : ((x as BuildPlatformNode != null ? (x as BuildPlatformNode).Platform : ((x as BuildSolutionNode != null ? (x as BuildSolutionNode).Solution : string.Empty)))))));
                BuildView.Columns.Add(MachineNameColumn);
				
				var BuildColumn = new BrightIdeasSoftware.OLVColumn("Build", "Build");
				BuildColumn.Width = 100;
				BuildColumn.IsEditable = false;
				BuildColumn.AspectGetter = x => ((x as BuildNode != null ? (x as BuildNode).Number : string.Empty));
				BuildView.Columns.Add(BuildColumn);

				var AutomatedTestStatusColumn = new BrightIdeasSoftware.OLVColumn("Automated Test", "Automated Test");
				AutomatedTestStatusColumn.Width = 100;
				AutomatedTestStatusColumn.IsEditable = false;
				AutomatedTestStatusColumn.AspectGetter = x => ((x as BuildNode != null ? (x as BuildNode).AutomatedTestStatus : string.Empty));
				BuildView.Columns.Add(AutomatedTestStatusColumn);

				var TimestampColumn = new BrightIdeasSoftware.OLVColumn("Timestamp", "Timestamp");
				TimestampColumn.Width = 120;
				TimestampColumn.IsEditable = false;
				TimestampColumn.AspectGetter = x => (x as BuildNode != null ? (x as BuildNode).Timestamp : string.Empty);
				BuildView.Columns.Add(TimestampColumn);

				var PathColumn = new BrightIdeasSoftware.OLVColumn("Build Server Path", "Build Server Path");
				PathColumn.Width = 750;
				PathColumn.IsEditable = false;
				PathColumn.AspectGetter = x => (x as BuildNode != null ? (x as BuildNode).Path : string.Empty);
				BuildView.Columns.Add(PathColumn);
			}
			catch (Exception ex)
			{
				Console.WriteLine(string.Format("FillBuildView Ex: {0}", ex.Message));
			}
		}

        public void BuildViewRightClickMenuEventHandler(object sender, EventArgs e)
        {
            var MachineNode = BuildView.SelectedItem.RowObject as BuildMachineNode;
            if (MachineNode != null)
            {
                if (MachineNode.Machine.Equals(System.Environment.MachineName.ToUpper()))
                {
                    LocalStagedBuildPath = Interaction.InputBox("Staged Build Path", "Supply Path", LocalStagedBuildPath, -1, -1);

                    if (string.IsNullOrEmpty(LocalStagedBuildPath))
                    {
                        return;
                    }

                    if (!Directory.Exists(LocalStagedBuildPath))
                    {
                        MessageBox.Show(string.Format("Supplied path '{0}' not valid", LocalStagedBuildPath), "Invalid Staged Build Path", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        LocalStagedBuildPath = string.Empty;
                        return;
                    }
                }

                LoadBuildView();
            }
        }

        public void DeviceViewRightClickMenuEventHandler(object sender, EventArgs e)
        {
            if (ServerView.SelectedItem == null)
            {
                return;
            }

            var RightClickMenuItem = sender as MenuItem;

            try
            {
                var SelectedDevice = ServerView.SelectedItem.RowObject as Device;

                var DeploySession = DeploySessions.Find(x => x.Device.Equals(SelectedDevice));

                string SelectedMenu = RightClickMenuItem.Text;

                if (SelectedMenu.Equals("Open Log"))
                {
                    OpenLogFile(SelectedDevice);
                }
                else if(SelectedMenu.Equals("Abort Deployment"))
                {
                    if (DeploySession != null)
                    {
                        DeploySession.Abort();
                    }
                }
                else
                {
                    if (SelectedDevice.Status == null || !SelectedDevice.Status.Equals(BuildDeploymentResult.Success.ToString()))
                    {
                        MessageBox.Show(string.Format("Cannot open device information until build has been deployed successfully"), "", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        return;
                    }

                    var DeviceInfoForm = new DeviceForm();
                    DeviceInfoForm.DeviceConfig = new Device(SelectedDevice.UseDevice, SelectedDevice.Platform, SelectedDevice.Role, SelectedDevice.Name, SelectedDevice.Address, SelectedDevice.Username, SelectedDevice.Password, SelectedDevice.CpuAffinity, SelectedDevice.DeploymentPath, SelectedDevice.CmdLineArguments, SelectedDevice.Build);
                    var Result = DeviceInfoForm.ShowDialog();
                }
            }
            catch (Exception Ex)
            {
                MessageBox.Show(string.Format("{0} error. {1}", RightClickMenuItem.Text, Ex.Message), "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenLogFile(Device SelectedDevice)
        {
            string LogFile = string.Empty;

            try
            {
                LogFile = FileLogger.GetLogFile(SelectedDevice);

                Process.Start(LogFile);
            }
            catch (Exception Ex)
            {
                MessageBox.Show(string.Format("Failed to open log file {0}. {1}", LogFile, Ex.Message), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CreateServerDeviceView()
		{
            ContextMenu RightClickMenu = new ContextMenu();
            RightClickMenu.MenuItems.Add("Open Device", new EventHandler(DeviceViewRightClickMenuEventHandler));
            RightClickMenu.MenuItems.Add("Open Log", new EventHandler(DeviceViewRightClickMenuEventHandler));
            RightClickMenu.MenuItems.Add("Abort Deployment", new EventHandler(DeviceViewRightClickMenuEventHandler));
            
            ServerView.Columns.Clear();
			ServerView.OwnerDraw = true;
            ServerView.ContextMenu = RightClickMenu;
			ServerView.CellEditActivation = BrightIdeasSoftware.TreeListView.CellEditActivateMode.SingleClickAlways;
			ServerView.CheckBoxes = true;
			ServerView.CellEditStarting += new CellEditEventHandler(OnServerDeviceViewCellEditingStarting);
			ServerView.CheckStatePutter += new CheckStatePutterDelegate(UseServerDeviceAspectPutter);
			ServerView.CheckStateGetter += new CheckStateGetterDelegate(UseServerDeviceAspectGetter);

			var UseDeviceColumn = new BrightIdeasSoftware.OLVColumn("Use Device", "Use Device");
			UseDeviceColumn.Width = 80;
			ServerView.Columns.Add(UseDeviceColumn);

			var ProgressColumn = new BrightIdeasSoftware.OLVColumn("Progress", "Progress");
			ProgressColumn.Width = 80;
			ProgressColumn.IsEditable = false;
			ProgressColumn.Renderer = new BarRenderer(0, 100);
			ProgressColumn.AspectGetter += new AspectGetterDelegate(ProgressBarUpdateAspectGetter);
			ServerView.Columns.Add(ProgressColumn);

			var DeploymentColumn = new BrightIdeasSoftware.OLVColumn("Progress %", "Progress %");
			DeploymentColumn.Width = 80;
			DeploymentColumn.IsEditable = false;
			DeploymentColumn.TextAlign = HorizontalAlignment.Center;
			DeploymentColumn.AspectGetter += new AspectGetterDelegate(ProgressUpdateAspectGetter);
			ServerView.Columns.Add(DeploymentColumn);

			var StatusColumn = new BrightIdeasSoftware.OLVColumn("Status", "Status");
			StatusColumn.Width = 80;
			StatusColumn.IsEditable = false;
			StatusColumn.AspectGetter = x => (x as Device).Status;
			ServerView.Columns.Add(StatusColumn);

			var PlatformColumn = new BrightIdeasSoftware.OLVColumn("Platform", "Platform");
			PlatformColumn.Width = 70;
			PlatformColumn.AspectGetter = x => (x as Device).Platform;
			ServerView.Columns.Add(PlatformColumn);

			var RoleColumn = new BrightIdeasSoftware.OLVColumn("Role", "Role");
			RoleColumn.Width = 70;
			RoleColumn.AspectGetter = x => (x as Device).Role;
			ServerView.Columns.Add(RoleColumn);

			var AddressColumn = new BrightIdeasSoftware.OLVColumn("Address", "Address");
			AddressColumn.Width = 90;
			AddressColumn.AspectGetter = x => (x as Device).Address;
			ServerView.Columns.Add(AddressColumn);

			var NameColumn = new BrightIdeasSoftware.OLVColumn("Name", "Name");
			NameColumn.Width = 150;
			NameColumn.AspectGetter = x => (x as Device).Name;
			ServerView.Columns.Add(NameColumn);

			var UserNameColumn = new BrightIdeasSoftware.OLVColumn("User Name", "User Name");
			UserNameColumn.Width = 80;
			UserNameColumn.AspectGetter = x => (x as Device).Username;
			ServerView.Columns.Add(UserNameColumn);

			var PasswordColumn = new BrightIdeasSoftware.OLVColumn("Password", "Password");
			PasswordColumn.Width = 80;
			PasswordColumn.AspectGetter = x => (x as Device).Password;
			ServerView.Columns.Add(PasswordColumn);

            var CpuAffinityColumn = new BrightIdeasSoftware.OLVColumn("CPU Affinity", "CPU Affinity");
            CpuAffinityColumn.Width = 80;
            CpuAffinityColumn.AspectGetter = x => (x as Device).CpuAffinity;
            ServerView.Columns.Add(CpuAffinityColumn);

            var TargetPathColumn = new BrightIdeasSoftware.OLVColumn("Deployment Path", "Deployment Path");
			TargetPathColumn.Width = 200;
			TargetPathColumn.AspectGetter = x => (x as Device).DeploymentPath;
			ServerView.Columns.Add(TargetPathColumn);

			var ArgumentColumn = new BrightIdeasSoftware.OLVColumn("Cmd Line Argument", "Cmd Line Argument");
			ArgumentColumn.Width = 200;
			ArgumentColumn.AspectGetter = x => (x as Device).CmdLineArguments;
			ServerView.Columns.Add(ArgumentColumn);
		}

		private void OnServerDeviceViewCellEditingStarting(object sender, CellEditEventArgs e)
		{
			try
			{
				SelectedServerDevice = e.RowObject as Device;

				if (SelectedServerDevice == null)
				{
					return;
				}

				int index = ServerDeviceList.FindIndex(x => x == SelectedServerDevice);

				if (ServerDeviceList.Count() - index <= 1)
				{
					ServerDeviceList.Add(new Device(false, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, string.Empty, string.Empty));
                    ServerView.SetObjects(ServerDeviceList);
				}

				if (e.Column.Text.Equals("Platform"))
				{
					var PlatformComboBox = new ComboBox { Bounds = e.CellBounds, DropDownStyle = ComboBoxStyle.DropDownList };
					PlatformComboBox.Validating += new CancelEventHandler(OnServerDeviceViewPlatformComboBoxValidating);
					PlatformComboBox.Items.Add("Linux");
					PlatformComboBox.Items.Add("Win64");
					PlatformComboBox.Text = SelectedServerDevice.Platform;
					e.Control = PlatformComboBox;
				}

				if (e.Column.Text.Equals("Role"))
				{
					var RoleComboBox = new ComboBox { Bounds = e.CellBounds, DropDownStyle = ComboBoxStyle.DropDownList };
					RoleComboBox.Validating += new CancelEventHandler(OnServerDeviceViewRoleComboBoxValidating);
					RoleComboBox.Items.Add("Server");
					RoleComboBox.Items.Add("Client");
					RoleComboBox.Text = SelectedServerDevice.Role;
					e.Control = RoleComboBox;
				}

				if (e.Column.Text.Equals("User Name"))
				{
					var UserNameTextBox = new TextBox { Bounds = e.CellBounds };
					UserNameTextBox.Text = SelectedServerDevice.Username;
					UserNameTextBox.Validating += new CancelEventHandler(OnServerDeviceViewUserNameTextBoxValidating);
					e.Control = UserNameTextBox;
				}

				if (e.Column.Text.Equals("Password"))
				{
					var PasswordTextBox = new TextBox { Bounds = e.CellBounds };
					PasswordTextBox.Text = SelectedServerDevice.Password;
					PasswordTextBox.Validating += new CancelEventHandler(OnServerDeviceViewPasswordTextBoxValidating);
					e.Control = PasswordTextBox;
				}

                if (e.Column.Text.Equals("CPU Affinity"))
                {
                    var CpuAffinityNumericUpDown = new NumericUpDown { Bounds = e.CellBounds };
                    CpuAffinityNumericUpDown.Minimum = 0;
                    CpuAffinityNumericUpDown.Maximum = 64;
                    CpuAffinityNumericUpDown.GotFocus += new EventHandler(OnDeviceViewCpuAffinityNumericUpDownClicked);
                    CpuAffinityNumericUpDown.LostFocus += new EventHandler(OnDeviceViewCpuAffinityNumericUpDownValidating);
                    e.Control = CpuAffinityNumericUpDown;
                }

                if (e.Column.Text.Equals("Deployment Path"))
				{
					var DeploymentPathTextBox = new TextBox { Bounds = e.CellBounds };
					DeploymentPathTextBox.Text = SelectedServerDevice.DeploymentPath;
					DeploymentPathTextBox.Validating += new CancelEventHandler(OnServerDeviceViewTargetInstallPathTextBoxValidating);
					e.Control = DeploymentPathTextBox;
				}

				if (e.Column.Text.Equals("Cmd Line Argument"))
				{
					var CmdLineArgumentTextBox = new TextBox { Bounds = e.CellBounds };
					CmdLineArgumentTextBox.Text = SelectedServerDevice.CmdLineArguments;
					CmdLineArgumentTextBox.Validating += new CancelEventHandler(OnServerDeviceViewTargetCmdLineArgumentTextBoxValidating);
					e.Control = CmdLineArgumentTextBox;
				}

				ServerView.Refresh();
			}
			catch (Exception ex)
			{
				Console.WriteLine(string.Format("Debug OnDeviceViewCellEditingStarting Ex: {0}", ex.Message));
			}
		}

        private void OnDeviceViewCpuAffinityNumericUpDownClicked(object sender, EventArgs e)
        {
            NumericUpDown CpuAffinityNumericUpDown = sender as NumericUpDown;
            if (CpuAffinityNumericUpDown != null && SelectedServerDevice != null)
            {
                CpuAffinityNumericUpDown.Value = SelectedServerDevice.CpuAffinity;
            }
        }

        private void OnDeviceViewCpuAffinityNumericUpDownValidating(object sender, EventArgs e)
        {
            NumericUpDown CpuAffinityNumericUpDown = sender as NumericUpDown;
            if (CpuAffinityNumericUpDown != null)
            {
                SelectedServerDevice.CpuAffinity = (int)CpuAffinityNumericUpDown.Value;
                ServerView.RefreshObject(SelectedServerDevice);
            }
        }

        private CheckState UseServerDeviceAspectPutter(object Object, CheckState Value)
		{
			var SelectedDevice = Object as Device;
			if (SelectedDevice != null)
			{
				SelectedDevice.UseDevice = (Value == CheckState.Checked);
			}

			return Value;
		}

		private CheckState UseServerDeviceAspectGetter(object Object)
		{
			SelectedServerDevice = Object as Device;

			return (SelectedServerDevice != null && SelectedServerDevice.UseDevice) ? CheckState.Checked : CheckState.Unchecked;
		}

		private object ProgressBarUpdateAspectGetter(object Object)
		{
			return CalculateProgressPercentage(Object as Device);
		}

		private object ProgressUpdateAspectGetter(object Object)
		{
			return string.Format("{0} %", CalculateProgressPercentage(Object as Device));
		}

		private int CalculateProgressPercentage(Device SelectedDevice)
		{
			if (SelectedDevice.ProgressMax == 0 || SelectedDevice.Progress == 0)
			{
				return 0;
			}

			double Ratio = Convert.ToDouble(SelectedDevice.Progress) / Convert.ToDouble(SelectedDevice.ProgressMax);

			int ProgressPercentage = Convert.ToInt32(Ratio * 100);

			return ProgressPercentage;
		}

		private void OnServerDeviceViewUseDeviceCheckBoxValidating(object sender, EventArgs e)
		{	
			CheckBox UseDeviceCheckBox = sender as CheckBox;
			if (UseDeviceCheckBox != null)
			{
				SelectedServerDevice.UseDevice = UseDeviceCheckBox.Checked;
				ServerView.RefreshObject(SelectedServerDevice);
			}
		}

		private void OnServerDeviceViewPlatformComboBoxValidating(object sender, EventArgs e)
		{
			ComboBox PlatformComboBox = sender as ComboBox;
			if (PlatformComboBox != null)
			{
				SelectedServerDevice.Platform = PlatformComboBox.Text;
				ServerView.RefreshObject(SelectedServerDevice);
			}
		}

		private void OnServerDeviceViewRoleComboBoxValidating(object sender, EventArgs e)
		{
			ComboBox RoleComboBox = sender as ComboBox;
			if (RoleComboBox != null)
			{
				SelectedServerDevice.Role = RoleComboBox.Text;
				ServerView.RefreshObject(SelectedServerDevice);
			}
		}

		private void OnServerDeviceViewUserNameTextBoxValidating(object sender, EventArgs e)
		{
			TextBox UserNameTextBox = sender as TextBox;
            if (UserNameTextBox != null)
            {
				SelectedServerDevice.Username = UserNameTextBox.Text;
				ServerView.RefreshObject(SelectedServerDevice);
			}
		}

		private void OnServerDeviceViewPasswordTextBoxValidating(object sender, EventArgs e)
		{
			TextBox PasswordTextBox = sender as TextBox;
			if (PasswordTextBox != null)
			{
				SelectedServerDevice.Password = PasswordTextBox.Text;
				ServerView.RefreshObject(SelectedServerDevice);
			}
		}

		private void OnServerDeviceViewTargetInstallPathTextBoxValidating(object sender, EventArgs e)
		{
			TextBox TargetInstallPathTextBox = sender as TextBox;
			if (TargetInstallPathTextBox != null)
			{
				SelectedServerDevice.DeploymentPath = TargetInstallPathTextBox.Text;
				ServerView.RefreshObject(SelectedServerDevice);
			}
		}

		private void OnServerDeviceViewTargetCmdLineArgumentTextBoxValidating(object sender, EventArgs e)
		{
			TextBox CmdLineArgumentTextBox = sender as TextBox;
			if (CmdLineArgumentTextBox != null)
			{
				SelectedServerDevice.CmdLineArguments = CmdLineArgumentTextBox.Text;
				ServerView.RefreshObject(SelectedServerDevice);
			}
		}

		private BuildSolutionNode CreateBuildSolutionNode(List<BuildRecord> Builds, Solution Solution, Role Role)
		{
			var SolutionBuilds = Builds.FindAll(x => x.Solution.Equals(Solution.ToString()));
			var BuildSolution = new BuildSolutionNode(Solution.ToString());

			foreach (var SolutionBuild in SolutionBuilds)
			{
				if (!Directory.Exists(SolutionBuild.Path))
				{
					continue;
				}

				BuildSolution.Children.Add(new BuildNode(false, SolutionBuild.BuildNumber, SolutionBuild.Timestamp.ToString(), SolutionBuild.Path, SolutionBuild.Platform, Solution.ToString(), Role.ToString(), SolutionBuild.Status));
			}

			return BuildSolution;
		}

		private BuildPlatformNode CreatePlatformNode(Platform Platform, Role Role)
		{
			var PlatformBuilds = MongoDatabase.GetAvailableBuilds(Platform, Role);
			var DevBuildNode = CreateBuildSolutionNode(PlatformBuilds, Solution.Development, Role);
			var TestBuildNode = CreateBuildSolutionNode(PlatformBuilds, Solution.Test, Role);
			var ShippingBuildNode = CreateBuildSolutionNode(PlatformBuilds, Solution.Shipping, Role);

			var PlatformNode = new BuildPlatformNode(Platform.ToString());

			PlatformNode.Children.Add(DevBuildNode);
			PlatformNode.Children.Add(TestBuildNode);
			PlatformNode.Children.Add(ShippingBuildNode);

			return PlatformNode;
		}

        private void LoadBuildView()
		{
			BuildList.Clear();
			BuildView.ClearObjects();
            
            var BuildServerNode = CreateBuildMachineNode();
            var LocalhostNode = CreateLocalHostNode();

            BuildList.Add(BuildServerNode);
            BuildList.Add(LocalhostNode);

            BuildView.Roots = BuildList;
			BuildView.Refresh();
        }

        private BuildMachineNode CreateBuildMachineNode()
        {
            BuildMachineNode BuildServerNode = new BuildMachineNode("SEH-DLAN-01");

            BuildRoleNode ServerNode = new BuildRoleNode("Server");
            ServerNode.Children.Add(CreatePlatformNode(Platform.Linux, Role.Server));
            ServerNode.Children.Add(CreatePlatformNode(Platform.Win64, Role.Server));

            BuildRoleNode ClientNode = new BuildRoleNode("Client");
            ClientNode.Children.Add(CreatePlatformNode(Platform.Win64, Role.Client));
            ClientNode.Children.Add(CreatePlatformNode(Platform.XboxOne, Role.Client));
            ClientNode.Children.Add(CreatePlatformNode(Platform.PS4, Role.Client));

            BuildServerNode.Children.Add(ServerNode);
            BuildServerNode.Children.Add(ClientNode);

            return BuildServerNode;
        }

        private BuildPlatformNode CreatePlatformNode(string Platform, string Role, string[] BuildDirectories)
        {
            var PlatformNode = new BuildPlatformNode(Platform);

            var DevelopmentBuild = new BuildSolutionNode("Development");
            var TestBuild = new BuildSolutionNode("Test");
            var ShippingBuild = new BuildSolutionNode("Shipping");
            var UnknownBuild = new BuildSolutionNode("Unknown");

            foreach (var  BuildDirectory in BuildDirectories)
            {
                DirectoryInfo BuildDirectoryInfo = new DirectoryInfo(BuildDirectory);
                if (BuildDirectoryInfo.Parent.ToString().ToLower().Equals("binaries") || BuildDirectoryInfo.Parent.ToString().ToLower().Equals("build"))
                {
                    continue;
                }

                if (BuildDirectoryInfo.Parent.ToString().ToLower().Equals("development"))
                {
                    DevelopmentBuild.Children.Add(new BuildNode(false, "Local-Cooked", BuildDirectoryInfo.CreationTime.ToString(), BuildDirectory, Platform, "Development", Role, ""));
                    continue;
                }

                if (BuildDirectoryInfo.Parent.ToString().ToLower().Equals("test"))
                {
                    TestBuild.Children.Add(new BuildNode(false, "Local-Cooked", BuildDirectoryInfo.CreationTime.ToString(), BuildDirectory, Platform, "Test", Role, ""));
                    continue;
                }

                if (BuildDirectoryInfo.Parent.ToString().ToLower().Equals("shipping"))
                {
                    ShippingBuild.Children.Add(new BuildNode(false, "Local-Cooked", BuildDirectoryInfo.CreationTime.ToString(), BuildDirectory, Platform, "Shipping", Role, ""));
                    continue;
                }

                UnknownBuild.Children.Add(new BuildNode(false, "Local-Cooked", BuildDirectoryInfo.CreationTime.ToString(), BuildDirectory, Platform, "Unknown", Role, ""));
            }

            PlatformNode.Children.Add(DevelopmentBuild);
            PlatformNode.Children.Add(TestBuild);
            PlatformNode.Children.Add(ShippingBuild);
            PlatformNode.Children.Add(UnknownBuild);

            return PlatformNode;
        }

        private BuildMachineNode CreateLocalHostNode()
        {
            BuildMachineNode LocalHostNode = new BuildMachineNode(System.Environment.MachineName);

            BuildRoleNode ServerNode = new BuildRoleNode("Server");
            BuildRoleNode ClientNode = new BuildRoleNode("Client");

            var CurrentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());

            if (string.IsNullOrEmpty(LocalStagedBuildPath) && CurrentDirectory.Parent != null && CurrentDirectory.Parent.Parent != null && CurrentDirectory.Parent.Parent.Name.Equals("UE4"))
            {
                LocalStagedBuildPath = Path.Combine(CurrentDirectory.Parent.Parent.FullName, "ShooterGame", "Saved", "StagedBuilds");

                if (!Directory.Exists(LocalStagedBuildPath))
                {
                    LocalStagedBuildPath = string.Empty;
                }
            }

            if (!string.IsNullOrEmpty(LocalStagedBuildPath))
            {
                var LinuxServerBuilds = Directory.GetDirectories(LocalStagedBuildPath, "LinuxServer", SearchOption.AllDirectories);
                ServerNode.Children.Add(CreatePlatformNode("Linux", "Server", LinuxServerBuilds));

                var WindowServerBuilds = Directory.GetDirectories(LocalStagedBuildPath, "WindowsServer", SearchOption.AllDirectories);
                ServerNode.Children.Add(CreatePlatformNode("Win64", "Server", WindowServerBuilds));

                var XboxOneBuilds = Directory.GetDirectories(LocalStagedBuildPath, "XboxOne", SearchOption.AllDirectories);
                ClientNode.Children.Add(CreatePlatformNode("XboxOne", "Client", XboxOneBuilds));

                var PS4Builds = Directory.GetDirectories(LocalStagedBuildPath, "PS4", SearchOption.AllDirectories);
                ClientNode.Children.Add(CreatePlatformNode("PS4", "Client", PS4Builds));

                var WindowClientBuilds = Directory.GetDirectories(LocalStagedBuildPath, "WindowsNoEditor", SearchOption.AllDirectories);
                ClientNode.Children.Add(CreatePlatformNode("Win64", "Client", WindowClientBuilds));
            }

            LocalHostNode.Children.Add(ServerNode);
            LocalHostNode.Children.Add(ClientNode);

            return LocalHostNode;
        }

        

        private void LoadServerDeviceView()
		{
			ServerDeviceList.Clear();
			ServerView.ClearObjects();

			var CurrentDirectory = Directory.GetCurrentDirectory();

			var ServerConfigFile = GetDeviceConfigFile();
			if (!System.IO.File.Exists(ServerConfigFile))
			{
				return;
			}

			var ServerDevices = new List<Device>();

			using (StreamReader Stream = new StreamReader(ServerConfigFile))
			{
				ServerDevices.AddRange(JsonConvert.DeserializeObject<List<Device>> (Stream.ReadToEnd()));
			}

			foreach (var ServerDevice in ServerDevices)
			{
				var DeviceNode = new Device(ServerDevice.UseDevice, ServerDevice.Platform, ServerDevice.Role, ServerDevice.Name, ServerDevice.Address, ServerDevice.Username, ServerDevice.Password, ServerDevice.CpuAffinity, ServerDevice.DeploymentPath, ServerDevice.CmdLineArguments);

				if (ServerDevice.UseDevice)
				{
					ServerView.CheckObject(DeviceNode);
				}

				ServerDeviceList.Add(DeviceNode);
			}

			ServerDeviceList.Add(new Device(false, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, string.Empty, string.Empty));
			ServerView.SetObjects(ServerDeviceList);
		}

		private void btnDeploy_Click(object sender, EventArgs e)
		{
			if (DeploySessions.Count() > 0)
			{
				MessageBox.Show("Deployment already in progress", "Deployment", MessageBoxButtons.OK, MessageBoxIcon.Information);

				return;
			}

			SaveDevices();

			DeployBuilds();
		}

		private void DeployBuilds()
		{
			try
			{
				var SelectedBuilds = BuildView.CheckedObjects as ArrayList;

				var SelectedServerBuilds = SelectedBuilds.ToArray().ToList().FindAll(x => (x as BuildNode != null) ? (x as BuildNode).Role.Equals("Server") : false);
				var SelectedClientBuilds = SelectedBuilds.ToArray().ToList().FindAll(x => (x as BuildNode != null) ? (x as BuildNode).Role.Equals("Client") : false);

				if (!IsValidDeviceConfiguration())
				{
					return;
				}

				if (!IsValidBuildSelection(SelectedServerBuilds, SelectedClientBuilds))
				{
					return;
				}

				DeploySessions.Clear();

				foreach (var CheckedObject in ServerView.CheckedObjects)
				{
					var SelectedDevice = CheckedObject as Device;

					SelectedDevice.Progress = 0;
					SelectedDevice.Status = string.Empty;
				}

				ServerView.SetObjects(ServerDeviceList);

				foreach (var CheckedObject in ServerView.CheckedObjects)
				{
					var SelectedDevice = CheckedObject as Device;

					if (SelectedDevice.Role.Equals(Role.Server.ToString()))
					{
                        if (SelectedServerBuilds.Find(x => (x as BuildNode).Platform.Equals(SelectedDevice.Platform)) == null)
                        {
                            MessageBox.Show("Build platform and device platform mismatch.", "Deployment Failure", MessageBoxButtons.OK, MessageBoxIcon.Error);

                            continue;
                        }

						Deploy(SelectedDevice, SelectedServerBuilds);
					}
					else
					{
                        if (SelectedClientBuilds.Find(x => (x as BuildNode).Platform.Equals(SelectedDevice.Platform)) == null)
                        {
                            MessageBox.Show("Build platform and device platform mismatch.", "Deployment Failure", MessageBoxButtons.OK, MessageBoxIcon.Error);

                            continue;
                        }

                        Deploy(SelectedDevice, SelectedClientBuilds);
					}
				}
			}
			catch(Exception e)
			{
				MessageBox.Show(string.Format("Deploy builds failed. {0}", e.Message), "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private bool IsValidDeviceConfiguration()
		{
			var CheckedDevices = ServerView.GetCheckedObjects().ToArray().ToList();

			foreach (var CheckedObject in ServerView.CheckedObjects)
			{
				var SelectedDevice = CheckedObject as Device;

				var DevicesWithSameAddress = CheckedDevices.FindAll(x => (x as Device).Platform.Equals(Platform.Win64.ToString()) && (x as Device).Address.Equals(SelectedDevice.Address) && (x as Device).DeploymentPath.Equals(SelectedDevice.DeploymentPath) && !(x as Device).Role.Equals(SelectedDevice.Role));
				if (DevicesWithSameAddress.Count() > 0)
				{
					MessageBox.Show(string.Format("The Win64 server and client with address {0} has the same deployment path, must be different", SelectedDevice.Address), "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Error);

					return false;
				}
			}

			return true;
		}

		private void Deploy(Device SelectedDevice, List<object> SelectedBuilds)
		{
			var SelectedBuild = SelectedBuilds.Find(x => (x as BuildNode).Platform.Equals(SelectedDevice.Platform)) as BuildNode;
			if (SelectedBuild != null)
			{
                SelectedDevice.Build = SelectedBuild;
                var Session = new DeploymentSession(this, this, SelectedDevice, ServerView, ServerDeviceList);
				DeploySessions.Add(Session);
				var Task = Session.Deploy(SelectedBuild);
			}
		}

		private bool IsValidBuildSelection(List<object> SelectedServerBuilds, List<object> SelectedClientBuilds)
		{
			if (SelectedServerBuilds.Count == 0 && SelectedClientBuilds.Count == 0)
			{
				MessageBox.Show("No selected builds to deploy", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}

			// Check that we have not selected more than one build for the same platform and role.
			if (!IsValidBuildSelection(SelectedServerBuilds, Platform.Linux))
			{
				MessageBox.Show("More than one Linux server build selected.", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}

			if (!IsValidBuildSelection(SelectedServerBuilds, Platform.Win64))
			{
				MessageBox.Show("More than one Win64 server build selected.", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}

			if (!IsValidBuildSelection(SelectedClientBuilds, Platform.Win64))
			{
				MessageBox.Show("More than one Win64 client build selected.", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}

			if (SelectedClientBuilds.ToList().FindAll(x => (x as BuildNode).Platform.Equals(Platform.PS4.ToString())).Count > 0)
			{
				MessageBox.Show("Client platform PS4 currently not supported", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}

			if (SelectedClientBuilds.ToList().FindAll(x => (x as BuildNode).Platform.Equals(Platform.XboxOne.ToString())).Count > 0)
			{
				MessageBox.Show("Client platform XboxOne currently not supported", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}

			string MatchingBuild = string.Empty;

			if (SelectedServerBuilds.Count() > 0 && SelectedClientBuilds.Count() > 0)
			{
				foreach (var SelectedServerBuild in SelectedServerBuilds)
				{
					var ServerBuild = SelectedServerBuild as BuildNode;
					string ServerBuildChangeList = ServerBuild.Number.Substring(ServerBuild.Number.IndexOf("-"));

					if (SelectedClientBuilds.Find(x => (x as BuildNode).Number.Substring((x as BuildNode).Number.IndexOf("-")).Equals(ServerBuildChangeList)) == null)
					{
						MatchingBuild += string.Format("{0}{1}", string.IsNullOrEmpty(MatchingBuild) ? "" : ",", ServerBuild.Number);
					}
				}

				if (!string.IsNullOrEmpty(MatchingBuild))
				{
					if (MessageBox.Show(string.Format("Server build(s) {0} does not match selected client build. Deploy anyway?", MatchingBuild), "Selected builds mismatch", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.Cancel)
					{
						return false;
					}
				}
			}

			return true;
		}


		private bool IsValidBuildSelection(List<object> SelectedBuilds, Platform Platform)
		{
			var SelectedPlatformBuilds = SelectedBuilds.ToList().FindAll(x => (x as BuildNode).Platform.Equals(Platform.ToString()));
			if (SelectedPlatformBuilds.Count > 1)
			{
				return false;
			}

			return true;
		}

		private void SaveDevices()
		{
			try
			{
				ServerDeviceList.RemoveAll(x => x.Address.Length == 0);

				string ConfigFile = GetDeviceSaveConfigFile();
				string JsonText = JsonConvert.SerializeObject(ServerDeviceList);
				System.IO.File.WriteAllText(ConfigFile, JsonText);
			}
			catch(Exception e)
			{
				MessageBox.Show(string.Format("Failed to save devices {0}", e.Message), "Save Devices Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}

			ServerDeviceList.Add(new Device(false, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, string.Empty, string.Empty));
		}

		private string GetDeviceConfigFile()
		{
			string DeviceConfigFile = GetDeviceSaveConfigFile();

			if (File.Exists(DeviceConfigFile))
			{
				return DeviceConfigFile;
			}

			string CurrentDirectory = Directory.GetCurrentDirectory();
			return Path.Combine(CurrentDirectory, "DeviceConfig.json");
		}

		private string GetDeviceSaveConfigFile()
		{
			string CurrentDirectory = Directory.GetCurrentDirectory();
			string DeviceConfigFileName = string.Format("{0}-DeviceConfig.json", System.Environment.MachineName);
			return Path.Combine(CurrentDirectory, DeviceConfigFileName);
		}

		private void btnAbort_Click(object sender, EventArgs e)
		{
			if (DeploySessions.Count() == 0)
			{
				MessageBox.Show("No deployments in progress", "Deployment", MessageBoxButtons.OK, MessageBoxIcon.Information);

				return;
			}

			foreach (var DeploySession in DeploySessions)
			{
                DeploySession.Abort();
            }
		}

	}

    public class BuildMachineNode
    {
        public string Machine { get; set; }
        public List<BuildRoleNode> Children { get; set; }
        public BuildMachineNode(string MachineName)
        {
            this.Machine = MachineName;
            this.Children = new List<BuildRoleNode>();
        }
    }

	public class BuildRoleNode
	{
		public string Role { get; set; }
		public List<BuildPlatformNode> Children { get; set; }
		public BuildRoleNode(string Role)
		{
			this.Role = Role;
			this.Children = new List<BuildPlatformNode>();
		}
	}

	public class BuildPlatformNode
	{
		public string Platform { get; set; }
		public List<BuildSolutionNode> Children { get; set; }
		public BuildPlatformNode(string Platform)
		{
			this.Platform = Platform;
			this.Children = new List<BuildSolutionNode>();
		}
	}

	public class BuildSolutionNode
	{
		public string Solution { get; set; }
		public List<BuildNode> Children { get; set; }
		public BuildSolutionNode(string Solution)
		{
			this.Solution = Solution;
			this.Children = new List<BuildNode>();
		}
	}

	public class BuildNode
	{
		public bool UseBuild { get; set; }
		public string Number { get; set; }
		public string Timestamp { get; set; }
		public string Path { get; set; }
		public string Platform { get; set; }
		public string Solution { get; set; }
		public string Role { get; set; }
		public string AutomatedTestStatus { get; set; }

		public BuildNode(bool UseBuild, string Number, string Timestamp, string Path, string Platform, string Solution, string Role, string AutomatedTestStatus)
		{
			this.UseBuild = UseBuild;
			this.Number = Number;
			this.Timestamp = Timestamp;
			this.Path = Path;
			this.Platform = Platform;
			this.Solution = Solution;
			this.Role = Role;
			this.AutomatedTestStatus = AutomatedTestStatus;
		}
	}


    public class DirectoryHelper
    {
        public static List<string> GetDirectories(string Search)
        {
            List<string> FoundDirectories = new List<string>();

            string[] Drives = System.Environment.GetLogicalDrives();

            foreach (string Drive in Drives)
            {
                System.IO.DriveInfo CurrentDriveInfo = new System.IO.DriveInfo(Drive);

                if (!CurrentDriveInfo.IsReady)
                {
                    continue;
                }

                WalkDirectoryTree(Search, CurrentDriveInfo.RootDirectory, FoundDirectories);
            }

            return FoundDirectories;
        }

        static private void WalkDirectoryTree(string Search, System.IO.DirectoryInfo RootDirectory, List<string> FoundDirectories)
        {
            System.IO.DirectoryInfo[]  SubDirs = RootDirectory.GetDirectories();

            foreach (System.IO.DirectoryInfo SubDir in SubDirs)
            {
                try
                {
                    if (SubDir.Name.Equals(Search))
                    {
                        FoundDirectories.Add(SubDir.FullName);

                        continue;
                    }

                    WalkDirectoryTree(Search, SubDir, FoundDirectories);
                }
                catch (UnauthorizedAccessException e)
                {
                }
            }
        }

    }
}
