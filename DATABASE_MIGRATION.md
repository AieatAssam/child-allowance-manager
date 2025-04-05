# Database Migration Project

This document outlines the plan for migrating directly from CosmosDB to PostgreSQL, creating a clean and consistent data model while maintaining database provider abstraction for future flexibility.

### Completed Tasks
- [x] Create new `ChildAllowanceManager.Data.PostgreSQL` project for EF Core implementation
- [x] Add required NuGet packages:
  - [x] Microsoft.EntityFrameworkCore
  - [x] Npgsql.EntityFrameworkCore.PostgreSQL
- [x] Create basic DataContext with DbSet properties for entities
- [x] Create AllowanceTransactionSetup with proper column mappings and relationships
- [x] Create ChildConfigurationSetup with proper column mappings and relationships
- [x] Create TenantConfigurationSetup with proper column mappings and relationships
- [x] Update TenantConfiguration model to remove CosmosDB-specific attributes
- [x] Rename configuration files and classes to avoid naming conflicts
- [x] Update User model to remove CosmosDB-specific attributes
- [x] Create UserSetup with proper column mappings and relationships
- [x] Implement many-to-many relationship between Users and Tenants

### In Progress Tasks
- [ ] Complete entity configurations for remaining entities:
  - [x] ChildConfiguration (completed)
  - [x] TenantConfiguration (completed)
  - [x] User (completed)
- [ ] Implement database provider abstraction layer
- [ ] Create generic repository interface `IRepository<T>` with basic CRUD operations
- [ ] Implement PostgreSQL-specific repository

### Future Tasks

#### 1. Setup and Infrastructure
- [x] Create new `ChildAllowanceManager.Data` project for EF Core implementation
- [x] Add required NuGet packages:
  - [x] Microsoft.EntityFrameworkCore
  - [x] Microsoft.EntityFrameworkCore.Design
  - [x] Microsoft.EntityFrameworkCore.Tools
  - [x] Npgsql.EntityFrameworkCore.PostgreSQL
- [ ] Implement `IDbContextFactory<DataContext>` for dependency injection
- [ ] Create PostgreSQL connection configuration
- [ ] Set up database provider abstraction layer

#### 2. Entity Configuration
- [ ] Create clean entity models without CosmosDB-specific attributes:
  - [x] AllowanceTransaction
    - [x] Configure relationships with Child and Tenant
    - [x] Set up proper indexing for queries
  - [x] ChildConfiguration
    - [x] Configure relationships with Tenant and AllowanceTransactions
    - [x] Set up proper indexing for queries
  - [x] TenantConfiguration
    - [x] Configure relationships with Children and AllowanceTransactions
    - [x] Set up proper indexing for queries
  - [x] User
    - [x] Set up proper indexing for queries
    - [x] Configure many-to-many relationship with Tenants
- [ ] Configure PostgreSQL-specific settings:
  - [x] Table names (for AllowanceTransaction)
  - [x] Column types and constraints (for AllowanceTransaction)
  - [x] Indexes and foreign keys (for AllowanceTransaction)
- [x] Implement soft delete functionality (for AllowanceTransaction)
- [x] Configure timestamp tracking (CreatedTimestamp, UpdatedTimestamp) (for AllowanceTransaction)
- [x] Ensure all properties are mapped correctly (for AllowanceTransaction)

#### 3. Repository Pattern Implementation
- [ ] Create generic repository interface `IRepository<T>` with basic CRUD operations
- [ ] Implement database provider abstraction interface
- [ ] Implement PostgreSQL-specific repository
- [ ] Implement pagination support using EF Core's built-in pagination
- [ ] Add bulk operations support using EF Core's bulk operations
- [ ] Implement proper relationship loading strategies
- [ ] Add query helper methods for common operations

#### 4. Data Migration Tool
- [ ] Create separate `ChildAllowanceManager.DataMigration` project
- [ ] Add required NuGet packages:
  - Microsoft.Azure.Cosmos
  - Microsoft.EntityFrameworkCore
  - Npgsql.EntityFrameworkCore.PostgreSQL
  - Microsoft.Extensions.Configuration
  - Microsoft.Extensions.Logging
- [ ] Create CosmosDB data reader
  - [ ] Implement connection to CosmosDB
  - [ ] Create data extraction logic for each entity
  - [ ] Handle pagination for large datasets
- [ ] Create PostgreSQL data writer
  - [ ] Implement connection to PostgreSQL
  - [ ] Create data insertion logic for each entity
  - [ ] Handle transaction management
- [ ] Implement data validation
  - [ ] Verify data integrity after migration
  - [ ] Check for missing or corrupted data
- [ ] Add rollback capability
- [ ] Create migration documentation
- [ ] Implement progress tracking and reporting
- [ ] Create command-line interface for migration tool

#### 5. Service Layer Updates
- [ ] Update service interfaces to use new repository pattern
- [ ] Modify services to work with EF Core:
  - [ ] TransactionService
    - [ ] Update query patterns to use EF Core navigation properties
    - [ ] Optimize relationship loading
  - [ ] ChildService
    - [ ] Update query patterns to use EF Core navigation properties
    - [ ] Optimize relationship loading
  - [ ] TenantService
    - [ ] Update query patterns to use EF Core navigation properties
    - [ ] Optimize relationship loading
  - [ ] UserService
    - [ ] Update query patterns to use EF Core navigation properties
    - [ ] Optimize relationship loading
- [ ] Update dependency injection in Program.cs
- [ ] Ensure all existing service behaviors are maintained

#### 6. Testing
- [ ] Unit tests for new repository implementation
- [ ] Integration tests for PostgreSQL operations
- [ ] Migration validation tests
- [ ] Performance testing
- [ ] Relationship loading tests

