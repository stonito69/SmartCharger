using System;
using Android.App;
using Android.Util;
using Android.Content;
using Android.OS;
using System.Threading;
using System.Net.Sockets;
using System.IO;
using Newtonsoft.Json;

namespace SmartCharger
{

	/// <summary>
	/// This is a sample started service. When the service is started, it will log a string that details how long 
	/// the service has been running (using Android.Util.Log). This service displays a notification in the notification
	/// tray while the service is active.
	/// </summary>
	[Service]
	public class TimestampService : Service
	{
		static readonly string TAG = typeof(TimestampService).FullName;

		bool isStarted;
		Handler handler;
		Action runnable;

		public override void OnCreate()
		{
			base.OnCreate();
			Log.Info(TAG, "OnCreate: the service is initializing.");
			
			handler = new Handler(Looper.MyLooper());

			// This Action is only for demonstration purposes.
			runnable = new Action(async () =>
							{
								byte[] bytes = new byte[1024];
								try
								{
									ISharedPreferences pref = Application.Context.GetSharedPreferences("UserInfo", FileCreationMode.Private);
									string host = pref.GetString("Host", "127.0.0.1");
									int port = pref.GetInt("Port", 9527);
									int timeout = 5000;

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
											var filter = new IntentFilter(Intent.ActionBatteryChanged);
											var battery = RegisterReceiver(null, filter);
											int level = battery.GetIntExtra(BatteryManager.ExtraLevel, -1);
											int scale = battery.GetIntExtra(BatteryManager.ExtraScale, -1);
											int BPercetage = (int)System.Math.Floor(level * 100D / scale);
											int status = battery.GetIntExtra(BatteryManager.ExtraStatus, -1);


											var myData = new
											{
												cmd = "status",
												level = BPercetage,
												state = status
											};
											string message = JsonConvert.SerializeObject(myData);
											await writer.WriteLineAsync(message);
											string response = await reader.ReadLineAsync();
										}
									}
								}
								catch (Exception e)
								{
									
								}
								// end my work

								Intent i = new Intent(Constants.NOTIFICATION_BROADCAST_ACTION);
								i.PutExtra(Constants.BROADCAST_MESSAGE_KEY, "ping");
								Android.Support.V4.Content.LocalBroadcastManager.GetInstance(this).SendBroadcast(i);
								handler.PostDelayed(runnable, Constants.DELAY_BETWEEN_LOG_MESSAGES);
							});
		}

		public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
		{
			if (intent.Action.Equals(Constants.ACTION_START_SERVICE))
			{
				if (isStarted)
				{
					Log.Info(TAG, "OnStartCommand: The service is already running.");
				}
				else 
				{
					Log.Info(TAG, "OnStartCommand: The service is starting.");
					RegisterForegroundService();
					handler.PostDelayed(runnable, 5);
					isStarted = true;
				}
			}
			else if (intent.Action.Equals(Constants.ACTION_STOP_SERVICE))
			{
				Log.Info(TAG, "OnStartCommand: The service is stopping.");
				StopForeground(true);
				StopSelf();
				isStarted = false;

			}
			else if (intent.Action.Equals(Constants.ACTION_RESTART_TIMER))
			{
				Log.Info(TAG, "OnStartCommand: Restarting the timer.");
			}

			// This tells Android not to restart the service if it is killed to reclaim resources.
			return StartCommandResult.Sticky;
		}


		public override IBinder OnBind(Intent intent)
		{
			// Return null because this is a pure started service. A hybrid service would return a binder that would
			// allow access to the GetFormattedStamp() method.
			return null;
		}


		public override void OnDestroy()
		{
			// We need to shut things down.
			Log.Info(TAG, "OnDestroy: The started service is shutting down.");

			// Stop the handler.
			handler.RemoveCallbacks(runnable);

			// Remove the notification from the status bar.
			var notificationManager = (NotificationManager)GetSystemService(NotificationService);
			notificationManager.Cancel(Constants.SERVICE_RUNNING_NOTIFICATION_ID);

			isStarted = false;
			base.OnDestroy();
		}

		/// <summary>
		/// This method will return a formatted timestamp to the client.
		/// </summary>
		/// <returns>A string that details what time the service started and how long it has been running.</returns>

		Notification.Builder notificationBuilder = null;

		void RegisterForegroundService()
		{
			NotificationChannel chan = new NotificationChannel("smart.charger.service", "My Channel", NotificationImportance.None);
			chan.EnableVibration(false);
			chan.LockscreenVisibility = NotificationVisibility.Secret;
			NotificationManager notificationManager = GetSystemService(NotificationService) as NotificationManager;
			notificationManager.CreateNotificationChannel(chan);
			notificationBuilder = new Notification.Builder(this, "smart.charger.service");
			var notification = notificationBuilder
				.SetContentTitle(Resources.GetString(Resource.String.app_name))
				.SetContentText(Resources.GetString(Resource.String.notification_text))
				.SetSmallIcon(Resource.Drawable.abc_btn_switch_to_on_mtrl_00001)
				.SetContentIntent(BuildIntentToShowMainActivity())
				.SetOngoing(true)
				//    .AddAction(BuildRestartTimerAction())
				.AddAction(BuildStopServiceAction())
				.Build();

			StartForeground(Constants.SERVICE_RUNNING_NOTIFICATION_ID, notification);

			/*
			var notification = new Notification.Builder(this)
				.SetContentTitle(Resources.GetString(Resource.String.app_name))
				.SetContentText(Resources.GetString(Resource.String.notification_text))
				.SetSmallIcon(Resource.Drawable.ic_stat_name)
				.SetContentIntent(BuildIntentToShowMainActivity())
				.SetOngoing(true)
				.AddAction(BuildRestartTimerAction())
				.AddAction(BuildStopServiceAction())
				.Build();


			// Enlist this instance of the service as a foreground service
			StartForeground(Constants.SERVICE_RUNNING_NOTIFICATION_ID, notification);
			*/
		}

		/// <summary>
		/// Builds a PendingIntent that will display the main activity of the app. This is used when the 
		/// user taps on the notification; it will take them to the main activity of the app.
		/// </summary>
		/// <returns>The content intent.</returns>
		PendingIntent BuildIntentToShowMainActivity()
		{
			var notificationIntent = new Intent(this, typeof(MainActivity));
			notificationIntent.SetAction(Constants.ACTION_MAIN_ACTIVITY);
			notificationIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTask);
			notificationIntent.PutExtra(Constants.SERVICE_STARTED_KEY, true);

			var pendingIntent = PendingIntent.GetActivity(this, 0, notificationIntent, PendingIntentFlags.UpdateCurrent);
			return pendingIntent;
		}

		/// <summary>
		/// Builds a Notification.Action that will instruct the service to restart the timer.
		/// </summary>
		/// <returns>The restart timer action.</returns>
		Notification.Action BuildRestartTimerAction()
		{
			var restartTimerIntent = new Intent(this, GetType());
			restartTimerIntent.SetAction(Constants.ACTION_RESTART_TIMER);
			var restartTimerPendingIntent = PendingIntent.GetService(this, 0, restartTimerIntent, 0);

			var builder = new Notification.Action.Builder(Resource.Drawable.abc_btn_switch_to_on_mtrl_00001,
											  GetText(Resource.String.restart_timer),
											  restartTimerPendingIntent);

			return builder.Build();
		}

		/// <summary>
		/// Builds the Notification.Action that will allow the user to stop the service via the
		/// notification in the status bar
		/// </summary>
		/// <returns>The stop service action.</returns>
		Notification.Action BuildStopServiceAction()
		{
			var stopServiceIntent = new Intent(this, GetType());
			stopServiceIntent.SetAction(Constants.ACTION_STOP_SERVICE);
			var stopServicePendingIntent = PendingIntent.GetService(this, 0, stopServiceIntent, 0);

			var builder = new Notification.Action.Builder(Android.Resource.Drawable.IcMediaPause,
														  GetText(Resource.String.stop_service),
														  stopServicePendingIntent);
			return builder.Build();

		}
	}
}
