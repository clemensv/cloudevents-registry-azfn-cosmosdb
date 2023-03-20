//----------------------
// <auto-generated>
//     Generated using the NSwag toolchain v13.18.2.0 (NJsonSchema v10.8.0.0 (Newtonsoft.Json v13.0.0.0)) (http://NSwag.org)
// </auto-generated>
//----------------------

using Microsoft.AspNetCore.Mvc;

#pragma warning disable 108 // Disable "CS0108 '{derivedDto}.ToJson()' hides inherited member '{dtoBase}.ToJson()'. Use the new keyword if hiding was intended."
#pragma warning disable 114 // Disable "CS0114 '{derivedDto}.RaisePropertyChanged(String)' hides inherited member 'dtoBase.RaisePropertyChanged(String)'. To make the current member override that implementation, add the override keyword. Otherwise add the new keyword."
#pragma warning disable 472 // Disable "CS0472 The result of the expression is always 'false' since a value of type 'Int32' is never equal to 'null' of type 'Int32?'
#pragma warning disable 1573 // Disable "CS1573 Parameter '...' has no matching param tag in the XML comment for ...
#pragma warning disable 1591 // Disable "CS1591 Missing XML comment for publicly visible type or member ..."
#pragma warning disable 8073 // Disable "CS8073 The result of the expression is always 'false' since a value of type 'T' is never equal to 'null' of type 'T?'"
#pragma warning disable 3016 // Disable "CS3016 Arrays as attribute arguments is not CLS-compliant"
#pragma warning disable 8603 // Disable "CS8603 Possible null reference return"

namespace xRegistry.Types.Server
{
    using System = global::System;

    [System.CodeDom.Compiler.GeneratedCode("NSwag", "13.18.2.0 (NJsonSchema v10.8.0.0 (Newtonsoft.Json v13.0.0.0))")]
    public interface IRegistryController
    {

        /// <remarks>
        /// Gets the root document
        /// </remarks>

        /// <param name="inline">Set if references shall be inlined</param>

        /// <returns>The root document</returns>

        System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Document>> GetAllAsync(bool? inline);

        /// <remarks>
        /// Uploads a registry document and upserts its contents into the registry
        /// </remarks>

        /// <param name="body">A request to create or update the discovery endpoint's collection of endpoints with the given endpoints</param>

        /// <returns>The resulting document</returns>

        System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Document>> UploadDocAsync(Document body);

        /// <remarks>
        /// Gets all entries of the resource group
        /// </remarks>

        /// <param name="name">The name of the schema group to be returned</param>

        /// <param name="gROUP">The GROUP (plural)</param>

        System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<System.Collections.Generic.IDictionary<string, Resource>>> GetResourceGroupAllAsync(string name, string gROUP);


        /// <param name="body">Create a new group</param>

        /// <param name="gROUP">The GROUP (plural)</param>

        /// <returns>The resulting resource</returns>

        System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Resource>> PostGroupAsync(Resource body, string gROUP);


        /// <param name="gROUP">The GROUP (plural)</param>

        /// <param name="groupid">The id of the group</param>

        /// <returns>The schema group</returns>

        System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Resource>> GetResourceGroupAsync(string gROUP, string groupid);

        /// <remarks>
        /// creates or updates the resource group
        /// </remarks>

        /// <param name="body">A request to create or update the discovery group's collection of groups with the given group</param>

        /// <param name="gROUP">The GROUP (plural)</param>

        /// <param name="groupid">The id of the group</param>

        System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Resource>> PutResourceGroupAsync(Resource body, string gROUP, string groupid);


        /// <param name="epoch">The epoch of the schema group to be deleted</param>

        /// <param name="gROUP">The GROUP (plural)</param>

        /// <param name="groupid">The id of the group</param>

        /// <returns>A list of the Endpoints that were deleted</returns>

        System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Resource>> DeleteResourceGroupAsync(long? epoch, string gROUP, string groupid);

        /// <remarks>
        /// Get an optionally filtered collection of resources
        /// </remarks>

        /// <param name="name">The name of the schema to be returned</param>

        /// <param name="gROUP">The GROUP (plural)</param>

        /// <param name="groupid">The id of the group</param>

        /// <param name="id">The RESOURCE (plural)</param>

