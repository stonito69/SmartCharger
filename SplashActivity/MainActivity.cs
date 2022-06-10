using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using Android.Util;
using System;
using Android.Views;
using System.Net.Sockets;
using System.IO;
using Newtonsoft.Json;
using Android.Content.PM;
using System.Threading.Tasks;

namespace SmartCharger
{
    [Activity(Label = "@string/app_name", MainLauncher = false, Icon = "@mipmap/ic_launcher", Theme = "@style/DialogUplatform", ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainActivity : Activity
    {
        static readonly string TAG = typeof(MainActivity).FullName;

        Button buttonOff;
        Button buttonOn;
        Intent startServiceIntent;
        Intent stopServiceIntent;
        SeekBar seekFrom;
        SeekBar seekTo;
        SeekBar pingMinutes;
        TextView textRange;
        TextView textPing;
        TextView textStatus;
        Button buttonSave;

        bool isStarted = false;
        Settings settings = null;
        Connection connection = null;
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.Main);
            OnNewIntent(this.Intent);

            settings = Intent.GetExtra<Settings>("MY_SETTINGS");
            connection = Intent.GetExtra<Connection>("MY_CONNECT");

            /*      stopServiceButton = FindViewById<Button>(Resource.Id.stop_timestamp_service_button);
                  startServiceButton = FindViewById<Button>(Resource.Id.start_timestamp_service_button);
            */
            buttonSave = FindViewById<Button>(Resource.Id.MyButton);
            buttonOn = FindViewById<Button>(Resource.Id.switch_on);
            buttonOff = FindViewById<Button>(Resource.Id.switch_off);
            textStatus = FindViewById<TextView>(Resource.Id.textStatus);
            seekFrom = FindViewById<SeekBar>(Resource.Id.seekFrom);
            seekTo = FindViewById<SeekBar>(Resource.Id.seekTo);
            pingMinutes = FindViewById<SeekBar>(Resource.Id.pingMinutes);
            textRange = FindViewById<TextView>(Resource.Id.textRange);
            textPing = FindViewById<TextView>(Resource.Id.textPing);

            buttonSave.Enabled = false;


            if (settings == null)
            {
                ShowDialog(connection.Host, connection.Port.ToString());
            }
            else
            {
                textStatus.Text = string.Format("connected to {0}:{1}", settings.Host, settings.Port);
                seekFrom.Progress = settings.BatteryMin;
                seekTo.Progress = settings.BatteryMax;
                pingMinutes.Progress = settings.PingMinutes;
                textRange.Text = String.Format("{0}-{1}", seekFrom.Progress, seekTo.Progress);
                textPing.Text = String.Format("{0} min", pingMinutes.Progress);
            }

            if (savedInstanceState != null)
            {
                isStarted = savedInstanceState.GetBoolean(Constants.SERVICE_STARTED_KEY, false);
            }

            startServiceIntent = new Intent(this, typeof(TimestampService));
            startServiceIntent.SetAction(Constants.ACTION_START_SERVICE);

            stopServiceIntent = new Intent(this, typeof(TimestampService));
            stopServiceIntent.SetAction(Constants.ACTION_STOP_SERVICE);


            seekFrom.ProgressChanged += (s, e) =>
            {
                if (seekFrom.Progress > 99) seekFrom.Progress = 99;
                if (seekTo.Progress <= seekFrom.Progress) seekTo.Progress = seekFrom.Progress + 1;
                textRange.Text = String.Format("{0}-{1}", seekFrom.Progress, seekTo.Progress);
                buttonSave.Enabled = true;
            };
            seekTo.ProgressChanged += (s, e) =>
            {
                if (seekFrom.Progress >= seekTo.Progress) seekFrom.Progress = seekTo.Progress - 1;
                textRange.Text = String.Format("{0}-{1}", seekFrom.Progress, seekTo.Progress);
                buttonSave.Enabled = true;
            };
            pingMinutes.ProgressChanged += (s, e) =>
            {
                textPing.Text = String.Format("{0} min", pingMinutes.Progress);
                buttonSave.Enabled = true;
            };
            if (settings != null && !isStarted)
            {
                Constants.DELAY_BETWEEN_LOG_MESSAGES = settings.PingMinutes * 60 * 1000;
                StartService(startServiceIntent);
            }

            buttonOn.Click += async (s, e) =>
            {
                await do_switch(true);
            };
            buttonOff.Click += async (s, e) => {
                await do_switch(false);
            };

            buttonSave.Click += async (s, e) =>
            {
                settings.BatteryMax = seekTo.Progress;
                settings.BatteryMin = seekFrom.Progress;
                settings.PingMinutes = pingMinutes.Progress;
                try
                {
                    ISharedPreferences pref = Application.Context.GetSharedPreferences("UserInfo", FileCreationMode.Private);
                    string host = pref.GetString("Host", "10.0.0.2");
                    int port = pref.GetInt("Port", 9527);
                    int timeout = 1000;

                    // Create TCP client and connect
                    // Then get the netstream and pass it
                    // To our StreamWriter and StreamReader
                    using (var client = new TcpClient())
                    {
                        // Asynchronsly attempt to connect to server

                        await client.ConnectAsync(host, port);
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
                                cmd = "set_config",
                                max = settings.BatteryMax,
                                min = settings.BatteryMin,
                                ping = settings.PingMinutes

                            };
                            string message = JsonConvert.SerializeObject(myData);
                            await writer.WriteLineAsync(message);

                            // Read server response
                            string response = await reader.ReadLineAsync();
                            if (response.StartsWith("OK"))
                            {
                                buttonSave.Enabled = false;
                                Constants.DELAY_BETWEEN_LOG_MESSAGES = settings.PingMinutes *  60 * 1000;
                            }

                        }
                    }
                    // The client and stream will close as control exits
                    // the using block (Equivilent but safer than calling Close();                }
                }
                catch (Exception ex)
                {
                    Log.Debug(TAG, ex.Message);
                }
            };

        }

        async Task do_switch(bool on)
        {
            try
            {
                ISharedPreferences pref = Application.Context.GetSharedPreferences("UserInfo", FileCreationMode.Private);
                string host = pref.GetString("Host", "10.0.0.2");
                int port = pref.GetInt("Port", 9527);
                int timeout = 1000;

                using (var client = new TcpClient())
                {
                    // Asynchronsly attempt to connect to server

                    await client.ConnectAsync(host, port);
                    using (var netstream = client.GetStream())
                    using (var writer = new StreamWriter(netstream))
                    using (var reader = new StreamReader(netstream))
                    {

                        writer.AutoFlush = true;
                        netstream.ReadTimeout = timeout;
                        var myData = new
                        {
                            cmd = "onoff",
                            on = on
                        };
                        string message = JsonConvert.SerializeObject(myData);
                        await writer.WriteLineAsync(message);
                        string response = await reader.ReadLineAsync();
                        if (!response.StartsWith("OK"))
                            Toast.MakeText(this, "Command Failed", ToastLength.Short).Show();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(TAG, ex.Message);
            }
        }

        void ShowDialog(string host = "", string port = "")
        {
            LayoutInflater layoutInflater = LayoutInflater.From(this);
            View view = layoutInflater.Inflate(Resource.Layout.connection_dialog, null);
            Android.Support.V7.App.AlertDialog.Builder alertbuilder = new Android.Support.V7.App.AlertDialog.Builder(this);
            alertbuilder.SetView(view);
            var userIp = view.FindViewById<EditText>(Resource.Id.editIP);
            var userPort = view.FindViewById<EditText>(Resource.Id.editPort);
            userIp.Text = host;
            userPort.Text = port;
            alertbuilder.SetCancelable(false)
            //.SetTitle("ROman")
            //.SetMessage("Message")
            .SetPositiveButton("Submit", delegate
            {
                int port = 0;
                if (int.TryParse(userPort.Text.Trim(), out port) && port > 0 && port < 65551)
                {
                    ISharedPreferences pref = Application.Context.GetSharedPreferences("UserInfo", FileCreationMode.Private);
                    ISharedPreferencesEditor edit = pref.Edit();

                    edit.PutString("Host", userIp.Text.Trim());
                    edit.PutInt("Port", port);
                    edit.Apply();
                    Toast.MakeText(this, "Submit Input: " + userIp.Text + ":" + userPort.Text, ToastLength.Short).Show();
                    StartActivity(new Intent(Application.Context, typeof(StartActivity)));
                }
                else
                {
                    Toast.MakeText(this, "Port should be integer greater than 1 and less than 65550", ToastLength.Short).Show();
                    ShowDialog(userIp.Text, userPort.Text);
                }
            })
            .SetNegativeButton("Cancel", delegate
            {
                alertbuilder.Dispose();
                this.Finish();
            });
            Android.Support.V7.App.AlertDialog dialog = alertbuilder.Create();
            dialog.Show();
        }
        protected override void OnNewIntent(Intent intent)
        {
            if (intent == null)
            {
                return;
            }

            var bundle = intent.Extras;
            if (bundle != null)
            {
                if (bundle.ContainsKey(Constants.SERVICE_STARTED_KEY))
                {
                    isStarted = true;
                }
            }
        }

        protected override void OnSaveInstanceState(Bundle outState)
        {
            outState.PutBoolean(Constants.SERVICE_STARTED_KEY, isStarted);
            base.OnSaveInstanceState(outState);
        }

        protected override void OnDestroy()
        {
            //Log.Info(TAG, "Activity is being destroyed; stop the service.");

            //StopService(startServiceIntent);
            base.OnDestroy();
        }


        /*
        void StopServiceButton_Click(object sender, System.EventArgs e)
        {
            stopServiceButton.Click -= StopServiceButton_Click;
            stopServiceButton.Enabled = false;

            Log.Info(TAG, "User requested that the service be stopped.");
            StopService(stopServiceIntent);
            isStarted = false;

            startServiceButton.Click += StartServiceButton_Click;
            startServiceButton.Enabled = true;
        }

        void StartServiceButton_Click(object sender, System.EventArgs e)
        {
            startServiceButton.Enabled = false;
            startServiceButton.Click -= StartServiceButton_Click;

            StartService(startServiceIntent);
            Log.Info(TAG, "User requested that the service be started.");

            isStarted = true;
            stopServiceButton.Click += StopServiceButton_Click;

            stopServiceButton.Enabled = true;
        }
        */


    }


}

