using Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Configurations;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Helpers;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Web.Helpers;
using Moq;
using Ploeh.AutoFixture;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.UnitTests.Web.Helpers
{
    public class IdentityHelperTests
    {
        private readonly Fixture fixture = new Fixture();
        [Fact]
        public void TestGetUser()
        {
            var a = IdentityHelper.GetCurrentUserName();
            Assert.Equal(a, "");
        }

        [Fact]
        public void TestCheckConfig()
        {
            //if no config item specifying superadmin, no filter will be applied.
            Mock<IConfigurationProvider> provider = new Mock<IConfigurationProvider>();
            provider.Setup(x => x.GetConfigurationSettingValue("SuperAdminList")).Returns("testadmin");
            var original = IdentityHelper.IsMultiTenantEnabled(provider.Object);
            Assert.Equal(original, true);

            //if the config item is there, but the user is not in it or is null/empty, filter will be applied.
            provider.Setup(x => x.GetConfigurationSettingValue("SuperAdminList")).Returns("");
            var mock = IdentityHelper.IsMultiTenantEnabled(provider.Object);
            Assert.Equal(mock, false);
        }

        [Fact]
        public void TestConfigValueMatchAlias()
        {
            var result1 = IdentityHelper.IsSuperAdmin("superadmin1,superadmin2", "superadmin1");
            Assert.Equal(result1, true);

            var result2 = IdentityHelper.IsSuperAdmin("superadmin1,superadmin2", "");
            Assert.Equal(result2, false);

            var result3 = IdentityHelper.IsSuperAdmin("superadmin1,superadmin2", null);
            Assert.Equal(result3, false);

            var result4 = IdentityHelper.IsSuperAdmin("superadmin1,superadmin2", "superadmin3");
            Assert.Equal(result4, false);

            var result5 = IdentityHelper.IsSuperAdmin("superadmin1;superadmin2", "superadmin1");
            Assert.Equal(result5, true);

            var result6 = IdentityHelper.IsSuperAdmin("superadmin1 superadmin2", "superadmin1");
            Assert.Equal(result6, true);

            var result7 = IdentityHelper.IsSuperAdmin("superadmin1, superadmin2", "superadmin1");
            Assert.Equal(result7, true);
        }

    }
}
