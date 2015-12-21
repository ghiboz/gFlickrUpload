using System;
using System.Net;
using System.Windows;
using FlickrNet;

namespace gFlickrUpload 
{
    public class FlickrManager
    {
        public const string ApiKey = "84a93ed2b16a9238226afe4797e3af06";
        public const string SharedSecret = "2d66f210a11737d1";

        public static Flickr GetInstance()
        {
            return new Flickr(ApiKey, SharedSecret);
        }

        public static Flickr GetAuthInstance()
        {
            var f = new Flickr(ApiKey, SharedSecret);
            f.OAuthAccessToken = OAuthToken.Token;
            f.OAuthAccessTokenSecret = OAuthToken.TokenSecret;
            return f;
        }

        public static OAuthAccessToken OAuthToken
        {
            get
            {
                return Properties.Settings.Default.OAuthToken;
            }
            set
            {
                Properties.Settings.Default.OAuthToken = value;
                Properties.Settings.Default.Save();
            }
        }
    }
}
