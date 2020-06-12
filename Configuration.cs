using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeploymentTool
{
    public enum PlatformType { Linux, Win64, XboxOne, PS4 };

	public enum RoleType { Server, Client };

	public enum SolutionType { Development, Test, Shipping };

    public class Project
    {
        [JsonProperty(PropertyName = "display")]
        public string DisplayName { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "buildmachine")]
        public string BuildMachine { get; set; }

        public Project(string DisplayName, string Name, string BuildMachine)
        {
            this.DisplayName = DisplayName;
            this.Name = Name;
            this.BuildMachine = BuildMachine;
        }
    }

    public class Device : ITargetDevice
    {
        [JsonProperty(PropertyName = "usedevice")]
        public bool UseDevice { get; set; }

        [JsonProperty(PropertyName = "platform")]
        public string Platform { get; set; }

        [JsonProperty(PropertyName = "role")]
        public string Role { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }

        [JsonProperty(PropertyName = "username")]
        public string Username { get; set; }

        [JsonProperty(PropertyName = "password")]
        public string Password { get; set; }

        [JsonProperty(PropertyName = "cpuaffinity")]
        public int CpuAffinity { get; set; }

        [JsonProperty(PropertyName = "deploymentpath")]
        public string DeploymentPath { get; set; }

        [JsonProperty(PropertyName = "arguments")]
        public string CmdLineArguments { get; set; }

        [JsonIgnore]
        public string Status
        {
            get { return Ping() ? "Online" : "Offline"; }
        }

        [JsonIgnore]
        public BuildNode Build { get; set; }

        [JsonIgnore]
        public Project ProjectConfig { get; set; }

        public Device(bool UseDevice, string Platform, string Role, string Name, string Address, string Username, string Password, int CpuAffinity, string DeploymentPath, string CmdLineArguments)
		{
			this.UseDevice = UseDevice;
			this.Platform = string.IsNullOrEmpty(Platform) ? string.Empty : Platform.Trim();
			this.Role = string.IsNullOrEmpty(Role) ? string.Empty : Role.Trim();
			this.Name = string.IsNullOrEmpty(Name) ? string.Empty : Name.Trim();
			this.Address = string.IsNullOrEmpty(Address) ? string.Empty : Address.Trim();
			this.Username = string.IsNullOrEmpty(Username) ? string.Empty : Username.Trim();
			this.Password = string.IsNullOrEmpty(Password) ? string.Empty : Password.Trim();
            this.CpuAffinity = CpuAffinity;
            this.DeploymentPath = string.IsNullOrEmpty(DeploymentPath) ? string.Empty : DeploymentPath;
			this.CmdLineArguments = string.IsNullOrEmpty(CmdLineArguments) ? string.Empty : CmdLineArguments;
            this.Build = null;
            this.ProjectConfig = null;
		}

        public virtual bool Ping()
        {
            throw new NotImplementedException();
        }

        public virtual bool DeployBuild(BuildNode Build, IDeploymentSession Callback, CancellationToken Token)
        {
            throw new NotImplementedException();
        }

        public virtual bool IsProcessRunning()
        {
            throw new NotImplementedException();
        }

        public virtual bool StartProcess()
        {
            throw new NotImplementedException();
        }

        public virtual bool StopProcess()
        {
            throw new NotImplementedException();
        }
    }

    public static class IListHelper
    {
        public static List<T> FindAll<T>(IList Source, Func<T, bool> Pred)
        {
            List<T> FoundItems = new List<T>();

            foreach (var SourceItem in Source)
            {
                if (SourceItem is T)
                {
                    T Item = (T)SourceItem;

                    if (Item != null && Pred(Item))
                    {
                        FoundItems.Add(Item);
                    }
                }
            }

            return FoundItems;
        }
    }
}
