using System;
using Windows.Devices.Enumeration;
using Windows.Devices.Sensors;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Communication;
using Microsoft.Maker.Serial;
using Microsoft.Maker.RemoteWiring;
using System.Collections.Generic;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace remote_controlled_car
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private Connections _connections = null;
        DateTime connectionAttemptStartedTime;

        public MainPage()
        {
            this.InitializeComponent();

            App.Telemetry.TrackPageView( "RC_Car_MainPage" );
            App.Accelerometer = Accelerometer.GetDefault();
            if( App.Accelerometer == null )
            {
                // The device on which the application is running does not support
                // the accelerometer sensor. Alert the user and disable the
                // Start and Stop buttons.
                App.Telemetry.TrackEvent( "RC_Car_NoAccelerometer" );
                mTextBlock.Text = "device does not support accelerometer";
                setButtonsEnabled( false );
            }
        }

        protected override void OnNavigatedTo( NavigationEventArgs e )
        {
            base.OnNavigatedTo( e );
            if( _connections == null )
            {
                RefreshDeviceList();
            }
        }

        private void RefreshDeviceList()
        {
            //invoke the listAvailableDevicesAsync method of BluetoothSerial. Since it is Async, we will wrap it in a Task and add a llambda to execute when finished
            BluetoothSerial.listAvailableDevicesAsync().AsTask<DeviceInformationCollection>().ContinueWith( listTask =>
            {
                //store the result and populate the device list on the UI thread
                var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
                {
                    _connections = new Connections();
                    foreach( DeviceInformation device in listTask.Result )
                    {
                        _connections.Add( new Connection( device.Name, device ) );
                    }
                    connectList.ItemsSource = _connections;
                } ) );
            } );
        }

        private void Refresh_Click( object sender, RoutedEventArgs e )
        {
            RefreshDeviceList();
        }

        private void Reconnect_Click( object sender, RoutedEventArgs e )
        {
            if( connectList.SelectedItem != null )
            {
                setButtonsEnabled( false );
                mTextBlock.Text = "Connecting...";

                var selectedConnection = connectList.SelectedItem as Connection;
                var device = selectedConnection.Source as DeviceInformation;

                //send telemetry about this connection attempt
                var properties = new Dictionary<string, string>();
                properties.Add( "Device_Name", device.Name );
                properties.Add( "Device_ID", device.Id );
                properties.Add( "Device_Kind", device.Kind.ToString() );
                App.Telemetry.TrackEvent( "RC_Car_Bluetooth_Connection_Attempt", properties );

                //construct the bluetooth serial object with the specified device
                App.Bluetooth = new BluetoothSerial( device );
                App.Arduino = new RemoteDevice( App.Bluetooth );

                App.Arduino.DeviceReady += Arduino_OnDeviceReady;
                App.Arduino.DeviceConnectionFailed += Arduino_OnDeviceConnectionFailed;

                connectionAttemptStartedTime = DateTime.Now;
                App.Bluetooth.begin( 115200, 0 );
            }
        }

        private void Arduino_OnDeviceConnectionFailed( string message )
        {
            App.Bluetooth.ConnectionEstablished -= Arduino_OnDeviceReady;
            App.Bluetooth.ConnectionFailed -= Arduino_OnDeviceConnectionFailed;
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                setButtonsEnabled( true );
                mTextBlock.Text = "Connection failed.";

                //telemetry
                App.Telemetry.TrackRequest( "Connection_Failed_Event", DateTimeOffset.Now, DateTime.Now - connectionAttemptStartedTime, message, true );
            } ) );
        }

        private void Arduino_OnDeviceReady()
        {
            App.Bluetooth.ConnectionEstablished -= Arduino_OnDeviceReady;
            App.Bluetooth.ConnectionFailed -= Arduino_OnDeviceConnectionFailed;
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                //telemetry
                App.Telemetry.TrackRequest( "RC_Car_Connection_Success_Event", DateTimeOffset.Now, DateTime.Now - connectionAttemptStartedTime, string.Empty, true );

                Frame.Navigate( typeof( ControlPage ) );
            } ) );
        }

        private void setButtonsEnabled( bool enabled )
        {
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                Reconnect.IsEnabled = enabled;
                Refresh.IsEnabled = enabled;
            } ) );
        }
    }
}
