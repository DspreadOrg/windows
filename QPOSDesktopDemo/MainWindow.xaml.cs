using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Windows.Devices.Enumeration;
using SDK_LIB;

namespace QPOSDesktopDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            // Initialise UI Components and reset bunch of flags.
            InitializeComponent();
            listOfDevices = new ObservableCollection<DeviceListEntry>();
            mapDeviceWatchersToDeviceSelector = new Dictionary<DeviceWatcher, string>();
            watchersStarted = false;
            watchersSuspended = false;
            isAllDevicesEnumerated = false;
            Current = this;

            // If we are connected to the device or planning to reconnect, we should disable the list of devices
            // to prevent the user from opening a device without explicitly closing or disabling the auto reconnect
            if (EventHandlerForDevice.Current.IsDeviceConnected
                || (EventHandlerForDevice.Current.IsEnabledAutoReconnect
                && EventHandlerForDevice.Current.DeviceInformation != null))
            {
                UpdateConnectDisconnectButtonsAndList(ButtonType.DisconnectButton);

                // These notifications will occur if we are waiting to reconnect to device when we start the page
                EventHandlerForDevice.Current.OnDeviceConnected = this.OnDeviceConnected;
                EventHandlerForDevice.Current.OnDeviceClose = this.OnDeviceClosing;
            }
            else
            {
                UpdateConnectDisconnectButtonsAndList(ButtonType.ConnectButton);
            }

            NotifyUser(promoteUserToStart, NotifyType.StatusMessage);
        }

        private void scanSerial_Click(object sender, RoutedEventArgs e)
        {
            // Initialize the desired device watchers so that we can watch for when devices are connected/removed
            InitializeDeviceWatchers();
            StartDeviceWatchers();

            //DeviceListSource.Source = listOfDeviceNames;
            CollectionViewSource DeviceListSource = (CollectionViewSource)Current.Resources["DeviceListSource"];
            DeviceListSource.Source = listOfDevices;
        }

        private void ButtonDisconnectFromDevice_Click(object sender, RoutedEventArgs e)
        {
            var selection = ConnectDevices.SelectedItems;
            MainWindow.NotifyType notificationStatus;
            notificationStatus = NotifyType.StatusMessage;
            String notificationMessage = null;
            DeviceListEntry entry = null;

            // Prevent auto reconnect because we are voluntarily closing it
            // Re-enable the ConnectDevice list and ConnectToDevice button if the connected/opened device was removed.
            EventHandlerForDevice.Current.IsEnabledAutoReconnect = false;

            if (selection.Count > 0)
            {
                var obj = selection[0];
                entry = (DeviceListEntry)obj;

                if (entry != null)
                {
                    var deviceType = entry.DeviceType;
                    if (deviceType == "Bluetooth Device")
                    {
                        pos.disConnectFull();
                    }
                    else { pos.disConnect(); }
                }
            }
            notificationMessage = "Device " + deviceInfo.Name + " closed";
            MainWindow.Current.NotifyUser(notificationMessage, notificationStatus);
            UpdateConnectDisconnectButtonsAndList(ButtonType.ConnectButton);
            waitForBTConnectionResult.Reset();
        }

        private void ConnectDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selection = ConnectDevices.SelectedItems;

            DeviceListEntry entry = null;
            if (selection.Count > 0)
            {
                var obj = selection[0];
                entry = (DeviceListEntry)obj;
                System.Diagnostics.Debug.WriteLine("Selection Changed!!***********************************");
                var deviceType = entry.DeviceType;
                if (deviceType == "Bluetooth Device")
                {
                    ButtonConnectToUSBDevice.IsEnabled = false;
                    ButtonConnectToBTDevice.IsEnabled = true;
                }
                else
                {
                    ButtonConnectToUSBDevice.IsEnabled = true;
                    ButtonConnectToBTDevice.IsEnabled = false;
                }
            }
        }

        private void doTrade_Click(object sender, RoutedEventArgs e)
        {
            pos.doTrade();
            return;
        }

        private void getPosId_Click(object sender, RoutedEventArgs e)
        {
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
            //pos.sendDeviceCommandString("0000");
            //Task.Delay(5000000);
            //pos.doTrade();

            // Testing for resetQPOSStatus() method
            //if (pos.resetQPosStatus())
            //{
            //    textResult.Text = "resetQPOSStatus() Operation is completed!";
            //}
            //else
            //{
            //    textResult.Text = "resetQPOSStatus() Operation is failed...";
            //}
            // Testing for resetQPOS() method
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

        private async void pairBluetooth_Click(object sender, RoutedEventArgs e)
        {
            var isSuccessful = await OpenBluetoothSettings();
            if (!isSuccessful)
            {
                MessageBox.Show("Error, unable to open System Bluetooth Setting Window","Oops....",MessageBoxButton.OK,MessageBoxImage.Error);
            }
        }

        private async void ButtonConnectToBTDevice_Click(object sender, RoutedEventArgs e)
        {
            NotifyUser(connectionInProgress, NotifyType.ProcessingMessage);
            var selection = ConnectDevices.SelectedItems;
            DeviceListEntry entry = null;

            if (selection.Count > 0)
            {
                var obj = selection[0];
                entry = (DeviceListEntry)obj;
                System.Diagnostics.Debug.WriteLine("button clicked !!***********************************");
                targetDeviceNo = entry.Name;
            }
            await findBTD(targetDeviceNo);
            // ************************************** //
            // To the people who maintain this Demo 
            // This if statement block should rarely triggered because All Bluetooth Device with Device Name should be 
            // enumerated when Demo App first launch. This is done with DeviceWatcher Class. To connect device with Bluetooth 
            // user needs to first select a bluetooth device. Otherwise "Connect via Bluetooth" button will be grayed out 
            // So if deviceService returns "null" after findBTD() method, please check to make sure correct device serial number 
            // has been passed to findBTD() Method, as well as the DeviceInformation.FindAllAsync() method and RfcommDeviceService.FromIdAsync()
            // does return proper result
            if (btDeviceService == null)
            {
                NotifyUser("No Bluetooth Devices are found. Please check your bluetooth settings or your hardware. E.g. if the device is powered on or device is correctly paired", NotifyType.ErrorMessage);
                return;
            }
            // ************************************** //

            pos = QPOSService.getInstance(QPOSService.CommunicationMode.BLUETOOTH);
            listener = new MyPosListener(pos, textResult);
            pos.initListener(listener);
            pos.connectBT(btDeviceService);
            waitForBTConnectionResult.WaitOne();
            this.deviceInfo = entry.DeviceInformation;
            if (deviceConnected)
            {
                Debug.WriteLine("OK");
                UpdateConnectDisconnectButtonsAndList(ButtonType.DisconnectButton);
                var notificationStatus = NotifyType.StatusMessage;
                var notificationMessage = "Device " + deviceInfo.Name + " is connected via Bluetooth Communication Method.";
                NotifyUser(notificationMessage, notificationStatus);
                waitForBTConnectionResult.Reset();
            }
            else
            {
                Debug.WriteLine("NG");
                UpdateConnectDisconnectButtonsAndList(ButtonType.ConnectButton);

                var notificationStatus = NotifyType.ErrorMessage;
                string notificationMessage;
                notificationMessage = "Error occured when connecting to device via Bluetooth. Please also check to see if your device is powered up and properly paired! Device Name: " + deviceInfo.Name;
                NotifyUser(notificationMessage, notificationStatus);
                waitForBTConnectionResult.Reset();
                pos.disConnectFull();
            }
        }

        private async void ButtonConnectToUSBDevice_Click(object sender, RoutedEventArgs e)
        {
            NotifyUser(connectionInProgress, NotifyType.ProcessingMessage);
            var selection = ConnectDevices.SelectedItems;
            DeviceListEntry entry = null;

            if (selection.Count > 0)
            {
                var obj = selection[0];
                entry = (DeviceListEntry)obj;
                System.Diagnostics.Debug.WriteLine("button clicked !!***********************************");
                if (entry != null)
                {
                    // Create an EventHandlerForDevice to watch for the device we are connecting to
                    EventHandlerForDevice.CreateNewEventHandlerForDevice();

                    // Get notified when the device was successfully connected to or about to be closed
                    EventHandlerForDevice.Current.OnDeviceConnected = this.OnDeviceConnected;
                    EventHandlerForDevice.Current.OnDeviceClose = this.OnDeviceClosing;

                    // It is important that the FromIdAsync call is made on the UI thread because the consent prompt, when present,
                    // can only be displayed on the UI thread. Since this method is invoked by the UI, we are already in the UI thread.
                    Boolean openSuccess = await OpenDeviceAsync(entry.DeviceInformation, entry.DeviceSelector);


                    // Disable connect button if we connected to the device
                    //UpdateConnectDisconnectButtonsAndList(!openSuccess);
                    if (openSuccess)
                    {
                        UpdateConnectDisconnectButtonsAndList(ButtonType.DisconnectButton);
                    }
                    else
                    {
                        UpdateConnectDisconnectButtonsAndList(ButtonType.ConnectButton);
                    }
                }
            }
        }
    }
}
