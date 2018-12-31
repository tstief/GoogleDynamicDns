using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using System.Net.Http;
using System.Timers;

namespace GoogleDynamicDns
{
    class Program
    {
        private static string invalidIpAddress = "0.0.0.0";

        private static Timer aTimer;

        private static Timer heartBeat;

        private static AppConfig config;

        private static MailGun.MailClient mailClient;

        private static void SendMessage(string message)
        {
            Console.WriteLine(message);
            try
            {
                mailClient.SendMessageAsync("tstief@gmail.com", "Google DDNS Update", message);
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("Esception caugh during call to MailGun: {0}", e.Message);
            }
        }

        private static async Task<string> GetIpAddressAsync()
        {
            var ipAddress = invalidIpAddress;
            using (var httpClient = new HttpClient())
            {
                try
                {
                    ipAddress = await httpClient.GetStringAsync(config.ipProviderUrl);
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine("Http request exception caught: {0}", e.Message);
                }
            }
            return ipAddress;
        }

        private static void SetTimer()
        {
            // Create a timer with a two second interval.
            aTimer = new Timer(config.timeInterval);
            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += async (sender, e) => await OnTimedEvent(sender, e);
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
            aTimer.Start();
        }

        private static void SetHeartBeatTimer()
        {
            SendMessage("Google DDNS heartbeat starting up.");

            // Create a timer with a two second interval.
            heartBeat = new Timer(config.heartBeat);
            // Hook up the Elapsed event for the timer. 
            heartBeat.Elapsed += (sender, e) => Console.WriteLine("{0}: Google DDNS is active.", e.SignalTime);
            heartBeat.AutoReset = true;
            heartBeat.Enabled = true;
            heartBeat.Start();
        }

        private static async Task UpdateIpAddress(DateTime time, string oldIpAddress, string newIpAddress)
        {
            SendMessage(string.Format("{0}: Changing IP address from {1} to {2}.", time, oldIpAddress, newIpAddress));

            bool saveIp = true;
            foreach (var host in config.hosts)
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://domains.google.com/nic/update?hostname=" + host.name);
                var byteArray = System.Text.Encoding.ASCII.GetBytes(host.userName + ":" + host.password);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                request.Headers.UserAgent.ParseAdd("Chrome/71.0.3578.94");

                using (var httpClient = new HttpClient())
                {
                    try
                    {
                        var response = await httpClient.SendAsync(request);
                        SendMessage(string.Format("{0}: Updating host {1}, server status {2}", time, host.name, response.StatusCode));
                    }
                    catch (HttpRequestException e)
                    {
                        saveIp = false;
                        Console.WriteLine("Http request exception caught: {0}", e.Message);
                        break;
                    }
                }
            }

            if (saveIp)
            {
                File.WriteAllLines("currentIp.txt", new string[] { newIpAddress });
            }
        }

        private static async Task OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            var currentIp = invalidIpAddress;
            if (File.Exists("currentIp.txt"))
            {
                currentIp = File.ReadAllLines("currentIp.txt")[0];
            }

            var ipAddress = await GetIpAddressAsync();
            ipAddress = ipAddress.TrimEnd(Environment.NewLine.ToCharArray());
            if (currentIp != ipAddress && ipAddress != invalidIpAddress)
            {
                await UpdateIpAddress(e.SignalTime, currentIp, ipAddress);
            }
        }

        static void Main(string[] args)
        {
            var configFile = new FileStream(args[0], FileMode.Open);
            var serializer = new DataContractJsonSerializer(typeof(AppConfig));
            config = serializer.ReadObject(configFile) as AppConfig;
            mailClient = new MailGun.MailClient(config.mailDomain, config.mailKey);
            SetHeartBeatTimer();
            SetTimer();
            while (true)
            {
                System.Threading.Thread.Sleep(1000);
                continue;
            }
        }
    }
}
