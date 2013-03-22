using System;
using System.Linq;
using Wolfpack.Core;
using Wolfpack.Core.Hosts;
using Wolfpack.Core.Interfaces;
using Wolfpack.Core.Interfaces.Entities;
using Wolfpack.Core.Interfaces.Magnum;
using Wolfpack.Core.Notification;

namespace Wolfpack.Agent.Roles
{
    public class Agent : PluginHostBase, IRolePlugin
    {
        protected readonly ILoader<INotificationEventPublisher> _publisherLoader;
        protected readonly ILoader<IHealthCheckSchedulerPlugin> _checksLoader;
        protected readonly ILoader<IActivityPlugin> _activitiesLoader;
        
        protected readonly INotificationHub _hub;
        protected PluginDescriptor _identity;

        protected AgentInfo _agentInfo;

        public Agent(AgentConfiguration config,
            ILoader<INotificationEventPublisher> publisherLoader,
            ILoader<IHealthCheckSchedulerPlugin> checksLoader,
            ILoader<IActivityPlugin> activitiesLoader,
            INotificationHub hub)
        {
            _hub = hub;
            _publisherLoader = publisherLoader;
            _checksLoader = checksLoader;
            _activitiesLoader = activitiesLoader;
            _agentInfo = config.BuildInfo();            

            _identity = new PluginDescriptor
                             {
                                 Description = "Agent description [TODO]",
                                 Name = "Agent",
                                 TypeId = new Guid("649D0AAC-3AA0-4457-B82D-F834EA324CFA")
                             };
        }

        public override PluginDescriptor Identity
        {
            get { return _identity; }
        }

        public override void Start()
        {
            // start the load process....
            var sessionInfo = new NotificationEventAgentStart
            {
                DiscoveryStarted = DateTime.UtcNow,
                AgentId = _agentInfo.AgentId,
                SiteId =  _agentInfo.SiteId
            };

            INotificationEventPublisher[] publishers;
            _publisherLoader.Load(out publishers,
                                        p =>
                                            {
                                                if (!p.Status.IsHealthy())
                                                {
                                                    Logger.Debug(
                                                        "*** Notification Publisher '{0}' reporting 'unhealthy', disabling it ***",
                                                        p.FriendlyId);
                                                    return;
                                                }

                                                Messenger.Subscribe(p);
                                                Logger.Debug("Loaded Notification Publisher '{0}'",
                                                             p.GetType().Name);
                                            });
            
            // load activities...
            IActivityPlugin[] activities;
            if (_activitiesLoader.Load(out activities))
                activities.ToList().ForEach(
                    a =>
                        {
                            if (!a.Status.IsHealthy())
                            {
                                Logger.Debug("*** Activity '{0}' reporting 'unhealthy', skipping it ***",
                                             a.Identity.Name);
                                return;
                            }

                            _plugins.Add(a);
                            Logger.Debug("Loaded Activity '{0}'", a.GetType().Name);
                        });

            // load health checks...
            IHealthCheckSchedulerPlugin[] healthChecks;
            _checksLoader.Load(out healthChecks);
            healthChecks.ToList().ForEach(
                h =>
                    {
                        if (!h.Status.IsHealthy())
                        {
                            Logger.Debug("*** HealthCheck '{0}' reporting 'unhealthy', skipping it ***", h.Identity.Name);
                            return;
                        }

                        _plugins.Add(h);
                        Logger.Debug("Loaded HealthCheck '{0}'", h.Identity.Name);
                    });

            // extract check info, attach and publish it to a session message
            sessionInfo.DiscoveryCompleted = DateTime.UtcNow;
            sessionInfo.Checks = (from healthCheck in healthChecks
                                  where healthCheck.Status.IsHealthy()
                                  select healthCheck.Identity).ToList();
            sessionInfo.UnhealthyChecks = (from healthCheck in healthChecks
                                  where !healthCheck.Status.IsHealthy()
                                  select healthCheck.Identity).ToList();
            sessionInfo.Activities = (from activity in activities
                                      where activity.Status.IsHealthy()
                                      select activity.Identity).ToList();
            sessionInfo.UnhealthyActivities = (from activity in activities
                                      where !activity.Status.IsHealthy()
                                      select activity.Identity).ToList();

            // ensure we are listening for anything to be published
            _hub.Initialise(_agentInfo);

            Messenger.Publish(NotificationRequestBuilder.AlwaysPublish(sessionInfo).Build());

            // finally start the checks & activities
            base.Start();
        }
    }
}