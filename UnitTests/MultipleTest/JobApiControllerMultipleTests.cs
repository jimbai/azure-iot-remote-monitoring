using Moq;
using Ploeh.AutoFixture;
using System.Collections.Generic;
using Xunit;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Web.WebApiControllers;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Repository;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Web.Models;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Models;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Web.DataTables;
using System;
using System.Net;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.UnitTests.Web.WebApiControllers
{
    public class JobApiControllerMultipleTests : IDisposable
    {
        private readonly JobApiController controller;
        private Mock<IJobRepository> jobRepository;
        private Mock<IIoTHubDeviceManager> iotHubDeviceManager;
        private readonly Fixture fixture;

        public JobApiControllerMultipleTests()
        {
            jobRepository = new Mock<IJobRepository>();
            iotHubDeviceManager = new Mock<IIoTHubDeviceManager>();
            controller = new JobApiController(jobRepository.Object, iotHubDeviceManager.Object);
            controller.InitializeRequest();
            fixture = new Fixture();
        }

        [Fact]
        public async void GetUnfilteredJobsTest()
        {
            List<string> jobResponses = new List<string>()
            {
                @"{
                    ""jobId"": ""73439503-321d-417a-8df6-e816bd618285"",
                    ""queryCondition"": ""select * from devices where deviceId = 'bb544c4d-1e45-4fed-83ef-aee17eb3810a'"",
                    ""createdTime"": ""2016-11-29T07:21:12.4816525Z"",
                    ""startTime"": ""2016-11-29T07:21:11.4793989Z"",
                    ""endTime"": ""2016-11-29T07:22:00.6324486Z"",
                    ""maxExecutionTimeInSeconds"": 3600,
                    ""type"": ""scheduleUpdateTwin"",
                    ""updateTwin"": {
                           ""deviceId"": null,
                           ""etag"": ""*"",
                           ""tags"": {""position"": ""Redmond""},
                           ""properties"": {""desired"": {},""reported"": {}}
                    },
                    ""status"": ""completed"",
                }",
            };
            JobRepositoryModel repositoryModel = fixture.Create<JobRepositoryModel>();
            //assign "" value to creator alias to match the user alias in test environment.
            repositoryModel.CreatorAlias = "";
            iotHubDeviceManager.Setup(x => x.GetJobResponsesAsync()).ReturnsAsync(jobResponses);
            jobRepository.Setup(x => x.QueryByJobIDAsync(It.IsNotNull<string>())).ReturnsAsync(repositoryModel);
            var result = await controller.GetJobs();
            result.AssertOnError();
            var table = result.ExtractContentAs<DataTablesResponse<DeviceJobModel>>();
            //not a real environment, user alias is string.empty matching the mock value. 
            //so the api controller will not filter out this record, data length is 1.
            Assert.Equal(table.Data.Length, 1);
        }

        [Fact]
        public async void GetfilteredJobsTest()
        {
            List<string> jobResponses = new List<string>()
            {
                @"{
                    ""jobId"": ""73439503-321d-417a-8df6-e816bd618285"",
                    ""queryCondition"": ""select * from devices where deviceId = 'bb544c4d-1e45-4fed-83ef-aee17eb3810a'"",
                    ""createdTime"": ""2016-11-29T07:21:12.4816525Z"",
                    ""startTime"": ""2016-11-29T07:21:11.4793989Z"",
                    ""endTime"": ""2016-11-29T07:22:00.6324486Z"",
                    ""maxExecutionTimeInSeconds"": 3600,
                    ""type"": ""scheduleUpdateTwin"",
                    ""updateTwin"": {
                           ""deviceId"": null,
                           ""etag"": ""*"",
                           ""tags"": {""position"": ""Redmond""},
                           ""properties"": {""desired"": {},""reported"": {}}
                    },
                    ""status"": ""completed"",
                }",
            };
            JobRepositoryModel repositoryModel = fixture.Create<JobRepositoryModel>();
            //assign "testadmin" value to creator alias to match the user alias in test environment.
            repositoryModel.CreatorAlias = "testadmin";
            iotHubDeviceManager.Setup(x => x.GetJobResponsesAsync()).ReturnsAsync(jobResponses);
            jobRepository.Setup(x => x.QueryByJobIDAsync(It.IsNotNull<string>())).ReturnsAsync(repositoryModel);
            var result = await controller.GetJobs();
            result.AssertOnError();
            var table = result.ExtractContentAs<DataTablesResponse<DeviceJobModel>>();
            //not a real environment, user alias is string.empty.The api controller will filter out all items.
            Assert.Equal(table.Data.Length, 1);
        }

        [Fact]
        public async void CancelJobTest()
        {
            JobRepositoryModel repositoryModel = fixture.Create<JobRepositoryModel>();
            repositoryModel.CreatorAlias = string.Empty;
            var jobResponse = fixture.Create<JobResponse>();
            iotHubDeviceManager.Setup(x => x.CancelJobByJobIdAsync(It.IsNotNull<string>())).ReturnsAsync(jobResponse);
            jobRepository.Setup(x => x.QueryByJobIDAsync("job1")).ReturnsAsync(repositoryModel);
            var result = await controller.CancelJob("job1");
            result.AssertOnError();
            result.ExtractContentDataAs<DeviceJobModel>();
        }

        [Fact]
        public async void CancleJobUnauthorizedTest()
        {
            JobRepositoryModel repositoryModel = fixture.Create<JobRepositoryModel>();
            repositoryModel.CreatorAlias = "someadmin";
            var jobResponse = fixture.Create<JobResponse>();
            iotHubDeviceManager.Setup(x => x.CancelJobByJobIdAsync(It.IsNotNull<string>())).ReturnsAsync(jobResponse);
            jobRepository.Setup(x => x.QueryByJobIDAsync("job1")).ReturnsAsync(repositoryModel);
            var result = await controller.CancelJob("job1");
            Assert.Equal(result.StatusCode, HttpStatusCode.Forbidden);

        }

        [Fact]
        public async void GetJobResultsTest()
        {
            JobRepositoryModel repositoryModel = fixture.Create<JobRepositoryModel>();
            repositoryModel.CreatorAlias = string.Empty;
            var jobResponses = fixture.Create<IEnumerable<DeviceJob>>();
            iotHubDeviceManager.Setup(x => x.GetDeviceJobsByJobIdAsync(It.IsNotNull<string>())).ReturnsAsync(jobResponses);
            jobRepository.Setup(x => x.QueryByJobIDAsync("job1")).ReturnsAsync(repositoryModel);
            var result = await controller.GetJobResults("job1");
            result.AssertOnError();
            result.ExtractContentDataAs<IEnumerable<DeviceJob>>();
        }

        [Fact]
        public async void GetJobUnauthorizedTest()
        {
            JobRepositoryModel repositoryModel = fixture.Create<JobRepositoryModel>();
            //in test scenario, getcurrentuser will return "". so in this case, current user is not creator of the job.
            repositoryModel.CreatorAlias = "someadmin";
            var jobResponses = fixture.Create<IEnumerable<DeviceJob>>();
            iotHubDeviceManager.Setup(x => x.GetDeviceJobsByJobIdAsync(It.IsNotNull<string>())).ReturnsAsync(jobResponses);
            jobRepository.Setup(x => x.QueryByJobIDAsync("job1")).ReturnsAsync(repositoryModel);
            var result = await controller.GetJobResults("job1");
            Assert.Equal(result.StatusCode, HttpStatusCode.Forbidden);
        }
        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    controller.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~JobApiControllerTests() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
