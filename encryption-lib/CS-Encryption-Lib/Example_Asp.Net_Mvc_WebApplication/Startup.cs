using com.tmobile.oss.security.taap.jwe;
using com.tmobile.oss.security.taap.poptoken.builder;
using Example_Asp.Net_Mvc_WebApplication.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace Example_Asp.Net_Mvc_WebApplication
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // IHttpClientFactory
            services.AddHttpClient();                           

            // ILogger
            services.AddLogging();                              

            // IOptions
            services.AddOptions();                             
            var encryptionOptionsSection = Configuration.GetSection(nameof(EncryptionOptions));
            services.Configure<EncryptionOptions>(encryptionOptionsSection);
            var encryptionOptions = encryptionOptionsSection.Get<EncryptionOptions>();

            //// Can use Jwks Service (no oAuth token)
            //services.AddSingleton(serviceProvider =>
            //{
            //    var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
            //    return new JwksService(httpClientFactory.CreateClient(), encryptionOptions.JwksUrl);
            //});

            // Or can use KeyVault Jwks Service (if oAuth token is needed for KeyVault Jwks Service)
            services.AddSingleton<IOAuth2JwksService>(serviceProvider =>
            {
                var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient();
                return new OAuth2JwksService(encryptionOptions.OAuthClientKey, encryptionOptions.OAuthClientSecret, encryptionOptions.OAuthUrl, httpClient, encryptionOptions.JwksUrl);
            });

            //// Or use KeyVault Jwks Service (if oAuth token is needed for KeyVault Jwks Service, which requires a PopToken)
            //services.AddTransient<IPopTokenBuilder>(serviceProvider =>
            //{
            //    return new PopTokenBuilder(encryptionOptions.PopTokenAudience, encryptionOptions.PopTokenIssuer);
            //});

            //// OAuth2JwksService
            //services.AddSingleton<IOAuth2JwksService>(serviceProvider =>
            //{
            //    var popTokenBuilder = (PopTokenBuilder)serviceProvider.GetService<IPopTokenBuilder>();
            //    var privateKeyXml = encryptionOptions.PopTokenPrivateKeyXml;

            //    var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();

            //    var oAuthUrl = encryptionOptions.OAuthUrl;
            //    var oAuthClientKey = encryptionOptions.OAuthClientKey;
            //    var oAuthClientSecret = encryptionOptions.OAuthClientSecret;

            //    return new OAuth2JwksService(popTokenBuilder, privateKeyXml, oAuthClientKey, oAuthClientSecret, oAuthUrl, httpClientFactory.CreateClient(), encryptionOptions.JwksUrl);
            //});I

            // KeyResolver
            services.AddSingleton<IKeyResolver>(serviceProvider =>
            {
                //var jwksService = serviceProvider.GetService<JwksService>();  // No KeyVault, just JwksService
                var jwksService = serviceProvider.GetService<IOAuth2JwksService>();  // KeyVault JwksService (with option to use oAuth2 / PopToken)

                var privateJwksJson = File.ReadAllText(@"TestData\AllPrivate.json");
                var privateJwks = JsonSerializer.Deserialize<Jwks>(privateJwksJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                var privateJsonWebKeyList = new List<JsonWebKey>();
                privateJsonWebKeyList.AddRange(privateJwks.Keys);

                var keyPreference = (KeyPreference)Enum.Parse(typeof(KeyPreference), encryptionOptions.KeyPreference);
                return new KeyResolver(privateJsonWebKeyList, jwksService, encryptionOptions.CacheDurationSeconds, keyPreference);
            });

            // Encryption
            services.AddTransient<IEncryption>(serviceProvider =>
            {
                var keyResolver = serviceProvider.GetService<IKeyResolver>();
                var encryptionLogger = serviceProvider.GetService<ILogger<IEncryption>>();
                return new Encryption(keyResolver, encryptionLogger);
            });

            services.AddControllersWithViews();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
