using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using CQRSlite.Bus;
using CQRSlite.Commands;
using CQRSlite.Events;
using CQRSlite.Domain;
using CQRSCode.WriteModel;
using CQRSlite.Cache;
using CQRSCode.ReadModel;
using CQRSCode.WriteModel.Handlers;
using System.Reflection;
using System.Linq;
using CQRSlite.Config;


namespace CQRSWeb
{
    public class Startup
    {
        public IContainer ApplicationContainer { get; private set; }
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddMemoryCache();
            var builder = new ContainerBuilder();

            services.AddSingleton<InProcessBus>(new InProcessBus());
            builder.Register<ICommandSender>(c => c.Resolve<InProcessBus>()).SingleInstance();
            builder.Register<IEventPublisher>(c => c.Resolve<InProcessBus>()).SingleInstance();
            builder.Register<IHandlerRegistrar>(c => c.Resolve<InProcessBus>());
            builder.RegisterType<Session>().As<ISession>().InstancePerLifetimeScope();
            builder.RegisterType<InMemoryEventStore>().As<IEventStore>().SingleInstance();
            builder.RegisterType<MemoryCache>().As<ICache>().InstancePerLifetimeScope();
            builder.Register<IRepository>(x =>
                new CacheRepository(new Repository(x.Resolve<IEventStore>()),
                x.Resolve<IEventStore>(), x.Resolve<ICache>())).InstancePerLifetimeScope();
            builder.RegisterType<ReadModelFacade>().As<IReadModelFacade>().InstancePerDependency();
            var targetAssembly = typeof(InventoryCommandHandlers).GetTypeInfo().Assembly;

            builder.RegisterAssemblyTypes(targetAssembly)
                .As(type => type.GetInterfaces()
                    .Where(interfacetype => interfacetype.IsAssignableFrom(typeof(ICommandHandler<>))
                            || interfacetype.IsAssignableFrom(typeof(IEventHandler<>))))
                     .AsSelf()
                .InstancePerDependency();

            builder.Populate(services);
            this.ApplicationContainer = builder.Build();

            var autofacServiceProvider = new AutofacServiceProvider(ApplicationContainer);
            var registrar = new BusRegistrar(new DependencyResolver(autofacServiceProvider));
            registrar.Register(typeof(InventoryCommandHandlers));


            return autofacServiceProvider;
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            app.UseDeveloperExceptionPage();
            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}