        /// <returns>A list of resources (optionally matching the query parameter)</returns>

        System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<System.Collections.Generic.IDictionary<string, Resource>>> GetResourcesAllAsync(string name, string gROUP, string groupid, string id);


        /// <param name="body">A request to create or update the discovery group's collection of groups with the given group</param>

        /// <param name="gROUP">The GROUP (plural)</param>

        /// <param name="groupid">The id of the group</param>

        /// <param name="id">The RESOURCE (plural)</param>

        /// <returns>A Endpoint Reference referencing the updated Endpoint</returns>

        System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Resource>> PutResourcesAsync(System.Collections.Generic.IEnumerable<Resource> body, string gROUP, string groupid, string id);


        /// <param name="gROUP">The GROUP (plural)</param>

        /// <param name="groupid">The id of the group</param>

        /// <param name="id">The RESOURCE (plural)</param>

        /// <returns>Delete succeeded</returns>

        System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.IActionResult> DeleteResourcesAsync(string gROUP, string groupid, string id);


        /// <param name="gROUP">The GROUP (plural)</param>

        /// <param name="groupid">The id of the group</param>

        /// <param name="id">The RESOURCE (plural)</param>

        /// <param name="resourceid">The id of the schema</param>

        /// <returns>The corresponding resource</returns>

        System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.IActionResult> GetLatestResourceAsync(string gROUP, string groupid, string id, string resourceid);

        /// <summary>
        /// Post new resource version
        /// </summary>

        /// <remarks>
        /// Register schema version If schema of specified name does not exist in specified group, schema and schema version is created at version 1. If schema of specified name exists already in specified group, schema is created at latest version + 1. If schema with identical content already exists, existing schema's ID is returned.
        /// </remarks>

        /// <param name="resource_description">A summary of the purpose of the resource.</param>

        /// <param name="resource_docs">Absolute URL that provides a link to additional documentation about the resource.</param>

        /// <param name="resource_origin">A URI reference to the original source of this resource.</param>


        /// <param name="format">format</param>

        /// <param name="body">A request to add a new schema document to the schema's document collection</param>

        /// <param name="gROUP">The GROUP (plural)</param>

        /// <param name="groupid">The id of the group</param>

        /// <param name="id">The RESOURCE (plural)</param>

        /// <param name="resourceid">The id of the schema</param>

        /// <returns>A request to add a new schema document to the schema's document collection</returns>

        System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.IActionResult> PostResourceDocumentAsync(string resource_description, System.Uri resource_docs, string resource_origin, System.Collections.Generic.IEnumerable<ResourceTag> resource_tags, string format, Microsoft.AspNetCore.Http.IFormFile body, string gROUP, string groupid, string id, string resourceid);


        /// <param name="gROUP">The GROUP (plural)</param>

        /// <param name="groupid">The id of the group</param>

        /// <param name="id">The RESOURCE (plural)</param>

        /// <param name="resourceid">The id of the schema</param>

        /// <returns>The corresponding schema</returns>

        System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Resource>> GetLatestResourceMetadataAsync(string gROUP, string groupid, string id, string resourceid);

        /// <remarks>
        /// Updates metadata of the document stored for the schema version
        /// </remarks>

        /// <param name="gROUP">The GROUP (plural)</param>

        /// <param name="groupid">The id of the group</param>

        /// <param name="id">The RESOURCE (plural)</param>

        /// <param name="resourceid">The id of the schema</param>

        /// <returns>The metadata of the schema version document</returns>

        System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Resource>> UpdateLatestResourceVersionMetadataAsync(Resource body, string gROUP, string groupid, string id, string resourceid);

        /// <remarks>
        /// Gets the document stored for the schema version
        /// </remarks>

        /// <param name="gROUP">The GROUP (plural)</param>

        /// <param name="groupid">The id of the group</param>

        /// <param name="id">The RESOURCE (plural)</param>

        /// <param name="resourceid">The id of the schema</param>

        /// <param name="versionid">The id of the schema</param>

        /// <returns>The schema version document</returns>

        System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.IActionResult> GetResourceVersionAsync(string gROUP, string groupid, string id, string resourceid, string versionid);


        /// <param name="gROUP">The GROUP (plural)</param>

