using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Exceptions;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Helpers;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Models;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Exceptions;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Models;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Extensions;
using Microsoft.Azure.Devices.Shared;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Repository
{
    public class DeviceRegistryRepository : IDeviceRegistryCrudRepository, IDeviceRegistryListRepository
    {
        protected readonly IDocumentDBClient<DeviceModel> _documentClient;

        public DeviceRegistryRepository(IDocumentDBClient<DeviceModel> documentClient)
        {
            _documentClient = documentClient;
        }

       
        /// <summary>
        /// Queries the DocumentDB and retrieves the device based on its deviceId
        /// </summary>
        /// <param name="deviceId">DeviceID of the device to retrieve</param>
        /// <returns>Device instance if present, null if a device was not found with the provided deviceId</returns>
        public virtual async Task<DeviceModel> GetDeviceAsync(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                throw new ArgumentException(deviceId);
            }

            var query = await _documentClient.QueryAsync();
            var devices = query.Where(x => x.DeviceProperties.DeviceID == deviceId).ToList();
            var result = devices.FirstOrDefault();
            if (result!=null&&IdentityHelper.IsMultiTenantEnabled() && !IdentityHelper.IsSuperAdmin() && IdentityHelper.GetCurrentUserName() != result?.Twin.Tags.Get("__UserName__")?.ToString())
            {
                throw new Exception("deviceId is invalid");
            }
            return result;
        }

        /// <summary>
        /// Adds a device to the DocumentDB.
        /// Throws a DeviceAlreadyRegisteredException if a device already exists in the database with the provided deviceId
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        public virtual async Task<DeviceModel> AddDeviceAsync(DeviceModel device)
        {
            if (device == null)
            {
                throw new ArgumentNullException("device");
            }

            if (string.IsNullOrEmpty(device.id))
            {
                device.id = Guid.NewGuid().ToString();
            }

            DeviceModel existingDevice = await GetDeviceAsync(device.DeviceProperties.DeviceID);
            if (existingDevice != null)
            {
                throw new DeviceAlreadyRegisteredException(device.DeviceProperties.DeviceID);
            }
            if (IdentityHelper.IsMultiTenantEnabled())
            {
                device.Twin.Tags.Set("__UserName__", IdentityHelper.GetCurrentUserName());
            }
            var savedDevice = await _documentClient.SaveAsync(device);
            return savedDevice;
        }

        public async Task RemoveDeviceAsync(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                throw new ArgumentNullException("deviceId");
            }

            DeviceModel existingDevice = await GetDeviceAsync(deviceId);
            if (existingDevice == null)
            {
                throw new DeviceNotRegisteredException(deviceId);
            }

            await _documentClient.DeleteAsync(existingDevice.id);
        }

        /// <summary>
        /// Updates an existing device in the DocumentDB
        /// Throws a DeviceNotRegisteredException is the device does not already exist in the DocumentDB
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        public virtual async Task<DeviceModel> UpdateDeviceAsync(DeviceModel device)
        {
            if (device == null)
            {
                throw new ArgumentNullException("device");
            }

            if (device.DeviceProperties == null)
            {
                throw new DeviceRequiredPropertyNotFoundException("'DeviceProperties' property is missing");
            }

            if (string.IsNullOrEmpty(device.DeviceProperties.DeviceID))
            {
                throw new DeviceRequiredPropertyNotFoundException("'DeviceID' property is missing");
            }

            DeviceModel existingDevice = await GetDeviceAsync(device.DeviceProperties.DeviceID);
            if (existingDevice == null)
            {
                throw new DeviceNotRegisteredException(device.DeviceProperties.DeviceID);
            }
            if (IdentityHelper.IsMultiTenantEnabled() && !IdentityHelper.IsSuperAdmin() && existingDevice.Twin.Tags.Get("__UserName__") != device.Twin.Tags.Get("__UserName__"))
            {
                throw new NotImplementedException("not allowed to update the  __UserName__");
            }
            string incomingRid = device._rid ?? "";

            if (string.IsNullOrWhiteSpace(incomingRid))
            {
                // copy the existing _rid onto the incoming data if needed
                var existingRid = existingDevice._rid ?? "";
                if (string.IsNullOrWhiteSpace(existingRid))
                {
                    throw new InvalidOperationException("Could not find _rid property on existing device");
                }
                device._rid = existingRid;
            }

            string incomingId = device.id ?? "";

            if (string.IsNullOrWhiteSpace(incomingId))
            {
                // copy the existing id onto the incoming data if needed
                if (existingDevice.DeviceProperties == null)
                {
                    throw new DeviceRequiredPropertyNotFoundException("'DeviceProperties' property is missing");
                }

                var existingId = existingDevice.id ?? "";
                if (string.IsNullOrWhiteSpace(existingId))
                {
                    throw new InvalidOperationException("Could not find id property on existing device");
                }
                device.id = existingId;
            }
            if (IdentityHelper.IsMultiTenantEnabled()&&!IdentityHelper.IsSuperAdmin())
            {
                device.Twin.Tags.Set("__UserName__", existingDevice.Twin.Tags.Get("__UserName__").ToString() as string);
            }
            device.DeviceProperties.UpdatedTime = DateTime.UtcNow;
            var savedDevice = await this._documentClient.SaveAsync(device);
            return savedDevice;
        }

        public virtual async Task<DeviceModel> UpdateDeviceEnabledStatusAsync(string deviceId, bool isEnabled)
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                throw new ArgumentNullException("deviceId");
            }

            DeviceModel existingDevice = await this.GetDeviceAsync(deviceId);

            if (existingDevice == null)
            {
                throw new DeviceNotRegisteredException(deviceId);
            }


            if (existingDevice.DeviceProperties == null)
            {
                throw new DeviceRequiredPropertyNotFoundException("Required DeviceProperties not found");
            }

            existingDevice.DeviceProperties.HubEnabledState = isEnabled;
            existingDevice.DeviceProperties.UpdatedTime = DateTime.UtcNow;
            var savedDevice =await this._documentClient.SaveAsync(existingDevice);
            return savedDevice;
        }

        public virtual async Task<DeviceListFilterResult> GetDeviceList(DeviceListFilter filter)
        {
            List<DeviceModel> deviceList = await this.GetAllDevicesAsync();
            if (IdentityHelper.IsMultiTenantEnabled() && !IdentityHelper.IsSuperAdmin())
            {
                if (filter?.Clauses == null)
                {
                    filter.Clauses = new List<Clause>();
                }
                filter.Clauses.Add(new Clause { ColumnName = "tags.__UserName__", ClauseType = ClauseType.EQ, ClauseDataType = TwinDataType.String, ClauseValue = IdentityHelper.GetCurrentUserName() });
            }
            IQueryable<DeviceModel> filteredDevices = FilterHelper.FilterDeviceList(deviceList.AsQueryable<DeviceModel>(), filter.Clauses);

            IQueryable<DeviceModel> filteredAndSearchedDevices = this.SearchDeviceList(filteredDevices, filter.SearchQuery);

            IQueryable<DeviceModel> sortedDevices = this.SortDeviceList(filteredAndSearchedDevices, filter.SortColumn, filter.SortOrder);

            List<DeviceModel> pagedDeviceList = sortedDevices.Skip(filter.Skip).Take(filter.Take).ToList();

            int filteredCount = filteredAndSearchedDevices.Count();

            return new DeviceListFilterResult()
            {
                Results = pagedDeviceList,
                TotalDeviceCount = deviceList.Count,
                TotalFilteredCount = filteredCount
            };
        }

        /// <summary>
        /// Queries the DocumentDB and retrieves all documents in the collection
        /// </summary>
        /// <returns>All documents in the collection</returns>
        private async Task<List<DeviceModel>> GetAllDevicesAsync()
        {
            var devices = await _documentClient.QueryAsync();
            var result = devices?.ToList();
            if (result?.Count>0&&IdentityHelper.IsMultiTenantEnabled() && !IdentityHelper.IsSuperAdmin())
            {
                return result.Where(m => m.Twin.Tags["__UserName__"]?.ToString() == IdentityHelper.GetCurrentUserName())?.ToList();
            }
            return result;
        }

        private IQueryable<DeviceModel> SearchDeviceList(IQueryable<DeviceModel> deviceList, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return deviceList;
            }

            Func<DeviceModel, bool> filter = (d) => this.SearchTypePropertiesForValue(d, search);

            // look for all devices that contain the search value in one of the DeviceProperties Properties
            return deviceList.Where(filter).AsQueryable();
        }

        private bool SearchTypePropertiesForValue(DeviceModel device, string search)
        {
            // if the device or its system properties are null then
            // there's nothing that can be searched on
            if (device?.DeviceProperties == null)
            {
                return false;
            }

            // iterate through the DeviceProperties Properties and look for the search value
            // case insensitive search
            var upperCaseSearch = search.ToUpperInvariant();
            return device.DeviceProperties.ToKeyValuePairs().Any(t =>
                    (t.Value != null) &&
                    t.Value.ToString().ToUpperInvariant().Contains(upperCaseSearch));
        }

        private IQueryable<DeviceModel> SortDeviceList(IQueryable<DeviceModel> deviceList, string sortColumn, QuerySortOrder sortOrder)
        {
            // if a sort column was not provided then return the full device list in its original sort
            if (string.IsNullOrWhiteSpace(sortColumn))
            {
                return deviceList;
            }

            Func<DeviceProperties, dynamic> getPropVal = ReflectionHelper.ProducePropertyValueExtractor(sortColumn, false, false);
            Func<DeviceModel, dynamic> keySelector = (item) =>
            {
                if (item?.DeviceProperties == null)
                {
                    return null;
                }

                if (string.Equals("hubEnabledState", sortColumn, StringComparison.CurrentCultureIgnoreCase))
                {
                    return item.DeviceProperties.GetHubEnabledState();
                }

                return getPropVal(item.DeviceProperties);
            };

            if (sortOrder == QuerySortOrder.Ascending)
            {
                return deviceList.OrderBy(keySelector).AsQueryable();
            }
            else
            {
                return deviceList.OrderByDescending(keySelector).AsQueryable();
            }
        }

        public virtual async Task<IEnumerable<string>> GetDeviceIdsByUserName(string userName = null)
        {
            var devices = await _documentClient.QueryAsync();
            var result = devices?.ToList();
            if (result?.Count > 0 && IdentityHelper.IsMultiTenantEnabled() && !IdentityHelper.IsSuperAdmin())
            {
                if (string.IsNullOrWhiteSpace(userName))
                {
                    userName = IdentityHelper.GetCurrentUserName();
                }
                return result.Where(m => m.Twin.Tags.Get("__UserName__")?.ToString() == userName)?.Select(m => m.DeviceProperties.DeviceID);
            }
            return null;
        }

        public virtual  async Task UpdateTwinAsync(string deviceId, Twin twin)
        {
            var device = await GetDeviceAsync(deviceId);
            if (device == null)
            {
                throw new NotImplementedException("deviceId is invalid");
            }
            if (IdentityHelper.IsMultiTenantEnabled()&&!IdentityHelper.IsSuperAdmin()&& twin.Tags.Get("__UserName__")!=device.Twin.Tags.Get("__UserName__"))
            {
                throw new NotImplementedException("not allowed to update the  __UserName__");
            }
            device.Twin = twin;
           await UpdateDeviceAsync(device);
        }

        public virtual async Task<Twin> GetTwinAsync(string deviceId)
        {
            return (await GetDeviceAsync(deviceId))?.Twin;
        }
    }
}