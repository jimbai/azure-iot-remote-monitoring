using Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Configurations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Helpers
{
    public static class IdentityHelper
    {
        public static string GetCurrentUserName()
        {
            return System.Web.HttpContext.Current?.User.Identity.Name ?? string.Empty;
        }

        public static bool IsMultiTenantEnabled(IConfigurationProvider configurationProvider)
        {
            var configItem = configurationProvider.GetConfigurationSettingValue("SuperAdminList");
            var user = GetCurrentUserName(); 
            return configItem.Any() && !IsSuperAdmin(configItem,user);
        }

        internal static bool IsSuperAdmin(string configValue,string currentAlias)
        {
            char[] separator = { ',',';',' '};
            var allAdmin = configValue.Split(separator,StringSplitOptions.RemoveEmptyEntries);
            if (string.IsNullOrEmpty(currentAlias)) return false;

            return allAdmin.Contains(currentAlias);
        } 
    }
}