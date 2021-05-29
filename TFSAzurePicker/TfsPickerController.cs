using System;
using System.Collections.Generic;
using System.Linq;
using Countersoft.Gemini.Extensibility.Apps;
using Countersoft.Foundation.Commons.Extensions;
using System.Net;
using System.Collections.ObjectModel;
using Countersoft.Gemini.Commons.Entity;
using System.Web.Mvc;
using Countersoft.Gemini.Infrastructure;
using Countersoft.Gemini.Infrastructure.Apps;
using Countersoft.Gemini.Commons.System;
using Countersoft.Gemini.Commons.Dto;
using Countersoft.Gemini.Contracts;
using System.Web.UI;
using System.Web.Routing;
using Countersoft.Gemini;
using Countersoft.Gemini.Authentication.OAuth;
using Microsoft.VisualStudio.Services.Common;
using Countersoft.Gemini.Commons.Entity.System;
using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;

namespace TFSAzurePicker
{

    public class TFSAuthenticationModel
    {
        public int IssueId { get; set; }
        public string Url { get; set; }
        public bool NoOAuthClient { get; internal set; }
    }


    [AppType(AppTypeEnum.Widget),
    AppGuid( TfsAzureConstants.AppId ),
    AppControlGuid( TfsAzureConstants.ControlId ),
    AppAuthor("Countersoft"),
    AppKey("tfsazurepicker"),
    AppName("tfsazurepicker"),
    AppDescription("tfsazurepicker")]
    [OutputCache(Duration = 0, NoStore = false, Location = OutputCacheLocation.None)]
    public class tfsazurepickerController : BaseAppController
    {
        ContentResult dataView = null;
        
        bool successView = true;
        
        string messageView = string.Empty;
        
        private bool _validLicense;
        
        private string RepositoryUrl { get; set; }

        private string Token { get; set; }


        public override WidgetResult Caption(IssueDto issue)
        {
            WidgetResult result = new WidgetResult();
            
            result.Success = true;
            
            result.Markup.Html = "TFS Azure Picker";

            return result;
        }

        public override void RegisterRoutes(RouteCollection routes)
        {
            routes.MapRoute(null, "apps/tfsazurepicker/search", new { controller = "tfsazurepicker", action = "Search" });
            
            routes.MapRoute(null, "apps/tfsazurepicker/add", new { controller = "tfsazurepicker", action = "Add" });
            
            routes.MapRoute(null, "apps/tfsazurepicker/delete", new { controller = "tfsazurepicker", action = "Delete" });
            
            routes.MapRoute(null, "apps/tfsazurepicker/authenticate/{issueid}", new { controller = "tfsazurepicker", action = "Authenticate" });
            
            routes.MapRoute(null, "apps/tfsazurepicker/logout", new { controller = "tfsazurepicker", action = "Logout" });
        }

