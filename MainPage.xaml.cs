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
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using System.Collections.ObjectModel;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using System.Threading.Tasks;
using System.Threading;
using System.Text;
using WindowsBluetooth;
using Windows.Devices.Gpio;
using System.Diagnostics;
using System.Net.Http;
using Windows.Devices.SmartCards;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;



// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace NotifyMe
{
    /// <summary>
    /// The Main Page for the app
    /// 
    /// 
    /// 
    /// 
    /// </summary>
    public sealed partial class MainPage : Page
    {
        string Title = "Child Notification";

        // For the Bluetooth use
        private Windows.Devices.Bluetooth.Rfcomm.RfcommDeviceService _service;
        private StreamSocket _socket;
        private DataWriter dataWriterObject;
        private DataReader dataReaderObject;
        ObservableCollection<PairedDeviceInfo> _pairedDevices;
        private CancellationTokenSource ReadCancellationTokenSource;
        String response;

        //A class which wraps the barometric sensor
        BMP280 BMP280; //temperature sensor 
        MCP3008 MCP3008; //analog to digital

        // temporary ( the car is off ) 
        Boolean carStatus = false;
        public static Boolean ConnectedToOBD = false;

        long RpmResponse;
        static int reconnect = 0;
        String recTextFromObd;

        // Values for which channels we will be using from the ADC chip
        const byte LowPotentiometerADCChannel = 0;

        // The number of pins in the Raspberry pi 
        private const int LED_PIN = 27;
        private const int PB_PIN = 5;
        private const int PIR_SENSOR = 19; 
        private GpioPin pin;
        private GpioPin pir;
        private GpioPinValue pirvalue;
        private DispatcherTimer timer;

        float weight;
        int lowPotReadVal;


        public MainPage()
        {
            InitializeComponent();
            MyTitle.Text = Title;

            InitAll(); // Init all GPIO, MCP3008, BMP280 and Bluetooth

            
            // Initilize The timer for 500 Milliseconds 
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(4000);
            timer.Tick += Timer_Tick;
            timer.Start();
            Unloaded += MainPage_Unloaded;
            

       }


        /*  
        *  Initialize Bluetooth select The OBDII from all paired devices (Paired manually to the raspberryPi) and paired it 
        *  Enter mode 0100 in the OBD by sending the OBD The command 010C and start listen for any response from the OBD.
        */
        async Task InitializeRfcommDeviceService()
        {
            
            try
            {
                DeviceInformationCollection DeviceInfoCollection = await DeviceInformation.FindAllAsync(RfcommDeviceService.GetDeviceSelector(RfcommServiceId.SerialPort));

                // Get the number of the paired devices to the RaspberryPi
                var numDevices = DeviceInfoCollection.Count();

                // By clearing the backing data, we are effectively clearing the ListBox
                _pairedDevices = new ObservableCollection<PairedDeviceInfo>();
                _pairedDevices.Clear();

                // If there is no paired devices to The raspberryPi 
                if (numDevices == 0)
                {
                    //MessageDialog md = new MessageDialog("No paired devices found", "Title");
                    //await md.ShowAsync();
                    System.Diagnostics.Debug.WriteLine("InitializeRfcommDeviceService: No paired devices found.");
                }
                // Found some paired devices.
                else
                {
                    
                    foreach (var deviceInfo in DeviceInfoCollection)
                    {
                        _pairedDevices.Add(new PairedDeviceInfo(deviceInfo));

                    }
                }
                PairedDevices.Source = _pairedDevices;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("InitializeRfcommDeviceService: " + ex.Message);
            }
           
            
            return ;
        }

        private async Task connectingToOBD()
        {

            if (ConnectedToOBD == false || reconnect > 3)
            {
                reconnect = 0;
                await InitializeRfcommDeviceService();
                await Task.Delay(3000);
                // select The OBD device and pair it 
                for (int i = 0; i < ConnectDevices.Items.Count(); i++)
                {
                    PairedDeviceInfo currDev = (PairedDeviceInfo)ConnectDevices.Items[i];
                    if (currDev.Name.Equals("OBDII"))
                    {
                        //connecting to OBD
                        connectDev();
                        ConnectedToOBD = true;
                    }
                }

                // Entering mode 0100 
                //  Send("010C\r");
                //  await Task.Delay(1000);
                // start listen for any response from the obd
                Send("010C\r");
                await Task.Delay(3000);
                Listen();
                
                Send("010C\r");
                await Task.Delay(3000);

            }

            return;
        }

        async private void getresponse()
        {
            if (ConnectedToOBD == true)
            {

                //request from obd the rpm
                Send("010C\r");
               await Task.Delay(3000);

                /* for test
                for (int i = 0; i < 8; i++)
                {
                    Send("010C\r");
                    await Task.Delay(3000);
                }
                */

                // The Obd connected successfuly 
                if (recTextFromObd != null)
                {
                    ConnectedToOBD = true;
                    Debug.WriteLine(recTextFromObd.ToString());
                    if (recTextFromObd.Contains("41") && recTextFromObd.Contains("0C"))
                    {
                        carStatus = true;
                        string[] arr = recTextFromObd.Split(' ');
                        response = arr[arr.Length - 3] + arr[arr.Length - 2];
                        Debug.WriteLine(response);
                        int res = Convert.ToInt32(response,16);
                        Debug.WriteLine("converted result : " + res/4);
                        recTextFromObd = "";
                    }
                    else if (recTextFromObd.Contains("SEARCHING") || recTextFromObd.Contains("STOPPED") || recTextFromObd.Contains("UNABLE TO CONNECT") || recTextFromObd.Contains("NO DATA"))
                    {
                        RpmResponse = 0;
                        carStatus = false;
                        recTextFromObd = "";
                    }
                    /*if (recTextFromObd.Split('\r').Length > 2)
                    {
                        //response = recTextFromObd.Split('\r')[2];
                        if (response != null)
                        {
                            
                            if (recTextFromObd.Substring(0, 2).Equals("41"))
                            {
                                //translating the received data from the OBD
                                string tmp1, tmp2;
                                tmp1 = recTextFromObd.Substring(6, 2);
                                tmp2 = recTextFromObd.Substring(9, 2);
                                RpmResponse = Convert.ToInt64(string.Concat(tmp1, tmp2), 16);
                                carStatus = true;

                            }
                            else if (string.Equals(recTextFromObd.Substring(0, 1), "N", StringComparison.OrdinalIgnoreCase) || string.Equals(recTextFromObd.Substring(0, 1), "C", StringComparison.OrdinalIgnoreCase))
                            {
                                carStatus = false;
                            }
                            else
                            {
                                carStatus = false;
                                // OBD no response - Emergency mode
                                RpmResponse = -1;
                            }
                        }
                    }*/

                        else
                        {
                            RpmResponse = -1;
                            recTextFromObd = "";
                        }

                    }
                    else
                    {
                        // didn't get a response 
                        RpmResponse = -1;
                        reconnect++;
                    }
               }
            }

        // After finding the OBDII ,this function make the Pairing 
        async private void connectDev()
        {
            //Revision: No need to requery for Device Information as we alraedy have it:
            DeviceInformation DeviceInfo; // = await DeviceInformation.CreateFromIdAsync(this.TxtBlock_SelectedID.Text);
            PairedDeviceInfo pairedDevice = (PairedDeviceInfo)ConnectDevices.SelectedItem;
            DeviceInfo = pairedDevice.DeviceInfo;

            bool success = true;
            try
            {
                _service = await RfcommDeviceService.FromIdAsync(DeviceInfo.Id);
               
                if (_socket != null)
                {
                    // Disposing the socket with close it and release all resources associated with the socket
                    _socket.Dispose();
                }

                _socket = new StreamSocket();
                try
                {
                    // Note: If either parameter is null or empty, the call will throw an exception
                    await _socket.ConnectAsync(_service.ConnectionHostName, _service.ConnectionServiceName);
                }
                catch (Exception ex)
                {
                    success = false;
                    System.Diagnostics.Debug.WriteLine("Bluetooth Connect:" + ex.Message);
                }
                // If the connection was successful, the RemoteAddress field will be populated
                if (success)
                {
                    
                    this.buttonDisconnect.IsEnabled = true;
                    this.buttonSend.IsEnabled = true;
                    this.buttonStartRecv.IsEnabled = true;
                    this.buttonStopRecv.IsEnabled = false;
                    string msg = String.Format("Connected to {0}!", _socket.Information.RemoteAddress.DisplayName);
                    //MessageDialog md = new MessageDialog(msg, Title);
                    System.Diagnostics.Debug.WriteLine(msg);
                    //await md.ShowAsync();
                  
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Overall Connect: " + ex.Message);
                _socket.Dispose();
                _socket = null;
            }
            
        }


        /*
         *  make The connection to the device that was selected in the GUI.
         *   
         */
        async private void ConnectDevice_Click(object sender, RoutedEventArgs e)
        {
            //Revision: No need to requery for Device Information as we alraedy have it:
            DeviceInformation DeviceInfo; // = await DeviceInformation.CreateFromIdAsync(this.TxtBlock_SelectedID.Text);
            PairedDeviceInfo pairedDevice = (PairedDeviceInfo)ConnectDevices.SelectedItem;
            DeviceInfo = pairedDevice.DeviceInfo;

            bool success = true;
            try
            {
                _service = await RfcommDeviceService.FromIdAsync(DeviceInfo.Id);

                if (_socket != null)
                {
                    // Disposing the socket with close it and release all resources associated with the socket
                    _socket.Dispose();
                }

                _socket = new StreamSocket();
                try
                {
                    // Note: If either parameter is null or empty, the call will throw an exception
                    await _socket.ConnectAsync(_service.ConnectionHostName, _service.ConnectionServiceName);
                }
                catch (Exception ex)
                {
                    success = false;
                    System.Diagnostics.Debug.WriteLine("Connect:" + ex.Message);
                }
                // If the connection was successful, the RemoteAddress field will be populated
                if (success)
                {
                    this.buttonDisconnect.IsEnabled = true;
                    this.buttonSend.IsEnabled = true;
                    this.buttonStartRecv.IsEnabled = true;
                    this.buttonStopRecv.IsEnabled = false;
                    string msg = String.Format("Connected to {0}!", _socket.Information.RemoteAddress.DisplayName);
                    //MessageDialog md = new MessageDialog(msg, Title);
                    System.Diagnostics.Debug.WriteLine(msg);
                    //await md.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Overall Connect: " + ex.Message);
                _socket.Dispose();
                _socket = null;
            }
        }


        private void ConnectDevices_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            PairedDeviceInfo pairedDevice = (PairedDeviceInfo)ConnectDevices.SelectedItem;
            this.TxtBlock_SelectedID.Text = pairedDevice.ID;
            this.textBlockBTName.Text = pairedDevice.Name;
            ConnectDevice_Click(sender, e);


        }

        //Windows.Storage.Streams.Buffer InBuff;
        //Windows.Storage.Streams.Buffer OutBuff;
        //private StreamSocket _socket;
        private async void button_Click(object sender, RoutedEventArgs e)
        {
            //OutBuff = new Windows.Storage.Streams.Buffer(100);
            Button button = (Button)sender;
            if (button != null)
            {
                switch ((string)button.Content)
                {
                    case "Disconnect":
                        await this._socket.CancelIOAsync();
                        _socket.Dispose();
                        _socket = null;
                        this.textBlockBTName.Text = "";
                        this.TxtBlock_SelectedID.Text = "";
                        this.buttonDisconnect.IsEnabled = false;
                        this.buttonSend.IsEnabled = false;
                        this.buttonStartRecv.IsEnabled = false;
                        this.buttonStopRecv.IsEnabled = false;
                        break;
                    case "Send":
                        //await _socket.OutputStream.WriteAsync(OutBuff);
                        // Send(this.textBoxSendText.Text);
                        Send("010C\r");
                        this.textBoxSendText.Text = "";
                        break;
                    case "Clear Send":
                        this.textBoxRecvdText.Text = "";
                        break;
                    case "Start Recv":
                        this.buttonStartRecv.IsEnabled = false;
                        this.buttonStopRecv.IsEnabled = true;
                        Listen();
                        break;
                    case "Stop Recv":
                        this.buttonStartRecv.IsEnabled = false;
                        this.buttonStopRecv.IsEnabled = false;
                        CancelReadTask();
                        break;
                    case "Refresh":
                        await InitializeRfcommDeviceService();
                        break;
                }
            }
        }


        public async void Send(string msg)
        {
            try
            {
                if (_socket.OutputStream != null)
                {
                    // Create the DataWriter object and attach to OutputStream
                    dataWriterObject = new DataWriter(_socket.OutputStream);

                    //Launch the WriteAsync task to perform the write
                    await WriteAsync(msg);
                }
                else
                {
                    //status.Text = "Select a device and connect";
                }
            }
            catch (Exception ex)
            {
                //status.Text = "Send(): " + ex.Message;
               // System.Diagnostics.Debug.WriteLine("Send(): " + ex.Message);
            }
            finally
            {
                // Cleanup once complete
                if (dataWriterObject != null)
                {
                    dataWriterObject.DetachStream();
                    dataWriterObject = null;
                }
            }
            return;
        }

        /// <summary>
        /// WriteAsync: Task that asynchronously writes data from the input text box 'sendText' to the OutputStream 
        /// </summary>
        /// <returns></returns>
        private async Task WriteAsync(string msg)
        {
            Task<UInt32> storeAsyncTask;

            if (msg == "")
                msg = "none";// sendText.Text;
            if (msg.Length != 0)
            //if (msg.sendText.Text.Length != 0)
            {
                // Load the text from the sendText input text box to the dataWriter object
                dataWriterObject.WriteString(msg);

                // Launch an async task to complete the write operation
                storeAsyncTask = dataWriterObject.StoreAsync().AsTask();

                UInt32 bytesWritten = await storeAsyncTask;
                if (bytesWritten > 0)
                {
                    string status_Text = msg + ", ";
                    status_Text += bytesWritten.ToString();
                    status_Text += " bytes written successfully!";
                    System.Diagnostics.Debug.WriteLine(status_Text);
                }
            }
            else
            {
                string status_Text2 = "Enter the text you want to write and then click on 'WRITE'";
                System.Diagnostics.Debug.WriteLine(status_Text2);
            }
        }



        /// <summary>
        /// - Create a DataReader object
        /// - Create an async task to read from the SerialDevice InputStream
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Listen()
        {
            try
            {
                ReadCancellationTokenSource = new CancellationTokenSource();
                if (_socket.InputStream != null) 
                {
                    dataReaderObject = new DataReader(_socket.InputStream);
                    this.buttonStopRecv.IsEnabled = true;
                    this.buttonDisconnect.IsEnabled = false;
                    // keep reading the serial input
                    while (true)
                    {
                        await ReadAsync(ReadCancellationTokenSource.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                this.buttonStopRecv.IsEnabled = false;
                this.buttonStartRecv.IsEnabled = false;
                this.buttonSend.IsEnabled = false;
                this.buttonDisconnect.IsEnabled = false;
                this.textBlockBTName.Text = "";
                this.TxtBlock_SelectedID.Text = "";
                if (ex.GetType().Name == "TaskCanceledException")
                {
                    System.Diagnostics.Debug.WriteLine("Listen: Reading task was cancelled, closing device and cleaning up");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Listen: " + ex.Message);
                }
            }
            finally
            {
                // Cleanup once complete
                if (dataReaderObject != null)
                {
                    dataReaderObject.DetachStream();
                    dataReaderObject = null;
                }
            }
        }

        /// <summary>
        /// ReadAsync: Task that waits on data and reads asynchronously from the serial device InputStream
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task ReadAsync(CancellationToken cancellationToken)
        {
            Task<UInt32> loadAsyncTask;

            uint ReadBufferLength = 1024;

            // If task cancellation was requested, comply
            cancellationToken.ThrowIfCancellationRequested();

            // Set InputStreamOptions to complete the asynchronous read operation when one or more bytes is available
            dataReaderObject.InputStreamOptions = InputStreamOptions.Partial;

            // Create a task object to wait for data on the serialPort.InputStream
            loadAsyncTask = dataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken);

            // Launch the task and wait
            UInt32 bytesRead = await loadAsyncTask;
            if (bytesRead > 0)
            {
                try
                {
                    string recvdtxt = dataReaderObject.ReadString(bytesRead);
                    recTextFromObd += recvdtxt.ToString();
                    System.Diagnostics.Debug.Write("\r\rOBD response is: \r" + recvdtxt);
                    this.textBoxRecvdText.Text += recvdtxt;
                    /*if (_Mode == Mode.JustConnected)
                    {
                        if (recvdtxt[0] == ArduinoLCDDisplay.keypad.BUTTON_SELECT_CHAR)
                        {
                            _Mode = Mode.Connected;

                            //Reset back to Cmd = Read sensor and First Sensor
                            await Globals.MP.UpdateText("@");
                            //LCD Display: Fist sensor and first comamnd
                            string lcdMsg = "~C" + Commands.Sensors[0];
                            lcdMsg += "~" + ArduinoLCDDisplay.LCD.CMD_DISPLAY_LINE_2_CH + Commands.CommandActions[1] + "           ";
                            Send(lcdMsg);

                            backButton_Click(null, null);
                        }
                    }
                    else if (_Mode == Mode.Connected)
                    {
                        await Globals.MP.UpdateText(recvdtxt);
                        recvdText.Text = "";
                        status.Text = "bytes read successfully!";
                    }*/
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("ReadAsync: " + ex.Message);
                }

            }
        }

        /// <summary>
        /// CancelReadTask:
        /// - Uses the ReadCancellationTokenSource to cancel read operations
        /// </summary>
        private void CancelReadTask()
        {
            if (ReadCancellationTokenSource != null)
            {
                if (!ReadCancellationTokenSource.IsCancellationRequested)
                {
                    ReadCancellationTokenSource.Cancel();
                }
            }
        }


        /// <summary>
        ///  Class to hold all paired device information
        /// </summary>
        public class PairedDeviceInfo
        {
            internal PairedDeviceInfo(DeviceInformation deviceInfo)
            {
                this.DeviceInfo = deviceInfo;
                this.ID = this.DeviceInfo.Id;
                this.Name = this.DeviceInfo.Name;
            }

            public string Name { get; private set; }
            public string ID { get; private set; }
            public DeviceInformation DeviceInfo { get; private set; }
        }


        private async void InitAll()
        {


            //await InitializeRfcommDeviceService();


            var gpio = GpioController.GetDefault();

            if (gpio == null)
            {
                pin = null;
                GpioStatus.Text = "There is no GPIO controller on this device.";
                return;
            }
            //Set pin as output 0v .
            pin = gpio.OpenPin(LED_PIN);
            pin.Write(GpioPinValue.Low);
            pin.SetDriveMode(GpioPinDriveMode.Output);
            //Set pir as input 
            pir = gpio.OpenPin(PIR_SENSOR);
            pir.SetDriveMode(GpioPinDriveMode.Input);
            

            GpioStatus.Text = "GPIO pin initialized correctly.";
            try
            {
                //Create a new object for our temperature sensor class
                BMP280 = new BMP280();
                //Initialize the sensor
                await BMP280.Initialize();

                await BMP280.callTemp();

                // Initialize the ADC chip for use
                MCP3008 = new MCP3008(3.3F);
                await MCP3008.Initialize();
                Debug.WriteLine("mcp300 init Done ");
                if (MCP3008 == null)
                {
                    Debug.WriteLine("MCP3008 Error");
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("tempurture / MCP3008 can not initialized! ");
            }
        }

        private void MainPage_Unloaded(object sender, object args)
        {
            pin.Dispose();

        }

        private async void Timer_Tick(object sender, object e)
        {

            await connectingToOBD();
            await Task.Delay(9000);
            getresponse();
            await Task.Delay(9000);

            // carstatus is OBD.rpm();
            //The car is off 
            if (carStatus == false && RpmResponse != -1 && ConnectedToOBD == true)
            {
                // Read from the ADC chip the current values of the two pots and the photo cell.
                lowPotReadVal = MCP3008.ReadADC(LowPotentiometerADCChannel);
                // convert the ADC readings to voltages to make them more friendly.
                weight = MCP3008.ADCToVoltage(lowPotReadVal);
                Debug.WriteLine("Cheking if the baby sitting.");
                Debug.WriteLine("The weight is : " + weight);
                //  pushButtonValue = pushButton.Read();
                // Someone is sitting on the seat
                if (weight > 1) // 3.2V is 3KG ( messured ) 
                {
                    pin.Write(GpioPinValue.Low); // turn the led on 
                    Task<string> tmp = null;
                    // call the IR Sensor .. 
                    pirvalue = pir.Read();
                    Debug.WriteLine("Checking motion sensor.");
                    if (pirvalue == GpioPinValue.Low)
                    {
                        Debug.WriteLine("No motion detection!");
                        tmp = BMP280.callTemp();
                        Debug.WriteLine(tmp);
                        string[] arr = ("" + tmp.Result).Split('.');
                        if (!tmp.Result.Equals("can't calculate Temperature") && int.Parse(arr[0]) > 40)
                        {
                            Emergency();
                        }
                    }
                    else if (pirvalue == GpioPinValue.High)
                    {
                        Debug.WriteLine("Motion detected.\rThe baby in the car !!!");
                        Emergency();

                    }
                }
                // No body is sitting 
                else
                {
                    pin.Write(GpioPinValue.High); // turn the led off 
                }
            }
            // The car is ON 
            else if (carStatus == true && RpmResponse != -1 && ConnectedToOBD == true)
            {
                // The car is ON - write the rpm 
                Debug.Write(RpmResponse);
            }

            // Something went wrong with the obd - entering emergency mode check The tempareture
            else if (RpmResponse == -1 && ConnectedToOBD == true)
            {
                Debug.WriteLine("Can't get OBD response.\rCheking Temprature.");
                Task<string> tmp = null;
                tmp = BMP280.callTemp();
                string[] arr = ("" + tmp.Result).Split('.');
                // check the temperature and take action 
                if (!tmp.Result.Equals("can't calculate Temperature") && int.Parse(arr[0]) > 40)
                {
                    // Read from the ADC chip the current values of the two pots and the photo cell.
                    lowPotReadVal = MCP3008.ReadADC(LowPotentiometerADCChannel);
                    // convert the ADC readings to voltages to make them more friendly.
                    weight = MCP3008.ADCToVoltage(lowPotReadVal);
                    pirvalue = pir.Read();
                    // The baby is sitting and the temperature is > 40 
                    // It doesn't mind if there is a motion because the temp is > 40 and could be that the baby is sleeping
                    Debug.WriteLine("Cheking Fsr sensor if the baby is sitting.");
                    if (weight > 1)
                    {
                        Emergency();

                    }
                }
            }
            else if(ConnectedToOBD == false)
            {
                Task<string> tmp = null;
                tmp = BMP280.callTemp();
                Debug.WriteLine(""+tmp.Result);
                string[] arr = (""+tmp.Result).Split('.');
                // check the temperature and take action 
                if (!tmp.Result.Equals("can't calculate Temperature") && int.Parse(arr[0]) >= 40)
                {
                    // Read from the ADC chip the current values of the two pots and the photo cell.
                    lowPotReadVal = MCP3008.ReadADC(LowPotentiometerADCChannel);
                    // convert the ADC readings to voltages to make them more friendly.
                    weight = MCP3008.ADCToVoltage(lowPotReadVal);
                    pirvalue = pir.Read();
                    // The baby is sitting and the temperature is > 40 
                    // It doesn't mind if there is a motion because the temp is > 40 and could be that the baby is sleeping 
                    if (weight > 1)
                    {
                        Emergency();

                    }
                }
            }
        }


        private void Emergency()
        {
            Task<string> tmp = null;
            tmp = BMP280.callTemp();
            Debug.WriteLine("Sending notification to parents.");
            Notification.SendNotification("your baby in the car, The windows will open in 1 minutes, The tempreture is: " + tmp.Result);
            Debug.WriteLine("Notification sent: \"Your baby in the car, The windows will open in 1 minutes, The tempreture is: " + tmp.Result + "\"");
            // Open car windows 
            Task.Delay(900000).Wait(); // Wait 15 minutes

        }

        private void textBoxSendText_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }

    


        /*
        private void FlipLed()
        {
            
            pushButtonValue = pushButton.Read();
            if (pushButtonValue == GpioPinValue.Low)
            {
                pin.Write(GpioPinValue.High);
                Task<string> temp = BMP280.callTemp();
                Notification.SendNotification("Someone is setting on the seat !!! The Temperature in the car is : " + temp + ".");
                Debug.WriteLine("Someone is setting on the seat !!! The Temperature in the car is :" + temp);
            }
            else if (pushButtonValue == GpioPinValue.High)
            {
                pin.Write(GpioPinValue.Low);
            }
        }
        */

    }



