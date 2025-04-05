# Child Allowance Manager

A comprehensive application for managing child allowances, built with .NET 9, Entity Framework Core, and PostgreSQL.

## Overview

Child Allowance Manager is a web application that helps parents manage allowances for their children. It provides features for tracking transactions, managing child configurations, and supports multiple families as completely independent tenants.

## Features

- **Allowance Transaction Management**: Track and manage allowance transactions
- **Child Configuration**: Configure allowance settings for each child
- **Multi-tenant Support**: Manage multiple families/households with separate configurations
- **User Management**: Handle user authentication and authorization
- **Cloud-Native Architecture**: Built with .NET Aspire for cloud-native deployment
- **Containerized Deployment**: Easy deployment with Docker Compose
- **Modern UI**: Built with MudBlazor and Blazor Server with interactive mode

## Technology Stack

- **.NET 9**: Modern, cross-platform framework for building applications
- **Entity Framework Core**: ORM for database access
- **PostgreSQL**: Relational database for data storage
- **.NET Aspire**: Cloud-native application development and orchestration
- **MudBlazor**: Material Design component library for Blazor
- **Blazor Server**: Server-side rendering with interactive mode
- **Docker**: Containerization for consistent deployment
- **Docker Compose**: Container orchestration for local development

## Project Structure

```
ChildAllowanceManager/
├── ChildAllowanceManager.Common/           # Common models and interfaces
│   └── Models/                             # Entity models
├── ChildAllowanceManager.Data.PostgreSQL/  # PostgreSQL data access
│   └── Configurations/                     # Entity configurations
├── ChildAllowanceManager/                  # Main API project
│   ├── Controllers/                        # API controllers
│   ├── Services/                           # Business logic services
│   └── Program.cs                          # Application entry point
└── ChildAllowanceManager.Aspire/           # .NET Aspire project
```

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Git](https://git-scm.com/downloads)
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) or [Visual Studio Code](https://code.visualstudio.com/)

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/AieatAssam/child-allowance-manager.git
   cd child-allowance-manager
   ```

2. Restore NuGet packages:
   ```bash
   dotnet restore
   ```

3. Build the solution:
   ```bash
   dotnet build
   ```

### Running the Application

#### Using .NET Aspire

The application is designed to run with .NET Aspire, which orchestrates all components:

1. Run the Aspire project:
   ```bash
   cd ChildAllowanceManager.Aspire
   dotnet run
   ```

2. Access the application:
   - Aspire Dashboard: http://localhost:8080
   - API: http://localhost:5000
   - Swagger UI: http://localhost:5000/swagger
   - Blazor UI: http://localhost:5001

The Aspire dashboard provides a comprehensive view of all services, including:
- Service health and status
- Metrics and telemetry
- Logs and traces
- Resource utilization

#### Using Docker Compose (Alternative)

If you prefer to run the application using Docker Compose:

1. Build the Docker images:
   ```bash
   docker-compose build
   ```

2. Start the services:
   ```bash
   docker-compose up -d
   ```

3. Access the application:
   - API: http://localhost:5000
   - Swagger UI: http://localhost:5000/swagger
   - Blazor UI: http://localhost:5001

4. Monitor the services:
   ```bash
   docker-compose logs -f
   ```

5. Stop the services:
   ```bash
   docker-compose down
   ```

## Development

### Adding a New Entity

1. Create a model in `ChildAllowanceManager.Common/Models/`
2. Create a configuration in `ChildAllowanceManager.Data.PostgreSQL/Configurations/`
3. Add a DbSet to `DataContext.cs`
4. Apply the configuration in `OnModelCreating`
5. Create a migration:
   ```bash
   dotnet ef migrations add AddNewEntity --project ChildAllowanceManager.Data.PostgreSQL --startup-project ChildAllowanceManager
   ```
6. Update the database:
   ```bash
   dotnet ef database update --project ChildAllowanceManager.Data.PostgreSQL --startup-project ChildAllowanceManager
   ```

### Adding a New Service

1. Create an interface in `ChildAllowanceManager.Common/Services/`
2. Implement the service in `ChildAllowanceManager/Services/`
3. Register the service in `Program.cs`:
   ```csharp
   builder.Services.AddScoped<IYourService, YourService>();
   ```

### Adding a New Controller

1. Create a controller in `ChildAllowanceManager/Controllers/`
2. Inject the required services
3. Implement the API endpoints

### UI Development with MudBlazor

The application uses MudBlazor for its UI components. To add new UI elements:

1. Use MudBlazor components in your Blazor pages:
   ```csharp
   <MudButton Variant="Variant.Filled" Color="Color.Primary">Click Me</MudButton>
   ```

2. For more complex UI elements, refer to the [MudBlazor documentation](https://mudblazor.com/docs/overview)

3. For interactive UI development with Blazor Server:
   ```csharp
   @page "/interactive"
   @using MudBlazor
   
   <MudContainer>
       <MudText Typo="Typo.h4">Interactive Example</MudText>
       <MudTextField @bind-Value="text" Label="Enter Text" />
       <MudButton OnClick="ShowMessage">Show Message</MudButton>
   </MudContainer>
   
   @code {
       private string text = "";
       
       private void ShowMessage()
       {
           // Server-side code execution with SignalR connection
       }
   }
   ```

## Testing

### Running Tests

```bash
dotnet test
```

### Adding Tests

1. Create a test class in the appropriate test project
2. Write unit tests for your code
3. Run the tests to ensure they pass

## Deployment

### Docker Compose Deployment

1. Build the Docker images:
   ```bash
   docker-compose build
   ```

2. Start the services:
   ```bash
   docker-compose up -d
   ```

### Scaling

To scale the API service:
```bash
docker-compose up -d --scale api=3
```

This will start 3 instances of the API service behind a load balancer.

## Environment Configuration

The application uses environment variables for configuration. These can be set in the `docker-compose.yml` file or in a `.env` file in the project root.

Key environment variables:
- `ConnectionStrings__DefaultConnection`: PostgreSQL connection string
- `ASPNETCORE_ENVIRONMENT`: Application environment (Development, Staging, Production)
- `ASPNETCORE_URLS`: URLs the application listens on

## Contributing

1. Fork the repository
2. Create a feature branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. Commit your changes:
   ```bash
   git commit -m 'Add some feature'
   ```
4. Push to the branch:
   ```bash
   git push origin feature/your-feature-name
   ```
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- [PostgreSQL](https://www.postgresql.org/)
- [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [MudBlazor](https://mudblazor.com/)
- [Blazor Server](https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models?view=aspnetcore-9.0&pivots=server)
- [Docker](https://www.docker.com/)