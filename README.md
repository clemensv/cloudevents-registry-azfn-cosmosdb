# CloudEvents Discovery Experimental prototype

This is a prototypical implementation of the work-in-progress CloudEvents
Discovery specification as an Azure Functions application backed by Azure
CosmosDB.

This repo also holds a working draft of the OpenAPI spec matching the spec
draft, which can be found
[here](src/Azure.CloudEvents.Discovery/specs/ce_discovery.json).

The repo contains several projects, all in various state of completeness. The
"Azure" namespace prefix is just there to tell you that this particular
prototype comes from the Azure team and runs on Azure. The names of projects are
reflecting the evolution here and need a cleanup.

Main Projects:

* [Azure.CloudEvents.Discovery](src/Azure.CloudEvents.Discovery/): This project
  hosts the OpenAPI spec document and automatically generates a client from it,
  which you can find in the "generated" subdirectory as "DiscoveryClient.cs"
  once the project it built. That file is not checked in. The data objects
  (Groups, Schema, Definition, etc) generated into that file are used throughout
  the other projects.
* [Azure.CloudEvents.Discovery.Service](src/Azure.CloudEvents.Discovery.Service/):
  This project is an Azure Functions based implementation of the OpenAPI spec. I
  chose not to auto-generate that side of the interface, but to build that
  separately to retain flexibility to change things around. This might
  eventually morph into an ASP.NET MVC app.
* [ceregistry](src/azcedisco/): This is a CLI tool that can upload full metadata
  documents into the registry and also edit aspects of the registry. The upload
  works, the rest is being worked on.
* [ce-disco](src/azcedisco/): This is a CLI tool that can read all resources
  from an Azure subscription, find those with Event Grid system topics and then
  export those endpoints into a given registry. 

Utilities:
* [Azure.CloudEvents.Discovery.SystemTopicLoader](src/Azure.CloudEvents.Discovery.SystemTopicLoader):
  This project hosts a utility class that can extract Azure Event Grid system
  topic metadata from the Azure Resource Manager API.
* [Azure.CloudEvents.EventGridBridge](src/Azure.CloudEvents.EventGridBridge):
  This is another Azure Function project that implements the Subscription API as
  a proxy to Azure Event Grid's subscription API. 
* [Azure.CloudEvents.SchemaRegistry](src/Azure.CloudEvents.SchemaRegistry/):
  This project automatically generates a client for the old draft of the Schema
  Registry.