        /// <param name="groupid">The id of the group</param>

        /// <param name="id">The RESOURCE (plural)</param>

        /// <param name="resourceid">The id of the schema</param>

        /// <param name="versionid">The id of the schema</param>

        /// <returns>A list of the Endpoints that were deleted</returns>

        System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Resource>> DeleteResourceVersionAsync(string gROUP, string groupid, string id, string resourceid, string versionid);

        /// <remarks>
        /// Gets metadata of the document stored for the schema version
        /// </remarks>

        /// <param name="gROUP">The GROUP (plural)</param>

        /// <param name="groupid">The id of the group</param>

        /// <param name="id">The RESOURCE (plural)</param>

        /// <param name="resourceid">The id of the schema</param>

        /// <param name="versionid">The id of the schema</param>

        /// <returns>The metadata of the schema version document</returns>

        System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Resource>> GetResourceVersionMetadataAsync(string gROUP, string groupid, string id, string resourceid, string versionid);

        /// <remarks>
        /// Updates metadata of the document stored for the schema version
        /// </remarks>

        /// <param name="gROUP">The GROUP (plural)</param>

        /// <param name="groupid">The id of the group</param>

        /// <param name="id">The RESOURCE (plural)</param>

        /// <param name="resourceid">The id of the schema</param>

        /// <param name="versionid">The id of the schema</param>

        /// <returns>The metadata of the schema version document</returns>

        System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Resource>> UpdateResourceVersionMetadataAsync(Resource body, string gROUP, string groupid, string id, string resourceid, string versionid);

    }

    [System.CodeDom.Compiler.GeneratedCode("NSwag", "13.18.2.0 (NJsonSchema v10.8.0.0 (Newtonsoft.Json v13.0.0.0))")]

    public partial class RegistryController : Microsoft.AspNetCore.Mvc.ControllerBase
    {
        private IRegistryController _implementation;

        public RegistryController(IRegistryController implementation)
        {
            _implementation = implementation;
        }

        /// <remarks>
        /// Gets the root document
        /// </remarks>
        /// <param name="inline">Set if references shall be inlined</param>
        /// <returns>The root document</returns>
        [Microsoft.AspNetCore.Mvc.HttpGet, Microsoft.AspNetCore.Mvc.Route("")]
        public System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Document>> GetAll([Microsoft.AspNetCore.Mvc.FromQuery] bool? inline)
        {

            return _implementation.GetAllAsync(inline);
        }

        /// <remarks>
        /// Uploads a registry document and upserts its contents into the registry
        /// </remarks>
        /// <param name="body">A request to create or update the discovery endpoint's collection of endpoints with the given endpoints</param>
        /// <returns>The resulting document</returns>
        [Microsoft.AspNetCore.Mvc.HttpPost, Microsoft.AspNetCore.Mvc.Route("")]
        public System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Document>> UploadDoc([Microsoft.AspNetCore.Mvc.FromBody] Document body)
        {

            return _implementation.UploadDocAsync(body);
        }

        /// <remarks>
        /// Gets all entries of the resource group
        /// </remarks>
        /// <param name="name">The name of the schema group to be returned</param>
        /// <param name="gROUP">The GROUP (plural)</param>
        [Microsoft.AspNetCore.Mvc.HttpGet, Microsoft.AspNetCore.Mvc.Route("{GROUP}")]
        public System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<System.Collections.Generic.IDictionary<string, Resource>>> GetResourceGroupAll([Microsoft.AspNetCore.Mvc.FromQuery] string name, string gROUP)
        {

            return _implementation.GetResourceGroupAllAsync(name, gROUP);
        }

        /// <param name="body">Create a new group</param>
        /// <param name="gROUP">The GROUP (plural)</param>
        /// <returns>The resulting resource</returns>
        [Microsoft.AspNetCore.Mvc.HttpPost, Microsoft.AspNetCore.Mvc.Route("{GROUP}")]
        public System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Resource>> PostGroup([Microsoft.AspNetCore.Mvc.FromBody] Resource body, string gROUP)
        {

            return _implementation.PostGroupAsync(body, gROUP);
        }

