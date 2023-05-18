using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SDK_LIB;

namespace QPOSDesktopDemo
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            NotifyUser(promoteUserToStart, NotifyType.StatusMessage);
            bleScanRunning = false;
           
            DeviceInfo = new Dictionary<string, string>();
            SearchDevice();

            ButtonConnectToUSBDevice.IsEnabled = false;
            ButtonDisconnectFromDevice.IsEnabled = false;
            ButtonConnectToBTDevice.IsEnabled = false;
            pairBluetooth.IsEnabled = false;
            scanBLE.IsEnabled = false;
            Current = this;
            CreateUSBWatcher();
            
        }

        private void scanSerial_Click(object sender, RoutedEventArgs e)
        {
            Tip.d("scanSerial_Click");
            DeviceInfo.Clear();
            SearchDevice();

           
            List<DeviceInformation> items = new List<DeviceInformation>();
            items.Add(new DeviceInformation() { Name=device.Name, DeviceType=device.DeviceType});
            CollectionViewSource DeviceListSource = (CollectionViewSource)Current.Resources["DeviceListSource"];
            DeviceListSource.Source = items;
            bleScanRunning = false;
        }

        private void scanBLE_Click(object sender, RoutedEventArgs e)
        {

        }

        private void pairBluetooth_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonConnectToBTDevice_Click(object sender, RoutedEventArgs e)
        {

        }
       
        private  void ButtonConnectToUSBDevice_Click(object sender, RoutedEventArgs e)
        {
            Tip.d("ButtonConnectToUSBDevice_Click");
            NotifyUser(connectionInProgress, NotifyType.ProcessingMessage);
            var selection = ConnectDevices.SelectedItems;

            DeviceInformation entry = null;
            var obj = selection[0];
            entry = (DeviceInformation)obj;
            System.Diagnostics.Debug.WriteLine("button clicked !!***********************************");
            if (entry != null)
            {
                String port = DeviceInfo["Device_port"];
                bool openSuccess = OpenDeviceAsync(port);
                if (openSuccess)
                {
                    Console.WriteLine("             thread: " + System.Threading.Thread.CurrentThread.ManagedThreadId);
                    ButtonConnectToUSBDevice.IsEnabled = false;
                    ButtonDisconnectFromDevice.IsEnabled = true;
                }
            }

        }

        private void ButtonDisconnectFromDevice_Click(object sender, RoutedEventArgs e)
        {
            Tip.d("ButtonDisconnectFromDevice_Click");
            MainWindow.NotifyType notificationStatus;
            notificationStatus = NotifyType.StatusMessage;
            String notificationMessage = null;
            pos.disConnect();
            notificationMessage = "Device " + DeviceInfo["Device_name"] +  " closed";
            MainWindow.Current.NotifyUser(notificationMessage, notificationStatus);
            ButtonDisconnectFromDevice.IsEnabled = false;
            ButtonConnectToUSBDevice.IsEnabled = true;
        }

        private void ConnectDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Tip.d("ConnectDevices_SelectionChanged");
            var selection = ConnectDevices.SelectedItems;
            Tip.d("selection: " + selection);
            DeviceInformation entry = null;
            if (selection.Count > 0)
            {
                var obj = selection[0];
                entry = (DeviceInformation)obj;
                System.Diagnostics.Debug.WriteLine("Selection Changed!!***********************************");
                var deviceType = entry.DeviceType;
                if (deviceType == "USB DEVICE")
                {
                    ButtonConnectToUSBDevice.IsEnabled = true;
                    ButtonConnectToBTDevice.IsEnabled = false;
                    
                }
                
            }

        }

        private void doTrade_Click(object sender, RoutedEventArgs e)
        {
            pos.doTrade();
           // update_firmware();
            return;
        }

        private void getPosId_Click(object sender, RoutedEventArgs e)
        {
            //pos.resetCmdStatus();
            pos.getQposId();
           
            return;
        }

        private void getPosInfo_Click(object sender, RoutedEventArgs e)
        {
            pos.getQposInfo();
            
            return;
        }

        private void resetPosStatus_Click(object sender, RoutedEventArgs e)
        {
            if (pos.resetQPOS())
            {
                textResult.Text = "Device Reset is completed!";
            }
            else
            {
                textResult.Text = "Unable to reset device due to unexpected error..";
            }

            return;
        }
    }
}
