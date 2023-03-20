using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using Newtonsoft.Json;
using System;
using System.Net.Mime;
using System.Text;

namespace xRegistry.Server.Services
{
    public class RegistryControllerImpl : IRegistryController
    {
        IRegistryDataProvider registryDataProvider;

        public RegistryControllerImpl(IRegistryDataProvider registryDataProvider)
        {
            this.registryDataProvider = registryDataProvider;
        }

        public async Task<IActionResult> DeleteResourceAsync(long? epoch, string groupType, string groupId, string id, string resourceId, bool? meta)
        {
            await registryDataProvider.DeleteResourceAsync(epoch, groupType, groupId, id, resourceId);
            return new NoContentResult();
        }

        public async Task<IActionResult> DeleteResourceGroupAsync(long? epoch, string groupType, string groupid)
        {
            await registryDataProvider.DeleteResourceGroupAsync(epoch, groupType, groupid);
            return new NoContentResult();
        }

        public async Task<IActionResult> DeleteResourcesAsync(string groupType, string groupid, string id)
        {
            await registryDataProvider.DeleteResourcesAsync(groupType, groupid, id);
            return new NoContentResult();
        }

        public async Task<IActionResult> DeleteResourceVersionAsync(string groupType, string groupId, string id, string resourceId, string versionId, bool? meta)
        {
            await registryDataProvider.DeleteResourceVersionAsync(groupType, groupId, id, resourceId, versionId);
            return new NoContentResult();

        }

        public async Task<ActionResult<Document>> GetAllAsync(bool? inline)
        {
            var document = await registryDataProvider.GetAllAsync(inline);
            return new OkObjectResult(document);

        }

        public async Task<IActionResult> GetResourceAsync(string groupType, string groupid, string id, string resourceId, bool? meta)
        {
            if (!registryDataProvider.SupportsRawResources(groupType) || (meta.HasValue && meta.Value))
            {
                var resource = await registryDataProvider.GetResourceMetadataAsync(groupType, groupid, id, resourceId);
                return new OkObjectResult(resource);
            }
            else
            {
                var resourceStream = await registryDataProvider.GetResourceStreamAsync(groupType, groupid, id, resourceId, out var contentType);
                var result = new OkObjectResult(resourceStream);
                result.ContentTypes.Add(contentType);
                return result;
            }
        }

        public async Task<ActionResult<IDictionary<string, Resource>>> GetResourceGroupAllAsync(bool? inline, string name, string groupType)
        {
            var resourceGroupAll = await registryDataProvider.GetResourceGroupAllAsync(inline, name, groupType);
            return new OkObjectResult(resourceGroupAll);
        }

        public async Task<ActionResult<Resource>> GetResourceGroupAsync(string groupType, string groupid)
        {
            var resourceGroup = await registryDataProvider.GetResourceGroupAsync(groupType, groupid);
            return new OkObjectResult(resourceGroup);
        }

        public async Task<ActionResult<IDictionary<string, Resource>>> GetResourcesAllAsync(string query, bool? inline, string groupType, string groupId, string id)
        {
            var resourcesAll = await registryDataProvider.GetResourcesAllAsync(query, inline, groupType, groupId, id);
            return new OkObjectResult(resourcesAll);
        }

        public async Task<IActionResult> GetResourceVersionAsync(string groupType, string groupId, string id, string resourceId, string versionId, bool? meta)
        {
            if (!registryDataProvider.SupportsRawResources(groupType) || (meta.HasValue && meta.Value))
            {
                var resourceVersion = await registryDataProvider.GetResourceVersionAsync(groupType, groupId, id, resourceId, versionId);
                return new OkObjectResult(resourceVersion);
            }
            else
            {
                var resourceStream = await registryDataProvider.GetResourceVersionStreamAsync(groupType, groupId, id, resourceId, versionId, out var contentType);
                var result = new OkObjectResult(resourceStream);
                result.ContentTypes.Add(contentType);
                return result;
            }

        }

        public async Task<IActionResult> PostResourceAsync(string resource_description, Uri resource_docs, string resource_origin, IEnumerable<ResourceTag> resource_tags, string format, FileParameter body, string groupType, string groupId, string id, string resourceId, bool? meta)
        {
            var ct = new System.Net.Mime.ContentType(body.ContentType);
            if (ct.MediaType == "application/json")
            {
                // deserialize body.Data into Resource
                using (var tr = new StreamReader(body.Data, Encoding.GetEncoding(ct.CharSet ?? "utf-8")))
                {
                    JsonTextReader reader = new JsonTextReader(tr);
                    JsonSerializer jsonSerializer = new JsonSerializer();
                    var resource = jsonSerializer.Deserialize<Resource>(reader);
                    await registryDataProvider.CreateResourceAsync(groupType, groupId, id, resourceId, resource);
                }
            }
            else
            {
                var resource = new Resource()
                {
                    Description = resource_description,
                    Origin = resource_origin,
                    Docs = resource_docs,
                    Tags = new List<Tags>(resource_tags.Select((a) => new Tags { Name = a.Name, Value = a.Value })),
                    Id = resourceId,
                    GroupId = groupId,
                };
                await registryDataProvider.CreateResourceStreamAsync(groupType, groupType, id, resourceId, resource, body.Data);
            }
            return new OkResult();
        }

        public async Task<ActionResult<Resource>> PostResourceGroupAsync(Resource resource, string groupType)
        {
            var result = await registryDataProvider.CreateResourceGroupAsync(groupType, resource);
            return new CreatedResult(result.Self, result);
        }

        public async Task<ActionResult<Resource>> PutResourceGroupAsync(Resource resource, string groupType, string groupId)
        {
            var result = await registryDataProvider.UpsertResourceGroupAsync(groupType, groupId, resource);
            return new OkObjectResult(result);
        }

        public async Task<ActionResult<IDictionary<string, Resource>>> PutResourcesAsync(IDictionary<string, Resource> resources, string groupType, string groupid, string id)
        {
            var result = await registryDataProvider.UpsertResources(groupType, groupid, id, resources);
            return new OkObjectResult(result);
        }


        public Task<ActionResult<Document>> UploadDocAsync(Document body)
        {
            throw new NotImplementedException();

        }
    }
}