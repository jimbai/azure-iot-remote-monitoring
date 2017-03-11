using GlobalResources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Web.DataTables;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Web.Helpers;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Web.Models;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Web.Security;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Models;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Repository;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Configurations;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Web.WebApiControllers
{
    [RoutePrefix("api/v1/jobs")]
    public class JobApiController : WebApiControllerBase
    {
        private readonly IJobRepository _jobRepository;
        private readonly IIoTHubDeviceManager _iotHubDeviceManager;
        private readonly bool _requireUserIdentity;

        public JobApiController(IJobRepository jobRepository, IIoTHubDeviceManager iotHubDeviceManager)
        {
            _jobRepository = jobRepository;
            _iotHubDeviceManager = iotHubDeviceManager;
            _requireUserIdentity = CheckRequireUserOrNot(new ConfigurationProvider());

        }

        [HttpGet]
        [Route("")]
        [WebApiRequirePermission(Permission.ViewJobs)]
        // GET: api/v1/jobs
        public async Task<HttpResponseMessage> GetJobs()
        {
            return await GetServiceResponseAsync<DataTablesResponse<DeviceJobModel>>(async () =>
            {
                var jobResponses = await _iotHubDeviceManager.GetJobResponsesAsync();

                var sortedJobs = jobResponses.Select(r => new DeviceJobModel(r)).OrderByDescending(j => j.StartTime).ToList();
                var tasks = sortedJobs.Select(job => AddMoreDetailsToJobAsync(job));
                await Task.WhenAll(tasks);
                if (_requireUserIdentity)
                {
                    string user = null;
                    try
                    {
                        user = System.Web.HttpContext.Current.User.Identity.Name.Split('@')[0];
                    }
                    catch
                    {
                        user = string.Empty;
                    }
                    List<DeviceJobModel> output = new List<DeviceJobModel>();
                    foreach(var job in sortedJobs)
                    {
                        if(await FindCreatorOfJob(job.JobId) == user)
                        {
                            output.Add(job);
                        }
                    }
                    //var filtertasks = sortedJobs.Select(async job => await FindCreatorOfJob(job.JobId) == user);
                    // await Task.WhenAll(filtertasks);
                    sortedJobs = output;
                }
                var dataTablesResponse = new DataTablesResponse<DeviceJobModel>()
                {
                    RecordsTotal = sortedJobs.Count,
                    Data = sortedJobs.ToArray()
                };

                return await Task.FromResult(dataTablesResponse);

            }, false);
        }

        [HttpPut]
        [Route("{id}/cancel")]
        [WebApiRequirePermission(Permission.ManageJobs)]
        // PUT: api/v1/jobs/{id}/cancel
        public async Task<HttpResponseMessage> CancelJob(string id)
        {
            return await GetServiceResponseAsync<DeviceJobModel>(async () =>
            {
                var jobResponse = await _iotHubDeviceManager.CancelJobByJobIdAsync(id);
                return new DeviceJobModel(jobResponse);
            });
        }

        [HttpGet]
        [Route("{id}/results")]
        [WebApiRequirePermission(Permission.ViewJobs)]
        // GET: api/v1/jobs/{id}/results
        public async Task<HttpResponseMessage> GetJobResults(string id)
        {
            return await GetServiceResponseAsync<IEnumerable<DeviceJob>>(async () =>
            {
                var jobResponses = await _iotHubDeviceManager.GetDeviceJobsByJobIdAsync(id);
                return jobResponses.OrderByDescending(j => j.LastUpdatedDateTimeUtc).ToList();
            });
        }

        private async Task AddMoreDetailsToJobAsync(DeviceJobModel job)
        {
            Task<JobRepositoryModel> queryJobTask = _jobRepository.QueryByJobIDAsync(job.JobId);
            await DeviceJobHelper.AddMoreDetailsToJobAsync(job, queryJobTask);
        }

        private async Task<string> FindCreatorOfJob(string jobid)
        {try {
            var job =  await _jobRepository.QueryByJobIDAsync(jobid);
           
            if (job.CreatorAlias == null) return "";
            else return job.CreatorAlias;
            }
            catch
            {
                return "";
            }
        }

        private bool CheckRequireUserOrNot(IConfigurationProvider configurationProvider)
        {
            try
            {
                //Config name will be changed in the future.
                var configItem = configurationProvider.GetConfigurationSettingValue("SuperAdminList");
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
