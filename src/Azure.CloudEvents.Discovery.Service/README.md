# Azure Functions CloudEvents Registry

This is an Azure Functions implementation of a CloudEvents Registry and a
CloudEvent Subscription Manager that acts as a proxy to Azure Event Grid's
subscription management API.

The service depends on an Azure CosmosDB instance. The
["cosmosdb_armtemplate.json"](setup/cosmosdb_armtemplate.json) ARM template
allows setting up the required containers and partition keys.

In addition to the common configuration parameters, the following app settings
of the Azure Function deployment need to be configured:

  * `BLOB_ACCOUNT` : Account name of an Azure Blob account where schema documents will stored
  * `BLOB_ENDPOINT` :  Endpoint URI of the blob account
  * `BLOB_KEY`: Access key of the blob account
  * `COSMOSDB_ENDPOINT`: Endpoint of the CosmosDB collection where the data is stored
  * `COSMOSDB_KEY`:  Access key for the CosmosDB collection
  * `EVENTGRID_ENDPOINT`: The publishing endpoint of the Event Grid custom topic where state change events are raised to
  * `EVENTGRID_KEY`: The access key for the Event Grid topic