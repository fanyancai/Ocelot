﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using CacheManager.Core;
using IdentityServer4.Models;
using IdentityServer4.Test;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ocelot.Authentication.Handler.Creator;
using Ocelot.Authentication.Handler.Factory;
using Ocelot.Authorisation;
using Ocelot.Cache;
using Ocelot.Claims;
using Ocelot.Configuration.Creator;
using Ocelot.Configuration.File;
using Ocelot.Configuration.Parser;
using Ocelot.Configuration.Provider;
using Ocelot.Configuration.Repository;
using Ocelot.Configuration.Setter;
using Ocelot.Configuration.Validator;
using Ocelot.DownstreamRouteFinder.Finder;
using Ocelot.DownstreamRouteFinder.UrlMatcher;
using Ocelot.DownstreamUrlCreator;
using Ocelot.DownstreamUrlCreator.UrlTemplateReplacer;
using Ocelot.Headers;
using Ocelot.Infrastructure.Claims.Parser;
using Ocelot.Infrastructure.RequestData;
using Ocelot.LoadBalancer.LoadBalancers;
using Ocelot.Logging;
using Ocelot.QueryStrings;
using Ocelot.Request.Builder;
using Ocelot.Requester;
using Ocelot.Requester.QoS;
using Ocelot.Responder;
using Ocelot.ServiceDiscovery;

namespace Ocelot.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {

        public static IServiceCollection AddOcelotOutputCaching(this IServiceCollection services, Action<ConfigurationBuilderCachePart> settings)
        {
            var cacheManagerOutputCache = CacheFactory.Build<HttpResponseMessage>("OcelotOutputCache", settings);
            var ocelotCacheManager = new OcelotCacheManagerCache<HttpResponseMessage>(cacheManagerOutputCache);
            services.AddSingleton<ICacheManager<HttpResponseMessage>>(cacheManagerOutputCache);
            services.AddSingleton<IOcelotCache<HttpResponseMessage>>(ocelotCacheManager);

            return services;
        }
        public static IServiceCollection AddOcelotFileConfiguration(this IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<FileConfiguration>(configurationRoot);
            services.AddSingleton<IOcelotConfigurationCreator, FileOcelotConfigurationCreator>();
            services.AddSingleton<IOcelotConfigurationRepository, InMemoryOcelotConfigurationRepository>();
            services.AddSingleton<IConfigurationValidator, FileConfigurationValidator>();
            return services;
        }

        public static IServiceCollection AddOcelot(this IServiceCollection services)
        {
            return AddOcelot(services, null);
        }

        public static IServiceCollection AddOcelot(this IServiceCollection services, IdentityServerConfiguration identityServerConfiguration)
        {
            if(identityServerConfiguration != null)
            {
                services.AddIdentityServer()
                    .AddTemporarySigningCredential()
                    .AddInMemoryApiResources(new List<ApiResource>
                    {
                        new ApiResource
                        {
                            Name = identityServerConfiguration.ApiName,
                            Description = identityServerConfiguration.Description,
                            Enabled = identityServerConfiguration.Enabled,
                            DisplayName = identityServerConfiguration.ApiName,
                            Scopes = identityServerConfiguration.AllowedScopes.Select(x => new Scope(x)).ToList(),
                            ApiSecrets = new List<Secret>
                            {
                                new Secret
                                {
                                    Value = identityServerConfiguration.ApiSecret.Sha256()
                                }
                            }
                        }
                    })
                    .AddInMemoryClients(new List<Client>
                    {
                        new Client
                        {
                            ClientId = identityServerConfiguration.ApiName,
                            AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                            ClientSecrets = new List<Secret> {new Secret(identityServerConfiguration.ApiSecret.Sha256())},
                            AllowedScopes = identityServerConfiguration.AllowedScopes,
                            AccessTokenType = identityServerConfiguration.AccessTokenType,
                            Enabled = identityServerConfiguration.Enabled,
                            RequireClientSecret = identityServerConfiguration.RequireClientSecret
                        }
                    })
                    .AddTestUsers(identityServerConfiguration.Users);
            }
        
            services.AddMvcCore()
                .AddAuthorization()
                .AddJsonFormatters();
            services.AddLogging();
            services.AddSingleton<IFileConfigurationRepository, FileConfigurationRepository>();
            services.AddSingleton<IFileConfigurationSetter, FileConfigurationSetter>();
            services.AddSingleton<Configuration.Provider.IFileConfigurationProvider, Configuration.Provider.FileConfigurationProvider>();
            services.AddSingleton<IQosProviderHouse, QosProviderHouse>();
            services.AddSingleton<IQoSProviderFactory, QoSProviderFactory>();
            services.AddSingleton<IServiceDiscoveryProviderFactory, ServiceDiscoveryProviderFactory>();
            services.AddSingleton<ILoadBalancerFactory, LoadBalancerFactory>();
            services.AddSingleton<ILoadBalancerHouse, LoadBalancerHouse>();
            services.AddSingleton<IOcelotLoggerFactory, AspDotNetLoggerFactory>();
            services.AddSingleton<IUrlBuilder, UrlBuilder>();
            services.AddSingleton<IRemoveOutputHeaders, RemoveOutputHeaders>();
            services.AddSingleton<IOcelotConfigurationProvider, OcelotConfigurationProvider>();
            services.AddSingleton<IClaimToThingConfigurationParser, ClaimToThingConfigurationParser>();
            services.AddSingleton<IAuthoriser, ClaimsAuthoriser>();
            services.AddSingleton<IAddClaimsToRequest, AddClaimsToRequest>();
            services.AddSingleton<IAddHeadersToRequest, AddHeadersToRequest>();
            services.AddSingleton<IAddQueriesToRequest, AddQueriesToRequest>();
            services.AddSingleton<IClaimsParser, ClaimsParser>();
            services.AddSingleton<IUrlPathToUrlTemplateMatcher, RegExUrlMatcher>();
            services.AddSingleton<IUrlPathPlaceholderNameAndValueFinder, UrlPathPlaceholderNameAndValueFinder>();
            services.AddSingleton<IDownstreamPathPlaceholderReplacer, DownstreamTemplatePathPlaceholderReplacer>();
            services.AddSingleton<IDownstreamRouteFinder, DownstreamRouteFinder.Finder.DownstreamRouteFinder>();
            services.AddSingleton<IHttpRequester, HttpClientHttpRequester>();
            services.AddSingleton<IHttpResponder, HttpContextResponder>();
            services.AddSingleton<IRequestCreator, HttpRequestCreator>();
            services.AddSingleton<IErrorsToHttpStatusCodeMapper, ErrorsToHttpStatusCodeMapper>();
            services.AddSingleton<IAuthenticationHandlerFactory, AuthenticationHandlerFactory>();
            services.AddSingleton<IAuthenticationHandlerCreator, AuthenticationHandlerCreator>();

            // see this for why we register this as singleton http://stackoverflow.com/questions/37371264/invalidoperationexception-unable-to-resolve-service-for-type-microsoft-aspnetc
            // could maybe use a scoped data repository
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddScoped<IRequestScopedDataRepository, HttpDataRepository>();

            return services;
        }
    }
}
