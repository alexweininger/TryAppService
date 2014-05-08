﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Hosting;
using Kudu.Client.Editor;
using Microsoft.WindowsAzure.Management.WebSites.Models;
using Newtonsoft.Json;
using SimpleWAWS.Kudu;

namespace SimpleWAWS.Code
{
    public class Site
    {
        private WebSpace _webSpace;
        private WebSite _webSite;
        private WebSiteGetConfigurationResponse _config;
        private WebSiteGetPublishProfileResponse _publishingProfile;

        private const string IsSimpleWAWSKey = "SIMPLE_WAWS";
        private const string InUseMetadataKey = "IN_USE";

        public Site(WebSpace webSpace, WebSite webSite)
        {
            _webSpace = webSpace;
            _webSite = webSite;
        }

        public Site(WebSite webSite, WebSiteGetConfigurationResponse config)
        {
            _webSite = webSite;
            _config = config;
        }

        public async Task LoadConfigurationAsync()
        {
            _config = await _webSpace.GetConfigurationAsync(Name);
            _publishingProfile = await _webSpace.GetPublishingProfile(Name);
        }

        public async Task InitializeNewSite()
        {
            var updateParams = Util.CreateWebSiteUpdateConfigurationParameters();

            // Mark it as our site
            updateParams.Metadata = new Dictionary<string, string> {
                {IsSimpleWAWSKey, "1"}
            };

            // Turn on Monaco
            updateParams.AppSettings = new Dictionary<string, string> {
                {"WEBSITE_NODE_DEFAULT_VERSION", "0.10.21"},
                {"MONACO_EXTENSION_VERSION", "beta"}
            };

            await _webSpace.UpdateConfigurationAsync(Name, updateParams);
            Trace.TraceInformation("Updated initial config for site '{0}' in {1}", this, _webSpace);

            // Get all the configuration
            _config = await _webSpace.GetConfigurationAsync(Name);

            var credentials = new NetworkCredential(PublishingUserName, PublishingPassword);
            var vfsManager = new RemoteVfsManager(ScmUrl + "vfs/", credentials);
            await vfsManager.Put("site/applicationHost.xdt", HostingEnvironment.MapPath("~/App_Data/applicationHost.xdt"));
            var processManager = new ProcessManager(ScmUrl, credentials);
            await processManager.Kill();

            Trace.TraceInformation("Read the configuration for site '{0}' in {1}", this, _webSpace);
        }

        [JsonProperty("name")]
        public string Name { get { return _webSite.Name; } }

        [JsonProperty("id")]
        // We use the password as an ID so users can't access other users's sites
        public string Id { get { return PublishingPassword; } }

        [JsonProperty("isSimpleWAWS")]
        public bool IsSimpleWAWS
        {
            get
            {
                return _config.Metadata.ContainsKey(IsSimpleWAWSKey);
            }
        }

        [JsonProperty("isInUse")]
        public bool IsInUse
        {
            get
            {
                return _config.Metadata.ContainsKey(InUseMetadataKey);
            }
        }

        [JsonProperty("url")]
        public string Url
        {
            get {
                return String.Format("http://{0}/", _webSite.HostNames[0]);
            }
        }

        [JsonProperty("scmUrl")]
        public string ScmUrl
        {
            get
            {
                string scmHostName = _webSite.EnabledHostNames.First(n => n.Contains(".scm."));
                return String.Format("https://{0}/", scmHostName);
            }
        }

        [JsonProperty("scmUrlWithCreds")]
        public string ScmUrlWithCreds
        {
            get
            {
                string scmHostName = _webSite.EnabledHostNames.First(n => n.Contains(".scm."));
                return String.Format("https://{0}:{1}@{2}/", PublishingUserName, PublishingPassword, scmHostName);
            }
        }

        [JsonProperty("kuduConsoleWithCreds")]
        public string KuduConsoleWithCreds
        {
            get
            {
                return ScmUrlWithCreds + "DebugConsole";
            }
        }

        [JsonProperty("gitUrlWithCreds")]
        public string GitUrlWithCreds
        {
            get
            {
                return ScmUrlWithCreds + Name + ".git";
            }
        }

        [JsonProperty("monacoUrl")]
        public string MonacoUrl
        {
            get
            {
                return Url + "dev";
            }
        }

        [JsonProperty("contentDownloadUrl")]
        public string ContentDownloadUrl
        {
            get
            {
                return ScmUrlWithCreds + "zip/site/wwwroot";
            }
        }

        [JsonProperty("timeLeftString")]
        public string TimeLeftString
        {
            get
            {
                TimeSpan timeUsed = DateTime.UtcNow - StartTime;
                TimeSpan timeLeft;
                if (timeUsed > SiteManager.SiteExpiryTime)
                {
                    timeLeft = TimeSpan.FromMinutes(0);
                }
                else
                {
                    timeLeft = SiteManager.SiteExpiryTime - timeUsed;
                }

                return String.Format("{0}m:{1:D2}s", timeLeft.Minutes, timeLeft.Seconds);
            }
        }

        [JsonProperty("startTime")]
        public DateTime StartTime { get { return _webSite.LastModifiedTimeUtc; } }

        [JsonProperty("publishingUrl")]
        public string PublishingUrl
        {
            get
            {
                return _publishingProfile.PublishProfiles.FirstOrDefault() == null
                    ? null
                    : _publishingProfile.PublishProfiles.FirstOrDefault().PublishUrl;
            }
        }

        [JsonProperty("publishingUserName")]
        public string PublishingUserName { get { return _config.PublishingUserName; } }

        [JsonProperty("publishingPassword")]
        public string PublishingPassword { get { return _config.PublishingPassword; } }

        public Task DeleteAndCreateReplacementAsync()
        {
            return _webSpace.DeleteAndCreateReplacementAsync(this);
        }

        public async Task MarkAsInUseAsync()
        {
            _webSite.LastModifiedTimeUtc = DateTime.UtcNow;

            var updateParams = Util.CreateWebSiteUpdateConfigurationParameters();
            _config.Metadata[InUseMetadataKey] = "true";
            updateParams.Metadata = _config.Metadata;

            await _webSpace.UpdateConfigurationAsync(Name, updateParams);
        }

        public override string ToString()
        {
            return Name;
        }

    }
}
