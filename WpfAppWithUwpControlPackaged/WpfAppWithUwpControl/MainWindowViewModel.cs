using CommunityToolkit.Mvvm.ComponentModel;
using PlatformManagement;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace WpfAppWithUwpControl
{
    public class MainWindowViewModel : ObservableObject, IDisposable
    {
        //collect the monitor brightness and CPU Busy from WMI
        private WmiServices wmi = null;
        private DataManager dm = null;
        private string currentCpu = null;
        private string currentGpu = null;
        private Timer timer;

        public MainWindowViewModel()
        {
            wmi = new WmiServices();
            timer = new Timer(OnTimerCallback, null, 1000, 1000);
            dm = new DataManager();
        }

        private void OnTimerCallback(object state)
        {
            CurrentCpu = $"{wmi.GetPercentProcessorTime():0.00}";
            string[] data = { currentCpu, "NA", "NA" };
            dm.WriteToCSV(dm.BuildRow(data));
        }

        public void Dispose()
        {
            if (timer != null)
            {
                timer.Dispose();
                timer = null;
            }
        }

        public string MyTitle => "My App";

        public string CurrentCpu { get => currentCpu; set => SetProperty(ref currentCpu, value); }
        public string CurrentGpu { get => currentGpu; set => SetProperty(ref currentGpu, value); }
    }
}
