using Serilog;

var host = Host.CreateDefaultBuilder(args)
               .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup("NetPro.Startup"))
               .UseSerilog();//����serilog��־������ȡ��UseSerilogע��

host.Build().Run();
