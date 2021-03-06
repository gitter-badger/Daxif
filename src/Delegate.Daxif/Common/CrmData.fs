﻿namespace DG.Daxif.Common

open System
open Microsoft.Crm.Sdk
open Microsoft.Xrm.Sdk
open Microsoft.Xrm.Sdk.Client
open Microsoft.Xrm.Sdk.Messages
open Microsoft.Xrm.Sdk.Metadata
open Microsoft.Xrm.Sdk.Query
open DG.Daxif
open DG.Daxif.Common

// http://msdn.microsoft.com/en-us/library/microsoft.xrm.sdk.organizationrequest.requestname.aspx
module CrmData = 
  module Metadata = 
    let private entityHelper proxy (logicalName:string) filter = 
      let (proxy : IOrganizationService) = proxy
      let (filter : EntityFilters) = filter
      let req = RetrieveEntityRequest()
      req.LogicalName <- logicalName
      req.EntityFilters <- filter
      req.MetadataId <- Guid.Empty
      req.RetrieveAsIfPublished <- true
      let resp = proxy.Execute(req)
      (Seq.head resp.Results).Value :?> EntityMetadata
    
    let entity proxy logicalName = 
      entityHelper proxy logicalName EntityFilters.Entity
    let entityAll proxy logicalName = 
      entityHelper proxy logicalName EntityFilters.All
    let entityAttributes proxy logicalName = 
      (entityHelper proxy logicalName EntityFilters.Attributes).Attributes
    let entityOneToManyRelationships proxy logicalName = 
      (entityHelper proxy logicalName EntityFilters.Relationships).OneToManyRelationships
    let entityManyToOneRelationships proxy logicalName = 
      (entityHelper proxy logicalName EntityFilters.Relationships).ManyToOneRelationships
    let entityManyToManyRelationships proxy logicalName = 
      (entityHelper proxy logicalName EntityFilters.Relationships).ManyToManyRelationships
    
    let allEntities (proxy : IOrganizationService) = 
      let req = OrganizationRequest()
      let param = ParameterCollection()
      param.Add(@"EntityFilters", EntityFilters.Entity)
      param.Add(@"RetrieveAsIfPublished", true)
      req.RequestName <- @"RetrieveAllEntities"
      req.Parameters.AddRange(param)
      let resp = proxy.Execute(req)
      (Seq.head resp.Results).Value :?> seq<EntityMetadata>
    
    let getEntityLogicalNameFromId (proxy:IOrganizationService) metadataId =
      let request = RetrieveEntityRequest()
      request.MetadataId <- metadataId
      request.EntityFilters <- Microsoft.Xrm.Sdk.Metadata.EntityFilters.Entity
      request.RetrieveAsIfPublished <- true
      let resp = proxy.Execute(request) :?> RetrieveEntityResponse
      resp.EntityMetadata.LogicalName
  
  module CRUD = 
  
    let createReq entity parameters = 
      let (parameters : ParameterCollection) = parameters
      let req = Messages.CreateRequest()
      req.Target <- entity
      req.Parameters.AddRange(parameters)
      req
    
    let create proxy entity parameters = 
      let (proxy : IOrganizationService) = proxy
      let (parameters : ParameterCollection) = parameters
      let req = createReq entity parameters
      let resp = proxy.Execute(req) :?> Messages.CreateResponse
      resp.id
    
    let retrieveReq logicalName guid = 
      let req = Messages.RetrieveRequest()
      req.ColumnSet <- ColumnSet(true)
      req.Target <- EntityReference(logicalName, id = guid)
      req
    
    let retrieve proxy logicalName guid = 
      let (proxy : IOrganizationService) = proxy
      let req = retrieveReq logicalName guid
      let resp = proxy.Execute(req) :?> Messages.RetrieveResponse
      resp.Entity
    
    let retrieveMultiple proxy logicalName query = 
      try 
        let (proxy : IOrganizationService) = proxy
        let (query : QueryExpression) = query
        query.PageInfo <- PagingInfo()
        query.PageInfo.ReturnTotalRecordCount <- true
        query.PageInfo.PageNumber <- 1
        query.PageInfo.Count <- 1000 // ItemsInEachCall 
        seq { 
          let resp = proxy.RetrieveMultiple(query)
          yield! resp.Entities
          let rec retrieveMultiple' (ec : EntityCollection) pn = 
            seq { 
              match ec.MoreRecords with
              | true -> 
                query.PageInfo.PageNumber <- (pn + 1)
                query.PageInfo.PagingCookie <- ec.PagingCookie
                let resp' = proxy.RetrieveMultiple(query)
                yield! resp'.Entities
                yield! retrieveMultiple' resp' (pn + 1)
              | false -> ()
            }
          yield! retrieveMultiple' resp 1
        }
      with ex -> 
        failwith 
          ("Retrieving " + logicalName + " failed: " 
           + Utility.getFullException (ex))
    
    let updateReq entity = 
      let req = Messages.UpdateRequest()
      req.Target <- entity
      req
    
    let update proxy entity = 
      let (proxy : IOrganizationService) = proxy
      let req = updateReq entity
      proxy.Execute(req) :?> Messages.UpdateResponse |> ignore
      entity.Id
    
    let deleteReq logicalName guid = 
      let req = Messages.DeleteRequest()
      req.Target <- EntityReference(logicalName, id = guid)
      req
    
    let delete proxy logicalName guid = 
      let (proxy : IOrganizationService) = proxy
      let req = deleteReq logicalName guid
      proxy.Execute(req) :?> Messages.DeleteResponse |> ignore
      Guid.Empty
  
  module Solution =
    let addWebResourceToSolutionReq (entity:Entity) solutionName = 
      let req = Messages.AddSolutionComponentRequest()
      req.ComponentId <- entity.Id
      req.ComponentType <- 61
      req.SolutionUniqueName <- solutionName
      req :> OrganizationRequest

    let addWebResourceToSolution proxy entity solutionName = 
      match solutionName with
      | Some n -> let (proxy : IOrganizationService) = proxy
                  let req = addWebResourceToSolutionReq entity n
                  proxy.Execute(req) :?> Messages.AddSolutionComponentResponse
                  |> ignore
      | None -> ()
