using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Configurations;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Helpers;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Models;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Exceptions;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Models;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Repository
{
    public class JobRepository : IJobRepository
    {
        private readonly IAzureTableStorageClient _azureTableStorageClient;
        private readonly bool _requireUserIdentity;
        public JobRepository(IConfigurationProvider configurationProvider, IAzureTableStorageClientFactory tableStorageClientFactory)
        {
            var connectionString = configurationProvider.GetConfigurationSettingValue("device.StorageConnectionString");
            var tableName = configurationProvider.GetConfigurationSettingValueOrDefault("JobTableName", "JobList");
            _requireUserIdentity = CheckRequireUserOrNot(configurationProvider); 
            _azureTableStorageClient = tableStorageClientFactory.CreateClient(connectionString, tableName);
        }

        public async Task AddAsync(JobRepositoryModel job)
        {
            if (_requireUserIdentity)
            {
                await AddAsyncWithUser(job);
            }
            else
            {
                await AddAsyncWithoutUser(job);
            }
        }

        public async Task AddAsyncWithoutUser(JobRepositoryModel job)
        {
            var entity = new JobTableEntity(job);
            var result = await _azureTableStorageClient.DoTableInsertOrReplaceAsync(entity, e => (object)null);

            if (result.Status != TableStorageResponseStatus.Successful)
            {
                throw new JobRepositorySaveException(job.JobId);
            }
        }


        public async Task AddAsyncWithUser(JobRepositoryModel job)
        {
            var entity = new JobTableEntity(job);
            string user = null;
            try {
                user = System.Web.HttpContext.Current.User.Identity.Name.Split('@')[0];
            }
            catch
            {
                user = string.Empty;
            }
            entity.JobCreatorAlias = user;
            var result = await _azureTableStorageClient.DoTableInsertOrReplaceAsync(entity, e => (object)null);

            if (result.Status != TableStorageResponseStatus.Successful)
            {
                throw new JobRepositorySaveException(job.JobId);
            }
        }

        public async Task DeleteAsync(string jobId)
        {
            var entity = await GetEntityAsync(jobId);
            var result = await _azureTableStorageClient.DoDeleteAsync(entity, e => (object)null);

            if (result.Status != TableStorageResponseStatus.Successful)
            {
                throw new JobRepositoryRemoveException(jobId);
            }
        }

        public async Task<JobRepositoryModel> QueryByJobIDAsync(string jobId)
        {
            var entity = await GetEntityAsync(jobId);

            return new JobRepositoryModel(entity);
        }

        public async Task<IEnumerable<JobRepositoryModel>> QueryByFilterIdAsync(string filterId)
        {
            if (string.IsNullOrWhiteSpace(filterId))
            {
                throw new ArgumentNullException(nameof(filterId));
            }

            var query = new TableQuery<JobTableEntity>().Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, filterId));
            var entities = await _azureTableStorageClient.ExecuteQueryAsync(query);

            return entities.Select(e => new JobRepositoryModel(e));
        }

        public async Task<IEnumerable<JobRepositoryModel>> UpdateAssociatedFilterNameAsync(IEnumerable<JobRepositoryModel> jobs)
        {
            var tasks = jobs.Select(async job => {
                var operation = TableOperation.Replace(new JobTableEntity(job) { ETag = "*" });
                try
                {
                    return await _azureTableStorageClient.ExecuteAsync(operation);
                }
                catch
                {
                    return null;
                }
            });

            var tableResults = await Task.WhenAll(tasks);
            return tableResults.Select(r =>
            {
                JobRepositoryModel model = null;
                if (r?.Result != null && r.Result.GetType() == typeof(JobTableEntity))
                {
                    model = new JobRepositoryModel((JobTableEntity)r.Result);
                }
                return model;
            });
        }

        private async Task<JobTableEntity> GetEntityAsync(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                throw new ArgumentNullException(nameof(jobId));
            }

            var query = new TableQuery<JobTableEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, jobId));
            var entities = await _azureTableStorageClient.ExecuteQueryAsync(query);

            if (!entities.Any())
            {
                throw new JobNotFoundException(jobId);
            }

            if (entities.Count() > 1)
            {
                throw new DuplicatedJobFoundException(jobId);
            }

            return entities.Single();
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