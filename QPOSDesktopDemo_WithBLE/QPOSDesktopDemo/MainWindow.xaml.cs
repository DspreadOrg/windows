﻿using System;
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
using System.IO;
using System.Threading;
using System.ComponentModel;
using System.Windows.Threading;

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
            //identify USB port----scan
            //StopBleDeviceWatcher();
            //InitializeDeviceWatchers();
            //StartDeviceWatchers();

            //bleScanRunning = false;
            //scanSerial.IsEnabled = false;
            //ButtonConnectToUSBDevice.IsEnabled = false;
            //ButtonConnectToBTDevice.IsEnabled = false;
        }
         
        private void scanSerial_Click(object sender, RoutedEventArgs e)
        {
            // Initialize the desired device watchers so that we can watch for when devices are connected/removed
            StopBleDeviceWatcher();
            InitializeDeviceWatchers();
            StartDeviceWatchers();

            CollectionViewSource DeviceListSource = (CollectionViewSource)Current.Resources["DeviceListSource"];
            DeviceListSource.Source = listOfDevices;
            bleScanRunning = false;

            ButtonConnectToUSBDevice.IsEnabled = true;
            ButtonConnectToBTDevice.IsEnabled = false;


        }

        private void ButtonDisconnectFromDevice_Click(object sender, RoutedEventArgs e)
        {
            var selection = ConnectDevices.SelectedItems;
            MainWindow.NotifyType notificationStatus;
            notificationStatus = NotifyType.StatusMessage;
            String notificationMessage = null;
            if (bleScanRunning)
            {
                pos.disConnectFull();
                if (selection.Count > 0)
                {
                    var obj = selection[0];
                    BluetoothLEDeviceDisplay bleEntry = (BluetoothLEDeviceDisplay)obj;
                    notificationMessage = "Bluetooth 4.0 Device " + bleEntry.Name + " closed";
                }
                MainWindow.Current.NotifyUser(notificationMessage, notificationStatus);
                UpdateConnectDisconnectButtonsAndList(ButtonType.ConnectButton);
                return;
            }
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

            if (!bleScanRunning)
            {
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
            else
            {
                BluetoothLEDeviceDisplay entry = null;
                if (selection.Count > 0)
                {
                    var obj = selection[0];
                    entry = (BluetoothLEDeviceDisplay)obj;
                    System.Diagnostics.Debug.WriteLine("Selection Changed!!***********************************");
                    ButtonConnectToUSBDevice.IsEnabled = false;
                    ButtonConnectToBTDevice.IsEnabled = true;
                    NotifyType messageType = new NotifyType();
                    messageType = (entry.IsPaired) ? NotifyType.StatusMessage : NotifyType.ErrorMessage;
                    NotifyUser("Bluetooth 4.0 Device - " + entry.Name + " is selected. Device pairing status: " + entry.IsPaired + ".", messageType);
                }
            }
        }
        private void UpdateEmv_from_xml()
        {
            string sStuName = string.Empty;
            string emv_app_str = "";
            string emv_capk_str = "";
            bool app_start = false;
            bool capk_start = false;
            string Filename = "emv_profile_tlv.xml";
            FileStream fs = new FileStream(Filename, FileMode.Open);
            //// "GB2312"用于显示中文字符，写其他的，中文会显示乱码
            StreamReader reader = new StreamReader(fs, UnicodeEncoding.GetEncoding("GB2312"));
            while ((sStuName = reader.ReadLine()) != null)
            {
                //更新APP
                if (app_start)
                {
                    emv_app_str += QPOSDesktopLib.Emv.format_tlv_value(sStuName);

                }
                else if (capk_start)
                {
                    emv_capk_str += QPOSDesktopLib.Emv.format_tlv_value(sStuName);
                }
                if (sStuName.Contains("<app>"))
                {
                    app_start = true;
                    Tip.d("app start-----------------\r\n");
                }
                else if (sStuName.Contains("</app>"))
                {
                    app_start = false;
                    //pos.updateEmv_AppConfig(emv_app_str);
                    emv_app_str += ",";
                }
                if (sStuName.Contains("<capk>"))
                {
                    capk_start = true;
                    app_start = false;
                    Tip.d("capk start-----------------\r\n");
                }
                else if (sStuName.Contains("</capk>"))
                {
                    capk_start = false;
                    emv_capk_str += ",";
                }

            }
            fs.Close();
            pos.CustomUpdateEmvConfig(emv_app_str, emv_capk_str);

        }
        private void UpdateEmv_from_bin()
        {
            string emvAppCfg = "";
            string emvCapkCfg = "";
            //读文件
            string file_app = "emv_app.bin";
            string file_capk = "emv_capk.bin";
            System.IO.FileStream fs = new FileStream(file_app, FileMode.Open);
            long size = fs.Length;
            fs.Seek(0, SeekOrigin.Begin);
            byte[] array = new byte[size];

            if (size != 0)
            {
                fs.Read(array, 0, array.Length);
                emvAppCfg = Util.byteArray2Hex(array);
                Tip.d("emvAPPcfg=  " + emvAppCfg + "\r\n");
                fs.Close();
            }
            System.IO.FileStream fs_capk = new FileStream(file_capk, FileMode.Open);
            long size_capk = fs_capk.Length;
            fs_capk.Seek(0, SeekOrigin.Begin);
            byte[] array_capk = new byte[size_capk];
            if (size_capk != 0)
            {
                fs_capk.Read(array_capk, 0, array_capk.Length);
                emvCapkCfg = Util.byteArray2Hex(array_capk);
                Tip.d("emvCapkCfg=  " + emvCapkCfg + "\r\n");
                fs_capk.Close();
            }
            pos.updateEmvConfig(emvAppCfg, emvCapkCfg);

        }
        private void update_firmware()
        {
            string file_name = "upgrader.asc";
            if (!File.Exists(file_name))
            {
                Tip.d(file_name + " not find");
                return;
            }
            System.IO.FileStream fs = new FileStream(file_name, FileMode.Open);
            long size = fs.Length;
            fs.Seek(0, SeekOrigin.Begin);
            byte[] array = new byte[size];
            
            if (size != 0)
            {
                fs.Read(array, 0, array.Length);
                fs.Close();
            }
            Tip.d("read upgrader.asc finish");
            
            pos.updatePosFirmware(array,"");
           
        }
        private void upload_dock_file(string filePath)
        {
            String fileName = System.IO.Path.GetFileName(filePath);
            Tip.d("fileName="+fileName);
            System.IO.FileStream fs = new FileStream(filePath, FileMode.Open);
            long size = fs.Length;
            fs.Seek(0, SeekOrigin.Begin);
            byte[] array = new byte[size];
            if (size != 0)
            {
                fs.Read(array, 0, array.Length);
                fs.Close();
            }
            pos.dock_file_Write(fileName, array, array.Length);
          
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

        private void firmwareUp_Click(object sender, RoutedEventArgs e)
        {
            update_firmware();
            return;
        }
        private async void pairBluetooth_Click(object sender, RoutedEventArgs e)
        {
            if (!bleScanRunning)
            {
                var isSuccessful = await OpenBluetoothSettings();
                if (!isSuccessful)
                {
                    MessageBox.Show("Error, unable to open System Bluetooth Setting Window", "Oops....", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }
            Pairing();
        }

        private async void ButtonConnectToBTDevice_Click(object sender, RoutedEventArgs e)
        {
            NotifyUser(connectionInProgress, NotifyType.ProcessingMessage);
            if (bleScanRunning)
            {
                ConnectToBLEDevice();
            }
            else
            {
                ConnectToBTLegacyDevice();
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

        private void scanBLE_Click(object sender, RoutedEventArgs e)
        {
            if (deviceWatcherBLE == null)
            {
                StopDeviceWatchers();
                StartBleDeviceWatcher();
                CollectionViewSource DeviceListSource = (CollectionViewSource) Current.Resources["DeviceListSource"];
                DeviceListSource.Source = ResultCollection;
                bleScanRunning = true;
                NotifyUser($"Device watcher for bluetooth 4.0 device started.", NotifyType.StatusMessage);
            }

        }

       
    }
}
