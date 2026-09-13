using Microsoft.Extensions.Options;
using PetChat.ApiServer.Model.Configuration;

namespace PetChat.ApiServer.Middleware
{
    public class AccessMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate next = next;

        public async Task InvokeAsync(HttpContext context, 
            IOptions<AccessConfig> accessConfig, 
            IWebHostEnvironment env, 
            ILogger<AccessMiddleware> logger)
        {
            if (env.IsDevelopment()) {
                await next.Invoke(context);
                return;
            }

            var clientKey = context.Request.Headers["X-CLIENT-KEY"];
            if (!accessConfig.Value.ClientKeys.Contains(clientKey.FirstOrDefault()))
            {
                context.Response.StatusCode = 404; 
                context.Connection.RequestClose();
                logger.LogInformation("Request no contains X-CLIENT-KEY. Connection closed with 404 error.");
                return;
            }

            var deviceId = context.Request.Headers["X-DEVICE-ID"];
            if (accessConfig.Value.BannedDevicesList?.Contains(deviceId.FirstOrDefault()) == true)
            {
                context.Response.StatusCode = 403;
                context.Connection.RequestClose();
                logger.LogWarning("Client device identities contains in BannedDevicesList. Connection closed with 403 error.");
                return;
            }

            await next.Invoke(context);
        }
    }
}