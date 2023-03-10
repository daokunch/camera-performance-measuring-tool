using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Management;
using System.Threading;

using System.Reflection;        //for PropertyType
using System.Runtime.InteropServices;

namespace PlatformManagement
{

    //Screen brightness event arguments:
    public class BrightnessChangedEventArgs : System.EventArgs
    {
        public readonly int level;

        public BrightnessChangedEventArgs(int newLevel)
        {
            level = newLevel;
        }
    }

    public class WmiServices 
    {
        bool m_wmiRunning;
        ObjectQuery query;
        ManagementObjectCollection queryCollection;

        //monitors monitor brightness changes, will update DeviceAssistenceMediaPlayerDemo UI
        ManagementEventWatcher monitorBrightnessWatcher;
        EventArrivedEventHandler monitorBrightnessHandler = null;  

        //for Percent Processor Time
        ulong m_N1;     //percent processor time
        ulong m_D1;     //timestamp
        double m_percentProcessorTime;
        public double GetPercentProcessorTime() { return m_percentProcessorTime; }

        private EventHandler<BrightnessChangedEventArgs> _brightnessChanged;
        public event EventHandler<BrightnessChangedEventArgs> BrightnessChanged
        {
            add 
            { 
                if (_brightnessChanged == null)
                {
                    //this part sets up the WMI events to monitor brightness changes
                    ManagementScope scope = new ManagementScope("\\\\.\\ROOT\\WMI");
                    EventQuery wmiEventQuery = new EventQuery("Select * From WmiMonitorBrightnessEvent");
                    monitorBrightnessWatcher = new ManagementEventWatcher(scope, wmiEventQuery);
                    monitorBrightnessWatcher.EventArrived += new EventArrivedEventHandler(MonitorBrightnessEventHandler);
                    monitorBrightnessWatcher.Start();
                    wmi_eventing_active = true;
                }
                _brightnessChanged += value;
            }
            remove { _brightnessChanged -= value; }  //more TODO here, i.e. caller removing eventhandler, watch for race condition here and closing app
        }

        private void MonitorBrightnessEventHandler(Object sender, EventArrivedEventArgs e)
        {
            ManagementBaseObject mbo = e.NewEvent;
            wmiCurrentBrightness = mbo["Brightness"].ToString();
            int level = Int32.Parse(wmiCurrentBrightness);
            OnBrightnessChanged(new BrightnessChangedEventArgs(level));
        }

        protected virtual void OnBrightnessChanged(BrightnessChangedEventArgs e)
        {
            if (_brightnessChanged != null)
            {
                _brightnessChanged(this, e);
            }
        }
    
        bool wmi_eventing_active = false;   //flag for requiring clean stop

        //for WmiMonitorBrightness
        string wmiActive = string.Empty;
        string wmiCurrentBrightness = string.Empty;
        byte[] wmiLevel;                    //array of allowable levels
        string wmiLevels = string.Empty;    //how many allowable levels

        public bool doCleanShutdown()
        {
            return wmi_eventing_active;
        }

        public WmiServices()
        {
            monitorBrightnessHandler += new EventArrivedEventHandler(MonitorBrightnessEventHandler);
            //create the one second CPU polling thread
            new Thread(WmiPolling).Start();
        }

        public void StopWmiServices()
        {
            m_wmiRunning = false;   //stop polling thread

            Console.WriteLine("StopWmiServices wmi_eventing_active:{0}", wmi_eventing_active);
            if (wmi_eventing_active)
            {
                wmi_eventing_active = false;
                monitorBrightnessWatcher.Stop();
                monitorBrightnessWatcher.EventArrived -= new EventArrivedEventHandler(monitorBrightnessHandler);
                monitorBrightnessWatcher.Dispose();
                monitorBrightnessWatcher = null;
            }
        }

        public void GetBrightnessInfo(out int level) //, out int min, out int max)
        {
            level = 0;
            //min = 0;
            //max = 0;
            try
            {
                ManagementScope scope = new ManagementScope("\\\\.\\ROOT\\WMI");
                query = new ObjectQuery("SELECT * FROM WmiMonitorBrightness");
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query);
                queryCollection = searcher.Get();

                //tip: to generate a C# class given a WMI class use this Microsoft tool documentation:
                //http://msdn.microsoft.com/en-us/library/2wkebaxa.aspx
                //i.e. mgmtclassgen WmiMonitorBrightness /n root\wmi /l CS /p c:\temp\monitor.cs
                foreach (ManagementObject m in queryCollection)
                {
                    //Microsoft's documentation is lacking, the comments below on the right are from MSDN:
                    wmiActive = m["Active"].ToString();                         //bool: Indicates the active monitor.
                    wmiCurrentBrightness = m["CurrentBrightness"].ToString();   //uint8: Current brightness as a percentage of total brightness.
                    wmiLevel = ((byte[])(m["Level"]));                          //uint8[]: Brightness level as a percentage.
                    wmiLevels = m["Levels"].ToString();                         //uint32: Supported brightness levels.

                    level = Int32.Parse(wmiCurrentBrightness);
                    //min = wmiLevel[0];
                    //max = wmiLevel[wmiLevel.Length - 1];
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetBrightnessInfo excptn:{0} stack:{1}",
                    ex.Message, ex.StackTrace);
            }
        }


        public void InitPercentProcessorTime()
        {
            GetPercentProcesssorTimes(out m_N1, out m_D1);
        }

        public void UpdatePercentProcessorTime()
        {
            if (m_N1 == 0 || m_D1 == 0)
            {
                InitPercentProcessorTime();
                return;
            }

            ulong N2, D2;  //second or subsequent readings
            GetPercentProcesssorTimes(out N2, out D2);
            if (N2 == 0 || D2 == 0)
            {
                InitPercentProcessorTime();
                return;
            }

            Double diffPercentProcessorTime, diffTimeStamp_Sys100ns;

            diffPercentProcessorTime = (Double)N2 - (Double)m_N1;
            diffTimeStamp_Sys100ns = (Double)D2 - (Double)m_D1;

            m_D1 = D2;
            m_N1 = N2;

            if (diffTimeStamp_Sys100ns == 0)
            {
                return;
            }
            m_percentProcessorTime = (1.0 - (diffPercentProcessorTime / (diffTimeStamp_Sys100ns))) * 100.0;
        }

        public void GetPercentProcesssorTimes(out ulong perProcTime, out ulong timeStamp100ns )
        {
            perProcTime = 0;
            timeStamp100ns = 0;
            try
            {
                ManagementScope scope = new ManagementScope("\\\\.\\ROOT\\CIMV2");
                query = new ObjectQuery("SELECT * FROM Win32_PerfRawData_PerfOS_Processor");
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query);
                queryCollection = searcher.Get();

                foreach (ManagementObject m in queryCollection)
                {
                    if (m["Name"].Equals(@"_Total"))
                    {
                        if ((m["PercentProcessorTime"] != null))
                        {
                            perProcTime = (ulong)(m["PercentProcessorTime"]);
                        }

                        if ((m["TimeStamp_Sys100NS"] != null))
                        {
                            timeStamp100ns = (ulong)(m["TimeStamp_Sys100NS"]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetCPUInfo excptn:{0} stack:{1}",
                    ex.Message, ex.StackTrace);
            }
        }

        void WmiPolling()
        {
            InitPercentProcessorTime();

            m_wmiRunning = true;

            while (m_wmiRunning)
            {
                Thread.Sleep(1000);
                UpdatePercentProcessorTime();
            }
        }

    } //public WmiServices
}
