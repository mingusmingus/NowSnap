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

            var requestMessage = CreateRequestMessage(bitmap);
            var response = await _httpClient.SendAsync(requestMessage);

            response.EnsureSuccessStatusCode();

            string responseString = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<ImgurResponse>(responseString);

            if (responseData.Data.Link is null)
                throw new Exception("Imgur API did not return a link.");

            return responseData.Data.Link;
        }

        private HttpRequestMessage CreateRequestMessage(Bitmap bitmap)
        {
            ImageConverter converter = new();
            byte[] bytes = ((byte[]) converter.ConvertTo(bitmap, typeof(byte[])))!;

            string base64Image = Convert.ToBase64String(bytes);

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.imgur.com/3/image");
            request.Headers.Authorization = new AuthenticationHeaderValue("Client-ID", _clientId);

            // Imgur API expects x-www-form-urlencoded or multipart/form-data.
            var content = new StringContent("image=" + Uri.EscapeDataString(base64Image));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            request.Content = content;

            return request;
        }
    }
}
