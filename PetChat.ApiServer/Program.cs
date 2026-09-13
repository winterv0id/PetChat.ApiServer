using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using PetChat.ApiServer.Model.Configuration;
using PetChat.ApiServer.Middleware;
using PetChat.ApiServer.Database;
using PetChat.ApiServer.Database.Repositories.Interfaces;
using PetChat.ApiServer.Database.Repositories;
using PetChat.ApiServer.Services.Synchronization.Interfaces;
using PetChat.ApiServer.Services.Synchronization;
using PetChat.ApiServer.Services.DomainServices.Interfaces;
using PetChat.ApiServer.Services.DomainServices;
using PetChat.ApiServer.Services.BackgroundServices;
using PetChat.ApiServer.SignalR.PresenceTracker;
using PetChat.ApiServer.SignalR.Hubs;
using PetChat.ApiServer.Model.Utils;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

namespace PetChat.ApiServer
{
    public class Program
    {
        public const string ClientVersionsFolder = "versions";
        
        public static void Main(string[] args)
        {
            var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

            #region BUILDER
            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseNLog();

            #region Configuration
            builder.Configuration["IsDevelopment"] = builder.Environment.IsDevelopment().ToString();

            builder.Configuration.AddJsonFile("access_config.json");

            builder.Services.Configure<AccessConfig>(options => 
                builder.Configuration.Bind(options, c => c.BindNonPublicProperties = true));
            #endregion

            #region Add services
            builder.Services.AddControllers(options =>
            {
                options.Conventions.Add(new DotNotationRoutingConvention());
            });

            builder.Services.AddAuthorization();
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    //regenerate cert
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = "PetChatApi",
                        ValidAudience = "PetChatClient",
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,

                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SymmetricKey"]!)),
                    };
                });

            if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddDbContext<PetChatDbContext>(options =>
                    options.UseNpgsql(builder.Configuration.GetConnectionString("TestConnection")));
            } 
            else
            {
                builder.Services.AddDbContext<PetChatDbContext>(options =>
                    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
            }
            
            builder.Services.AddScoped<IUserCreditionalsRepository, UserCreditionalsRepository>();
            builder.Services.AddScoped<IUsersRepository, UsersRepository>();
            builder.Services.AddScoped<IUserEventRepository, UserEventRepository>();
            builder.Services.AddScoped<IUserAckRepository, UserAckRepository>();
            builder.Services.AddScoped<IChatsRepository, ChatsRepository>();
            builder.Services.AddScoped<IChatPropertiesRepository, ChatPropertiesRepository>();
            builder.Services.AddScoped<IMessagesRepository, MessagesRepository>();
            builder.Services.AddScoped<IUserRelationsRepository, UserRelationsRepository>();
            builder.Services.AddScoped<ISnapshotService, SnapshotService>();
            builder.Services.AddScoped<ISyncService, SyncService>();
            builder.Services.AddScoped<IAckService, AckService>();
            builder.Services.AddScoped<IUserEventPublisher, UserEventPublisher>();
            builder.Services.AddScoped<IMessagesService, MessagesService>();
            builder.Services.AddScoped<IPresenceEventService, PresenceEventService>();
            builder.Services.AddScoped<IPushNotificationsService, FcmPushNotificationsService>();
            builder.Services.AddSingleton<IPresenceTracker, InMemoryPresenceTracker>(); // redis?

            builder.Services.AddHostedService<UserEventTrimJob>();

            builder.Services.AddSignalR()
                .AddJsonProtocol(options =>
                {
                    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                });

            FirebaseApp.Create(new AppOptions
            {
                Credential = CredentialFactory
                    .FromFile<ServiceAccountCredential>(builder.Configuration["Firebase:ServiceAccountPath"])
                    .ToGoogleCredential()
            });
            #endregion

            #region Environment-dependent
            if (builder.Environment.IsDevelopment())
            {
                logger.Info("[!] App Environment is \"Development\". Swagger will be available.");

                builder.Services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new OpenApiInfo
                    {
                        Title = "PetChat",
                        Description = "API Server",
                        Version = "v1"
                    });
                });

                //DEV KESTREL CONFIGURATION
                builder.WebHost.ConfigureKestrel(serverOptions =>
                {
                    serverOptions.ConfigureHttpsDefaults(listenOptions =>
                    {
                        listenOptions.ClientCertificateMode = ClientCertificateMode.NoCertificate;
                    });
                });
            }
            else
            {
                //PROD KESTREL CONFIGURATION
                builder.WebHost.ConfigureKestrel(serverOptions =>
                {
                    serverOptions.ConfigureHttpsDefaults(listenOptions =>
                    {
                        listenOptions.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
                        listenOptions.SslProtocols = SslProtocols.Tls13;
                    });
                });
            }
            #endregion

            #endregion

            #region APP
            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "PetChat API"));
            }

            app.UseMiddleware<AccessMiddleware>();

            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.MapHub<ChatHub>("/hubs/chat");

            MapApiMethods(app);
            #endregion

            app.Run();
        }

        
        private static void MapApiMethods(WebApplication app)
        {
            app.MapGet("/", () => Results.Ok());

            app.MapGet("auth/check", (HttpContext context, ILogger<Program> logger) =>
            {
                return Results.Ok();
            }).RequireAuthorization();
        }
    }
}