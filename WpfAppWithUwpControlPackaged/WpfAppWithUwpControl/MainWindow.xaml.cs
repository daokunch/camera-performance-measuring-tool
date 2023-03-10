using Microsoft.Toolkit.Wpf.UI.XamlHost;
using MyUwpApp;
using PlatformManagement;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Windows.Devices.Enumeration;
using Windows.Graphics.Display;
using Windows.Media.Capture;
using Windows.Media.Editing;
using Windows.System.Display;
using Windows.UI.Xaml.Controls;

namespace WpfAppWithUwpControl
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        MediaCapture mediaCapture;
        ScrollViewer scrollViewer;
        bool isPreviewing;
        DisplayRequest displayRequest = new DisplayRequest();
        SDKTemplate.MyUserControl1 myUserControl;
        private string myText;

        public event PropertyChangedEventHandler PropertyChanged;



        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.MyText = "This overlay should work because it is a UWP UIElement";
        }

        public string MyText { get => myText; set { myText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MyText))); } }

        private async void myCaptureElement_ChildChanged(object sender, EventArgs e)
        {
            WindowsXamlHost windowsXamlHost = (WindowsXamlHost)sender;
            myUserControl = (SDKTemplate.MyUserControl1)windowsXamlHost.Child;

            if (myUserControl != null)
            {
                myUserControl.DataContext = this;
                await StartPreviewAsync();
            }
        }

        private async Task StartPreviewAsync()
        {
            try
            {
                // Get available devices for capturing pictures
                var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

                // Get the desired camera by panel
                DeviceInformation desiredDevice = allVideoDevices.Last();

                mediaCapture = new MediaCapture();
                scrollViewer = new ScrollViewer();
                await mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings() { SharingMode = MediaCaptureSharingMode.SharedReadOnly, StreamingCaptureMode = StreamingCaptureMode.Video, VideoDeviceId = desiredDevice.Id });
                displayRequest.RequestActive();
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape;
            }
            catch (UnauthorizedAccessException)
            {
                // This will be thrown if the user denied access to the camera in privacy settings
                ShowMessageToUser("The app was denied access to the camera");
                return;
            }

            try
            {
                //myUserControl.scrollViewer = scrollViewer;
                //myUserControl.CaptureElement.Source = mediaCapture;
                await mediaCapture.StartPreviewAsync();
                isPreviewing = true;
            }
            catch (System.IO.FileLoadException)
            {
                mediaCapture.CaptureDeviceExclusiveControlStatusChanged += _mediaCapture_CaptureDeviceExclusiveControlStatusChanged;
            }
            catch (Exception)
            {

            }
        }

        private async void _mediaCapture_CaptureDeviceExclusiveControlStatusChanged(MediaCapture sender, MediaCaptureDeviceExclusiveControlStatusChangedEventArgs args)
        {
            if (args.Status == MediaCaptureDeviceExclusiveControlStatus.SharedReadOnlyAvailable)
            {
                ShowMessageToUser("The camera preview can't be displayed because another app has exclusive access");
            }
            else if (args.Status == MediaCaptureDeviceExclusiveControlStatus.ExclusiveControlAvailable && !isPreviewing)
            {
                await Dispatcher.BeginInvoke((Action)(async () =>
                {
                    await StartPreviewAsync();
                }));
            }
        }

        private async Task CleanupCameraAsync()
        {
            try
            {
                if (mediaCapture != null)
                {
                    if (isPreviewing)
                    {
                        // await mediaCapture.StopPreviewAsync();
                    }

                    await Dispatcher.BeginInvoke((Action)(() =>
                    {
                        //myUserControl.CaptureElement.Source = null;
                        if (displayRequest != null)
                        {
                            displayRequest.RequestRelease();
                        }

                        mediaCapture.Dispose();
                        mediaCapture = null;
                    }));
                }
            }
            catch (Exception ex)
            {
                var tmp = $"{ex.GetType()}: {ex.Message}\n{ex.StackTrace}";
            }
        }

        private void ShowMessageToUser(string message)
        {
            MessageBox.Show(message);
        }
    }
}
