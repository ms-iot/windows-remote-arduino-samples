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
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ControlPage : Page
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
        private const byte FB_DIRECTION_CONTROL_PIN = 8;
        private const byte FB_MOTOR_CONTROL_PIN = 9;
        private const byte LR_DIRECTION_CONTROL_PIN = 2;
        private const byte LR_MOTOR_CONTROL_PIN = 3;
		
        private const PinState LEFT = PinState.LOW;
        private const PinState RIGHT = PinState.HIGH;
        private const PinState FORWARD = PinState.LOW;
        private const PinState REVERSE = PinState.HIGH;

        private DisplayRequest keepScreenOnRequest;
        private Accelerometer accelerometer;
        private BluetoothSerial bluetooth;
        private RemoteDevice arduino;
        private Turn turn;
        private Direction direction;

        public ControlPage()
        {
            this.InitializeComponent();

            turn = Turn.none;
            direction = Direction.none;

            accelerometer = App.accelerometer;
            bluetooth = App.bluetooth;
            arduino = App.arduino;

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

            App.arduino.pinMode( LR_DIRECTION_CONTROL_PIN, PinMode.OUTPUT );
            App.arduino.pinMode( FB_DIRECTION_CONTROL_PIN, PinMode.OUTPUT );
            App.arduino.pinMode( LR_MOTOR_CONTROL_PIN, PinMode.PWM );
            App.arduino.pinMode( FB_MOTOR_CONTROL_PIN, PinMode.PWM );
        }

        private void Bluetooth_ConnectionLost()
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
                    //stop motor & set direction left
                    arduino.digitalWrite( LR_MOTOR_CONTROL_PIN, PinState.LOW );
                    arduino.digitalWrite( LR_DIRECTION_CONTROL_PIN, LEFT );
                }

                //start the motor by setting the pin high
                arduino.digitalWrite( LR_MOTOR_CONTROL_PIN, PinState.HIGH );
                turn = Turn.left;
            }
            else if( lr > LR_MAG )
            {
                if( turn != Turn.right )
                {
                    //stop motor & set direction right
                    arduino.digitalWrite( LR_MOTOR_CONTROL_PIN, PinState.LOW );
                    arduino.digitalWrite( LR_DIRECTION_CONTROL_PIN, RIGHT );
                }

                //start the motor by setting the pin high
                arduino.digitalWrite( LR_MOTOR_CONTROL_PIN, PinState.HIGH );
                turn = Turn.right;
            }
            else
            {
                //stop the motor
                arduino.digitalWrite( LR_MOTOR_CONTROL_PIN, PinState.LOW );
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
                double weight = -( fb + FB_MAG );
                byte analogVal = mapWeight( weight );

                if( direction != Direction.reverse )
                {
                    //stop motor & set direction forward
                    arduino.analogWrite( FB_MOTOR_CONTROL_PIN, 0 );
                    arduino.digitalWrite( FB_DIRECTION_CONTROL_PIN, REVERSE );
                }

                //start the motor by setting the pin to the appropriate analog value
                arduino.analogWrite( FB_MOTOR_CONTROL_PIN, analogVal );
                direction = Direction.reverse;
            }
            else if( fb > 0 )
            {
                //reading is greater than zero, the phone is being tilted forward and the car should move forward
                byte analogVal = mapWeight( fb );

                if( direction != Direction.forward )
                {
                    //stop motor & set direction forward
                    arduino.analogWrite( FB_MOTOR_CONTROL_PIN, 0 );
                    arduino.digitalWrite( FB_DIRECTION_CONTROL_PIN, FORWARD );
                }

                //start the motor by setting the pin to the appropriate analog value
                arduino.analogWrite( FB_MOTOR_CONTROL_PIN, analogVal );
                direction = Direction.forward;
            }
            else
            {
                //reading is in the neutral zone (between -FB_MAG and 0) and the car should stop/idle
                arduino.analogWrite( FB_MOTOR_CONTROL_PIN, 0 );
            }
        }

        private byte mapWeight( double weight )
        {
            //the value should be [0, 0.5], but we want to clamp the value between [0, 1]
            weight = Math.Max( Math.Min( weight * 2, 1.0 ), 0.0 );
            return (byte)( weight * MAX_ANALOG_VALUE );
        }

        private void startButton_Click( object sender, RoutedEventArgs e )
        {
            if( accelerometer != null )
            {
                //lets slow down the report interval a bit so we don't overwhelm the Arduino
                accelerometer.ReportInterval = 100;
                accelerometer.ReadingChanged += Accelerometer_ReadingChanged;
            }
        }

        private void stopButton_Click( object sender, RoutedEventArgs e )
        {
            if( accelerometer != null )
            {
                accelerometer.ReadingChanged -= Accelerometer_ReadingChanged;
            }
        }

        private void disconnectButton_Click( object sender, RoutedEventArgs e )
        {
            stopAndReturn();
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
            App.bluetooth.end();
            App.bluetooth = null;
            App.arduino = null;
            Frame.Navigate( typeof( MainPage ) );
        }
    }
}