        /// <param name="gROUP">The GROUP (plural)</param>
        /// <param name="groupid">The id of the group</param>
        /// <returns>The schema group</returns>
        [Microsoft.AspNetCore.Mvc.HttpGet, Microsoft.AspNetCore.Mvc.Route("{GROUP}/{groupid}")]
        public System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Resource>> GetResourceGroup(string gROUP, string groupid)
        {

            return _implementation.GetResourceGroupAsync(gROUP, groupid);
        }

        /// <remarks>
        /// creates or updates the resource group
        /// </remarks>
        /// <param name="body">A request to create or update the discovery group's collection of groups with the given group</param>
        /// <param name="gROUP">The GROUP (plural)</param>
        /// <param name="groupid">The id of the group</param>
        [Microsoft.AspNetCore.Mvc.HttpPut, Microsoft.AspNetCore.Mvc.Route("{GROUP}/{groupid}")]
        public System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Resource>> PutResourceGroup([Microsoft.AspNetCore.Mvc.FromBody] Resource body, string gROUP, string groupid)
        {

            return _implementation.PutResourceGroupAsync(body, gROUP, groupid);
        }

        /// <param name="epoch">The epoch of the schema group to be deleted</param>
        /// <param name="gROUP">The GROUP (plural)</param>
        /// <param name="groupid">The id of the group</param>
        /// <returns>A list of the Endpoints that were deleted</returns>
        [Microsoft.AspNetCore.Mvc.HttpDelete, Microsoft.AspNetCore.Mvc.Route("{GROUP}/{groupid}")]
        public System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Resource>> DeleteResourceGroup([Microsoft.AspNetCore.Mvc.FromQuery] long? epoch, string gROUP, string groupid)
        {

            return _implementation.DeleteResourceGroupAsync(epoch, gROUP, groupid);
        }

        /// <remarks>
        /// Get an optionally filtered collection of resources
        /// </remarks>
        /// <param name="name">The name of the schema to be returned</param>
        /// <param name="gROUP">The GROUP (plural)</param>
        /// <param name="groupid">The id of the group</param>
        /// <param name="id">The RESOURCE (plural)</param>
        /// <returns>A list of resources (optionally matching the query parameter)</returns>
        [Microsoft.AspNetCore.Mvc.HttpGet, Microsoft.AspNetCore.Mvc.Route("{GROUP}/{groupid}/{RESOURCE}")]
        public System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<System.Collections.Generic.IDictionary<string, Resource>>> GetResourcesAll([Microsoft.AspNetCore.Mvc.FromQuery] string name, string gROUP, string groupid, string id)
        {

            return _implementation.GetResourcesAllAsync(name, gROUP, groupid, id);
        }

        /// <param name="body">A request to create or update the discovery group's collection of groups with the given group</param>
        /// <param name="gROUP">The GROUP (plural)</param>
        /// <param name="groupid">The id of the group</param>
        /// <param name="id">The RESOURCE (plural)</param>
        /// <returns>A Endpoint Reference referencing the updated Endpoint</returns>
        [Microsoft.AspNetCore.Mvc.HttpPut, Microsoft.AspNetCore.Mvc.Route("{GROUP}/{groupid}/{RESOURCE}")]
        public System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Resource>> PutResources([Microsoft.AspNetCore.Mvc.FromBody] System.Collections.Generic.IEnumerable<Resource> body, string gROUP, string groupid, string id)
        {

            return _implementation.PutResourcesAsync(body, gROUP, groupid, id);
        }

        /// <param name="gROUP">The GROUP (plural)</param>
        /// <param name="groupid">The id of the group</param>
        /// <param name="id">The RESOURCE (plural)</param>
        /// <returns>Delete succeeded</returns>
        [Microsoft.AspNetCore.Mvc.HttpDelete, Microsoft.AspNetCore.Mvc.Route("{GROUP}/{groupid}/{RESOURCE}")]
        public System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.IActionResult> DeleteResources(string gROUP, string groupid, string id)
        {

            return _implementation.DeleteResourcesAsync(gROUP, groupid, id);
        }

