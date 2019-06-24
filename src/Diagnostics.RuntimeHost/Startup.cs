﻿using Diagnostics.DataProviders;
using Diagnostics.DataProviders.TokenService;
using Diagnostics.RuntimeHost.Middleware;
using Diagnostics.RuntimeHost.Services;
using Diagnostics.RuntimeHost.Services.CacheService;
using Diagnostics.RuntimeHost.Services.CacheService.Interfaces;
using Diagnostics.RuntimeHost.Services.SourceWatcher;
using Diagnostics.RuntimeHost.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;

namespace Diagnostics.RuntimeHost
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IHostingEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public IConfiguration Configuration { get; }
        public IHostingEnvironment Environment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();

            services.AddSingleton<IDataSourcesConfigurationService, DataSourcesConfigurationService>();
            services.AddSingleton<ICompilerHostClient, CompilerHostClient>();
            services.AddSingleton<ISourceWatcherService, SourceWatcherService>();
            services.AddSingleton<IInvokerCacheService, InvokerCacheService>();
            services.AddSingleton<IGistCacheService, GistCacheService>();
            services.AddSingleton<ISiteService, SiteService>();
            services.AddSingleton<IStampService, StampService>();
            services.AddSingleton<IAssemblyCacheService, AssemblyCacheService>();

            bool searchIsEnabled = false;
            if (Environment.IsProduction())
            {
                searchIsEnabled = Convert.ToBoolean(Registry.GetValue(RegistryConstants.SearchAPIRegistryPath, RegistryConstants.SearchAPIEnabledKey, string.Empty));
            }
            else
            {
                searchIsEnabled = Convert.ToBoolean(Configuration[$"SearchAPI:{RegistryConstants.SearchAPIEnabledKey}"]);
            }
            if (searchIsEnabled)
            {
                services.AddSingleton<ISearchService, SearchService>();
            }
            else
            {
                services.AddSingleton<ISearchService, SearchServiceDisabled>();
            }

            var servicesProvider = services.BuildServiceProvider();
            var dataSourcesConfigService = servicesProvider.GetService<IDataSourcesConfigurationService>();
            var observerConfiguration = dataSourcesConfigService.Config.SupportObserverConfiguration;
            var kustoConfiguration = dataSourcesConfigService.Config.KustoConfiguration;

            services.AddSingleton<IKustoHeartBeatService>(new KustoHeartBeatService(kustoConfiguration));

            observerConfiguration.AADAuthority = dataSourcesConfigService.Config.KustoConfiguration.AADAuthority;
            var wawsObserverTokenService = new ObserverTokenService(observerConfiguration.WawsObserverResourceId, observerConfiguration);
            var supportBayApiObserverTokenService = new ObserverTokenService(observerConfiguration.SupportBayApiObserverResourceId, observerConfiguration);
            services.AddSingleton<IWawsObserverTokenService>(wawsObserverTokenService);
            services.AddSingleton<ISupportBayApiObserverTokenService>(supportBayApiObserverTokenService);

            KustoTokenService.Instance.Initialize(dataSourcesConfigService.Config.KustoConfiguration);
            ChangeAnalysisTokenService.Instance.Initialize(dataSourcesConfigService.Config.ChangeAnalysisDataProviderConfiguration);
            AscTokenService.Instance.Initialize(dataSourcesConfigService.Config.AscDataProviderConfiguration);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseDiagnosticsRequestMiddleware();
            app.UseMvc();
        }
    }
}
