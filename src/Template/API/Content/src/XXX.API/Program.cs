using Serilog;

Environment.SetEnvironmentVariable("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "NetPro.Startup");
var host = Host.CreateDefaultBuilder(args).ConfigureWebHostDefaults(webBuilder => { }).UseSerilog();//����serilog��־������ȡ��UseSerilogע��
host.Build().Run();