#### 7. .NET Aspire Integration
- [ ] Create new `ChildAllowanceManager.Aspire` project
- [ ] Add required NuGet packages:
  - [ ] Aspire.Hosting
  - [ ] Aspire.Components.Common
  - [ ] Aspire.Components.ServiceDefaults
  - [ ] Aspire.Components.OpenTelemetry
  - [ ] Aspire.Components.HealthChecks
  - [ ] Aspire.Components.Caching
- [ ] Configure Aspire host builder:
  - [ ] Set up service defaults
  - [ ] Configure OpenTelemetry for distributed tracing
  - [ ] Set up health checks
  - [ ] Configure metrics collection
  - [ ] Implement distributed caching
- [ ] Implement service resilience patterns:
  - [ ] Circuit breaker pattern
  - [ ] Retry policies
  - [ ] Timeout policies
  - [ ] Rate limiting
- [ ] Configure environment variables:
  - [ ] Development environment settings
  - [ ] Production environment settings
  - [ ] Secrets management
- [ ] Set up container orchestration:
  - [ ] Configure container networking
  - [ ] Set up service discovery
  - [ ] Configure load balancing
  - [ ] Implement health-based routing
- [ ] Implement cloud-native features:
  - [ ] Configure service-to-service communication
  - [ ] Set up distributed logging
  - [ ] Implement distributed tracing
  - [ ] Configure metrics and dashboards
- [ ] Create Aspire dashboard:
  - [ ] Configure service visualization
  - [ ] Set up metrics visualization
  - [ ] Configure log aggregation
  - [ ] Implement distributed tracing visualization

#### 8. Container Deployment
- [ ] Create Dockerfile for API project
- [ ] Create Dockerfile for PostgreSQL database
- [ ] Create docker-compose.yml file with:
  - [ ] API service configuration
  - [ ] PostgreSQL service configuration
  - [ ] Volume mappings for data persistence
  - [ ] Network configuration
  - [ ] Environment variables
- [ ] Create deployment documentation
- [ ] Implement container health checks
- [ ] Set up container logging
- [ ] Configure container resource limits
- [ ] Create deployment scripts
- [ ] Document scaling strategies

## Implementation Plan

### Architecture Decisions
1. Use simple repository pattern with basic CRUD operations
2. Create clean entity models without CosmosDB-specific elements
3. Use EF Core migrations for schema management
4. Implement soft delete at the repository level
5. Use async/await throughout the codebase
6. Use proper navigation properties for relationships
7. Implement efficient relationship loading strategies
8. Maintain database provider abstraction for future flexibility
9. Create separate project for data migration tool
10. Implement .NET Aspire for cloud-native application development
11. Use Docker Compose for container orchestration

### Data Flow
1. Application layer → Repository Interface → Database Provider Abstraction → Concrete Repository → DbContext → Database
2. Migration: CosmosDB → Data Migration Tool → PostgreSQL
3. Containerized deployment with Docker Compose

### Technical Components
1. Clean entity configurations with proper relationships
2. Generic repository interface with basic CRUD operations
3. Database provider abstraction layer
4. PostgreSQL-specific DbContext implementation
5. Separate data migration tool project for CosmosDB to PostgreSQL migration
6. .NET Aspire project for cloud-native features
7. Docker Compose configuration for containerized deployment

### Environment Configuration
1. Database connection strings
2. Provider selection
3. Migration settings
4. Performance tuning parameters
5. Container environment variables
6. Aspire service configuration

## Relevant Files
- `ChildAllowanceManager.Common/Models/*` - Entity models to be migrated
- `ChildAllowanceManager/Services/*` - Services to be updated
- `ChildAllowanceManager/Program.cs` - Dependency injection configuration
- `ChildAllowanceManager.Data.PostgreSQL/DataContext.cs` - Existing PostgreSQL context (reference)
- `ChildAllowanceManager.DataMigration/*` - New project for data migration tool
- `ChildAllowanceManager.Aspire/*` - New project for .NET Aspire integration
- `Dockerfile` - Container definition for API
- `docker-compose.yml` - Container orchestration configuration

## Entity Relationships
1. AllowanceTransaction
   - Child (many-to-one)
   - Tenant (many-to-one)
2. ChildConfiguration
   - Tenant (many-to-one)
   - AllowanceTransactions (one-to-many)
3. TenantConfiguration
   - Children (one-to-many)
   - AllowanceTransactions (one-to-many)
   - Users (many-to-many through UserTenant)
4. User
   - Tenants (many-to-many through UserTenant)

## Docker Compose Deployment Instructions

### Prerequisites
- Docker Desktop installed
- .NET 8 SDK installed
- Git for cloning the repository

### Deployment Steps

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/ChildAllowanceManager.git
   cd ChildAllowanceManager
   ```

2. Build the Docker images:
   ```bash
   docker-compose build
   ```

3. Start the services:
   ```bash
   docker-compose up -d
   ```

4. Access the application:
   - API: http://localhost:5000
   - Swagger UI: http://localhost:5000/swagger
   - Aspire Dashboard: http://localhost:8080

5. Monitor the services:
   ```bash
   docker-compose logs -f
   ```

6. Stop the services:
   ```bash
   docker-compose down
   ```

### Environment Configuration

The application uses environment variables for configuration. These can be set in the `docker-compose.yml` file or in a `.env` file in the project root.

Key environment variables:
- `ConnectionStrings__DefaultConnection`: PostgreSQL connection string
- `ASPNETCORE_ENVIRONMENT`: Application environment (Development, Staging, Production)
- `ASPNETCORE_URLS`: URLs the application listens on

### Scaling

To scale the API service:
```bash
docker-compose up -d --scale api=3
```

This will start 3 instances of the API service behind a load balancer. 