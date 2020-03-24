using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeploymentTool
{
	public enum Platform { Linux, Win64, XboxOne, PS4 };

	public enum Role { Server, Client };

	public enum Solution { Development, Test, Shipping };

	public class Device
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

		[JsonProperty(PropertyName = "deploymentpath")]
		public string DeploymentPath { get; set; }

		[JsonProperty(PropertyName = "arguments")]
		public string CmdLineArguments { get; set; }

		[JsonIgnore]
		public string Status { get; set; }

		[JsonIgnore]
		public int Progress { get; set; }

		[JsonIgnore]
		public int ProgressMax { get; set; }

		public Device(bool UseDevice, string Platform, string Role, string Name, string Address, string Username, string Password, string DeploymentPath, string CmdLineArguments)
		{
			this.UseDevice = UseDevice;
			this.Platform = Platform.Trim();
			this.Role = Role.Trim();
			this.Name = Name.Trim();
			this.Address = Address.Trim();
			this.Username = Username.Trim();
			this.Password = Password.Trim();
			this.DeploymentPath = DeploymentPath;
			this.CmdLineArguments = CmdLineArguments;
			this.Progress = 0;
			this.ProgressMax = 0;
		}
	}
}
