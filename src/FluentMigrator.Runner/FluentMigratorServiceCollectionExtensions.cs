#region License
// Copyright (c) 2018, FluentMigrator Project
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using FluentMigrator;
using FluentMigrator.Exceptions;
using FluentMigrator.Infrastructure;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Announcers;
using FluentMigrator.Runner.Conventions;
using FluentMigrator.Runner.Generators;
using FluentMigrator.Runner.Initialization;
using FluentMigrator.Runner.Initialization.AssemblyLoader;
using FluentMigrator.Runner.Processors;
using FluentMigrator.Runner.VersionTableInfo;

using JetBrains.Annotations;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for setting up the migration runner services in an <see cref="IServiceCollection"/>.
    /// </summary>
    [CLSCompliant(false)]
    public static class FluentMigratorServiceCollectionExtensions
    {
        /// <summary>
        /// Adds migration runner services to the specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <typeparam name="TMigrationProcessorFactory">The type of the migration processor factory</typeparam>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="connectionString">The connection string used to connect to the database</param>
        /// <param name="configure">The <see cref="IMigrationRunnerBuilder"/> configuration delegate.</param>
        /// <returns>The updated service collection</returns>
        [NotNull]
        public static IServiceCollection AddFluentMigrator<TMigrationProcessorFactory>(
            [NotNull] this IServiceCollection services,
            [NotNull] string connectionString,
            [NotNull] Action<IMigrationRunnerBuilder> configure)
            where TMigrationProcessorFactory : class, IMigrationProcessorFactory
        {
            return services.AddFluentMigrator(typeof(TMigrationProcessorFactory), connectionString, configure);
        }

        /// <summary>
        /// Adds migration runner services to the specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="migrationProcessorFactoryType">The type of the migration processor factory</param>
        /// <param name="connectionString">The connection string used to connect to the database</param>
        /// <param name="configure">The <see cref="IMigrationRunnerBuilder"/> configuration delegate.</param>
        /// <returns>The updated service collection</returns>
        [NotNull]
        public static IServiceCollection AddFluentMigrator(
            [NotNull] this IServiceCollection services,
            [NotNull] Type migrationProcessorFactoryType,
            [NotNull] string connectionString,
            [NotNull] Action<IMigrationRunnerBuilder> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            return services
                .AddFluentMigratorCore()
                .ConfigureProcessorFactory(migrationProcessorFactoryType, connectionString)
                .ConfigureRunner(configure);
        }

        /// <summary>
        /// Configures the migration runner
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="configure">The <see cref="IMigrationRunnerBuilder"/> configuration delegate.</param>
        /// <returns>The updated service collection</returns>
        public static IServiceCollection ConfigureRunner(
            [NotNull] this IServiceCollection services,
            [NotNull] Action<IMigrationRunnerBuilder> configure)
        {
            var builder = new MigrationRunnerBuilder(services);
            configure.Invoke(builder);
            return services;
        }

        /// <summary>
        /// Creates services for a given runner context, connection string provider and assembly loader factory.
        /// </summary>
        /// <param name="runnerContext">The runner context</param>
        /// <param name="assemblyLoadFunc">Function to load the assemblies</param>
        /// <param name="connectionStringProvider">The connection string provider</param>
        /// <param name="defaultAssemblyLoaderFactory">The assembly loader factory</param>
        /// <returns>The new service collection</returns>
        [NotNull]
        public static IServiceCollection CreateServices(
            [NotNull] this IRunnerContext runnerContext,
            [NotNull] Func<AssemblyLoaderFactory, IRunnerContext, IEnumerable<Assembly>> assemblyLoadFunc,
            [CanBeNull] IConnectionStringProvider connectionStringProvider,
            [CanBeNull] AssemblyLoaderFactory defaultAssemblyLoaderFactory = null)
        {
            var services = new ServiceCollection();
            var assemblyLoaderFactory = defaultAssemblyLoaderFactory ?? new AssemblyLoaderFactory();
            var assemblies = assemblyLoadFunc(assemblyLoaderFactory, runnerContext).ToList();
#pragma warning disable 612
            var assemblyCollection = new AssemblyCollection(assemblies);
#pragma warning restore 612

            if (!runnerContext.NoConnection && connectionStringProvider == null)
            {
                runnerContext.NoConnection = true;
            }

            // Configure without the processor and migrations
            services
                .AddFluentMigratorCore()
                .AddSingleton(assemblyLoaderFactory)
                .AddSingleton(connectionStringProvider);

            // Configure the processor
            if (runnerContext.NoConnection)
            {
                var processorFactoryType = typeof(ConnectionlessProcessorFactory);
                services
                    .AddMigrationGenerators(MigrationGeneratorFactory.RegisteredGenerators)
                    .ConfigureProcessorFactory(processorFactoryType, string.Empty);
            }
            else
            {
                var connectionString = assemblies.LoadConnectionString(connectionStringProvider, runnerContext);
                services
                    .AddMigrationProcessorFactories(MigrationProcessorFactoryProvider.RegisteredFactories)
                    .ConfigureProcessor(connectionString);
            }

            // Configure other options
            services
                .ConfigureRunner(
                    builder =>
                    {
                        builder
                            .WithRunnerContext(runnerContext)
                            .WithAnnouncer(runnerContext.Announcer)
#pragma warning disable 612
                            .WithRunnerConventions(assemblyCollection.GetMigrationRunnerConventions())
#pragma warning restore 612
                            .AddMigrations(assemblies, runnerContext.Namespace, runnerContext.NestedNamespaces);
                    });

            // Configure the version table
            services.AddVersionTableMetaData(sp => assemblies.GetVersionTableMetaDataType(sp));

            return services;
        }

        /// <summary>
        /// Add the version table meta data using the configured services
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="getVersionTableMetaDataType">The function to determine the version table meta data type</param>
        /// <returns>The updated service collection</returns>
        public static IServiceCollection AddVersionTableMetaData(
            [NotNull] this IServiceCollection services,
            Func<IServiceProvider, Type> getVersionTableMetaDataType)
        {
            // Configure the version table
            using (var sp = services.BuildServiceProvider(false))
            {
                var versionTableMetaDataType = getVersionTableMetaDataType(sp);
                return services.AddScoped(typeof(IVersionTableMetaData), versionTableMetaDataType);
            }
        }

        /// <summary>
        /// Adds a migration generator accessor service
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="connectionString">The connection string used to connect to the database</param>
        /// <returns>The updated service collection</returns>
        internal static IServiceCollection ConfigureProcessor(
            [NotNull] this IServiceCollection services,
            [NotNull] string connectionString)
        {
            return services
                .ConfigureProcessor(_ => connectionString);
        }

        /// <summary>
        /// Adds a migration generator accessor service
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="connectionStringAccessor">Used to get the connection string</param>
        /// <returns>The updated service collection</returns>
        internal static IServiceCollection ConfigureProcessor(
            [NotNull] this IServiceCollection services,
            [NotNull] Func<IServiceProvider, string> connectionStringAccessor)
        {
            return services
                // Initialize the DB-specific processor
                .AddScoped(sp => CreateMigrationProcessor(sp, connectionStringAccessor(sp)));
        }

        /// <summary>
        /// Adds migration runner (except the DB processor specific) services to the specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <returns>The updated service collection</returns>
        [NotNull]
        internal static IServiceCollection AddFluentMigratorCore(
            [NotNull] this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services
                // Create the announcer to output the migration messages
                .AddSingleton<IAnnouncer, NullAnnouncer>()

                // Processor specific options (usually none are needed)
                .AddScoped<IMigrationProcessorOptions, ProcessorOptions>()

                // The default assembly loader factory
                .AddSingleton<AssemblyLoaderFactory>()

                // Configure the default way to get the connection string
                .AddSingleton<IConnectionStringProvider, DefaultConnectionStringProvider>()

                // Configure the default version table metadata
                .AddScoped<IVersionTableMetaData, DefaultVersionTableMetaData>()

                // Add the default embedded resource provider
                .AddScoped<IEmbeddedResourceProvider, DefaultEmbeddedResourceProvider>()

                // Configure the loader for migrations that should be executed during maintenance steps
                .AddScoped<IMaintenanceLoader, MaintenanceLoader>()

                // Configure the migration information loader
                .AddScoped<IMigrationInformationLoader, DefaultMigrationInformationLoader>()

                // Configure the runner context
                .AddScoped<IRunnerContext, RunnerContext>()

                // Provide a way to get the migration accessor selected by the runner context
                .AddScoped<IMigrationGeneratorAccessor, DefaultMigrationGeneratorAccessor>()

                // Configure the runner conventions
                .AddScoped<IMigrationRunnerConventions, MigrationRunnerConventions>()

                // The default set of conventions to be applied to migration expressions
                .AddScoped<IConventionSet, DefaultConventionSet>()

                // IQuerySchema is the base interface for the IMigrationProcessor
                .AddScoped<IQuerySchema>(sp => sp.GetRequiredService<IMigrationProcessor>())

                // Configure the runner
                .AddScoped<IMigrationRunner, MigrationRunner>();

            return services;
        }

        /// <summary>
        /// Adds a migration processor factory services
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="migrationProcessorFactoryType">The type of the migration processor factory</param>
        /// <param name="connectionString">The connection string used to connect to the database</param>
        /// <returns>The updated service collection</returns>
        internal static IServiceCollection ConfigureProcessorFactory(
            [NotNull] this IServiceCollection services,
            [NotNull] Type migrationProcessorFactoryType,
            [NotNull] string connectionString)
        {
            return services
                // Initialize the DB-specific processor
                .AddSingleton(typeof(IMigrationProcessorFactory), migrationProcessorFactoryType)
                .ConfigureProcessor(connectionString);
        }

        /// <summary>
        /// Adds all migration generators and let the default logic sort it out
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="generators">The generators to add</param>
        /// <returns>The updated service collection</returns>
        internal static IServiceCollection AddMigrationGenerators(
            [NotNull] this IServiceCollection services,
            [NotNull, ItemNotNull] IEnumerable<IMigrationGenerator> generators)
        {
            foreach (var generator in generators)
            {
                var type = generators.GetType();
                if (type.IsFluentMigratorRunnerType())
                {
                    services.AddSingleton(typeof(IMigrationGenerator), type);
                }
                else
                {
                    services.AddSingleton(generator);
                }
            }

            return services;
        }

        /// <summary>
        /// Adds all migration processor factories and let the default logic sort it out
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <param name="factories">The factories to add</param>
        /// <returns>The updated service collection</returns>
        internal static IServiceCollection AddMigrationProcessorFactories(
            [NotNull] this IServiceCollection services,
            [NotNull, ItemNotNull] IEnumerable<IMigrationProcessorFactory> factories)
        {
            foreach (var factory in factories)
            {
                var type = factory.GetType();
                if (type.IsFluentMigratorRunnerType())
                {
                    services.AddSingleton(typeof(IMigrationProcessorFactory), type);
                }
                else
                {
                    services.AddSingleton(factory);
                }
            }

            return services;
        }

        private static IMigrationProcessor CreateMigrationProcessor(IServiceProvider serviceProvider, string connectionString)
        {
            var processorFactories = serviceProvider.GetRequiredService<IEnumerable<IMigrationProcessorFactory>>().ToList();
            IMigrationProcessorFactory processorFactory;
            if (processorFactories.Count == 1)
            {
                processorFactory = processorFactories[0];
            }
            else
            {
                var runnerContext = serviceProvider.GetRequiredService<IRunnerContext>();
                processorFactory = processorFactories
                    .FirstOrDefault(f => string.Equals(f.Name, runnerContext.Database, StringComparison.OrdinalIgnoreCase));
                if (processorFactory == null)
                {
                    var choices = string.Join(", ", processorFactories.Select(x => x.Name));
                    throw new ProcessorFactoryNotFoundException(
                        $"The provider or dbtype parameter is incorrect. Available choices are: {choices}");
                }
            }

            var announcer = serviceProvider.GetRequiredService<IAnnouncer>();
            var options = serviceProvider.GetRequiredService<IMigrationProcessorOptions>();
            return processorFactory.Create(connectionString, announcer, options);
        }

        private class MigrationRunnerBuilder : IMigrationRunnerBuilder
        {
            public MigrationRunnerBuilder(IServiceCollection services)
            {
                Services = services;
            }

            /// <inheritdoc />
            public IServiceCollection Services { get; }
        }
    }
}