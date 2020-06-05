﻿using ORTMAPILib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeploymentTool
{
    public class TargetDevicePS4 : Device
    {
        private ILogger Logger;
        private IDeploymentCallback Callback;
        private CancellationToken Token;
        private ORTMAPI TargetManager;
        private OrbisCtrl OrbisCtrlProc;
        private string MappedDirectory;

        public TargetDevicePS4(bool UseDevice, string Platform, string Role, string Name, string Address, string Username, string Password, int CpuAffinity, string DeploymentPath, string CmdLineArguments)
            : base(UseDevice, Platform, Role, Name, Address, Username, Password, CpuAffinity, DeploymentPath, CmdLineArguments)
        {
            this.Logger = new FileLogger(this);
            this.OrbisCtrlProc = null;
            this.TargetManager = new ORTMAPI();
            this.TargetManager.CheckCompatibility((uint)eCompatibleVersion.BuildVersion);
        }

        public override bool DeployBuild(IDeploymentCallback Callback, CancellationToken Token)
        {
            try
            {
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

                if (!InstallBuild())
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
            throw new NotImplementedException();
        }

        public override bool StopProcess()
        {
            throw new NotImplementedException();
        }

        private bool ResetProgress()
        {
            Logger.Info(string.Format("Start deploying build {0} to device {1}", Build.Number, Address));

            Build.Progress = 0;
            Build.Status = "";
            Build.ProgressMax = 4;

            return Ping();
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
}