using System.Net.Http.Headers;

using BloxstrapWebsite.Data;
using BloxstrapWebsite.Services;

using Coravel;
using InfluxDB.Client;
using Microsoft.AspNetCore.HttpOverrides;

namespace BloxstrapWebsite
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor 
                    | ForwardedHeaders.XForwardedProto 
                    | ForwardedHeaders.XForwardedHost;
            });

            builder.Services.AddSingleton<IStatsService, StatsService>();
            builder.Services.AddSingleton<IInfluxDBClient, InfluxDBClient>(x =>
            {
                var options = new InfluxDBClientOptions("https://influxdb.internal.pizzaboxer.xyz")
                {
                    Token = builder.Configuration["Credentials:InfluxDBToken"]
                };

                return new InfluxDBClient(options);
            });

            builder.Services.AddScheduler();
            builder.Services.AddTransient<StatsJobInvocable>();

            builder.Services.AddDbContext<ApplicationDbContext>();

            builder.Services.AddHttpClient("Global", client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "bloxstraplabs/website");
            });

            builder.Services.AddHttpClient("GitHub", client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "bloxstraplabs/website");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Bearer", builder.Configuration["Credentials:GitHubToken"]
                );
            });

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            app.UseForwardedHeaders();
            app.Services.UseScheduler(scheduler => scheduler.Schedule<StatsJobInvocable>().Hourly());

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
            }
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
