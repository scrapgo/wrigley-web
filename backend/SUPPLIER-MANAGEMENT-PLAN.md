# Supplier Management Implementation Plan

## Overview

This document outlines the work required to implement supplier management functionality that integrates with Quickbase. The backend already has a robust Quickbase integration infrastructure in place through the `QuickbaseEngine` module.

## Current Infrastructure

The backend provides a complete caching layer over the Quickbase REST API with:

- Thread-safe query execution with TTL-based caching
- Stale-if-error functionality for resilience
- Authorization integration ensuring only authenticated users can access Quickbase data
- PostgreSQL-based caching for performance

## Implementation Requirements

### 1. Create Suppliers Module

Following the existing modular architecture pattern, create:

```
backend/src/Modules/Suppliers/
├── ScrapGo.Core.Modules.Suppliers.Api/
├── ScrapGo.Core.Modules.Suppliers.Application/
├── ScrapGo.Core.Modules.Suppliers.Domain/
└── ScrapGo.Core.Modules.Suppliers.Infrastructure/
```

### 2. Define Supplier Data Models

- Identify relevant Quickbase table fields for suppliers
- Create domain entities that map to Quickbase data
- Implement DTOs for API responses

### 3. Implement Business Logic

- Service classes that use `IQuickbaseQueryService` to query supplier data
- Mapping logic from Quickbase responses to domain models
- Validation and business rules for supplier operations

### 4. Create API Controllers

- REST endpoints for supplier CRUD operations (list, get, create, update, delete)
- Integration with Identity module for authorization
  - Leverage existing `Downstream.Suppliers.Read` and `Downstream.Suppliers.Write` permissions
- Proper error handling and response formatting

### 5. Register the Module

- Add module registration in `ScrapGoModulesServiceCollectionExtensions`
- Register module assembly in controller discovery

## Implementation Approach

Leverage the existing `IQuickbaseQueryService` for all Quickbase interactions:

```csharp
// Example service implementation
public class SupplierQueryService(IQuickbaseQueryService queryService)
{
    private static readonly QuickbaseQuery SupplierQuery =
        new("bsupp0001", Select: [3, 6, 8, 12]); // Supplier table ID with field selections

    public async Task<List<Supplier>> GetSuppliersAsync(CancellationToken ct)
    {
        var result = await queryService.QueryAsync(SupplierQuery, ct: ct);
        // Parse result.ResponseJson into Supplier entities
        return ParseSuppliers(result.ResponseJson);
    }
}
```

## Dependencies

- Quickbase table ID for suppliers (needs to be identified)
- Field mappings from Quickbase supplier table to domain model
- Authorization permissions (already defined as `Downstream.Suppliers.Read/Write`)

## Estimated Effort

Based on existing patterns in the codebase, implementation should take approximately 2-3 days for a basic supplier management feature set.
