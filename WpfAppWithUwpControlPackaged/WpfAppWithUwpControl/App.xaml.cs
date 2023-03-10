using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace WpfAppWithUwpControl
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private MainWindowViewModel vm;

        protected override void OnStartup(StartupEventArgs e)
        {
            vm = new MainWindowViewModel();
            var view = new MainWindow();
            view.Closing += View_Closing;
            view.DataContext = vm;
            view.Show();
        }

        private void View_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (vm != null)
            {
                vm.Dispose();
            }
        }
    }
}
