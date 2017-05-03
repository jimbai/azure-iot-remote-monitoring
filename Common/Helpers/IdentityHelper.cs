
using System;
using System.Linq;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Configurations;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Helpers
{
    public static class IdentityHelper
    {
        public static string GetCurrentUserName()
        {
            return System.Web.HttpContext.Current?.User.Identity.Name ?? string.Empty;
        }

        public static string GetUserShortName(string longname = null)
        {
            longname = longname == null ? GetCurrentUserName() : longname;
            if (longname.Contains('@'))
            {
                return longname.Split('@')[0];
            }
            return longname;
        }

        public static bool IsOtherUserInvisible()
        {
            return IsMultiTenantEnabled() && !IsSuperAdmin();
        }

        public static bool IsMultiTenantEnabled(IConfigurationProvider configurationProvider = null)
        {
            if (configurationProvider == null)
            {
                configurationProvider = new ConfigurationProvider();
            }
            var configItem = configurationProvider.GetConfigurationSettingValue("SuperAdminList");
            var user = GetCurrentUserName();
            return configItem.Any();
        }

        public static bool IsSuperAdmin()
        {
            var configurationProvider = new ConfigurationProvider();
            return IsSuperAdmin(configurationProvider.GetConfigurationSettingValue("SuperAdminList"), GetCurrentUserName());
        }

        internal static bool IsSuperAdmin(string configValue, string currentAlias)
        {
            char[] separator = { ',', ';', ' ' };
            var allAdmin = configValue.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            if (string.IsNullOrEmpty(currentAlias)) return false;

            return allAdmin.Contains(currentAlias);
        }
    }
}