        /// <param name="gROUP">The GROUP (plural)</param>
        /// <param name="groupid">The id of the group</param>
        /// <param name="id">The RESOURCE (plural)</param>
        /// <param name="resourceid">The id of the schema</param>
        /// <returns>The corresponding resource</returns>
        [Microsoft.AspNetCore.Mvc.HttpGet, Microsoft.AspNetCore.Mvc.Route("{GROUP}/{groupid}/{RESOURCE}/{resourceid}")]
        public System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.IActionResult> GetLatestResource(string gROUP, string groupid, string id, string resourceid)
        {

            return _implementation.GetLatestResourceAsync(gROUP, groupid, id, resourceid);
        }

        /// <summary>
        /// Post new resource version
        /// </summary>
        /// <remarks>
        /// Register schema version If schema of specified name does not exist in specified group, schema and schema version is created at version 1. If schema of specified name exists already in specified group, schema is created at latest version + 1. If schema with identical content already exists, existing schema's ID is returned.
        /// </remarks>
        /// <param name="resource_description">A summary of the purpose of the resource.</param>
        /// <param name="resource_docs">Absolute URL that provides a link to additional documentation about the resource.</param>
        /// <param name="resource_origin">A URI reference to the original source of this resource.</param>
        /// <param name="format">format</param>
        /// <param name="body">A request to add a new schema document to the schema's document collection</param>
        /// <param name="gROUP">The GROUP (plural)</param>
        /// <param name="groupid">The id of the group</param>
        /// <param name="id">The RESOURCE (plural)</param>
        /// <param name="resourceid">The id of the schema</param>
        /// <returns>A request to add a new schema document to the schema's document collection</returns>
        [Microsoft.AspNetCore.Mvc.HttpPost, Microsoft.AspNetCore.Mvc.Route("{GROUP}/{groupid}/{RESOURCE}/{resourceid}")]
        public System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.IActionResult> PostResourceDocument([Microsoft.AspNetCore.Mvc.FromHeader(Name = "resource-description")] string resource_description, [Microsoft.AspNetCore.Mvc.FromHeader(Name = "resource-docs")] System.Uri resource_docs, [Microsoft.AspNetCore.Mvc.FromHeader(Name = "resource-origin")] string resource_origin, [Microsoft.AspNetCore.Mvc.FromHeader(Name = "resource-tags")] System.Collections.Generic.IEnumerable<ResourceTag> resource_tags, [Microsoft.AspNetCore.Mvc.FromHeader] string format, Microsoft.AspNetCore.Http.IFormFile body, string gROUP, string groupid, string id, string resourceid)
        {

            return _implementation.PostResourceDocumentAsync(resource_description, resource_docs, resource_origin, resource_tags, format, body, gROUP, groupid, id, resourceid);
        }

        /// <param name="gROUP">The GROUP (plural)</param>
        /// <param name="groupid">The id of the group</param>
        /// <param name="id">The RESOURCE (plural)</param>
        /// <param name="resourceid">The id of the schema</param>
        /// <returns>The corresponding schema</returns>
        [Microsoft.AspNetCore.Mvc.HttpGet, Microsoft.AspNetCore.Mvc.Route("{GROUP}/{groupid}/{RESOURCE}/{resourceid}/meta")]
        public System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Resource>> GetLatestResourceMetadata(string gROUP, string groupid, string id, string resourceid)
        {

            return _implementation.GetLatestResourceMetadataAsync(gROUP, groupid, id, resourceid);
        }

        /// <remarks>
        /// Updates metadata of the document stored for the schema version
        /// </remarks>
        /// <param name="gROUP">The GROUP (plural)</param>
        /// <param name="groupid">The id of the group</param>
        /// <param name="id">The RESOURCE (plural)</param>
        /// <param name="resourceid">The id of the schema</param>
        /// <returns>The metadata of the schema version document</returns>
        [Microsoft.AspNetCore.Mvc.HttpPut, Microsoft.AspNetCore.Mvc.Route("{GROUP}/{groupid}/{RESOURCE}/{resourceid}/meta")]
        public System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Resource>> UpdateLatestResourceVersionMetadata([Microsoft.AspNetCore.Mvc.FromBody] Resource body, string gROUP, string groupid, string id, string resourceid)
        {

            return _implementation.UpdateLatestResourceVersionMetadataAsync(body, gROUP, groupid, id, resourceid);
        }

