using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Countersoft.Gemini.Extensibility.Apps;
using Countersoft.Foundation.Commons.Extensions;
using Microsoft.TeamFoundation.Client;
using System.Net;
using Microsoft.TeamFoundation.Framework.Common;
using Microsoft.TeamFoundation.Framework.Client;
using System.Collections.ObjectModel;
using Microsoft.TeamFoundation.VersionControl.Client;
using Countersoft.Gemini.Commons.Entity;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Proxy;
using System.Web.Mvc;
using Countersoft.Gemini.Infrastructure;
using Countersoft.Gemini.Infrastructure.Apps;
using Microsoft.TeamFoundation;
using Countersoft.Gemini.Commons.System;
using Countersoft.Gemini.Commons.Dto;
using Countersoft.Gemini.Contracts;
using System.Web;
using System.Web.UI;
using System.Web.Routing;
using Countersoft.Gemini;

namespace TFSPicker
{
    internal static class Constants
    {
        public static string AppId = "782D003D-D9F0-455F-AF09-74417D6DFD2B";
        public static string ControlId = "061F1F58-35BC-4509-83FC-CCC996ED4F36";
    }

    public class UserWidgetDataDetails
    {
        public string RepositoryUrl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class TFSPicker 
    {
        private string Username { get; set; }
        private string Password { get; set; }
        private string RepositoryUrl { get; set; }
        public static bool IsBasicAuth { get; set; }

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

        public class ConnectByImplementingCredentialsProvider : ICredentialsProvider
        {
            private string Username { get; set; }
            
            private string Password { get; set; }
            
            private string RepositoryUrl { get; set; }

            public ICredentials GetCredentials(Uri uri, ICredentials iCredentials)
            {
                return new NetworkCredential(Username, Password);
            }

            public void NotifyCredentialsAuthenticated(Uri uri)
            {
                throw new ApplicationException("Unable to authenticate");
            }

            public void setLoginDetails(string authUsername, string authPassword, string authRepositoryUrl)
            {
                Username = authUsername;
                
                Password = authPassword;
                
                RepositoryUrl = authRepositoryUrl;
            }
        }

        public bool AuthenticateUser(ItemWidgetArguments args)
        {
            UserWidgetData<UserWidgetDataDetails> userDataRaw = args.GeminiContext.UserWidgetStore.Get<UserWidgetDataDetails>(args.UserContext.User.Entity.Id, Constants.AppId, Constants.ControlId);

            if (userDataRaw == null) return false;

            Username = userDataRaw.Value.Username;
            
            Password = SecretsHelper.Decrypt(userDataRaw.Value.Password, SecretsHelper.EncryptionKey);
            
            RepositoryUrl = userDataRaw.Value.RepositoryUrl;

            return true;
        }

        public void setLoginDetails(string authUsername, string authPassword, string authRepositoryUrl)
        {
            Username = authUsername;
            
            Password = authPassword;
            
            RepositoryUrl = authRepositoryUrl;
        }

        public UserWidgetDataDetails getLoginDetails()
        {
            UserWidgetDataDetails user = new UserWidgetDataDetails();
            
            user.Password = Password;
            
            user.Username = Username;
            
            user.RepositoryUrl = RepositoryUrl;

            return user;
        }

    }

    [AppType(AppTypeEnum.Widget),
    AppGuid("782D003D-D9F0-455F-AF09-74417D6DFD2B"),
    AppControlGuid("061F1F58-35BC-4509-83FC-CCC996ED4F36"),
    AppAuthor("Countersoft"),
    AppKey("TfsPicker"),
    AppName("TFSPicker"),
    AppDescription("TFSPicker")]
    [OutputCache(Duration = 0, NoStore = false, Location = OutputCacheLocation.None)]
    public class TfsPickerController : BaseAppController
    {
        ContentResult dataView = null;
        
        bool successView = true;
        
        string messageView = string.Empty;
        
        private bool _validLicense;
        
        private string Username { get; set; }
        
        private string Password { get; set; }
        
        private string RepositoryUrl { get; set; }
        
        public static bool IsBasicAuth { get; set; }
        //bool isTfs2012 = true;

        public override WidgetResult Caption(IssueDto issue)
        {
            WidgetResult result = new WidgetResult();
            
            result.Success = true;
            
            result.Markup.Html = "TFS Picker";

            return result;
        }

