using BrightIdeasSoftware;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeploymentTool
{
	public class ThreadHelperClass
	{
        delegate void UpdateDeviceDeploymentProgressDelegate(Form form, ObjectListView ListView, Device DeviceConfig, List<Device> DeviceList);
		public static void UpdateDeviceDeploymentProgress(Form form, ObjectListView listView, Device deviceConfig, List<Device> deviceList)
		{
			if (form.InvokeRequired)
			{
				var d = new UpdateDeviceDeploymentProgressDelegate(UpdateDeviceDeploymentProgress);
				form.Invoke(d, new object[] { form, listView, deviceConfig, deviceList });
			}
			else
			{
				
				listView.SelectedObject = deviceConfig;
				deviceConfig.Progress++;
				listView.RefreshObject(deviceConfig);
			}
		}

		delegate void SetDeviceDeploymentResultDelegate(Form form, ObjectListView ListView, Device DeviceConfig, List<Device> DeviceList, BuildDeploymentResult Result);
		public static void SetDeviceDeploymentResult(Form form, ObjectListView listView, Device deviceConfig, List<Device> deviceList, BuildDeploymentResult result)
		{
			if (form.InvokeRequired)
			{
				var d = new SetDeviceDeploymentResultDelegate(SetDeviceDeploymentResult);
				form.Invoke(d, new object[] { form, listView, deviceConfig, deviceList, result });
			}
			else
			{
				deviceConfig.Status = result.ToString();
				listView.RefreshObject(deviceConfig);
			}
		}

		delegate void AddListViewItemCallback(Form f, ListView ctrl, ListViewItem Item);
		public static void AddListViewItem(Form WinForm, ListView Ctrl, ListViewItem Item)
		{
			if (Ctrl.InvokeRequired)
			{
				AddListViewItemCallback d = new AddListViewItemCallback(AddListViewItem);
				WinForm.Invoke(d, new object[] { WinForm, Ctrl, Item });
			}
			else
			{
				Ctrl.Items.Insert(0, Item);
			}
		}
	}
}
