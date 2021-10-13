using ChargePointOperator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OCPPCentralStation.Controllers;
using ProtocolGateway;
using SoapCore;
using System;

namespace OCPPCentralStation
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

            services.Configure<CookiePolicyOptions>(o =>
            {
                o.Secure = CookieSecurePolicy.Always;
                o.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;

            });

            services.AddHsts(options =>
            {
                options.Preload = true;
                options.IncludeSubDomains = true;
                options.MaxAge = TimeSpan.FromDays(365);
            });

            //Injecting the Protocol gateway client
            services.AddSingleton<ICloudGatewayClient>(new IoTHubClient(Configuration));
            services.AddSingleton<CentralSystemService>(new SOAPMiddleware());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHsts();
            app.UseHttpsRedirection();
            app.UseCookiePolicy();

            app.UseRouting();

            app.UseAuthorization();

            app.UseSoapEndpoint<CentralSystemService>("/service.asmx", new SoapEncoderOptions(), SoapSerializer.XmlSerializer);

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
            app.UseMiddleware<WebsocketMiddleware>();

            app.Run(async (context) =>
            {
                if (context.Request.Path.Value == "/")
                    await context.Response.WriteAsync("OCPP Central System running.");
                else
                    await context.Response.WriteAsync("Invalid Request");
            });
        }
    }
}
