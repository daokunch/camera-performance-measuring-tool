//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the Microsoft Public License.
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace SDKTemplate
{
    public sealed partial class Scenario2_FindAvailableSourceGroups : Page
    {
        private MediaCapture _mediaCapture;
        private MediaFrameSource _source;
        private MediaFrameReader _reader;
        private FrameRenderer _frameRenderer;
        private bool _streaming = false;

        private SourceGroupCollection _groupCollection;
        private readonly SimpleLogger _logger;

        private static int s_cnt = 0;
        private static double s_ullDeltaSum = 0;
        private static double s_ullDeltaSumInternal = 0;
        private static double s_ullTimeStart = 0;

        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceCounter(
            out long lpPerformanceCount);

        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(
            out long lpFrequency);

        public Scenario2_FindAvailableSourceGroups()
        {
            InitializeComponent();
            _logger = new SimpleLogger(outputTextBlock);
            _frameRenderer = new FrameRenderer(PreviewImage);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            /// SourceGroupCollection will setup device watcher to monitor
            /// SourceGroup devices enabled or disabled from the system.
            _groupCollection = new SourceGroupCollection(this.Dispatcher);
            GroupComboBox.ItemsSource = _groupCollection.FrameSourceGroups;
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            _groupCollection?.Dispose();
            await StopReaderAsync();
            DisposeMediaCapture();
        }

        private async Task UpdateButtonStateAsync()
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                StartButton.IsEnabled = _source != null && !_streaming;
                StopButton.IsEnabled = _source != null && _streaming;
            });
        }

        /// <summary>
        /// Disposes of the MediaCapture object and clears the items from the Format and Source ComboBoxes.
        /// </summary>
        private void DisposeMediaCapture()
        {
            FormatComboBox.ItemsSource = null;
            SourceComboBox.ItemsSource = null;

            _source = null;

            _mediaCapture?.Dispose();
            _mediaCapture = null;
        }

        /// <summary>
        /// Stops reading from the previous selection and starts reading frames from the newly selected source group.
        /// </summary>
        private async void GroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await StopReaderAsync();
            DisposeMediaCapture();

            var group = GroupComboBox.SelectedItem as FrameSourceGroupModel;
            if (group != null)
            {
                await InitializeCaptureAsync();

                SourceComboBox.ItemsSource = group.SourceInfos;
                SourceComboBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Starts reading from the newly selected source.
        /// </summary>
        private async void SourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await StopReaderAsync();

            if (SourceComboBox.SelectedItem != null)
            {
                await StartReaderAsync();

                // Get the formats supported by the selected source into the FormatComboBox.
                IEnumerable<FrameFormatModel> formats = null;
                if (_mediaCapture != null && _source != null)
                {
                    // Limit ourselves to formats that we can render.
                    formats = _source.SupportedFormats
                        .Where(format => FrameRenderer.GetSubtypeForFrameReader(_source.Info.SourceKind, format) != null)
                        .Select(format => new FrameFormatModel(format));
                }

                FormatComboBox.ItemsSource = formats;
            }
        }

        /// <summary>
        /// Sets the video format for the current frame source.
        /// </summary>
        private async void FormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var format = FormatComboBox.SelectedItem as FrameFormatModel;
            await ChangeMediaFormatAsync(format);
        }

        /// <summary>
        /// Starts reading frames from the current reader.
        /// </summary>
        private async Task StartReaderAsync()
        {
            await CreateReaderAsync();

            if (_reader != null && !_streaming)
            {
                MediaFrameReaderStartStatus result = await _reader.StartAsync();
                _logger.Log($"Start reader with result: {result}");

                if (result == MediaFrameReaderStartStatus.Success)
                {
                    _streaming = true;
                    await UpdateButtonStateAsync();
                }
            }
        }

        /// <summary>
        /// Creates a frame reader from the current frame source and registers to handle its frame events.
        /// </summary>
        private async Task CreateReaderAsync()
        {
            await InitializeCaptureAsync();

            UpdateFrameSource();

            if (_source != null)
            {
                // Ask the MediaFrameReader to use a subtype that we can render.
                string requestedSubtype = FrameRenderer.GetSubtypeForFrameReader(_source.Info.SourceKind, _source.CurrentFormat);
                if (requestedSubtype != null)
                {
                    _reader = await _mediaCapture.CreateFrameReaderAsync(_source, requestedSubtype);

                    _reader.FrameArrived += Reader_FrameArrived;

                    _logger.Log($"Reader created on source: {_source.Info.Id}");
                }
                else
                {
                    _logger.Log($"Cannot render current format on source: {_source.Info.Id}");
                }
            }
        }

        /// <summary>
        /// Updates the current frame source to the one corresponding to the user's selection.
        /// </summary>
        private void UpdateFrameSource()
        {
            var info = SourceComboBox.SelectedItem as FrameSourceInfoModel;
            if (_mediaCapture != null && info != null && info.SourceGroup != null)
            {
                var groupModel = GroupComboBox.SelectedItem as FrameSourceGroupModel;
                if (groupModel == null || groupModel.Id != info.SourceGroup.Id)
                {
                    SourceComboBox.SelectedItem = null;
                    return;
                }

                if (_source == null || _source.Info.Id != info.SourceInfo.Id)
                {
                    _mediaCapture.FrameSources.TryGetValue(info.SourceInfo.Id, out _source);
                }
            }
            else
            {
                _source = null;
            }
        }

        /// <summary>
        /// Initializes the MediaCapture object with the current source group.
        /// </summary>
        private async Task InitializeCaptureAsync()
        {
            var groupModel = GroupComboBox.SelectedItem as FrameSourceGroupModel;
            if (_mediaCapture == null && groupModel != null)
            {
                // Create a new media capture object.
                _mediaCapture = new MediaCapture();

                var settings = new MediaCaptureInitializationSettings()
                {
                    // Select the source we will be reading from.
                    SourceGroup = groupModel.SourceGroup,

                    // This media capture has exclusive control of the source.
                    SharingMode = MediaCaptureSharingMode.SharedReadOnly,

                    // Set to CPU to ensure frames always contain CPU SoftwareBitmap images,
                    // instead of preferring GPU D3DSurface images.
                    MemoryPreference = MediaCaptureMemoryPreference.Cpu,

                    // Capture only video. Audio device will not be initialized.
                    StreamingCaptureMode = StreamingCaptureMode.Video,
            };

                try
                {
                    // Initialize MediaCapture with the specified group.
                    // This can raise an exception if the source no longer exists,
                    // or if the source could not be initialized.
                    await _mediaCapture.InitializeAsync(settings);
                    _logger.Log($"Successfully initialized MediaCapture for {groupModel.DisplayName}");
                }
                catch (Exception exception)
                {
                    _logger.Log(exception.Message);
                    DisposeMediaCapture();
                }
            }
        }

        /// <summary>
        /// Sets the frame format of the current frame source.
        /// </summary>
        private async Task ChangeMediaFormatAsync(FrameFormatModel format)
        {
            if (_source == null)
            {
                _logger.Log("Unable to set format when source is not set.");
                return;
            }

            // Set the source format if the selected one is different from the current one.
            if (format != null && !format.HasSameFormat(_source.CurrentFormat))
            {
                await _source.SetFormatAsync(format.Format);
                _logger.Log($"Format set to {format.DisplayName}");
            }
        }

        /// <summary>
        /// Stops reading from the frame reader, disposes of the reader and updates the button state.
        /// </summary>
        private async Task StopReaderAsync()
        {
            _streaming = false;

            if (_reader != null)
            {
                await _reader.StopAsync();
                _reader.FrameArrived -= Reader_FrameArrived;
                _reader.Dispose();
                _reader = null;

                _logger.Log("Reader stopped.");
            }

            await UpdateButtonStateAsync();
        }

        /// <summary>
        /// Bound to the click of the Start button.
        /// </summary>
        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            await StartReaderAsync();
        }

        /// <summary>
        /// Bound to the click of the Stop button.
        /// </summary>
        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            await StopReaderAsync();
        }


        [StructLayout(LayoutKind.Explicit, Size = 8)]
        struct LARGE_INTEGER
        {
            [FieldOffset(0)] public Int64 QuadPart;
            [FieldOffset(0)] public UInt32 LowPart;
            [FieldOffset(4)] public Int32 HighPart;
        }

        /// <summary>
        /// Handles the frame arrived event by converting the frame to a displayable
        /// format and rendering it to the screen.
        /// </summary>
        private void Reader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            // TryAcquireLatestFrame will return the latest frame that has not yet been acquired.
            // This can return null if there is no such frame, or if the reader is not in the
            // "Started" state. The latter can occur if a FrameArrived event was in flight
            // when the reader was stopped.
            using (var frame = sender.TryAcquireLatestFrame())
            {
                _frameRenderer.ProcessFrame(frame);

                double _ullLogTimeOut, _ullLogTimeIn;

                var input_time = new Guid(0x1f1342da, 0x4737, 0x4feb, 0x88, 0xd4, 0x3b, 0x77, 0x5d, 0x57, 0x5a, 0xb7);
                bool b = frame.Properties.ContainsKey(input_time);
                if (frame.Properties.ContainsKey(input_time))
                {
                    var output_time = new Guid(0x1f1342da, 0x4737, 0x4feb, 0x88, 0xd4, 0x3b, 0x77, 0x5d, 0x57, 0x5a, 0xb6);

                    var v = new Object();
                    frame.Properties.TryGetValue(input_time, out v);
                    _ullLogTimeIn = Convert.ToDouble(v);
                    //_logger.Log("The Input Time is : " + _ullLogTimeIn + " \n");

                    if (frame.Properties.ContainsKey(output_time))
                    {
                        var x = new Object();
                        frame.Properties.TryGetValue(input_time, out x);
                        _ullLogTimeOut = Convert.ToDouble(x);
                        //_logger.Log("The Output Time is : " + _ullLogTimeOut + " \n");

                        double ullLogTime;
                        long freq = 0;
                        long currtime = 0;
                        if (QueryPerformanceCounter(out currtime))
                        {
                            if (QueryPerformanceFrequency(out freq))
                            {
                                ullLogTime = currtime * 1000.0 / (double)freq;
                                // DebugPrint(L"_ullLogTime = %lld, ullLogTime = %lld, delta = %lld", _ullLogTime, ullLogTime, (ullLogTime - _ullLogTime));

                                double delta = ullLogTime - _ullLogTimeOut;
                                double deltaInternal = _ullLogTimeOut - _ullLogTimeIn;
                                s_ullDeltaSum += delta;
                                s_ullDeltaSumInternal += deltaInternal;
                                if (++s_cnt == 30)
                                {
                                    double fps = (double)30.0 / (double)(ullLogTime - s_ullTimeStart) * 1000.0;
                                    s_ullTimeStart = ullLogTime;
                                    _logger.Log("delta = " + s_ullDeltaSum / s_cnt + ", fps = " + fps + ", deltaInternal = " + s_ullDeltaSumInternal / s_cnt + "\n");
                                    s_ullDeltaSum = 0;
                                    s_ullDeltaSumInternal = 0;
                                    s_cnt = 0;
                                }
                            }
                        }
                    }
                }

                double fps_in, fps_out;
                var output_fps = new Guid(0x1f1342da, 0x4737, 0x4feb, 0x88, 0xd4, 0x3b, 0x77, 0x5d, 0x57, 0x5a, 0xb4);

                if (frame.Properties.ContainsKey(output_fps))
                {
                    var v = new Object();
                    frame.Properties.TryGetValue(output_fps, out v);
                    fps_out = Convert.ToDouble(v);
                    _logger.Log("The Output FPS is : " + fps_out + " \n");
                }

                var input_fps = new Guid(0x1f1342da, 0x4737, 0x4feb, 0x88, 0xd4, 0x3b, 0x77, 0x5d, 0x57, 0x5a, 0xb5);

                if (frame.Properties.ContainsKey(input_fps))
                {
                    var v = new Object();
                    frame.Properties.TryGetValue(input_fps, out v);
                    fps_in = Convert.ToDouble(v);
                    _logger.Log("The Input FPS is : " + fps_in + " \n");
                }
            }
        }
    }
}
