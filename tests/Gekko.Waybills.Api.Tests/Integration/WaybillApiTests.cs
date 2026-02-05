using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gekko.Waybills.Domain;
using Gekko.Waybills.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gekko.Waybills.Api.Tests.Integration;

public sealed class WaybillApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public WaybillApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task InvalidStatusTransition_ReturnsBadRequest()
    {
        var tenantId = "TENANT001";
        var waybill = await SeedWaybillAsync(tenantId, WaybillStatus.DELIVERED);

        var client = CustomWebApplicationFactory.CreateClientWithTenant(_factory, tenantId);
        var detail = await GetWaybillAsync(client, waybill.Id);

        var payload = BuildUpdatePayload(detail, status: "PENDING");
        var response = await client.PutAsJsonAsync($"/api/waybills/{waybill.Id}", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("INVALID_STATUS_TRANSITION", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task QuantityValidation_ReturnsBadRequest()
    {
        var tenantId = "TENANT001";
        var waybill = await SeedWaybillAsync(tenantId, WaybillStatus.PENDING);

        var client = CustomWebApplicationFactory.CreateClientWithTenant(_factory, tenantId);
        var detail = await GetWaybillAsync(client, waybill.Id);

        var payload = BuildUpdatePayload(detail, quantity: 0.1m, totalAmount: 1m);
        var response = await client.PutAsJsonAsync($"/api/waybills/{waybill.Id}", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OptimisticConcurrency_ReturnsConflict()
    {
        var tenantId = "TENANT001";
        var waybill = await SeedWaybillAsync(tenantId, WaybillStatus.PENDING);

        var client = CustomWebApplicationFactory.CreateClientWithTenant(_factory, tenantId);
        var detail = await GetWaybillAsync(client, waybill.Id);
        var rowVersion = detail.GetProperty("rowVersionBase64").GetString();

        var payload = BuildUpdatePayload(detail);
        var firstResponse = await client.PutAsJsonAsync($"/api/waybills/{waybill.Id}", payload);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var secondResponse = await client.PutAsJsonAsync($"/api/waybills/{waybill.Id}", payload);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        var body = await secondResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Waybill was modified", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task TenantIsolation_FiltersWaybills()
    {
        var tenant1 = "TENANT001";
        var tenant2 = "TENANT002";

        var wb1 = await SeedWaybillAsync(tenant1, WaybillStatus.PENDING, "WB-T1");
        var wb2 = await SeedWaybillAsync(tenant2, WaybillStatus.PENDING, "WB-T2");

        var client = CustomWebApplicationFactory.CreateClientWithTenant(_factory, tenant1);
        var response = await client.GetFromJsonAsync<JsonElement>("/api/waybills?page=1&pageSize=50");
        var items = response.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("waybillNumber").GetString())
            .ToList();

        Assert.Contains(wb1.WaybillNumber, items);
        Assert.DoesNotContain(wb2.WaybillNumber, items);
    }

    private static object BuildUpdatePayload(JsonElement detail, string? status = null, decimal? quantity = null, decimal? totalAmount = null)
    {
        var quantityValue = quantity ?? detail.GetProperty("quantity").GetDecimal();
        var unitPrice = detail.GetProperty("unitPrice").GetDecimal();
        return new
        {
            deliveryDate = detail.GetProperty("deliveryDate").GetString(),
            productCode = detail.GetProperty("productCode").GetString(),
            quantity = quantityValue,
            unitPrice,
            totalAmount = totalAmount ?? detail.GetProperty("totalAmount").GetDecimal(),
            status = status ?? detail.GetProperty("status").GetString(),
            rowVersionBase64 = detail.GetProperty("rowVersionBase64").GetString()
        };
    }

    private static async Task<JsonElement> GetWaybillAsync(HttpClient client, Guid id)
    {
        var response = await client.GetFromJsonAsync<JsonElement>($"/api/waybills/{id}");
        return response;
    }

    private async Task<Waybill> SeedWaybillAsync(string tenantId, WaybillStatus status, string? waybillNumber = null)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Project-{Guid.NewGuid()}"
        };
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Supplier-{Guid.NewGuid()}"
        };

        var waybill = new Waybill
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WaybillNumber = waybillNumber ?? $"WB-{Guid.NewGuid():N}",
            ProjectId = project.Id,
            SupplierId = supplier.Id,
            WaybillDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            DeliveryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            ProductCode = "P-100",
            Quantity = 2,
            UnitPrice = 10,
            TotalAmount = 20,
            Status = status,
            RowVersion = Guid.NewGuid().ToByteArray()
        };

        dbContext.Projects.Add(project);
        dbContext.Suppliers.Add(supplier);
        dbContext.Waybills.Add(waybill);
        await dbContext.SaveChangesAsync();

        return waybill;
    }

}
