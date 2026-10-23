IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.PersonalFinance_ApiService>("apiservice");

builder.AddProject<Projects.PersonalFinance_Web>("web")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