        /// <remarks>
        /// Gets the document stored for the schema version
        /// </remarks>
        /// <param name="gROUP">The GROUP (plural)</param>
        /// <param name="groupid">The id of the group</param>
        /// <param name="id">The RESOURCE (plural)</param>
        /// <param name="resourceid">The id of the schema</param>
        /// <param name="versionid">The id of the schema</param>
        /// <returns>The schema version document</returns>
        [Microsoft.AspNetCore.Mvc.HttpGet, Microsoft.AspNetCore.Mvc.Route("{GROUP}/{groupid}/{RESOURCE}/{resourceid}/versions/{versionid}")]
        public System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.IActionResult> GetResourceVersion(string gROUP, string groupid, string id, string resourceid, string versionid)
        {

            return _implementation.GetResourceVersionAsync(gROUP, groupid, id, resourceid, versionid);
        }

        /// <param name="gROUP">The GROUP (plural)</param>
        /// <param name="groupid">The id of the group</param>
        /// <param name="id">The RESOURCE (plural)</param>
        /// <param name="resourceid">The id of the schema</param>
        /// <param name="versionid">The id of the schema</param>
        /// <returns>A list of the Endpoints that were deleted</returns>
        [Microsoft.AspNetCore.Mvc.HttpDelete, Microsoft.AspNetCore.Mvc.Route("{GROUP}/{groupid}/{RESOURCE}/{resourceid}/versions/{versionid}")]
        public System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Resource>> DeleteResourceVersion(string gROUP, string groupid, string id, string resourceid, string versionid)
        {

            return _implementation.DeleteResourceVersionAsync(gROUP, groupid, id, resourceid, versionid);
        }

        /// <remarks>
        /// Gets metadata of the document stored for the schema version
        /// </remarks>
        /// <param name="gROUP">The GROUP (plural)</param>
        /// <param name="groupid">The id of the group</param>
        /// <param name="id">The RESOURCE (plural)</param>
        /// <param name="resourceid">The id of the schema</param>
        /// <param name="versionid">The id of the schema</param>
        /// <returns>The metadata of the schema version document</returns>
        [Microsoft.AspNetCore.Mvc.HttpGet, Microsoft.AspNetCore.Mvc.Route("{GROUP}/{groupid}/{RESOURCE}/{resourceid}/versions/{versionid}/meta")]
        public System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Resource>> GetResourceVersionMetadata(string gROUP, string groupid, string id, string resourceid, string versionid)
        {

            return _implementation.GetResourceVersionMetadataAsync(gROUP, groupid, id, resourceid, versionid);
        }

