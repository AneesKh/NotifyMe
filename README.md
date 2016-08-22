# NotifyMe
A project that aims to save the life of trapped passengers in cars by sending a notification to the driver in case he left someone behind.

# Motivation

The main motive behind this project is to obviously save lives. Every now and then we hear about an incident where someone forgets their kid in a car, and we would like to address this issue and provide a solution for it. Given that the other projects out there don't offer a "smart" solution, we thought it would be a good idea to create out own.

# Installation

Our project consists of two major applications:

1- A "Server-side" UWP C# application. Its purpose is to administrate all the logic running "behind the scenes", including connecting to the OBD2 (ELM 327) via bluetooth, polling a variety of sensors and sending a notification to the user. 

2- A client-side Android application. Its purpose is to receive the notification that is being sent from the "Server-side" app.

To install the UWP C# application, simply load it Visual Studio 2015 and then run it. Please note that you must have setup a notification hub and have created a project that supports Google Cloud Messaging, a setup tutorial can be found here: https://azure.microsoft.com/en-us/documentation/articles/notification-hubs-android-push-notification-google-gcm-get-started/ 

The Android application can be downloaded from Google Play (once we upload it) but in the mean time, the source code can be found here, so in order to install it, you'll have to download Android Studio, load it in there and then either run it on a virtual device or just plug in your own device and run the project on it (note that "USB Debugging" must be enabled on your Android device).

# Files

MCP3008.cs: The MCP3008 10-bit Analog-to-Digital Converter (ADC), the code handles its initialization, communicating over the SPI bus with the chip, conversion to voltage, etc.

Bmp280.cs: Handles the temperature sensor. Contains a variety of functions for the purpose of measuring the temperature.

SendingNotification.cs: Responsible for connecting to a notifcation hub and uses the REST API of GCM to send a notification to Android devices using said notification hub. More info about the API can be found here: https://msdn.microsoft.com/en-us/library/azure/dn223273.aspx


MainPage.xaml.cs: The core of our project. Pieces everything together by initializing all the components (GPIO, MCP3008, BMP280 and Bluetooth), connecting to them, and passing data along to the relevant components. It first connects to the OBDII, checks that the car is actually off and starts polling the sensors if so. When the sensors gather enough information to know for sure that someone is in the car, it sends a notification to the user.

App.xaml.cs: Initializes the main page.
