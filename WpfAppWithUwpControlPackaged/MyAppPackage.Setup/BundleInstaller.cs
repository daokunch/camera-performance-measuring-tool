using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace MyAppPackage.Setup
{
    class BunderInstaller
    {
        private const string _Cert = "test.cer";
        private const string _Bundle = "test.msixbundle";

        public void Install()
        {
            /// DEVELOPERS: To properly debug this code, you must run Visual Studio in admin mode.
            /// Put a breakpoint here, then right-click this project in the Solution Explorer, and
            /// choose the context menu "Debug"->"Start New Instance".
            InstallCert();
            LaunchBundle();
        }

        private void InstallCert()
        {
            var Cert = new X509Certificate2(Properties.Resources.cer);

            var Store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
            Store.Open(OpenFlags.ReadWrite);
            Store.Add(Cert);
        }

        private void LaunchBundle()
        {
            var BundleTemp = Path.Combine(Path.GetTempPath(), _Bundle);
            File.WriteAllBytes(BundleTemp, Properties.Resources.bundle);

            Process.Start(BundleTemp);
        }
    }
}
