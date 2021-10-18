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
using System.Threading;
using System.Threading.Tasks;

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
            IoTCentralClient centralClient = new IoTCentralClient();
            services.AddSingleton<ICloudGatewayClient>(centralClient);

            SOAPMiddlewareOCPP15 s15Client = new SOAPMiddlewareOCPP15(centralClient);
            services.AddSingleton<I_OCPP_CentralSystemService_15>(s15Client);

            SOAPMiddlewareOCPP16 s16Client = new SOAPMiddlewareOCPP16(centralClient);
            services.AddSingleton<I_OCPP_CentralSystemService_16>(s16Client);

            if (Environment.GetEnvironmentVariable("RUN_TESTS") != null)
            {
                Program.TestTask = RunTests15S(s15Client);
            }
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

        private async Task RunTests15S(SOAPMiddlewareOCPP15 s15Client)
        {
            await Task.Run(() =>
            {
                // loop forever on a background thread
                Thread.CurrentThread.IsBackground = true;

                // wait 5 seconds for our backend to be up
                Thread.Sleep(5000);

                while (true)
                {
                    try
                    {
                        // send boot notification
                        Schemas.OCPP15.BootNotificationRequest bootRequest = new Schemas.OCPP15.BootNotificationRequest();
                        bootRequest.chargeBoxIdentity = "TestChargePoint";
                        s15Client.BootNotification(bootRequest);
                        Thread.Sleep(2000);

                        // send status avaiable
                        Schemas.OCPP15.StatusNotificationRequest statusRequest = new Schemas.OCPP15.StatusNotificationRequest();
                        statusRequest.chargeBoxIdentity = "TestChargePoint";
                        statusRequest.connectorId = 1;
                        statusRequest.status = Schemas.OCPP15.ChargePointStatus.Available;
                        s15Client.StatusNotification(statusRequest);
                        Thread.Sleep(30000);

                        // run 100 tests
                        int meter = 0;
                        for (int i = 0; i < 100; i++)
                        {

                            // send auth for a badge
                            Schemas.OCPP15.AuthorizeRequest authRequest = new Schemas.OCPP15.AuthorizeRequest();
                            authRequest.chargeBoxIdentity = "TestChargePoint";
                            authRequest.idTag = "1234";
                            Schemas.OCPP15.AuthorizeResponse authResponse = s15Client.Authorize(authRequest);
                            Thread.Sleep(2000);

                            // send status occupied
                            statusRequest.status = Schemas.OCPP15.ChargePointStatus.Occupied;
                            Schemas.OCPP15.StatusNotificationResponse statusResponse = s15Client.StatusNotification(statusRequest);
                            Thread.Sleep(2000);

                            // send start transaction
                            Schemas.OCPP15.StartTransactionRequest startRequest = new Schemas.OCPP15.StartTransactionRequest();
                            startRequest.chargeBoxIdentity = "TestChargePoint";
                            startRequest.connectorId = 1;
                            startRequest.idTag = "1234";
                            startRequest.meterStart = meter;
                            startRequest.timestamp = DateTime.UtcNow;
                            Schemas.OCPP15.StartTransactionResponse startResponse = s15Client.StartTransaction(startRequest);
                            Thread.Sleep(30000);

                            // consume 10kW
                            meter += 10;

                            // send meter readings
                            Schemas.OCPP15.MeterValuesRequest meterRequest = new Schemas.OCPP15.MeterValuesRequest();
                            meterRequest.chargeBoxIdentity = "TestChargePoint";
                            meterRequest.connectorId = 1;
                            meterRequest.transactionId = startResponse.transactionId;
                            meterRequest.values = new Schemas.OCPP15.MeterValue[1];
                            meterRequest.values[0] = new Schemas.OCPP15.MeterValue();
                            meterRequest.values[0].timestamp = DateTime.UtcNow;
                            meterRequest.values[0].value = new Schemas.OCPP15.MeterValueValue[1];
                            meterRequest.values[0].value[0] = new Schemas.OCPP15.MeterValueValue();
                            meterRequest.values[0].value[0].Value = meter.ToString();
                            meterRequest.values[0].value[0].unit = Schemas.OCPP15.UnitOfMeasure.kW;
                            meterRequest.values[0].value[0].unitSpecified = true;
                            Schemas.OCPP15.MeterValuesResponse meterResponse = s15Client.MeterValues(meterRequest);

                            // send stop transaction
                            Schemas.OCPP15.StopTransactionRequest stopRequest = new Schemas.OCPP15.StopTransactionRequest();
                            stopRequest.chargeBoxIdentity = "TestChargePoint";
                            stopRequest.idTag = "1234";
                            stopRequest.transactionId = startResponse.transactionId;
                            stopRequest.meterStop = meter;
                            stopRequest.timestamp = DateTime.UtcNow;
                            Schemas.OCPP15.StopTransactionResponse stopResponse = s15Client.StopTransaction(stopRequest);
                            Thread.Sleep(2000);

                            // send status avaiable
                            statusRequest.status = Schemas.OCPP15.ChargePointStatus.Available;
                            statusResponse = s15Client.StatusNotification(statusRequest);
                            Thread.Sleep(30000);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception: " + ex.Message);
                        Console.WriteLine(ex.StackTrace);
                    }
                }
            }).ConfigureAwait(false);
        }
    }
}