        public override WidgetResult Show(IssueDto issueItem)
        {
            WidgetResult result = new WidgetResult();

            if (!_validLicense)
            {
                _validLicense = !GeminiApp.LicenseSummary.IsFree || GeminiApp.LicenseSummary.IsGeminiTrial();
                
                if (!_validLicense)
                {
                    result.Markup = new WidgetMarkup(UnlicensedMessage);
                    result.Success = true;
                    return result;
                }
            }

            List<string> tfsDetails = new List<string>();
            

            //Check if OAuth Configured
            var oauthClient = GeminiContext.OAuthClients
                .FindWhere( c => c.App == OAuthSupportedApps.TFS )
                .FirstOrDefault();
            TFSAuthenticationModel authModel = new TFSAuthenticationModel();
            authModel.IssueId = issueItem.Entity.Id;
            ITokenProvider provider = null;
            if ( oauthClient == null )
            {
                authModel.NoOAuthClient = true;
                authModel.Url = GeminiApp.UserContext().Url + "/configure";
                result.Markup = new WidgetMarkup( "views\\authenticationForm.cshtml", authModel );
                result.Success = true;
                return result;
            }

            OAuthTokenManager manager = new OAuthTokenManager( GeminiApp.UserContext().Url );
            provider = manager.Create( oauthClient, GeminiApp.UserContext().User.Entity.Id );

            Token = provider.AquireTokenSilently();
            if(Token == null || string.IsNullOrEmpty( Token ) )
            {
                result.Markup = new WidgetMarkup( "views\\authenticationForm.cshtml", authModel );
                authModel.Url = provider.GetAuthorizationUrl();
                result.Success = true;
                return result;
            }
            var userDetails = GetUserDetails();
            if ( string.IsNullOrEmpty( userDetails.Organisation ) )
            {
                authModel.Url = provider.GetAuthorizationUrl();
                result.Markup = new WidgetMarkup( "views\\authenticationForm.cshtml", authModel );
                result.Success = true;
                return result;
            }

            Dictionary<string, WorkItem> details = new Dictionary<string, WorkItem>();

            IssueWidgetData<List<string>> data = GeminiContext.IssueWidgetStore.Get<List<string>>( issueItem.Entity.Id, TfsAzureConstants.AppId, TfsAzureConstants.ControlId );
            if ( data != null && data.Value != null && data.Value.Count > 0 )
            {
                tfsDetails = data.Value;
            }

            foreach ( var tfs in tfsDetails )
            {
                string url = string.Empty;
                try
                {
                    WorkItem item = GetItem( tfs );
                    if ( item != null )
                    {
                        if(item.Fields.ContainsKey("System.TeamProject"))
                        {
                            url = string.Format( "https://dev.azure.com/{0}/{1}/_workitems/edit/{2}", userDetails.Organisation, item.Fields["System.TeamProject"], item.Id );
                        }
                        if ( !details.ContainsKey( url ) )
                        {
                            details.Add( url, item );
                        }
                    }

                }
                catch (Exception ex)
                {
                    var tempWorkItem = new WorkItem();
                    var error = string.Format( "Error loading item {0} from Organisation {1}: {2} ", tfs, userDetails.Organisation, ex.Message );
                    if(ex.InnerException != null )
                    {
                        error += ex.InnerException.Message;
                    }
                    tempWorkItem.Fields.Add( "System.Description", error);
                    details.Add( "error" + tfs, tempWorkItem );
                }
            }

            Dictionary<string, TfsPickerItem> tfspickerModel = ConvertWorkItemsToPickerItems( details );
            result.Markup = new WidgetMarkup( "views\\items.cshtml", tfspickerModel );


            //Dictionary<string, WorkItem> details = new Dictionary<string, WorkItem>();

            //Pair<int, string> authenticationModel = new Pair<int, string>(issueItem.Entity.Id, string.Concat("apps/tfsazurepicker/authenticate/", issueItem.Entity.Id));
            //authModel.IssueId = issueItem.Entity.Id;


            //    IssueWidgetData<List<string>> data = GeminiContext.IssueWidgetStore.Get<List<string>>( issueItem.Entity.Id, TfsAzureConstants.AppId, TfsAzureConstants.ControlId );
            //if ( data != null && data.Value != null && data.Value.Count > 0 )
            //{
            //    tfsDetails = data.Value;
            //}


            //if ( tfsDetails.Count > 0)
            //{
            //    if (AuthenticateUser(issueItem, provider))
            //    {
            //        //IsTfs2012();
            //        foreach (var tfs in tfsDetails)
            //        {
            //            try
            //            {
            //                string url;

            //                var item = GetItem(tfs, out url);

            //                if (item != null)
            //                {
            //                    if (url == null) url = string.Format("{0}/web/UI/Pages/WorkItems/WorkItemEdit.aspx?id={1}&pguid={2}", RepositoryUrl, item.Id, item.Project.Guid);
            //                    /*if (isTfs2012)
            //                    {
            //                       url = string.Format("{0}/DefaultCollection/Countersoft/_workitems#_a=edit&id={1}", RepositoryUrl, item.Id);
            //                    }
            //                    else
            //                    {
            //                        url = string.Format("{0}/web/UI/Pages/WorkItems/WorkItemEdit.aspx?id={1}&pguid={2}", RepositoryUrl, item.Id, item.Project.Guid);
            //                    }*/
            //                    if (!details.ContainsKey(url))
            //                    {
            //                        details.Add(url, item);
            //                    }
            //                }

            //                Dictionary<string, tfsazurepickerItem> tfsazurepickerModel = ConvertWorkItemsTotfsazurepickerItems(details);

            //                result.Markup = new WidgetMarkup("views\\items.cshtml", tfsazurepickerModel);
            //            }
            //            catch (Exception ex)
            //            {
            //                result.Markup = new WidgetMarkup("views\\authenticationForm.cshtml", authModel);

            //                GeminiApp.LogException(new Exception(ex.Message) { Source = "TFS Picker" }, false);
            //            }
            //        }
            //    }
            //    else
            //    {
            //        result.Markup = new WidgetMarkup("views\\authenticationForm.cshtml", authModel);
            //    }
            //}
            //else
            //{
            //    try
            //    {
            //        if (AuthenticateUser(issueItem, provider))
            //        {
            //            string url;
            //            WorkItem item = null;
            //            if ( authModel.IsOAuth )
            //            {

            //            }
            //            else
            //            {
            //                item = GetItem( "", out url );
            //            }

            //            Dictionary<string, tfsazurepickerItem> tfsazurepickerModel = ConvertWorkItemsTotfsazurepickerItems(details);

            //            result.Markup = new WidgetMarkup("views\\items.cshtml", tfsazurepickerModel);
            //        }
            //        else
            //        {
            //            result.Markup = new WidgetMarkup("views\\authenticationForm.cshtml", authModel);
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        result.Markup = new WidgetMarkup("views\\authenticationForm.cshtml", authModel);

            //        GeminiApp.LogException(new Exception(ex.Message) { Source = "TFS Picker" }, false);
            //    }


            //}

            result.Success = true;
            
            return result;
        }
        
