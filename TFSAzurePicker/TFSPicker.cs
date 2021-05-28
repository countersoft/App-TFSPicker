using System.Text;
using Countersoft.Gemini.Extensibility.Apps;
using Countersoft.Foundation.Commons.Extensions;
using System.Net;
using Countersoft.Gemini.Commons.Entity;
using Countersoft.Gemini.Commons.System;
using System.Web;

namespace TFSPicker
{
    internal static class Constants
    {
        public static string AppId = "782D003D-D9F0-455F-AF09-74417D6DFD2B";
        public static string ControlId = "061F1F58-35BC-4509-83FC-CCC996ED4F36";
    }

    public class TFSPicker 
    {
        private string Username { get; set; }
        private string Password { get; set; }
        private string RepositoryUrl { get; set; }
        public static bool IsBasicAuth { get; set; }

        static TFSPicker()
        {
            try
            {
                IsBasicAuth = System.Configuration.ConfigurationManager.AppSettings["gemini.tfs.basicauth"].ToBool();
            }
            catch
            {
            }
        }

        public TFSPicker()
        {
            try
            {
                IsBasicAuth = System.Configuration.ConfigurationManager.AppSettings["gemini.tfs.basicauth"].ToBool();
            }
            catch
            {
            }
        }


        public void setLoginDetails(string authUsername, string authPassword, string authRepositoryUrl)
        {
            Username = authUsername;
            
            Password = authPassword;
            
            RepositoryUrl = authRepositoryUrl;
        }

        //public UserWidgetDataDetails getLoginDetails()
        //{
        //    UserWidgetDataDetails user = new UserWidgetDataDetails();
            
        //    user.Password = Password;
            
        //    user.Username = Username;
            
        //    user.RepositoryUrl = RepositoryUrl;

        //    return user;
        //}

    }
}
