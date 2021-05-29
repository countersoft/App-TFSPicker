using System;

namespace TFSAzurePicker
{
    public class UserWidgetDataDetails
    {

        public string Organisation { get; set; }
        public string Project { get; set; }


        public string RepositoryUrl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime Expiry { get; internal set; }
    }
}