        public override void RegisterRoutes(RouteCollection routes)
        {
            routes.MapRoute(null, "apps/tfspicker/search", new { controller = "TfsPicker", action = "Search" });
            
            routes.MapRoute(null, "apps/tfspicker/add", new { controller = "TfsPicker", action = "Add" });
            
            routes.MapRoute(null, "apps/tfspicker/delete", new { controller = "TfsPicker", action = "Delete" });
            
            routes.MapRoute(null, "apps/tfspicker/authenticate/{issueid}", new { controller = "TfsPicker", action = "Authenticate" });
            
            routes.MapRoute(null, "apps/tfspicker/logout", new { controller = "TfsPicker", action = "Logout" });
        }

        public override WidgetResult Show(IssueDto issueItem)
        {
            WidgetResult result = new WidgetResult();

            if (!_validLicense)
            {
                _validLicense = !GeminiApp.LicenseSummary.IsFree;//new Countersoft.Gemini.Infrastructure.LicenseManager().HasValidLicense(AppId, false);
                
                if (!_validLicense)
                {
                    result.Markup = new WidgetMarkup(UnlicensedMessage);
                    result.Success = true;
                    return result;
                }
            }

            List<string> tfsDetails = new List<string>();
            
            IssueWidgetData<List<string>> data = GeminiContext.IssueWidgetStore.Get<List<string>>(issueItem.Entity.Id, Constants.AppId, Constants.ControlId);
            
            if (data != null && data.Value != null && data.Value.Count > 0)
            {
                tfsDetails = data.Value;
            }

            Dictionary<string, WorkItem> details = new Dictionary<string, WorkItem>();
            
            Pair<int, string> authenticationModel = new Pair<int, string>(issueItem.Entity.Id, string.Concat("apps/tfspicker/authenticate/", issueItem.Entity.Id));

            if (tfsDetails.Count > 0)
            {
                if (AuthenticateUser(issueItem))
                {
                    //IsTfs2012();
                    foreach (var tfs in tfsDetails)
                    {
                        try
                        {
                            string url;
                            
                            var item = GetItem(tfs, out url);
                            
                            if (item != null)
                            {
                                if (url == null) url = string.Format("{0}/web/UI/Pages/WorkItems/WorkItemEdit.aspx?id={1}&pguid={2}", RepositoryUrl, item.Id, item.Project.Guid);
                                /*if (isTfs2012)
                                {
                                   url = string.Format("{0}/DefaultCollection/Countersoft/_workitems#_a=edit&id={1}", RepositoryUrl, item.Id);
                                }
                                else
                                {
                                    url = string.Format("{0}/web/UI/Pages/WorkItems/WorkItemEdit.aspx?id={1}&pguid={2}", RepositoryUrl, item.Id, item.Project.Guid);
                                }*/
                                if (!details.ContainsKey(url))
                                {
                                    details.Add(url, item);
                                }
                            }

                            Dictionary<string, TfsPickerItem> tfsPickerModel = ConvertWorkItemsToTfsPickerItems(details);
                            
                            result.Markup = new WidgetMarkup("views\\items.cshtml", tfsPickerModel);
                        }
                        catch (Exception ex)
                        {
                            result.Markup = new WidgetMarkup("views\\authenticationForm.cshtml", authenticationModel);
                            
                            GeminiApp.LogException(new Exception(ex.Message) { Source = "TFS Picker" }, false);
                        }
                    }
                }
                else
                {
                    result.Markup = new WidgetMarkup("views\\authenticationForm.cshtml", authenticationModel);
                }
            }
            else
            {
                try
                {
                    if (AuthenticateUser(issueItem))
                    {
                        string url;
                        
                        var item = GetItem("", out url);
                        
                        Dictionary<string, TfsPickerItem> tfsPickerModel = ConvertWorkItemsToTfsPickerItems(details);
                        
                        result.Markup = new WidgetMarkup("views\\items.cshtml", tfsPickerModel);
                    }
                    else
                    {
                        result.Markup = new WidgetMarkup("views\\authenticationForm.cshtml", authenticationModel);
                    }
                }
                catch (Exception ex)
                {
                    result.Markup = new WidgetMarkup("views\\authenticationForm.cshtml", authenticationModel);
                    
                    GeminiApp.LogException(new Exception(ex.Message) { Source = "TFS Picker" }, false);
                }


            }

            result.Success = true;
            
            return result;
        }