        private Dictionary<string, TfsPickerItem> ConvertWorkItemsToPickerItems(Dictionary<string, WorkItem> details)
        {
            Dictionary<string, TfsPickerItem> tfsazurepickerModel = new Dictionary<string, TfsPickerItem>();
            
            foreach (var workitem in details)
            {
                TfsPickerItem pickerItem = new TfsPickerItem();
                
                pickerItem.Id = workitem.Value.Id.GetValueOrDefault();
                if( workitem.Value.Fields.ContainsKey("System.WorkItemType" )) {
                    pickerItem.TypeName = workitem.Value.Fields["System.WorkItemType"].ToString();
                }
                if( workitem.Value.Fields.ContainsKey( "System.Title" ))
                {
                    pickerItem.Title = workitem.Value.Fields["System.Title"].ToString();
                }
                if ( workitem.Value.Fields.ContainsKey( "System.Description" ) )
                {
                    pickerItem.Description = workitem.Value.Fields["System.Description"].ToString();
                }
                if ( workitem.Value.Fields.ContainsKey( "System.TeamProject" ) )
                {
                    pickerItem.ProjectName = workitem.Value.Fields["System.TeamProject"].ToString();
                }
                tfsazurepickerModel.Add(workitem.Key, pickerItem);
            }

            return tfsazurepickerModel;
        }

        [AppUrl( "saveinstance" )]
        public ActionResult SaveInstance()
        {
            var userData = base.GeminiContext.UserWidgetStore.Get<UserWidgetDataDetails>( UserContext.User.Entity.Id, TfsAzureConstants.AppId, TfsAzureConstants.ControlId );
            if ( userData == null )
            {
                userData = new UserWidgetData<UserWidgetDataDetails>();

                userData.Value = new UserWidgetDataDetails();
            }
            userData.Value.Organisation = Request["organisation"] ?? string.Empty;
            userData.Value.Project = Request["project"] ?? string.Empty;

            GeminiContext.UserWidgetStore.Save( UserContext.User.Entity.Id, TfsAzureConstants.AppId, TfsAzureConstants.ControlId, userData.Value );

            return JsonSuccess();
        }

