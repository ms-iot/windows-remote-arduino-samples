using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.Maker.Serial;
using Microsoft.Maker.RemoteWiring;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace RemoteBlinky
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        //Usb is not supported on Win8.1. To see the USB connection steps, refer to the win10 solution instead.
        BluetoothSerial bluetooth;
        RemoteDevice arduino;

        public MainPage()
        {
            this.InitializeComponent();

            /*
             * I've written my bluetooth device name as a parameter to the BluetoothSerial constructor. You should change this to your previously-paired
             * device name if using Bluetooth. You can also use the BluetoothSerial.listAvailableDevicesAsync() function to list
             * available devices, but that is not covered in this sample.
             */
            bluetooth = new BluetoothSerial("RNBT-E072");

            arduino = new RemoteDevice(bluetooth);
            bluetooth.ConnectionEstablished += OnConnectionEstablished;

            //these parameters don't matter for bluetooth
            bluetooth.begin(0, 0);
        }

        private void OnConnectionEstablished()
        {
            //enable the buttons on the UI thread!
            var action = Dispatcher.RunAsync( Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler( () => {
                OnButton.IsEnabled = true;
                OffButton.IsEnabled = true;
            }));
        }

        private void OnButton_Click( object sender, RoutedEventArgs e )
        {
            //turn the LED connected to pin 5 ON
            arduino.digitalWrite( 5, PinState.HIGH );
        }

        private void OffButton_Click( object sender, RoutedEventArgs e )
        {
            //turn the LED connected to pin 5 OFF
            arduino.digitalWrite( 5, PinState.LOW );
        }
    }
}
