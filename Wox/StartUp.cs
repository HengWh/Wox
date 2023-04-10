using System;
using System.Globalization;
using System.Threading;
using Wox.Helper;

namespace Wox
{
    public class StartUp
    {
        private const string Unique = "Wox_Unique_Application_Mutex";

        [STAThread]
        public static void Main()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;


            if (SingleInstance<App>.InitializeAsFirstInstance(Unique))
            {
                using (var application = new App())
                {
                    application.InitializeComponent();
                    application.Run();
                }
            }
        }
    }
}