        private UserWidgetDataDetails GetUserDetails()
        {
            var userData = base.GeminiContext.UserWidgetStore.Get<UserWidgetDataDetails>( UserContext.User.Entity.Id, TfsAzureConstants.AppId, TfsAzureConstants.ControlId );
            return userData == null ? new UserWidgetDataDetails() : userData.Value;
        }

        [AppUrl("Search")]
        public ActionResult Search( string id, string search )
        {
            //ItemWidgetArguments args = new ItemWidgetArguments( UserContext, GeminiContext, Cache, System.Web.HttpContext.Current.Request, CurrentIssue );
            
            OAuthTokenManager manager = new OAuthTokenManager( GeminiApp.UserContext().Url );
            var client = GeminiContext.OAuthClients
                .FindWhere( c => c.App == OAuthSupportedApps.TFS )
                .FirstOrDefault();
            var provider = manager.Create( client, UserContext.User.Entity.Id );
            Token = provider.AquireTokenSilently();
            string query = "Select [Id], [Work Item Type], [Title], [State] From WorkItems Where [Title] Contains '" + search + "' Order By [Id] Asc";

            if ( search.Trim().Length == 0 )
            {
                query = "Select [Id], [Work Item Type], [Title], [Description] From WorkItems Order By [Id] Asc";
            }

            var wiql = new Wiql()
            {
                Query = query
            };
            UserWidgetDataDetails loginDetails = GetUserDetails();
            Dictionary<string, WorkItem> details = new Dictionary<string, WorkItem>();
            try
            {
                using ( var httpClient = GetClient() )
                {
                    var result = httpClient.QueryByWiqlAsync( wiql ).Result;
                    var ids = result.WorkItems.Select( item => item.Id ).ToArray();
                    if ( ids.Length == 0 )
                    {
                        //handle empty list
                    }
                    else
                    {
                        var fields = new[] { "System.Id", "System.Title", "System.State", "System.Description", "System.WorkItemType", "System.TeamProject" };
                        var items = httpClient.GetWorkItemsAsync( ids, fields, result.AsOf ).Result;
                        foreach ( var item in items )
                        {
                            var url = string.Format( "https://dev.azure.com/{0}/{1}/_workitems/edit/{2}/", loginDetails.Organisation, item.Fields["System.TeamProject"], item.Id );
                            details.Add( url, item );
                        }
                        // return Content( items.ToJson() );
                    }
                }
            }
            catch ( Exception ex )
            {
                TFSAuthenticationModel auth = new TFSAuthenticationModel()
                {
                    IssueId = CurrentIssue.Entity.Id,
                    NoOAuthClient = false,
                    Url = provider.GetAuthorizationUrl()
                };

                dataView = Content( BaseController.RenderPartialViewToString( this, 
                    AppManager.Instance.GetAppUrl( TfsAzureConstants.AppId, "views/authenticationForm.cshtml" ), auth ) );

                successView = false;

                messageView = ex.Message;

                GeminiApp.LogException( new Exception( ex.Message ) { Source = "TFS Picker" }, false );
            }
            
            Dictionary<string, TfsPickerItem> tfsazurepickerModel = ConvertWorkItemsToPickerItems( details );

            dataView = Content( BaseController.RenderPartialViewToString( this, AppManager.Instance.GetAppUrl( TfsAzureConstants.AppId, "views/search.cshtml" ), tfsazurepickerModel ) );

            return JsonSuccess( new { success = successView, data = dataView, message = messageView } );


        }

