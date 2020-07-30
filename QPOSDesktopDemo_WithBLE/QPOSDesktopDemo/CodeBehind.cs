using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SDK_LIB;
using System.Windows;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Threading;
using System.Windows.Media;
using Windows.Foundation;
using Windows.Networking.Connectivity;
using Windows.Devices.SerialCommunication;
using System.Diagnostics;
using System.Collections;
using System.Windows.Controls;

namespace QPOSDesktopDemo
{
    public partial class MainWindow : Window
    {
        #region Private Fields
        #region Member for Legacy Bluetooth Connection
        /// <summary>
        /// Private field that stores the device serial number for Legacy Bluetooth Connection
        /// </summary>
        private static string targetDeviceNo;

        /// <summary>
        /// RfcommDeviceService object for establishing Bluetooth communication
        /// </summary>
        private RfcommDeviceService btDeviceService = null;
        #endregion

        #region Member for Bluetooth 4.0 Device Connection
        /// <summary>
        /// Device Watcher object for BLE devices
        /// </summary>
        private DeviceWatcher deviceWatcherBLE;

        /// <summary>
        /// Boolean flag to incidate scan result is obtained in BLE mode.
        /// </summary>
        private static bool bleScanRunning = false;

        /// <summary>
        /// Store the Bluetooth 4.0 Device ID
        /// </summary>
        private static string bleTargetDeviceId; 
        #endregion

        #region Members for device enumeration
        /// <summary>
        /// DeviceInformation Object used along with native device enumeration
        /// </summary>
        private DeviceInformation deviceInfo;

        /// <summary>
        /// ObservableCollection of list of DeviceListEntry object which will be
        /// used for listing the connected USB and paired Bluetooth devices
        /// </summary>
        private ObservableCollection<DeviceListEntry> listOfDevices;

        /// <summary>
        /// Observable Collection for all discovered Bluetooth 4.0 Devices
        /// </summary>
        private ObservableCollection<BluetoothLEDeviceDisplay> ResultCollection = new ObservableCollection<BluetoothLEDeviceDisplay>();

        /// <summary>
        /// Dictionary used to map the DeviceWatcher object with the string
        /// </summary>
        private Dictionary<DeviceWatcher, string> mapDeviceWatchersToDeviceSelector;

        /// <summary>
        /// Private Boolean value to indicate if the device watcher is suspended.
        /// </summary>
        private bool watchersSuspended;

        /// <summary>
        /// Private Boolean value to indicate if the device watcher is started.
        /// </summary>
        private bool watchersStarted;

        /// <summary>
        /// Private Boolean value to indicate if device enumeration operation executed 
        /// by device watcher is completed.
        /// </summary>
        private bool isAllDevicesEnumerated;

        /// <summary>
        /// Private Boolean value to indicate if the device is connected.
        /// </summary>
        private static bool deviceConnected = false;


        #endregion

        #region AutoEventHandler
        /// <summary>
        /// Used to synchronize execution and wait for Bluetooth device connection to firmly establish
        /// </summary>
        private static AutoResetEvent waitForBTConnectionResult = new AutoResetEvent(false);
        #endregion

        #region MainWindow Object
        /// <summary>
        /// Used MainWindow Object that will be assigned to current Application
        /// It is required by the EventHandlerForDevice class
        /// </summary>
        public static MainWindow Current;

        #endregion

        #region Notification Messages
        private const String ButtonNameDisconnectFromDevice = "Disconnect from device";
        private const String ButtonNameDisableReconnectToDevice = "Do not automatically reconnect to device that was just closed";
        private const string connectionInProgress = "Connecting, please wait....";
        private const string promoteUserToStart = "Please start the demo by clicking the \"Scan Serial\\Legacy Bluetooth Device\" Button";
        #endregion

        #region Computer Name Extraction for Bluetooth Device result filtering
        // Before add device to the list, check if PC Bluetooth device is recognised as a new device.
        // BTDevice on PC shares the same name as the PC name.
        // Next three lines gather the current PC name.
        private static IReadOnlyList<Windows.Networking.HostName> hostNames = NetworkInformation.GetHostNames();
        private static Windows.Networking.HostName localName = hostNames.FirstOrDefault(name => name.DisplayName.Contains(".local"));
        private static string computerName = localName.DisplayName.Replace(".local", "");
        #endregion

        #region QPOS Services
        /// <summary>
        /// Private field to hold an instance of QPOSService class
        /// </summary>
        private QPOSService pos;

        /// <summary>
        /// Private field to hold an instance of QPOSService Listener class
        /// </summary>
        private MyPosListener listener;
        #endregion

        #endregion

        #region Methods

        #region Call for System Bluetooth Settings Window
        /// <summary>
        /// Task to open the System Bluetooth Settings Window
        /// </summary>
        /// <returns>Boolean Value to indicate if the task executes successfully</returns>
        public async Task<bool> OpenBluetoothSettings()
        {
            bool result = await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:bluetooth"));
            return result;
        }
        #endregion

        #region UpdateButtonsAndLists Methods
        /// <summary>
        /// This method will enable the type of button specified in the method
        /// parameter
        /// </summary>
        /// <param name="buttonType">Type of the button that needs to be enabled</param>
        private void UpdateConnectDisconnectButtonsAndList(ButtonType buttonType)
        {
            if (buttonType == ButtonType.ConnectButton)
            {

                if (!bleScanRunning)
                {
                    // Check the current selection and enable the corresponding connection button
                    // If no selection made, disable both connection button
                    var selection = ConnectDevices.SelectedItems;
                    DeviceListEntry entry = null;
                    string deviceType = null;

                    if (selection.Count > 0)
                    {
                        var obj = selection[0];
                        entry = (DeviceListEntry)obj;
                        deviceType = entry.DeviceType;
                    }

                    if (entry == null)
                    {
                        ButtonConnectToUSBDevice.IsEnabled = false;
                        ButtonConnectToBTDevice.IsEnabled = false;
                        ButtonDisconnectFromDevice.IsEnabled = false;
                        ConnectDevices.IsEnabled = true;
                        return;
                    }

                    if (deviceType == "Bluetooth Device")
                    {
                        ButtonConnectToUSBDevice.IsEnabled = false;
                        ButtonConnectToBTDevice.IsEnabled = true;
                        ButtonDisconnectFromDevice.IsEnabled = false;
                        ConnectDevices.IsEnabled = true;
                    }
                    else if (deviceType == "USB Device")
                    {
                        ButtonConnectToUSBDevice.IsEnabled = true;
                        ButtonConnectToBTDevice.IsEnabled = false;
                        ButtonDisconnectFromDevice.IsEnabled = false;
                        ConnectDevices.IsEnabled = true;
                    }
                }
                else
                {
                    ButtonConnectToUSBDevice.IsEnabled = false;
                    ButtonConnectToBTDevice.IsEnabled = true;
                    ButtonDisconnectFromDevice.IsEnabled = false;
                    ConnectDevices.IsEnabled = true;
                }
            }

            if (buttonType == ButtonType.DisconnectButton)
            {
                ButtonConnectToUSBDevice.IsEnabled = false;
                ButtonConnectToBTDevice.IsEnabled = false;
                ButtonDisconnectFromDevice.IsEnabled = true;
                ConnectDevices.IsEnabled = false;
            }
        }

        /// <summary>
        /// Enum for type of buttons on the UI
        /// </summary>
        public enum ButtonType
        {
            ConnectButton,
            DisconnectButton
        }
        #endregion

        #region NotifyUser Method
        /// <summary>
        /// Enumeration of Available NotifyType
        /// </summary>
        public enum NotifyType
        {
            StatusMessage,
            ProcessingMessage,
            ErrorMessage
        };

