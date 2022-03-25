using Serilog;

Environment.SetEnvironmentVariable("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "NetPro.Startup");//;NetPro.ConsulClient");

var host = Host.CreateDefaultBuilder(args)
                //.ConfigureAppConfiguration((hostingContext, config) =>
                //{
                //    ApolloClientHelper.ApolloConfig(hostingContext, config, args);
                //})
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(options =>
                    {
                        //options.Limits.MaxRequestBodySize = null;// �����쳣 Unexpected end of request content.
                    });
                }).UseSerilog();//����serilog��־������ȡ������ע��
host.Build().Run();
