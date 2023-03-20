# xRegistry.Types.MessageDefinitionClient - the C# library for the xRegistry API

xRegistry API

This C# SDK is automatically generated by the [OpenAPI Generator](https://openapi-generator.tech) project:

- API version: 0.5-wip
- SDK version: 1.0.0
- Build package: org.openapitools.codegen.languages.CSharpNetCoreClientCodegen

<a name="frameworks-supported"></a>
## Frameworks supported

<a name="dependencies"></a>
## Dependencies

- [RestSharp](https://www.nuget.org/packages/RestSharp) - 106.13.0 or later
- [Json.NET](https://www.nuget.org/packages/Newtonsoft.Json/) - 13.0.2 or later
- [JsonSubTypes](https://www.nuget.org/packages/JsonSubTypes/) - 1.8.0 or later
- [System.ComponentModel.Annotations](https://www.nuget.org/packages/System.ComponentModel.Annotations) - 5.0.0 or later

The DLLs included in the package may not be the latest version. We recommend using [NuGet](https://docs.nuget.org/consume/installing-nuget) to obtain the latest version of the packages:
```
Install-Package RestSharp
Install-Package Newtonsoft.Json
Install-Package JsonSubTypes
Install-Package System.ComponentModel.Annotations
```

NOTE: RestSharp versions greater than 105.1.0 have a bug which causes file uploads to fail. See [RestSharp#742](https://github.com/restsharp/RestSharp/issues/742).
NOTE: RestSharp for .Net Core creates a new socket for each api call, which can lead to a socket exhaustion problem. See [RestSharp#1406](https://github.com/restsharp/RestSharp/issues/1406).

<a name="installation"></a>
## Installation
Run the following command to generate the DLL
- [Mac/Linux] `/bin/sh build.sh`
- [Windows] `build.bat`

Then include the DLL (under the `bin` folder) in the C# project, and use the namespaces:
```csharp
using xRegistry.Types.MessageDefinitionClient.Api;
using xRegistry.Types.MessageDefinitionClient.Client;
using xRegistry.Types.MessageDefinitionClient.Model;
```
<a name="packaging"></a>
## Packaging

A `.nuspec` is included with the project. You can follow the Nuget quickstart to [create](https://docs.microsoft.com/en-us/nuget/quickstart/create-and-publish-a-package#create-the-package) and [publish](https://docs.microsoft.com/en-us/nuget/quickstart/create-and-publish-a-package#publish-the-package) packages.

This `.nuspec` uses placeholders from the `.csproj`, so build the `.csproj` directly:

```
nuget pack -Build -OutputDirectory out xRegistry.Types.MessageDefinitionClient.csproj
```

Then, publish to a [local feed](https://docs.microsoft.com/en-us/nuget/hosting-packages/local-feeds) or [other host](https://docs.microsoft.com/en-us/nuget/hosting-packages/overview) and consume the new package via Nuget as usual.

<a name="usage"></a>
## Usage

To use the API client with a HTTP proxy, setup a `System.Net.WebProxy`
```csharp
Configuration c = new Configuration();
System.Net.WebProxy webProxy = new System.Net.WebProxy("http://myProxyUrl:80/");
webProxy.Credentials = System.Net.CredentialCache.DefaultCredentials;
c.Proxy = webProxy;
```

<a name="getting-started"></a>
## Getting Started

```csharp
using System.Collections.Generic;
using System.Diagnostics;
using xRegistry.Types.MessageDefinitionClient.Api;
using xRegistry.Types.MessageDefinitionClient.Client;
using xRegistry.Types.MessageDefinitionClient.Model;

namespace Example
{
    public class Example
    {
        public static void Main()
        {

            Configuration config = new Configuration();
            config.BasePath = "http://localhost";
            // Configure API key authorization: api_key
            config.ApiKey.Add("code", "YOUR_API_KEY");
            // Uncomment below to setup prefix (e.g. Bearer) for API key, if needed
            // config.ApiKeyPrefix.Add("code", "Bearer");

            var apiInstance = new DefaultApi(config);
            var groupid = "groupid_example";  // string | The id of the group
            var epoch = 789L;  // long? | The epoch of the schema group to be deleted (optional) 

            try
            {
                DefinitionGroup result = apiInstance.DeleteResourceGroup(groupid, epoch);
                Debug.WriteLine(result);
            }
            catch (ApiException e)
            {
                Debug.Print("Exception when calling DefaultApi.DeleteResourceGroup: " + e.Message );
                Debug.Print("Status Code: "+ e.ErrorCode);
                Debug.Print(e.StackTrace);
            }

        }
    }
}
```

<a name="documentation-for-api-endpoints"></a>
## Documentation for API Endpoints

All URIs are relative to *http://localhost*

Class | Method | HTTP request | Description
------------ | ------------- | ------------- | -------------
*DefaultApi* | [**DeleteResourceGroup**](docs\DefaultApi.md#deleteresourcegroup) | **DELETE** /definitiongroups/{groupid} | 
*DefaultApi* | [**DeleteResourceVersion**](docs\DefaultApi.md#deleteresourceversion) | **DELETE** /definitiongroups/{groupid}/definitions/{resourceid}/versions/{versionid} | 
*DefaultApi* | [**DeleteResources**](docs\DefaultApi.md#deleteresources) | **DELETE** /definitiongroups/{groupid}/definitions | 
*DefaultApi* | [**GetAll**](docs\DefaultApi.md#getall) | **GET** / | 
*DefaultApi* | [**GetLatestResource**](docs\DefaultApi.md#getlatestresource) | **GET** /definitiongroups/{groupid}/definitions/{resourceid} | 
*DefaultApi* | [**GetLatestResourceMetadata**](docs\DefaultApi.md#getlatestresourcemetadata) | **GET** /definitiongroups/{groupid}/definitions/{resourceid}/meta | 
*DefaultApi* | [**GetResourceGroup**](docs\DefaultApi.md#getresourcegroup) | **GET** /definitiongroups/{groupid} | 
*DefaultApi* | [**GetResourceGroupAll**](docs\DefaultApi.md#getresourcegroupall) | **GET** /definitiongroups | 
*DefaultApi* | [**GetResourceVersion**](docs\DefaultApi.md#getresourceversion) | **GET** /definitiongroups/{groupid}/definitions/{resourceid}/versions/{versionid} | 
*DefaultApi* | [**GetResourceVersionMetadata**](docs\DefaultApi.md#getresourceversionmetadata) | **GET** /definitiongroups/{groupid}/definitions/{resourceid}/versions/{versionid}/meta | 
*DefaultApi* | [**GetResourcesAll**](docs\DefaultApi.md#getresourcesall) | **GET** /definitiongroups/{groupid}/definitions | 
*DefaultApi* | [**PostGroup**](docs\DefaultApi.md#postgroup) | **POST** /definitiongroups | 
*DefaultApi* | [**PostResourceDocument**](docs\DefaultApi.md#postresourcedocument) | **POST** /definitiongroups/{groupid}/definitions/{resourceid} | Post new resource version
*DefaultApi* | [**PutResourceGroup**](docs\DefaultApi.md#putresourcegroup) | **PUT** /definitiongroups/{groupid} | 
*DefaultApi* | [**PutResources**](docs\DefaultApi.md#putresources) | **PUT** /definitiongroups/{groupid}/definitions | 
*DefaultApi* | [**UpdateLatestResourceVersionMetadata**](docs\DefaultApi.md#updatelatestresourceversionmetadata) | **PUT** /definitiongroups/{groupid}/definitions/{resourceid}/meta | 
*DefaultApi* | [**UpdateResourceVersionMetadata**](docs\DefaultApi.md#updateresourceversionmetadata) | **PUT** /definitiongroups/{groupid}/definitions/{resourceid}/versions/{versionid}/meta | 
*DefaultApi* | [**UploadDoc**](docs\DefaultApi.md#uploaddoc) | **POST** / | 


<a name="documentation-for-models"></a>
## Documentation for Models

 - [Model.AmqpDefinition](docs\AmqpDefinition.md)
 - [Model.AmqpHeader](docs\AmqpHeader.md)
 - [Model.AmqpMetadata](docs\AmqpMetadata.md)
 - [Model.AmqpProperties](docs\AmqpProperties.md)
 - [Model.CloudEventDefinition](docs\CloudEventDefinition.md)
 - [Model.CloudEventMetadata](docs\CloudEventMetadata.md)
 - [Model.CloudEventMetadataAttributes](docs\CloudEventMetadataAttributes.md)
 - [Model.Definition](docs\Definition.md)
 - [Model.DefinitionBase](docs\DefinitionBase.md)
 - [Model.DefinitionGroup](docs\DefinitionGroup.md)
 - [Model.DefinitionGroupAllOf](docs\DefinitionGroupAllOf.md)
 - [Model.DefinitionGroupBase](docs\DefinitionGroupBase.md)
 - [Model.HttpDefinition](docs\HttpDefinition.md)
 - [Model.HttpHeaderProperty](docs\HttpHeaderProperty.md)
 - [Model.HttpMetadata](docs\HttpMetadata.md)
 - [Model.KafkaDefinition](docs\KafkaDefinition.md)
 - [Model.KafkaMetadata](docs\KafkaMetadata.md)
 - [Model.MetadataProperty](docs\MetadataProperty.md)
 - [Model.MetadataPropertyBinary](docs\MetadataPropertyBinary.md)
 - [Model.MetadataPropertyBoolean](docs\MetadataPropertyBoolean.md)
 - [Model.MetadataPropertyInteger](docs\MetadataPropertyInteger.md)
 - [Model.MetadataPropertyString](docs\MetadataPropertyString.md)
 - [Model.MetadataPropertySymbol](docs\MetadataPropertySymbol.md)
 - [Model.MetadataPropertyTimeStamp](docs\MetadataPropertyTimeStamp.md)
 - [Model.MetadataPropertyUriTemplate](docs\MetadataPropertyUriTemplate.md)
 - [Model.MqttDefinition](docs\MqttDefinition.md)
 - [Model.MqttMetadata](docs\MqttMetadata.md)
 - [Model.MqttUserProperty](docs\MqttUserProperty.md)
 - [Model.MqttUserPropertyAllOf](docs\MqttUserPropertyAllOf.md)
 - [Model.Resource](docs\Resource.md)
 - [Model.ResourceTag](docs\ResourceTag.md)
 - [Model.Tag](docs\Tag.md)
 - [Model.XregistryMessagedefinitionDefinition](docs\XregistryMessagedefinitionDefinition.md)
 - [Model.XregistryMessagedefinitionRegistry](docs\XregistryMessagedefinitionRegistry.md)


<a name="documentation-for-authorization"></a>
## Documentation for Authorization

<a name="api_key"></a>
### api_key

- **Type**: API key
- **API key parameter name**: code
- **Location**: URL query string