        private Dictionary<string, TfsPickerItem> ConvertWorkItemsToTfsPickerItems(Dictionary<string, WorkItem> details)
        {
            Dictionary<string, TfsPickerItem> tfsPickerModel = new Dictionary<string, TfsPickerItem>();
            
            foreach (var workitem in details)
            {
                TfsPickerItem tfsPickerItem = new TfsPickerItem();
                
                tfsPickerItem.Id = workitem.Value.Id;
                
                tfsPickerItem.TypeName = workitem.Value.Type.Name;
                
                tfsPickerItem.Title = workitem.Value.Title;
                
                tfsPickerItem.Description = workitem.Value.Description;
                
                tfsPickerItem.ProjectName = workitem.Value.Project.Name;
                
                tfsPickerModel.Add(workitem.Key, tfsPickerItem);
            }

            return tfsPickerModel;
        }

        public bool AuthenticateUser(IssueDto args)
        {
            UserWidgetData<UserWidgetDataDetails> userDataRaw = GeminiContext.UserWidgetStore.Get<UserWidgetDataDetails>(CurrentUser.Entity.Id, Constants.AppId, Constants.ControlId);

            if (userDataRaw == null) return false;

            Username = userDataRaw.Value.Username;
            
            Password = SecretsHelper.Decrypt(userDataRaw.Value.Password, SecretsHelper.EncryptionKey);
            
            RepositoryUrl = userDataRaw.Value.RepositoryUrl;

            return true;
        }

        public class WorkItem2
        {
            public WorkItem Item { get; set; }
            
            public string BaseUrl { get; set; }

            public Uri FullUrl { get; set; }
        }

