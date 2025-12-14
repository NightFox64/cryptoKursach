using ChatClient.Services;
using ChatClient.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ChatClient
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider _serviceProvider;

        public App()
        {
            ServiceCollection services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(ServiceCollection services)
        {
            services.AddSingleton<HttpClient>();
            services.AddSingleton<IChatApiClient, ChatApiClient>();
            services.AddSingleton<IEncryptionService, EncryptionService>();
            services.AddSingleton<IServiceProvider>(sp => _serviceProvider);

            // Configure DbContext
            services.AddDbContext<ChatClient.Data.ApplicationDbContext>(options =>
                options.UseSqlite("Data Source=chatclient.db"));

            // Register local data service
            services.AddSingleton<ILocalDataService, LocalDataService>();


            // Register your windows and viewmodels here
            services.AddTransient<LoginWindow>();
            services.AddTransient<RegisterWindow>();
            services.AddTransient<ChatListWindow>();
            services.AddTransient<ChatWindow>();
            services.AddTransient<AlgorithmSettingsWindow>();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                var localDataService = _serviceProvider.GetService<ILocalDataService>();
                if (localDataService != null)
                {
                    await localDataService.InitializeAsync();
                }

                var loginWindow = _serviceProvider.GetService<LoginWindow>();
                loginWindow?.Show();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unhandled exception occurred during startup: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                MessageBox.Show($"An unhandled error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1); // Shut down the application with an error code
            }
        }
    }
}
