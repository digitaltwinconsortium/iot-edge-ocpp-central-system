/*
Copyright 2020 Cognizant
Copyright 2021 Microsoft Corporation
*/

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OCPPCentralSystem.Controllers;
using OCPPCentralSystem.Schemas.OCPP15;
using OCPPCentralSystem.Schemas.OCPP16;
using SoapCore;
using System;
using System.ServiceModel.Channels;

namespace OCPPCentralSystem
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSoapCore();
            services.AddControllers();

            services.AddHsts(options =>
            {
                options.Preload = true;
                options.IncludeSubDomains = true;
                options.MaxAge = TimeSpan.FromDays(365);
            });

            // Injecting the Protocol gateway client
            IoTCentralClient client = new IoTCentralClient();
            services.AddSingleton<ICloudGatewayClient>(client);
            services.AddSingleton<I_OCPP_CentralSystemService_15>(new SOAPMiddlewareOCPP15(client));
            services.AddSingleton<I_OCPP_CentralSystemService_16>(new SOAPMiddlewareOCPP16(client));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHsts();

            app.UseRouting();

            SoapEncoderOptions options = new SoapEncoderOptions();
            options.MessageVersion = MessageVersion.Soap12WSAddressing10;
            app.UseSoapEndpoint<I_OCPP_CentralSystemService_15>("/ocpp/centralsystem/1/5.asmx", options, SoapSerializer.XmlSerializer);
            app.UseSoapEndpoint<I_OCPP_CentralSystemService_16>("/ocpp/centralsystem/1/6.asmx", options, SoapSerializer.XmlSerializer);

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.Use(async (context, next) =>
            {
                context.Request.Headers.TryGetValue("Origin", out var origin);
                context.Response.Headers.Append("Access-Control-Allow-Origin", origin);
                context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
                await next();
            });

            var webSocketOptions = new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(1)
            };
            app.UseWebSockets(webSocketOptions);
            app.UseMiddleware<WebsocketJsonMiddlewareOCPP16>();

            app.Run(async (context) =>
            {
                if (context.Request.Path.Value == "/")
                {
                    await context.Response.WriteAsync("OCPP Central System running.");
                }
                else
                {
                    await context.Response.WriteAsync("Invalid Request");
                }
            });
        }
    }
}
