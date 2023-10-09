// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.Samples.Common;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.Sql;
using Azure.ResourceManager.Sql.Models;
using Azure.ResourceManager.Monitor;
using Azure.ResourceManager.Monitor.Models;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Collections;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;

namespace GettingSqlServerMetrics
{
    public class Program
    {
        private static ResourceIdentifier? _resourceGroupId = null;
        /**
         * Azure SQL sample for getting SQL Server and Databases metrics
         *  - Create a primary SQL Server with a sample database.
         *  - Run some queries on the sample database.
         *  - Create a new table and insert some values into the database.
         *  - List the SQL subscription usage metrics, the database usage metrics and the other database metrics
         *  - Use the Monitor Service Fluent APIs to list the SQL Server metrics and the SQL Database metrics
         *  - Delete Sql Server
         */
        public static async Task RunSample(ArmClient client)
        {
            DateTime startTime = DateTime.Now.ToUniversalTime().Subtract(new TimeSpan(1, 0, 0, 0));
            try
            {
                //Get default subscription
                SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();

                //Create a resource group in the EastUs region
                string rgName = Utilities.CreateRandomName("rgSQLServer");
                Utilities.Log("creating resource group...");
                ArmOperation<ResourceGroupResource> rgLro = await subscription.GetResourceGroups().CreateOrUpdateAsync(WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.EastUS));
                ResourceGroupResource resourceGroup = rgLro.Value;
                _resourceGroupId = resourceGroup.Id;
                Utilities.Log("created a resource group with name: " + resourceGroup.Data.Name);

                // ============================================================
                // Create a SQL Server with one database from a sample.

                Utilities.Log("creating SQL Server ...");
                string sqlServerName = Utilities.CreateRandomName("sqlserver");
                string sqlAdmin = "sqladmin1234";
                string sqlAdminPwd = Utilities.CreatePassword();
                SqlServerData sqlData = new SqlServerData(AzureLocation.EastUS)
                {
                    AdministratorLogin = sqlAdmin,
                    AdministratorLoginPassword = sqlAdminPwd,
                    Identity = new ManagedServiceIdentity(ManagedServiceIdentityType.SystemAssigned)
                };
                var sqlServer = (await resourceGroup.GetSqlServers().CreateOrUpdateAsync(WaitUntil.Completed, sqlServerName, sqlData)).Value;
                Utilities.Log("created SQL Server: " + sqlServer.Data.Name);

                Utilities.Log("Creating a range ipaddress firewall rule...");
                string testFirewallRuleName = Utilities.CreateRandomName("allowAll");
                var testFirewallRuleData = new SqlFirewallRuleData()
                {
                    StartIPAddress = "0.0.0.1",
                    EndIPAddress = "255.255.255.255"
                };
                var testFirewallRule = (await sqlServer.GetSqlFirewallRules().CreateOrUpdateAsync(WaitUntil.Completed, testFirewallRuleName, testFirewallRuleData)).Value;
                Utilities.Log($"Created a range ipaddress firewall rule with name: {testFirewallRule.Data.Name}");

                Utilities.Log("Creating a elastic pool of SQL Server...");
                string epName = Utilities.CreateRandomName("epSample");
                var epData = new ElasticPoolData(AzureLocation.EastUS)
                {
                    Sku = new SqlSku("StandardPool")
                };
                var ep = (await sqlServer.GetElasticPools().CreateOrUpdateAsync(WaitUntil.Completed,epName,epData)).Value;
                Utilities.Log($"Created a elastic pool of SQL Server with name {ep.Data.Name}");

                Utilities.Log("creating SQL Server Database ...");
                string DBName = Utilities.CreateRandomName("dbSample");
                SqlDatabaseData DBData = new SqlDatabaseData(AzureLocation.EastUS)
                {
                    ElasticPoolId = ep.Id
                };
                var sqlDBLro = await sqlServer.GetSqlDatabases().CreateOrUpdateAsync(WaitUntil.Completed, DBName, DBData);
                SqlDatabaseResource sqlDB = sqlDBLro.Value;
                Utilities.Log($"created SQl Server Database with name: {sqlDB.Data.Name}");
                var SQLServerConnectionString = $"user id={sqlAdmin};" +
                                       $"password={sqlAdminPwd};" +
                                       $"server={sqlServer.Data.FullyQualifiedDomainName};" +
                                       $"database={sqlDB.Data.Name}; " +
                                       "Trusted_Connection=False;" +
                                       "Encrypt=True;" +
                                       "connection timeout=30";

                // ============================================================
                // Create a connection to the SQL Server.

                Utilities.Log("creating connection to SQL Server ...");
                using (SqlConnection sqlConnection = new SqlConnection(SQLServerConnectionString))
                {
                    // ============================================================
                    // Create and execute a "select" SQL statement on the sample database.
                    try
                    {
                        sqlConnection.Open();
                        SqlDataReader myReader = null;
                        Utilities.Log("Creating a new table with name SalesLT.Customer...");
                        string sqlCreateTableCommand = "CREATE TABLE [SalesLT.Customer] ([Title] [varchar](30) NOT NULL , [FirstName] [varchar](30) NOT NULL , [LastName] [varchar](30) NOT NULL, [Name] [varchar](30) NOT NULL , [ProductNumber] [varchar](30) NOT NULL, [Color] [varchar](30) NOT NULL , [StandardCost] [varchar](30) NOT NULL, [ListPrice] [varchar](30) NOT NULL, [SellStartDate] [varchar](30) NOT NULL )";
                        SqlCommand createTable = new SqlCommand(sqlCreateTableCommand, sqlConnection);
                        createTable.ExecuteNonQuery();
                        SqlCommand myCommand = new SqlCommand("SELECT TOP 10 Title, FirstName, LastName from [dbo].[SalesLT.Customer] ", sqlConnection);
                        Utilities.Log("execute a \"select\" SQL statement on database");
                        myReader = myCommand.ExecuteReader();  
                        while (myReader.Read())
                        {
                            Utilities.Log(myReader["Title"].ToString() + " " + myReader["FirstName"].ToString() + " " + myReader["LastName"].ToString());
                        }
                        myReader.Close();

                        // ============================================================
                        // Create and execute an "INSERT" SQL statement on the sample database.
                        string insertSqlSample = "INSERT INTO [dbo].[SalesLT.Customer] (Title, FirstName, LastName, Name, ProductNumber, Color, StandardCost, ListPrice, SellStartDate) VALUES "
                            + "('dbtest', 'sample', '1', 'Bike', 'B1', 'Blue', 50, 120, '2016-01-01');";

                        SqlCommand prepsInsertProduct = new SqlCommand(insertSqlSample, sqlConnection);
                        prepsInsertProduct.ExecuteNonQuery();

                        // ============================================================
                        // Create a new table into the SQL Server database and insert one value.
                        Utilities.Log("Creating a new table into the SQL Server database and insert one value...");
                        sqlCreateTableCommand = "CREATE TABLE [Sample_Test] ([Name] [varchar](30) NOT NULL)";
                        createTable = new SqlCommand(sqlCreateTableCommand, sqlConnection);
                        createTable.ExecuteNonQuery();
                        string sqlInsertCommand = "INSERT INTO [dbo].[Sample_Test] (Name) VALUES ('Test')";
                        SqlCommand insertValue = new SqlCommand(sqlInsertCommand, sqlConnection);
                        insertValue.ExecuteNonQuery();

                        // ============================================================
                        // Run a "select" query for the new table.
                        Utilities.Log("Running a \"SELECT\" query for the new table...");

                        string sqlSelectNewTableCommand = "SELECT * FROM [dbo].[Sample_Test];";
                        SqlCommand selectCommand = new SqlCommand(sqlSelectNewTableCommand, sqlConnection);
                        myReader = selectCommand.ExecuteReader();
                        while (myReader.Read())
                        {
                            Utilities.Log(myReader["Name"].ToString());
                        }

                        // ============================================================
                        // List the SQL subscription usage metrics for the current selected region.
                        Utilities.Log("Listing the SQL subscription usage metrics for the current selected region...");
                        var subscriptionUsageMetrics = subscription.GetSubscriptionUsages(AzureLocation.EastUS);
                        foreach (var usageMetric in subscriptionUsageMetrics)
                        {
                            Utilities.Log($"Listing the SQL subscription usage metrics with name : {usageMetric.Data.Name}");
                        }

                        // ============================================================
                        // List the SQL database usage metrics for the sample database.
                        Utilities.Log("Listing the SQL database usage metrics for the sample database...");
                        var databaseUsageMetrics = await sqlDB.GetDatabaseUsagesAsync().ToEnumerableAsync();
                        foreach (var usageMetric in databaseUsageMetrics)
                        {
                            Utilities.Log($"Listing the SQL database usage metrics with name: {usageMetric.Name}");
                        }

                        // ============================================================
                        // List the SQL database CPU metrics for the sample database.
                        Utilities.Log("Listing the SQL database CPU metrics for the sample database...");

                        DateTime endTime = DateTime.Now.ToUniversalTime();
                        string filter = $"name/value eq 'cpu_percent' and startTime eq '{startTime}' and endTime eq '{endTime}'";
                        var dbMetrics = await sqlDB.GetMetricsAsync(filter).ToEnumerableAsync();

                        foreach (var metric in dbMetrics)
                        {
                            Utilities.Log($"Listing the SQL database CPU metrics with name: {metric.Name.Value}");
                        }

                        // ============================================================
                        // List the SQL database metrics for the sample database.
                        Utilities.Log("Listing the SQL database metrics for the sample database...");
                        filter = $"startTime eq '{startTime}' and endTime eq '{endTime}'";
                        dbMetrics = await sqlDB.GetMetricsAsync(filter).ToEnumerableAsync();

                        foreach (var metric in dbMetrics)
                        {
                            Utilities.Log($"Listing the SQL database metrics with name: {metric.Name.Value}");
                        }

                        // ============================================================
                        // Use Monitor Service to list the SQL server metrics.
                        Utilities.Log("Using Monitor Service to list the SQL server metrics");
                        var metricClient = new MetricsQueryClient(new DefaultAzureCredential());
                        var elasticPool = (await sqlServer.GetElasticPoolAsync(ep.Data.Name)).Value;
                        var metricDefinitions = await client.GetMonitorMetricDefinitionsAsync(sqlServer.Id).ToEnumerableAsync();
                        foreach (var metricDefinition in metricDefinitions)
                        {
                            // find metric definition for "DTU used" and "Storage used"
                            if (metricDefinition.Name.LocalizedValue.Equals("dtu used", StringComparison.OrdinalIgnoreCase)
                                || metricDefinition.Name.LocalizedValue.Equals("storage used", StringComparison.OrdinalIgnoreCase))
                            {
                                // get metric records
                                var metricRecords = new MetricsQueryOptions()
                                {
                                    TimeRange = new QueryTimeRange(startTime, endTime),
                                    Granularity = TimeSpan.FromMinutes(5),
                                    Aggregations =
                                    {
                                        MetricAggregationType.Average
                                    },
                                    Filter = $"ElasticPoolResourceId eq '{elasticPool.Id}'"
                                };
                                MetricsQueryResult metricCollection = (await metricClient.QueryResourceAsync(sqlServer.Data.Id, new[] { metricDefinition.Name.Value }, metricRecords)).Value;

                                Utilities.Log($"SQL server \"{sqlServer.Data.Name}\" {metricDefinition.Name.LocalizedValue} metrics\n");
                                Utilities.Log("\tNamespacse: " + metricCollection.Namespace);
                                Utilities.Log("\tQuery time: " + metricCollection.TimeSpan);
                                Utilities.Log("\tTime Grain: " + metricCollection.Granularity);
                                Utilities.Log("\tCost: " + metricCollection.Cost);

                                foreach (var metric in metricCollection.Metrics)
                                {
                                    Utilities.Log("\tMetric: " + metric.Name);
                                    Utilities.Log("\tType: " + metric.ResourceType);
                                    Utilities.Log("\tUnit: " + metric.Unit);
                                    Utilities.Log("\tTime Series: ");
                                    foreach (var timeElement in metric.TimeSeries)
                                    {
                                        Utilities.Log("\t\tMetadata: ");
                                        foreach (var metadata in timeElement.Metadata)
                                        {
                                            Utilities.Log("\t\t\t" + metadata.Key + ": " + metadata.Value);
                                        }
                                        Utilities.Log("\t\tData: ");
                                        foreach (var data in timeElement.Values)
                                        {
                                            Utilities.Log("\t\t\t" + data.TimeStamp
                                                    + " : (Min) " + data.Minimum
                                                    + " : (Max) " + data.Maximum
                                                    + " : (Avg) " + data.Average
                                                    + " : (Total) " + data.Total
                                                    + " : (Count) " + data.Count);
                                        }
                                    }
                                }
                            }
                        }

                        // ============================================================
                        // Use Monitor Service to list the SQL Database metrics.
                        Utilities.Log("Using Monitor Service to list the SQL Database metrics");
                        var dbMetricClient = new MetricsQueryClient(new DefaultAzureCredential());
                        var dataBasemetricDefinitions =  await client.GetMonitorMetricDefinitionsAsync(sqlDB.Id).ToEnumerableAsync();

                        foreach (var metricDefinition in dataBasemetricDefinitions)
                        {
                            // find metric definition for "dtu used", "cpu used" and "storage"
                            if (metricDefinition.Name.LocalizedValue.Equals("dtu_used", StringComparison.OrdinalIgnoreCase)
                                || metricDefinition.Name.LocalizedValue.Equals("cpu_used", StringComparison.OrdinalIgnoreCase)
                                || metricDefinition.Name.LocalizedValue.Equals("storage_used", StringComparison.OrdinalIgnoreCase))
                            {
                                
                                // get metric records
                                var metricRecords = new MetricsQueryOptions()
                                {
                                    TimeRange = new QueryTimeRange(startTime, endTime),
                                };
                                MetricsQueryResult metricCollection = (await dbMetricClient.QueryResourceAsync(sqlDB.Data.Id, new[] { metricDefinition.Name.Value }, metricRecords)).Value;
                                Utilities.Log("Metrics for '" + sqlDB.Id + "':");
                                Utilities.Log("\tNamespacse: " + metricCollection.Namespace);
                                Utilities.Log("\tQuery time: " + metricCollection.TimeSpan);
                                Utilities.Log("\tTime Grain: " + metricCollection.Granularity);
                                Utilities.Log("\tCost: " + metricCollection.Cost);

                                foreach (var metric in metricCollection.Metrics)
                                {
                                    Utilities.Log("\tMetric: " + metric.Name);
                                    Utilities.Log("\tType: " + metric.ResourceType);
                                    Utilities.Log("\tUnit: " + metric.Unit);
                                    Utilities.Log("\tTime Series: ");
                                    foreach (var timeElement in metric.TimeSeries)
                                    {
                                        Utilities.Log("\t\tMetadata: ");
                                        foreach (var metadata in timeElement.Metadata)
                                        {
                                            Utilities.Log("\t\t\t" + metadata.Key + ": " + metadata.Value);
                                        }
                                        Utilities.Log("\t\tData: ");
                                        foreach (var data in timeElement.Values)
                                        {
                                            Utilities.Log("\t\t\t" + data.TimeStamp
                                                    + " : (Min) " + data.Minimum
                                                    + " : (Max) " + data.Maximum
                                                    + " : (Avg) " + data.Average
                                                    + " : (Total) " + data.Total
                                                    + " : (Count) " + data.Count);
                                        }
                                    }
                                }
                            }
                        }

                        sqlConnection.Close();
                    }
                    catch (Exception e)
                    {
                        Utilities.Log(e.ToString());
                    }
                }

                // Delete the SQL Server.
                Utilities.Log("Deleting a Sql Server");
                await sqlServer.DeleteAsync(WaitUntil.Completed);
            }
            finally
            {
                try
                {
                    if(_resourceGroupId is not null)
                    {
                        Utilities.Log($"Deleting Resource Group...");
                        await client.GetResourceGroupResource(_resourceGroupId).DeleteAsync(WaitUntil.Completed);
                        Utilities.Log($"Deleted Resource Group: {_resourceGroupId.Name}");
                    }
                }
                catch (Exception e)
                {
                    Utilities.Log(e);
                }
            }
        }
        public static async Task Main(string[] args)
        {
            try
            {
                //=================================================================
                // Authenticate

                var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
                var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
                var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
                ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                ArmClient client = new ArmClient(credential, subscription);

                await RunSample(client);
            }
            catch (Exception e)
            {
                Utilities.Log(e);
            }
        }
    }
}