        public ActionResult Search(string id, string search)
        {
            try
            {
                ItemWidgetArguments args = new ItemWidgetArguments(UserContext, GeminiContext, Cache, System.Web.HttpContext.Current.Request, CurrentIssue);

                TFSPicker tfsPicker = new TFSPicker();
                
                tfsPicker.AuthenticateUser(args);
                
                UserWidgetDataDetails loginDetails = tfsPicker.getLoginDetails();

                TFSPicker.ConnectByImplementingCredentialsProvider connect = new TFSPicker.ConnectByImplementingCredentialsProvider();
                
                ICredentials iCred = new NetworkCredential(loginDetails.Username, loginDetails.Password);
                
                connect.setLoginDetails(loginDetails.Username, loginDetails.Password, loginDetails.RepositoryUrl);
                
                connect.GetCredentials(new Uri(loginDetails.RepositoryUrl), iCred);

                TfsConfigurationServer configurationServer = TfsConfigurationServerFactory.GetConfigurationServer(new Uri(loginDetails.RepositoryUrl));
                
                configurationServer.Credentials = iCred;
                
                if (TFSPicker.IsBasicAuth)
                {
                    configurationServer.ClientCredentials = new TfsClientCredentials(new BasicAuthCredential(iCred));
                }
                else
                {
                    configurationServer.ClientCredentials = new TfsClientCredentials(new WindowsCredential(iCred));
                }
                
                configurationServer.EnsureAuthenticated();

                CatalogNode catalogNode = configurationServer.CatalogNode;

                ReadOnlyCollection<CatalogNode> tpcNodes = catalogNode.QueryChildren(new Guid[] { CatalogResourceTypes.ProjectCollection }, false, CatalogQueryOptions.None);
                
                string url = string.Empty;
                
                List<WorkItem2> queryResults = new List<WorkItem2>();

                TfsTeamProjectCollection tpc = null;

                string query = "Select [Id], [Work Item Type], [Title], [State] From WorkItems Where [Title] Contains '" + search + "' Order By [Id] Asc";
                
                if (search.Trim().Length == 0)
                {
                    query = "Select [Id], [Work Item Type], [Title], [Description] From WorkItems Order By [Id] Asc";
                }

                foreach (CatalogNode tpcNode in tpcNodes)
                {
                    tpc = new TfsTeamProjectCollection(new Uri(string.Concat(loginDetails.RepositoryUrl, '/', tpcNode.Resource.DisplayName)), iCred);
                    
                    if (TFSPicker.IsBasicAuth) tpc.ClientCredentials = new TfsClientCredentials(new BasicAuthCredential(iCred));
                    
                    WorkItemStore workItemStore = (WorkItemStore)tpc.GetService(typeof(WorkItemStore));
                    
                    var result = workItemStore.Query(query);
                    
                    if (result != null)
                    {
                        TswaClientHyperlinkService hyperlinkService = null;
                        
                        try
                        {
                            hyperlinkService = ((TswaClientHyperlinkService)tpc.GetService(typeof(TswaClientHyperlinkService)));
                        }
                        catch
                        {
                        }

                        foreach (WorkItem res in result)
                        {
                            WorkItem2 item = new WorkItem2() { Item = res, BaseUrl = string.Concat(tpcNode.Resource.DisplayName, '/', res.AreaPath) };
                            
                            try
                            {
                                if (hyperlinkService != null)
                                {
                                    item.FullUrl = hyperlinkService.GetWorkItemEditorUrl(res.Id);
                                }
                            }
                            catch
                            {
                            }

                            queryResults.Add(item);
                        }
                    }
                }

                Dictionary<string, WorkItem> details = new Dictionary<string, WorkItem>();

                if (queryResults.Count > 0)
                {
                    IssueWidgetData<List<string>> data = GeminiContext.IssueWidgetStore.Get<List<string>>(id.ToInt(), Constants.AppId, Constants.ControlId);
                    
                    if (data == null || data.Value == null)
                    {
                        data = new IssueWidgetData<List<string>>();
                        
                        data.AppId = Constants.AppId;
                        
                        data.ControlId = Constants.ControlId;
                        
                        data.IssueId = id.ToInt();
                        
                        data.Value = new List<string>();
                    }

                    foreach (WorkItem2 item in queryResults)
                    {
                        //check if we are not already there!
                        if (data.Value.Contains(item.Item.Id.ToString())) continue;

                        /*if (isTfs2012)
                        {*/
                        if (item.FullUrl != null && item.FullUrl.ToString().HasValue())
                        {
                            url = item.FullUrl.ToString();
                        }
                        else
                        {
                            url = string.Format("{0}/{1}/_workitems#_a=edit&id={2}", loginDetails.RepositoryUrl, item.BaseUrl, item.Item.Id);
                        }

                        details.Add(url, item.Item);
                    }
                }
                
                Dictionary<string, TfsPickerItem> tfsPickerModel = ConvertWorkItemsToTfsPickerItems(details);
                
                dataView = Content(BaseController.RenderPartialViewToString(this, AppManager.Instance.GetAppUrl("782D003D-D9F0-455F-AF09-74417D6DFD2B", "views/search.cshtml"), tfsPickerModel));
            }
            catch (Exception ex)
            {
                Pair<int, string> authenticationModel = new Pair<int, string>(CurrentIssue.Entity.Id, string.Concat(UserContext.Url, "/apps/tfspicker/authenticate/", CurrentIssue.Entity.Id));

                dataView = Content(BaseController.RenderPartialViewToString(this, AppManager.Instance.GetAppUrl("782D003D-D9F0-455F-AF09-74417D6DFD2B", "views/authenticationForm.cshtml"), authenticationModel));
                
                successView = false;
                
                messageView = ex.Message;
                
                GeminiApp.LogException(new Exception(ex.Message) { Source = "TFS Picker" }, false);
            }

            return JsonSuccess(new { success = successView, data = dataView, message = messageView });
        }

        public ActionResult Add(int issueId, string tfsId)
        {
            IssueWidgetData<List<string>> data = GeminiContext.IssueWidgetStore.Get<List<string>>(issueId, Constants.AppId, Constants.ControlId);
            if (data == null || data.Value == null)
            {
                data = new IssueWidgetData<List<string>>();
                
                data.AppId = Constants.AppId;
                
                data.ControlId = Constants.ControlId;
                
                data.IssueId = issueId;
                
                data.Value = new List<string>();
            }

            data.Value.AddRange(tfsId.TrimEnd(',').Split(','));

            GeminiContext.IssueWidgetStore.Save(data);

            return JsonSuccess(AppManager.Instance.ItemContentWidgetsOnShow(this, UserContext, GeminiContext, Cache, UserContext.Issue, Constants.AppId, Constants.ControlId));
        }

