using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V7.App;
using Android.Util;
using Android.Views;
using Android.Widget;
using Newtonsoft.Json;

namespace SmartCharger
{
    [Activity(Theme = "@style/MyTheme.Splash", MainLauncher = true, NoHistory = true)]
    public class StartActivity : AppCompatActivity
    {
        static readonly string TAG = "X:" + typeof(StartActivity).Name;

        public override void OnCreate(Bundle savedInstanceState, PersistableBundle persistentState)
        {
            base.OnCreate(savedInstanceState, persistentState);
            Log.Debug(TAG, "SplashActivity.OnCreate");
        }

        // Launches the startup task
        protected override void OnResume()
        {
            base.OnResume();
            Task startupWork = new Task(() => { SimulateStartup(); });
            startupWork.Start();
        }

        // Prevent the back button from canceling the startup process
        public override void OnBackPressed() { }

        // Simulates background work that happens behind the splash screen
        async void SimulateStartup()
        {
            Log.Debug(TAG, "Performing some startup work that takes a bit of time.");
            byte[] bytes = new byte[1024];
            Settings settings = null;
            Connection connection = null;
            try
            {
                ISharedPreferences pref = Application.Context.GetSharedPreferences("UserInfo", FileCreationMode.Private);
                string host = pref.GetString("Host", "10.0.0.2");
                int port = pref.GetInt("Port", 9527);
                connection = new Connection { Host = host, Port = port };
                int timeout = 1000;

                // Create TCP client and connect
                // Then get the netstream and pass it
                // To our StreamWriter and StreamReader
                using (var client = new TcpClient())
                {
                    // Asynchronsly attempt to connect to server
                    CancellationToken ct = new CancellationToken();
                    if (client.ConnectAsync(host, port).Wait(5000, ct))
                        using (var netstream = client.GetStream())
                        using (var writer = new StreamWriter(netstream))
                        using (var reader = new StreamReader(netstream))
                        {

                            // AutoFlush the StreamWriter
                            // so we don't go over the buffer
                            writer.AutoFlush = true;

                            // Optionally set a timeout
                            netstream.ReadTimeout = timeout;

                            // Write a message over the TCP Connection
                            var myData = new
                            {
                                cmd = "get_config"
                            };
                            string message = JsonConvert.SerializeObject(myData);
                            await writer.WriteLineAsync(message);

                            // Read server response
                            string response = await reader.ReadLineAsync();
                            settings = JsonConvert.DeserializeObject<Settings>(response);

                            //  Log.Debug(TAG,string.Format($"Server: {response}"));
                        }
                }
                // The client and stream will close as control exits
                // the using block (Equivilent but safer than calling Close();                }
            }
            catch (Exception e)
            {
                Log.Debug(TAG, e.Message);
            }
            // end my workebug(TAG, "Startup work is finished - starting MainActivity.");

            StartActivity(new Intent(Application.Context, typeof(MainActivity)).PutExtra("MY_SETTINGS", settings).PutExtra("MY_CONNECT", connection));


        }


    }
}