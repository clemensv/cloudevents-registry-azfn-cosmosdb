﻿
using System;
using System.Collections.Generic;

namespace Azure.CloudEvents.Registry
{
    internal class Catalog : Document
    {
        public Uri EndpointsUrl { get; set; }
        public Uri DefinitionGroupsUrl { get; set; }
        public Uri SchemaGroupsUrl { get; set; }
        public IDictionary<string, SchemaRegistry.SchemaGroup> SchemaGroups { get; internal set; }
        public IDictionary<string, MessageDefinitionsRegistry.DefinitionGroup> DefinitionGroups { get; internal set; }
        public IDictionary<string, EndpointRegistry.Endpoint> Endpoints { get; internal set; }
    }
}