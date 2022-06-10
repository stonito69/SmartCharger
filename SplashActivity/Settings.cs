using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SmartCharger
{
    
    public class Settings
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public int PingMinutes { get; set; }
            public int BatteryMin { get; set; }
            public int BatteryMax { get; set; }
    }

    public class Connection
    {
        public string Host { get; set; }
        public int Port { get; set; }
    }

    public static class IntentExtension
    {
        public static Intent PutExtra<TExtra>(this Intent intent, string name, TExtra extra)
        {
            var json = JsonConvert.SerializeObject(extra);
            intent.PutExtra(name, json);
            return intent;
        }

        public static TExtra GetExtra<TExtra>(this Intent intent, string name)
        {
            var json = intent.GetStringExtra(name);
            if (string.IsNullOrWhiteSpace(json))
            {
                return default(TExtra);
            }

            return JsonConvert.DeserializeObject<TExtra>(json);
        }
    }
}