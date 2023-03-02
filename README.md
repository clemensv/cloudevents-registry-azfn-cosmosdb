# CloudEvents Registry Experimental prototype

This is a prototypical implementation of the work-in-progress CloudEvents
Registry specification as an Azure Functions application backed by Azure
CosmosDB.

This repo also holds a working draft of the OpenAPI spec matching the spec
draft, which can be found
[here](src/Azure.CloudEvents.Registry/specs/ce_registry.json).

The repo contains several projects, all in various state of completeness. The
"Azure" namespace prefix is just there to tell you that this particular
prototype comes from the Azure team and runs on Azure. The names of projects are
reflecting the evolution here and need a cleanup.

Main Projects:

* [Azure.CloudEvents.Registry.Service](src/Azure.CloudEvents.Registry.Service/):
  This project is an Azure Functions based implementation of the OpenAPI spec. I
  chose not to auto-generate that side of the interface, but to build that
  separately to retain flexibility to change things around. This might
  eventually morph into an ASP.NET MVC app.
* [Azure.CloudEvents.Registry](src/Azure.CloudEvents.Registry/): This project
  hosts the OpenAPI spec document and automatically generates a client from it,
  which you can find in the "generated" subdirectory as "RegistryClient.cs"
  once the project it built. That file is not checked in. The data objects
  (Groups, Schema, Definition, etc) generated into that file are used throughout
  the other projects.
* [Azure.CloudEvents.Subscriptions](src/Azure.CloudEvents.Subscriptions/): This
  project automatically generates a client from the CloudEvents Subscription API
  OpenAPI spec, which you can find in the "generated" subdirectory as
  "SubscriptionsClient.cs" once the project it built. That file is not checked in. 
* [CloudEventsRegistryCli](src/CloudEventsRegistryCli/): This is a CLI tool that can upload full metadata
  documents into the registry and also edit aspects of the registry. The upload
  works, the rest is being worked on.
* [AzureEventSubscriber](src/AzureEventSubscriber/): This is a CLI tool that
  uses the Azure Relay to create a subscriber endpoint on the local machine (!)
  and then subscribes that endpoint via the registry service endpoint's
  Subscription API to the subscriber endpoints available in the registry. The
  tool uses the local user's identity to obtain an Azure Management token and
  passes that token through the endpoint. Subscriptions on Azure Event Grid that
  are proxies through the Subscriptions API are created using this token.
* [AzureResourceImporter](src/AzureResourceImporter/): This is a CLI tool that can read all resources
  from an Azure subscription, find those with Event Grid system topics and then
  export those endpoints into a given registry. 

Utilities:
* [Azure.CloudEvents.Registry.SystemTopicLoader](src/Azure.CloudEvents.Registry.SystemTopicLoader):
  This project hosts a utility class that can extract Azure Event Grid system
  topic metadata from the Azure Resource Manager API.

The repo is set up with an automated deployment flow to a set of Azure resources.

The endpoint is https://ceregistryinterop.azurewebsites.net/registry and requires an access key.

The access key is only available for participants in the CNCF CloudEvents project.