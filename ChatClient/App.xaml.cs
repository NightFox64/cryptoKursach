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
using System.Windows.Threading; // Added for DispatcherUnhandledException

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

            // Add global exception handlers
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void ConfigureServices(ServiceCollection services)
        {
            services.AddSingleton<HttpClient>();
            services.AddSingleton<IChatApiClient, ChatApiClient>();
            services.AddSingleton<IEncryptionService, EncryptionService>();
            services.AddSingleton<IServiceProvider>(sp => _serviceProvider);

            // Configure DbContext
            services.AddDbContext<ChatClient.Data.ApplicationDbContext>(options =>
                options.UseNpgsql("Host=localhost;Port=5432;Database=chatclient_db;Username=postgres;Password=Ichiho64"));

            // Register local data service as Scoped to match DbContext lifetime
            services.AddScoped<ILocalDataService, LocalDataService>();


            // Register your windows and viewmodels here
            services.AddTransient<LoginWindow>();
            services.AddTransient<RegisterWindow>();
            services.AddTransient<ChatListWindow>();
            services.AddTransient<ChatWindow>();
            services.AddTransient<ChatView>();
            services.AddTransient<AlgorithmSettingsWindow>();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize file logger
            Services.FileLogger.Initialize();
            Services.FileLogger.Log("=== Application Starting ===");

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
                Services.FileLogger.Log($"FATAL: Unhandled exception during startup: {ex.Message}");
                Services.FileLogger.Log($"Stack trace: {ex.StackTrace}");
                Console.WriteLine($"An unhandled exception occurred during startup: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                MessageBox.Show($"An unhandled error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1); // Shut down the application with an error code
            }
        }

        // Handler for exceptions on the UI thread
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Console.WriteLine($"Unhandled exception (UI thread): {e.Exception.Message}");
            Console.WriteLine(e.Exception.StackTrace);
            MessageBox.Show($"An unhandled error occurred: {e.Exception.Message}", "Error (UI Thread)", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true; // Mark the exception as handled to prevent application termination
            // For severe errors, you might still want to shutdown: Shutdown(-1);
        }

        // Handler for exceptions on non-UI threads
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            if (ex != null)
            {
                Console.WriteLine($"Unhandled exception (non-UI thread): {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                MessageBox.Show($"An unhandled error occurred: {ex.Message}", "Error (Non-UI Thread)", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            // If e.IsTerminating is true, the application will terminate regardless of what we do here.
            // Forcing shutdown with -1 will ensure a non-zero exit code.
            if (e.IsTerminating)
            {
                Shutdown(-1);
            }
        }
    }
}
