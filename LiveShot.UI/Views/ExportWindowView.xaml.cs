using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using LiveShot.API;
using LiveShot.API.Events.Window;
using LiveShot.API.Upload;

namespace LiveShot.UI.Views
{
    public partial class ExportWindowView : Window
    {
        private readonly IEventPipeline _events;
        private readonly IUploadService _uploadService;
        private readonly HttpClient _httpClient;

        public ExportWindowView(IEventPipeline events, IUploadService uploadService, HttpClient httpClient)
        {
            InitializeComponent();

            _events = events;
            _uploadService = uploadService;
            _httpClient = httpClient;

            OpenBtn.Click += OpenBtnOnClick;
            CopyBtn.Click += CopyBtnOnClick;
        }

        private void CopyBtnOnClick(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(LinkBox.Text);
        }

        private void OpenBtnOnClick(object sender, RoutedEventArgs e)
        {
            OpenUrl(LinkBox.Text);
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch (Exception)
            {
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") {CreateNoWindow = true});
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            _events.Dispatch<OnClosed>(new OnClosedArgs
            {
                Root = e,
                Window = this
            });
        }

        public void Upload(Bitmap bitmap, bool google)
        {
            Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    if (google)
                    {
                        await UploadToGoogleLens(bitmap);
                        Close();
                        return;
                    }

                    LinkBox.Text = await _uploadService.Upload(bitmap);

                    UploadResultGrid.Visibility = Visibility.Visible;
                    ProgressBarGrid.Visibility = Visibility.Hidden;
                }
                catch (Exception e)
                {
                    MessageBox.Show(
                        e.Message,
                        API.Properties.Resources.Exception_Message,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    Close();
                }
            });
        }

        private async Task UploadToGoogleLens(Bitmap bitmap)
        {
            // TODO: [SUSPENDED] Feature disabled due to instability. Uncomment to restore.
            if (bitmap == null)
            {
                throw new Exception(API.Properties.Resources.Upload_Failed);
            }

            string base64Image;
            try
            {
                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    byte[] bytes = ms.ToArray();
                    if (bytes.Length == 0) throw new Exception();
                    base64Image = Convert.ToBase64String(bytes);
                }
            }
            catch
            {
                throw new Exception(API.Properties.Resources.Upload_Failed);
            }

            string tempPath = Path.GetTempPath();
            string fileName = $"liveshot_google_bridge_{Guid.NewGuid()}.html";
            string filePath = Path.Combine(tempPath, fileName);

            string htmlContent = $@"
<!DOCTYPE html>
<html>
<head><title>Redirecting...</title></head>
<body onload=""document.forms[0].submit()"">
    <form action=""https://www.google.com/searchbyimage/upload"" method=""POST"" enctype=""multipart/form-data"">
        <input type=""hidden"" name=""image_content"" value=""{base64Image}"" />
    </form>
</body>
</html>";

            await File.WriteAllTextAsync(filePath, htmlContent);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                throw new Exception(API.Properties.Resources.Upload_Failed + " " + ex.Message);
            }

            _ = Task.Delay(5000).ContinueWith(_ =>
            {
                try
                {
                    if (File.Exists(filePath)) File.Delete(filePath);
                }
                catch { }
            }, TaskScheduler.Default);
        }
    }
}