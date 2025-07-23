using FlickrNet;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Net;

namespace gFlickrUpload
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        Flickr flickr;

        string BW_FullPathPhoto;
        string BW_Set;
        string BW_lastSet = ".";
        string BW_PhotosetID = null;
        string BW_Title;
        string BW_Description;
        string BW_PhotoID;
        long BW_totalBytes;
        string userID;

        int initialFileCount;
        long lastSize = 0;
        int MAX_RETRY = 3;
        int nRetry = 0;

        string[] extensions = { ".jpg", ".avi", ".mp4", ".mpg", ".mov", ".jpeg", ".mpeg", ".png", ".gif", ".m4v",
                                ".JPG", ".AVI", ".MP4", ".MPG", ".MOV", ".JPEG", ".MPEG", ".PNG", ".GIF", ".M4V"};


        private readonly BackgroundWorker worker = new BackgroundWorker();

        public MainWindow()
        {
            Properties.Settings.Default.Upgrade();
            worker.DoWork += worker_DoWork;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;
            InitializeComponent();
        }

        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // autorizzazione flickr
            if (FlickrManager.OAuthToken == null || FlickrManager.OAuthToken.Token == null)
            {
                AuthForm form = new AuthForm();
                form.ShowDialog();
                if (FlickrManager.OAuthToken == null || FlickrManager.OAuthToken.Token == null)
                {
                    MessageBox.Show("You must authenticate before you can upload a photo.");
                    //ShowSimpleDialog(null, null);
                    this.Close();
                }

            }
            userID = FlickrManager.OAuthToken.UserId;
            flickr = FlickrManager.GetAuthInstance();
        }

        private async void ShowSimpleDialog(string title, string message, bool close = false)
        {
            var mySettings = new MetroDialogSettings()
            {
                AffirmativeButtonText = "Close",
                NegativeButtonText = "Go away!",
                FirstAuxiliaryButtonText = "Cancel"
            };

            MessageDialogResult result = await this.ShowMessageAsync(title, message, MessageDialogStyle.Affirmative, mySettings);

            if (result == MessageDialogResult.Affirmative && close)
            {
                this.Close();
            }
            /*
            if (result != MessageDialogResult.FirstAuxiliary)
            {

                await this.ShowMessageAsync("Result", "You said: " + (result == MessageDialogResult.Affirmative ? mySettings.AffirmativeButtonText : mySettings.NegativeButtonText +
                    Environment.NewLine + Environment.NewLine + "This dialog will follow the Use Accent setting."));
            }
            */
        }

        private void MetroWindow_Closed(object sender, EventArgs e)
        {
            Properties.Settings.Default.Save();
        }

        private void btnSetsFolder_Click(object sender, RoutedEventArgs e)
        {
            Ookii.Dialogs.Wpf.VistaFolderBrowserDialog dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            dlg.RootFolder = Environment.SpecialFolder.MyComputer;
            dlg.Description = "Please select the Sets folder:";
            dlg.UseDescriptionForTitle = true;
            dlg.ShowNewFolderButton = false;
            if ((bool)dlg.ShowDialog())
            {
                Properties.Settings.Default.FlickrFolder = dlg.SelectedPath;
                Properties.Settings.Default.Save();
            }
        }

        private void btnCopyFolder_Click(object sender, RoutedEventArgs e)
        {
            Ookii.Dialogs.Wpf.VistaFolderBrowserDialog dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            dlg.RootFolder = Environment.SpecialFolder.MyComputer;
            dlg.Description = "Please select the copy folder:";
            dlg.UseDescriptionForTitle = true;
            dlg.ShowNewFolderButton = true;
            if ((bool)dlg.ShowDialog())
            {
                Properties.Settings.Default.FlickrCopyFolder = dlg.SelectedPath;
                Properties.Settings.Default.Save();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Properties.Settings.Default.FlickrFolder))
            {
                ShowSimpleDialog("Error!", "Please enter the Sets folder...");
                return;
            }
            if (string.IsNullOrEmpty(Properties.Settings.Default.FlickrCopyFolder))
            {
                ShowSimpleDialog("Error!", "Please enter the Copy folder...");
                return;
            }

            // controllo che abbia lo slash al fondo
            if (!Properties.Settings.Default.FlickrFolder.EndsWith("\\"))
            {
                Properties.Settings.Default.FlickrFolder += "\\";
            }
            if (!Properties.Settings.Default.FlickrCopyFolder.EndsWith("\\"))
            {
                Properties.Settings.Default.FlickrCopyFolder += "\\";
            }

            flickr.HttpTimeout = int.MaxValue; //1000000; //100000;
            flickr.OnUploadProgress += flickr_OnUploadProgress;

            initialFileCount = getFilesCount(Properties.Settings.Default.FlickrFolder, SearchOption.AllDirectories);

            pgbFiles.Maximum = initialFileCount;

            process(Properties.Settings.Default.FlickrFolder);
        }

        private void process(string sFolder)
        {

            int fileCount = getFilesCount(Properties.Settings.Default.FlickrFolder, SearchOption.AllDirectories);
            /*
             * (from file in Directory.EnumerateFiles(sFolderToProcess, "*.*", SearchOption.AllDirectories)
                                select file).Count();
             */
            lblCount.Content = string.Format("{0} of {1}", initialFileCount - fileCount, initialFileCount);
            pgbFiles.Value = Math.Min(initialFileCount - fileCount, initialFileCount);
            // leggo le cartelle
            foreach (string sSet in getSets(sFolder))
            {
                BW_Set = sSet;
                lblSet.Content = BW_Set;

                foreach (string sFile in getPhotos(sFolder + sSet))
                {
                    BW_FullPathPhoto = string.Format(@"{0}{1}\{2}", sFolder, sSet, sFile);
                    BW_Title = System.IO.Path.GetFileNameWithoutExtension(BW_FullPathPhoto);
                    BW_Description = getPhotoTitle(BW_FullPathPhoto);

                    lblPhoto.Content = BW_Title;
                    /*
                    // ----------------------------------------------------
                    // precopio già!
                    string sFolderCopy = Properties.Settings.Default.FlickrCopyFolder + BW_Set;
                    // prima creo la cartella
                    if (!Directory.Exists(sFolderCopy))
                    {
                        Directory.CreateDirectory(sFolderCopy);
                    }

                    FileInfo file = new FileInfo(BW_FullPathPhoto);
                    file.MoveTo(sFolderCopy + "\\" + System.IO.Path.GetFileName(BW_FullPathPhoto));
                    BW_FullPathPhoto = sFolderCopy + "\\" + System.IO.Path.GetFileName(BW_FullPathPhoto);
                    // ----------------------------------------------------
                    */
                    worker.RunWorkerAsync();
                    //string photoId = flickr.UploadPicture(sFullPathPhoto, sTitle, sDescription, "", false, true, false);
                    break;
                }
                break;
            }
        }

        /// <summary>
        /// ottengo i sets (le cartelle da caricare)
        /// </summary>
        /// <param name="sFolderBase">cartella parent (_FLICKR_)</param>
        /// <returns>Lista ordinata alfabeticamente</returns>
        private List<string> getSets(string sFolderBase)
        {
            List<string> sRet = new List<string>();

            string[] sDirs = Directory.GetDirectories(sFolderBase);
            foreach (string sDir in sDirs)
            {
                string[] sElements = sDir.Split('\\');

                var folder = sFolderBase + sElements[sElements.Length - 1];
                // controllo se è vuota
                int nFilesInDir = getFilesCount(folder, SearchOption.TopDirectoryOnly);
                /*
                // controllo se è vuota
                int nFilesInDir = (from file in Directory.EnumerateFiles(sFolderBase + sElements[sElements.Length - 1], "*.*", SearchOption.TopDirectoryOnly)
                                    select file).Count();
                */
                if (nFilesInDir > 0)
                {
                    sRet.Add(sElements[sElements.Length - 1]);
                }
                else
                {
                    // provo a cancellare la cartella
                    if (Directory.GetFiles(folder).Length == 0)
                    {
                        Directory.Delete(folder);
                    }
                }
            }

            sRet.Sort();
            return sRet;
        }
        /// <summary>
        /// ottengo le foto
        /// </summary>
        /// <param name="sFolder">cartella completa (parent + set)</param>
        /// <returns>lista foto ordinata alfabeticamente</returns>
        private List<string> getPhotos(string sFolder)
        {
            return getFiles(sFolder, true);
        }

        /// <summary>
        /// leggo il titolo della foto
        /// </summary>
        /// <param name="sFile">percorso completo della foto</param>
        /// <returns></returns>
        private string getPhotoTitle(string sFile)
        {
            try
            {
                System.Drawing.Image image = new System.Drawing.Bitmap(sFile);

                // Get the PropertyItems property from image.
                PropertyItem[] propItems = image.PropertyItems;
                System.Text.ASCIIEncoding encoding = new System.Text.ASCIIEncoding();
                var item = propItems[0];
                if (item.Len <= 1)
                {
                    return "";
                }
                string description = encoding.GetString(propItems[0].Value);
                description = description.Trim();
                description = description.Replace("\0", "");
                image.Dispose();
                image = null;
                return description;
            }
            catch (System.Exception ex)
            {
                return "";
            }
        }

        /// <summary>
        /// ottengo la lista dei files con estensione jpg, avi o mp4 presenti nella cartella
        /// </summary>
        /// <param name="sDirectory"></param>
        /// <param name="useOnlyFileName">true ottengo il nome file, altrimenti il percorso completo</param>
        /// <returns></returns>
        private List<string> getFiles(string sDirectory, bool useOnlyFileName)
        {
            if (useOnlyFileName)
            {
                var filteredFiles = Directory
                    .EnumerateFiles(sDirectory, "*", SearchOption.AllDirectories)
                    .Select(System.IO.Path.GetFileName)
                    .Where(s => extensions.Any(ext => ext == System.IO.Path.GetExtension(s)) &&
                    !s.StartsWith(".")).ToList();

                filteredFiles.Sort();
                return filteredFiles;
            }
            else
            {
                throw new Exception("WRONG PARAMETER");
            }

            /*
            var filteredFiles = Directory
            .GetFiles(sDirectory, "*.*")
            .Where(file => file.ToLower().EndsWith("jpg")
                || file.ToLower().EndsWith("avi")
                || file.ToLower().EndsWith("mp4")
                || file.ToLower().EndsWith("mpg")
                || file.ToLower().EndsWith("mov"))
            .ToList();

            List<string> filteredFilesGood = new List<string>();
            if (useOnlyFileName)
            {
                for (int i = 0; i < filteredFiles.Count; i++)
                {
                    var singleFile = System.IO.Path.GetFileName(filteredFiles[i]);
                    if (!singleFile.StartsWith("."))
                    {
                        filteredFilesGood.Add(singleFile);
                    }
                }
            }

            filteredFiles.Sort();
            return filteredFiles;
            */ 
        }

        /// <summary>
        /// ottengo il numero di files con estensione jpg, avi o mp4 presenti nella cartella
        /// </summary>
        /// <param name="sDirectory">cartella base</param>
        /// <param name="option"></param>
        /// <returns></returns>
        private int getFilesCount(string sDirectory, SearchOption option)
        {
            /*
            int nFilesInDir = Directory
           .GetFiles(sDirectory, "*.*", option)
           .Where(file => (file.ToLower().EndsWith("jpg")
               || file.ToLower().EndsWith("avi")
               || file.ToLower().EndsWith("mp4")
               || file.ToLower().EndsWith("mpg")
               || file.ToLower().EndsWith("mov"))
               &&
               (!file.ToLower().StartsWith("."))
               )
           .Count();
            */
            var filenames4 = Directory
                .EnumerateFiles(sDirectory, "*", SearchOption.AllDirectories)
                .Select(System.IO.Path.GetFileName)
                .Where(s => extensions.Any(ext => ext == System.IO.Path.GetExtension(s)) &&
                !s.StartsWith("."));
            int count = filenames4.Count();
            return count;
        }

        /// <summary>Constructs a download speed indicator string.</summary>
        /// <param name="bytes">Bytes per second transfer rate.</param>
        /// <param name="time">Is a time value (e.g. bytes/second)</param>
        /// <returns>String represenation of the transfer rate in bytes/sec, KB/sec, MB/sec, etc.</returns>
        static string BytesToString(double bytes, bool time)
        {
            int i = 0;
            while (bytes >= 0.9 * 1024)
            {
                bytes /= 1024;
                i++;
            }

            // don't show N.NN bytes when *not* dealing with time values (e.g. 8.88 bytes/sec).
            return (time || i > 0) ? string.Format("{0:0.00} {1}", bytes, units[i]) : string.Format("{0} {1}", bytes, units[i]);
        }

        static readonly string[] units = new[] { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

        void flickr_OnUploadProgress(object sender, UploadProgressEventArgs e)
        {
            int valPC = Math.Min(100, e.ProcessPercentage);
            valPC = Math.Max(0, valPC);

            long realSent = Math.Min(e.BytesSent, e.TotalBytesToSend);
            BW_totalBytes = e.TotalBytesToSend;

            long totSizeTmp = lastSize + realSent;

            string sizeFormatted = string.Format("{0} of {1} [{2}]", BytesToString(realSent, false), BytesToString(e.TotalBytesToSend, false), BytesToString(totSizeTmp, false));
            pgbSize.Dispatcher.BeginInvoke((Action)(() => { pgbSize.Value = valPC; }));
            lblBytes.Dispatcher.BeginInvoke((Action)(() => { lblBytes.Content = sizeFormatted; }));
            //lblBytes.Dispatcher.BeginInvoke((Action)(() => { lblBytes.Content = BytesToString(totSizeTmp, false); }));
        }

        void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // cancello la foto
            if (!string.IsNullOrEmpty(BW_PhotoID))
            {
                
                string sFolderCopy = Properties.Settings.Default.FlickrCopyFolder + BW_Set;
                // prima creo la cartella
                if (!Directory.Exists(sFolderCopy))
                {
                    Directory.CreateDirectory(sFolderCopy);
                }

                try
                {
                    FileInfo file = new FileInfo(BW_FullPathPhoto);
                    file.MoveTo(sFolderCopy + "\\" + System.IO.Path.GetFileName(BW_FullPathPhoto));
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                    Close();
                    /*   
                    try
                    {
                        
                        System.Threading.Thread.Sleep(5000);
                        FileInfo file = new FileInfo(BW_FullPathPhoto);
                        file.MoveTo(sFolderCopy + "\\" + System.IO.Path.GetFileName(BW_FullPathPhoto));
                    }
                    catch (System.Exception ex1)
                    {
                    	try
                    	{
                            System.Threading.Thread.Sleep(5000);
                            FileInfo file = new FileInfo(BW_FullPathPhoto);
                            file.MoveTo(sFolderCopy + "\\" + System.IO.Path.GetFileName(BW_FullPathPhoto));
                        }
                    	catch (System.Exception ex2)
                    	{
                    		
                    	}
                    }
                    */ 
                }
                
                //File.Move(BW_FullPathPhoto, sFolderCopy + "\\" + System.IO.Path.GetFileName(BW_FullPathPhoto));
                if (!(bool)ckbPause.IsChecked)
                {
                    nRetry = 0;
                    process(Properties.Settings.Default.FlickrFolder);
                }
                else
                {
                    nRetry = 0;
                    ckbPause.IsChecked = false;
                    pgbSize.Value = 0;
                    lblBytes.Content = "";
                    lblCount.Content = "Pause Active";
                }
                BW_PhotoID = null;
                lastSize += BW_totalBytes;
            }
            else
            {
                if (nRetry < MAX_RETRY)
                {
                    process(Properties.Settings.Default.FlickrFolder);
                    txbLog.Content += string.Format("{0} r:{1}\r\n", BW_Title, nRetry);
                    nRetry++;
                }
                else
                {
                    ShowSimpleDialog("Error!", "Error uploading " + BW_Title);
                }
            }
        }

        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                BW_PhotoID = flickr.UploadPicture(BW_FullPathPhoto,
                    BW_Title,
                    BW_Description,
                    null,
                    Properties.Settings.Default.IsPublic,
                    Properties.Settings.Default.IsFamily,
                    Properties.Settings.Default.IsFriends);

                bool bSetChanged = BW_lastSet != BW_Set;
                bool bNoPhotoset = string.IsNullOrEmpty(BW_PhotosetID);

                if (bSetChanged || bNoPhotoset)
                {
                    BW_lastSet = BW_Set;
                    //Flickr f = new Flickr("cff5d61fdfa8ece5f3ab70835d5ec5c2", "3cbc8ed408d3555a", "72157634588192699-07c5d0d06d1caf71");
                    //f.HttpTimeout = 1000000; //100000;

                    PhotosetCollection sets = flickr.PhotosetsGetList(userID); //1, 5000);

                    int setFound = -1;
                    for (int i = 0; i < sets.Count; i++)
                    {
                        if (sets[i].Title.ToLowerInvariant().Trim() == BW_Set.ToLowerInvariant().Trim())
                        {
                            setFound = i;
                            break;
                        }
                    }

                    if (setFound == -1)
                    {
                        Photoset ps = flickr.PhotosetsCreate(BW_Set, BW_PhotoID);
                        BW_PhotosetID = ps.PhotosetId;
                    }
                    else
                    {
                        BW_PhotosetID = sets[setFound].PhotosetId;
                    }
                    flickr.PhotosetsAddPhoto(BW_PhotosetID, BW_PhotoID);
                }
                else
                {
                    flickr.PhotosetsAddPhoto(BW_PhotosetID, BW_PhotoID);
                }
            }
            catch (System.Exception ex)
            {
                //MessageBox.Show(ex.ToString());
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // paypal //
            Process.Start("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=S8LZW7C3BPJF6");
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            // download!
            PhotosetCollection sets = flickr.PhotosetsGetList(userID);
            foreach (var item in sets)
            {
                var newFolder = $"{Properties.Settings.Default.FlickrFolder}\\{item.Title}";
                if (!Directory.Exists(newFolder))
                {
                    Directory.CreateDirectory(newFolder);
                }
                var photos = flickr.PhotosetsGetPhotos(item.PhotosetId);
                foreach (var photo in photos)
                {
                    var url = photo.OriginalUrl;
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFile(new Uri(url), $@"{newFolder}\{photo.Title}.jpg");
                    }
                }
                /*
                PhotoSearchOptions o = new PhotoSearchOptions();
                o.Extras = PhotoSearchExtras.AllUrls | PhotoSearchExtras.Description | PhotoSearchExtras.OwnerName;
                o.SortOrder = PhotoSearchSortOrder.Relevance;
                */
                //o.Tags = textBox1.Text;

                return;
            }
        }
    }
}
