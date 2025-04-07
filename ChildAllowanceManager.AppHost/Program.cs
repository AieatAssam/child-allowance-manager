using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<ChildAllowanceManager>("ChildAllowanceManager");
builder.AddPostgres("postgresdb");

await builder.Build().RunAsync();
