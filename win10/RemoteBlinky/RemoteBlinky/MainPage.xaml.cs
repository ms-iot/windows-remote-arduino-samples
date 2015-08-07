using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.Maker.RemoteWiring;
using System;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace RemoteBlinky
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        RemoteDevice arduino;
        DispatcherTimer timer;
        PinState currentState;

        public MainPage()
        {
            this.InitializeComponent();

            arduino = App.Arduino;
            App.Telemetry.TrackEvent( "RemoteBlinky_Windows10_SuccessfullyConnected" );

            App.Arduino.DeviceConnectionLost += Arduino_OnDeviceConnectionLost;

            currentState = PinState.LOW;
            OnButton.IsEnabled = true;
            OffButton.IsEnabled = true;
            BlinkButton.IsEnabled = true;
        }

        private void Arduino_OnDeviceConnectionLost( string message )
        {
            ConnectionStatusMessage.Text = "Your device connection was lost!";
            
            if( timer != null )
            {
                timer.Stop();
                timer = null;
            }

            OnButton.IsEnabled = false;
            OffButton.IsEnabled = false;
            BlinkButton.IsEnabled = false;
        }

        private void OnButton_Click( object sender, RoutedEventArgs e )
        {
            //turn the LED connected to pin 13 ON
            currentState = PinState.HIGH;
            arduino.digitalWrite( 13, currentState );
        }

        private void OffButton_Click( object sender, RoutedEventArgs e )
        {
            //turn the LED connected to pin 13 OFF
            currentState = PinState.LOW;
            arduino.digitalWrite( 13, currentState );
        }

        private void ToggleButton_Click( object sender, RoutedEventArgs e )
        {
            if( timer == null )
            {
                timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds( 500 );
                timer.Tick += ToggleLed;
                timer.Start();
                BlinkButton.Content = "Stop Blinking!";
            }
            else
            {
                timer.Stop();
                timer = null;
                var obj = BlinkButton.Content as TextBlock;
                BlinkButton.Content = "Blink!";
            }
        }

        private void ToggleLed( object sender, object e )
        {
            currentState = ( currentState == PinState.LOW ? PinState.HIGH : PinState.LOW );
            arduino.digitalWrite( 13, currentState );
        }
    }
}
