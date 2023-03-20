namespace xRegistry.Server.Services
{
    public interface IRegistryDataProvider
    {
        Task CreateResourceAsync(string groupType, string groupid, string id, string resourceId, Resource? resource);
        Task<Resource> CreateResourceGroupAsync(string groupType, Resource resource);
        Task CreateResourceStreamAsync(string groupType1, string groupType2, string id, string resourceId, Resource resource, Stream data);
        Task DeleteResourceAsync(long? epoch, string groupType, string groupId, string id, string resourceId);
        Task DeleteResourceGroupAsync(long? epoch, string groupType, string groupid);
        Task DeleteResourcesAsync(string groupType, string groupid, string id);
        Task DeleteResourceVersionAsync(string groupType, string groupid, string id, string resourceId, string versionId);
        Task<Document> GetAllAsync(bool? inline);
        Task<IDictionary<string, Resource>> GetResourceGroupAllAsync(bool? inline, string name, string groupType);
        Task<Resource> GetResourceGroupAsync(string groupType, string groupid);
        Task<Resource> GetResourceMetadataAsync(string groupType, string groupid, string id, string resourceId);
        Task<IDictionary<string, Resource>> GetResourcesAllAsync(string query, bool? inline, string groupType, string groupId, string id);
        Task<Stream> GetResourceStreamAsync(string groupType, string groupid, string id, string resourceId, out string contentType);
        Task<Resource> GetResourceVersionAsync(string groupType, string groupid, string id, string resourceId, string versionId);
        Task<Stream> GetResourceVersionStreamAsync(string groupType, string groupId, string id, string resourceId, string versionId, out string contentType);
        bool SupportsRawResources(string groupType);
        Task<Resource> UpsertResourceGroupAsync(string groupType, string groupId, Resource resource);
        Task<IDictionary<string, Resource>> UpsertResources(string groupType, string groupid, string id, IDictionary<string, Resource> resources);
    }
}