﻿using BrightIdeasSoftware;
using ORTMAPILib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeploymentTool
{
    public class DeploymentSessionPS4 : IDeploymentSession
    {
        private MainForm WinForm;

        private TreeListView DeviceView;

        private IDeploymentCallback Callback;

        private Task<bool> DeploymentTask;

        private CancellationTokenSource CancellationTaskTokenSource;

        private Project ProjectConfig;

        private int DeployedDeviceCount;

        public List<ITargetDevice> Devices { get; private set; }

        public DeploymentSessionPS4(IDeploymentCallback Callback, MainForm WinForm, TreeListView DeviceView, List<ITargetDevice> Devices)
        {
            this.WinForm = WinForm;
            this.DeviceView = DeviceView;
            this.Callback = Callback;
            this.Devices = Devices;
            this.DeployedDeviceCount = 0;
        }

        public void OnFileDeployed(ITargetDevice Device, string SourceFile)
        {
            ThreadHelperClass.UpdateDeviceDeploymentProgress(WinForm, DeviceView, Device);
        }

        public void OnBuildDeployed(ITargetDevice Device, BuildNode Build)
        {
            ThreadHelperClass.SetDeviceDeploymentResult(WinForm, DeviceView, Device, BuildDeploymentResult.Success);

            DeployedDeviceCount++;

            if (Devices.Count == DeployedDeviceCount)
            {
                Callback.OnDeploymentDone(this);
            }
        }

        public void OnBuildDeployedError(ITargetDevice Device, BuildNode Build, string ErrorMessage)
        {
            ThreadHelperClass.SetDeviceDeploymentResult(WinForm, DeviceView, Device, BuildDeploymentResult.Failure);

            DeployedDeviceCount++;

            if (Devices.Count == DeployedDeviceCount)
            {
                Callback.OnDeploymentDone(this);
            }
        }

        public void OnBuildDeployedAborted(ITargetDevice Device, BuildNode Build)
        {
            ThreadHelperClass.SetDeviceDeploymentResult(WinForm, DeviceView, Device, BuildDeploymentResult.Aborted);

            DeployedDeviceCount++;

            if (Devices.Count == DeployedDeviceCount)
            {
                Callback.OnDeploymentDone(this);
            }
        }

        public void Abort()
        {
            MessageBox.Show("Cancel deployment not supported for PS4 device", "Abort Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public async Task<bool> Deploy(BuildNode InBuild)
        {
            DeployedDeviceCount = 0;
            CancellationTaskTokenSource = new CancellationTokenSource();

            DeploymentTask = Task.Run(() => DeployBuild(InBuild, this, CancellationTaskTokenSource.Token), CancellationTaskTokenSource.Token);

            await DeploymentTask;

            return DeploymentTask.Result;
        }

        private bool DeployBuild(BuildNode InBuild, IDeploymentSession Callback, CancellationToken Token)
        {
            foreach (var Device in Devices)
            {
                var Build = new BuildNode(InBuild.UseBuild, InBuild.Number, InBuild.Timestamp, InBuild.Path, InBuild.Platform, InBuild.Solution, InBuild.Role, InBuild.AutomatedTestStatus);

                Device.Build = Build;
                Build.Status = "Waiting for Device";

                ThreadHelperClass.DeployBuild(WinForm, DeviceView, Device);
            }

            foreach (var Device in Devices)
            {
                Device.ProjectConfig = WinForm.GetProject(Device.Build);
                Device.DeployBuild(Device.Build, Callback, Token);
            }

            return true;
        }
    }

    public class TargetDevicePS4 : Device
    {
        private ILogger Logger;
        private IDeploymentSession Callback;
        private CancellationToken Token;
        private ORTMAPI TargetManager;
        private OrbisCtrl OrbisCtrlProc;
        private string MappedDirectory;
        private uint ProcessID = 0;


        public TargetDevicePS4(bool UseDevice, string Platform, string Role, string Name, string Address, string Username, string Password, int CpuAffinity, string DeploymentPath, string CmdLineArguments)
            : base(UseDevice, Platform, Role, Name, Address, Username, Password, CpuAffinity, DeploymentPath, CmdLineArguments)
        {
            this.Logger = new FileLogger(this);
            this.OrbisCtrlProc = null;
            this.TargetManager = new ORTMAPI();
            this.TargetManager.CheckCompatibility((uint)eCompatibleVersion.BuildVersion);
        }

        public override bool DeployBuild(BuildNode Build, IDeploymentSession Callback, CancellationToken Token)
        {
            try
            {
                this.Build = Build;
                this.Callback = Callback;
                this.Token = Token;

                OrbisCtrlProc = new OrbisCtrl(this, Logger, Callback, Token);

                if (!ResetProgress())
                {
                    return CheckCancelationRequestAndReport();
                }

                Build.Progress++;

                // Add PS4 to Neighborhood.
                ITarget Target = TargetManager.AddTarget(Address);
                
                SetMappedDirectory(Target);

                LogDeviceDetails(Target);

                Build.Progress++;

                if (Target.PowerStatus != ePowerStatus.POWER_STATUS_ON)
                {
                    Target.PowerOn();
                }
                
                if (!OrbisCtrlProc.Execute("pkill"))
                {
                    return false;
                }

                int ExitCode = 0;
                while (!OrbisCtrlProc.HasExited(out ExitCode))
                {
                    if (Token.IsCancellationRequested)
                    {
                        OrbisCtrlProc.Kill();

                        Callback.OnBuildDeployedAborted(this, Build);

                        return false;
                    }

                    Logger.Info(string.Format("Waiting for PS4 process to exit on PS4 device {0}", Address));
                }
                
                Build.Progress++;

                if (!InstallPackage())
                {
                    return CheckCancelationRequestAndReport();
                }
                
                Build.Progress++;

                Callback.OnBuildDeployed(this, Build);
            }
            catch(Exception e)
            {
                Callback.OnBuildDeployedError(this, Build, e.Message);

                Logger.Error(string.Format("Failed to deploy build to PS4 device '{0}'. Ex: {1}", Address, e.Message));
            }
            return false;
        }

        public override bool IsProcessRunning()
        {
            throw new NotImplementedException();
        }

        public override bool Ping()
        {
            return NetworkHelper.PingDevice(Address, Logger);
        }

        public override bool StartProcess()
        {
            try
            {
                string Executable = Path.Combine(DeploymentPath, ProjectConfig.Name, "binaries", "ps4", string.Format("{0}.self", ProjectConfig.Name)).ToLower();

                if (!File.Exists(Executable))
                {
                    Logger.Error(string.Format("Failed to launch application on PS4 device '{0}'. No executable found.", Address));
                    return false;
                }

                Array Targets = TargetManager.GetTargetsByHost(Address) as Array;
                if (Targets == null || Targets.Length == 0)
                {
                    Logger.Error(string.Format("Failed to launch application on PS4 device '{0}'. Get targets by host failed", Address));
                    return false;
                }

                ITarget TargetDevice = Targets.GetValue(0) as ITarget;
                if (TargetDevice == null)
                {
                    Logger.Error(string.Format("Failed to launch application on PS4 device '{0}'. Get target device failed", Address));
                    return false;
                }

                if (TargetDevice.PowerStatus != ePowerStatus.POWER_STATUS_ON)
                {
                    TargetDevice.PowerOn();
                }

                TargetDevice.Connect();

                Array Processes = (Array)TargetDevice.ProcessInfoSnapshot;



                IProcess Process = TargetDevice.LoadProcess(eDevice.DEVICE_RAW, string.Empty, (uint)eLoadOptions.LOAD_OPTIONS_DEFAULT, 0, CmdLineArguments, @"O:\192.168.1.181\NPXX53530"); // DeploymentPath);

                ProcessID = Process.Id;

                if (ProcessID == 0)
                {
                    Logger.Error(string.Format("Failed to launch application on PS4 device '{0}'. Process id is 0", Address));
                    return false;
                }

                new Task(() => AsyncMonitorProcess(TargetDevice, Process)).Start();

                return true;
            }
            catch(Exception e)
            {
                Logger.Error(string.Format("Failed to launch application on PS4 device '{0}'. Ex: {1}", Address, e.Message));
            }

            return false;
        }

        private void AsyncMonitorProcess(ITarget TargetDevice, IProcess Process)
        {
            PS4Process ProcessHandler = new PS4Process(TargetDevice, Process, Logger);

            ProcessHandler.WaitForExit();
        }


        public override bool StopProcess()
        {
            if (ProcessID == 0)
            {
                return true;
            }

            if (!OrbisCtrlProc.Execute(string.Format("pkill {0}", ProcessID)))
            {
                return false;
            }

            int ExitCode = 0;
            while (!OrbisCtrlProc.HasExited(out ExitCode))
            {
                if (Token.IsCancellationRequested)
                {
                    OrbisCtrlProc.Kill();

                    Callback.OnBuildDeployedAborted(this, Build);

                    return false;
                }

                Logger.Info(string.Format("Waiting for PS4 process to exit on PS4 device {0}", Address));
            }

            return true;
        }

        private ITarget GetTarget(ORTMAPI TM, string TargetString)
        {
            ITarget Target = null;

            if (String.IsNullOrEmpty(TargetString) || TargetString.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                // Use default
                Target = TM.DefaultTarget;
            }
            else
            {
                // Find target
                Array Targets = (Array)TM.GetTargetsByName(TargetString);
                if (Targets.Length == 0)
                {
                    Targets = (Array)TM.GetTargetsByHost(TargetString);
                }

                if (Targets.Length != 0)
                {
                    Target = (ITarget)Targets.GetValue(0);
                }

                if (Target == null)
                {
                    Console.WriteLine("No target " + TargetString);
                }
            }

            return Target;
        }

        private bool ResetProgress()
        {
            Logger.Info(string.Format("Start deploying build {0} to device {1}", Build.Number, Address));

            Build.Progress = 0;
            Build.Status = "";
            Build.ProgressMax = 5;

            return Ping();
        }

        private bool InstallPackage()
        {
            try
            {
                if (Token.IsCancellationRequested)
                {
                    return false;
                }

                Array Targets = TargetManager.GetTargetsByHost(Address);
                if (Targets == null)
                {
                    Logger.Error(string.Format("Failed to install package to PS4 device {0}. Could not get targets by ORBIS", Address));
                    return false;
                }

                ITarget Target = (ITarget)Targets.GetValue(0);
                if (Target == null)
                {
                    Logger.Error(string.Format("Failed to install package to PS4 device {0}. Could not get target by ORBIS", Address));
                    return false;
                }

                Target.Connect();

                Build.Status = "Installing Build";

                // e.g O:\KitName\Data\Sandbox\Project\Saved
                string ArtifactPath = Path.Combine(DeploymentPath, ProjectConfig.Name, @"Saved").ToLower();

                // Remove any existing saved directory
                if (Directory.Exists(ArtifactPath))
                {
                    Directory.Delete(ArtifactPath, true);
                }

                string BuildPath = Path.Combine(Build.Path, "ArchivedBuilds");

                if (!Directory.Exists(BuildPath))
                {
                    Logger.Error(string.Format("Failed to install package to PS4 device {0}. Could not find ArchivedBuilds folder in path {1}", Address, Build.Path));
                    return false;
                }

                string[] PkgFiles = Directory.GetFiles(BuildPath, "*.pkg", SearchOption.AllDirectories);
                if (PkgFiles.Length == 0)
                {
                    Logger.Error(string.Format("Failed to install package to PS4 device {0}. Could not find .Pkg files in path {1}", Address, BuildPath));
                    return false;
                }

                Callback.OnFileDeployed(this, "");

                foreach (var PkgFile in PkgFiles)
                {
                    Target.InstallPackage(PkgFile, null);
                }

                return true;
            }
            catch (Exception e)
            {
                Logger.Error(string.Format("Failed to install package to PS4 device {0}. Error: {1}", Address, e.Message));
            }

            return false;
        }

        private bool InstallBuild()
        {
            try
            {
                if (Token.IsCancellationRequested)
                {
                    return false;
                }

                Build.Status = "Installing Build";
                
                // e.g O:\KitName\Data\Sandbox\Project\Saved
                string ArtifactPath = Path.Combine(DeploymentPath, ProjectConfig.Name, @"Saved").ToLower();

                // Remove any existing saved directory
                if (Directory.Exists(ArtifactPath))
                {
                    Directory.Delete(ArtifactPath, true);
                }

                string BuildPath = Path.Combine(Build.Path, "StagedBuilds"); // contains both a StagedBuild and a archive folder for some reason....

                DirectoryInfo SourceDir = new DirectoryInfo(BuildPath);
                System.IO.FileInfo[] SourceFiles = SourceDir.GetFiles("*", SearchOption.AllDirectories);

                // Transform any path/file to lower case otherwise PS4 will not accept installed build.
                foreach (FileInfo SourceInfo in SourceFiles)
                {
                    string SourceFilePath = SourceInfo.FullName.Replace(SourceDir.FullName, "");

                    // Remove leading separator
                    if (SourceFilePath.First() == Path.DirectorySeparatorChar)
                    {
                        SourceFilePath = SourceFilePath.Substring(1);
                    }

                    string Transformed = TransformPathForPS4(SourceFilePath);

                    if (Transformed != SourceFilePath)
                    {
                        if (Transformed.Contains(@"\"))
                        {
                            string Path = SourceInfo.Directory.FullName.Substring(0, SourceInfo.FullName.IndexOf(Transformed, StringComparison.OrdinalIgnoreCase));

                            var RenamePathsFile = Transformed.Split('\\').Select(x => { return x; }).ToList();

                            RenameLowerCase(new DirectoryInfo(Path), RenamePathsFile);
                        }
                        else
                        {
                            System.IO.File.Move(SourceInfo.FullName, System.IO.Path.Combine(SourceInfo.DirectoryName, Transformed));
                        }

                    }
                }

                if (!OrbisCtrlProc.Execute("dcopy enable"))
                {
                    return false;
                }

                int ExitCode = 0;
                while (!OrbisCtrlProc.HasExited(out ExitCode))
                {
                    if (Token.IsCancellationRequested)
                    {
                        OrbisCtrlProc.Kill();

                        Callback.OnBuildDeployedAborted(this, Build);

                        return false;
                    }

                    Logger.Info(string.Format("Enabling dcopy on PS4 device {0}", Address));
                }

                string PS4LocalPath = Path.Combine("/data", ProjectConfig.Name).Replace('\\', '/').ToLower();
                // fast, mirroring copy, only transfers what has changed
                return DistributedCopy(OrbisCtrlProc, Address, BuildPath, PS4LocalPath);
            }
            catch (Exception e)
            {
                Logger.Error(string.Format("Failed to install build '{0}' to target device '{1}'. Ex: {2}", Build.Number, Address, e.Message));
            }

            return false;
        }

        private bool DistributedCopy(OrbisCtrl OrbisCtrlProc,string Hostname, string SourcePath, string DestPath, int RetryCount = 5) //, Utils.SystemHelpers.CopyOptions Options = Utils.SystemHelpers.CopyOptions.Copy, int RetryCount = 5)
        {
            bool IsDirectory = false;

            if (Directory.Exists(SourcePath))
            {
                IsDirectory = true;
            }

            if (!File.Exists(SourcePath) && !IsDirectory)
            {
                return false;
            }

            // Setup dcopy arguments
            List<string> CopyArgs = new List<string>() { "dcopy" };

            // Source path
            CopyArgs.Add(string.Format("\"{0}\"", SourcePath.Trim(new char[] { ' ', '"' })));

            // Destination path
            CopyArgs.Add(string.Format("\"{0}\"", DestPath));

            // Directory options
            if (IsDirectory)
            {
                CopyArgs.Add("/recursive");

                //if ((Options & Utils.SystemHelpers.CopyOptions.Mirror) == Utils.SystemHelpers.CopyOptions.Mirror)
                {
                    CopyArgs.Add("/mirror");
                }
            }

            if (RetryCount > 0)
            {
                CopyArgs.Add(String.Format("/retry-count:{0}", Math.Min(RetryCount, 5)));
            }

            // This forces copy whether source and dest are reported matching or not...
            // (enable if issues with src/dst matches are being incorrectly identified
            // CopyArgs.Add("/force");

            // @todo: support device copy for same build to multiple devices
            CopyArgs.Add(Hostname);

            // @todo: we could generate a copy time estimate based on file sizes and gigabit copy or add option
            // This is currently set to 45 minutes due to current network issue, should be more like 15 minutes
            //const int WaitTimeMinutes = 45 * 60;

            // don't warn on dcopy timeouts, these are generally due to slow network conditions and are not actionable
            // for other failures, an exception is raised
            if (!OrbisCtrlProc.Execute(String.Join(" ", CopyArgs)))
            {
                return false;
            }

            int ExitCode = 0;
            while (!OrbisCtrlProc.HasExited(out ExitCode))
            {
                if (Token.IsCancellationRequested)
                {
                    OrbisCtrlProc.Kill();

                    Callback.OnBuildDeployedAborted(this, Build);

                    return false;
                }

                //Callback.OnFileDeployed(this, "Dummy");

                Logger.Info(string.Format("Copying files to PS4 device {0}", Address));

                Thread.Sleep(1000);
            }

            return true;
        }

        private string TransformPathForPS4(string Path)
        {
            if (Path.StartsWith("CUSA", StringComparison.OrdinalIgnoreCase) || Path.IndexOf("sce_", StringComparison.OrdinalIgnoreCase) != -1)
            {
                return Path;
            }

            return Path.ToLower();
        }

        private void RenameLowerCase(DirectoryInfo DirInfo, List<string> RenamePathsFile)
        {
            Func<DirectoryInfo, DirectoryInfo> RenameDirectory = Info =>
            {
                bool IsOnlyLower = Info.Name.Count(c => Char.IsLower(c)) == Info.Name.Length;
                if (IsOnlyLower)
                {
                    return Info;
                }

                string StrLower = Info.FullName.ToLower();
                string StrTmpName = Info.FullName + "_temp";
                Info.MoveTo(StrTmpName);
                Info = new DirectoryInfo(StrTmpName);
                Info.MoveTo(StrLower);
                return new DirectoryInfo(StrLower);
            };

            Func<FileInfo, FileInfo> RenameFile = Info =>
            {
                string strLower = Info.FullName.ToLower();
                string strTmpName = Info.FullName + "_temp";
                Info.MoveTo(strTmpName);
                Info = new FileInfo(strTmpName);
                Info.MoveTo(strLower);
                return new FileInfo(strLower);
            };

            foreach (var Dirs in DirInfo.GetDirectories())
            {
                if (RenamePathsFile.Contains(Dirs.Name.ToLower()))
                {
                    RenameLowerCase(RenameDirectory(Dirs), RenamePathsFile);
                }
            }

            foreach (var Files in DirInfo.GetFiles())
            {
                if (RenamePathsFile.Contains(Files.Name.ToLower()))
                {
                    RenameFile(Files);
                }
            }
        }

        private bool CheckCancelationRequestAndReport()
        {
            if (Token.IsCancellationRequested)
            {
                Logger.Warning(string.Format("User aborted deployment of build {0} on PS4 device {1}", Build.Number, Address));

                Callback.OnBuildDeployedAborted(this, Build);

                return false;
            }

            Callback.OnBuildDeployedError(this, Build, "Deploy build failed, see logs for more information");

            return false;
        }

        private void LogDeviceDetails(ITarget Target)
        {
            Logger.Info(string.Format("Name=\"{0}\"", Target.CachedName));
            Logger.Info(string.Format("HostName={0}", Target.HostName));
            Logger.Info(string.Format("Default={0}", Target.Default));
            Logger.Info(string.Format("PowerStatus={0}", Target.PowerStatus));
            Logger.Info(string.Format("ConnectionState={0}", Target.ConnectionState));
            Logger.Info(string.Format("MappedDir={0}", MappedDirectory));

            // This is the only property that requires power to query
            if (Target.PowerStatus == ePowerStatus.POWER_STATUS_ON)
            {
                uint Major = 0, Minor = 0, Build = 0;
                Target.SDKVersion(out Major, out Minor, out Build);

                Logger.Info(string.Format("SDKVersion={0:x}.{1:x3}.{2:x3}", Major, Minor, Build));
            }
        }

        private void SetMappedDirectory(ITarget Target)
        {
            uint Flags = 0;
            sbyte DriveLetter = TargetManager.GetPFSDrive(out Flags);
            switch (DriveLetter)
            {
                case 0:
                case -1:
                default:
                    {
                        string Drive = string.Format(@"{0}:\", ((char)DriveLetter).ToString());
                        if (Directory.Exists(Path.Combine(Drive, Target.Name)))
                        {
                            MappedDirectory = Path.Combine(Drive, Target.Name, "data").ToLower();
                        }
                        else if (Directory.Exists(Path.Combine(Drive, Target.HostName)))
                        {
                            MappedDirectory = Path.Combine(Drive, Target.HostName, "data").ToLower();
                        }
                        else
                        {
                            throw new Exception(string.Format("Failed to deploy PS4 build no mapped directory for device '{0}'", Address));
                        }

                        DeploymentPath = Path.Combine(MappedDirectory, ProjectConfig.Name).ToLower();
                    }
                    break;
            }
        }
    }



    public class PS4Process : IEventConsoleOutput, IEventTarget, IEventDebug
    {
        public enum eProcessResult
        {
            Success = 0,
            ErrorGeneric = -100,
            ErrorProcessKilled,
            ErrorShutdown,
            ErrorDisconnected,
            ErrorForceDisconnected,
            ErrorLoadExecFail,
            ErrorLoadExecTimeout,

        }

        private ILogger Logger;

        ManualResetEvent ProcessExitEvent = new ManualResetEvent(false);

        readonly ITarget Target = null;

        uint ProcessId = 0;

        public eProcessResult Result { get; protected set; }

        public string ResultString { get; protected set; }

        public int ExitCode { get; protected set; }

        protected bool bSawFatalExit;

        public PS4Process(ITarget InTarget, IProcess Process, ILogger InLogger)
        {
            this.Target = InTarget;
            this.ProcessId = Process.Id;
            this.Logger = InLogger;
            Target.AdviseDebugEvents(this);
            Target.AdviseTargetEvents(this);
            Target.AdviseConsoleOutputEvents(this);
            ExitCode = -1;
        }

        public void Unregister()
        {
            try
            {
                Target.UnadviseDebugEvents(this);
            }
            catch
            { }

            try
            {
                Target.UnadviseTargetEvents(this);
            }
            catch
            { }

            try
            {
                Target.UnadviseConsoleOutputEvents(this);
            }
            catch
            { }
        }

        public void WaitForExit()
        {
            while (!ProcessExitEvent.WaitOne(0, false))
            {
                Thread.CurrentThread.Join(50);
            }
        }

        public void OnProcessExit(IProcessExitEvent pEvent)
        {
            if (ProcessId == pEvent.ProcessId)
            {
                ExitCode = (int)pEvent.ExitCode;
                SetDone(eProcessResult.Success, "Process Exited");
            }
        }

        public void OnProcessKill(IProcessKillEvent pEvent)
        {
            if (ProcessId == pEvent.ProcessId)
            {
                // Differentiate between the user closing the app,
                // and the app crashing and being killed by the system.
                if (bSawFatalExit)
                {
                    ExitCode = -1;
                    SetDone(eProcessResult.ErrorProcessKilled, "Process was Killed");
                }
                else
                {
                    SetDone(eProcessResult.Success, "Process was Killed");
                    ExitCode = 0;
                }
            }
        }

        public void OnPowerState(IPowerStateEvent pEvent)
        {
            if (pEvent.Operation == ePowerOperation.POWEROP_SHUTDOWN)
            {
                if (pEvent.Progress == ePowerProgress.POWER_OP_STATUS_COMPLETED)
                {
                    SetDone(eProcessResult.ErrorShutdown, "Powerdown Occurred");
                }
            }
        }

        public void OnDisconnect(IDisconnectEvent pEvent)
        {
            SetDone(eProcessResult.ErrorDisconnected, "Disconnect Occurred");
        }

        public void OnForceDisconnected(IForceDisconnectedEvent pEvent)
        {
            SetDone(eProcessResult.ErrorForceDisconnected, "Force Disconnect Occurred");
        }

        public void OnLoadExecFail(ILoadExecFailEvent pEvent)
        {
            if (this.ProcessId == pEvent.Process.Id)
            {
                SetDone(eProcessResult.ErrorLoadExecFail, "LoadExec Fail");
            }
        }

        public void OnLoadExecTimeout(ILoadExecTimeoutEvent pEvent)
        {
            if (this.ProcessId == pEvent.Process.Id)
            {
                SetDone(eProcessResult.ErrorLoadExecTimeout, "LoadExec Timeout");
            }
        }

        protected void SetDone(eProcessResult InResult, string InReason)
        {
            Result = InResult;
            ResultString = InReason;
            ProcessExitEvent.Set();
        }

        public void OnCoredumpCompleted(ICoredumpCompletedEvent pEvent)
        { }

        public void OnCoredumpInProgress(ICoredumpInProgressEvent pEvent)
        { }

        public void OnDynamicLibraryLoad(IDynamicLibraryLoadEvent pEvent)
        { }
        public void OnDynamicLibraryUnload(IDynamicLibraryUnloadEvent pEvent)
        { }
        public void OnProcessCreate(IProcessCreateEvent pEvent)
        { }
        public void OnProcessLoading(IProcessLoadingEvent pEvent)
        { }
        public void OnStopNotification(IStopNotificationEvent pEvent)
        { }
        public void OnThreadCreate(IThreadCreateEvent pEvent)
        { }
        public void OnThreadExit(IThreadExitEvent pEvent)
        { }

        public void OnBusy(IBusyEvent pEvent)
        { }
        public void OnConnect(IConnectEvent pEvent)
        { }
        public void OnConnected(IConnectedEvent pEvent)
        { }
        public void OnExpiryTime(IExpiryTimeEvent pEvent)
        { }
        public void OnFileServingCaseSensitivityChanged(IFileServingCaseSensitivityChangedEvent pEvent)
        { }
        public void OnFileServingRootChanged(IFileServingRootChangedEvent pEvent)
        { }
        public void OnForcedPowerOff(IForcedPowerOffEvent pEvent)
        { }
        public void OnIdle(IIdleEvent pEvent)
        { }
        public void OnMultiPhaseProgress(IMultiPhaseProgressEvent pEvent)
        { }
        public void OnMultiPhaseProgressError(IMultiPhaseProgressErrorEvent pEvent)
        { }
        public void OnNameUpdate(INameUpdateEvent pEvent)
        { }
        public void OnProgress(IProgressEvent pEvent)
        { }
        public void OnSettingsChanged(ISettingsChangedEvent pEvent)
        { }
        public void OnUpdateError(IUpdateErrorEvent pEvent)
        { }
        public void OnUpdateProgress(IUpdateProgressEvent pEvent)
        { }


        public void OnConsoleOutput(IConsoleOutputEvent pEvent)
        {
            if (pEvent.Category == eConsoleOutputCategory.PROCESS_OUTPUT)
            {
                if (pEvent.ProcessId == ProcessId)
                {
                    if (pEvent.Port == eConsoleOutputPort.STREAM_STDIO)
                    {
                        Console.Out.Write(pEvent.Text);
                    }
                    else if (pEvent.Port == eConsoleOutputPort.STREAM_STDERR)
                    {
                        Console.Out.Write("STDERR:" + pEvent.Text);
                    }
                }
            }
            else if (pEvent.Category == eConsoleOutputCategory.KERNEL_OUTPUT || pEvent.Category == eConsoleOutputCategory.CP_KERNEL_OUTPUT)
            {
                if (Regex.IsMatch(pEvent.Text, @"\(SIG.+\)") || pEvent.Text.Contains("abort"))
                {
                    bSawFatalExit = true;
                }
            }
        }

        public void OnBufferReady(IBufferReadyEvent pEvent)
        {
        }
    }
}
