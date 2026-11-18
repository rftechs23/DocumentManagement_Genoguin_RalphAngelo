using DocumentManagement.Application.Services;
using DocumentManagement.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    var defaultInvalidModelStateResponseFactory = options.InvalidModelStateResponseFactory;
    options.InvalidModelStateResponseFactory = context =>
    {
        var requestBodyTooLarge = context.ModelState.Values
            .SelectMany(value => value.Errors)
            .Any(error => error.ErrorMessage.Contains(
                "request body too large",
                StringComparison.OrdinalIgnoreCase));

        if (!requestBodyTooLarge)
        {
            return defaultInvalidModelStateResponseFactory(context);
        }

        return new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status413PayloadTooLarge,
            Title = "Payload too large",
            Detail = "The uploaded file exceeds the maximum allowed size of 10 MiB."
        })
        {
            StatusCode = StatusCodes.Status413PayloadTooLarge
        };
    };
});
builder.Services.AddSwaggerGen(options =>
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Document Management API",
        Version = "v1"
    }));
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddScoped<IDocumentService, DocumentService>();

var app = builder.Build();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Document Management API v1"));
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
