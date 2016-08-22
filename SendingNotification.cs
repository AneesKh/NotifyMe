using Windows.UI.Xaml.Controls;
using Windows.Devices.Gpio;
using Windows.UI.Xaml;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;

namespace NotifyMe
{
    public class Notification
    {
        //public string Endpoint { get; private set; }
        //public string SasKeyName { get; private set; }
        //public string SasKeyValue { get; private set; }
        public static string Endpoint = "";
        public static string SasKeyName = "";
        public static string SasKeyValue = "";

        public static void ConnectionStringUtility(string connectionString)
        {
            //Parse Connectionstring
            char[] separator = { ';' };
            string[] parts = connectionString.Split(separator);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].StartsWith("Endpoint"))
                    Endpoint = "https" + parts[i].Substring(11);
                if (parts[i].StartsWith("SharedAccessKeyName"))
                    SasKeyName = parts[i].Substring(20);
                if (parts[i].StartsWith("SharedAccessKey"))
                    SasKeyValue = parts[i].Substring(16);
            }
        }


        public static string getSaSToken(string uri, int minUntilExpire)
        {
            string targetUri = Uri.EscapeDataString(uri.ToLower()).ToLower();

            // Add an expiration in seconds to it.
            long expiresOnDate = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            expiresOnDate += minUntilExpire * 60 * 1000;
            long expires_seconds = expiresOnDate / 1000;
            String toSign = targetUri + "\n" + expires_seconds;

            // Generate a HMAC-SHA256 hash or the uri and expiration using your secret key.
            MacAlgorithmProvider macAlgorithmProvider = MacAlgorithmProvider.OpenAlgorithm(MacAlgorithmNames.HmacSha256);
            BinaryStringEncoding encoding = BinaryStringEncoding.Utf8;
            var messageBuffer = CryptographicBuffer.ConvertStringToBinary(toSign, encoding);
            IBuffer keyBuffer = CryptographicBuffer.ConvertStringToBinary(SasKeyValue, encoding);
            CryptographicKey hmacKey = macAlgorithmProvider.CreateKey(keyBuffer);
            IBuffer signedMessage = CryptographicEngine.Sign(hmacKey, messageBuffer);

            string signature = Uri.EscapeDataString(CryptographicBuffer.EncodeToBase64String(signedMessage));

            return "SharedAccessSignature sr=" + targetUri + "&sig=" + signature + "&se=" + expires_seconds + "&skn=" + SasKeyName;
        }

        public static async void SendNotification(String msg)
        {
            ConnectionStringUtility("Endpoint=sb://iottau.servicebus.windows.net/;SharedAccessKeyName=DefaultFullSharedAccessSignature;SharedAccessKey=Oy5yO8bH5/dcf3fqlAAaFEZem1HEoLUaAKzmafk2fKA=");

            var uri = Notification.Endpoint + "IoTHUB" + "/messages/?api-version=2015-01";
            string json = "{\"data\":{\"message\":\"" + msg + "\"}}";
            byte[] byteArray = Encoding.UTF8.GetBytes(json);

            using (var httpClient = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Post, uri);
                request.Content = new StringContent(json);

                //request.Method = "POST";
                request.Headers.Add("Authorization", getSaSToken(uri, 1000));
                // request.Headers.Add("ServiceBusNotification-Tags", "1470878167838183135-6585072770486047276-1");
                //request.Headers.Add("Content-Type", "application/json"); // not sure if there should be a dot in the end
                request.Headers.Add("ServiceBusNotification-Format", "gcm");
                var response = await httpClient.SendAsync(request);
                Debug.WriteLine(await response.Content.ReadAsStringAsync());
            }
        }




    }
}
