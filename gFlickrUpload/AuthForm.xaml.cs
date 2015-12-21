using FlickrNet;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace gFlickrUpload
{
    /// <summary>
    /// Interaction logic for AuthForm.xaml
    /// </summary>
    public partial class AuthForm : MetroWindow
    {
        private OAuthRequestToken _requestToken;

        public AuthForm()
        {
            InitializeComponent();
        }

        private void btnAuth_Click(object sender, RoutedEventArgs e)
        {
            Flickr f = FlickrManager.GetInstance();
            _requestToken = f.OAuthGetRequestToken("oob");

            string url = f.OAuthCalculateAuthorizationUrl(_requestToken.Token, AuthLevel.Write);

            System.Diagnostics.Process.Start(url);

            gbStep2.IsEnabled = true;
        }

        private void btnComplete_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(txbVerifier.Text))
            {
                //MessageBox.Show("You must paste the verifier code into the textbox above.");
                ShowSimpleDialog("Warning", "You must paste the verifier code into the textbox!");
                return;
            }

            Flickr f = FlickrManager.GetInstance();
            try
            {
                var accessToken = f.OAuthGetAccessToken(_requestToken, txbVerifier.Text);
                FlickrManager.OAuthToken = accessToken;
                //ResultLabel.Text = "Successfully authenticated as " + accessToken.Username;
                ShowSimpleDialog("All done!", "Successfully authenticated as " + accessToken.Username, true);
            }
            catch (FlickrApiException ex)
            {
                //MessageBox.Show("Failed to get access token. Error message: " + ex.Message);
                ShowSimpleDialog("Error!", "Failed to get access token. Error message: " + ex.Message);
            }
        }

        private async void ShowSimpleDialog(string title, string message, bool close = false)
        {
            var mySettings = new MetroDialogSettings()
            {
                AffirmativeButtonText = "Ok",
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
    }
}