        /// <summary>
        /// Used to display messages to the user
        /// </summary>
        /// <param name="strMessage">Message to be notified to User</param>
        /// <param name="type">Type of the Notification</param>
        public void NotifyUser(string strMessage, NotifyType type)
        {
            switch (type)
            {
                case NotifyType.StatusMessage:
                    StatusBorder.Background = new SolidColorBrush(Color.FromArgb(0XFF, 0X00, 0X80, 0X00));
                    break;
                case NotifyType.ErrorMessage:
                    StatusBorder.Background = new SolidColorBrush(Color.FromArgb(0XFF, 0XFF, 0X00, 0X00));
                    break;
                case NotifyType.ProcessingMessage:
                    StatusBorder.Background = new SolidColorBrush(Color.FromArgb(0XFF, 0XFF, 0XFF, 0X00));
                    break;
            }
            StatusBlock.Text = strMessage;

            // Collapse the StatusBlock if it has no text to conserve real estate.
            StatusBorder.Visibility = (StatusBlock.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
            if (StatusBlock.Text != String.Empty)
            {
                StatusBorder.Visibility = Visibility.Visible;
            }
            else
            {
                StatusBorder.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region Start/Stop Serial/Bluetooth Legacy DeviceWatcher
        /// <summary>
        /// Initialize device watchers to watch for the Serial Devices.
        ///
        /// GetDeviceSelector return an AQS string that can be passed directly into DeviceWatcher.createWatcher() or  DeviceInformation.createFromIdAsync(). 
        ///
        /// In this sample, a DeviceWatcher will be used to watch for devices because we can detect surprise device removals.
        /// </summary>
        private void InitializeDeviceWatchers()
        {

            // Target all Serial Devices present on the system
            var deviceSelector = SerialDevice.GetDeviceSelector();

            // Other variations of GetDeviceSelector() usage are commented for reference
            //
            // Target a specific USB Serial Device using its VID and PID (here Arduino VID/PID is used)
            // var deviceSelector = SerialDevice.GetDeviceSelectorFromUsbVidPid(0x2341, 0x0043);
            //
            // Target a specific Serial Device by its COM PORT Name - "COM3"
            // var deviceSelector = SerialDevice.GetDeviceSelector("COM3");
            //
            // Target a specific UART based Serial Device by its COM PORT Name (usually defined in ACPI) - "UART1"
            // var deviceSelector = SerialDevice.GetDeviceSelector("UART1");
            //

            // Create a device watcher to look for instances of the Serial Device that match the device selector
            // used earlier.

            var deviceWatcher = DeviceInformation.CreateWatcher(deviceSelector);

            // Allow the EventHandlerForDevice to handle device watcher events that relates or effects our device (i.e. device removal, addition, app suspension/resume)
            AddDeviceWatcher(deviceWatcher, deviceSelector);
        }

        /// <summary>
        /// Registers for Added, Removed, and Enumerated events on the provided deviceWatcher before adding it to an internal list.
        /// </summary>
        /// <param name="deviceWatcher"></param>
        /// <param name="deviceSelector">The AQS used to create the device watcher</param>
        private void AddDeviceWatcher(DeviceWatcher deviceWatcher, String deviceSelector)
        {
            deviceWatcher.Added += new TypedEventHandler<DeviceWatcher, DeviceInformation>(this.OnDeviceAdded);
            deviceWatcher.Removed += new TypedEventHandler<DeviceWatcher, DeviceInformationUpdate>(this.OnDeviceRemoved);
            deviceWatcher.EnumerationCompleted += new TypedEventHandler<DeviceWatcher, Object>(this.OnDeviceEnumerationComplete);
            
            mapDeviceWatchersToDeviceSelector.Add(deviceWatcher, deviceSelector);
        }
        
        /// <summary>
        /// Starts all device watchers including ones that have been individually stopped.
        /// </summary>
        private void StartDeviceWatchers()
        {
            // Start all device watchers
            watchersStarted = true;
            isAllDevicesEnumerated = false;

            foreach (DeviceWatcher deviceWatcher in mapDeviceWatchersToDeviceSelector.Keys)
            {
                if ((deviceWatcher.Status != DeviceWatcherStatus.Started)
                    && (deviceWatcher.Status != DeviceWatcherStatus.EnumerationCompleted))
                {
                    deviceWatcher.Start();
                }
                
            }
        }

        /// <summary>
        /// Stops all device watchers.
        /// </summary>
        private void StopDeviceWatchers()
        {
            // Stop all device watchers
            foreach (DeviceWatcher deviceWatcher in mapDeviceWatchersToDeviceSelector.Keys)
            {
                if ((deviceWatcher.Status == DeviceWatcherStatus.Started)
                    || (deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted))
                {
                    deviceWatcher.Stop();
                }
            }

            // Clear the list of devices so we don't have potentially disconnected devices around
            ClearDeviceEntries();

            watchersStarted = false;
        }
        #endregion

        #region Start/Stop BLE DeviceWatcher

        private void StopBleDeviceWatcher()
        {
            if (deviceWatcherBLE != null)
            {
                // Unregister the event handlers.
                deviceWatcherBLE.Added -= DeviceWatcher_Added;
                deviceWatcherBLE.Updated -= DeviceWatcher_Updated;
                deviceWatcherBLE.Removed -= DeviceWatcher_Removed;
                deviceWatcherBLE.EnumerationCompleted -= DeviceWatcher_EnumerationCompleted;
                deviceWatcherBLE.Stopped -= DeviceWatcher_Stopped;

                // Stop the watcher.
                deviceWatcherBLE.Stop();
                deviceWatcherBLE = null;
            }
        }

        /// <summary>
        ///     Starts a device watcher that looks for all nearby BT devices (paired or unpaired). Attaches event handlers and
        ///     populates the collection of devices.
        /// </summary>
        private void StartBleDeviceWatcher()
        {
            // Additional properties we would like about the device.
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };

            // BT_Code: Currently Bluetooth APIs don't provide a selector to get ALL devices that are both paired and non-paired.
            deviceWatcherBLE =
                    DeviceInformation.CreateWatcher(
                        "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")",
                        requestedProperties,
                        DeviceInformationKind.AssociationEndpoint);

            // Register event handlers before starting the watcher.
            deviceWatcherBLE.Added += DeviceWatcher_Added;
            deviceWatcherBLE.Updated += DeviceWatcher_Updated;
            deviceWatcherBLE.Removed += DeviceWatcher_Removed;
            deviceWatcherBLE.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            deviceWatcherBLE.Stopped += DeviceWatcher_Stopped;

            // Start over with an empty collection.
            ResultCollection.Clear();

            // Start the watcher.
            deviceWatcherBLE.Start();
        }

        private BluetoothLEDeviceDisplay FindBluetoothLEDeviceDisplay(string id)
        {
            foreach (BluetoothLEDeviceDisplay bleDeviceDisplay in ResultCollection)
            {
                if (bleDeviceDisplay.Id == id)
                {
                    return bleDeviceDisplay;
                }
            }
            return null;
        }

        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.InvokeAsync(() =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcherBLE)
                {
                    // Make sure device name isn't blank or already present in the list.
                    if (deviceInfo.Name != string.Empty && FindBluetoothLEDeviceDisplay(deviceInfo.Id) == null)
                    {
                        ResultCollection.Add(new BluetoothLEDeviceDisplay(deviceInfo));
                    }
                }
            }, DispatcherPriority.Normal);
        }

        private async void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.InvokeAsync(() =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcherBLE)
                {
                    BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                    if (bleDeviceDisplay != null)
                    {
                        bleDeviceDisplay.Update(deviceInfoUpdate);
                    }
                }
            }, DispatcherPriority.Normal);
        }

        private async void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.InvokeAsync(() =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcherBLE)
                {
                    // Find the corresponding DeviceInformation in the collection and remove it.
                    BluetoothLEDeviceDisplay bleDeviceDisplay = FindBluetoothLEDeviceDisplay(deviceInfoUpdate.Id);
                    if (bleDeviceDisplay != null)
                    {
                        ResultCollection.Remove(bleDeviceDisplay);
                    }
                }
            },DispatcherPriority.Normal);
        }

        private async void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object e)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.InvokeAsync(() =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcherBLE)
                {
                    NotifyUser($"{ResultCollection.Count} devices found. Enumeration completed.",
                        NotifyType.StatusMessage);
                }
            },DispatcherPriority.Normal);
        }

        private async void DeviceWatcher_Stopped(DeviceWatcher sender, object e)
        {
            // We must update the collection on the UI thread because the collection is databound to a UI element.
            await Dispatcher.InvokeAsync(() =>
            {
                // Protect against race condition if the task runs after the app stopped the deviceWatcher.
                if (sender == deviceWatcherBLE)
                {
                    NotifyUser($"No longer watching for devices.",
                            sender.Status == DeviceWatcherStatus.Aborted ? NotifyType.ErrorMessage : NotifyType.StatusMessage);
                }
            },DispatcherPriority.Normal);
        }
        #endregion

        #region DeviceWatcher Related events
        /// <summary>
        /// This function will add the device to the listOfDevices so that it shows up in the UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="deviceInformation"></param>
        private async void OnDeviceAdded(DeviceWatcher sender, DeviceInformation deviceInformation)
        {
            await Dispatcher.InvokeAsync(
                new Action(() =>
               {

                    if (deviceInformation.Name != computerName)
                    {
                        Console.WriteLine(deviceInformation.Name);
                        var deviceType = deviceInformation.Id.Substring(4, 4);
                        if (deviceType == "BTHE")
                        {
                            NotifyUser("Bluetooth Device added - Device Name: " + deviceInformation.Name, NotifyType.StatusMessage);
                            AddDeviceToList(deviceInformation, mapDeviceWatchersToDeviceSelector[sender]);
                        }
                        else if (deviceType == "USB#")
                        {
                            // Used to filter out virtualised USB port so only one USB connection will be enumerated.
                            var usbFilterStr = deviceInformation.Id.Substring(8, 23);
                           Console.WriteLine("deviceInformation.Id="+ deviceInformation.Id);
                            if (usbFilterStr.Substring(usbFilterStr.Length - 2, 2) == "00")
                            {
                                NotifyUser("USB Device added - Device Name: " + deviceInformation.Name, NotifyType.StatusMessage);
                                AddDeviceToList(deviceInformation, mapDeviceWatchersToDeviceSelector[sender]);
                               // Boolean openSuccess = await OpenDeviceAsync(deviceInformation, "com3");
                               //if (openSuccess)
                               //{
                               //    Debug.WriteLine("OK");
                               //}
                               //else
                               //{
                               //    Debug.WriteLine("NG");
                               //}
                           }
                       }
                        else
                        {
                            NotifyUser("Device added - " + deviceInformation.Name, NotifyType.StatusMessage);
                            AddDeviceToList(deviceInformation, mapDeviceWatchersToDeviceSelector[sender]);
                        }


                    }

                }),DispatcherPriority.Normal);
        }

        /// <summary>
        /// We will remove the device from the UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="deviceInformationUpdate"></param>
        private async void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate deviceInformationUpdate)
        {
            await Dispatcher.InvokeAsync(
                new Action(() =>
                {
                    var deviceEntity = FindDevice(deviceInformationUpdate.Id);
                    if (deviceEntity != null)
                    {
                        var deviceType = deviceEntity.DeviceInformation.Id.Substring(4, 4);
                        if (deviceType == "BTHE")
                        {
                            NotifyUser("Bluetooth Device removed - Device Name: " + deviceEntity.Name, NotifyType.StatusMessage);

                        }
                        else if (deviceType == "USB#")
                        {
                            NotifyUser("USB Device removed - Device Name: " + deviceEntity.Name, NotifyType.StatusMessage);
                        }
                        else
                        {
                            NotifyUser("Device removed - Device Name: " + deviceEntity.Name, NotifyType.StatusMessage);
                        }

                        RemoveDeviceFromList(deviceInformationUpdate.Id);
                    }

                }), DispatcherPriority.Normal);
        }

        /// <summary>
        /// Notify the UI whether or not we are connected to a device
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void OnDeviceEnumerationComplete(DeviceWatcher sender, Object args)
        {
            await Dispatcher.InvokeAsync(
                new Action(() =>
                {
                    isAllDevicesEnumerated = true;

                    // If we finished enumerating devices and the device has not been connected yet, the OnDeviceConnected method
                    // is responsible for selecting the device in the device list (UI); otherwise, this method does that.
                    if (EventHandlerForDevice.Current.IsDeviceConnected)
                    {
                        SelectDeviceInList(EventHandlerForDevice.Current.DeviceInformation.Id);

                        ButtonDisconnectFromDevice.Content = ButtonNameDisconnectFromDevice;

                        if (EventHandlerForDevice.Current.Device.PortName != "")
                        {
                            NotifyUser("Connected to - " +
                                                EventHandlerForDevice.Current.Device.PortName +
                                                " - " +
                                                EventHandlerForDevice.Current.DeviceInformation.Id, NotifyType.StatusMessage);
                        }
                        else
                        {
                            NotifyUser("Connected to - " +
                                                EventHandlerForDevice.Current.DeviceInformation.Id, NotifyType.StatusMessage);
                        }
                    }
                    else if (EventHandlerForDevice.Current.IsEnabledAutoReconnect && EventHandlerForDevice.Current.DeviceInformation != null)
                    {
                        // We will be reconnecting to a device
                        ButtonDisconnectFromDevice.Content = ButtonNameDisableReconnectToDevice;

                        NotifyUser("Waiting to reconnect to device -  " + EventHandlerForDevice.Current.DeviceInformation.Id, NotifyType.StatusMessage);
                    }
                    else
                    {
                        NotifyUser("All devices that is currently available to the system have been listed. Please select the device that you want to connect from the device list.", NotifyType.StatusMessage);
                    }
                }), DispatcherPriority.Normal);
        }

        /// <summary>
        /// If all the devices have been enumerated, select the device in the list we connected to. Otherwise let the EnumerationComplete event
        /// from the device watcher handle the device selection
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="deviceInformation"></param>
        private void OnDeviceConnected(EventHandlerForDevice sender, DeviceInformation deviceInformation)
        {
            // Find and select our connected device
            if (isAllDevicesEnumerated)
            {
                SelectDeviceInList(EventHandlerForDevice.Current.DeviceInformation.Id);

                ButtonDisconnectFromDevice.Content = ButtonNameDisconnectFromDevice;
            }

            if (EventHandlerForDevice.Current.Device.PortName != "")
            {
                NotifyUser("Connected to - " +
                                    EventHandlerForDevice.Current.Device.PortName +
                                    " - " +
                                    EventHandlerForDevice.Current.DeviceInformation.Id, NotifyType.StatusMessage);
            }
            else
            {
                NotifyUser("Connected to - " +
                                    EventHandlerForDevice.Current.DeviceInformation.Id, NotifyType.StatusMessage);
            }
        }

        /// <summary>
        /// The device was closed. If we will autoreconnect to the device, reflect that in the UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="deviceInformation"></param>
        private async void OnDeviceClosing(EventHandlerForDevice sender, DeviceInformation deviceInformation)
        {
            await Dispatcher.InvokeAsync(
                new Action(() =>
                {
                    // We were connected to the device that was unplugged, so change the "Disconnect from device" button
                    // to "Do not reconnect to device"
                    if (ButtonDisconnectFromDevice.IsEnabled && EventHandlerForDevice.Current.IsEnabledAutoReconnect)
                    {
                        ButtonDisconnectFromDevice.Content = ButtonNameDisableReconnectToDevice;
                    }
                }),DispatcherPriority.Normal);
        }
        #endregion

        #region DeviceList Manipulation Methods

        /// <summary>
        /// Searches through the existing list of devices for the first DeviceListEntry that has
        /// the specified device Id.
        /// </summary>
        /// <param name="deviceId">Id of the device that is being searched for</param>
        /// <returns>DeviceListEntry that has the provided Id; else a nullptr</returns>
        private DeviceListEntry FindDevice(String deviceId)
        {
            if (deviceId != null)
            {
                foreach (DeviceListEntry entry in listOfDevices)
                {
                    if (entry.DeviceInformation.Id == deviceId)
                    {
                        return entry;
                    }
                }
            }

            return null;
        }
        
        /// <summary>
        /// Creates a DeviceListEntry for a device and adds it to the list of devices in the UI
        /// </summary>
        /// <param name="deviceInformation">DeviceInformation on the device to be added to the list</param>
        /// <param name="deviceSelector">The AQS used to find this device</param>
        private void AddDeviceToList(DeviceInformation deviceInformation, String deviceSelector)
        {
            // search the device list for a device with a matching interface ID
            var match = FindDevice(deviceInformation.Id);

            // Add the device if it's new
            if (match == null)
            {
                // Create a new element for this device interface, and queue up the query of its
                // device information
                match = new DeviceListEntry(deviceInformation, deviceSelector);

                // PC Bluetooth Adaptor name will not be listed if it is detected as a device presented to the system.
                if (!(deviceInformation.Name == computerName))
                {
                    // Add the new element to the end of the list of devices
                    listOfDevices.Add(match);
                }

            }
        }

        /// <summary>
        /// Remove the device from device list
        /// </summary>
        /// <param name="deviceId"></param>
        private void RemoveDeviceFromList(String deviceId)
        {
            // Removes the device entry from the interal list; therefore the UI
            var deviceEntry = FindDevice(deviceId);

            listOfDevices.Remove(deviceEntry);
        }

        /// <summary>
        /// Clear the device list that will be used to display on the UI.
        /// </summary>
        private void ClearDeviceEntries()
        {
            listOfDevices.Clear();
        }

        /// <summary>
        /// Selects the item in the UI's listbox that corresponds to the provided device id. If there are no
        /// matches, we will deselect anything that is selected.
        /// </summary>
        /// <param name="deviceIdToSelect">The device id of the device to select on the list box</param>
        private void SelectDeviceInList(String deviceIdToSelect)
        {
            // Don't select anything by default.
            ConnectDevices.SelectedIndex = -1;

            for (int deviceListIndex = 0; deviceListIndex < listOfDevices.Count; deviceListIndex++)
            {
                if (listOfDevices[deviceListIndex].DeviceInformation.Id == deviceIdToSelect)
                {
                    ConnectDevices.SelectedIndex = deviceListIndex;

                    break;
                }
            }
        }

        #endregion

        #region Device Searching Methods
        /// <summary>
        /// Asynchronously find Bluetooth Device Device ID by comparing the QPOS Serial Number
        /// </summary>
        /// <param name="blueToothAddress">QPOS Hardware Serial Number</param>
        /// <returns></returns>
        async Task findBTD(String blueToothAddress)
        {
            btDeviceService = null;
            var servicesInfos = await DeviceInformation.FindAllAsync(RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort));
            RfcommDeviceService deviceServiceHolder;
            foreach (var serviceInfo in servicesInfos)
            {
                deviceServiceHolder = await RfcommDeviceService.FromIdAsync(serviceInfo.Id);
                System.Diagnostics.Debug.WriteLine(deviceServiceHolder.Device.Name);
                if (blueToothAddress.CompareTo(deviceServiceHolder.Device.Name) == 0)
                {
                    btDeviceService = deviceServiceHolder;
                    break;
                }
            }

            return;
        }
        #endregion

        #region USB Device Open Async Method
        /// <summary>
        /// This method opens the device using the WinRT Serial API. After the device is opened, save the device
        /// so that it can be used across scenarios.
        ///
        /// It is important that the FromIdAsync call is made on the UI thread because the consent prompt can only be displayed
        /// on the UI thread.
        /// 
        /// This method is used to reopen the device after the device reconnects to the computer and when the app resumes.
        /// </summary>
        /// <param name="deviceInfo">Device information of the device to be opened</param>
        /// <param name="deviceSelector">The AQS used to find this device</param>
        /// <returns>True if the device was successfully opened, false if the device could not be opened for well known reasons.
        /// An exception may be thrown if the device could not be opened for extraordinary reasons.</returns>
        public async Task<Boolean> OpenDeviceAsync(DeviceInformation deviceInfo, String deviceSelector)
        {
            System.Diagnostics.Debug.WriteLine(deviceInfo.Id);
            pos = QPOSService.getInstance(QPOSService.CommunicationMode.com);
            listener = new MyPosListener(pos, textResult);
            pos.initListener(listener);
            Boolean openSuccess = pos.connectUSB(deviceInfo);
            if (openSuccess)
            {
                Debug.WriteLine("OK");
            }
            else
            {
                Debug.WriteLine("NG");
            }
            //device = await SerialDevice.FromIdAsync(deviceInfo.Id);
            //device= await SerialDevice.get
            Boolean successfullyOpenedDevice = false;
            MainWindow.NotifyType notificationStatus;
            String notificationMessage = null;

            // Device could have been blocked by user or the device has already been opened by another app.
            if (openSuccess)
            {
                successfullyOpenedDevice = true;

                this.deviceInfo = deviceInfo;
                //this.deviceSelector = deviceSelector;

                notificationStatus = NotifyType.StatusMessage;
                notificationMessage = "Device " + deviceInfo.Name + " is connected via Serial Communication Method.";
            }
            else
            {
                successfullyOpenedDevice = false;

                notificationStatus = NotifyType.ErrorMessage;

                var deviceAccessStatus = DeviceAccessInformation.CreateFromId(deviceInfo.Id).CurrentStatus;

                if (deviceAccessStatus == DeviceAccessStatus.DeniedByUser)
                {
                    notificationMessage = "Access to the device was blocked by the user : " + deviceInfo.Name;
                }
                else if (deviceAccessStatus == DeviceAccessStatus.DeniedBySystem)
                {
                    // This status is most likely caused by app permissions (did not declare the device in the app's package.appxmanifest)
                    // This status does not cover the case where the device is already opened by another app.
                    notificationMessage = "Access to the device was blocked by the system : " + deviceInfo.Name;
                }
                else
                {
                    // Most likely the device is opened by another app, but cannot be sure
                    notificationMessage = "Unknown error, possibly opened by another app : " + deviceInfo.Name;
                }
            }

            MainWindow.Current.NotifyUser(notificationMessage, notificationStatus);

            return successfullyOpenedDevice;
        }
        #endregion

        #region Pairing
        /// <summary>
        /// Boolean flag to prevent multiple executions while one execution is currently in progress
        /// </summary>
        private bool isBusy = false;
        /// <summary>
        /// Pairing Method
        /// </summary>
        private async Task<bool> blePairing(BluetoothLEDeviceDisplay btLEDeviceItem)
        {
            // Do not allow a new Pair operation to start if an existing one is in progress.
            if (isBusy)
            {
                return false;
            }

            isBusy = true;

            Tip.d("Pairing started. Please wait...");

            // BT_Code: Pair the device.
            var result = await btLEDeviceItem.DeviceInformation.Pairing.PairAsync(DevicePairingProtectionLevel.None);
            var status = result.Status == DevicePairingResultStatus.Paired || result.Status == DevicePairingResultStatus.AlreadyPaired;
            isBusy = false;

            return status;
        }

        private async void Pairing()
        {
            var selection = ConnectDevices.SelectedItems;
            BluetoothLEDeviceDisplay entry = null;
            if (selection.Count > 0)
            {
                var obj = selection[0];
                entry = (BluetoothLEDeviceDisplay)obj;
            }
            if (!entry.IsPaired) // If the device has paired, don't run the pairing routine again because it consume resources.
            {
                var pairingSuccessful = await blePairing(entry);
                NotifyType messageType = (pairingSuccessful) ? NotifyType.StatusMessage : NotifyType.ErrorMessage;
                string message = (pairingSuccessful) ? "Bluetooth 4.0 Device: " + entry.Name + " has paired successful." :
                    "Bluetooth 4.0 Device: " + entry.Name + " failed to pair.";
                NotifyUser(message, messageType);
            }
        }

        #endregion

        #region Legacy Bluetooth Device Connection
        async private void ConnectToBTLegacyDevice()
        {
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
            if (MainWindow.deviceConnected)
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
                string notificationMessage = "Unable to connect to device. Please also check to see if your device is powered up! Device Name: " + deviceInfo.Name;
                NotifyUser(notificationMessage, notificationStatus);
                waitForBTConnectionResult.Reset();
                pos.disConnectFull();
            }
        }
        #endregion

        #region Bluetooth 4.0 Device Connection
        /// <summary>
        /// Implementation of button code behind function to connect Bluetooth LE (Bluetooth 4.0) Device routine
        /// Its implementation will be similar to Legacy Bluetooth
        /// </summary>
        /// <param name="sender">Button object</param>
        /// <param name="e">Additional Event args</param>
        private async void ConnectToBLEDevice()
        {
            StopBleDeviceWatcher();
            var selection = ConnectDevices.SelectedItems;
            BluetoothLEDeviceDisplay entry = null;

            if (selection.Count > 0)
            {
                var obj = selection[0];
                entry = (BluetoothLEDeviceDisplay)obj;
                System.Diagnostics.Debug.WriteLine("button clicked !!***********************************");
                bleTargetDeviceId = entry.Id;
            }

            // Initiate a new Bluetooth LE pos instance
            pos = QPOSService.getInstance(QPOSService.CommunicationMode.BLUETOOTH_LE);
            listener = new MyPosListener(pos, textResult);
            pos.initListener(listener);
            pos.connectBTLED(bleTargetDeviceId);
            waitForBTConnectionResult.WaitOne();
            //this.deviceInfo = entry.DeviceInformation;
            if (MainWindow.deviceConnected)
            {
                Debug.WriteLine("OK");
                var notificationStatus = NotifyType.StatusMessage;
                var notificationMessage = "Device is connected via Bluetooth Communication Method.";
                NotifyUser(notificationMessage, notificationStatus);
                UpdateConnectDisconnectButtonsAndList(ButtonType.DisconnectButton);
                waitForBTConnectionResult.Reset();
            }
            else
            {
                Debug.WriteLine("NG");

                var notificationStatus = NotifyType.ErrorMessage;
                string notificationMessage = "Unknown error, possibly opened by another app. Please also check to see if your device is powered up!";
                NotifyUser(notificationMessage, notificationStatus);
                UpdateConnectDisconnectButtonsAndList(ButtonType.ConnectButton);
                waitForBTConnectionResult.Reset();
                pos.disConnectFull();
            }
        }
        #endregion

        #endregion

        #region Device Listener
        private class MyPosListener : QPOSService.QPOSServiceListener
        {
            private TextBox txtDisplay;
            private QPOSService pos;
            public MyPosListener(QPOSService apos, TextBox txtDisplayResult)
            {
                pos = apos;
                txtDisplay = txtDisplayResult;
            }

            public void onRequestSetAmount()
            {
                amount = "123";
                cashbackAmount = "66";
                QPOSService.TransactionType transactionType = QPOSService.TransactionType.GOODS;
                pos.setAmount(amount, cashbackAmount, "840", transactionType);
            }

            async public void onError(QPOSService.Error errorState)
            {
                String strMsg = "";
                if (errorState == QPOSService.Error.CMD_NOT_AVAILABLE)
                {
                    strMsg = "command_not_available";
                }
                else if (errorState == QPOSService.Error.DO_TRADE_ICC_EXCECPTION)
                {
                    strMsg = "swipe card only";
                }
                else if (errorState == QPOSService.Error.TIMEOUT)
                {
                    strMsg = "device_no_response";
                }
                else if (errorState == QPOSService.Error.DEVICE_RESET)
                {
                    strMsg = "device_reset";
                }
                else if (errorState == QPOSService.Error.UNKNOWN)
                {
                    strMsg = "unknown_error";
                }
                else if (errorState == QPOSService.Error.DEVICE_BUSY)
                {
                    strMsg = "device_busy";
                }
                else if (errorState == QPOSService.Error.INPUT_OUT_OF_RANGE)
                {
                    strMsg = "out_of_range";
                }
                else if (errorState == QPOSService.Error.INPUT_INVALID_FORMAT)
                {
                    strMsg = "invalid_format";
                }
                else if (errorState == QPOSService.Error.INPUT_ZERO_VALUES)
                {
                    strMsg = "zero_values";
                }
                else if (errorState == QPOSService.Error.INPUT_INVALID)
                {
                    strMsg = "input_invalid";
                }
                else if (errorState == QPOSService.Error.CASHBACK_NOT_SUPPORTED)
                {
                    strMsg = "cashback_not_supported";
                }
                else if (errorState == QPOSService.Error.CRC_ERROR)
                {
                    strMsg = "crc_error";
                }
                else if (errorState == QPOSService.Error.COMM_ERROR)
                {
                    strMsg = "comm_error";
                }
                else if (errorState == QPOSService.Error.MAC_ERROR)
                {
                    strMsg = "mac_error";
                }
                else if (errorState == QPOSService.Error.CMD_TIMEOUT)
                {
                    strMsg = "cmd_timeout";
                }
                else if (errorState == QPOSService.Error.EMV_APP_CFG_ERROR)
                {
                    strMsg = "emv app config error!";
                }
                else if (errorState == QPOSService.Error.EMV_CAPK_CFG_ERROR)
                {
                    strMsg = "emv capk config error!";
                }
                else if (errorState == QPOSService.Error.WR_DATA_ERROR)
                {
                    strMsg = "write or read data error!";
                }
                //txtDisplay.Text = strMsg;
                //this.txtDisplay.Dispatcher.Invoke(new Action(() =>
                await this.txtDisplay.Dispatcher.InvokeAsync(() =>
                {
                    this.txtDisplay.Text = strMsg;
                }, DispatcherPriority.Normal);
            }

            async public void onRequestDisplay(QPOSService.Display displayMsg)
            {
                String msg = "";
                if (displayMsg == QPOSService.Display.CLEAR_DISPLAY_MSG)
                {
                    msg = "";
                }
                else if (displayMsg == QPOSService.Display.PLEASE_WAIT)
                {
                    msg = "wait";
                }
                else if (displayMsg == QPOSService.Display.INPUT_OFFLINE_PIN_ONLY)
                {
                    msg = "input offline pin only";
                }
                else if (displayMsg == QPOSService.Display.REMOVE_CARD)
                {
                    msg = "remove_card";
                }
                else if (displayMsg == QPOSService.Display.TRY_ANOTHER_INTERFACE)
                {
                    msg = "Tap Prohibit,please insert or swipe";
                }
                else if (displayMsg == QPOSService.Display.PROCESSING)
                {
                    msg = "processing";
                }
                else if (displayMsg == QPOSService.Display.PIN_OK)
                {
                    msg = "pin_ok";
                }
                else if (displayMsg == QPOSService.Display.TRANSACTION_TERMINATED)
                {
                    msg = "transaction_terminated";
                }
                else if (displayMsg == QPOSService.Display.INPUT_PIN_ING)
                {
                    msg = "pin inputting";
                }
                else if (displayMsg == QPOSService.Display.INPUT_OFFLINE_PIN_ONLY)
                {
                    msg = "offline pin inputting";
                }
                else if (displayMsg == QPOSService.Display.MAG_TO_ICC_TRADE)
                {
                    msg = "magstripe card to icc card";
                }
                else if (displayMsg == QPOSService.Display.SELECT_APP_TIMEOUT)
                {
                    msg = "select app timeout,emv transaction terminated";
                }
                else if (displayMsg == QPOSService.Display.SELECT_APP_CANCEL)
                {
                    msg = "select app cancel,emv transaction terminated";
                }
                /*
                this.txtDisplay.Dispatcher.Invoke(new Action(() =>
                {
                    this.txtDisplay.Text = msg;
                }));
                */
                await this.txtDisplay.Dispatcher.InvokeAsync(() =>
                {
                    this.txtDisplay.Text = msg;
                }, DispatcherPriority.Normal);

                // txtDisplay.Text = msg;
            }

            async public void onDoTradeResult(QPOSService.DoTradeResult result, Dictionary<String, String> decodeData)
            {
                String content = "";
                if (result == QPOSService.DoTradeResult.NONE)
                {
                    content = "No card detected. Please insert or swipe card again and press check card.";
                }
                else if (result == QPOSService.DoTradeResult.ICC)
                {
                    content = "ICC Card Inserted.";
                    pos.doEmvApp(QPOSService.EmvOption.START);
                }
                else if (result == QPOSService.DoTradeResult.NOT_ICC)
                {
                    content = "Card Inserted (Not ICC).";
                }
                else if (result == QPOSService.DoTradeResult.BAD_SWIPE)
                {
                    content = "Bad Swipe. Please swipe again and press check card.";
                }

                else if (result == QPOSService.DoTradeResult.PLAIN_TRACK)
                {
                    String formatID = decodeData["formatID"];
                    String type = decodeData["type"] == null ? "" : decodeData["type"];
                    String encTrack1 = decodeData["encTrack1"] == null ? "" : decodeData["encTrack1"];
                    String encTrack2 = decodeData["encTrack2"] == null ? "" : decodeData["encTrack2"];
                    String encTrack3 = decodeData["encTrack3"] == null ? "" : decodeData["encTrack3"];


                    content = " card_swiped : " + "\n";
                    content += " format_id =" + " " + formatID + "\n";
                    content += " type =" + " " + type + "\n";
                    content += " encrypted_track_1 :" + " " + encTrack1 + "\n";
                    content += " encrypted_track_2 :" + " " + encTrack2 + "\n";
                    content += " encrypted_track_3 :" + " " + encTrack3 + "\n";

                }
                else if (result == QPOSService.DoTradeResult.ManulEnc)
                {
                    String pinksn = decodeData["pinKsn"];
                    String trackblock = decodeData["trackblock"] == null ? "" : decodeData["trackblock"];


                    content = " card_swiped : " + "\n";
                    content += " pinksn =" + " " + pinksn + "\n";
                    content += " trackblock =" + " " + trackblock + "\n";

                }
                else if (result == QPOSService.DoTradeResult.MCR)
                {
                    String formatID = decodeData["formatID"];
                    if (formatID.Equals("30") || formatID.Equals("38") || formatID.Equals("57"))
                    {
                        String maskedPAN = decodeData["maskedPAN"];
                        String expiryDate = decodeData["expiryDate"];
                        String cardHolderName = decodeData["cardholderName"];
                        String ksn = decodeData["ksn"];
                        String serviceCode = decodeData["serviceCode"];
                        String track1Length = decodeData["track1Length"];
                        String track2Length = decodeData["track2Length"];
                        String track3Length = decodeData["track3Length"];
                        String encTracks = decodeData["encTracks"];
                        String encTrack1 = decodeData["encTrack1"];
                        String encTrack2 = decodeData["encTrack2"];
                        String encTrack3 = decodeData["encTrack3"];
                        String partialTrack = decodeData["partialTrack"];
                        //TODO 
                        String pinKsn = decodeData["pinKsn"];
                        String trackksn = decodeData["trackksn"];
                        String pinBlock = decodeData["pinBlock"];

                        content = " card_swiped ";

                        content += " format_id " + " " + formatID + "\n";
                        content += " masked_pan " + " " + maskedPAN + "\n";
                        content += " expiry_date " + " " + expiryDate + "\n";
                        content += " cardholder_name " + " " + cardHolderName + "\n";
                        content += " ksn " + " " + ksn + "\n";
                        content += " pinKsn " + " " + pinKsn + "\n";
                        content += " trackksn " + " " + trackksn + "\n";
                        content += " service_code " + " " + serviceCode + "\n";
                        content += " track_1_length " + " " + track1Length + "\n";
                        content += " track_2_length " + " " + track2Length + "\n";
                        content += " track_3_length " + " " + track3Length + "\n";
                        content += " encrypted_tracks " + " " + encTracks + "\n";
                        content += " encrypted_track_1 " + " " + encTrack1 + "\n";
                        content += " encrypted_track_2 " + " " + encTrack2 + "\n";
                        content += " encrypted_track_3 " + " " + encTrack3 + "\n";
                        content += " partial_track " + " " + partialTrack + "\n";
                        content += " pinBlock " + " " + pinBlock + "\n";
                    }
                    else
                    {
                        String maskedPAN = decodeData["maskedPAN"];
                        String expiryDate = decodeData["expiryDate"];
                        String cardHolderName = decodeData["cardholderName"];

                        String serviceCode = decodeData["serviceCode"];

                        String trackblock = decodeData["trackblock"];
                        String psamId = decodeData["psamId"];
                        //TODO 
                        String posId = decodeData["posId"];
                        String macblock = decodeData["macblock"];
                        String pinblock = decodeData["pinblock"];
                        String activateCode = decodeData["activateCode"];

                        content = " card_swiped ";

                        content += " format_id " + " " + formatID + "\n";
                        content += " maskedPAN " + " " + maskedPAN + "\n";
                        content += " expiry_date " + " " + expiryDate + "\n";
                        content += " cardholder_name " + " " + cardHolderName + "\n";
                        content += " service_code " + " " + serviceCode + "\n";

                        content += " trackblock " + " " + trackblock + "\n";
                        content += " psamId " + " " + psamId + "\n";
                        content += " posId " + " " + posId + "\n";
                        content += " macblock " + " " + macblock + "\n";
                        content += " pinblock " + " " + pinblock + "\n";
                        content += " activateCode " + " " + activateCode + "\n";

                    }

                    //pos.getPin(9,0,6,"PLS Input Pin:", maskedPAN,"",60);

                }

                else if ((result == QPOSService.DoTradeResult.NFC_ONLINE) || (result == QPOSService.DoTradeResult.NFC_OFFLINE))
                {
                    String formatID = decodeData["formatID"];
                    Console.WriteLine("formatID=" + formatID + "\r\n");
                    if (formatID.Equals("31") || formatID.Equals("40") || formatID.Equals("37") || formatID.Equals("17") || formatID.Equals("11") || formatID.Equals("10"))
                    {
                        String maskedPAN = decodeData["maskedPAN"] == null ? "" : decodeData["maskedPAN"];
                        String expiryDate = decodeData["expiryDate"] == null ? "" : decodeData["expiryDate"];
                        String cardHolderName = decodeData["cardholderName"] == null ? "" : decodeData["cardholderName"];
                        String serviceCode = decodeData["serviceCode"] == null ? "" : decodeData["serviceCode"];
                        String trackblock = decodeData["trackblock"] == null ? "" : decodeData["trackblock"];
                        String psamId = decodeData["psamId"] == null ? "" : decodeData["psamId"];
                        String posId = decodeData["posId"] == null ? "" : decodeData["posId"];
                        String pinblock = decodeData["pinblock"] == null ? "" : decodeData["pinblock"];
                        String macblock = decodeData["macblock"] == null ? "" : decodeData["macblock"];
                        String activateCode = decodeData["activateCode"] == null ? "" : decodeData["activateCode"];
                        String NFCBatchData = decodeData["NFCBatchData"] == null ? "" : decodeData["NFCBatchData"];

                        content = "tap card";
                        content += " format_id " + " " + formatID + "\n";
                        content += " masked_pan " + " " + maskedPAN + "\n";
                        content += " expiry_date " + " " + expiryDate + "\n";
                        content += " cardholder_name " + " " + cardHolderName + "\n";

                        content += "service_code " + "" + serviceCode + "\n";
                        content += "trackblock: " + trackblock + "\n";
                        content += "psamId: " + psamId + "\n";
                        content += "posId: " + posId + "\n";
                        content += "pinBlock" + " " + pinblock + "\n";
                        content += "macblock: " + macblock + "\n";
                        content += "activateCode: " + activateCode + "\n";
                        content += "NFCBatchData: " + NFCBatchData + "\n";
                    }
                    else
                    {
                        String maskedPAN = decodeData["maskedPAN"] == null ? "" : decodeData["maskedPAN"];
                        String expiryDate = decodeData["expiryDate"] == null ? "" : decodeData["expiryDate"];
                        String cardHolderName = decodeData["cardholderName"] == null ? "" : decodeData["cardholderName"];
                        String ksn = decodeData["ksn"] == null ? "" : decodeData["ksn"];
                        String serviceCode = decodeData["serviceCode"] == null ? "" : decodeData["serviceCode"];
                        String track1Length = decodeData["track1Length"] == null ? "" : decodeData["track1Length"];
                        String track2Length = decodeData["track2Length"] == null ? "" : decodeData["track2Length"];
                        String track3Length = decodeData["track3Length"] == null ? "" : decodeData["track3Length"];
                        String encTracks = decodeData["encTracks"] == null ? "" : decodeData["encTracks"];
                        String encTrack1 = decodeData["encTrack1"] == null ? "" : decodeData["encTrack1"];
                        String encTrack2 = decodeData["encTrack2"] == null ? "" : decodeData["encTrack2"];
                        String encTrack3 = decodeData["encTrack3"] == null ? "" : decodeData["encTrack3"];
                        String partialTrack = decodeData["partialTrack"] == null ? "" : decodeData["partialTrack"];
                        //TODO 
                        String pinKsn = decodeData["pinKsn"] == null ? "" : decodeData["pinKsn"];
                        String trackksn = decodeData["trackksn"] == null ? "" : decodeData["trackksn"];
                        String pinBlock = decodeData["pinBlock"] == null ? "" : decodeData["pinBlock"];
                        String NFCBatchData = decodeData["NFCBatchData"] == null ? "" : decodeData["NFCBatchData"];

                        content = " tap card ";

                        content += " format_id " + " " + formatID + "\n";
                        content += " masked_pan " + " " + maskedPAN + "\n";
                        content += " expiry_date " + " " + expiryDate + "\n";
                        content += " cardholder_name " + " " + cardHolderName + "\n";
                        content += " ksn " + " " + ksn + "\n";
                        content += " pinKsn " + " " + pinKsn + "\n";
                        content += " trackksn " + " " + trackksn + "\n";
                        content += " service_code " + " " + serviceCode + "\n";
                        content += " track_1_length " + " " + track1Length + "\n";
                        content += " track_2_length " + " " + track2Length + "\n";
                        content += " track_3_length " + " " + track3Length + "\n";
                        content += " encrypted_tracks " + " " + encTracks + "\n";
                        content += " encrypted_track_1 " + " " + encTrack1 + "\n";
                        content += " encrypted_track_2 " + " " + encTrack2 + "\n";
                        content += " encrypted_track_3 " + " " + encTrack3 + "\n";
                        content += " partial_track " + " " + partialTrack + "\n";
                        content += " pinBlock " + " " + pinBlock + "\n";
                        content += "NFCBatchData: " + NFCBatchData + "\n";
                    }

                }
                else if ((result == QPOSService.DoTradeResult.NFC_DECLINED))
                {
                    content = " nfc declined";
                }
                else if (result == QPOSService.DoTradeResult.NO_RESPONSE)
                {
                    content = " card_no_response";
                }
                else if (result == QPOSService.DoTradeResult.NO_UPDATE_WORK_KEY)
                {
                    content = "device no update work key!";
                }
                else if (result == QPOSService.DoTradeResult.TRY_ANOTHER_INTERFACE)
                {
                    content = "please insert or swipe";
                }

                await this.txtDisplay.Dispatcher.InvokeAsync(() =>
                {
                    //this.txtDisplay.Text = msg;
                    this.txtDisplay.Text = content;
                },DispatcherPriority.Normal);

                /*
               this.txtDisplay.Dispatcher.Invoke(new Action(() =>
               {
                   this.txtDisplay.Text = content;
               }));
               */
            }

            public void onRequestSelectEmvApp(List<String> appList)
            {
                
                pos.selectEmvApp(0);
               
            }

            public void onRequestFinalConfirm()
            {
                String message = "amount" + ": $" + amount;
                if (!(cashbackAmount == ""))
                {
                    message += "\n" + "cashback_amount" + ": $" + cashbackAmount;
                }
                /*
                if (MessageBox.Show(message, "amount confirm", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    pos.finalConfirm(true);
                }
                else
                {
                    pos.finalConfirm(false);
                }
                */
            }

            async public void onQposInfoResult(Dictionary<String, String> posInfoData)
            {
                String isSupportedTrack1 = posInfoData["isSupportedTrack1"] == null ? "" : posInfoData["isSupportedTrack1"];
                String isSupportedTrack2 = posInfoData["isSupportedTrack2"] == null ? "" : posInfoData["isSupportedTrack2"];
                String isSupportedTrack3 = posInfoData["isSupportedTrack3"] == null ? "" : posInfoData["isSupportedTrack3"];
                String bootloaderVersion = posInfoData["bootloaderVersion"] == null ? "" : posInfoData["bootloaderVersion"];
                String firmwareVersion = posInfoData["firmwareVersion"] == null ? "" : posInfoData["firmwareVersion"];
                String isUsbConnected = posInfoData["isUsbConnected"] == null ? "" : posInfoData["isUsbConnected"];
                String isCharging = posInfoData["isCharging"] == null ? "" : posInfoData["isCharging"];
                String batteryLevel = posInfoData["batteryLevel"] == null ? "" : posInfoData["batteryLevel"];
                String hardwareVersion = posInfoData["hardwareVersion"] == null ? "" : posInfoData["hardwareVersion"];
                String updateWorkKeyFlag = posInfoData["updateWorkKeyFlag"] == null ? "" : posInfoData["updateWorkKeyFlag"];

                String content = "";
                content += " bootloader_version " + bootloaderVersion + "\n";
                content += " firmware_version " + firmwareVersion + "\n";
                content += " usb " + isUsbConnected + "\n";
                content += " charge " + isCharging + "\n";
                content += " battery_level " + batteryLevel + "\n";
                content += " hardware_version " + hardwareVersion + "\n";
                content += " track_1_supported " + isSupportedTrack1 + "\n";
                content += " track_2_supported " + isSupportedTrack2 + "\n";
                content += " track_3_supported " + isSupportedTrack3 + "\n";
                content += "updateWorkKeyFlag: " + updateWorkKeyFlag + "\n";

                // txtDisplay.Text = content;this.txtDisplay.Dispatcher.Invoke(new Action(() =>
                await this.txtDisplay.Dispatcher.InvokeAsync(() =>
                {
                    //this.txtDisplay.Text = msg;
                    this.txtDisplay.Text = content;
                }, DispatcherPriority.Normal);

                /*
                this.txtDisplay.Dispatcher.Invoke(new Action(() =>
                {
                    this.txtDisplay.Text = content;
                }));
                */

            }

            public void onRequestOnlineProcess(String tlv)
            {
                Dictionary<String, String> decodeData = pos.anlysEmvIccData(tlv);

                //String maskedPAN=decodeData["maskedPAN"];
                //String expiryDate = decodeData["expiryDate"];
                //String cardholderName = decodeData["cardholderName"];
                //String encTracks = decodeData["trackblock"];
                //String MacBlock  = decodeData["macblock"];

                //Tip.d("online result :\n" + "maskedPAN=" + maskedPAN + "\n"+"macBlock ="+ MacBlock+"\n"+"expiryDate=" + expiryDate + "\n" + "encTracks=" + encTracks);

                Dictionary<String, String> hashtable = new Dictionary<String, String>();
                //pos.selectEmvApp(0);
                hashtable = pos.getICCTag(0, 1, "9F33");
                
                Console.WriteLine("9F33=" + hashtable["tlv"] + "\r\n");
                pos.sendOnlineProcessResult("8A023030");
                //pos.sendOnlineProcessResult("8A025A33");
                /*
                if (MessageBox.Show("Request is Online process!", "callback tips", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    pos.sendOnlineProcessResult("8A023030");
                }
                else
                {
                    pos.sendOnlineProcessResult("");
                }
                */
            }

            public void onRequestIsServerConnected()
            {
                //MessageBox.Show("Request is Server connected!", "callback tips", MessageBoxButton.OK);
                pos.isServerConnected(true);
            }

            async public void onRequestTime()
            {
                String terminalTime = DateTime.Now.ToString("yyyyMMddHHmmss");
                pos.sendTime(terminalTime);
                //txtDisplay.Text = "onRequestTime : " + terminalTime;
                await this.txtDisplay.Dispatcher.InvokeAsync(() =>
                {
                    this.txtDisplay.Text = "onRequestTime : " + terminalTime;
                }, DispatcherPriority.Normal);
                /*
                this.txtDisplay.Dispatcher.Invoke(new Action(() =>
                {
                    this.txtDisplay.Text = "onRequestTime : " + terminalTime;
                }));
                */
            }

            public String amount = "";
            public String cashbackAmount = "";
            public void onRequestTransactionResult(QPOSService.TransactionResult transactionResult)
            {
                String message = "";
                if (transactionResult == QPOSService.TransactionResult.APPROVED)
                {
                    message = "transaction_approved" + "\n"
                            + "amount" + ": $" + amount + "\n";
                    if (!(cashbackAmount == ""))
                    {
                        message += "cashback_amount" + ": $" + cashbackAmount;
                    }
                }
                else if (transactionResult == QPOSService.TransactionResult.TERMINATED)
                {
                    message = ("transaction_terminated");
                }
                else if (transactionResult == QPOSService.TransactionResult.DECLINED)
                {
                    message = ("transaction_declined");
                }
                else if (transactionResult == QPOSService.TransactionResult.CANCEL)
                {
                    message = ("transaction_cancel");
                }
                else if (transactionResult == QPOSService.TransactionResult.CAPK_FAIL)
                {
                    message = ("transaction_capk_fail");
                }
                else if (transactionResult == QPOSService.TransactionResult.NOT_ICC)
                {
                    message = ("transaction_not_icc");
                }
                else if (transactionResult == QPOSService.TransactionResult.FALLBACK)
                {
                    message = ("transaction fall back");
                }
                else if (transactionResult == QPOSService.TransactionResult.SELECT_APP_FAIL)
                {
                    message = ("transaction_app_fail");
                }
                else if (transactionResult == QPOSService.TransactionResult.SELECT_APP_TIMEOUT)
                {
                    message = ("transaction_select_timeout");
                }
                else if (transactionResult == QPOSService.TransactionResult.SELECT_APP_CANCEL)
                {
                    message = ("transaction_select_cancel");
                }
                else if (transactionResult == QPOSService.TransactionResult.DEVICE_ERROR)
                {
                    message = ("transaction_device_error");
                }
                else if (transactionResult == QPOSService.TransactionResult.CARD_NOT_SUPPORTED)
                {
                    message = ("card_not_supported");
                }
                else if (transactionResult == QPOSService.TransactionResult.MISSING_MANDATORY_DATA)
                {
                    message = ("missing_mandatory_data");
                }
                else if (transactionResult == QPOSService.TransactionResult.CARD_BLOCKED_OR_NO_EMV_APPS)
                {
                    message = ("card_blocked_or_no_evm_apps");
                }
                else if (transactionResult == QPOSService.TransactionResult.INVALID_ICC_DATA)
                {
                    message = ("invalid_icc_data");
                }

                amount = "";
                cashbackAmount = "";

                Debug.WriteLine("TransResult: " + message);
                // MessageBox.Show(message, "TransResult:", MessageBoxButton.OK);
            }

            async public void onRequestTransactionLog(String tlv)
            {
                String content = "transaction_log: \n";
                content += tlv;
                /*
                //this.txtDisplay.Dispatcher.Invoke(new Action(() =>
                {
                    this.txtDisplay.Text = content;
                }));
                */

                await this.txtDisplay.Dispatcher.InvokeAsync(() =>
                {
                    this.txtDisplay.Text = content;
                }, DispatcherPriority.Normal);

            }

            async public void onRequestBatchData(String tlv)
            {
                String content = "batch_data: \n";
                content += tlv;
                //txtDisplay.Text = content;
                await this.txtDisplay.Dispatcher.InvokeAsync(() =>
                {
                    this.txtDisplay.Text = content;
                }, DispatcherPriority.Normal);
                /*
                this.txtDisplay.Dispatcher.Invoke(new Action(() =>
                {
                    this.txtDisplay.Text = content;
                }));
                */
            }

            public void onRequestQposConnected()
            {
                MainWindow.deviceConnected = true;
                waitForBTConnectionResult.Set();
            }

            public void onRequestQposDisconnected()
            {
                Tip.d("onRequestQposConnected-connected");
            }

            public void onRequestNoQposDetected()
            {
                MainWindow.deviceConnected = false;
                waitForBTConnectionResult.Set();
            }

            async public void onRequestWaitingUser()
            {
                /*
                // txtDisplay.Text = "please input/swap the card";
                this.txtDisplay.Dispatcher.Invoke(new Action(() =>
                {
                    this.txtDisplay.Text = "please input/swap the card";
                }));
                */
                await this.txtDisplay.Dispatcher.InvokeAsync(() =>
                {
                    this.txtDisplay.Text = "please input/swap/tap the card";
                },DispatcherPriority.Normal);
            }

            public void onRequestSetPin()
            {
                pos.sendPin("1234");
                //pos.emptyPin();
            }

            async public void onReturnCustomConfigResult(bool isSuccess, String result)
            {

                await this.txtDisplay.Dispatcher.InvokeAsync(() =>
                {
                    this.txtDisplay.Text = result;
                }, DispatcherPriority.Normal);

                return;
            }
            async public void onReturnUpdateFirmwareResult(bool isSuccess, String result)
            {

                await this.txtDisplay.Dispatcher.InvokeAsync(() =>
                {
                    this.txtDisplay.Text = result;
                }, DispatcherPriority.Normal);

                return;
            }
            async public void onReturnUpdateEmvConfigResult(bool isSuccess, String result)
            {

                await this.txtDisplay.Dispatcher.InvokeAsync(() =>
                {
                    this.txtDisplay.Text = result;
                }, DispatcherPriority.Normal);

                return;
            }

            public void onRequestCalculateMac(String calMac)
            {

            }

            public void onGetCardNoResult(String cardNo)
            {

            }

            public void onReturniccCashBack(Dictionary<String, String> result)
            {

            }

            public void onReturnSetSleepTimeResult(bool isSuccess)
            {

            }

            public void onReturnApduResult(bool isSuccess, String apdu, int apduLen)
            {

            }

            public void onReturnPowerOffIccResult(bool isSuccess)
            {

            }

            public void onReturnPowerOnIccResult(bool isSuccess, String ksn, String atr, int atrLen)
            {

            }

            async public void onReturnGetPinResult(Dictionary<String, String> result)
            {
                String pinBlock = result["pinBlock"];
                String pinKsn = result["pinKsn"];
                String content = "get pin result\n";

                content += "pinKsn: " + " " + pinKsn + "\n";
                content += "pinBlock: " + " " + pinBlock + "\n";
                /*
                //txtDisplay.Text = content;
                this.txtDisplay.Dispatcher.Invoke(new Action(() =>
                {
                    this.txtDisplay.Text = content;
                }));
                */
                await this.txtDisplay.Dispatcher.InvokeAsync(() =>
                {
                    this.txtDisplay.Text = content;
                }, DispatcherPriority.Normal);
            }

            async public void onReturnReversalData(String tlv)
            {
                Tip.d("onRequestBatchData-\r\n");
                String content = "ReversalData\n";

                content += tlv;
                await this.txtDisplay.Dispatcher.InvokeAsync(() =>
                {
                    this.txtDisplay.Text = content;
                }, DispatcherPriority.Normal);

            }

            public void onRequestUpdateWorkKeyResult(QPOSService.UpdateInformationResult result)
            {

                Debug.WriteLine("result : " + result);

                return;
            }
            async public void onReturnUpdateFirmwareResult(QPOSService.UpdateInformationResult result)
            {
                String updateFirmware_result = "";
                String content = "";
                switch (result)
                {
                    case QPOSService.UpdateInformationResult.UPDATE_SUCCESS:
                        updateFirmware_result = "SUCCESS";
                        break;
                    case QPOSService.UpdateInformationResult.UPDATE_FAIL:
                        updateFirmware_result = "FAIL";
                        break;
                    case QPOSService.UpdateInformationResult.UPDATE_PACKET_VEFIRY_ERROR:
                        updateFirmware_result = "PACKET VERIFY ERROR";
                        break;
                    case QPOSService.UpdateInformationResult.UPDATE_LOWPOWER:
                        updateFirmware_result = "LOW POWER";
                        break;
                    case QPOSService.UpdateInformationResult.USB_RECONNECTING:
                        updateFirmware_result = "RECONNECTIONG";
                        break;
                }
                content += "Firmware Update status: " + updateFirmware_result + "\n";
                await this.txtDisplay.Dispatcher.InvokeAsync(() =>
                {
                    this.txtDisplay.Text = content;
                }, DispatcherPriority.Normal);
            }

            public void onRequestSignatureResult(byte[] paras)
            {

            }

            async public void onQposIdResult(Dictionary<String, String> posIdTable)
            {
                String posId = posIdTable["posId"] == null ? "" : posIdTable["posId"];

                String content = "";
                content += "posId: " + posId + "\n";

                /*
                this.txtDisplay.Dispatcher.Invoke(new Action(() =>
                {
                    this.txtDisplay.Text = content;
                }));
                */
                await this.txtDisplay.Dispatcher.InvokeAsync(() =>
                {
                    this.txtDisplay.Text = content;
                }, DispatcherPriority.Normal);

            }
            async public void onQposRSAResult(String rsaPubkey)
            {
                await this.txtDisplay.Dispatcher.InvokeAsync(() =>
                {
                    this.txtDisplay.Text = rsaPubkey;
                }, DispatcherPriority.Normal);
            }

            public void onReturnSetMasterKeyResult(bool isSuccess)
            {

            }

            public void onReturniccCashBack(Hashtable result)
            {
                throw new NotImplementedException();
            }
            async public void onReturnSendDeviceCommandString(bool isSuccess)
            {
                String content;
                if (isSuccess)
                {
                    content = "success";
                }
                else
                {

                    content = "fail";

                }
                await this.txtDisplay.Dispatcher.InvokeAsync(() =>
                {
                    this.txtDisplay.Text = content;
                }, DispatcherPriority.Normal);

            }
        }
        #endregion
    }
}
