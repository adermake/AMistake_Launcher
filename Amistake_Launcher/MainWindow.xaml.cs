using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
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

        public static int getOnlineVersion()
        {
            var version_json_string = new AMWebClient().DownloadString("https://localhost:8080/artifact/MP/version/current");
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
                webClient.DownloadFileAsync(new Uri("https://localhost:8080/artifact/MP/version/current/download"), gameZip, version);
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
                string certPath = strWorkPath+"/amistakeCert.crt";
                
                request.ClientCertificates.Add(X509Certificate.CreateFromCertFile(certPath));
                return request;
            }
        }

    }

  

}




