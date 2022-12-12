# Azure Resource Importer

`azureresourceimporter` is a command-line tool that accesses an Azure resource group and find resources that raise Event Grid events. The tool uses the ambient security context of the AAD login for authentication, so users do not have to worry about providing their own credentials. Once the relevant resources are found, azureresourceimporter puts the metadata into a cloudevents registry.


## Usage
To use azureresourceimporter, run the following command:

```
azureresourceimporter [options]
```

## Options

To use the tool, you need to specify the following options:

-s|--subscription-id <SUBSCRIPTION_ID>: This is the Azure subscription id (in the form of a Guid) that you want to access.

-r|--resource-group-name <RESOURCE_GROUP_NAME>: This is the name of the Azure resource group that you want to access.

-e|--discovery-endpoint <DISCOVERY_ENDPOINT>: This is the discovery endpoint that the tool will use to post the discovered metadata.

-f|--functions-key <FUNCTIONS_KEY>: This is the FunctionsKey that the tool will use to access the discovery endpoint.

The -?|-h|--help option can be used to show help information for the tool.

