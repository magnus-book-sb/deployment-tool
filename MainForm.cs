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
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeploymentTool
{
	public partial class MainForm : Form, IDeploymentCallback
	{
		private List<BuildRoleNode> BuildList = new List<BuildRoleNode>();

		private MongoDb MongoDatabase = null;

		private List<Device> ServerDeviceList = new List<Device>();

		private Device SelectedServerDevice = null;

		private List<DeploymentSession> DeploySessions = new List<DeploymentSession>();

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
			this.MongoDatabase = new MongoDb();

			LoadBuildView();

			LoadServerDeviceView();
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

				BuildView.Columns.Clear();
				BuildView.SmallImageList = BuildImageList;
				BuildView.CellEditActivation = BrightIdeasSoftware.TreeListView.CellEditActivateMode.SingleClickAlways;
				BuildView.FullRowSelect = true;
				BuildView.CheckBoxes = true;
				BuildView.CanExpandGetter = x => (x as BuildRoleNode != null) ? (x as BuildRoleNode).Children.Count > 0 : ((x as BuildPlatformNode != null) ? (x as BuildPlatformNode).Children.Count > 0 : ((x as BuildSolutionNode != null) ? (x as BuildSolutionNode).Children.Count > 0 : false));
				BuildView.ChildrenGetter = x => (x as BuildRoleNode != null) ? new ArrayList((x as BuildRoleNode).Children) : ((x as BuildPlatformNode != null) ? new ArrayList((x as BuildPlatformNode).Children) : ((x as BuildSolutionNode != null) ? new ArrayList((x as BuildSolutionNode).Children) : null));

				var BuildRoleColumn = new BrightIdeasSoftware.OLVColumn("Role", "Role");
				BuildRoleColumn.Width = 150;
				BuildRoleColumn.IsEditable = false;
				BuildRoleColumn.ImageGetter += new ImageGetterDelegate(ImageGetter);
				BuildRoleColumn.AspectGetter = x => (x as BuildRoleNode != null ? (x as BuildRoleNode).Role : ((x as BuildPlatformNode != null ? (x as BuildPlatformNode).Platform : ((x as BuildSolutionNode != null ? (x as BuildSolutionNode).Solution : string.Empty)))));
				BuildView.Columns.Add(BuildRoleColumn);
				
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

		private void CreateServerDeviceView()
		{
			ServerView.Columns.Clear();
			ServerView.OwnerDraw = true;
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
			PlatformColumn.Width = 100;
			PlatformColumn.AspectGetter = x => (x as Device).Platform;
			ServerView.Columns.Add(PlatformColumn);

			var RoleColumn = new BrightIdeasSoftware.OLVColumn("Role", "Role");
			RoleColumn.Width = 100;
			RoleColumn.AspectGetter = x => (x as Device).Role;
			ServerView.Columns.Add(RoleColumn);

			var AddressColumn = new BrightIdeasSoftware.OLVColumn("Address", "Address");
			AddressColumn.Width = 100;
			AddressColumn.AspectGetter = x => (x as Device).Address;
			ServerView.Columns.Add(AddressColumn);

			var NameColumn = new BrightIdeasSoftware.OLVColumn("Name", "Name");
			NameColumn.Width = 100;
			NameColumn.AspectGetter = x => (x as Device).Name;
			ServerView.Columns.Add(NameColumn);

			var UserNameColumn = new BrightIdeasSoftware.OLVColumn("User Name", "User Name");
			UserNameColumn.Width = 100;
			UserNameColumn.AspectGetter = x => (x as Device).Username;
			ServerView.Columns.Add(UserNameColumn);

			var PasswordColumn = new BrightIdeasSoftware.OLVColumn("Password", "Password");
			PasswordColumn.Width = 100;
			PasswordColumn.AspectGetter = x => (x as Device).Password;
			ServerView.Columns.Add(PasswordColumn);

			var TargetPathColumn = new BrightIdeasSoftware.OLVColumn("Deployment Path", "Deployment Path");
			TargetPathColumn.Width = 400;
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
					ServerDeviceList.Add(new Device(false, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty));
				}

				if (e.Column.Text.Equals("Use Device"))
				{
					var LogButton = new Button { Bounds = e.CellBounds };
					LogButton.Text = "...";
					LogButton.Click += new EventHandler(OnServerDeviceViewTargetLogButtonClickedEvent);
					e.Control = LogButton;
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

		private void OnServerDeviceViewTargetLogButtonClickedEvent(object sender, EventArgs e)
		{
			string LogFile = string.Empty;

			try
			{
				LogFile = FileLogger.GetLogFile(SelectedServerDevice);

				Process.Start(LogFile);
			}
			catch(Exception Ex)
			{
				MessageBox.Show(string.Format("Failed to open log file {0}. {1}", LogFile, Ex.Message), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
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
			
			BuildRoleNode ServerNode = new BuildRoleNode("Server");
			ServerNode.Children.Add(CreatePlatformNode(Platform.Linux, Role.Server));
			ServerNode.Children.Add(CreatePlatformNode(Platform.Win64, Role.Server));

			BuildRoleNode ClientNode = new BuildRoleNode("Client");
			ClientNode.Children.Add(CreatePlatformNode(Platform.Win64, Role.Client));
			ClientNode.Children.Add(CreatePlatformNode(Platform.XboxOne, Role.Client));
			ClientNode.Children.Add(CreatePlatformNode(Platform.PS4, Role.Client));

			BuildList.Add(ServerNode);
			BuildList.Add(ClientNode);

			BuildView.Roots = BuildList;
			BuildView.Refresh();
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
				var DeviceNode = new Device(ServerDevice.UseDevice, ServerDevice.Platform, ServerDevice.Role, ServerDevice.Name, ServerDevice.Address, ServerDevice.Username, ServerDevice.Password, ServerDevice.DeploymentPath, ServerDevice.CmdLineArguments);

				if (ServerDevice.UseDevice)
				{
					ServerView.CheckObject(DeviceNode);
				}

				ServerDeviceList.Add(DeviceNode);
			}

			ServerDeviceList.Add(new Device(false, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty));
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

			DeploymentBuilds();
		}

		private void DeploymentBuilds()
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
						Deploy(SelectedDevice, SelectedServerBuilds);
					}
					else
					{
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

			ServerDeviceList.Add(new Device(false, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty));
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

}
