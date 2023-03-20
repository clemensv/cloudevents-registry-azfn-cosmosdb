using Microsoft.Graph;

namespace xRegistry.Server.Services
{
    public class CosmosRegistryDataProvider : IRegistryDataProvider
    {
        public Task CreateResourceAsync(string groupType, string groupid, string id, string resourceId, Resource? resource)
        {
            throw new NotImplementedException();
        }

        public Task<Resource> CreateResourceGroupAsync(string groupType, Resource resource)
        {
            throw new NotImplementedException();
        }

        public Task CreateResourceStreamAsync(string groupType1, string groupType2, string id, string resourceId, Resource resource, Stream data)
        {
            throw new NotImplementedException();
        }

        public Task DeleteResourceAsync(long? epoch, string groupType, string groupId, string id, string resourceId)
        {
            throw new NotImplementedException();
        }

        public Task DeleteResourceGroupAsync(long? epoch, string groupType, string groupid)
        {
            throw new NotImplementedException();
        }

        public Task DeleteResourcesAsync(string groupType, string groupid, string id)
        {
            throw new NotImplementedException();
        }

        public Task DeleteResourceVersionAsync(string groupType, string groupid, string id, string resourceId, string versionId)
        {
            throw new NotImplementedException();
        }

        public Task<Document> GetAllAsync(bool? inline)
        {
            throw new NotImplementedException();
        }

        public Task<IDictionary<string, Resource>> GetResourceGroupAllAsync(bool? inline, string name, string groupType)
        {
            throw new NotImplementedException();
        }

        public Task<Resource> GetResourceGroupAsync(string groupType, string groupid)
        {
            throw new NotImplementedException();
        }

        public Task<Resource> GetResourceMetadataAsync(string groupType, string groupid, string id, string resourceId)
        {
            throw new NotImplementedException();
        }

        public Task<IDictionary<string, Resource>> GetResourcesAllAsync(string query, bool? inline, string groupType, string groupId, string id)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> GetResourceStreamAsync(string groupType, string groupid, string id, string resourceId, out string contentType)
        {
            throw new NotImplementedException();
        }

        public Task<Resource> GetResourceVersionAsync(string groupType, string groupid, string id, string resourceId, string versionId)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> GetResourceVersionStreamAsync(string groupType, string groupId, string id, string resourceId, string versionId, out string contentType)
        {
            throw new NotImplementedException();
        }

        public bool SupportsRawResources(string groupType)
        {
            throw new NotImplementedException();
        }

        public Task<Resource> UpsertResourceGroupAsync(string groupType, string groupId, Resource resource)
        {
            throw new NotImplementedException();
        }

        public Task<IDictionary<string, Resource>> UpsertResources(string groupType, string groupid, string id, IDictionary<string, Resource> resources)
        {
            throw new NotImplementedException();
        }

        void DeleteGroup(string groupId)
        {

        }


    }
}
