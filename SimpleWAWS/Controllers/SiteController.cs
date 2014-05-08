﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.Http;
using SimpleWAWS.Code;

namespace SimpleWAWS.Controllers
{
    [RoutePrefix("api/site")]
    public class SiteController : ApiController
    {
        [Route("{siteId}")]
        [HttpGet]
        public async Task<Site> Get(string siteId)
        {
            var siteManager = await SiteManager.GetInstanceAsync();
            return siteManager.GetSite(siteId);
        }

        [Route("reset")]
        public async Task Get()
        {
            var siteManager = await SiteManager.GetInstanceAsync();
            await siteManager.ResetAllFreeSites();
        }

        [HttpPost]
        public async Task<Site> Post([FromBody] Template template)
        {
            var siteManager = await SiteManager.GetInstanceAsync();
            return
                await
                    siteManager.ActivateSiteAsync(template == null
                        ? null
                        : TemplatesManager.GetTemplates()
                            .SingleOrDefault(t => t.Name == template.Name && t.Language == template.Language)
                            .GetFullPath());
        }

        [Route("{siteId}")]
        [HttpDelete]
        public async Task Delete(string siteId)
        {
            var siteManager = await SiteManager.GetInstanceAsync();
            await siteManager.DeleteSite(siteId);
        }
    }
}