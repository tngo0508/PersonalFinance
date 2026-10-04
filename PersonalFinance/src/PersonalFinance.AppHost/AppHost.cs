var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.PersonalFinance_ApiService>("apiservice");

builder.AddProject<Projects.PersonalFinance_Web>("web")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
