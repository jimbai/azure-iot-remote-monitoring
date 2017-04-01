using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Constants
{
    public static class WebConstants
    {
        public const string JSVersion = "1.6.0.0";
        public const string CultureCookieName = "_culture";
        public const string DeviceIconTagName = "__icon__";
        public const string DeviceUserTagName = "__UserName__";
        public const string DeviceIconFullTagName = "tags." + DeviceIconTagName;
    }
}
