using System;
using System.Collections.Generic;
using Wolfpack.Core.Interfaces.Entities;
using Wolfpack.Core.Interfaces.Magnum;
using Wolfpack.Core.Publishers;

namespace Wolfpack.Core.Database.SqlServer
{
    public class SqlServerPublisherBase : PublisherBase
    {
        protected readonly SqlServerConfiguration myConfig;

        public SqlServerPublisherBase(SqlServerConfiguration config)
        {
            myConfig = config;

            Enabled = config.Enabled;
            FriendlyId = config.FriendlyId;
        }

        public override void Initialise()
        {
            try
            {
                Logger.Debug("\tCreating {0} schema...", myConfig.SchemaName);
                using (var cmd = SqlServerAdhocCommand.UsingSmartConnection(myConfig.ConnectionString)
                    .WithSql(SqlServerStatement.Create("IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'{0}') SELECT CAST(1 as bit) ELSE Select CAST(0 as bit)", myConfig.SchemaName)))
                {
                    bool exists = (bool)cmd.ExecuteScalar();
                    if (exists)
                        using (var createCmd = SqlServerAdhocCommand.UsingSmartConnection(myConfig.ConnectionString)
                            .WithSql(SqlServerStatement.Create("CREATE SCHEMA {0}", myConfig.SchemaName)))
                        {
                            createCmd.ExecuteNonQuery();
                            Logger.Debug("\tDone");
                        }
                }

                Logger.Debug("\tCreating AgentData table...");
                using (var cmd = SqlServerAdhocCommand.UsingSmartConnection(myConfig.ConnectionString)
                    .WithSql(SqlServerStatement.Create("IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[{0}].[AgentData]') AND type in (N'U')) BEGIN", myConfig.SchemaName)
                    .Append("CREATE TABLE [{0}].[AgentData](", myConfig.SchemaName)
                    .Append("[TypeId] [uniqueidentifier] NOT NULL,")
                    .Append("[EventType] [varchar](20) COLLATE Latin1_General_CI_AS NOT NULL,")
                    .Append("[SiteId] [varchar](50) COLLATE Latin1_General_CI_AS NOT NULL,")
                    .Append("[AgentId] [varchar](50) COLLATE Latin1_General_CI_AS NOT NULL,")
                    .Append("[CheckId] [varchar](50) COLLATE Latin1_General_CI_AS NULL,")
                    .Append("[Result] [bit] NULL,")
                    .Append("[GeneratedOnUtc] [datetime] NOT NULL,")
                    .Append("[ReceivedOnUtc] [datetime] NOT NULL,")
                    .Append("[Data] [xml] NOT NULL,")
                    .Append("[Version] [uniqueidentifier] NOT NULL) END")))
                {
                    cmd.ExecuteNonQuery();
                    Logger.Debug("\tDone");
                }

                Logger.Debug("\tApplying schema updates (ResultCount) to AgentData table...");
                using (var cmd = SqlServerAdhocCommand.UsingSmartConnection(myConfig.ConnectionString)
                    .WithSql(SqlServerStatement.Create("IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AgentData' AND COLUMN_NAME = 'ResultCount') BEGIN")
                    .Append("ALTER TABLE {0}.AgentData ADD ResultCount DECIMAL(20,4) NULL", myConfig.SchemaName)
                    .Append("END ELSE BEGIN")
                    .Append("IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AgentData' AND COLUMN_NAME = 'ResultCount' AND DATA_TYPE = 'bigint') BEGIN")
                    .Append("ALTER TABLE {0}.AgentData ALTER COLUMN ResultCount DECIMAL(20,4)", myConfig.SchemaName)
                    .Append("END")
                    .Append("END")))
                {
                    cmd.ExecuteNonQuery();
                    Logger.Debug("\tDone");
                }

                Logger.Debug("\tApplying schema updates to AgentData table...");
                AddColumnIfMissing("Tags", "VARCHAR(200)", true);
                AddColumnIfMissing("MinuteBucket", "INT", true);
                AddColumnIfMissing("HourBucket", "INT", true);
                AddColumnIfMissing("DayBucket", "INT", true);
                // Geo - point
                AddColumnIfMissing("Latitude", "VARCHAR(12)", true);
                AddColumnIfMissing("Longitude", "VARCHAR(12)", true);

                Logger.Debug("\tSuccess, AgentData table established");
            }
            catch (Exception)
            {
                Logger.Debug("\tFailed to create AgentData table");
                throw;
            }
        }

