using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Devices.Sensors;
using Microsoft.Maker.Serial;
using Microsoft.Maker.RemoteWiring;
using Windows.System.Display;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace remote_controlled_car
{
    /// <summary>
    /// This class controls the original Maisto PCB connected to an Arduino to drive forward and back while also optionally
    ///     turning left or right. It is nearly identical to ControlPage.xaml.cs, but the way in which this board is controlled is different.
    /// Unlike ControlPage, this class (ControlPageMaisto) is specific to the behavior of the Maisto PCB. The Maisto board uses two pins to control each motor.
    ///     Powering the left control pin moves the motor one direction, while powering the right control pin moves it in the other direction. The forward/back pins
    ///     work in the same way. Therefore, it is crucial not to attempt to power both of these pins at the same time.
    /// </summary>
    public sealed partial class ControlPageMaisto : Page
    {
        private enum Turn
        {
            none,
            left,
            right
        }

        private enum Direction
        {
            none,
            forward,
            reverse
        }

        private const double LR_MAG = 0.4;
        private const double FB_MAG = 0.5;
        private const double MAX_ANALOG_VALUE = 255.0;

        /*
         * You may need to modify these pin values depending on your motor shield / pin configuration.
         * For the Velleman ka03 motor shield that I used, the direction control pins determine if the motor is driven fwd or back
         *   while the motor control pins determine the drive power. Further, the mustang RC car uses a stalling motor on the front, meaning that
         *   analog power is not desired. Therefore, you will see the direction control pins being switched when the phone tilt changes from fwd/back
         *   and left/right, and you'll notice that the FB_MOTOR_CONTROL_PIN is driven with analogWrite while the LR_MOTOR_CONTROL_PIN is driven with digitalWrite
         */
        private const byte FORWARD_CONTROL_PIN = 8;
        private const byte REVERSE_CONTROL_PIN = 9;
        private const byte LEFT_CONTROL_PIN = 10;
        private const byte RIGHT_CONTROL_PIN = 11;

        private DisplayRequest keepScreenOnRequest;
        private Accelerometer accelerometer;
        private IStream bluetooth;
        private RemoteDevice arduino;
        private Turn turn;
        private Direction direction;

        public ControlPageMaisto()
        {
            this.InitializeComponent();

            turn = Turn.none;
            direction = Direction.none;

            App.Telemetry.TrackPageView( "RC_Car_ControlPageMaisto" );

            accelerometer = App.Accelerometer;
            bluetooth = App.Bluetooth;
            arduino = App.Arduino;

            if( accelerometer == null || bluetooth == null || arduino == null )
            {
                Frame.Navigate( typeof( MainPage ) );
                return;
            }

            startButton.IsEnabled = true;
            stopButton.IsEnabled = true;
            disconnectButton.IsEnabled = true;

            bluetooth.ConnectionLost += Bluetooth_ConnectionLost;

            keepScreenOnRequest = new DisplayRequest();
            keepScreenOnRequest.RequestActive();

            App.Arduino.pinMode( FORWARD_CONTROL_PIN, PinMode.OUTPUT );
            App.Arduino.pinMode( REVERSE_CONTROL_PIN, PinMode.OUTPUT );
            App.Arduino.pinMode( LEFT_CONTROL_PIN, PinMode.OUTPUT );
            App.Arduino.pinMode( RIGHT_CONTROL_PIN, PinMode.OUTPUT );
        }

        private void Bluetooth_ConnectionLost( string message )
        {
            stopAndReturn();
        }

        private void Accelerometer_ReadingChanged( Accelerometer sender, AccelerometerReadingChangedEventArgs accel )
        {
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () => UpdateUI( accel.Reading ) ) );

            //X is the left/right tilt, while Y is the fwd/rev tilt
            double lr = accel.Reading.AccelerationX;
            double fb = accel.Reading.AccelerationY;

            handleTurn( lr );
            handleDirection( fb );
        }

        private void handleTurn( double lr )
        {
            //left and right turns work best using digital signals

            if( lr < -LR_MAG )
            {
                //if we've switched directions, we need to be careful about how we switch
                if( turn != Turn.left )
                {
                    //make sure we aren't turning right
                    arduino.digitalWrite( RIGHT_CONTROL_PIN, PinState.LOW );
                }

                //start the motor by setting the pin high
                arduino.digitalWrite( LEFT_CONTROL_PIN, PinState.HIGH );
                turn = Turn.left;
            }
            else if( lr > LR_MAG )
            {
                if( turn != Turn.right )
                {
                    //make sure we aren't turning left
                    arduino.digitalWrite( LEFT_CONTROL_PIN, PinState.LOW );
                }

                //start the motor by setting the pin high
                arduino.digitalWrite( RIGHT_CONTROL_PIN, PinState.HIGH );
                turn = Turn.right;
            }
            else
            {
                //stop any pins that may be high
                arduino.digitalWrite( LEFT_CONTROL_PIN, PinState.LOW );
                arduino.digitalWrite( RIGHT_CONTROL_PIN, PinState.LOW );
                turn = Turn.none;
            }
        }

        private void handleDirection( double fb )
        {
            /*
             * The neutral state is anywhere from (-0.5, 0), so that the phone can be held like a controller, at a moderate angle.
             * This is because holding the phone at an angle is natural, tilting back to -1.0 is easy, while it feels awkward to tilt the phone
             *  forward beyond 0.5 Therefore, reverse is from [-1.0, -0.5] and forward is from [0, 0.5].
             *
             * if the tilt goes beyond -0.5 in the negative direction the phone is being tilted backwards, and the car will start to reverse.
             * if the tilt goes beyond 0 in the positive direction the phone is being tilted forwards, and the car will start to move forward.
             */

            if( fb < -FB_MAG )
            {
                //reading is less than the negative magnitude, the phone is being tilted back and the car should reverse
                if( direction != Direction.reverse )
                {
                    //make sure we aren't moving forward
                    arduino.digitalWrite( FORWARD_CONTROL_PIN, PinState.LOW );
                }

                //start the motor by setting the reverse pin high
                arduino.digitalWrite( REVERSE_CONTROL_PIN, PinState.HIGH );
                direction = Direction.reverse;
            }
            else if( fb > 0 )
            {
                //reading is greater than zero, the phone is being tilted forward and the car should move forward
                if( direction != Direction.forward )
                {
                    //make sure we aren't moving backward
                    arduino.digitalWrite( REVERSE_CONTROL_PIN, PinState.LOW );
                }

                //start the motor by setting the forward pin high
                arduino.digitalWrite( FORWARD_CONTROL_PIN, PinState.HIGH );
                direction = Direction.forward;
            }
            else
            {
                //reading is in the neutral zone (between -FB_MAG and 0) and the car should stop/idle
                arduino.digitalWrite( REVERSE_CONTROL_PIN, PinState.LOW );
                arduino.digitalWrite( FORWARD_CONTROL_PIN, PinState.LOW );
                direction = Direction.none;
            }
        }

        private void startButton_Click( object sender, RoutedEventArgs e )
        {
            if( accelerometer != null )
            {
                //lets slow down the report interval a bit so we don't overwhelm the Arduino
                accelerometer.ReportInterval = 100;
                accelerometer.ReadingChanged += Accelerometer_ReadingChanged;
            }
            App.Telemetry.TrackEvent( "RC_Car_StartDrivingButtonPressed" );
        }

        private void stopButton_Click( object sender, RoutedEventArgs e )
        {
            if( accelerometer != null )
            {
                accelerometer.ReadingChanged -= Accelerometer_ReadingChanged;
            }
            turn = Turn.none;
            direction = Direction.none;
            arduino.digitalWrite( LEFT_CONTROL_PIN, PinState.LOW );
            arduino.digitalWrite( RIGHT_CONTROL_PIN, PinState.LOW );
            arduino.digitalWrite( FORWARD_CONTROL_PIN, PinState.LOW );
            arduino.digitalWrite( REVERSE_CONTROL_PIN, PinState.LOW );
            App.Telemetry.TrackEvent( "RC_Car_StopDrivingButtonPressed" );
        }

        private void disconnectButton_Click( object sender, RoutedEventArgs e )
        {
            stopAndReturn();
            App.Telemetry.TrackEvent( "RC_Car_DisconnectButtonPressed" );
        }

        private void UpdateUI( AccelerometerReading reading )
        {
            statusTextBlock.Text = "getting data";

            // Show the numeric values.
            xTextBlock.Text = "X: " + reading.AccelerationX.ToString( "0.00" );
            yTextBlock.Text = "Y: " + reading.AccelerationY.ToString( "0.00" );
            zTextBlock.Text = "Z: " + reading.AccelerationZ.ToString( "0.00" );

            // Show the values graphically.
            xLine.X2 = xLine.X1 + reading.AccelerationX * 200;
            yLine.Y2 = yLine.Y1 - reading.AccelerationY * 200;
        }
        
        private void stopAndReturn()
        {
            stopButton_Click( null, null );
            App.Bluetooth.end();
            App.Bluetooth = null;
            App.Arduino = null;
            Frame.Navigate( typeof( MainPage ) );
        }
    }
}