        public ActionResult Delete(int issueId, string tfsRow)
        {
            IssueWidgetData<List<string>> data = GeminiContext.IssueWidgetStore.Get<List<string>>(issueId, Constants.AppId, Constants.ControlId);

            issueId = data.IssueId;

            if (data == null || data.Value == null)
            {
                return JsonError();
            }
            
            var index = data.Value.FindIndex(d => string.Compare(d, tfsRow, StringComparison.InvariantCultureIgnoreCase) == 0);
            
            if (index == -1) return JsonError();

            data.Value.RemoveAt(index);

            GeminiContext.IssueWidgetStore.Save(data);

            return JsonSuccess(AppManager.Instance.ItemContentWidgetsOnShow(this, UserContext, GeminiContext, Cache, UserContext.Issue, Constants.AppId, Constants.ControlId));

        }

        public ActionResult Logout()
        {
            var data = GeminiContext.UserWidgetStore.Get<UserWidgetDataDetails>(CurrentUser.Entity.Id, Constants.AppId, Constants.ControlId);
            
            if (data != null)
            {
                GeminiContext.UserWidgetStore.Delete(data.Id);
                return JsonSuccess();
            }

            return JsonError();
        }

        [AppUrl(@"authenticate/{issueid}")]
        public ActionResult Authenticate(int issueId)
        {
            //Authentication
            string username = Request["username"] ?? string.Empty;
            
            string password = Request["password"] ?? string.Empty;
            
            string repositoryUrl = Request["repositoryurl"] ?? string.Empty;

            string message = string.Empty;
            
            bool success = true;
            
            string dataView = string.Empty;

            if (username.IsEmpty() || password.IsEmpty() || repositoryUrl.IsEmpty())
            {
                message = "Please make sure Username, Password and Url are not empty";
                success = false;
            }

            if (success)
            {
                UserWidgetDataDetails userData = new UserWidgetDataDetails();
                
                userData.Username = username.Trim();
                
                userData.Password = SecretsHelper.Encrypt(password.Trim(), SecretsHelper.EncryptionKey);
                
                userData.RepositoryUrl = repositoryUrl.Trim();

                SaveLoginDetails(CurrentUser, userData, GeminiContext);

                TFSPicker tfsPicker = new TFSPicker();

                try
                {
                    ItemWidgetArguments args = new ItemWidgetArguments(UserContext, GeminiContext, Cache, System.Web.HttpContext.Current.Request, CurrentIssue);
                    
                    tfsPicker.AuthenticateUser(args);
                    
                    UserWidgetDataDetails loginDetails = tfsPicker.getLoginDetails();

                    TFSPicker.ConnectByImplementingCredentialsProvider connect = new TFSPicker.ConnectByImplementingCredentialsProvider();
                    
                    ICredentials iCred = new NetworkCredential(loginDetails.Username, loginDetails.Password);
                    
                    connect.setLoginDetails(loginDetails.Username, loginDetails.Password, loginDetails.RepositoryUrl);
                    
                    connect.GetCredentials(new Uri(loginDetails.RepositoryUrl), iCred);

                    TfsConfigurationServer configurationServer = TfsConfigurationServerFactory.GetConfigurationServer(new Uri(loginDetails.RepositoryUrl));
                    
                    configurationServer.Credentials = iCred;
                    
                    if (TFSPicker.IsBasicAuth)
                    {
                        configurationServer.ClientCredentials = new TfsClientCredentials(new BasicAuthCredential(iCred));
                    }
                    else
                    {
                        configurationServer.ClientCredentials = new TfsClientCredentials(new WindowsCredential(iCred));
                    }
                    
                    configurationServer.EnsureAuthenticated();
                }
                catch (Exception ex)
                {
                    var logindetails = GeminiContext.UserWidgetStore.Get<UserWidgetDataDetails>(CurrentUser.Entity.Id, Constants.AppId, Constants.ControlId);
                    
                    if (logindetails != null)
                    {
                        GeminiContext.UserWidgetStore.Delete(logindetails.Id);
                    }
                    
                    success = false;
                    
                    message = ex.Message;
                    
                    GeminiApp.LogException(new Exception(ex.Message) { Source = "TFS Picker" }, false);
                    
                    return JsonSuccess(new { success = success, message = message });
                }

                tfsPicker.setLoginDetails(userData.Username, password.Trim(), userData.RepositoryUrl);

                WidgetResult result = new WidgetResult();

                List<string> tfsDetails = new List<string>();
                
                IssueWidgetData<List<string>> data = GeminiContext.IssueWidgetStore.Get<List<string>>(issueId, Constants.AppId, Constants.ControlId);

                if (data != null && data.Value != null && data.Value.Count > 0)
                {
                    tfsDetails = data.Value;
                }

                List<WorkItem> details = new List<WorkItem>();

                foreach (var tfs in tfsDetails)
                {
                    try
                    {
                        string url;

                        UserWidgetDataDetails loginDetails = tfsPicker.getLoginDetails();

                        if (Username.IsEmpty())
                        {
                            Username = loginDetails.Username;
                        }

                        if (Password.IsEmpty())
                        {
                            Password = loginDetails.Password;
                        }

                        if (RepositoryUrl.IsEmpty())
                        {
                            RepositoryUrl = loginDetails.RepositoryUrl;
                        }

                        var item = GetItem(tfs, out url);
                        
                        if (item != null)
                        {
                            details.Add(item);
                        }
                    }
                    catch (Exception ex)
                    {
                        success = false;
                        
                        message = ex.Message;
                        
                        GeminiApp.LogException(new Exception(ex.Message) { Source = "TFS Picker" }, false);
                    }
                }
            }

            return JsonSuccess(new { success = success, message = message });
        }

