using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DotNetVersionFinder;

namespace pyRevitLabs.Common {
    public static class UserEnv {
        public static Version GetInstalledDotNetVersion() {
            return DotNetVersion.Find();
        }
    }
}
