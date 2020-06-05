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
        private List<ProjectNode> BuildList = new List<ProjectNode>();

        private List<PlatformNode> DeviceList = new List<PlatformNode>();

        private MongoDb MongoDatabase = null;

        private ITargetDevice SelectedDevice = null;

        private List<DeploymentSession> DeploySessions = new List<DeploymentSession>();

        private string LocalStagedBuildPath = string.Empty;

        public MainForm()
        {
            InitializeComponent();

            CreateBuildView();

            CreateDeviceView();
        }

        public void OnFileDeployed(ITargetDevice Device, string SourceFile)
        {
            ThreadHelperClass.UpdateDeviceDeploymentProgress(this, DeviceView, Device);
        }

        public void OnBuildDeployed(ITargetDevice Device, BuildNode Build)
        {
            DeploySessions.RemoveAll(x => x.Device.Address.Equals(Device.Address) && x.Device.Role.Equals(Device.Role));

            ThreadHelperClass.SetDeviceDeploymentResult(this, DeviceView, Device, BuildDeploymentResult.Success);
        }

        public void OnBuildDeployedError(ITargetDevice Device, BuildNode Build, string ErrorMessage)
        {
            DeploySessions.RemoveAll(x => x.Device.Address.Equals(Device.Address) && x.Device.Role.Equals(Device.Role));

            ThreadHelperClass.SetDeviceDeploymentResult(this, DeviceView, Device, BuildDeploymentResult.Failure);
        }

        public void OnBuildDeployedAborted(ITargetDevice Device, BuildNode Build)
        {
            DeploySessions.RemoveAll(x => x.Device.Address.Equals(Device.Address) && x.Device.Role.Equals(Device.Role));

            ThreadHelperClass.SetDeviceDeploymentResult(this, DeviceView, Device, BuildDeploymentResult.Aborted);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                this.MongoDatabase = new MongoDb();

                LoadBuildView();

                LoadDeviceView();
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
                BuildView.CanExpandGetter = x => (x as ProjectNode != null ? (x as ProjectNode).Children.Count > 0 : ((x as BuildMachineNode != null ? (x as BuildMachineNode).Children.Count > 0 : (x as BuildRoleNode != null) ? (x as BuildRoleNode).Children.Count > 0 : ((x as BuildPlatformNode != null) ? (x as BuildPlatformNode).Children.Count > 0 : ((x as BuildSolutionNode != null) ? (x as BuildSolutionNode).Children.Count > 0 : false)))));
                BuildView.ChildrenGetter = x => (x as ProjectNode != null) ? new ArrayList((x as ProjectNode).Children) : ((x as BuildMachineNode != null) ? new ArrayList((x as BuildMachineNode).Children) : (x as BuildRoleNode != null) ? new ArrayList((x as BuildRoleNode).Children) : ((x as BuildPlatformNode != null) ? new ArrayList((x as BuildPlatformNode).Children) : ((x as BuildSolutionNode != null) ? new ArrayList((x as BuildSolutionNode).Children) : null)));

                var ProjectNameColumn = new BrightIdeasSoftware.OLVColumn("Project", "Project");
                ProjectNameColumn.Width = 190;
                ProjectNameColumn.IsEditable = false;
                ProjectNameColumn.ImageGetter += new ImageGetterDelegate(ImageGetter);
                ProjectNameColumn.AspectGetter = x => (x as ProjectNode != null ? (x as ProjectNode).Project : ((x as BuildMachineNode != null ? (x as BuildMachineNode).Machine : (x as BuildRoleNode != null ? (x as BuildRoleNode).Role : ((x as BuildPlatformNode != null ? (x as BuildPlatformNode).Platform : ((x as BuildSolutionNode != null ? (x as BuildSolutionNode).Solution : string.Empty))))))));
                BuildView.Columns.Add(ProjectNameColumn);

                var MachineNameColumn = new BrightIdeasSoftware.OLVColumn("Machine", "Machine");
                MachineNameColumn.Width = 90;
                MachineNameColumn.IsEditable = false;
                MachineNameColumn.AspectGetter = x => ((x as BuildMachineNode != null ? (x as BuildMachineNode).Machine : string.Empty)); // ((x as BuildRoleNode != null ? (x as BuildRoleNode).Role : ((x as BuildPlatformNode != null ? (x as BuildPlatformNode).Platform : ((x as BuildSolutionNode != null ? (x as BuildSolutionNode).Solution : string.Empty)))))));
                BuildView.Columns.Add(MachineNameColumn);

                var BuildColumn = new BrightIdeasSoftware.OLVColumn("Build", "Build");
				BuildColumn.Width = 90;
				BuildColumn.IsEditable = false;
				BuildColumn.AspectGetter = x => ((x as BuildNode != null ? (x as BuildNode).Number : string.Empty));
				BuildView.Columns.Add(BuildColumn);

				var AutomatedTestStatusColumn = new BrightIdeasSoftware.OLVColumn("Automated Test", "Automated Test");
				AutomatedTestStatusColumn.Width = 100;
				AutomatedTestStatusColumn.IsEditable = false;
				AutomatedTestStatusColumn.AspectGetter = x => ((x as BuildNode != null ? (x as BuildNode).AutomatedTestStatus : string.Empty));
				BuildView.Columns.Add(AutomatedTestStatusColumn);

				var TimestampColumn = new BrightIdeasSoftware.OLVColumn("Timestamp", "Timestamp");
				TimestampColumn.Width = 110;
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

        public void DeviceViewRightClickCellEvent<CellRightClickEventArgs>(object Sender, CellRightClickEventArgs e)
        {
            if (DeviceView.SelectedObject as ITargetDevice != null)
            {
                ContextMenu RightClickMenu = new ContextMenu();
                //RightClickMenu.MenuItems.Add("Open Device", new EventHandler(DeviceViewRightClickMenuEventHandler));
                RightClickMenu.MenuItems.Add("Open Log", new EventHandler(DeviceViewRightClickMenuEventHandler));
                RightClickMenu.MenuItems.Add("Abort Deployment", new EventHandler(DeviceViewRightClickMenuEventHandler));

                if (DeploySessions.Find(x => x.Device.Equals(SelectedDevice)) == null && (DeviceView.SelectedObject as ITargetDevice).Build != null)
                {
                    RightClickMenu.MenuItems.Add("Start Application", new EventHandler(DeviceViewRightClickMenuEventHandler));
                    RightClickMenu.MenuItems.Add("Stop Application", new EventHandler(DeviceViewRightClickMenuEventHandler));
                }

                DeviceView.ContextMenu = RightClickMenu;
            }
            /*
            if (DeviceView.SelectedObject as BuildNode != null)
            {
                ContextMenu RightClickMenu = new ContextMenu();
                RightClickMenu.MenuItems.Add("Start Application", new EventHandler(DeviceViewRightClickMenuEventHandler));
                RightClickMenu.MenuItems.Add("Stop Application", new EventHandler(DeviceViewRightClickMenuEventHandler));
                DeviceView.ContextMenu = RightClickMenu;
            }
            */
        }

        public void CreateDeviceView()
        {
            try
            {
                DeviceView.Columns.Clear();
                DeviceView.OwnerDraw = true;
                DeviceView.CellRightClick += new EventHandler<CellRightClickEventArgs>(DeviceViewRightClickCellEvent);
                DeviceView.CellEditActivation = BrightIdeasSoftware.TreeListView.CellEditActivateMode.SingleClickAlways;
                DeviceView.CheckBoxes = true;

                DeviceView.CellEditStarting += new CellEditEventHandler(OnDeviceViewCellEditingStarting);
                DeviceView.CanExpandGetter = new TreeListView.CanExpandGetterDelegate(CanExpandBuildView);
                DeviceView.ChildrenGetter = new TreeListView.ChildrenGetterDelegate(BuildViewChildrenGetter); 
                DeviceView.CheckStatePutter += new CheckStatePutterDelegate(UseServerDeviceAspectPutter);
                DeviceView.CheckStateGetter += new CheckStateGetterDelegate(UseServerDeviceAspectGetter);

                var UseDeviceColumn = new BrightIdeasSoftware.OLVColumn("Use Device", "Use Device");
                UseDeviceColumn.Width = 90;
                UseDeviceColumn.AspectGetter = x => (x as PlatformNode != null ? (x as PlatformNode).Platform : string.Empty);
                DeviceView.Columns.Add(UseDeviceColumn);

                var BuildColumn = new BrightIdeasSoftware.OLVColumn("Build", "Build");
                BuildColumn.Width = 80;
                BuildColumn.CheckBoxes = false;
                BuildColumn.IsEditable = false;
                BuildColumn.AspectGetter = x => (x as BuildNode != null ? (x as BuildNode).Number : string.Empty);
                DeviceView.Columns.Add(BuildColumn);

                var StatusColumn = new BrightIdeasSoftware.OLVColumn("Status", "Status");
                StatusColumn.Width = 80;
                StatusColumn.IsEditable = false;
                StatusColumn.AspectGetter = x => (x as BuildNode != null ? (x as BuildNode).Status : string.Empty);
                DeviceView.Columns.Add(StatusColumn);

                var ProgressColumn = new BrightIdeasSoftware.OLVColumn("Progress", "Progress");
                ProgressColumn.Width = 80;
                ProgressColumn.IsEditable = false;
                ProgressColumn.Renderer = new BarRenderer(0, 100);
                ProgressColumn.AspectGetter += new AspectGetterDelegate(ProgressBarUpdateAspectGetter);
                DeviceView.Columns.Add(ProgressColumn);
                /*
                var DeploymentColumn = new BrightIdeasSoftware.OLVColumn("Progress %", "Progress %");
                DeploymentColumn.Width = 80;
                DeploymentColumn.IsEditable = false;
                DeploymentColumn.TextAlign = HorizontalAlignment.Center;
                DeploymentColumn.AspectGetter += new AspectGetterDelegate(ProgressUpdateAspectGetter);
                DeviceView.Columns.Add(DeploymentColumn);
                */
                var AddressColumn = new BrightIdeasSoftware.OLVColumn("Address", "Address");
                AddressColumn.Width = 150;
                AddressColumn.IsEditable = true;
                AddressColumn.AspectGetter = x => ((x as Device != null ? (x as Device).Address : string.Empty));
                DeviceView.Columns.Add(AddressColumn);

                var RoleColumn = new BrightIdeasSoftware.OLVColumn("Role", "Role");
                RoleColumn.Width = 70;
                RoleColumn.IsEditable = true;
                RoleColumn.AspectGetter = x => ((x as Device != null ? (x as Device).Role : string.Empty));
                DeviceView.Columns.Add(RoleColumn);

                var NameColumn = new BrightIdeasSoftware.OLVColumn("Name", "Name");
                NameColumn.Width = 150;
                NameColumn.IsEditable = true;
                NameColumn.AspectGetter = x => ((x as Device != null ? (x as Device).Name : string.Empty));
                DeviceView.Columns.Add(NameColumn);

                var UserNameColumn = new BrightIdeasSoftware.OLVColumn("User Name", "User Name");
                UserNameColumn.Width = 80;
                UserNameColumn.IsEditable = true;
                UserNameColumn.AspectGetter = x => ((x as Device != null ? (x as Device).Username : string.Empty));
                DeviceView.Columns.Add(UserNameColumn);
                
                var PasswordColumn = new BrightIdeasSoftware.OLVColumn("Password", "Password");
                PasswordColumn.Width = 80;
                PasswordColumn.IsEditable = true;
                PasswordColumn.AspectGetter = x => (x as Device != null ? (x as Device).Password : string.Empty);
                DeviceView.Columns.Add(PasswordColumn);

                var CpuAffinityColumn = new BrightIdeasSoftware.OLVColumn("CPU Affinity", "CPU Affinity");
                CpuAffinityColumn.Width = 80;
                CpuAffinityColumn.IsEditable = true;
                CpuAffinityColumn.AspectGetter = x => (x as Device != null ? (x as Device).CpuAffinity : 0);
                DeviceView.Columns.Add(CpuAffinityColumn);

                var TargetPathColumn = new BrightIdeasSoftware.OLVColumn("Deployment Path", "Deployment Path");
                TargetPathColumn.Width = 200;
                TargetPathColumn.IsEditable = true;
                TargetPathColumn.AspectGetter = x => (x as Device != null ? (x as Device).DeploymentPath : string.Empty);
                DeviceView.Columns.Add(TargetPathColumn);

                var ArgumentColumn = new BrightIdeasSoftware.OLVColumn("Cmd Line Argument", "Cmd Line Argument");
                ArgumentColumn.Width = 200;
                ArgumentColumn.IsEditable = true;
                ArgumentColumn.AspectGetter = x => (x as Device != null ? (x as Device).CmdLineArguments : string.Empty);
                DeviceView.Columns.Add(ArgumentColumn);
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("FillBuildView Ex: {0}", ex.Message));
            }
        }

        public bool CanExpandBuildView(object Model)
        {
            if (Model as PlatformNode != null)
            {
                return (Model as PlatformNode).Children.Count > 0;
            }

            if (Model as ITargetDevice != null)
            {
                return (Model as ITargetDevice).Build != null;
            }

            return false;
        }

        public IEnumerable BuildViewChildrenGetter(object Model)
        {
            if (Model as PlatformNode != null)
            {
                return new ArrayList((Model as PlatformNode).Children);
            }

            if (Model as Device != null && (Model as Device).Build != null)
            {
                ArrayList Array = new ArrayList();
                Array.Add((Model as Device).Build);
                return Array;
            }

            return null;
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
            if (DeviceView.SelectedItem == null)
            {
                return;
            }

            var RightClickMenuItem = sender as MenuItem;

            try
            {
                var SelectedDevice = DeviceView.SelectedItem.RowObject as ITargetDevice;

                if (SelectedDevice == null)
                {
                    return;
                }

                string SelectedMenu = RightClickMenuItem.Text;

                if (SelectedMenu.Equals("Open Log"))
                {
                    OpenLogFile(SelectedDevice);
                    return;
                }

                var DeploySession = DeploySessions.Find(x => x.Device.Equals(SelectedDevice));

                if (SelectedMenu.Equals("Abort Deployment"))
                {
                    if (DeploySession != null)
                    {
                        DeploySession.Abort();
                    }
                    return;
                }

                if (SelectedMenu.Equals("Start Application"))
                {
                    SelectedDevice.StartProcess();
                }
                
                if (SelectedMenu.Equals("Stop Application"))
                {
                    SelectedDevice.StopProcess();
                }

                /*
                else
                {
                    if (SelectedDevice.Build.Status == null || !SelectedDevice.Status.Equals(BuildDeploymentResult.Success.ToString()))
                    {
                        MessageBox.Show(string.Format("Cannot open device information until build has been deployed successfully"), "", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        return;
                    }

                    var DeviceInfoForm = new DeviceForm();
                    DeviceInfoForm.DeviceConfig = new Device(SelectedDevice.UseDevice, SelectedDevice.Platform, SelectedDevice.Role, SelectedDevice.Name, SelectedDevice.Address, SelectedDevice.Username, SelectedDevice.Password, SelectedDevice.CpuAffinity, SelectedDevice.DeploymentPath, SelectedDevice.CmdLineArguments, SelectedDevice.Build);
                    var Result = DeviceInfoForm.ShowDialog();
                }
                */
            }
            catch (Exception Ex)
            {
                MessageBox.Show(string.Format("{0} error. {1}", RightClickMenuItem.Text, Ex.Message), "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenLogFile(ITargetDevice SelectedDevice)
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
        
        private void OnDeviceViewCellEditingStarting(object sender, CellEditEventArgs e)
        {
            try
            {
                SelectedDevice = e.RowObject as ITargetDevice;

                if (SelectedDevice == null)
                {
                    return;
                }

                Func<string, string> GetRole = PlatformName =>
                {
                    if (PlatformName == PlatformType.Linux.ToString())
                    {
                        return RoleType.Server.ToString();
                    }

                    return RoleType.Client.ToString();
                };

                foreach (var Platform in DeviceList)
                {
                    int index = Platform.Children.FindIndex(x => x == SelectedDevice);

                    if (Platform.Children.Count() - index <= 1)
                    {
                        Platform.Children.Add(DeviceFactory.CreateTargetDevice(false, Platform.Platform, GetRole(Platform.Platform), string.Empty, string.Empty, string.Empty, string.Empty, 0, string.Empty, string.Empty));

                        DeviceView.SetObjects(DeviceList);
                    }
                }

                if (e.Column.Text.Equals("Role") && SelectedDevice.Platform.Equals(PlatformType.Win64.ToString()))
                {
                    var RoleComboBox = new ComboBox { Bounds = e.CellBounds, DropDownStyle = ComboBoxStyle.DropDownList };
                    RoleComboBox.Validating += new CancelEventHandler(OnServerDeviceViewRoleComboBoxValidating);
                    RoleComboBox.Items.Add("Server");
                    RoleComboBox.Items.Add("Client");
                    RoleComboBox.Text = SelectedDevice.Role;
                    e.Control = RoleComboBox;
                }

                if (e.Column.Text.Equals("User Name"))
                {
                    var UserNameTextBox = new TextBox { Bounds = e.CellBounds };
                    UserNameTextBox.Text = SelectedDevice.Username;
                    UserNameTextBox.Validating += new CancelEventHandler(OnServerDeviceViewUserNameTextBoxValidating);
                    e.Control = UserNameTextBox;
                }

                if (e.Column.Text.Equals("Password"))
                {
                    var PasswordTextBox = new TextBox { Bounds = e.CellBounds };
                    PasswordTextBox.Text = SelectedDevice.Password;
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
                    DeploymentPathTextBox.Text = SelectedDevice.DeploymentPath;
                    DeploymentPathTextBox.Validating += new CancelEventHandler(OnServerDeviceViewTargetInstallPathTextBoxValidating);
                    e.Control = DeploymentPathTextBox;
                }

                if (e.Column.Text.Equals("Cmd Line Argument"))
                {
                    var CmdLineArgumentTextBox = new TextBox { Bounds = e.CellBounds };
                    CmdLineArgumentTextBox.Text = SelectedDevice.CmdLineArguments;
                    CmdLineArgumentTextBox.Validating += new CancelEventHandler(OnServerDeviceViewTargetCmdLineArgumentTextBoxValidating);
                    e.Control = CmdLineArgumentTextBox;
                }

                DeviceView.Refresh();
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Debug OnDeviceViewCellEditingStarting Ex: {0}", ex.Message));
            }
        }

        private void OnDeviceViewCpuAffinityNumericUpDownClicked(object sender, EventArgs e)
        {
            NumericUpDown CpuAffinityNumericUpDown = sender as NumericUpDown;
            if (CpuAffinityNumericUpDown != null && SelectedDevice != null)
            {
                CpuAffinityNumericUpDown.Value = SelectedDevice.CpuAffinity;
            }
        }

        private void OnDeviceViewCpuAffinityNumericUpDownValidating(object sender, EventArgs e)
        {
            NumericUpDown CpuAffinityNumericUpDown = sender as NumericUpDown;
            if (CpuAffinityNumericUpDown != null)
            {
                SelectedDevice.CpuAffinity = (int)CpuAffinityNumericUpDown.Value;
                DeviceView.RefreshObject(SelectedDevice);
            }
        }

        private CheckState UseServerDeviceAspectPutter(object Object, CheckState Value)
		{
            var SelectedPlatform = Object as PlatformNode;
            if (SelectedPlatform != null)
            {
                SelectedPlatform.UsePlatform = (Value == CheckState.Checked);

                foreach (var Device in SelectedPlatform.Children)
                {
                    if (Device.Address.Length == 0)
                    {
                        continue;
                    }

                    Device.UseDevice = SelectedPlatform.UsePlatform;
                }
            }

            var SelectedDevice = Object as ITargetDevice;
			if (SelectedDevice != null)
			{
				SelectedDevice.UseDevice = (Value == CheckState.Checked);
			}

			return Value;
		}

		private CheckState UseServerDeviceAspectGetter(object Object)
		{
            var SelectedPlatform = Object as PlatformNode;
            if (SelectedPlatform != null)
            {
                return SelectedPlatform.UsePlatform ? CheckState.Checked : CheckState.Unchecked;
            }

            SelectedDevice = Object as ITargetDevice;

			return (SelectedDevice != null && SelectedDevice.UseDevice) ? CheckState.Checked : CheckState.Unchecked;
		}

		private object ProgressBarUpdateAspectGetter(object Object)
		{
            if (Object == null || (Object as BuildNode) == null)
            {
                return null;
            }

            return CalculateProgressPercentage(Object as BuildNode);
		}

		private object ProgressUpdateAspectGetter(object Object)
		{
			return string.Format("{0} %", CalculateProgressPercentage(Object as BuildNode));
		}

		private int CalculateProgressPercentage(BuildNode SelectedBuild)
		{
			if (SelectedBuild == null || SelectedBuild.ProgressMax == 0 || SelectedBuild.Progress == 0)
			{
				return 0;
			}

			double Ratio = Convert.ToDouble(SelectedBuild.Progress) / Convert.ToDouble(SelectedBuild.ProgressMax);

			int ProgressPercentage = Convert.ToInt32(Ratio * 100);

			return ProgressPercentage;
		}

		private void OnServerDeviceViewUseDeviceCheckBoxValidating(object sender, EventArgs e)
		{	
			CheckBox UseDeviceCheckBox = sender as CheckBox;
			if (UseDeviceCheckBox != null)
			{
                SelectedDevice.UseDevice = UseDeviceCheckBox.Checked;
				DeviceView.RefreshObject(SelectedDevice);
			}
		}

		private void OnServerDeviceViewPlatformComboBoxValidating(object sender, EventArgs e)
		{
			ComboBox PlatformComboBox = sender as ComboBox;
			if (PlatformComboBox != null)
			{
                SelectedDevice.Platform = PlatformComboBox.Text;
                DeviceView.RefreshObject(SelectedDevice);
			}
		}

		private void OnServerDeviceViewRoleComboBoxValidating(object sender, EventArgs e)
		{
			ComboBox RoleComboBox = sender as ComboBox;
			if (RoleComboBox != null)
			{
                SelectedDevice.Role = RoleComboBox.Text;
                DeviceView.RefreshObject(SelectedDevice);
			}
		}

		private void OnServerDeviceViewUserNameTextBoxValidating(object sender, EventArgs e)
		{
			TextBox UserNameTextBox = sender as TextBox;
            if (UserNameTextBox != null)
            {
                SelectedDevice.Username = UserNameTextBox.Text;
                DeviceView.RefreshObject(SelectedDevice);
			}
		}

		private void OnServerDeviceViewPasswordTextBoxValidating(object sender, EventArgs e)
		{
			TextBox PasswordTextBox = sender as TextBox;
			if (PasswordTextBox != null)
			{
                SelectedDevice.Password = PasswordTextBox.Text;
                DeviceView.RefreshObject(SelectedDevice);
			}
		}

		private void OnServerDeviceViewTargetInstallPathTextBoxValidating(object sender, EventArgs e)
		{
			TextBox TargetInstallPathTextBox = sender as TextBox;
			if (TargetInstallPathTextBox != null)
			{
                SelectedDevice.DeploymentPath = TargetInstallPathTextBox.Text;
                DeviceView.RefreshObject(SelectedDevice);
			}
		}

		private void OnServerDeviceViewTargetCmdLineArgumentTextBoxValidating(object sender, EventArgs e)
		{
			TextBox CmdLineArgumentTextBox = sender as TextBox;
			if (CmdLineArgumentTextBox != null)
			{
                SelectedDevice.CmdLineArguments = CmdLineArgumentTextBox.Text;
                DeviceView.RefreshObject(SelectedDevice);
			}
		}

		private BuildSolutionNode CreateBuildSolutionNode(List<BuildRecord> Builds, SolutionType Solution, RoleType Role)
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

		private BuildPlatformNode CreatePlatformNode(PlatformType Platform, RoleType Role)
		{
			var PlatformBuilds = MongoDatabase.GetAvailableBuilds(Platform, Role);
			var DevBuildNode = CreateBuildSolutionNode(PlatformBuilds, SolutionType.Development, Role);
			var TestBuildNode = CreateBuildSolutionNode(PlatformBuilds, SolutionType.Test, Role);
			var ShippingBuildNode = CreateBuildSolutionNode(PlatformBuilds, SolutionType.Shipping, Role);

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

            string ProjectConfigFile = GetProjectConfigFile();
            if (!File.Exists(ProjectConfigFile))
            {
                MessageBox.Show(string.Format("Could not find any project config file {0}", ProjectConfigFile), "Missing Project Config File", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var ConfiguredProjects = new List<Project>();

            using (StreamReader Stream = new StreamReader(ProjectConfigFile))
            {
                ConfiguredProjects.AddRange(JsonConvert.DeserializeObject<List<Project>>(Stream.ReadToEnd()));
            }

            foreach (var ConfiguredProject in ConfiguredProjects)
            {
                BuildList.Add(CreateProjectNode(ConfiguredProject));
            }

            BuildView.Roots = BuildList;
			BuildView.Refresh();
        }

        private void LoadDeviceView()
        {
            DeviceList.Clear();
            DeviceView.ClearObjects();

            var LinuxNode = new PlatformNode("Linux");
            var WindowsNode = new PlatformNode("Win64");
            var PS4Node = new PlatformNode("PS4");
            
            var CurrentDirectory = Directory.GetCurrentDirectory();

            var ServerConfigFile = GetDeviceConfigFile();
            if (System.IO.File.Exists(ServerConfigFile))
            {
                var ConfigPlatformDevices = new List<PlatformConfig>();

                using (StreamReader Stream = new StreamReader(ServerConfigFile))
                {
                    ConfigPlatformDevices.AddRange(JsonConvert.DeserializeObject<List<PlatformConfig>>(Stream.ReadToEnd()));
                }

                foreach (var PlatformDeviceNode in ConfigPlatformDevices)
                {
                    foreach (var ConfigDevice in PlatformDeviceNode.Children)
                    {
                        var DeviceNode = DeviceFactory.CreateTargetDevice(ConfigDevice.UseDevice, ConfigDevice.Platform, ConfigDevice.Role, ConfigDevice.Name, ConfigDevice.Address, ConfigDevice.Username, ConfigDevice.Password, ConfigDevice.CpuAffinity, ConfigDevice.DeploymentPath, ConfigDevice.CmdLineArguments);

                        if (ConfigDevice.UseDevice)
                        {
                            DeviceView.CheckObject(DeviceNode);
                        }

                        if (ConfigDevice.Platform == PlatformType.Linux.ToString())
                        {
                            LinuxNode.Children.Add(DeviceNode);
                            continue;
                        }

                        if (ConfigDevice.Platform == PlatformType.Win64.ToString())
                        {
                            WindowsNode.Children.Add(DeviceNode);
                            continue;
                        }

                        if (ConfigDevice.Platform == PlatformType.PS4.ToString())
                        {
                            PS4Node.Children.Add(DeviceNode);
                            continue;
                        }
                    }
                }
            }

            LinuxNode.Children.Add(DeviceFactory.CreateTargetDevice(false, PlatformType.Linux.ToString(), RoleType.Server.ToString(), string.Empty, string.Empty, string.Empty, string.Empty, 0, string.Empty, string.Empty));
            DeviceList.Add(LinuxNode);

            WindowsNode.Children.Add(DeviceFactory.CreateTargetDevice(false, PlatformType.Win64.ToString(), string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, string.Empty, string.Empty));
            DeviceList.Add(WindowsNode);

            PS4Node.Children.Add(DeviceFactory.CreateTargetDevice(false, PlatformType.PS4.ToString(), RoleType.Client.ToString(), string.Empty, string.Empty, string.Empty, string.Empty, 0, string.Empty, string.Empty));
            DeviceList.Add(PS4Node);

            DeviceView.Roots = DeviceList;
            DeviceView.Refresh();
        }

        private ProjectNode CreateProjectNode(Project ConfigProject)
        {
            ProjectNode GameProjectNode = new ProjectNode(ConfigProject.DisplayName);

            GameProjectNode.Children.Add(CreateBuildMachineNode(ConfigProject.BuildMachine));
            GameProjectNode.Children.Add(CreateLocalHostNode());

            return GameProjectNode;
        }

        private BuildMachineNode CreateBuildMachineNode(string BuildMachineName)
        {
            BuildMachineNode BuildServerNode = new BuildMachineNode(BuildMachineName);

            BuildRoleNode ServerNode = new BuildRoleNode("Server");
            ServerNode.Children.Add(CreatePlatformNode(PlatformType.Linux, RoleType.Server));
            ServerNode.Children.Add(CreatePlatformNode(PlatformType.Win64, RoleType.Server));

            BuildRoleNode ClientNode = new BuildRoleNode("Client");
            ClientNode.Children.Add(CreatePlatformNode(PlatformType.Win64, RoleType.Client));
            ClientNode.Children.Add(CreatePlatformNode(PlatformType.XboxOne, RoleType.Client));
            ClientNode.Children.Add(CreatePlatformNode(PlatformType.PS4, RoleType.Client));

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

                List<string> PlatformSelected = new List<string>();
                
                foreach (var CheckedObject in DeviceView.CheckedObjects)
                {
                    var SelectedPlatform = CheckedObject as PlatformNode;

                    if (SelectedPlatform != null)
                    {
                        PlatformSelected.Add(SelectedPlatform.Platform);

                        foreach (var Device in SelectedPlatform.Children)
                        {
                            Deploy(Device, SelectedServerBuilds, SelectedClientBuilds);
                        }
                    }

                    var SelectedDevice = CheckedObject as ITargetDevice;

                    if (SelectedDevice != null && PlatformSelected.Find(x => x.Equals(SelectedDevice.Platform)) == null)
                    {
                        Deploy(SelectedDevice, SelectedServerBuilds, SelectedClientBuilds);
                    }
				}
            }
			catch(Exception e)
			{
				MessageBox.Show(string.Format("Deploy builds failed. {0}", e.Message), "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

        private void Deploy(ITargetDevice SelectedDevice, List<object> SelectedServerBuilds, List<object> SelectedClientBuilds)
        {
            if (SelectedDevice.UseDevice)
            {
                if (SelectedDevice.Role.Equals(RoleType.Server.ToString()))
                {
                    if (SelectedServerBuilds.Find(x => (x as BuildNode).Platform.Equals(SelectedDevice.Platform)) != null)
                    {
                        Deploy(SelectedDevice, SelectedServerBuilds);
                    }
                }
                else
                {
                    if (SelectedClientBuilds.Find(x => (x as BuildNode).Platform.Equals(SelectedDevice.Platform)) != null)
                    {
                        Deploy(SelectedDevice, SelectedClientBuilds);
                    }
                }
            }
        }

		private bool IsValidDeviceConfiguration()
		{
			var CheckedDevices = DeviceView.GetCheckedObjects().ToArray().ToList();

			foreach (var CheckedObject in DeviceView.CheckedObjects)
			{
				var SelectedDevice = CheckedObject as Device;

                if (SelectedDevice != null)
                {
                    var DevicesWithSameAddress = CheckedDevices.FindAll(x => (x as ITargetDevice) != null && (x as ITargetDevice).Platform.Equals(PlatformType.Win64.ToString()) && (x as ITargetDevice).Address.Equals(SelectedDevice.Address) && (x as ITargetDevice).DeploymentPath.Equals(SelectedDevice.DeploymentPath) && !(x as ITargetDevice).Role.Equals(SelectedDevice.Role));
                    if (DevicesWithSameAddress.Count() > 0)
                    {
                        MessageBox.Show(string.Format("The Win64 server and client with address {0} has the same deployment path, must be different", SelectedDevice.Address), "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Error);

                        return false;
                    }
                }
			}

			return true;
		}

		private void Deploy(ITargetDevice SelectedDevice, List<object> SelectedBuilds)
		{
			var SelectedBuild = SelectedBuilds.Find(x => (x as BuildNode).Platform.Equals(SelectedDevice.Platform)) as BuildNode;
			if (SelectedBuild != null)
			{
                SelectedDevice.Build = new BuildNode(SelectedBuild.UseBuild, SelectedBuild.Number, SelectedBuild.Timestamp, SelectedBuild.Path, SelectedBuild.Platform, SelectedBuild.Solution, SelectedBuild.Role, SelectedBuild.AutomatedTestStatus);
                SelectedDevice.ProjectConfig = GetProject(SelectedBuild);

                DeviceView.RefreshObjects(DeviceList);

                var Session = new DeploymentSession(this, this, SelectedDevice, DeviceView, DeviceList, SelectedDevice.ProjectConfig);
				DeploySessions.Add(Session);
				var Task = Session.Deploy(SelectedBuild);
			}
		}

        private Project GetProject(BuildNode SelectedBuild)
        {
            var SelectedProject = GetProjectNode(SelectedBuild);
            if (SelectedProject == null)
            {
                MessageBox.Show(string.Format("Could not find project node for build {0}", SelectedBuild.Number), "Missing Project", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            var ConfiguredProjects = new List<Project>();

            using (StreamReader Stream = new StreamReader(GetProjectConfigFile()))
            {
                ConfiguredProjects.AddRange(JsonConvert.DeserializeObject<List<Project>>(Stream.ReadToEnd()));
            }

            foreach (var ConfiguredProject in ConfiguredProjects)
            {
                if (ConfiguredProject.DisplayName == SelectedProject.Project)
                {
                    return ConfiguredProject;
                }
            }

            return null;
        }

        private ProjectNode GetProjectNode(BuildNode SelectedBuild)
        {
            // @HACK - Pretty Please OMFG please shoot me!!!

            foreach (var Project in BuildList)
            {
                foreach (var BuildMachine in Project.Children)
                {
                    foreach (var BuildRole in BuildMachine.Children)
                    {
                        foreach (var Platform in BuildRole.Children)
                        {
                            foreach (var BuildSolution in Platform.Children)
                            {
                                foreach(var Build in BuildSolution.Children)
                                {
                                    if (SelectedBuild == Build)
                                    {
                                        return Project;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

		private bool IsValidBuildSelection(List<object> SelectedServerBuilds, List<object> SelectedClientBuilds)
		{
			if (SelectedServerBuilds.Count == 0 && SelectedClientBuilds.Count == 0)
			{
				MessageBox.Show("No selected builds to deploy", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}

			// Check that we have not selected more than one build for the same platform and role.
			if (!IsValidBuildSelection(SelectedServerBuilds, PlatformType.Linux))
			{
				MessageBox.Show("More than one Linux server build selected.", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}

			if (!IsValidBuildSelection(SelectedServerBuilds, PlatformType.Win64))
			{
				MessageBox.Show("More than one Win64 server build selected.", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}

			if (!IsValidBuildSelection(SelectedClientBuilds, PlatformType.Win64))
			{
				MessageBox.Show("More than one Win64 client build selected.", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}

            if (!IsValidBuildSelection(SelectedClientBuilds, PlatformType.PS4))
            {
                MessageBox.Show("More than one PS4 build selected.", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (SelectedClientBuilds.ToList().FindAll(x => (x as BuildNode).Platform.Equals(PlatformType.XboxOne.ToString())).Count > 0)
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


		private bool IsValidBuildSelection(List<object> SelectedBuilds, PlatformType Platform)
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
                foreach (var Platform in DeviceList)
                {
                    Platform.Children.RemoveAll(x => x.Address.Length == 0);
                }

				string ConfigFile = GetDeviceSaveConfigFile();
				string JsonText = JsonConvert.SerializeObject(DeviceList);
				System.IO.File.WriteAllText(ConfigFile, JsonText);
			}
			catch(Exception e)
			{
				MessageBox.Show(string.Format("Failed to save devices {0}", e.Message), "Save Devices Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}

            foreach (var Platform in DeviceList)
            {
                Platform.Children.Add(DeviceFactory.CreateTargetDevice(false, Platform.Platform, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, string.Empty, string.Empty));
            }
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

        private string GetProjectConfigFile()
        {
            string CurrentDirectory = Directory.GetCurrentDirectory();
            return Path.Combine(CurrentDirectory, "ProjectConfig.json");
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

    // @Hack to be able to serialize json...
    public struct PlatformConfig
    {
        public string Platform { get; set; }
        public List<Device> Children { get; set; }
        public PlatformConfig(string Platform)
        {
            this.Platform = Platform;
            this.Children = new List<Device>();
        }
    };


    public class PlatformNode
    {
        public string Platform { get; set; }

        public bool UsePlatform { get; set; }

        public List<ITargetDevice> Children { get; set; }
        public PlatformNode(string Platform, bool UsePlatform = false)
        {
            this.Platform = Platform;
            this.UsePlatform = UsePlatform;
            this.Children = new List<ITargetDevice>();
        }
    }


    public class ProjectNode
    {
        public string Project { get; set; }

        public List<BuildMachineNode> Children { get; set; }
        public ProjectNode(string Project)
        {
            this.Project = Project;
            this.Children = new List<BuildMachineNode>();
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
        public int Progress { get; set; }
        public int ProgressMax { get; set; }
        public string Status { get; set; }

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
            this.Progress = 0;
            this.ProgressMax = 0;
            this.Status = string.Empty;
        }
	}


}
