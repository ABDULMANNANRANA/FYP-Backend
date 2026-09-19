using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TODOLISTAPI.Models;

var builder = WebApplication.CreateBuilder(args);

// =====================================================
// KESTREL - LISTEN ON ALL NETWORK INTERFACES
// =====================================================
// IMPORTANT:
// 0.0.0.0 means the API accepts connections from:
// localhost, LAN IP, phone, etc.
builder.WebHost.UseUrls("http://0.0.0.0:5000");

// =====================================================
// CONTROLLERS
// =====================================================
builder.Services.AddControllers();

// =====================================================
// DATABASE
// =====================================================
builder.Services.AddDbContext<TodoSmartAlertsContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// =====================================================
// JWT AUTHENTICATION
// =====================================================
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "JWT Key is missing from appsettings.json"
    );
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            ),

            ClockSkew = TimeSpan.Zero
        };
    });

// =====================================================
// AUTHORIZATION
// =====================================================
builder.Services.AddAuthorization();

// =====================================================
// CORS
// =====================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// =====================================================
// SWAGGER
// =====================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// =====================================================
// BUILD APP
// =====================================================
var app = builder.Build();

// =====================================================
// SWAGGER
// =====================================================
app.UseSwagger();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(
        "/swagger/v1/swagger.json",
        "TODOLISTAPI v1"
    );

    options.RoutePrefix = "swagger";
});

// =====================================================
// CORS
// =====================================================
app.UseCors("AllowAll");

// =====================================================
// AUTHENTICATION
// =====================================================
app.UseAuthentication();

// =====================================================
// AUTHORIZATION
// =====================================================
app.UseAuthorization();

// =====================================================
// CONTROLLERS
// =====================================================
app.MapControllers();

// =====================================================
// START API
// =====================================================
app.Run();















//var builder = WebApplication.CreateBuilder(args);

//// Add services to the container.

//builder.Services.AddControllers();
//// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
//builder.Services.AddOpenApi();

//var app = builder.Build();

//// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.MapOpenApi();
//}

//app.UseHttpsRedirection();

//app.UseAuthorization();

//app.MapControllers();

//app.Run();
