using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Fiddler;
using FiddlerCore;

namespace FiddlerCore.Demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //https代理
        Proxy oSecureEndpoint;
        //主机名
        string sSecureEndpointHostname = "localhost";
        //伪装https服务器（别人这么说，我也没搞明白这个技术细节）
        int iSecureEndpointPort = 8877;
        //代理端口
        ushort iStartPort = 8888;
        //FiddlerCore抓取到的会话不会缓存，所以，要自己维护一个会话列表，来保存所关心的请求
        List<Session> oAllSessions = new();
        //伪造的证书
        X509Certificate2 oRootCert;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            FiddlerApplication.SetAppDisplayName("lishewen");

            //-----------处理证书-----------
            //如果没有伪造过证书并把伪造的证书加入本机证书库中
            if (null == CertMaker.GetRootCertificate())
            {
                //创建伪造证书
                CertMaker.createRootCert();

                //重新获取
                oRootCert = CertMaker.GetRootCertificate();

                //打开本地证书库
                X509Store certStore = new(StoreName.Root, StoreLocation.LocalMachine);
                certStore.Open(OpenFlags.ReadWrite);
                try
                {
                    //将伪造的证书加入到本地的证书库
                    certStore.Add(oRootCert);
                }
                finally
                {
                    certStore.Close();
                }
            }
            else
            {
                //以前伪造过证书，并且本地证书库中保存过伪造的证书
                oRootCert = CertMaker.GetRootCertificate();
            }

            //-----------------------------

            //指定伪造证书
            FiddlerApplication.oDefaultClientCertificate = oRootCert;
            //忽略服务器证书错误
            CONFIG.IgnoreServerCertErrors = true;
            //信任证书
            CertMaker.trustRootCert();

            MessageBox.Show("证书已经伪造完毕");
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            //绑定事件处理————当会话结束之后
            FiddlerApplication.AfterSessionComplete += On_AfterSessionComplete;
            txt1.Clear();
            MessageBox.Show("绑定事件处理");
        }

        //封包响应结束事件————同样，基于这个Session oS可以做很多很多事
        private void On_AfterSessionComplete(Session oS)
        {
            string respStr = "";
            respStr = oS.GetResponseBodyAsString();
            if (oS.bHasResponse && respStr.Length > 0)
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    txt1.AppendText($"{oS.fullUrl}\r\n");
                }));
            }

            if (oAllSessions.Count > 300)
            {
                Monitor.Enter(oAllSessions);
                oAllSessions.Clear();
                Monitor.Exit(oAllSessions);
                GC.Collect();
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            //看字面意思知道是啥，但实际起到啥作用。。。鬼才知道，官方例程里有这句，加上吧，管它呢。
            FiddlerApplication.Prefs.SetBoolPref("fiddler.network.streaming.abortifclientaborts", true);
            //启动代理服务————启动参数1：捕捉https；启动参数2：允许局域网其他终端连入本代理
            //FiddlerApplication.Startup(iStartPort, FiddlerCoreStartupFlags.DecryptSSL | FiddlerCoreStartupFlags.AllowRemoteClients | FiddlerCoreStartupFlags.Default, null);
            FiddlerCoreStartupSettings fiddlerCoreStartupSettings = new FiddlerCoreStartupSettingsBuilder()
                .ListenOnPort(iStartPort)
                .RegisterAsSystemProxy()
                .DecryptSSL()
                .AllowRemoteClients()
                .Build();
            FiddlerApplication.Startup(fiddlerCoreStartupSettings);
            //创建https代理
            oSecureEndpoint = FiddlerApplication.CreateProxyEndpoint(iSecureEndpointPort, true, oRootCert);

            MessageBox.Show("代理服务启动成功！");
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            //解绑事件处理————当会话结束之后
            FiddlerApplication.AfterSessionComplete -= On_AfterSessionComplete;

            MessageBox.Show("解绑事件处理");
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            if (FiddlerApplication.oProxy != null && FiddlerApplication.oProxy.IsAttached)
            {
                FiddlerApplication.oProxy.Detach();
                oSecureEndpoint.Detach();
            }

            FiddlerApplication.Shutdown();

            MessageBox.Show("代理服务器已关闭");
        }

        private async void Button_Click_5(object sender, RoutedEventArgs e)
        {
            HttpClient GhostClient = new();
            if (FiddlerApplication.oProxy != null && FiddlerApplication.oProxy.IsAttached)
            {
                HttpClientHandler handler = new()
                {
                    Proxy = new WebProxy($"http://localhost:{iStartPort}"),
                    UseProxy = true,
                };
                GhostClient = new HttpClient(handler);
            }
            txt2.Clear();
            try
            {
                var res = await GhostClient.GetAsync("https://www.m884d.com/");
                string html = await res.Content.ReadAsStringAsync();
                Dispatcher.Invoke(new Action(() =>
                {
                    txt2.Text = html;
                }));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    txt2.Text = ex.Message;
                }));
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            //解绑事件处理————当会话结束之后
            FiddlerApplication.AfterSessionComplete -= On_AfterSessionComplete;

            if (FiddlerApplication.oProxy != null && FiddlerApplication.oProxy.IsAttached)
            {
                FiddlerApplication.oProxy.Detach();
                oSecureEndpoint.Detach();
            }

            FiddlerApplication.Shutdown();
        }
    }
}
