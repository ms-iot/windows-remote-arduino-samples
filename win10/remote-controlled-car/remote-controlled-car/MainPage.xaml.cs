using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Devices.Sensors;
using Microsoft.Maker.Serial;
using Microsoft.Maker.RemoteWiring;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace remote_controlled_car
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        //each numbered button on the first page will attempt to connect to the device with the corresponding numbered device name below
        //to connect to your own RC cars, you'll want to change the device name for at least one of these strings!
        private const string CAR_ONE_BLUETOOTH_DEVICE_NAME = "RNBT-773E";
        private const string CAR_TWO_BLUETOOTH_DEVICE_NAME = "RNBT-73B5";
        private const string CAR_THREE_BLUETOOTH_DEVICE_NAME = "RNBT-777E";

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

        private void carButton_Click( object sender, RoutedEventArgs e )
        {
            setButtonsEnabled( false );
            mTextBlock.Text = "Attempting to connect...";

            Button button = sender as Button;
            
            switch( button.Name )
            {
                case "carOneButton":
                    App.bluetooth = new BluetoothSerial( CAR_ONE_BLUETOOTH_DEVICE_NAME );
                    break;

                case "carTwoButton":
                    App.bluetooth = new BluetoothSerial( CAR_TWO_BLUETOOTH_DEVICE_NAME );
                    break;

                case "carThreeButton":
                    App.bluetooth = new BluetoothSerial( CAR_THREE_BLUETOOTH_DEVICE_NAME );
                    break;

                default:
                    App.bluetooth = new BluetoothSerial();
                    break;
            }

            App.bluetooth.ConnectionEstablished += Bluetooth_ConnectionEstablished;
            App.bluetooth.ConnectionFailed += Bluetooth_ConnectionFailed;
            App.arduino = new RemoteDevice( App.bluetooth );
            App.bluetooth.begin( 0, 0 );
        }

        private void Bluetooth_ConnectionFailed( string message )
        {
            App.bluetooth.ConnectionEstablished -= Bluetooth_ConnectionEstablished;
            App.bluetooth.ConnectionFailed -= Bluetooth_ConnectionFailed;
            setButtonsEnabled( true );
            mTextBlock.Text = "Connection failed.";
        }

        private void Bluetooth_ConnectionEstablished()
        {
            App.bluetooth.ConnectionEstablished -= Bluetooth_ConnectionEstablished;
            App.bluetooth.ConnectionFailed -= Bluetooth_ConnectionFailed;
            Frame.Navigate( typeof( ControlPage ) );
        }

        private void setButtonsEnabled( bool enabled )
        {
            carOneButton.IsEnabled = enabled;
            carTwoButton.IsEnabled = enabled;
            carThreeButton.IsEnabled = enabled;
        }
    }
}
