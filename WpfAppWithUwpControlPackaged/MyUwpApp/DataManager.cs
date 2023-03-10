using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace PlatformManagement
{
    internal class DataManager
    {
        string deliminator = ",";
        string filePath = null;

        public DataManager() 
        {
            filePath = "UWPOutput.csv";
            string[] headings = { "FPS", "Latency"};
            try
            {
                using(StreamWriter sw = new StreamWriter(filePath, false))
                {
                    sw.WriteLine(BuildRow(headings));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return;
            }
        }

        public string BuildRow(string[] data)
        {
            return string.Join(deliminator, data);
        }

        public void WriteToCSV(string row)
        {
            try
            {
                using(StreamWriter sw = new StreamWriter(filePath, true))
                {
                    sw.WriteLine(row);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return;
            }
        }
    }

    internal class Row
    {
        public double CPU { get; set; }
        public double GPU { get; set; }
        public double VPU { get; set; }
        public double FPS { get; set; }
        public double Latency { get; set; }

        public Row(double CPU, double GPU, double VPU, double FPS, double Latency)
        {
            this.CPU = CPU;
            this.GPU = GPU;
            this.VPU = VPU;
            this.FPS = FPS;
            this.Latency = Latency;
        }
    }
}
