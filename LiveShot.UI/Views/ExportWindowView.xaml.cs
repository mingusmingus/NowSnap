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
            ImageConverter converter = new();
            byte[]? bytes = (byte[]?)converter.ConvertTo(bitmap, typeof(byte[]));

            if (bytes == null)
            {
                throw new Exception(API.Properties.Resources.Upload_Failed);
            }

            var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(bytes);
            imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
            content.Add(imageContent, "encoded_image", "image.png");

            // Google Lens Upload URL
            // Using the standard upload endpoint.
            // Note: Google's endpoints change. This is a best-effort reverse-engineered endpoint commonly used.
            var request = new HttpRequestMessage(HttpMethod.Post, "https://lens.google.com/upload?ep=ccm&s=&st=");
            request.Content = content;

            // Google often returns the redirect URL in the header or body.
            // HttpClient automatically follows redirects for GET, but strictly for POST 302/303 it might depend.
            // However, Lens often returns a 200 OK with a window.location.replace or similar, OR a 302.

            // We want to stop auto-redirect to capture the URL if it's a 302,
            // OR if it returns HTML, we might need to parse it.
            // But usually, Search By Image returns a redirect.

            // Let's rely on HttpClient following redirects and getting the Final URL?
            // No, because we want to open it in the System Browser, not in HttpClient.

            // We need to verify if the response is a redirect.
            // If HttpClient follows it, we end up downloading the Google Search Results Page (HTML) in the background.
            // We want the URL.

            // So we need to disable AutoRedirect for this request.
            // But _httpClient is injected and shared (likely configured with defaults).
            // We cannot easily change its handler settings here.

            // Workaround: Use a separate HttpRequestMessage and check the response.
            // If the shared client follows redirects, response.RequestMessage.RequestUri will be the *final* URL.

            var response = await _httpClient.SendAsync(request);

            string finalUrl;

            if (response.StatusCode == System.Net.HttpStatusCode.Redirect ||
                response.StatusCode == System.Net.HttpStatusCode.Moved ||
                response.StatusCode == System.Net.HttpStatusCode.SeeOther)
            {
                finalUrl = response.Headers.Location?.ToString() ?? "";
            }
            else
            {
                // If it followed the redirect (200 OK)
                if (response.RequestMessage?.RequestUri != null &&
                    response.RequestMessage.RequestUri.Host.Contains("google"))
                {
                    finalUrl = response.RequestMessage.RequestUri.ToString();
                }
                else
                {
                    // Fallback: Sometimes it returns a text payload with the URL if not a standard redirect.
                    // Or if we failed to capture the redirect.
                    // For now, assume success if we are on a google page.
                    // If we are still at /upload, something failed or we need to parse content.

                    // Simple regex fallback for "AF_initDataCallback" which often contains the URL in the HTML
                    // or just fail gracefully.
                     throw new Exception(API.Properties.Resources.Upload_Failed);
                }
            }

            if (!string.IsNullOrEmpty(finalUrl))
            {
                OpenUrl(finalUrl);
            }
            else
            {
                 throw new Exception(API.Properties.Resources.Upload_Failed);
            }
        }
    }
}