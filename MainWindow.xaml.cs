using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace ProxySwitcher
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const string PROXY_FILENAME = "proxy_list.conf";

        private string[] urlsProxy = null;

        /*
            exemple of proxy list:
             http://www.us-proxy.org
             https://free-proxy-list.net/
             https://www.sslproxies.org/
             https://free-proxy-list.net/anonymous-proxy.html
             http://proxy-ip-list.com/free-usa-proxy-ip.html
        */

        //"[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}:[0-9]{1,4}") 97.77.104.22:80
        //<tr><td>54.244.126.137</td><td>3128</td>
        static readonly IReadOnlyDictionary<string, int[]> patterns = new Dictionary<string, int[]>
        {
            //@"(([01]?\d\d?|2[0-4]\d|25[0-5])([01]?\d\d?|2[0-4]\d|25[0-5])\.([01]?\d\d?|2[0-4]\d|25[0-5])\.([01]?\d\d?|2[0-4]\d|25[0-5]))(</td><td>)[0-9]{1,4}(</td>)",
            //@"[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}:[0-9]{1,4}"
            //@"([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3})(</td><td>)[0-9]{1,4}(</td>)",
            { @"((<td>)([0-9]{1,3}.[0-9]{1,3}.[0-9]{1,3}.[0-9]{1,3})(</td>))((<td>)([0-9]{1,5})(</td>))",  new int[] {3,7} },
            { @"[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}:[0-9]{1,5}",new int[] {1,2 } }   
        };


        public List<Proxy> proxyAddress = new List<Proxy>();

        readonly BackgroundWorker worker;

        public MainWindow()
        {
            InitializeComponent();

            worker = new BackgroundWorker();
            worker.DoWork += Worker_DoWork;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            worker.ProgressChanged += Worker_ProgressChanged;

            if (LoadProxyUrls())
                PopulateProxyUrls();            
        }

        private void PopulateProxyUrls()
        {
            lstProxysList.Items.Add(string.Empty);
            foreach (var url in urlsProxy)
                lstProxysList.Items.Add(url);
        }
        void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                lstProxysList.IsEnabled = true;
                dgProxyList.IsEnabled = true;
                pbBar.BeginAnimation(ProgressBar.ValueProperty, null);
                tbStatusProgressBar.Visibility = Visibility.Collapsed;
            }));

            if (e.Error != null)
            {
                MessageBox.Show("An error has occured " + e.Error.Message);
                return;
            }
            
            Dispatcher.Invoke(new Action(() =>
            {
                dgProxyList.ItemsSource = proxyAddress;
                dgProxyList.Items.Refresh();
            }));
            
        }

        void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Dispatcher.Invoke(new Action(()=>
            {
                pbBar.Value = e.ProgressPercentage;
            }));
        }

        void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            var obj = (Paramobject)e.Argument;
            var cc = GetContentFromUrl(obj.Url);
            _ = Test(cc, patterns);
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private bool LoadProxyUrls()
        {
            bool isLoad = false;
            if (File.Exists(PROXY_FILENAME))
            {
                urlsProxy = File.ReadAllLines(PROXY_FILENAME);
                isLoad = (urlsProxy != null && urlsProxy.Length > 0);
            }
            if (!isLoad)
                MessageBox.Show("Please add the proxy_list.conf file with url(s)", "Information", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            return isLoad;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="content"></param>
        /// <param name="patterns"></param>
        /// <returns></returns>
        bool Test(string content, IReadOnlyDictionary<string,int[]> patterns)
        {
            for (int i = 0; i < patterns.Count;i++)
            {
                var patternConfiguration = patterns.ElementAt(i);
                Regex reg = new Regex(patternConfiguration.Key);
                var matches = reg.Matches(content);
                if (!(matches != null && matches.Count > 0))
                    continue;
                foreach (Match m in matches)
                {
                    string endpoint = string.Empty;
                    int j = 0;
                    foreach (var groupIndex in patternConfiguration.Value) {
                        endpoint += m.Groups[groupIndex];
                        if (j == 0) endpoint += ":";
                        ++j;
                    }
                    proxyAddress.Add(new Proxy { IP = endpoint, Action = GetScriptAction(true, endpoint) });
                }
                return true;
            }
            return false;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        static string GetContentFromUrl(string url)
        {
            string result = string.Empty;
            HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create(url);
            myRequest.Method = "GET";
            using (WebResponse myResponse = myRequest.GetResponse())
            using (StreamReader sr = new StreamReader(myResponse.GetResponseStream(), System.Text.Encoding.UTF8))
                result = sr.ReadToEnd();
            return result;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="enableProxy"></param>
        /// <param name="proxyPort"></param>
        /// <returns></returns>
        static string GetScriptAction(bool enableProxy, string proxyPort)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Windows Registry Editor Version 5.00");
            sb.AppendLine(@"[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Internet Settings]");
            sb.AppendLine("\"ProxyEnable\"=dword:0000000" + (enableProxy ? "1" : "0"));
            sb.Append("\"ProxyServer\"=\"");
            sb.Append(proxyPort);
            sb.Append("\"");
            sb.AppendLine();
            sb.AppendLine("\"ProxyOverride\"=\"<local>\"");

            return sb.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        private void InitProgressBar()
        {
            ProgressBar progbar = pbBar;
            Duration duration = new Duration(TimeSpan.FromSeconds(1));
            DoubleAnimation doubleanimation = new DoubleAnimation(100.0, duration);
            doubleanimation.RepeatBehavior = RepeatBehavior.Forever;
            progbar.BeginAnimation(ProgressBar.ValueProperty, doubleanimation);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnProxysListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int index = (e.Source as ComboBox).SelectedIndex;
            if (index > 0)
            {
                var url = lstProxysList.SelectedItem.ToString();
                lstProxysList.IsEnabled = false;
                dgProxyList.IsEnabled = false;
                InitProgressBar();
                tbStatusProgressBar.Visibility = Visibility.Visible;
                worker.RunWorkerAsync(new Paramobject(url, index));
            }
            else
                dgProxyList.Items.Clear();
        }

        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        private string GetGenerateRegFile(string content)
        {
            string path = string.Empty;
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var file = Guid.NewGuid().ToString("N") + ".reg";
            path = System.IO.Path.Combine(basePath, file);
            using (StreamWriter sr = new StreamWriter(path))
                sr.WriteLine(content.ToCharArray());
            return path;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnRegExecute(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = e.Source as Button;
                var regContent = button.Tag.ToString();
                var filePath = GetGenerateRegFile(regContent);
                RunRegCommand(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        private void RunRegCommand(string filePath)
        {
            Process regeditProcess = Process.Start("regedit.exe", "/s " + filePath);
            regeditProcess.WaitForExit();
            try
            {
                File.Delete(filePath);
            }
            catch
            { }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnExitApplication(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(new Action(() => {
                MessageBoxResult result= MessageBox.Show("Exit program ?", "Information", MessageBoxButton.OKCancel, MessageBoxImage.Information);
                if( result == MessageBoxResult.OK )
                    Close();
            }));
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnDisableProxy(object sender, RoutedEventArgs e)
        {
            try
            {
                var regContent = GetScriptAction(false, string.Empty);
                var filePath = GetGenerateRegFile(regContent);
                RunRegCommand(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error has occured {0}", ex.Message);
            }
        }

    }
}