        protected virtual TableSchema GetSchema(string table)
        {
            var columns = new List<TableSchema.ColumnDefinition>();

            using (var cmd = SqlServerAdhocCommand.UsingSmartConnection(myConfig.ConnectionString)
                .WithSql(SqlServerStatement.Create("SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{0}'", table)))
            {
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var def = new TableSchema.ColumnDefinition
                                      {
                                          Name = reader["COLUMN_NAME"].ToString(),
                                          Type = reader["DATA_TYPE"].ToString(),
                                          IsNullable =
                                              (string.Compare(reader["IS_NULLABLE"].ToString(), "yes", true) == 0)
                                      };
                        columns.Add(def);
                    }
                }
            }

            return new TableSchema
            {
                Columns = columns.AsReadOnly()
            };
        }

        protected virtual bool AddColumnIfMissing(string column, string datatype, bool nullable)
        {
            if (GetSchema("AgentData").HasColumn(column))
                return false;

            using (var cmd = SqlServerAdhocCommand.UsingSmartConnection(myConfig.ConnectionString)
                .WithSql(SqlServerStatement.Create("ALTER TABLE [{3}].[AgentData] ADD {0} {1} {2} NULL",
                    column,
                    datatype,
                    nullable ? string.Empty : "NOT",
                    myConfig.SchemaName)))
            {
                cmd.ExecuteNonQuery();
                Logger.Debug("\tAdded column {0} [{1}]", column, datatype);
            }
            return true;
        }
    }

    //public class SqlHealthCheckSessionPublisher : SqlServerPublisherBase, IHealthCheckSessionPublisher
    //{
    //    public SqlHealthCheckSessionPublisher(SqlServerConfiguration config)
    //        : base(config)
    //    {
    //    }

    //    public void Publish(NotificationEventAgentStart message)
    //    {
    //        var data = Serialiser<NotificationEventAgentStart>.ToXml(message);

    //        using (var cmd = SqlServerAdhocCommand.UsingSmartConnection(myConfig.ConnectionString)
    //            .WithSql(SqlServerStatement.Create("INSERT INTO {0}.AgentData (", myConfig.SchemaName)
    //            .Append("TypeId,EventType,SiteId,AgentId,GeneratedOnUtc,ReceivedOnUtc,Data,Version")
    //            .Append(") VALUES (")
    //            .InsertParameter("@pTypeId", message.Id).Append(",")
    //            .InsertParameter("@pEventType", "SessionStart").Append(",")
    //            .InsertParameter("@pSiteId", message.Agent.SiteId).Append(",")
    //            .InsertParameter("@pAgentId", message.Agent.AgentId).Append(",")
    //            .InsertParameter("@pGeneratedOnUtc", message.GeneratedOnUtc).Append(",")
    //            .InsertParameter("@pReceivedOnUtc", DateTime.UtcNow).Append(",")
    //            .InsertParameter("@pData", data).Append(",")
    //            .InsertParameter("@pVersion", message.Id)
    //            .Append(")")))
    //        {
    //            cmd.ExecuteNonQuery();
    //        }
    //    }

    //    public void Consume(NotificationEventAgentStart message)
    //    {
    //        Publish(message);
    //    }
    //}

    public class SqlNotificationEventPublisher : SqlServerPublisherBase, INotificationEventPublisher
    {
        public SqlNotificationEventPublisher(SqlServerConfiguration config)
            : base(config)
        {
        }

        public void Publish(NotificationEvent message)
        {
            throw new NotImplementedException();
            //var data = Serialiser<NotificationEventHealthCheck>.ToXml(message);

            //var statement = SqlServerStatement.Create("INSERT INTO {0}.AgentData (", myConfig.SchemaName)
            //    .Append("TypeId,EventType,SiteId,AgentId,CheckId,")
            //    .AppendIf(() => message.Check.Result.HasValue, "Result,")
            //    .AppendIf(() => message.Check.ResultCount.HasValue, "ResultCount,")
            //    .AppendIf(() => !string.IsNullOrEmpty(message.Check.Tags), "Tags,")
            //    .Append("GeneratedOnUtc,ReceivedOnUtc,Data,")
            //    .AppendIf(() => message.MinuteBucket.HasValue, "MinuteBucket,")
            //    .AppendIf(() => message.HourBucket.HasValue, "HourBucket,")
            //    .AppendIf(() => message.DayBucket.HasValue, "DayBucket,")
            //    .Append("Version")
            //    .AppendIf(() => (message.Check.Geo != null), ",Longitude,Latitude")
            //    .Append(") VALUES (")
            //    .InsertParameter("@pTypeId", message.Check.Identity.TypeId).Append(",")
            //    .InsertParameter("@pEventType", message.EventType).Append(",")
            //    .InsertParameter("@pSiteId", message.Agent.SiteId).Append(",")
            //    .InsertParameter("@pAgentId", message.Agent.AgentId).Append(",")
            //    .InsertParameter("@pCheckId", message.Check.Identity.Name).Append(",")
            //    .InsertParameterIf(() => message.Check.Result.HasValue, "@pResult", message.Check.Result)
            //    .AppendIf(() => message.Check.Result.HasValue, ",")
            //    .InsertParameterIf(() => message.Check.ResultCount.HasValue, "@pResultCount", message.Check.ResultCount)
            //    .AppendIf(() => message.Check.ResultCount.HasValue, ",")
            //    .InsertParameterIf(() => !string.IsNullOrEmpty(message.Check.Tags), "@pTags", message.Check.Tags)
            //    .AppendIf(() => !string.IsNullOrEmpty(message.Check.Tags), ",")
            //    .InsertParameter("@pGeneratedOnUtc", message.Check.GeneratedOnUtc).Append(",")
            //    .InsertParameter("@pReceivedOnUtc", DateTime.UtcNow).Append(",")
            //    .InsertParameter("@pData", data).Append(",")
            //    .InsertParameterIf(() => message.MinuteBucket.HasValue, "@pMinuteBucket", message.MinuteBucket)
            //    .AppendIf(() => message.MinuteBucket.HasValue, ",")
            //    .InsertParameterIf(() => message.HourBucket.HasValue, "@pHourBucket", message.HourBucket)
            //    .AppendIf(() => message.HourBucket.HasValue, ",")
            //    .InsertParameterIf(() => message.DayBucket.HasValue, "@pDayBucket", message.DayBucket)
            //    .AppendIf(() => message.DayBucket.HasValue, ",")
            //    .InsertParameter("@pVersion", message.Id);

            //if (message.Check.Geo != null)
            //{
            //    statement.Append(",")
            //        .InsertParameter("@pLongitude", message.Check.Geo.Longitude).Append(",")
            //        .InsertParameter("@pLatitude", message.Check.Geo.Latitude);
            //}
            //statement.Append(")");

            //using (var cmd = SqlServerAdhocCommand.UsingSmartConnection(myConfig.ConnectionString)
            //    .WithSql(statement))
            //{
            //    cmd.ExecuteNonQuery();
            //}
        }

        public void Consume(NotificationEvent message)
        {
            Publish(message);
        }
    }
}