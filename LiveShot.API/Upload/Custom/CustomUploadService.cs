using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using LiveShot.API.Properties;
using LiveShot.API.Upload.Exceptions;
using Microsoft.Extensions.Configuration;

namespace LiveShot.API.Upload.Custom
{
    public class CustomUploadService : IUploadService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public CustomUploadService(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public async Task<string> Upload(Bitmap bitmap)
        {
            try
            {
                var request = CreateRequest(bitmap);
                var response = await _httpClient.SendAsync(request);

                response.EnsureSuccessStatusCode();

                string responseString = await response.Content.ReadAsStringAsync();

                // Assuming the custom upload returns the link directly or we just return success message as before.
                // The previous implementation returned Resources.Upload_Success regardless of content,
                // but read the stream to end.
                // We will stick to the previous behavior unless we can parse a link.

                return Resources.Upload_Success;
            }
            catch (Exception)
            {
                throw new Exception(Resources.Upload_Failed);
            }
        }

        private HttpRequestMessage CreateRequest(Bitmap bitmap)
        {
            string uploadType = _configuration["UploadType"] ?? throw new InvalidUploadTypeException();

            var uploadConfig = _configuration
                .GetSection("UploadTypes")
                ?.GetSection(uploadType) ?? throw new InvalidUploadTypeException();

            string endpoint = uploadConfig["Endpoint"] ?? throw new InvalidUploadTypeException();
            string methodString = uploadConfig["Method"] ?? "POST";
            HttpMethod method = new HttpMethod(methodString);

            var request = new HttpRequestMessage(method, endpoint);

            var headers = uploadConfig.GetSection("Headers")?.GetChildren();
            if (headers != null)
            {
                foreach (var child in headers)
                {
                    if (child.Key != null && child.Value != null)
                    {
                        request.Headers.TryAddWithoutValidation(child.Key, child.Value);
                    }
                }
            }

            // Determine content type (defaulting to x-www-form-urlencoded if not specified to match legacy behavior,
            // but we can support multipart if config says so or if we want to modernize).
            // The prompt says "Implementa la subida de imágenes usando MultipartFormDataContent".
            // However, CustomUploadService is generic. If we force Multipart, we might break existing configs
            // that expect x-www-form-urlencoded.
            // But looking at the legacy implementation: it built a body string manually.
            // Let's check the config "ContentType".

            string contentType = uploadConfig["ContentType"] ?? "application/x-www-form-urlencoded";

            ImageConverter converter = new();
            byte[]? bytes = (byte[]?)converter.ConvertTo(bitmap, typeof(byte[]));

            if (bytes == null) throw new InvalidOperationException("Failed to convert bitmap");

            if (contentType.Contains("multipart/form-data"))
            {
                var multipartContent = new MultipartFormDataContent();
                var imageContent = new ByteArrayContent(bytes);
                imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

                string imageKey = uploadConfig["BodyImageKey"] ?? "image";
                multipartContent.Add(imageContent, imageKey, "image.png");

                var body = uploadConfig.GetSection("Body")?.GetChildren();
                if (body != null)
                {
                    foreach (var child in body)
                    {
                         if (child.Key != null && child.Value != null)
                         {
                             multipartContent.Add(new StringContent(child.Value), child.Key);
                         }
                    }
                }
                request.Content = multipartContent;
            }
            else
            {
                // Fallback to x-www-form-urlencoded (Legacy behavior replication with HttpClient)
                var kvp = new List<KeyValuePair<string, string>>();

                string imageKey = uploadConfig["BodyImageKey"] ?? "image";
                kvp.Add(new KeyValuePair<string, string>(imageKey, Convert.ToBase64String(bytes)));

                var body = uploadConfig.GetSection("Body")?.GetChildren();
                if (body != null)
                {
                    foreach (var child in body)
                    {
                        if (child.Key != null && child.Value != null)
                        {
                            kvp.Add(new KeyValuePair<string, string>(child.Key, child.Value));
                        }
                    }
                }

                request.Content = new FormUrlEncodedContent(kvp);
            }

            return request;
        }
    }
}
