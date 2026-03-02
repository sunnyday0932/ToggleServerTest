using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ToggleServer.Core.Interfaces;
using ToggleServer.Core.Models;

namespace ToggleServer.Api.Endpoints;

public static class ToggleEndpoints
{
    public static void MapToggleEndpoints(this IEndpointRouteBuilder routes)
    {
        // ==========================================
        // Client API
        // ==========================================
        var clientApi = routes.MapGroup("/api/v1/client/toggles")
            .WithTags("Client API");

        clientApi.MapGet("/", async (IToggleService service, CancellationToken ct) => 
        {
            var toggles = await service.GetAllTogglesAsync(ct);
            return Results.Ok(toggles);
        });

        // ==========================================
        // Management API
        // ==========================================
        var managementApi = routes.MapGroup("/api/v1/management/toggles")
            .WithTags("Management API");
            // 暫時註解，方便本地直接使用 Swagger 測試
            // .RequireAuthorization();

        managementApi.MapGet("/", async (IToggleService service, CancellationToken ct) => 
        {
            var toggles = await service.GetAllTogglesAsync(ct);
            return Results.Ok(toggles);
        });

        managementApi.MapGet("/{key}", async (string key, IToggleService service, CancellationToken ct) => 
        {
            var toggle = await service.GetToggleAsync(key, ct);
            return toggle is not null ? Results.Ok(toggle) : Results.NotFound();
        });

        managementApi.MapPost("/", async (
            [FromBody] FeatureToggle toggle, 
            IToggleService service, 
            IValidator<FeatureToggle> validator,
            HttpContext context,
            CancellationToken ct) => 
        {
            var validationResult = await validator.ValidateAsync(toggle, ct);
            if (!validationResult.IsValid) return Results.ValidationProblem(validationResult.ToDictionary());

            var operatorId = context.User.Identity?.Name ?? "system"; // Simplified for mock auth
            
            try
            {
                var result = await service.CreateToggleAsync(toggle, operatorId, "Operator Name", ct);
                return Results.Created($"/api/v1/management/toggles/{result.Key}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(ex.Message);
            }
        });

        managementApi.MapPut("/{key}", async (
            string key, 
            [FromBody] FeatureToggle toggle, 
            IToggleService service, 
            IValidator<FeatureToggle> validator, // Should ideally use UpdateToggleRequestValidator here via DI trick or manual instantiation
            HttpContext context,
            CancellationToken ct) => 
        {
            if (key != toggle.Key) return Results.BadRequest("Key in route does not match key in body.");
            
            var validationResult = await validator.ValidateAsync(toggle, ct);
            if (!validationResult.IsValid) return Results.ValidationProblem(validationResult.ToDictionary());
            
            if (toggle.Version <= 0) return Results.BadRequest("Version is required for update.");

            var operatorId = context.User.Identity?.Name ?? "system";
            
            try
            {
                var result = await service.UpdateToggleAsync(toggle, operatorId, "Operator Name", ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(ex.Message); // 409 Conflict for Optimistic Locking
            }
        });

        managementApi.MapPost("/{key}/disable", async (string key, IToggleService service, HttpContext context, CancellationToken ct) => 
        {
            var operatorId = context.User.Identity?.Name ?? "system";
            try
            {
                var result = await service.KillSwitchAsync(key, operatorId, "Operator Name", ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(ex.Message);
            }
        });

        managementApi.MapGet("/{key}/audit-logs", async (string key, IToggleService service, CancellationToken ct) => 
        {
            var logs = await service.GetAuditLogsAsync(key, ct);
            return Results.Ok(logs);
        });

        managementApi.MapPost("/{key}/rollback/{version:int}", async (string key, int version, IToggleService service, HttpContext context, CancellationToken ct) => 
        {
            var operatorId = context.User.Identity?.Name ?? "system";
            try
            {
                var result = await service.RollbackAsync(key, version, operatorId, "Operator Name", ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message); // NotFound or BadRequest depending on nuance
            }
            catch (InvalidOperationException ex)
            {
                 return Results.Conflict(ex.Message);
            }
        });
    }
}
