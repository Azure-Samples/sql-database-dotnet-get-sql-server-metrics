---
page_type: sample
languages:
- csharp
products:
- azure
extensions:
  services: Sql
  platforms: dotnet
---

# Getting started on getting SQL server metrics in C# #

 Azure SQL sample for getting SQL Server and Databases metrics
  - Create a primary SQL Server with a sample database.
  - Run some queries on the sample database.
  - Create a new table and insert some values into the database.
  - List the SQL subscription usage metrics, the database usage metrics and the other database metrics
  - Use the Monitor Service Fluent APIs to list the SQL Server metrics and the SQL Database metrics
  - Delete Sql Server


## Running this Sample ##

To run this sample:

Set the environment variable `AZURE_AUTH_LOCATION` with the full path for an auth file. See [how to create an auth file](https://github.com/Azure/azure-libraries-for-net/blob/master/AUTH.md).

    git clone https://github.com/Azure-Samples/sql-database-dotnet-get-sql-server-metrics.git

    cd sql-database-dotnet-get-sql-server-metrics

    dotnet build

    bin\Debug\net452\GettingSqlServerMetrics.exe

## More information ##

[Azure Management Libraries for C#](https://github.com/Azure/azure-sdk-for-net/tree/Fluent)
[Azure .Net Developer Center](https://azure.microsoft.com/en-us/develop/net/)
If you don't have a Microsoft Azure subscription you can get a FREE trial account [here](http://go.microsoft.com/fwlink/?LinkId=330212)

---

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.