        [AppUrl("Add")]
        public ActionResult Add( int issueId, string tfsId )
        {
            IssueWidgetData<List<string>> data = 
                GeminiContext.IssueWidgetStore.Get<List<string>>( issueId, TfsAzureConstants.AppId, TfsAzureConstants.ControlId );
            if ( data == null || data.Value == null )
            {
                data = new IssueWidgetData<List<string>>();
                data.AppId = TfsAzureConstants.AppId;
                data.ControlId = TfsAzureConstants.ControlId;
                data.IssueId = issueId;
                data.Value = new List<string>();
            }

            data.Value.AddRange( tfsId.TrimEnd( ',' ).Split( ',' ) );

            GeminiContext.IssueWidgetStore.Save( data );

            var result = AppManager.Instance.ItemContentWidgetsOnShow( this, UserContext, GeminiContext, Cache, UserContext.Issue, TfsAzureConstants.AppId, TfsAzureConstants.ControlId );
            return JsonSuccess(result) ;
        }

        [AppUrl( "Delete" )]
        public ActionResult Delete( int issueId, string tfsRow )
        {
            IssueWidgetData<List<string>> data = GeminiContext.IssueWidgetStore.Get<List<string>>( issueId, TfsAzureConstants.AppId, TfsAzureConstants.ControlId );

            issueId = data.IssueId;

            if ( data == null || data.Value == null )
            {
                return JsonError();
            }

            var index = data.Value.FindIndex( d => string.Compare( d, tfsRow, StringComparison.InvariantCultureIgnoreCase ) == 0 );

            if ( index == -1 ) return JsonError();

            data.Value.RemoveAt( index );

            GeminiContext.IssueWidgetStore.Save( data );

            return JsonSuccess( AppManager.Instance.ItemContentWidgetsOnShow( this, UserContext, GeminiContext, Cache, UserContext.Issue, TfsAzureConstants.AppId, TfsAzureConstants.ControlId ) );

        }


        [AppUrl("Logout")]
        public ActionResult Logout()
        {
            var data = GeminiContext.UserWidgetStore.Get<UserWidgetDataDetails>( CurrentUser.Entity.Id, TfsAzureConstants.AppId, TfsAzureConstants.ControlId );

            if ( data != null )
            {
                GeminiContext.UserWidgetStore.Delete( data.Id );
                return JsonSuccess();
            }

            return JsonError();
        }


        /*
                public bool AuthenticateUser(IssueDto args, ITokenProvider provider )
                {
                    UserWidgetData<UserWidgetDataDetails> userDataRaw = GeminiContext.UserWidgetStore.Get<UserWidgetDataDetails>(CurrentUser.Entity.Id, Constants.AppId, Constants.ControlId);

                    var token = provider.AquireTokenSilently();
                    if(token != null )
                    {
                        return true;
                    }

                    if (userDataRaw == null && token == null) return false;

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

                        tfsazurepicker tfsazurepicker = new TFSAzurePicker();

                        try
                        {
                            ItemWidgetArguments args = new ItemWidgetArguments(UserContext, GeminiContext, Cache, System.Web.HttpContext.Current.Request, CurrentIssue);

                            tfsazurepicker.AuthenticateUser(args);

                            UserWidgetDataDetails loginDetails = tfsazurepicker.getLoginDetails();

                            TFSAzurePicker.ConnectByImplementingCredentialsProvider connect = new TFSAzurePicker.ConnectByImplementingCredentialsProvider();

                            ICredentials iCred = new NetworkCredential(loginDetails.Username, loginDetails.Password);

                            connect.setLoginDetails(loginDetails.Username, loginDetails.Password, loginDetails.RepositoryUrl);

                            connect.GetCredentials(new Uri(loginDetails.RepositoryUrl), iCred);

                            TfsConfigurationServer configurationServer = TfsConfigurationServerFactory.GetConfigurationServer(new Uri(loginDetails.RepositoryUrl));


                            configurationServer.Credentials = iCred;

                            if (tfsazurepicker.IsBasicAuth)
                            {
                                configurationServer.ClientCredentials = new TfsClientCredentials(new BasicAuthCredential(iCred));
                            }
                            else
                            {
                                configurationServer.ClientCredentials = new TfsClientCredentials(new WindowsCredential(iCred));
                            }

                            try
                            {
                                configurationServer.EnsureAuthenticated();
                            }
                            catch
                            {
                                System.Threading.Thread.Sleep(1000);
                                configurationServer = TfsConfigurationServerFactory.GetConfigurationServer(new Uri(loginDetails.RepositoryUrl));
                                configurationServer.Credentials = iCred;

                                if (tfsazurepicker.IsBasicAuth)
                                {
                                    configurationServer.ClientCredentials = new TfsClientCredentials(new BasicAuthCredential(iCred));
                                }
                                else
                                {
                                    configurationServer.ClientCredentials = new TfsClientCredentials(new WindowsCredential(iCred));
                                }
                                configurationServer.EnsureAuthenticated();
                            }
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

                        tfsazurepicker.setLoginDetails(userData.Username, password.Trim(), userData.RepositoryUrl);

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

                                UserWidgetDataDetails loginDetails = tfsazurepicker.getLoginDetails();

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

                */

