using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Models;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Repository
{
    public interface IJobRepository
    {
        Task AddAsync(JobRepositoryModel job);
        Task DeleteAsync(string jobId);
        Task<JobRepositoryModel> QueryByJobIDAsync(string jobId);
        Task<IEnumerable<string>> QueryJobIDsByUserName(string userName);
        Task<IEnumerable<JobRepositoryModel>> QueryByFilterIdAsync(string filterId);
        Task<IEnumerable<JobResponse>> GetJobResponsesByStatus(JobStatus status);
        Task<IEnumerable<JobRepositoryModel>> UpdateAssociatedFilterNameAsync(IEnumerable<JobRepositoryModel> jobs);
    }
}
