using HRMS.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy
                .WithOrigins("http://localhost:3000")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (!int.TryParse(principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId))
                { context.Fail("Invalid account."); return; }
                var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var user = await db.Users.AsNoTracking().Where(u => u.UserId == userId)
                    .Select(u => new { u.IsActive, u.SessionVersion, u.MustChangePassword }).FirstOrDefaultAsync();
                if (user == null || !user.IsActive || principal?.FindFirst("session_version")?.Value != user.SessionVersion.ToString())
                { context.Fail("Session expired. Please sign in again."); return; }
                context.HttpContext.Items["MustChangePassword"] = user.MustChangePassword;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseHttpsRedirection();

app.UseCors("AllowReactApp");

app.UseAuthentication();

app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true && context.Items["MustChangePassword"] is true &&
        !(HttpMethods.IsPost(context.Request.Method) && string.Equals(context.Request.Path.Value?.TrimEnd('/'), "/api/Auth/change-password", StringComparison.OrdinalIgnoreCase)) &&
        !(HttpMethods.IsGet(context.Request.Method) && string.Equals(context.Request.Path.Value?.TrimEnd('/'), "/api/Auth/session", StringComparison.OrdinalIgnoreCase)))
    {
        context.Response.StatusCode = 403;
        await context.Response.WriteAsJsonAsync(new { code = "PASSWORD_CHANGE_REQUIRED", message = "Change your temporary password before continuing." });
        return;
    }
    await next();
});

app.UseAuthorization();

app.MapControllers();

app.Run();