        private WorkItemTrackingHttpClient GetClient()
        {
            var vssCred = new VssOAuthAccessTokenCredential( Token );

            UserWidgetDataDetails loginDetails = GetUserDetails();

            var uri = new Uri( "https://dev.azure.com/" + loginDetails.Organisation );

            return new WorkItemTrackingHttpClient( uri, vssCred );
        }
        public WorkItem GetItem( string id )
        {
            var qry = string.Format( "Select [Id], [Work Item Type], [Title], [State] From WorkItems WHERE [Id] = '{0}' Order By [Id] Asc", id );
            var wiql = new Wiql()
            {
                Query = qry
            };
            
            using ( var httpClient = GetClient() )
            {
                var workitem = httpClient.GetWorkItemAsync( id.ToInt() ).Result;
                return workitem;
            }
        }
        /*
        public WorkItem GetItem1(string id, out string url)
        {
            url = null;

            TFSAzurePicker.ConnectByImplementingCredentialsProvider connect = new TFSAzurePicker.ConnectByImplementingCredentialsProvider();

            ICredentials iCred = new NetworkCredential(Username, Password);

            connect.setLoginDetails(Username, Password, RepositoryUrl);

            connect.GetCredentials(new Uri(RepositoryUrl), iCred);

            TfsConfigurationServer configurationServer = TfsConfigurationServerFactory.GetConfigurationServer(new Uri(RepositoryUrl));


            configurationServer.Credentials = iCred;

            if (TFSAzurePicker.IsBasicAuth)
            {
                configurationServer.ClientCredentials = new TfsClientCredentials(new BasicAuthCredential(iCred));
            }
            else
            {
                configurationServer.ClientCredentials = new TfsClientCredentials(new WindowsCredential(iCred));
            }

            try
            {
                configurationServer.EnsureAuthenticated();
            }
            catch
            {
                System.Threading.Thread.Sleep(1000);
                configurationServer = TfsConfigurationServerFactory.GetConfigurationServer(new Uri(RepositoryUrl));
                configurationServer.Credentials = iCred;

                if (TFSAzurePicker.IsBasicAuth)
                {
                    configurationServer.ClientCredentials = new TfsClientCredentials(new BasicAuthCredential(iCred));
                }
                else
                {
                    configurationServer.ClientCredentials = new TfsClientCredentials(new WindowsCredential(iCred));
                }
                configurationServer.EnsureAuthenticated();
            }

            CatalogNode catalogNode = configurationServer.CatalogNode;

            ReadOnlyCollection<CatalogNode> tpcNodes = catalogNode.QueryChildren(new Guid[] { CatalogResourceTypes.ProjectCollection }, false, CatalogQueryOptions.None);

            foreach (CatalogNode tpcNode in tpcNodes)
            {
                TfsTeamProjectCollection tpc = configurationServer.GetTeamProjectCollection(new Guid(tpcNode.Resource.Properties["InstanceId"]));
                //TfsTeamProjectCollection tpc = new TfsTeamProjectCollection(new Uri(string.Concat(RepositoryUrl, '/', tpcNode.Resource.DisplayName)), iCred);

                if (TFSAzurePicker.IsBasicAuth) tpc.ClientCredentials = new TfsClientCredentials(new BasicAuthCredential(iCred));

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

        */
    }
}
