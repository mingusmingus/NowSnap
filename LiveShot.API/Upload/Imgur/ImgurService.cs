using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using LiveShot.API.Upload.Exceptions;
using Microsoft.Extensions.Configuration;

namespace LiveShot.API.Upload.Imgur
{
    public class ImgurService : IUploadService
    {
        private readonly string? _clientId;
        private readonly HttpClient _httpClient;

        public ImgurService(IConfiguration configuration, HttpClient httpClient)
        {
            _clientId = configuration.GetSection("UploadTypes")?.GetSection("Imgur")?["ClientID"];
            _httpClient = httpClient;
        }

        public async Task<string> Upload(Bitmap bitmap)
        {
            if (_clientId is null)
                throw new InvalidClientIdException();

            try
            {
                var requestMessage = CreateRequestMessage(bitmap);
                var response = await _httpClient.SendAsync(requestMessage);

                response.EnsureSuccessStatusCode();

                string responseString = await response.Content.ReadAsStringAsync();
                var responseData = JsonSerializer.Deserialize<ImgurResponse>(responseString);

                if (response != null && response.IsSuccessStatusCode)
                    throw new Exception(Properties.Resources.Upload_Failed);

                return responseData.Data.Link;
            }
            catch (Exception)
            {
                throw new Exception(Properties.Resources.Upload_Failed);
            }
        }

        private HttpRequestMessage CreateRequestMessage(Bitmap bitmap)
        {
            ImageConverter converter = new();
            byte[]? bytes = (byte[]?)converter.ConvertTo(bitmap, typeof(byte[]));

            if (bytes == null)
            {
                throw new InvalidOperationException("Failed to convert bitmap to bytes.");
            }

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.imgur.com/3/image");
            request.Headers.Authorization = new AuthenticationHeaderValue("Client-ID", _clientId);

            var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(bytes);
            imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
            content.Add(imageContent, "image", "image.png");

            request.Content = content;

            return request;
        }
    }
}