        /// <remarks>
        /// Updates metadata of the document stored for the schema version
        /// </remarks>
        /// <param name="gROUP">The GROUP (plural)</param>
        /// <param name="groupid">The id of the group</param>
        /// <param name="id">The RESOURCE (plural)</param>
        /// <param name="resourceid">The id of the schema</param>
        /// <param name="versionid">The id of the schema</param>
        /// <returns>The metadata of the schema version document</returns>
        [Microsoft.AspNetCore.Mvc.HttpPut, Microsoft.AspNetCore.Mvc.Route("{GROUP}/{groupid}/{RESOURCE}/{resourceid}/versions/{versionid}/meta")]
        public System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<Resource>> UpdateResourceVersionMetadata([Microsoft.AspNetCore.Mvc.FromBody] Resource body, string gROUP, string groupid, string id, string resourceid, string versionid)
        {

            return _implementation.UpdateResourceVersionMetadataAsync(body, gROUP, groupid, id, resourceid, versionid);
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "13.18.2.0 (NJsonSchema v10.8.0.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial class ResourceTag
    {
        [Newtonsoft.Json.JsonProperty("name", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string Name { get; set; }

        [Newtonsoft.Json.JsonProperty("value", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string Value { get; set; }

        private System.Collections.Generic.IDictionary<string, object> _additionalProperties;

        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new System.Collections.Generic.Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "13.18.2.0 (NJsonSchema v10.8.0.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial class Document
    {
        [Newtonsoft.Json.JsonProperty("specversion", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string Specversion { get; set; }

        private System.Collections.Generic.IDictionary<string, object> _additionalProperties;

        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new System.Collections.Generic.Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "13.18.2.0 (NJsonSchema v10.8.0.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial class Resource
    {
        /// <summary>
        /// A unique identifier for this Endpoint. This value MUST be globally unique
        /// </summary>
        [Newtonsoft.Json.JsonProperty("id", Required = Newtonsoft.Json.Required.Always)]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Id { get; set; }

        /// <summary>
        /// Optional reference to a definitionGroup that this resource is subordinate to
        /// </summary>
        [Newtonsoft.Json.JsonProperty("groupId", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string GroupId { get; set; }

        /// <summary>
        /// A number representing the version number of the resource.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("version", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public long Version { get; set; }

        /// <summary>
        /// A unique URI for the resource. The URI MUST be a combination of the base URI of the list of this resource type for the current Discovery Service appended with the `id` of this resource.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("self", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string Self { get; set; }

        /// <summary>
        /// A summary of the purpose of the resource.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("description", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string Description { get; set; }

        /// <summary>
        /// The name of the resource.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("name", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string Name { get; set; }

        /// <summary>
        /// Absolute URL that provides a link to additional documentation about the resource.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("docs", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public System.Uri Docs { get; set; }

        /// <summary>
        /// A URI reference to the original source of this resource.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("origin", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string Origin { get; set; }

        [Newtonsoft.Json.JsonProperty("tags", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public System.Collections.Generic.List<Tags> Tags { get; set; }

        /// <summary>
        /// Identity of who created this entity
        /// </summary>
        [Newtonsoft.Json.JsonProperty("createdBy", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string CreatedBy { get; set; }

        /// <summary>
        /// Time when this entity was created
        /// </summary>
        [Newtonsoft.Json.JsonProperty("createdOn", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public System.DateTimeOffset CreatedOn { get; set; }

        /// <summary>
        /// Identity of who last modified this entity
        /// </summary>
        [Newtonsoft.Json.JsonProperty("modifiedBy", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string ModifiedBy { get; set; }

        /// <summary>
        /// Time when this entity was last modified
        /// </summary>
        [Newtonsoft.Json.JsonProperty("modifiedOn", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public System.DateTimeOffset ModifiedOn { get; set; }

        private System.Collections.Generic.IDictionary<string, object> _additionalProperties;

        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new System.Collections.Generic.Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "13.18.2.0 (NJsonSchema v10.8.0.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial class Tags
    {
        [Newtonsoft.Json.JsonProperty("name", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string Name { get; set; }

        [Newtonsoft.Json.JsonProperty("value", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string Value { get; set; }

        private System.Collections.Generic.IDictionary<string, object> _additionalProperties;

        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new System.Collections.Generic.Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NSwag", "13.18.2.0 (NJsonSchema v10.8.0.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial class FileParameter
    {
        public FileParameter(System.IO.Stream data)
            : this (data, null, null)
        {
        }

        public FileParameter(System.IO.Stream data, string fileName)
            : this (data, fileName, null)
        {
        }

        public FileParameter(System.IO.Stream data, string fileName, string contentType)
        {
            Data = data;
            FileName = fileName;
            ContentType = contentType;
        }

        public System.IO.Stream Data { get; private set; }

        public string FileName { get; private set; }

        public string ContentType { get; private set; }
    }

    [System.CodeDom.Compiler.GeneratedCode("NSwag", "13.18.2.0 (NJsonSchema v10.8.0.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial class FileResponse : System.IDisposable
    {
        private System.IDisposable _client;
        private System.IDisposable _response;

        public int StatusCode { get; private set; }

        public System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IEnumerable<string>> Headers { get; private set; }

        public System.IO.Stream Stream { get; private set; }

        public bool IsPartial
        {
            get { return StatusCode == 206; }
        }

        public FileResponse(int statusCode, System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IEnumerable<string>> headers, System.IO.Stream stream, System.IDisposable client, System.IDisposable response)
        {
            StatusCode = statusCode;
            Headers = headers;
            Stream = stream;
            _client = client;
            _response = response;
        }

        public void Dispose()
        {
            Stream.Dispose();
            if (_response != null)
                _response.Dispose();
            if (_client != null)
                _client.Dispose();
        }
    }



}

#pragma warning restore 1591
#pragma warning restore 1573
#pragma warning restore  472
#pragma warning restore  114
#pragma warning restore  108
#pragma warning restore 3016
#pragma warning restore 8603