        public void SaveLoginDetails(UserDto user, UserWidgetDataDetails userData, GeminiContext gemini)
        {
            UserWidgetData<UserWidgetDataDetails> userDataRaw = gemini.UserWidgetStore.Get<UserWidgetDataDetails>(user.Entity.Id, Constants.AppId, Constants.ControlId);

            if (userDataRaw == null)
            {
                var data = new UserWidgetData<UserWidgetDataDetails>();
                
                data.Value = new UserWidgetDataDetails();

                data.Value = userData;
                
                gemini.UserWidgetStore.Save(user.Entity.Id, Constants.AppId, Constants.ControlId, data.Value);
            }
            else
            {
                userDataRaw.Value.Password = userData.Password;
                
                userDataRaw.Value.Username = userData.Username;
                
                userDataRaw.Value.RepositoryUrl = userData.RepositoryUrl;

                gemini.UserWidgetStore.Save(user.Entity.Id, Constants.AppId, Constants.ControlId, userDataRaw.Value);
            }
        }

        public WorkItem GetItem(string id, out string url)
        {
            url = null;

            TFSPicker.ConnectByImplementingCredentialsProvider connect = new TFSPicker.ConnectByImplementingCredentialsProvider();

            ICredentials iCred = new NetworkCredential(Username, Password);
            
            connect.setLoginDetails(Username, Password, RepositoryUrl);
            
            connect.GetCredentials(new Uri(RepositoryUrl), iCred);

            TfsConfigurationServer configurationServer = TfsConfigurationServerFactory.GetConfigurationServer(new Uri(RepositoryUrl));
            
            configurationServer.Credentials = iCred;
            
            if (IsBasicAuth)
            {
                configurationServer.ClientCredentials = new TfsClientCredentials(new BasicAuthCredential(iCred));
            }
            else
            {
                configurationServer.ClientCredentials = new TfsClientCredentials(new WindowsCredential(iCred));
            }
            
            configurationServer.EnsureAuthenticated();

            CatalogNode catalogNode = configurationServer.CatalogNode;

            ReadOnlyCollection<CatalogNode> tpcNodes = catalogNode.QueryChildren(new Guid[] { CatalogResourceTypes.ProjectCollection }, false, CatalogQueryOptions.None);

            foreach (CatalogNode tpcNode in tpcNodes)
            {
                TfsTeamProjectCollection tpc = new TfsTeamProjectCollection(new Uri(string.Concat(RepositoryUrl, '/', tpcNode.Resource.DisplayName)), iCred);
            
                if (IsBasicAuth) tpc.ClientCredentials = new TfsClientCredentials(new BasicAuthCredential(iCred));

                WorkItemStore workItemStore = (WorkItemStore)tpc.GetService(typeof(WorkItemStore));

                WorkItemCollection queryResults = workItemStore.Query(string.Format("Select [Id], [Work Item Type], [Title], [State] From WorkItems WHERE [Id] = '{0}' Order By [Id] Asc", id));

                if (queryResults.Count >= 1)
                {
                    var item = queryResults[0];
                    
                    try
                    {
                        TswaClientHyperlinkService hyperlinkService = (TswaClientHyperlinkService)tpc.GetService(typeof(TswaClientHyperlinkService));
                        url = hyperlinkService.GetWorkItemEditorUrl(item.Id).ToString();
                    }
                    catch
                    {
                    }

                    return item;
                }
            }

            return null;
        }
    }
}
