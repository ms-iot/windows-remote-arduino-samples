using System;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Enumeration;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Devices.Sensors;
using Microsoft.Maker.Serial;
using Microsoft.Maker.RemoteWiring;
using remote_controlled_car.Communication;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace remote_controlled_car
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private Connections _connections = null;

        public MainPage()
        {
            this.InitializeComponent();

            App.accelerometer = Accelerometer.GetDefault();
            if( App.accelerometer == null )
            {
                // The device on which the application is running does not support
                // the accelerometer sensor. Alert the user and disable the
                // Start and Stop buttons.
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

                //construct the bluetooth serial object with the specified device
                App.bluetooth = new BluetoothSerial( device );

                App.bluetooth.ConnectionEstablished += Bluetooth_ConnectionEstablished;
                App.bluetooth.ConnectionFailed += Bluetooth_ConnectionFailed;
                App.arduino = new RemoteDevice( App.bluetooth );
                App.bluetooth.begin( 115200, 0 );
            }
        }

        private void Bluetooth_ConnectionFailed()
        {
            App.bluetooth.ConnectionEstablished -= Bluetooth_ConnectionEstablished;
            App.bluetooth.ConnectionFailed -= Bluetooth_ConnectionFailed;
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
                setButtonsEnabled( true );
                mTextBlock.Text = "Connection failed.";
            } ) );
        }

        private void Bluetooth_ConnectionEstablished()
        {
            App.bluetooth.ConnectionEstablished -= Bluetooth_ConnectionEstablished;
            App.bluetooth.ConnectionFailed -= Bluetooth_ConnectionFailed;
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () =>
            {
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
