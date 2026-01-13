using System.Linq;
using System.Reflection;
using LiveShot.API.Background;
using LiveShot.API.Background.ContextOptions;
using LiveShot.API.Drawing;
using LiveShot.API.Upload;
using LiveShot.API.Upload.Custom;
using LiveShot.API.Upload.Imgur;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LiveShot.API
{
    public static class Container
    {
        public static IServiceCollection ConfigureAPI(this IServiceCollection services, IConfiguration? configuration)
        {
            // SERVICIOS CORE: Fundamentales para que el programa reaccione y no se cierre
            services.AddSingleton<IEventPipeline, EventPipeline>();
            services.AddSingleton<IBackgroundApplication, BackgroundApplication>();

            // CLIENTE DE RED PROFESIONAL: Con identidad de navegador moderno (Anti-Bloqueo)
            services.AddHttpClient("GoogleLensClient", client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("Accept-Language", "es-ES,es;q=0.9");
            });

            if (configuration != null)
            {
                string? uploadType = configuration["UploadType"];
                if (uploadType != null && uploadType.ToLower().Equals("imgur"))
                    services.AddSingleton<IUploadService, ImgurService>();
                else
                    services.AddSingleton<IUploadService, CustomUploadService>();
            }
            else
            {
                services.AddSingleton<IUploadService, ImgurService>();
            }

            services.AddSingleton<ILiveShotService, LiveShotService>();

            // Escaneo automático de herramientas y opciones (Arquitectura limpia)
            var drawingTools = Assembly.GetExecutingAssembly().GetTypes()
                .Where(a => a.GetInterfaces().Contains(typeof(IDrawingTool)) && !a.IsAbstract);
            foreach (var tool in drawingTools) services.AddSingleton(typeof(IDrawingTool), tool);

            var contextOptions = Assembly.GetExecutingAssembly().GetTypes()
                .Where(a => a.GetInterfaces().Contains(typeof(IContextOption)) && !a.IsAbstract);
            foreach (var option in contextOptions) services.AddSingleton(typeof(IContextOption), option);

            return services;
        }
    }
}