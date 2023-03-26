using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Windows;

namespace Amistake_Launcher
{
    enum LauncherStatus
    {
        ready,
        failed,
        downloadingGame,
        downloadingUpdate
    }

    public partial class MainWindow : Window
    {
        private string rootPath;
        private string gamePath;
        private string gameZip;
        private string gameExe;

        private LauncherStatus _status;
        internal LauncherStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                switch (_status)
                {
                    case LauncherStatus.ready:
                        PlayButton.Content = "Play";
                        break;
                    case LauncherStatus.failed:
                        PlayButton.Content = "Update Failed - Retry";
                        break;
                    case LauncherStatus.downloadingGame:
                        PlayButton.Content = "Downloading Game";
                        break;
                    case LauncherStatus.downloadingUpdate:
                        PlayButton.Content = "Downloading Update";
                        break;
                    default:
                        break;
                }
            }
        }


        public MainWindow()
        {
            InitializeComponent();

            rootPath = Directory.GetCurrentDirectory();
            gamePath = Path.Combine(rootPath, "game");
            gameZip = Path.Combine(rootPath, "windows64");
            gameExe = Path.Combine(gamePath, "game.exe");
            
        }
        private void Window_ContentRendered(object sender, EventArgs args)
        {
            CheckForUpdates();
        }
        private void PlayButton_Click(object sender, EventArgs args)
        {

        }


        private void CheckForUpdates()
        {
            if(!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("AMistake_MultiplayerPrototype")))
            {

                int localVersion = int.Parse(Environment.GetEnvironmentVariable("AMistake_MultiplayerPrototype"));
              

                try
                {
                    AMWebClient webClient = new AMWebClient();
                    //System.Net.ServicePointManager.ServerCertificateValidationCallback = (s, ce, ca, p) => true;
       
                    int version = getOnlineVersion();

                    if (version != localVersion)
                    {
                        InstallGameFiles(true, version);
                    }
                    else
                    {
                        Status = LauncherStatus.ready;
                    }
                }
                catch (Exception ex)
                {
                    Status = LauncherStatus.failed;
                    MessageBox.Show($"Error checking for game updates: {ex}");
                }
            }
            else
            {
                int version = getOnlineVersion();
                InstallGameFiles(false, version);
            }
        }

        static bool OnValidateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            var certPublicString = certificate?.GetPublicKeyString();
            Console.WriteLine("Mom I'm on TV:"+certPublicString);
            var PUBLIC_KEY = "3082010A0282010100CAAD99D553E566106A3ADD40EA41D43358F7E6683354353C69F9CA2923B4EB94F819B12D04EC68557B8355ED5ECFC53233AF547825B271945CB0A1174D523411DF7A5B09EC17FE3BE341153BA4B5B19B7109ED55E3B38945D2B1A46BFDC61FE70A50B00F9D90288E33920E91343F79B1B65A95B06ECDC9426B7F44CA9BCA0A12AE8FBABF4606946E4457C26D642266EF654C02FE4B67BFD0AB5A7C6822D122C1B1B342EADFB54E77D1E2E4E5F25B3F5182835B43A7629CC7DF01485CB3466C8CFA95D94AFA362F3FAF6C6EE23C497047D90B12BA2EE5FAF6F18C376459375398C9E79A322C39AB09B3678F98D008EA5CCA01B2C47924C8826AC4E356F019E62D0203010001";
            var keysMatch = PUBLIC_KEY == certPublicString;
            return keysMatch;
        }


        public static int getOnlineVersion()
        {

            ServicePointManager.ServerCertificateValidationCallback = OnValidateCertificate;
            Console.WriteLine("GETTING ONLINE VERSION");
            var version_json_string = new AMWebClient().DownloadString("https://nrwv2yxngcbjcw6n.myfritz.net:25565/artifact/MP/version/current");
            MessageBox.Show($"Got json: {version_json_string}");
            JObject version_json = JObject.Parse(version_json_string);
            int version = version_json.Value<int>("version");
            return version;
        }

        private void InstallGameFiles(bool _isUpdate, int version)
        {
            try
            {
                AMWebClient webClient = new AMWebClient();
                
                //System.Net.ServicePointManager.ServerCertificateValidationCallback = (s, ce, ca, p) => true;
                if (_isUpdate)
                {
                    Status = LauncherStatus.downloadingUpdate;
                }
                else
                {
                    Status = LauncherStatus.downloadingGame;
                    
                }
                
                webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadGameCompletedCallback);
                webClient.QueryString.Add("version", ""+version);
                webClient.DownloadFileAsync(new Uri("https://nrwv2yxngcbjcw6n.myfritz.net:25565/artifact/MP/version/current/download"), gameZip, version);
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error installing game files: {ex}");
            }
        }

        private void DownloadGameCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                string version = ((AMWebClient)(sender)).QueryString["version"];

                if (Directory.Exists(gamePath))
                {
                    var dir = new DirectoryInfo(gamePath);
                    dir.Delete(true);
                }

                ZipFile.ExtractToDirectory(gameZip, gamePath);
                File.Delete(gameZip);

                Environment.SetEnvironmentVariable("AMistake_MultiplayerPrototype", "0");
                VersionText.Text = version;
                Status = LauncherStatus.ready;
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error finishing download: {ex}");
            }
        }

        public void UnZip(string zipFile, string folderPath)
        {
            if (!File.Exists(zipFile))
                throw new FileNotFoundException();

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            Shell32.Shell objShell = new Shell32.Shell();
            Shell32.Folder destinationFolder = objShell.NameSpace(folderPath);
            Shell32.Folder sourceFile = objShell.NameSpace(zipFile);

            foreach (var file in sourceFile.Items())
            {
                destinationFolder.CopyHere(file, 4 | 16);
            }
        }



        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(gameExe) && Status == LauncherStatus.ready)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(gameExe);
                startInfo.WorkingDirectory = Path.Combine(rootPath, "Build");
                Process.Start(startInfo);

                Close();
            }
            else if (Status == LauncherStatus.failed)
            {
                CheckForUpdates();
            }
        }

        class AMWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                HttpWebRequest request = (HttpWebRequest)base.GetWebRequest(address);
                string strExeFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string strWorkPath = System.IO.Path.GetDirectoryName(strExeFilePath);
                string certPath = strWorkPath+"\\amistakeCert.crt";
                Console.WriteLine("PATH --->" +certPath);
                //request.ClientCertificates.Add(X509Certificate.CreateFromCertFile(certPath));
                return request;
            }
        }

    }

  

}




